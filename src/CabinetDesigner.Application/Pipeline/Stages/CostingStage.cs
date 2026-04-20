using System.Security.Cryptography;
using System.Text;
using CabinetDesigner.Application.Costing;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.InstallContext;
using CabinetDesigner.Domain.ManufacturingContext;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class CostingStage : IResolutionStage
{
    private const string MissingPriceCode = "COSTING_PRICE_MISSING";
    private const string NoPartsCode = "COSTING_NO_PARTS";

    private readonly ICatalogService _catalog;
    private readonly ICostingPolicy _policy;
    private readonly IPreviousApprovedCostLookup _previousCostLookup;
    private readonly IAppLogger? _logger;

    public CostingStage()
        : this(new CatalogService(), new DefaultCostingPolicy())
    {
    }

    public CostingStage(
        ICatalogService catalog,
        ICostingPolicy policy,
        IAppLogger? logger = null,
        IPreviousApprovedCostLookup? previousCostLookup = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _logger = logger;
        _previousCostLookup = previousCostLookup ?? new NullPreviousApprovedCostLookup();
    }

    public int StageNumber => 9;

    public string StageName => "Costing";

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.PartResult.Parts.Count == 0 || context.ManufacturingResult.Plan.CutList.Count == 0)
        {
            return FailClosed(
                NoPartsCode,
                "Cannot calculate cost because no manufacturing parts were produced.",
                []);
        }

        var cutList = context.ManufacturingResult.Plan.CutList
            .OrderBy(item => item.CabinetId.Value)
            .ThenBy(item => item.PartType, StringComparer.Ordinal)
            .ThenBy(item => item.PartId, StringComparer.Ordinal)
            .ToArray();
        var cutListByPartId = cutList.ToDictionary(item => item.PartId, StringComparer.Ordinal);
        var cabinetIds = context.SpatialResult.Placements
            .Select(placement => placement.CabinetId)
            .Distinct()
            .OrderBy(id => id.Value)
            .ToArray();
        var cabinetBreakdowns = cabinetIds.ToDictionary(
            cabinetId => cabinetId,
            cabinetId => new CabinetAccumulator(),
            EqualityComparer<CabinetId>.Default);
        var runCabinetIds = context.SpatialResult.Placements
            .GroupBy(placement => placement.RunId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(placement => placement.CabinetId)
                    .Distinct()
                    .OrderBy(id => id.Value)
                    .ToArray());
        var issues = new List<ValidationIssue>();

        var materialCost = 0m;
        foreach (var item in cutList)
        {
            var price = _catalog.GetMaterialPricePerSquareFoot(item.MaterialId, item.MaterialThickness);
            if (price <= 0m)
            {
                issues.Add(CreateIssue(
                    $"Material price is missing for part '{item.Label}'.",
                    [item.PartId, item.CabinetId.ToString()]));
                continue;
            }

            var lineCost = CalculateAreaSquareFeet(item.CutWidth, item.CutHeight) * price;
            materialCost += lineCost;
            cabinetBreakdowns[item.CabinetId].MaterialCost += lineCost;
        }

        var openingLookup = BuildOpeningCabinetLookup(cabinetIds);
        var hardwareCost = 0m;
        foreach (var assignment in context.ConstraintResult.HardwareAssignments
                     .OrderBy(assignment => assignment.OpeningId.Value))
        {
            var cabinetId = openingLookup.TryGetValue(assignment.OpeningId, out var resolvedCabinetId)
                ? resolvedCabinetId
                : (CabinetId?)null;

            foreach (var hardwareId in assignment.HardwareIds.OrderBy(id => id.Value))
            {
                var price = _catalog.GetHardwarePrice(hardwareId);
                if (price <= 0m)
                {
                    var affected = cabinetId is { } knownCabinetId
                        ? new[] { assignment.OpeningId.ToString(), knownCabinetId.ToString(), hardwareId.ToString() }
                        : new[] { assignment.OpeningId.ToString(), hardwareId.ToString() };
                    issues.Add(CreateIssue(
                        $"Hardware price is missing for opening '{assignment.OpeningId}'.",
                        affected));
                    continue;
                }

                hardwareCost += price;
                if (cabinetId is { } hardwareCabinetId && cabinetBreakdowns.TryGetValue(hardwareCabinetId, out var accumulator))
                {
                    accumulator.HardwareCost += price;
                }
            }
        }

        var laborCost = 0m;
        foreach (var operation in context.ManufacturingResult.Plan.Operations
                     .OrderBy(operation => operation.PartId, StringComparer.Ordinal)
                     .ThenBy(operation => operation.Sequence))
        {
            var rate = _policy.GetLaborRate(operation.Kind);
            laborCost += rate;

            if (cutListByPartId.TryGetValue(operation.PartId, out var item) &&
                cabinetBreakdowns.TryGetValue(item.CabinetId, out var accumulator))
            {
                accumulator.LaborCost += rate;
            }
        }

        var installCost = 0m;
        foreach (var step in context.InstallResult.Plan.Steps
                     .OrderBy(step => step.Order)
                     .ThenBy(step => step.StepKey, StringComparer.Ordinal))
        {
            var stepCost = _policy.InstallRatePerStep;
            installCost += stepCost;

            if (step.CabinetId is { } stepCabinetId && cabinetBreakdowns.TryGetValue(stepCabinetId, out var accumulator))
            {
                accumulator.InstallCost += stepCost;
                continue;
            }

            if (!runCabinetIds.TryGetValue(step.RunId, out var stepCabinetIds) || stepCabinetIds.Length == 0)
            {
                continue;
            }

            var apportioned = stepCost / stepCabinetIds.Length;
            foreach (var cabinetId in stepCabinetIds)
            {
                cabinetBreakdowns[cabinetId].InstallCost += apportioned;
            }
        }

        if (issues.Count > 0)
        {
            return StageResult.Failed(
                StageNumber,
                issues
                    .OrderBy(issue => issue.Message, StringComparer.Ordinal)
                    .ThenBy(issue => issue.AffectedEntityIds is { Count: > 0 } ? issue.AffectedEntityIds[0] : string.Empty, StringComparer.Ordinal)
                    .ToArray());
        }

        var subtotal = materialCost + hardwareCost + laborCost + installCost;
        var markup = RoundCurrency(subtotal * _policy.MarkupFraction);
        var tax = RoundCurrency((subtotal + markup) * _policy.TaxFraction);
        var total = RoundCurrency(subtotal + markup + tax);

        context.CostingResult = new CostingResult
        {
            MaterialCost = RoundCurrency(materialCost),
            HardwareCost = RoundCurrency(hardwareCost),
            LaborCost = RoundCurrency(laborCost),
            InstallCost = RoundCurrency(installCost),
            Subtotal = RoundCurrency(subtotal),
            Markup = markup,
            Tax = tax,
            Total = total,
            RevisionDelta = BuildRevisionDelta(total),
            CabinetBreakdowns = cabinetBreakdowns
                .OrderBy(pair => pair.Key.Value)
                .Select(pair => new CabinetCostBreakdown(
                    pair.Key.ToString(),
                    RoundCurrency(pair.Value.MaterialCost),
                    RoundCurrency(pair.Value.HardwareCost),
                    RoundCurrency(pair.Value.LaborCost),
                    RoundCurrency(pair.Value.InstallCost)))
                .ToArray()
        };

        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Debug,
            Category = "CostingStage",
            Message = $"Calculated costing for {cutList.Length} cut-list parts across {cabinetBreakdowns.Count} cabinets.",
            Timestamp = DateTimeOffset.UtcNow,
            StageNumber = StageNumber.ToString()
        });

        return StageResult.Succeeded(StageNumber);
    }

    private StageResult FailClosed(string code, string message, IReadOnlyList<string> affectedEntityIds)
    {
        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Warning,
            Category = "CostingStage",
            Message = message,
            Timestamp = DateTimeOffset.UtcNow,
            StageNumber = StageNumber.ToString()
        });

        return StageResult.Failed(
            StageNumber,
            [new ValidationIssue(ValidationSeverity.Error, code, message, affectedEntityIds)]);
    }

    private static ValidationIssue CreateIssue(string message, IReadOnlyList<string> affectedEntityIds) =>
        new(ValidationSeverity.Error, MissingPriceCode, message, affectedEntityIds);

    private static decimal CalculateAreaSquareFeet(Domain.Geometry.Length width, Domain.Geometry.Length height) =>
        (width.Inches * height.Inches) / 144m;

    private static decimal RoundCurrency(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.ToEven);

    private CostDelta? BuildRevisionDelta(decimal currentTotal)
    {
        var previousTotal = _previousCostLookup.GetMostRecentApprovedTotal();
        if (previousTotal is not { } previous)
        {
            return null;
        }

        var rounded = RoundCurrency(previous);
        var difference = RoundCurrency(currentTotal - rounded);
        var summary = difference switch
        {
            > 0m => $"Total increased by {difference:C} from prior approved revision.",
            < 0m => $"Total decreased by {Math.Abs(difference):C} from prior approved revision.",
            _ => "Total unchanged from prior approved revision."
        };

        return new CostDelta(rounded, currentTotal, difference, summary);
    }

    private static IReadOnlyDictionary<OpeningId, CabinetId> BuildOpeningCabinetLookup(IEnumerable<CabinetId> cabinetIds)
    {
        var lookup = new Dictionary<OpeningId, CabinetId>();

        foreach (var cabinetId in cabinetIds.OrderBy(id => id.Value))
        {
            for (var ordinal = 0; ordinal < 8; ordinal++)
            {
                lookup[CreateStableOpeningId(cabinetId, ordinal)] = cabinetId;
            }
        }

        return lookup;
    }

    private static OpeningId CreateStableOpeningId(CabinetId cabinetId, int ordinal)
    {
        var bytes = Encoding.UTF8.GetBytes($"opening:{cabinetId.Value:D}:{ordinal}");
        var hashBytes = SHA256.HashData(bytes);
        return new OpeningId(new Guid(hashBytes.AsSpan(0, 16)));
    }

    private sealed class CabinetAccumulator
    {
        public decimal MaterialCost { get; set; }

        public decimal HardwareCost { get; set; }

        public decimal LaborCost { get; set; }

        public decimal InstallCost { get; set; }
    }
}
