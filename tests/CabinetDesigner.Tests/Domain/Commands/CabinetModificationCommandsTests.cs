using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Modification;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using Xunit;

namespace CabinetDesigner.Tests.Domain.Commands;

public sealed class CabinetModificationCommandsTests
{
    [Fact]
    public void ResizeCabinetCommand_ValidatesAppliesAndIsDeterministic()
    {
        var invalid = new ResizeCabinetCommand(default, Length.Zero, Length.Zero, Length.Zero, CommandOrigin.User, "resize", DateTimeOffset.UnixEpoch);
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "MISSING_CABINET");
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "INVALID_WIDTH");

        var command = new ResizeCabinetCommand(CabinetId.New(), Length.FromInches(42m), Length.FromInches(24m), Length.FromInches(34.5m), CommandOrigin.User, "resize", DateTimeOffset.UnixEpoch);
        var first = ApplyCommand(command);
        var second = ApplyCommand(command);

        Assert.Equal(Length.FromInches(42m), first.Cabinet.NominalWidth);
        Assert.Equal(Length.FromInches(24m), first.Cabinet.NominalDepth);
        Assert.Equal(Length.FromInches(34.5m), first.Cabinet.EffectiveNominalHeight);
        Assert.Equal(first.Cabinet.NominalWidth, second.Cabinet.NominalWidth);
        Assert.Equal(first.Run.Slots[0].OccupiedWidth, second.Run.Slots[0].OccupiedWidth);
        Assert.Equal(first.Run.Slots[0].CabinetId, second.Run.Slots[0].CabinetId);
        Assert.Equal(first.CabinetSnapshot, second.CabinetSnapshot);
    }

    [Fact]
    public void SetCabinetConstructionCommand_ValidatesAppliesAndIsDeterministic()
    {
        var invalid = new SetCabinetConstructionCommand(default, ConstructionMethod.FaceFrame, CommandOrigin.User, "construction", DateTimeOffset.UnixEpoch);
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "MISSING_CABINET");

        var command = new SetCabinetConstructionCommand(CabinetId.New(), ConstructionMethod.FaceFrame, CommandOrigin.User, "construction", DateTimeOffset.UnixEpoch);
        var first = ApplyCabinetMutation(command);
        var second = ApplyCabinetMutation(command);

        Assert.Equal(ConstructionMethod.FaceFrame, first.Cabinet.Construction);
        Assert.Equal(first.CabinetSnapshot, second.CabinetSnapshot);
    }

    [Fact]
    public void SetCabinetCategoryCommand_ValidatesAppliesAndIsDeterministic()
    {
        var invalid = new SetCabinetCategoryCommand(default, CabinetCategory.Tall, CommandOrigin.User, "category", DateTimeOffset.UnixEpoch);
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "MISSING_CABINET");

        var command = new SetCabinetCategoryCommand(CabinetId.New(), CabinetCategory.Tall, CommandOrigin.User, "category", DateTimeOffset.UnixEpoch);
        var first = ApplyCabinetMutation(command);
        var second = ApplyCabinetMutation(command);

        Assert.Equal(CabinetCategory.Tall, first.Cabinet.Category);
        Assert.Equal(first.CabinetSnapshot, second.CabinetSnapshot);
    }

    [Fact]
    public void AddOpeningCommand_ValidatesAppliesAndIsDeterministic()
    {
        var invalid = new AddOpeningCommand(default, OpeningType.Door, Length.Zero, Length.Zero, -1, CommandOrigin.User, "opening", DateTimeOffset.UnixEpoch);
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "MISSING_CABINET");
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "INVALID_WIDTH");
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "INVALID_HEIGHT");
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "INVALID_OPENING_INDEX");

        var command = new AddOpeningCommand(CabinetId.New(), OpeningType.Drawer, Length.FromInches(18m), Length.FromInches(12m), 0, CommandOrigin.User, "opening", DateTimeOffset.UnixEpoch);
        var first = ApplyCabinetMutation(command);
        var second = ApplyCabinetMutation(command);

        var opening = Assert.Single(first.Cabinet.EffectiveOpenings);
        Assert.Equal(OpeningType.Drawer, opening.Type);
        Assert.Equal(Length.FromInches(18m), opening.Width);
        Assert.Equal(Length.FromInches(12m), opening.Height);
        Assert.Equal(first.CabinetSnapshot, second.CabinetSnapshot);
    }

    [Fact]
    public void RemoveOpeningCommand_ValidatesAppliesAndIsDeterministic()
    {
        var invalid = new RemoveOpeningCommand(default, default, CommandOrigin.User, "remove opening", DateTimeOffset.UnixEpoch);
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "MISSING_CABINET");
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "MISSING_OPENING");

        var openingId = OpeningId.New();
        var command = new RemoveOpeningCommand(CabinetId.New(), openingId, CommandOrigin.User, "remove opening", DateTimeOffset.UnixEpoch);
        var first = ApplyCabinetMutation(command, openings: [new CabinetOpeningStateRecord(openingId.Value, 0, OpeningType.FalseFront, Length.FromInches(18m), Length.FromInches(12m))]);
        var second = ApplyCabinetMutation(command, openings: [new CabinetOpeningStateRecord(openingId.Value, 0, OpeningType.FalseFront, Length.FromInches(18m), Length.FromInches(12m))]);

        Assert.Empty(first.Cabinet.EffectiveOpenings);
        Assert.Equal(first.CabinetSnapshot, second.CabinetSnapshot);
    }

    [Fact]
    public void ReorderOpeningCommand_ValidatesAppliesAndIsDeterministic()
    {
        var invalid = new ReorderOpeningCommand(default, default, -1, CommandOrigin.User, "reorder opening", DateTimeOffset.UnixEpoch);
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "MISSING_CABINET");
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "MISSING_OPENING");
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "INVALID_OPENING_INDEX");

        var firstOpeningId = OpeningId.New();
        var secondOpeningId = OpeningId.New();
        var command = new ReorderOpeningCommand(CabinetId.New(), secondOpeningId, 0, CommandOrigin.User, "reorder opening", DateTimeOffset.UnixEpoch);
        var openings = new[]
        {
            new CabinetOpeningStateRecord(firstOpeningId.Value, 0, OpeningType.Door, Length.FromInches(18m), Length.FromInches(30m)),
            new CabinetOpeningStateRecord(secondOpeningId.Value, 1, OpeningType.Drawer, Length.FromInches(12m), Length.FromInches(8m))
        };
        var first = ApplyCabinetMutation(command, openings: openings);
        var second = ApplyCabinetMutation(command, openings: openings);

        Assert.Equal(secondOpeningId.Value, first.Cabinet.EffectiveOpenings[0].OpeningId);
        Assert.Equal(first.CabinetSnapshot, second.CabinetSnapshot);
    }

    [Fact]
    public void SetCabinetOverrideCommand_ValidatesAppliesAndIsDeterministic()
    {
        var invalid = new SetCabinetOverrideCommand(default, " ", new OverrideValue.OfString("x"), CommandOrigin.User, "override", DateTimeOffset.UnixEpoch);
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "MISSING_CABINET");
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "INVALID_OVERRIDE_KEY");

        var command = new SetCabinetOverrideCommand(CabinetId.New(), "notes", new OverrideValue.OfString("keep doors aligned"), CommandOrigin.User, "override", DateTimeOffset.UnixEpoch);
        var first = ApplyCabinetMutation(command);
        var second = ApplyCabinetMutation(command);

        var notes = Assert.IsType<OverrideValue.OfString>(first.Cabinet.EffectiveOverrides["notes"]);
        Assert.Equal("keep doors aligned", notes.Value);
        Assert.Equal(first.CabinetSnapshot, second.CabinetSnapshot);
    }

    [Fact]
    public void RemoveCabinetOverrideCommand_ValidatesAppliesAndIsDeterministic()
    {
        var invalid = new RemoveCabinetOverrideCommand(default, " ", CommandOrigin.User, "remove override", DateTimeOffset.UnixEpoch);
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "MISSING_CABINET");
        Assert.Contains(invalid.ValidateStructure(), issue => issue.Code == "INVALID_OVERRIDE_KEY");

        var command = new RemoveCabinetOverrideCommand(CabinetId.New(), "notes", CommandOrigin.User, "remove override", DateTimeOffset.UnixEpoch);
        var first = ApplyCabinetMutation(command, overrides: new Dictionary<string, OverrideValue>(StringComparer.Ordinal) { ["notes"] = new OverrideValue.OfString("keep") });
        var second = ApplyCabinetMutation(command, overrides: new Dictionary<string, OverrideValue>(StringComparer.Ordinal) { ["notes"] = new OverrideValue.OfString("keep") });

        Assert.DoesNotContain("notes", first.Cabinet.EffectiveOverrides.Keys);
        Assert.Equal(first.CabinetSnapshot, second.CabinetSnapshot);
    }

    private static AppliedMutation<TCommand> ApplyCabinetMutation<TCommand>(
        TCommand command,
        IReadOnlyList<CabinetOpeningStateRecord>? openings = null,
        IReadOnlyDictionary<string, OverrideValue>? overrides = null)
        where TCommand : IDesignCommand
    {
        var result = ApplyCommand(command, openings, overrides);
        return result;
    }

    private static AppliedMutation<TCommand> ApplyCommand<TCommand>(
        TCommand command,
        IReadOnlyList<CabinetOpeningStateRecord>? openings = null,
        IReadOnlyDictionary<string, OverrideValue>? overrides = null)
        where TCommand : IDesignCommand
    {
        var store = CreateStore(command, openings, overrides, out var cabinetId);
        var context = new ResolutionContext
        {
            Command = command,
            Mode = ResolutionMode.Full
        };

        Assert.True(new InputCaptureStage(store).Execute(context).Success);
        var deltaTracker = new InMemoryDeltaTracker();
        deltaTracker.Begin();
        Assert.True(new InteractionInterpretationStage(deltaTracker, store).Execute(context).Success);

        var cabinet = Assert.IsType<CabinetStateRecord>(store.GetCabinet(cabinetId));
        var run = Assert.IsType<CabinetRun>(store.GetRun(cabinet.RunId));
        return new AppliedMutation<TCommand>(
            store,
            cabinet,
            run,
            SnapshotCabinet(cabinet),
            SnapshotRun(run));
    }

    private static InMemoryDesignStateStore CreateStore(
        IDesignCommand command,
        IReadOnlyList<CabinetOpeningStateRecord>? openings,
        IReadOnlyDictionary<string, OverrideValue>? overrides,
        out CabinetId cabinetId)
    {
        var store = new InMemoryDesignStateStore();
        var wall = new Wall(WallId.New(), RoomId.New(), Point2D.Origin, new Point2D(96m, 0m), Thickness.Exact(Length.FromInches(4m)));
        cabinetId = ExtractCabinetId(command);
        var runId = new RunId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var initialSlotId = new RunSlotId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var slot = RunSlot.ForCabinet(initialSlotId, runId, cabinetId, Length.FromInches(30m), 0);
        var run = CabinetRun.Reconstitute(runId, wall.Id, Length.FromInches(96m), [slot]);
        store.AddWall(wall);
        store.AddRun(run, wall.StartPoint, wall.EndPoint);
        store.AddCabinet(new CabinetStateRecord(
            cabinetId,
            "base-30",
            Length.FromInches(30m),
            Length.FromInches(24m),
            run.Id,
            slot.Id,
            CabinetCategory.Base,
            ConstructionMethod.Frameless,
            Length.FromInches(34.5m),
            openings,
            overrides,
            1));
        return store;
    }

    private static CabinetId ExtractCabinetId(IDesignCommand command) =>
        command switch
        {
            ResizeCabinetCommand resize => resize.CabinetId,
            SetCabinetConstructionCommand construction => construction.CabinetId,
            SetCabinetCategoryCommand category => category.CabinetId,
            AddOpeningCommand add => add.CabinetId,
            RemoveOpeningCommand remove => remove.CabinetId,
            ReorderOpeningCommand reorder => reorder.CabinetId,
            SetCabinetOverrideCommand set => set.CabinetId,
            RemoveCabinetOverrideCommand remove => remove.CabinetId,
            _ => throw new InvalidOperationException($"Unsupported command type {command.GetType().Name}.")
        };

    private static string SnapshotCabinet(CabinetStateRecord cabinet) =>
        string.Join("|", [
            cabinet.CabinetTypeId,
            cabinet.Category.ToString(),
            cabinet.Construction.ToString(),
            cabinet.NominalWidth.Inches.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            cabinet.NominalDepth.Inches.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            cabinet.EffectiveNominalHeight.Inches.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            string.Join(",", cabinet.EffectiveOpenings.OrderBy(opening => opening.Index).Select(opening => $"{opening.Index}:{opening.OpeningId}:{opening.Type}:{opening.Width.Inches:0.###}:{opening.Height.Inches:0.###}")),
            string.Join(",", cabinet.EffectiveOverrides.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}:{entry.Value.GetType().Name}:{entry.Value}"))
        ]);

    private static string SnapshotRun(CabinetRun run) =>
        string.Join("|", run.Slots.Select(slot => $"{slot.SlotIndex}:{slot.Id}:{slot.CabinetId}:{slot.OccupiedWidth.Inches:0.###}"));

    private sealed record AppliedMutation<TCommand>(
        InMemoryDesignStateStore Store,
        CabinetStateRecord Cabinet,
        CabinetRun Run,
        string CabinetSnapshot,
        string RunSnapshot);
}
