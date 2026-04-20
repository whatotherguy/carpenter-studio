using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ManufacturingContext;
using CabinetDesigner.Domain.MaterialContext;

namespace CabinetDesigner.Application.Projection;

public sealed class ManufacturingProjector : IManufacturingProjector
{
    private readonly ManufacturingProjectionSettings _settings;

    public ManufacturingProjector()
        : this(ManufacturingProjectionSettings.Default)
    {
    }

    public ManufacturingProjector(ManufacturingProjectionSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public ManufacturingPlan Project(
        PartGenerationResult partResult,
        ConstraintPropagationResult constraintResult)
    {
        ArgumentNullException.ThrowIfNull(partResult);
        ArgumentNullException.ThrowIfNull(constraintResult);

        var blockers = new List<ManufacturingBlocker>();
        blockers.AddRange(BuildConstraintBlockers(constraintResult));

        var materialAssignments = constraintResult.MaterialAssignments
            .OrderBy(assignment => assignment.PartId, StringComparer.Ordinal)
            .GroupBy(assignment => assignment.PartId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Last(),
                StringComparer.Ordinal);

        var projectedParts = new List<ManufacturingPart>();

        if (partResult.Parts.Count == 0)
        {
            blockers.Add(new ManufacturingBlocker
            {
                Code = ManufacturingBlockerCode.NoPartsProduced,
                Message = "No parts were produced for manufacturing.",
                AffectedEntityIds = []
            });
        }

        foreach (var part in partResult.Parts)
        {
            var resolvedMaterialId = ResolveMaterialId(part, materialAssignments);
            var resolvedThickness = ResolveThickness(part, materialAssignments);
            var resolvedGrain = ResolveGrainDirection(part, materialAssignments);
            var partBlockers = ValidatePart(part, resolvedMaterialId, resolvedThickness);
            blockers.AddRange(partBlockers);

            if (partBlockers.Count > 0)
            {
                continue;
            }

            projectedParts.Add(new ManufacturingPart
            {
                PartId = part.PartId,
                CabinetId = part.CabinetId,
                PartType = part.PartType,
                Label = part.Label,
                CutWidth = part.Width,
                CutHeight = part.Height,
                MaterialThickness = resolvedThickness,
                MaterialId = resolvedMaterialId,
                GrainDirection = resolvedGrain,
                EdgeTreatment = new ManufacturedEdgeTreatment(
                    part.Edges.TopEdgeBandingId,
                    part.Edges.BottomEdgeBandingId,
                    part.Edges.LeftEdgeBandingId,
                    part.Edges.RightEdgeBandingId)
            });
        }

        var duplicatePartIds = projectedParts
            .GroupBy(part => part.PartId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();

        if (duplicatePartIds.Length > 0)
        {
            blockers.AddRange(duplicatePartIds.Select(group => new ManufacturingBlocker
            {
                Code = ManufacturingBlockerCode.MalformedPart,
                Message = $"Cut list contains duplicate part identifier '{group.Key}'.",
                AffectedEntityIds = group
                    .Select(part => part.PartId)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            }));
        }

        var invalidPartIds = duplicatePartIds
            .SelectMany(group => group.Select(part => part.PartId))
            .ToHashSet(StringComparer.Ordinal);

        var orderedParts = projectedParts
            .Where(part => !invalidPartIds.Contains(part.PartId))
            .OrderBy(part => part.MaterialId.Value, Comparer<Guid>.Default)
            .ThenBy(part => part.MaterialThickness.Nominal.Inches)
            .ThenBy(part => part.MaterialThickness.Actual.Inches)
            .ThenBy(part => part.GrainDirection)
            .ThenBy(part => part.Label, StringComparer.Ordinal)
            .ThenBy(part => part.PartId, StringComparer.Ordinal)
            .ToArray();

        var groups = orderedParts
            .GroupBy(part => new MaterialGroupKey(part.MaterialId, part.MaterialThickness, part.GrainDirection))
            .Select(group => new ManufacturingMaterialGroup
            {
                MaterialId = group.Key.MaterialId,
                MaterialThickness = group.Key.MaterialThickness,
                GrainDirection = group.Key.GrainDirection,
                Parts = group
                    .OrderBy(part => part.Label, StringComparer.Ordinal)
                    .ThenBy(part => part.PartId, StringComparer.Ordinal)
                    .ToArray()
            })
            .OrderBy(group => group.MaterialId.Value, Comparer<Guid>.Default)
            .ThenBy(group => group.MaterialThickness.Nominal.Inches)
            .ThenBy(group => group.MaterialThickness.Actual.Inches)
            .ThenBy(group => group.GrainDirection)
            .ToArray();

        var cutList = orderedParts
            .Select(part => new CutListItem
            {
                PartId = part.PartId,
                CabinetId = part.CabinetId,
                PartType = part.PartType,
                Label = part.Label,
                CutWidth = part.CutWidth,
                CutHeight = part.CutHeight,
                MaterialThickness = part.MaterialThickness,
                MaterialId = part.MaterialId,
                GrainDirection = part.GrainDirection,
                EdgeTreatment = part.EdgeTreatment
            })
            .ToArray();

        return new ManufacturingPlan
        {
            MaterialGroups = groups,
            CutList = cutList,
            Operations = BuildOperations(orderedParts),
            EdgeBandingRequirements = BuildEdgeBandingRequirements(orderedParts),
            Readiness = new ManufacturingReadinessResult
            {
                IsReady = blockers.Count == 0,
                Blockers = blockers
                    .OrderBy(blocker => blocker.Code)
                    .ThenBy(blocker => blocker.Message, StringComparer.Ordinal)
                    .ThenBy(blocker => blocker.AffectedEntityIds.Count > 0 ? blocker.AffectedEntityIds[0] : string.Empty, StringComparer.Ordinal)
                    .ToArray()
            }
        };
    }

    private static IReadOnlyList<ManufacturingBlocker> BuildConstraintBlockers(ConstraintPropagationResult constraintResult) =>
        constraintResult.Violations
            .OrderBy(violation => violation.ConstraintCode, StringComparer.Ordinal)
            .ThenBy(violation => violation.Message, StringComparer.Ordinal)
            .SelectMany(violation => violation.ConstraintCode switch
            {
                "MATERIAL_UNRESOLVED" =>
                [
                    new ManufacturingBlocker
                    {
                        Code = ManufacturingBlockerCode.MissingMaterial,
                        Message = violation.Message,
                        AffectedEntityIds = violation.AffectedEntityIds
                    }
                ],
                "NO_HARDWARE_CATALOG" =>
                [
                    new ManufacturingBlocker
                    {
                        Code = ManufacturingBlockerCode.MissingHardware,
                        Message = violation.Message,
                        AffectedEntityIds = violation.AffectedEntityIds
                    }
                ],
                _ => Array.Empty<ManufacturingBlocker>()
            })
            .ToArray();

    private static MaterialId ResolveMaterialId(
        GeneratedPart part,
        IReadOnlyDictionary<string, MaterialAssignment> materialAssignments) =>
        materialAssignments.TryGetValue(part.PartId, out var assignment)
            ? assignment.MaterialId
            : part.MaterialId;

    private static Thickness ResolveThickness(
        GeneratedPart part,
        IReadOnlyDictionary<string, MaterialAssignment> materialAssignments) =>
        materialAssignments.TryGetValue(part.PartId, out var assignment)
            ? assignment.ResolvedThickness
            : part.MaterialThickness;

    private static GrainDirection ResolveGrainDirection(
        GeneratedPart part,
        IReadOnlyDictionary<string, MaterialAssignment> materialAssignments) =>
        materialAssignments.TryGetValue(part.PartId, out var assignment)
            ? assignment.GrainDirection
            : part.GrainDirection;

    private IReadOnlyList<ManufacturingBlocker> ValidatePart(
        GeneratedPart part,
        MaterialId materialId,
        Thickness thickness)
    {
        var blockers = new List<ManufacturingBlocker>();

        if (string.IsNullOrWhiteSpace(part.PartId))
        {
            blockers.Add(new ManufacturingBlocker
            {
                Code = ManufacturingBlockerCode.MalformedPart,
                Message = "A manufacturing part is missing a stable identifier.",
                AffectedEntityIds = []
            });
        }

        if (part.CabinetId == default)
        {
            blockers.Add(new ManufacturingBlocker
            {
                Code = ManufacturingBlockerCode.MalformedPart,
                Message = $"Part '{part.Label}' is missing its cabinet association.",
                AffectedEntityIds = string.IsNullOrWhiteSpace(part.PartId) ? [] : [part.PartId]
            });
        }

        if (string.IsNullOrWhiteSpace(part.PartType))
        {
            blockers.Add(new ManufacturingBlocker
            {
                Code = ManufacturingBlockerCode.MalformedPart,
                Message = $"Part '{part.Label}' is missing a part type.",
                AffectedEntityIds = string.IsNullOrWhiteSpace(part.PartId) ? [] : [part.PartId]
            });
        }

        if (string.IsNullOrWhiteSpace(part.Label))
        {
            blockers.Add(new ManufacturingBlocker
            {
                Code = ManufacturingBlockerCode.MalformedPart,
                Message = $"Part '{part.PartId}' is missing a cut-list label.",
                AffectedEntityIds = string.IsNullOrWhiteSpace(part.PartId) ? [] : [part.PartId]
            });
        }

        if (materialId == default)
        {
            blockers.Add(new ManufacturingBlocker
            {
                Code = ManufacturingBlockerCode.MissingMaterial,
                Message = $"Part '{part.Label}' is missing a material assignment.",
                AffectedEntityIds = [part.PartId]
            });
        }

        if (part.Width <= Length.Zero || part.Height <= Length.Zero)
        {
            blockers.Add(new ManufacturingBlocker
            {
                Code = ManufacturingBlockerCode.InvalidDimensions,
                Message = $"Part '{part.Label}' has impossible cut dimensions.",
                AffectedEntityIds = [part.PartId]
            });
        }

        if (thickness.Nominal <= Length.Zero || thickness.Actual <= Length.Zero)
        {
            blockers.Add(new ManufacturingBlocker
            {
                Code = ManufacturingBlockerCode.InvalidThickness,
                Message = $"Part '{part.Label}' has incomplete thickness data.",
                AffectedEntityIds = [part.PartId]
            });
        }

        var smallestDimension = Length.Min(part.Width, part.Height);
        if (smallestDimension < _settings.MinimumPartDimension)
        {
            blockers.Add(new ManufacturingBlocker
            {
                Code = ManufacturingBlockerCode.PartTooSmall,
                Message = $"Part '{part.Label}' is below the minimum shop dimension.",
                AffectedEntityIds = [part.PartId]
            });
        }

        var largestDimension = Length.Max(part.Width, part.Height);
        if (largestDimension > _settings.MaximumPartDimension)
        {
            blockers.Add(new ManufacturingBlocker
            {
                Code = ManufacturingBlockerCode.PartTooLarge,
                Message = $"Part '{part.Label}' exceeds the maximum shop dimension.",
                AffectedEntityIds = [part.PartId]
            });
        }

        return blockers;
    }

    private static IReadOnlyList<ManufacturingOperation> BuildOperations(IReadOnlyList<ManufacturingPart> parts)
    {
        var operations = new List<ManufacturingOperation>();

        foreach (var part in parts)
        {
            operations.Add(new ManufacturingOperation
            {
                PartId = part.PartId,
                Sequence = 0,
                Kind = ManufacturingOperationKind.SawCutRectangle,
                Parameters = new Dictionary<string, OverrideValue>(StringComparer.Ordinal)
                {
                    ["cut_width"] = new OverrideValue.OfLength(part.CutWidth),
                    ["cut_height"] = new OverrideValue.OfLength(part.CutHeight),
                    ["actual_thickness"] = new OverrideValue.OfThickness(part.MaterialThickness)
                }
            });

            if (!part.RequiresEdgeBanding)
            {
                continue;
            }

            operations.Add(new ManufacturingOperation
            {
                PartId = part.PartId,
                Sequence = 1,
                Kind = ManufacturingOperationKind.ApplyEdgeBanding,
                Parameters = new Dictionary<string, OverrideValue>(StringComparer.Ordinal)
                {
                    ["banded_edge_count"] = new OverrideValue.OfInt(part.EdgeTreatment.BandedEdgeCount),
                    ["banded_length"] = new OverrideValue.OfLength(GetTotalBandedLength(part))
                }
            });
        }

        return operations
            .OrderBy(operation => operation.PartId, StringComparer.Ordinal)
            .ThenBy(operation => operation.Sequence)
            .ToArray();
    }

    private static IReadOnlyList<EdgeBandingRequirement> BuildEdgeBandingRequirements(IReadOnlyList<ManufacturingPart> parts)
    {
        var requirements = new Dictionary<string, (int EdgeCount, Length TotalLinearLength)>(StringComparer.Ordinal);

        foreach (var part in parts)
        {
            AccumulateEdge(requirements, part.EdgeTreatment.TopEdgeBandingId, part.CutWidth);
            AccumulateEdge(requirements, part.EdgeTreatment.BottomEdgeBandingId, part.CutWidth);
            AccumulateEdge(requirements, part.EdgeTreatment.LeftEdgeBandingId, part.CutHeight);
            AccumulateEdge(requirements, part.EdgeTreatment.RightEdgeBandingId, part.CutHeight);
        }

        return requirements
            .OrderBy(requirement => requirement.Key, StringComparer.Ordinal)
            .Select(requirement => new EdgeBandingRequirement
            {
                EdgeBandingId = requirement.Key,
                EdgeCount = requirement.Value.EdgeCount,
                TotalLinearLength = requirement.Value.TotalLinearLength
            })
            .ToArray();
    }

    private static Length GetTotalBandedLength(ManufacturingPart part)
    {
        var total = Length.Zero;

        if (part.EdgeTreatment.TopEdgeBandingId is not null)
        {
            total += part.CutWidth;
        }

        if (part.EdgeTreatment.BottomEdgeBandingId is not null)
        {
            total += part.CutWidth;
        }

        if (part.EdgeTreatment.LeftEdgeBandingId is not null)
        {
            total += part.CutHeight;
        }

        if (part.EdgeTreatment.RightEdgeBandingId is not null)
        {
            total += part.CutHeight;
        }

        return total;
    }

    private static void AccumulateEdge(
        IDictionary<string, (int EdgeCount, Length TotalLinearLength)> requirements,
        string? edgeBandingId,
        Length edgeLength)
    {
        if (string.IsNullOrWhiteSpace(edgeBandingId))
        {
            return;
        }

        if (requirements.TryGetValue(edgeBandingId, out var current))
        {
            requirements[edgeBandingId] = (current.EdgeCount + 1, current.TotalLinearLength + edgeLength);
            return;
        }

        requirements[edgeBandingId] = (1, edgeLength);
    }

    private sealed record MaterialGroupKey(
        MaterialId MaterialId,
        Thickness MaterialThickness,
        GrainDirection GrainDirection);
}

public sealed record ManufacturingProjectionSettings
{
    public required Length MinimumPartDimension { get; init; }

    public required Length MaximumPartDimension { get; init; }

    public static ManufacturingProjectionSettings Default { get; } = new()
    {
        MinimumPartDimension = Length.FromInches(1m),
        MaximumPartDimension = Length.FromInches(120m)
    };
}
