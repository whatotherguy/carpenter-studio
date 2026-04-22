using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Projection;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ManufacturingContext;
using CabinetDesigner.Domain.MaterialContext;
using Xunit;

namespace CabinetDesigner.Tests.Application.Projection;

public sealed class ManufacturingProjectorTests
{
    private readonly ManufacturingProjector _projector = new();

    [Fact]
    public void Project_ProjectsPartsIntoGroupsCutListAndOperations()
    {
        var materialId = MaterialId.New();
        var partA = CreatePart(
            partId: "part-b",
            label: "Left Side",
            materialId: materialId,
            width: Length.FromInches(18m),
            height: Length.FromInches(34.5m),
            edgeTreatment: new EdgeTreatment("edge-1", null, null, "edge-1"));
        var partB = CreatePart(
            partId: "part-a",
            label: "Bottom",
            materialId: materialId,
            width: Length.FromInches(24m),
            height: Length.FromInches(18m),
            edgeTreatment: new EdgeTreatment(null, null, null, null));

        var plan = _projector.Project(
            new PartGenerationResult { Parts = [partA, partB] },
            EmptyConstraints());

        var group = Assert.Single(plan.MaterialGroups);
        Assert.Equal(new[] { "Bottom", "Left Side" }, group.Parts.Select(part => part.Label).ToArray());
        Assert.Equal(new[] { "part-a", "part-b" }, plan.CutList.Select(item => item.PartId).ToArray());
        Assert.Equal(
            new[]
            {
                ManufacturingOperationKind.SawCutRectangle,
                ManufacturingOperationKind.SawCutRectangle,
                ManufacturingOperationKind.ApplyEdgeBanding
            },
            plan.Operations.Select(operation => operation.Kind).ToArray());

        var edgeRequirement = Assert.Single(plan.EdgeBandingRequirements);
        Assert.Equal("edge-1", edgeRequirement.EdgeBandingId);
        Assert.Equal(2, edgeRequirement.EdgeCount);
        Assert.Equal(Length.FromInches(52.5m), edgeRequirement.TotalLinearLength);
        Assert.True(plan.Readiness.IsReady);
    }

    [Fact]
    public void Project_PreservesGeometryValueObjectAccuracy()
    {
        var width = Length.FromMillimeters(762m);
        var height = Length.FromMillimeters(457.2m);
        var thickness = new Thickness(
            Length.FromMillimeters(19.05m),
            Length.FromMillimeters(17.8562m));

        var part = CreatePart(
            partId: "metric-panel",
            label: "Metric Panel",
            materialId: MaterialId.New(),
            width: width,
            height: height,
            thickness: thickness,
            edgeTreatment: new EdgeTreatment("edge-1", null, null, null));

        var plan = _projector.Project(
            new PartGenerationResult { Parts = [part] },
            EmptyConstraints());

        var item = Assert.Single(plan.CutList);
        Assert.Equal(width, item.CutWidth);
        Assert.Equal(height, item.CutHeight);
        Assert.Equal(thickness.Actual, item.MaterialThickness.Actual);

        var sawCut = Assert.Single(plan.Operations.Where(operation => operation.Kind == ManufacturingOperationKind.SawCutRectangle));
        Assert.Equal(width, Assert.IsType<OverrideValue.OfLength>(sawCut.Parameters["cut_width"]).Value);
        Assert.Equal(thickness, Assert.IsType<OverrideValue.OfThickness>(sawCut.Parameters["actual_thickness"]).Value);
    }

    [Fact]
    public void Project_OrdersOutputDeterministically()
    {
        var materialA = new MaterialId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var materialB = new MaterialId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var part1 = CreatePart("z-part", "Zulu", materialA, Length.FromInches(12m), Length.FromInches(20m));
        var part2 = CreatePart("a-part", "Alpha", materialB, Length.FromInches(12m), Length.FromInches(20m));
        var part3 = CreatePart("m-part", "Middle", materialA, Length.FromInches(10m), Length.FromInches(20m));
        var result = new PartGenerationResult { Parts = [part1, part2, part3] };

        var firstPlan = _projector.Project(result, EmptyConstraints());
        var secondPlan = _projector.Project(result, EmptyConstraints());

        Assert.Equal(
            firstPlan.CutList.Select(item => item.PartId).ToArray(),
            secondPlan.CutList.Select(item => item.PartId).ToArray());
        Assert.Equal(
            new[] { part3.PartId, part1.PartId, part2.PartId },
            firstPlan.CutList.Select(item => item.PartId).ToArray());
        Assert.Equal(
            firstPlan.Operations.Select(operation => $"{operation.PartId}:{operation.Sequence}").ToArray(),
            secondPlan.Operations.Select(operation => $"{operation.PartId}:{operation.Sequence}").ToArray());
    }

    [Fact]
    public void Project_DoesNotMutateInputParts()
    {
        var originalParts = new[]
        {
            CreatePart("part-1", "Panel", MaterialId.New(), Length.FromInches(18m), Length.FromInches(24m), edgeTreatment: new EdgeTreatment("edge-1", null, null, null))
        };
        var snapshot = originalParts.Select(part => part with { }).ToArray();

        _projector.Project(new PartGenerationResult { Parts = originalParts }, EmptyConstraints());

        Assert.Equal(snapshot, originalParts);
    }

    [Fact]
    public void Project_InvalidStateProducesSortedBlockersAndSkipsInvalidParts()
    {
        var missingMaterial = CreatePart("missing-material", "Missing Material", default, Length.FromInches(18m), Length.FromInches(24m));
        var invalidThickness = CreatePart(
            "invalid-thickness",
            "Invalid Thickness",
            MaterialId.New(),
            Length.FromInches(18m),
            Length.FromInches(24m),
            thickness: new Thickness(Length.FromInches(0.75m), Length.Zero));
        var tooSmall = CreatePart("too-small", "Too Small", MaterialId.New(), Length.FromInches(0.5m), Length.FromInches(10m));
        var tooLarge = CreatePart("too-large", "Too Large", MaterialId.New(), Length.FromInches(121m), Length.FromInches(18m));
        var valid = CreatePart("valid", "Valid", MaterialId.New(), Length.FromInches(18m), Length.FromInches(24m));

        var plan = _projector.Project(
            new PartGenerationResult { Parts = [tooLarge, valid, tooSmall, invalidThickness, missingMaterial] },
            EmptyConstraints());

        Assert.False(plan.Readiness.IsReady);
        Assert.Equal(
            new[]
            {
                ManufacturingBlockerCode.MissingMaterial,
                ManufacturingBlockerCode.InvalidThickness,
                ManufacturingBlockerCode.PartTooSmall,
                ManufacturingBlockerCode.PartTooLarge
            },
            plan.Readiness.Blockers.Select(blocker => blocker.Code).ToArray());
        Assert.Equal(new[] { "valid" }, plan.CutList.Select(item => item.PartId).ToArray());
    }

    [Fact]
    public void Project_ConstraintViolationsBlockReadiness_WhenMaterialOrHardwareDataIsMissing()
    {
        var part = CreatePart("valid", "Valid", MaterialId.New(), Length.FromInches(18m), Length.FromInches(24m));

        var plan = _projector.Project(
            new PartGenerationResult { Parts = [part] },
            new ConstraintPropagationResult
            {
                MaterialAssignments = [],
                HardwareAssignments = [],
                Violations =
                [
                    new ConstraintViolation(
                        "NO_HARDWARE_CATALOG",
                        "Opening hardware could not be resolved.",
                        ValidationSeverity.Warning,
                        ["opening:1"]),
                    new ConstraintViolation(
                        "MATERIAL_UNRESOLVED",
                        "Part material could not be resolved.",
                        ValidationSeverity.Error,
                        ["valid"])
                ]
            });

        Assert.False(plan.Readiness.IsReady);
        Assert.Equal(
            new[] { ManufacturingBlockerCode.MissingMaterial },
            plan.Readiness.Blockers.Select(blocker => blocker.Code).Distinct().ToArray());
    }

    [Fact]
    public void Project_InvalidDimensionsAndMalformedParts_AreBlocked()
    {
        var malformed = CreatePart(
            partId: string.Empty,
            label: string.Empty,
            materialId: MaterialId.New(),
            width: Length.FromInches(18m),
            height: Length.FromInches(24m)) with
        {
            CabinetId = default
        };
        var impossible = CreatePart(
            partId: "impossible",
            label: "Impossible",
            materialId: MaterialId.New(),
            width: Length.Zero,
            height: Length.FromInches(24m));
        var valid = CreatePart("valid", "Valid", MaterialId.New(), Length.FromInches(18m), Length.FromInches(24m));

        var plan = _projector.Project(
            new PartGenerationResult { Parts = [valid, malformed, impossible] },
            EmptyConstraints());

        Assert.False(plan.Readiness.IsReady);
        Assert.Contains(plan.Readiness.Blockers, blocker => blocker.Code == ManufacturingBlockerCode.MalformedPart);
        Assert.Contains(plan.Readiness.Blockers, blocker => blocker.Code == ManufacturingBlockerCode.InvalidDimensions);
        Assert.Equal(new[] { "valid" }, plan.CutList.Select(item => item.PartId).ToArray());
    }

    private static ConstraintPropagationResult EmptyConstraints() =>
        new()
        {
            MaterialAssignments = [],
            HardwareAssignments = [],
            Violations = []
        };

    private static GeneratedPart CreatePart(
        string partId,
        string label,
        MaterialId materialId,
        Length width,
        Length height,
        Thickness? thickness = null,
        EdgeTreatment? edgeTreatment = null) =>
        new()
        {
            PartId = partId,
            CabinetId = CabinetId.New(),
            PartType = "panel",
            Width = width,
            Height = height,
            MaterialThickness = thickness ?? new Thickness(Length.FromInches(0.75m), Length.FromInches(0.71m)),
            MaterialId = materialId,
            GrainDirection = GrainDirection.LengthWise,
            Edges = edgeTreatment ?? new EdgeTreatment(null, null, null, null),
            Label = label
        };
}
