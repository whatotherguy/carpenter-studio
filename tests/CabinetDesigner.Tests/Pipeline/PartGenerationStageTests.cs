using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class PartGenerationStageTests
{
    [Fact]
    public void Execute_ForFramelessBase_Produces_ExpectedParts()
    {
        var cabinetId = CabinetId.New();
        var store = CreateStore([
            CreateCabinet(cabinetId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless, 30m, 24m, 34.5m)
        ]);
        var stage = new PartGenerationStage(store);
        var context = CreateContext([CreatePlacement(cabinetId)]);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        var types = context.PartResult.Parts.Select(p => p.PartType).ToArray();
        Assert.Contains("LeftSide", types);
        Assert.Contains("RightSide", types);
        Assert.Contains("Top", types);
        Assert.Contains("Bottom", types);
        Assert.Contains("Back", types);
        Assert.Contains("AdjustableShelf", types);
        Assert.Contains("ToeKick", types);
        Assert.Equal(7, context.PartResult.Parts.Count);
    }

    [Fact]
    public void Execute_ForTall_ShelfCountDefaultsToThree()
    {
        var cabinetId = CabinetId.New();
        var store = CreateStore([
            CreateCabinet(cabinetId, "tall-84", CabinetCategory.Tall, ConstructionMethod.Frameless, 24m, 24m, 84m)
        ]);
        var stage = new PartGenerationStage(store);
        var context = CreateContext([CreatePlacement(cabinetId)]);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.True(
            context.PartResult.Parts.Count(p => p.PartType == "AdjustableShelf") >= 3,
            "Tall cabinet should have at least 3 adjustable shelves.");
    }

    [Fact]
    public void Execute_ForWall_ShelfCountDefaultsToTwo()
    {
        var cabinetId = CabinetId.New();
        var store = CreateStore([
            CreateCabinet(cabinetId, "wall-30", CabinetCategory.Wall, ConstructionMethod.Frameless, 30m, 12m, 30m)
        ]);
        var stage = new PartGenerationStage(store);
        var context = CreateContext([CreatePlacement(cabinetId)]);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.Equal(2, context.PartResult.Parts.Count(p => p.PartType == "AdjustableShelf"));
    }

    [Fact]
    public void Execute_ForCabinetWithTwoDoorOpenings_ProducesTwoDoorParts()
    {
        var cabinetId = CabinetId.New();
        var opening1 = new CabinetOpeningStateRecord(Guid.NewGuid(), 0, OpeningType.Door, Length.FromInches(14m), Length.FromInches(28m));
        var opening2 = new CabinetOpeningStateRecord(Guid.NewGuid(), 1, OpeningType.Door, Length.FromInches(14m), Length.FromInches(28m));
        var store = CreateStore([
            CreateCabinetWithOpenings(cabinetId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless, 30m, 24m, 34.5m, [opening1, opening2])
        ]);
        var stage = new PartGenerationStage(store);
        var context = CreateContext([CreatePlacement(cabinetId)]);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        var doorParts = context.PartResult.Parts.Where(p => p.PartType == "Door").ToArray();
        Assert.Equal(2, doorParts.Length);
        Assert.All(doorParts, door =>
        {
            Assert.Equal(Length.FromInches(14m), door.Width);
            Assert.Equal(Length.FromInches(28m), door.Height);
        });
    }

    [Fact]
    public void Execute_ForCabinetWithThreeDrawers_ProducesThreeDrawerFronts_AndThreeDrawerBoxSets()
    {
        var cabinetId = CabinetId.New();
        var openings = Enumerable.Range(0, 3)
            .Select(i => new CabinetOpeningStateRecord(Guid.NewGuid(), i, OpeningType.Drawer, Length.FromInches(16m), Length.FromInches(10m)))
            .ToList();
        var store = CreateStore([
            CreateCabinetWithOpenings(cabinetId, "base-drawer-18", CabinetCategory.Base, ConstructionMethod.Frameless, 18m, 24m, 34.5m, openings)
        ]);
        var stage = new PartGenerationStage(store);
        var context = CreateContext([CreatePlacement(cabinetId)]);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.Equal(3, context.PartResult.Parts.Count(p => p.PartType == "DrawerFront"));
        Assert.Equal(3, context.PartResult.Parts.Count(p => p.PartType == "DrawerBoxBottom"));
        Assert.Equal(3, context.PartResult.Parts.Count(p => p.PartType == "DrawerBoxFront"));
        Assert.Equal(3, context.PartResult.Parts.Count(p => p.PartType == "DrawerBoxBack"));
        Assert.Equal(3, context.PartResult.Parts.Count(p => p.PartType == "DrawerBoxLeftSide"));
        Assert.Equal(3, context.PartResult.Parts.Count(p => p.PartType == "DrawerBoxRightSide"));

        var front = context.PartResult.Parts.First(p => p.PartType == "DrawerFront");
        Assert.Equal(Length.FromInches(16m), front.Width);
        Assert.Equal(Length.FromInches(10m), front.Height);

        var boxBottom = context.PartResult.Parts.First(p => p.PartType == "DrawerBoxBottom");
        Assert.Equal(Length.FromInches(15m), boxBottom.Width);
        Assert.Equal(Length.FromInches(23m), boxBottom.Height);
    }

    [Fact]
    public void Execute_LabelsAreStable_WhenTwoCabinetsShareType()
    {
        var firstId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000011"));
        var secondId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000012"));
        var store = CreateStore([
            CreateCabinet(firstId, "wall-30", CabinetCategory.Wall, ConstructionMethod.Frameless, 30m, 12m, 30m),
            CreateCabinet(secondId, "wall-30", CabinetCategory.Wall, ConstructionMethod.Frameless, 30m, 12m, 30m)
        ]);
        var stage = new PartGenerationStage(store);
        var context = CreateContext([CreatePlacement(firstId), CreatePlacement(secondId)]);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        var labels = context.PartResult.Parts.Select(p => p.Label).ToArray();
        Assert.Contains("wall-30-1-LeftSide", labels);
        Assert.Contains("wall-30-2-LeftSide", labels);
        Assert.DoesNotContain("wall-30-LeftSide", labels);
    }

    [Fact]
    public void Execute_ForFramelessBase_Produces6CorePartsPlusShelves()
    {
        var cabinetId = CabinetId.New();
        var store = CreateStore([
            CreateCabinet(cabinetId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless, 30m, 24m, 34.5m)
        ]);
        var stage = new PartGenerationStage(store);
        var context = CreateContext([CreatePlacement(cabinetId)]);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        var parts = context.PartResult.Parts;
        Assert.Equal(7, parts.Count);
        Assert.Equal(
            new[] { "AdjustableShelf", "Back", "Bottom", "LeftSide", "RightSide", "ToeKick", "Top" },
            parts.Select(part => part.PartType).ToArray());

        AssertPart(parts, "LeftSide", Length.FromInches(24m), Length.FromInches(34.5m));
        AssertPart(parts, "Top", Length.FromInches(28.5m), Length.FromInches(24m));
        AssertPart(parts, "Bottom", Length.FromInches(28.5m), Length.FromInches(23.25m));
        AssertPart(parts, "Back", Length.FromInches(28.5m), Length.FromInches(33m));
        AssertPart(parts, "AdjustableShelf", Length.FromInches(28.5m), Length.FromInches(23.25m));
        AssertPart(parts, "ToeKick", Length.FromInches(28.5m), Length.FromInches(4m));
    }

    [Fact]
    public void Execute_ForFaceFrameBase_AddsStilesRailsAndMullions()
    {
        var cabinetId = CabinetId.New();
        var store = CreateStore([
            CreateCabinet(cabinetId, "base-36-ff", CabinetCategory.Base, ConstructionMethod.FaceFrame, 36m, 24m, 34.5m)
        ]);
        var stage = new PartGenerationStage(store);
        var context = CreateContext([CreatePlacement(cabinetId)]);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.Equal(12, context.PartResult.Parts.Count);
        Assert.Equal(2, context.PartResult.Parts.Count(part => part.PartType == "FrameStile"));
        Assert.Equal(2, context.PartResult.Parts.Count(part => part.PartType == "FrameRail"));
        Assert.Single(context.PartResult.Parts.Where(part => part.PartType == "FrameMullion"));
    }

    [Fact]
    public void Execute_ForWallCabinet_ProducesCaseAndShelves()
    {
        var cabinetId = CabinetId.New();
        var store = CreateStore([
            CreateCabinet(cabinetId, "wall-30", CabinetCategory.Wall, ConstructionMethod.Frameless, 30m, 12m, 30m)
        ]);
        var stage = new PartGenerationStage(store);
        var context = CreateContext([CreatePlacement(cabinetId)]);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        var parts = context.PartResult.Parts;
        Assert.Equal(7, parts.Count);
        Assert.Equal(2, parts.Count(part => part.PartType == "AdjustableShelf"));
        Assert.DoesNotContain(parts, part => part.PartType == "ToeKick");
        AssertPart(parts, "Back", Length.FromInches(28.5m), Length.FromInches(28.5m));
    }

    [Fact]
    public void Execute_DimensionsAreDeterministic_AcrossRepeatedRuns()
    {
        var firstCabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var secondCabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000002"));
        var store = CreateStore([
            CreateCabinet(firstCabinetId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless, 30m, 24m, 34.5m),
            CreateCabinet(secondCabinetId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless, 30m, 24m, 34.5m)
        ]);
        var stage = new PartGenerationStage(store);

        var firstContext = CreateContext([CreatePlacement(firstCabinetId), CreatePlacement(secondCabinetId)]);
        var secondContext = CreateContext([CreatePlacement(firstCabinetId), CreatePlacement(secondCabinetId)]);

        var firstResult = stage.Execute(firstContext);
        var secondResult = stage.Execute(secondContext);

        Assert.True(firstResult.Success);
        Assert.True(secondResult.Success);
        Assert.Equal(firstContext.PartResult.Parts, secondContext.PartResult.Parts);
        Assert.Equal(
            new[]
            {
                "base-30-1-AdjustableShelf",
                "base-30-1-Back",
                "base-30-1-Bottom",
                "base-30-1-LeftSide",
                "base-30-1-RightSide",
                "base-30-1-ToeKick",
                "base-30-1-Top",
                "base-30-2-AdjustableShelf",
                "base-30-2-Back",
                "base-30-2-Bottom",
                "base-30-2-LeftSide",
                "base-30-2-RightSide",
                "base-30-2-ToeKick",
                "base-30-2-Top"
            },
            firstContext.PartResult.Parts.Select(part => part.Label).ToArray());
    }

    [Fact]
    public void Execute_WithEmptySpatialPlacements_Fails_WithPartGenEmpty()
    {
        var stage = new PartGenerationStage(new InMemoryDesignStateStore());
        var context = CreateContext([]);

        var result = stage.Execute(context);

        var issue = Assert.Single(result.Issues);
        Assert.False(result.Success);
        Assert.Equal("PART_GEN_EMPTY", issue.Code);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
    }

    [Fact]
    public void Execute_WithUnsupportedCabinet_Fails_WithBlocker()
    {
        var cabinetId = CabinetId.New();
        var store = CreateStore([
            CreateCabinet(cabinetId, "specialty-weird", CabinetCategory.Specialty, ConstructionMethod.Frameless, 24m, 24m, 34.5m)
        ]);
        var stage = new PartGenerationStage(store);
        var context = CreateContext([CreatePlacement(cabinetId)]);

        var result = stage.Execute(context);

        var issue = Assert.Single(result.Issues);
        Assert.False(result.Success);
        Assert.Equal("PART_GEN_UNSUPPORTED_CABINET", issue.Code);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
    }

    private static InMemoryDesignStateStore CreateStore(IReadOnlyList<CabinetStateRecord> cabinets)
    {
        var store = new InMemoryDesignStateStore();
        foreach (var cabinet in cabinets)
        {
            store.AddCabinet(cabinet);
        }

        return store;
    }

    private static CabinetStateRecord CreateCabinet(
        CabinetId cabinetId,
        string cabinetTypeId,
        CabinetCategory category,
        ConstructionMethod construction,
        decimal widthInches,
        decimal depthInches,
        decimal heightInches,
        IReadOnlyDictionary<string, OverrideValue>? overrides = null) =>
        new(
            cabinetId,
            cabinetTypeId,
            Length.FromInches(widthInches),
            Length.FromInches(depthInches),
            RunId.New(),
            RunSlotId.New(),
            category,
            construction,
            Length.FromInches(heightInches),
            null,
            overrides);

    private static CabinetStateRecord CreateCabinetWithOpenings(
        CabinetId cabinetId,
        string cabinetTypeId,
        CabinetCategory category,
        ConstructionMethod construction,
        decimal widthInches,
        decimal depthInches,
        decimal heightInches,
        IReadOnlyList<CabinetOpeningStateRecord> openings) =>
        new(
            cabinetId,
            cabinetTypeId,
            Length.FromInches(widthInches),
            Length.FromInches(depthInches),
            RunId.New(),
            RunSlotId.New(),
            category,
            construction,
            Length.FromInches(heightInches),
            openings,
            null);

    private static RunPlacement CreatePlacement(CabinetId cabinetId, RunId? runId = null) =>
        new(
            runId ?? new RunId(Guid.Parse("20000000-0000-0000-0000-000000000001")),
            cabinetId,
            Point2D.Origin,
            new Vector2D(1m, 0m),
            new Rect2D(Point2D.Origin, Length.FromInches(1m), Length.FromInches(1m)),
            Length.FromInches(24m));

    private static ResolutionContext CreateContext(IReadOnlyList<RunPlacement> placements)
    {
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand(),
            Mode = ResolutionMode.Full
        };

        context.SpatialResult = new SpatialResolutionResult
        {
            SlotPositionUpdates = [],
            AdjacencyChanges = [],
            RunSummaries = [],
            Placements = placements
        };

        return context;
    }

    private static void AssertPart(
        IReadOnlyList<GeneratedPart> parts,
        string partType,
        Length width,
        Length height)
    {
        var part = Assert.Single(parts.Where(candidate => candidate.PartType == partType));
        Assert.Equal(width, part.Width);
        Assert.Equal(height, part.Height);
    }

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Part Generation Stage Test", []);

        public string CommandType => "test.part_generation_stage";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
