using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Handlers;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Commands.Modification;
using CabinetDesigner.Domain.Commands.Structural;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using Xunit;

namespace CabinetDesigner.Tests.Application.Services;

public sealed class RunServiceTests
{
    [Fact]
    public async Task AddCabinetAsync_ConvertsDecimalInchesToLengthBeforeCommandConstruction()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());

        await service.AddCabinetAsync(new AddCabinetRequestDto(Guid.NewGuid(), "base-36", 36.5m, "EndOfRun"));

        var command = Assert.IsType<AddCabinetToRunCommand>(handler.LastCommand);
        Assert.Equal(36.5m, command.NominalWidth.Inches);
    }

    [Fact]
    public async Task AddCabinetAsync_SetsOriginToUser()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());

        await service.AddCabinetAsync(new AddCabinetRequestDto(Guid.NewGuid(), "base-36", 36m, "EndOfRun"));

        var command = Assert.IsType<AddCabinetToRunCommand>(handler.LastCommand);
        Assert.Equal(CommandOrigin.User, command.Metadata.Origin);
    }

    [Fact]
    public async Task AddCabinetAsync_SetsTimestampFromClock()
    {
        var now = new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero);
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(now), new InMemoryDesignStateStore());

        await service.AddCabinetAsync(new AddCabinetRequestDto(Guid.NewGuid(), "base-36", 36m, "EndOfRun"));

        var command = Assert.IsType<AddCabinetToRunCommand>(handler.LastCommand);
        Assert.Equal(now, command.Metadata.Timestamp);
    }

    [Fact]
    public async Task ResizeCabinetAsync_CapturesPreviousWidthFromRequest()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());

        await service.ResizeCabinetAsync(new ResizeCabinetRequestDto(Guid.NewGuid(), 30m, 33m));

        var command = Assert.IsType<ResizeCabinetCommand>(handler.LastCommand);
        Assert.Equal(30m, command.PreviousNominalWidth.Inches);
        Assert.Equal(33m, command.NewNominalWidth.Inches);
    }

    [Fact]
    public async Task CreateRunAsync_ConstructsPoint2DFromRawCoordinates()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());

        await service.CreateRunAsync(new CreateRunRequestDto("wall-1", 1m, 2m, 3m, 4m));

        var command = Assert.IsType<CreateRunCommand>(handler.LastCommand);
        Assert.Equal(1m, command.StartPoint.X);
        Assert.Equal(2m, command.StartPoint.Y);
        Assert.Equal(3m, command.EndPoint.X);
        Assert.Equal(4m, command.EndPoint.Y);
    }

    [Fact]
    public async Task InsertCabinetAsync_UsesHandlerDispatchPath()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());

        await service.InsertCabinetAsync(
            new InsertCabinetRequestDto(Guid.NewGuid(), "base-24", 24m, 1, Guid.NewGuid(), Guid.NewGuid()));

        Assert.IsType<InsertCabinetIntoRunCommand>(handler.LastCommand);
        Assert.Equal(1, handler.ExecuteCalls);
    }

    [Fact]
    public async Task DeleteRunAsync_UsesDeleteRunCommand()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());
        var runId = RunId.New();

        await service.DeleteRunAsync(runId);

        var command = Assert.IsType<DeleteRunCommand>(handler.LastCommand);
        Assert.Equal(runId, command.RunId);
        Assert.Equal(1, handler.ExecuteCalls);
    }

    [Fact]
    public async Task DeleteRunAsync_RemovesRun_WhenEmpty()
    {
        var stateStore = new InMemoryDesignStateStore();
        var (_, wall) = CreateRoomAndWall(stateStore);
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(72m));
        stateStore.AddRun(run, wall.StartPoint, wall.EndPoint);
        var service = new RunService(new RecordingDesignCommandHandler(), new FixedClock(DateTimeOffset.UnixEpoch), stateStore);

        await service.DeleteRunAsync(run.Id, default);

        Assert.Null(stateStore.GetRun(run.Id));
    }

    [Fact]
    public async Task MoveCabinetAsync_UsesHandlerDispatchPath()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());

        await service.MoveCabinetAsync(
            new MoveCabinetRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "AtIndex", 2));

        var command = Assert.IsType<MoveCabinetCommand>(handler.LastCommand);
        Assert.Equal(2, command.TargetIndex);
        Assert.Equal(1, handler.ExecuteCalls);
    }

    [Fact]
    public async Task SetCabinetOverrideAsync_MapsDecimalInchesToLengthOverride()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());
        var cabinetId = Guid.NewGuid();

        await service.SetCabinetOverrideAsync(new SetCabinetOverrideRequestDto(
            cabinetId,
            "nominal_width",
            new OverrideValueDto.OfDecimalInches(33.5m)));

        var command = Assert.IsType<SetCabinetOverrideCommand>(handler.LastCommand);
        Assert.Equal(new CabinetId(cabinetId), command.CabinetId);
        Assert.Equal("nominal_width", command.OverrideKey);
        var value = Assert.IsType<OverrideValue.OfLength>(command.Value);
        Assert.Equal(33.5m, value.Value.Inches);
        Assert.Equal(1, handler.ExecuteCalls);
    }

    [Fact]
    public async Task SetCabinetOverrideAsync_UpdatesOverride()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());
        var cabinetId = Guid.NewGuid();

        await service.SetCabinetOverrideAsync(new SetCabinetOverrideRequestDto(
            cabinetId,
            "nominal_width",
            new OverrideValueDto.OfDecimalInches(33.5m)));

        var command = Assert.IsType<SetCabinetOverrideCommand>(handler.LastCommand);
        Assert.Equal(new CabinetId(cabinetId), command.CabinetId);
        Assert.Equal("nominal_width", command.OverrideKey);
        var value = Assert.IsType<OverrideValue.OfLength>(command.Value);
        Assert.Equal(33.5m, value.Value.Inches);
    }

    [Fact]
    public async Task SetCabinetOverrideAsync_UsesMaterialOverrideValue()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());
        var materialId = Guid.NewGuid();

        await service.SetCabinetOverrideAsync(new SetCabinetOverrideRequestDto(
            Guid.NewGuid(),
            "material.LeftSide",
            new OverrideValueDto.OfMaterialId(materialId)));

        var command = Assert.IsType<SetCabinetOverrideCommand>(handler.LastCommand);
        var value = Assert.IsType<OverrideValue.OfMaterialId>(command.Value);
        Assert.Equal(new MaterialId(materialId), value.Value);
    }

    [Fact]
    public async Task SetCabinetOverrideAsync_RejectsEmptyKey()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetCabinetOverrideAsync(new SetCabinetOverrideRequestDto(
            Guid.NewGuid(),
            string.Empty,
            new OverrideValueDto.OfString("x"))));
    }

    [Fact]
    public async Task CreateRunAsync_PersistsRun_AtRequestedPosition()
    {
        var stateStore = new InMemoryDesignStateStore();
        var (room, wall) = CreateRoomAndWall(stateStore);
        var service = new RunService(new RecordingDesignCommandHandler(), new FixedClock(DateTimeOffset.UnixEpoch), stateStore);

        var run = await service.CreateRunAsync(room.Id, wall.Id, Length.FromInches(12m), Length.FromInches(24m));

        Assert.Equal(wall.Id, run.WallId);
        var spatialInfo = Assert.IsType<RunSpatialInfo>(stateStore.GetRunSpatialInfo(run.Id));
        Assert.Equal(12m, spatialInfo.StartWorld.X);
        Assert.Equal(0m, spatialInfo.StartWorld.Y);
        Assert.Equal(36m, spatialInfo.EndWorld.X);
        Assert.Equal(0m, spatialInfo.EndWorld.Y);
    }

    [Fact]
    public async Task PlaceCabinetAsync_AppendsToEndOfRun_InInsertionOrder()
    {
        var stateStore = new InMemoryDesignStateStore();
        var (_, wall) = CreateRoomAndWall(stateStore);
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(72m));
        stateStore.AddRun(run, wall.StartPoint, wall.EndPoint);
        var service = new RunService(new RecordingDesignCommandHandler(), new FixedClock(DateTimeOffset.UnixEpoch), stateStore);

        var first = await service.PlaceCabinetAsync(run.Id, "base-standard-24");
        var second = await service.PlaceCabinetAsync(run.Id, "base-drawer-18");

        var storedRun = Assert.IsType<CabinetRun>(stateStore.GetRun(run.Id));
        Assert.Equal(2, storedRun.CabinetCount);
        Assert.Equal(first.Id, storedRun.Slots[0].CabinetId);
        Assert.Equal(second.Id, storedRun.Slots[1].CabinetId);
        Assert.Equal(0, storedRun.Slots[0].SlotIndex);
        Assert.Equal(1, storedRun.Slots[1].SlotIndex);
    }

    [Fact]
    public async Task PlaceCabinetAsync_ExceedsCapacity_Throws_WithStableMessage()
    {
        var stateStore = new InMemoryDesignStateStore();
        var (_, wall) = CreateRoomAndWall(stateStore);
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(24m));
        stateStore.AddRun(run, wall.StartPoint, wall.EndPoint);
        var service = new RunService(new RecordingDesignCommandHandler(), new FixedClock(DateTimeOffset.UnixEpoch), stateStore);

        await service.PlaceCabinetAsync(run.Id, "base-standard-24");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PlaceCabinetAsync(run.Id, "base-drawer-18"));

        Assert.Equal("Cannot place cabinet: run capacity exceeded.", ex.Message);
    }

    [Fact]
    public async Task DeleteCabinetAsync_RemovesCabinet_AndCompactsRun()
    {
        var stateStore = new InMemoryDesignStateStore();
        var (_, wall) = CreateRoomAndWall(stateStore);
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(72m));
        stateStore.AddRun(run, wall.StartPoint, wall.EndPoint);
        var service = new RunService(new RecordingDesignCommandHandler(), new FixedClock(DateTimeOffset.UnixEpoch), stateStore);

        var first = await service.PlaceCabinetAsync(run.Id, "base-standard-24");
        var second = await service.PlaceCabinetAsync(run.Id, "base-drawer-18");

        await service.DeleteCabinetAsync(first.Id);

        Assert.Null(stateStore.GetCabinet(first.Id));
        var storedRun = Assert.IsType<CabinetRun>(stateStore.GetRun(run.Id));
        Assert.Single(storedRun.Slots.Where(slot => slot.SlotType == RunSlotType.Cabinet));
        Assert.Equal(second.Id, storedRun.Slots[0].CabinetId);
        Assert.Equal(0, storedRun.Slots[0].SlotIndex);
    }

    [Fact]
    public async Task DeleteRunAsync_Rejects_WhenRunHasCabinets()
    {
        var stateStore = new InMemoryDesignStateStore();
        var (_, wall) = CreateRoomAndWall(stateStore);
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(72m));
        stateStore.AddRun(run, wall.StartPoint, wall.EndPoint);
        var service = new RunService(new RecordingDesignCommandHandler(), new FixedClock(DateTimeOffset.UnixEpoch), stateStore);

        await service.PlaceCabinetAsync(run.Id, "base-standard-24");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteRunAsync(run.Id, default));

        Assert.Equal("RUN_NOT_EMPTY", ex.Message);
        Assert.NotNull(stateStore.GetRun(run.Id));
    }

    [Fact]
    public void GetRunSummary_ForKnownRunId_ReturnsCorrectRunId()
    {
        var stateStore = new InMemoryDesignStateStore();
        var wall = new Wall(WallId.New(), RoomId.New(), Point2D.Origin, new Point2D(96m, 0m), Thickness.Exact(Length.FromInches(4m)));
        stateStore.AddWall(wall);
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(96m));
        stateStore.AddRun(run, wall.StartPoint, wall.EndPoint);

        var service = new RunService(new RecordingDesignCommandHandler(), new FixedClock(DateTimeOffset.UnixEpoch), stateStore);
        var summary = service.GetRunSummary(run.Id);

        Assert.Equal(run.Id.Value, summary.RunId);
    }

    [Fact]
    public void GetRunSummary_ForKnownRunWithCabinets_ReturnsCorrectCabinetCount()
    {
        var stateStore = new InMemoryDesignStateStore();
        var wall = new Wall(WallId.New(), RoomId.New(), Point2D.Origin, new Point2D(96m, 0m), Thickness.Exact(Length.FromInches(4m)));
        stateStore.AddWall(wall);
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(96m));
        run.AppendCabinet(CabinetId.New(), Length.FromInches(24m));
        run.AppendCabinet(CabinetId.New(), Length.FromInches(36m));
        stateStore.AddRun(run, wall.StartPoint, wall.EndPoint);

        var service = new RunService(new RecordingDesignCommandHandler(), new FixedClock(DateTimeOffset.UnixEpoch), stateStore);
        var summary = service.GetRunSummary(run.Id);

        Assert.Equal(2, summary.CabinetCount);
        Assert.Equal(60m, summary.TotalNominalWidthInches);
    }

    [Fact]
    public void GetRunSummary_ForUnknownRunId_ThrowsKeyNotFoundException()
    {
        var stateStore = new InMemoryDesignStateStore();
        var service = new RunService(new RecordingDesignCommandHandler(), new FixedClock(DateTimeOffset.UnixEpoch), stateStore);

        Assert.Throws<KeyNotFoundException>(() => service.GetRunSummary(RunId.New()));
    }

    [Fact]
    public async Task AddCabinetAsync_WithInvalidPlacementString_ThrowsArgumentException()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.AddCabinetAsync(new AddCabinetRequestDto(Guid.NewGuid(), "base-36", 36m, "NotAPlacement")));

        Assert.Contains("NotAPlacement", ex.Message);
        Assert.Contains("Expected one of:", ex.Message);
        Assert.Contains("StartOfRun", ex.Message);
        Assert.Equal(0, handler.ExecuteCalls);
    }

    [Fact]
    public async Task MoveCabinetAsync_WithInvalidTargetPlacementString_ThrowsArgumentException()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.MoveCabinetAsync(new MoveCabinetRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "BadValue", null)));

        Assert.Contains("BadValue", ex.Message);
        Assert.Contains("Expected one of:", ex.Message);
        Assert.Contains("StartOfRun", ex.Message);
        Assert.Equal(0, handler.ExecuteCalls);
    }

    [Fact]
    public async Task AddCabinetAsync_WithNumericPlacementString_ThrowsArgumentException()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.AddCabinetAsync(new AddCabinetRequestDto(Guid.NewGuid(), "base-36", 36m, "0")));

        Assert.Contains("0", ex.Message);
        Assert.Equal(0, handler.ExecuteCalls);
    }

    [Fact]
    public async Task MoveCabinetAsync_WithNumericTargetPlacementString_ThrowsArgumentException()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.MoveCabinetAsync(new MoveCabinetRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "1", null)));

        Assert.Contains("1", ex.Message);
        Assert.Equal(0, handler.ExecuteCalls);
    }

    [Fact]
    public async Task AddCabinetAsync_WithWallCabinet_InfersCategoryAsWall()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());

        await service.AddCabinetAsync(new AddCabinetRequestDto(Guid.NewGuid(), "wall-36", 36m, "EndOfRun"));

        var command = Assert.IsType<AddCabinetToRunCommand>(handler.LastCommand);
        Assert.Equal(CabinetCategory.Wall, command.Category);
        Assert.Equal(ConstructionMethod.Frameless, command.Construction);
    }

    [Fact]
    public async Task AddCabinetAsync_WithTallCabinet_InfersCategoryAsTall()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());

        await service.AddCabinetAsync(new AddCabinetRequestDto(Guid.NewGuid(), "tall-24", 24m, "EndOfRun"));

        var command = Assert.IsType<AddCabinetToRunCommand>(handler.LastCommand);
        Assert.Equal(CabinetCategory.Tall, command.Category);
    }

    [Fact]
    public async Task AddCabinetAsync_WithBaseCabinet_InfersCategoryAsBase()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch), new InMemoryDesignStateStore());

        await service.AddCabinetAsync(new AddCabinetRequestDto(Guid.NewGuid(), "base-30", 30m, "EndOfRun"));

        var command = Assert.IsType<AddCabinetToRunCommand>(handler.LastCommand);
        Assert.Equal(CabinetCategory.Base, command.Category);
    }

    private sealed class RecordingDesignCommandHandler : IDesignCommandHandler
    {
        public int ExecuteCalls { get; private set; }

        public IDesignCommand? LastCommand { get; private set; }

        public Task<CommandResultDto> ExecuteAsync(IDesignCommand command, CancellationToken ct = default)
        {
            ExecuteCalls++;
            LastCommand = command;
            return Task.FromResult(new CommandResultDto(Guid.NewGuid(), command.CommandType, true, [], command.Metadata.AffectedEntityIds, []));
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }

    private static (Room Room, Wall Wall) CreateRoomAndWall(IDesignStateStore stateStore)
    {
        var revisionId = RevisionId.New();
        var room = new Room(RoomId.New(), revisionId, "Kitchen", Length.FromInches(96m));
        var wall = room.AddWall(Point2D.Origin, new Point2D(96m, 0m), Thickness.Exact(Length.FromInches(4m)));
        stateStore.AddRoom(room);
        stateStore.AddWall(wall);
        return (room, wall);
    }
}
