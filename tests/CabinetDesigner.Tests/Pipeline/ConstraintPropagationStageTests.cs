using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.MaterialContext;
using CabinetDesigner.Domain.RunContext;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class ConstraintPropagationStageTests
{
    [Fact]
    public void Execute_AssignsDefaultMaterial_FromCatalog()
    {
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000010"));
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000010"));
        var store = CreateStore([
            CreateRun(runId),
            CreateCabinet(cabinetId, runId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless)
        ]);
        var catalog = new CatalogService();
        var stage = new ConstraintPropagationStage(catalog, store);
        var context = CreateContext([
            CreatePart(cabinetId, "part:1", "LeftSide"),
            CreatePart(cabinetId, "part:2", "Back"),
            CreatePart(cabinetId, "part:3", "AdjustableShelf")
        ]);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.Equal(3, context.ConstraintResult.MaterialAssignments.Count);

        var leftSide = Assert.Single(context.ConstraintResult.MaterialAssignments.Where(x => x.PartId == "part:1"));
        var back = Assert.Single(context.ConstraintResult.MaterialAssignments.Where(x => x.PartId == "part:2"));
        var shelf = Assert.Single(context.ConstraintResult.MaterialAssignments.Where(x => x.PartId == "part:3"));

        Assert.Equal(catalog.ResolvePartMaterial("LeftSide", CabinetCategory.Base, ConstructionMethod.Frameless), leftSide.MaterialId);
        Assert.Equal(catalog.ResolvePartMaterial("Back", CabinetCategory.Base, ConstructionMethod.Frameless), back.MaterialId);
        Assert.Equal(catalog.ResolvePartMaterial("AdjustableShelf", CabinetCategory.Base, ConstructionMethod.Frameless), shelf.MaterialId);
        Assert.NotEqual(default, leftSide.MaterialId);
        Assert.NotEqual(default, back.MaterialId);
        Assert.NotEqual(default, shelf.MaterialId);
    }

    [Fact]
    public void Execute_AssignsDefaultMaterial_FromCatalog_ForEveryPart()
    {
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000030"));
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000030"));
        var store = CreateStore([
            CreateRun(runId),
            CreateCabinet(cabinetId, runId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless)
        ]);
        var catalog = new CatalogService();
        var stage = new ConstraintPropagationStage(catalog, store);
        var allPartTypes = new[]
        {
            "LeftSide", "RightSide", "Top", "Bottom", "Back", "AdjustableShelf", "ToeKick",
            "Door", "DrawerFront", "DrawerBoxBottom", "DrawerBoxFront", "DrawerBoxBack",
            "DrawerBoxLeftSide", "DrawerBoxRightSide"
        };
        var parts = allPartTypes.Select((t, i) => CreatePart(cabinetId, $"part:{i}", t)).ToArray();
        var context = CreateContext(parts);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.Equal(allPartTypes.Length, context.ConstraintResult.MaterialAssignments.Count);
        Assert.All(context.ConstraintResult.MaterialAssignments, assignment =>
        {
            Assert.NotEqual(default, assignment.MaterialId);
            Assert.True(assignment.ResolvedThickness.Actual > Length.Zero,
                $"Part {assignment.PartId} should have a positive thickness.");
        });
    }

    [Fact]
    public void Execute_RespectsRunLevelOverride_WhenCabinetOverrideAbsent()
    {
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000031"));
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000031"));
        var catalog = new CatalogService();
        var runLevelMaterial = catalog.ResolvePartMaterial("Back", CabinetCategory.Base, ConstructionMethod.Frameless);
        var store = CreateStore([
            CreateRun(runId),
            CreateCabinet(
                cabinetId, runId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless,
                new Dictionary<string, OverrideValue>
                {
                    ["material.All"] = new OverrideValue.OfMaterialId(runLevelMaterial)
                })
        ]);
        var stage = new ConstraintPropagationStage(catalog, store);
        var context = CreateContext([CreatePart(cabinetId, "part:1", "LeftSide")]);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        var assignment = Assert.Single(context.ConstraintResult.MaterialAssignments);
        Assert.Equal(runLevelMaterial, assignment.MaterialId);
    }

    [Fact]
    public void Execute_RespectsCabinetLevelOverride()
    {
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000011"));
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000011"));
        var catalog = new CatalogService();
        var overrideMaterial = catalog.ResolvePartMaterial("Back", CabinetCategory.Base, ConstructionMethod.Frameless);
        var store = CreateStore([
            CreateRun(runId),
            CreateCabinet(
                cabinetId,
                runId,
                "base-30",
                CabinetCategory.Base,
                ConstructionMethod.Frameless,
                new Dictionary<string, OverrideValue>
                {
                    ["material.LeftSide"] = new OverrideValue.OfMaterialId(overrideMaterial)
                })
        ]);
        var stage = new ConstraintPropagationStage(catalog, store);
        var context = CreateContext([CreatePart(cabinetId, "part:1", "LeftSide")]);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        var assignment = Assert.Single(context.ConstraintResult.MaterialAssignments);
        Assert.Equal(overrideMaterial, assignment.MaterialId);
    }

    [Fact]
    public void Execute_AssignsResolvedThickness_FromMaterial()
    {
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000016"));
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000016"));
        var store = CreateStore([
            CreateRun(runId),
            CreateCabinet(cabinetId, runId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless)
        ]);
        var catalog = new CatalogService();
        var stage = new ConstraintPropagationStage(catalog, store);
        var context = CreateContext([CreatePart(cabinetId, "part:1", "Back")]);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        var assignment = Assert.Single(context.ConstraintResult.MaterialAssignments);
        Assert.Equal(Thickness.Exact(Length.FromInches(0.25m)), assignment.ResolvedThickness);
    }

    [Fact]
    public void Execute_EmitsViolation_WhenPartHasNoResolution()
    {
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000012"));
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000012"));
        var store = CreateStore([
            CreateRun(runId),
            CreateCabinet(cabinetId, runId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless)
        ]);
        var stage = new ConstraintPropagationStage(new CatalogService(), store);
        var context = CreateContext([CreatePart(cabinetId, "part:1", "UnmappedPart")]);

        var result = stage.Execute(context);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, i => i.Code == "MATERIAL_UNRESOLVED" && i.Severity == ValidationSeverity.Error);

        var materialViolation = Assert.Single(context.ConstraintResult.Violations
            .Where(v => v.ConstraintCode == "MATERIAL_UNRESOLVED"));
        Assert.Equal(ValidationSeverity.Error, materialViolation.Severity);
        Assert.Contains("part:1", materialViolation.AffectedEntityIds);
    }

    [Fact]
    public void Execute_EmitsError_WhenPartHasNoResolution()
    {
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000032"));
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000032"));
        var store = CreateStore([
            CreateRun(runId),
            CreateCabinet(cabinetId, runId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless)
        ]);
        var stage = new ConstraintPropagationStage(new CatalogService(), store);
        var context = CreateContext([CreatePart(cabinetId, "part:x", "UnknownType")]);

        var result = stage.Execute(context);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, i => i.Code == "MATERIAL_UNRESOLVED" && i.Severity == ValidationSeverity.Error);
        Assert.DoesNotContain(result.Issues, i => i.Code == "MATERIAL_UNRESOLVED" && i.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void Execute_HardwareEmpty_EmitsWarningViolation_NotError()
    {
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000033"));
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000033"));
        var store = CreateStore([
            CreateRun(runId),
            CreateCabinet(cabinetId, runId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless)
        ]);
        var stage = new ConstraintPropagationStage(new CatalogService(), store);
        var context = CreateContext([CreatePart(cabinetId, "part:1", "LeftSide")]);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.Empty(context.ConstraintResult.HardwareAssignments);
        Assert.All(
            context.ConstraintResult.Violations.Where(v => v.ConstraintCode == "NO_HARDWARE_CATALOG"),
            v => Assert.Equal(ValidationSeverity.Warning, v.Severity));
        Assert.DoesNotContain(result.Issues, i => i.Code == "NO_HARDWARE_CATALOG" && i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Execute_HardwareEmpty_EmitsWarningViolation()
    {
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000013"));
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000013"));
        var store = CreateStore([
            CreateRun(runId),
            CreateCabinet(cabinetId, runId, "specialty-blank", CabinetCategory.Specialty, ConstructionMethod.Frameless)
        ]);
        var stage = new ConstraintPropagationStage(new CatalogService(), store);
        var context = CreateContext([CreatePart(cabinetId, "part:1", "LeftSide")]);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.Empty(context.ConstraintResult.HardwareAssignments);
        var violation = Assert.Single(context.ConstraintResult.Violations);
        Assert.Equal("NO_HARDWARE_CATALOG", violation.ConstraintCode);
        Assert.Equal(ValidationSeverity.Warning, violation.Severity);
        Assert.Equal(result.Issues, result.Issues.OrderBy(issue => issue.Code, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Execute_IsDeterministic()
    {
        var firstCabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000014"));
        var secondCabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000015"));
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000014"));
        var store = CreateStore([
            CreateRun(runId),
            CreateCabinet(firstCabinetId, runId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless),
            CreateCabinet(secondCabinetId, runId, "wall-30", CabinetCategory.Wall, ConstructionMethod.FaceFrame)
        ]);
        var stage = new ConstraintPropagationStage(new CatalogService(), store);
        GeneratedPart[] parts =
        [
            CreatePart(secondCabinetId, "part:2", "Back"),
            CreatePart(firstCabinetId, "part:1", "LeftSide"),
            CreatePart(firstCabinetId, "part:3", "AdjustableShelf"),
            CreatePart(secondCabinetId, "part:4", "FrameRail")
        ];

        var firstContext = CreateContext(parts);
        var secondContext = CreateContext(parts.Reverse().ToArray());

        var firstResult = stage.Execute(firstContext);
        var secondResult = stage.Execute(secondContext);

        Assert.True(firstResult.Success);
        Assert.True(secondResult.Success);
        Assert.Equal(firstContext.ConstraintResult.MaterialAssignments, secondContext.ConstraintResult.MaterialAssignments);
        Assert.Equal(
            firstContext.ConstraintResult.HardwareAssignments.Select(FormatHardwareAssignment),
            secondContext.ConstraintResult.HardwareAssignments.Select(FormatHardwareAssignment));
        Assert.Equal(
            firstContext.ConstraintResult.Violations.Select(FormatViolation),
            secondContext.ConstraintResult.Violations.Select(FormatViolation));
    }

    private static InMemoryDesignStateStore CreateStore(object[] entities)
    {
        var store = new InMemoryDesignStateStore();
        foreach (var entity in entities)
        {
            switch (entity)
            {
                case CabinetRun run:
                    store.AddRun(run, Point2D.Origin, new Point2D(run.Capacity.Inches, 0m));
                    break;
                case CabinetStateRecord cabinet:
                    store.AddCabinet(cabinet);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported test entity {entity.GetType().Name}.");
            }
        }

        return store;
    }

    private static CabinetRun CreateRun(RunId runId) =>
        new(runId, new WallId(Guid.Parse("40000000-0000-0000-0000-000000000001")), Length.FromInches(120m));

    private static CabinetStateRecord CreateCabinet(
        CabinetId cabinetId,
        RunId runId,
        string cabinetTypeId,
        CabinetCategory category,
        ConstructionMethod construction,
        IReadOnlyDictionary<string, OverrideValue>? overrides = null) =>
        new(
            cabinetId,
            cabinetTypeId,
            Length.FromInches(30m),
            Length.FromInches(24m),
            runId,
            new RunSlotId(Guid.Parse($"50000000-0000-0000-0000-{cabinetId.Value.ToString("N")[20..32]}")),
            category,
            construction,
            Length.FromInches(category == CabinetCategory.Wall ? 30m : 34.5m),
            overrides);

    private static GeneratedPart CreatePart(CabinetId cabinetId, string partId, string partType) =>
        new()
        {
            PartId = partId,
            CabinetId = cabinetId,
            PartType = partType,
            Width = Length.FromInches(10m),
            Height = Length.FromInches(20m),
            MaterialThickness = Thickness.Exact(Length.FromInches(0.75m)),
            MaterialId = default,
            GrainDirection = GrainDirection.None,
            Edges = new EdgeTreatment(null, null, null, null),
            Label = $"{cabinetId}-{partType}"
        };

    private static ResolutionContext CreateContext(IReadOnlyList<GeneratedPart> parts)
    {
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand(),
            Mode = ResolutionMode.Full
        };

        context.PartResult = new PartGenerationResult
        {
            Parts = parts
        };

        return context;
    }

    private static string FormatHardwareAssignment(HardwareAssignment assignment) =>
        $"{assignment.OpeningId}|{string.Join(",", assignment.HardwareIds.Select(id => id.ToString()))}|{assignment.BoringPattern}";

    private static string FormatViolation(ConstraintViolation violation) =>
        $"{violation.ConstraintCode}|{violation.Message}|{violation.Severity}|{string.Join(",", violation.AffectedEntityIds)}";

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Constraint Propagation Stage Test", []);

        public string CommandType => "test.constraint_propagation_stage";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
