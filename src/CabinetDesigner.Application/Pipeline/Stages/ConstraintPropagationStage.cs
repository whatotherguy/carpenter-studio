using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Pipeline.Parts;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.MaterialContext;
using System.Security.Cryptography;
using System.Text;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class ConstraintPropagationStage : IResolutionStage
{
    private static readonly IReadOnlyDictionary<string, string[]> MaterialOverrideKeys =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["LeftSide"] = ["material.LeftSide", "material.Case", "material.All"],
            ["RightSide"] = ["material.RightSide", "material.Case", "material.All"],
            ["Top"] = ["material.Top", "material.Case", "material.All"],
            ["Bottom"] = ["material.Bottom", "material.Case", "material.All"],
            ["Back"] = ["material.Back", "material.All"],
            ["AdjustableShelf"] = ["material.AdjustableShelf", "material.Shelf", "material.All"],
            ["ToeKick"] = ["material.ToeKick", "material.Case", "material.All"],
            ["StructuralBase"] = ["material.StructuralBase", "material.Case", "material.All"],
            ["FrameStile"] = ["material.FrameStile", "material.FaceFrame", "material.All"],
            ["FrameRail"] = ["material.FrameRail", "material.FaceFrame", "material.All"],
            ["FrameMullion"] = ["material.FrameMullion", "material.FaceFrame", "material.All"],
            ["Door"] = ["material.Door", "material.All"],
            ["DrawerFront"] = ["material.DrawerFront", "material.All"],
            ["DrawerBoxBottom"] = ["material.DrawerBoxBottom", "material.DrawerBox", "material.All"],
            ["DrawerBoxFront"] = ["material.DrawerBoxFront", "material.DrawerBox", "material.All"],
            ["DrawerBoxBack"] = ["material.DrawerBoxBack", "material.DrawerBox", "material.All"],
            ["DrawerBoxLeftSide"] = ["material.DrawerBoxLeftSide", "material.DrawerBox", "material.All"],
            ["DrawerBoxRightSide"] = ["material.DrawerBoxRightSide", "material.DrawerBox", "material.All"]
        };

    private readonly ICatalogService _catalog;
    private readonly IDesignStateStore _stateStore;
    private readonly IAppLogger? _logger;

    public ConstraintPropagationStage()
        : this(new CatalogService(), new InMemoryDesignStateStore())
    {
    }

    public ConstraintPropagationStage(ICatalogService catalog, IDesignStateStore stateStore, IAppLogger? logger = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _logger = logger;
    }

    public int StageNumber => 6;

    public string StageName => "Constraint Propagation";

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var materialAssignments = new List<MaterialAssignment>();
        var hardwareAssignments = new List<HardwareAssignment>();
        var violations = new List<ConstraintViolation>();
        var issues = new List<ValidationIssue>();

        foreach (var part in context.PartResult.Parts
                     .OrderBy(part => part.CabinetId.Value)
                     .ThenBy(part => part.PartType, StringComparer.Ordinal)
                     .ThenBy(part => part.PartId, StringComparer.Ordinal))
        {
            var cabinet = _stateStore.GetCabinet(part.CabinetId);
            if (cabinet is null)
            {
                AddViolation(
                    violations,
                    issues,
                    "MATERIAL_UNRESOLVED",
                    ValidationSeverity.Error,
                    $"Cabinet '{part.CabinetId}' could not be resolved for part '{part.Label}'.",
                    [part.PartId, part.CabinetId.ToString()]);
                continue;
            }

            var materialId = ResolveMaterialId(part, cabinet, out var usedOverride);
            if (materialId == default)
            {
                AddViolation(
                    violations,
                    issues,
                    "MATERIAL_UNRESOLVED",
                    ValidationSeverity.Error,
                    $"Part '{part.Label}' could not resolve a material assignment.",
                    [part.PartId, cabinet.CabinetId.ToString()]);
                continue;
            }

            if (usedOverride && !_catalog.IsKnownMaterial(materialId))
            {
                AddViolation(
                    violations,
                    issues,
                    "MATERIAL_UNRESOLVED",
                    ValidationSeverity.Error,
                    $"Part '{part.Label}' references unknown material '{materialId}'.",
                    [part.PartId, cabinet.CabinetId.ToString()]);
                continue;
            }

            var resolvedThickness = ResolveThickness(part.PartType, cabinet.Category, materialId, usedOverride);
            if (resolvedThickness.Actual <= Length.Zero)
            {
                AddViolation(
                    violations,
                    issues,
                    "MATERIAL_UNRESOLVED",
                    ValidationSeverity.Error,
                    $"Part '{part.Label}' resolved material '{materialId}' without a valid thickness.",
                    [part.PartId, cabinet.CabinetId.ToString()]);
                continue;
            }

            materialAssignments.Add(new MaterialAssignment(
                part.PartId,
                materialId,
                resolvedThickness,
                ResolveGrainDirection(part.PartType, materialId)));
        }

        foreach (var cabinet in _stateStore.GetAllCabinets()
                     .OrderBy(cabinet => cabinet.CabinetId.Value))
        {
            var openingCount = PartGeometry.ResolveOpeningCount(cabinet);
            if (openingCount <= 0)
            {
                continue;
            }

            for (var index = 0; index < openingCount; index++)
            {
                var openingId = CreateStableOpeningId(cabinet.CabinetId, index);
                var hardwareIds = _catalog.ResolveHardwareForOpening(openingId, cabinet.Category);
                if (hardwareIds.Count == 0)
                {
                    AddViolation(
                        violations,
                        issues,
                        "NO_HARDWARE_CATALOG",
                        ValidationSeverity.Warning,
                        $"No hardware catalog configured for {cabinet.Category} opening. V2 will integrate vendor hardware.",
                        [cabinet.CabinetId.ToString(), openingId.ToString()]);
                    continue;
                }

                hardwareAssignments.Add(new HardwareAssignment(openingId, hardwareIds, null));
            }
        }

        context.ConstraintResult = new ConstraintPropagationResult
        {
            MaterialAssignments = materialAssignments
                .OrderBy(assignment => assignment.PartId, StringComparer.Ordinal)
                .ToArray(),
            HardwareAssignments = hardwareAssignments
                .OrderBy(assignment => assignment.OpeningId.Value)
                .ToArray(),
            Violations = violations
                .OrderBy(violation => violation.ConstraintCode, StringComparer.Ordinal)
                .ThenBy(violation => violation.Message, StringComparer.Ordinal)
                .ToArray()
        };

        _logger?.Log(new LogEntry
        {
            Level = issues.Any(issue => issue.Severity >= ValidationSeverity.Error) ? LogLevel.Warning : LogLevel.Debug,
            Category = "ConstraintPropagationStage",
            Message = $"Resolved {materialAssignments.Count} materials, {hardwareAssignments.Count} hardware assignments, {violations.Count} constraint violations.",
            Timestamp = DateTimeOffset.UtcNow,
            StageNumber = StageNumber.ToString()
        });

        return issues.Any(issue => issue.Severity >= ValidationSeverity.Error)
            ? StageResult.Failed(StageNumber, issues
                .OrderBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                .ToArray())
            : StageResult.Succeeded(
                StageNumber,
                warnings: issues
                    .OrderBy(issue => issue.Code, StringComparer.Ordinal)
                    .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                    .ToArray());
    }

    private MaterialId ResolveMaterialId(GeneratedPart part, CabinetStateRecord cabinet, out bool usedOverride)
    {
        foreach (var key in GetOverrideKeys(part.PartType))
        {
            if (cabinet.EffectiveOverrides.TryGetValue(key, out var overrideValue) &&
                overrideValue is OverrideValue.OfMaterialId materialIdOverride &&
                materialIdOverride.Value != default)
            {
                usedOverride = true;
                return materialIdOverride.Value;
            }
        }

        usedOverride = false;
        return _catalog.ResolvePartMaterial(part.PartType, cabinet.Category, cabinet.Construction);
    }

    private Thickness ResolveThickness(string partType, CabinetCategory category, MaterialId materialId, bool usedOverride)
    {
        if (usedOverride)
        {
            return _catalog.ResolveMaterialThickness(materialId);
        }

        var partThickness = _catalog.ResolvePartThickness(partType, category);
        return partThickness.Actual > Length.Zero
            ? partThickness
            : _catalog.ResolveMaterialThickness(materialId);
    }

    private GrainDirection ResolveGrainDirection(string partType, MaterialId materialId) =>
        _catalog.ResolveMaterialGrain(materialId) == GrainDirection.None || partType is "Back" or "ToeKick"
            ? GrainDirection.None
            : GrainDirection.LengthWise;

    private static IEnumerable<string> GetOverrideKeys(string partType) =>
        MaterialOverrideKeys.TryGetValue(partType, out var keys)
            ? keys
            : [$"material.{partType}", "material.All"];

    private static OpeningId CreateStableOpeningId(CabinetId cabinetId, int ordinal)
    {
        var bytes = Encoding.UTF8.GetBytes($"opening:{cabinetId.Value:D}:{ordinal}");
        var hashBytes = SHA256.HashData(bytes);
        return new OpeningId(new Guid(hashBytes.AsSpan(0, 16)));
    }

    private static void AddViolation(
        ICollection<ConstraintViolation> violations,
        ICollection<ValidationIssue> issues,
        string code,
        ValidationSeverity severity,
        string message,
        IReadOnlyList<string> affectedEntityIds)
    {
        var violation = new ConstraintViolation(code, message, severity, affectedEntityIds);
        violations.Add(violation);
        issues.Add(new ValidationIssue(severity, code, message, affectedEntityIds));
    }
}
