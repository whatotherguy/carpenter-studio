using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Pipeline.Parts;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class PartGenerationStage : IResolutionStage
{
    private readonly IDesignStateStore _stateStore;
    private readonly IAppLogger? _logger;

    public PartGenerationStage()
        : this(new InMemoryDesignStateStore())
    {
    }

    public PartGenerationStage(IDesignStateStore stateStore, IAppLogger? logger = null)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _logger = logger;
    }

    public int StageNumber => 5;

    public string StageName => "Part Generation";

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.SpatialResult.Placements.Count == 0)
        {
            return Fail("PART_GEN_EMPTY", "Part generation requires at least one resolved cabinet placement.");
        }

        var orderedPlacements = context.SpatialResult.Placements
            .OrderBy(placement => placement.RunId.Value)
            .ThenBy(placement => placement.CabinetId.Value)
            .ToArray();
        var cabinetLabelPrefixes = BuildCabinetLabelPrefixes(orderedPlacements);
        var parts = new List<GeneratedPart>();
        var issues = new List<ValidationIssue>();

        foreach (var placement in orderedPlacements)
        {
            var cabinet = _stateStore.GetCabinet(placement.CabinetId);
            if (cabinet is null)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "PART_GEN_CABINET_NOT_FOUND",
                    $"Cabinet '{placement.CabinetId}' could not be resolved for part generation.",
                    [placement.CabinetId.ToString()]));
                continue;
            }

            IReadOnlyList<PartGeometrySpec> specs;
            try
            {
                specs = PartGeometry.BuildParts(cabinet);
            }
            catch (NotSupportedException exception)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "PART_GEN_UNSUPPORTED_CABINET",
                    $"Cabinet '{cabinet.CabinetTypeId}' cannot be generated: {exception.Message}",
                    [cabinet.CabinetId.ToString()]));
                continue;
            }

            var thickness = PartGeometry.ResolveThickness(cabinet);
            var perTypeOrdinals = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var spec in specs)
            {
                var ordinal = perTypeOrdinals.TryGetValue(spec.PartType, out var existingOrdinal)
                    ? existingOrdinal + 1
                    : 1;
                perTypeOrdinals[spec.PartType] = ordinal;

                var labelPrefix = cabinetLabelPrefixes[cabinet.CabinetId];
                parts.Add(new GeneratedPart
                {
                    PartId = $"part:{cabinet.CabinetId.Value:D}:{spec.PartType}:{ordinal}",
                    CabinetId = cabinet.CabinetId,
                    PartType = spec.PartType,
                    Width = spec.Width,
                    Height = spec.Height,
                    MaterialThickness = thickness,
                    MaterialId = default,
                    GrainDirection = spec.GrainDirection,
                    Edges = spec.Edges,
                    Label = BuildPartLabel(labelPrefix, spec.PartType, ordinal)
                });
            }
        }

        if (issues.Count > 0)
        {
            return StageResult.Failed(StageNumber, issues);
        }

        var orderedParts = parts
            .OrderBy(part => part.CabinetId.Value)
            .ThenBy(part => part.PartType, StringComparer.Ordinal)
            .ThenBy(part => part.PartId, StringComparer.Ordinal)
            .ToArray();

        if (orderedParts.Length == 0)
        {
            return Fail("PART_GEN_EMPTY", "Part generation completed without producing any parts.");
        }

        context.PartResult = new PartGenerationResult
        {
            Parts = orderedParts
        };

        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Debug,
            Category = "PartGenerationStage",
            Message = $"Generated {orderedParts.Length} parts from {orderedPlacements.Length} cabinet placements.",
            Timestamp = DateTimeOffset.UtcNow,
            StageNumber = StageNumber.ToString()
        });

        return StageResult.Succeeded(StageNumber);
    }

    private StageResult Fail(string code, string message)
    {
        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Warning,
            Category = "PartGenerationStage",
            Message = message,
            Timestamp = DateTimeOffset.UtcNow,
            StageNumber = StageNumber.ToString()
        });

        return StageResult.Failed(StageNumber, [new ValidationIssue(ValidationSeverity.Error, code, message)]);
    }

    private static string BuildPartLabel(string cabinetLabelPrefix, string partType, int ordinal) =>
        ordinal == 1
            ? $"{cabinetLabelPrefix}-{partType}"
            : $"{cabinetLabelPrefix}-{partType}-{ordinal}";

    private IReadOnlyDictionary<CabinetDesigner.Domain.Identifiers.CabinetId, string> BuildCabinetLabelPrefixes(
        IReadOnlyList<RunPlacement> placements)
    {
        var prefixes = new Dictionary<CabinetDesigner.Domain.Identifiers.CabinetId, string>();
        var cabinets = placements
            .Select(placement => _stateStore.GetCabinet(placement.CabinetId))
            .Where(cabinet => cabinet is not null)
            .Select(cabinet => cabinet!)
            .OrderBy(cabinet => cabinet.CabinetTypeId, StringComparer.Ordinal)
            .ThenBy(cabinet => cabinet.CabinetId.Value)
            .ToArray();

        foreach (var group in cabinets.GroupBy(cabinet => cabinet.CabinetTypeId, StringComparer.Ordinal))
        {
            var instance = 0;
            var requiresInstanceSuffix = group.Count() > 1;
            foreach (var cabinet in group)
            {
                instance++;
                prefixes[cabinet.CabinetId] = requiresInstanceSuffix
                    ? $"{cabinet.CabinetTypeId}-{instance}"
                    : cabinet.CabinetTypeId;
            }
        }

        return prefixes;
    }
}
