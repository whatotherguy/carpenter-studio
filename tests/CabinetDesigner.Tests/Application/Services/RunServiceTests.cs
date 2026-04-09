using System.Threading.Tasks;
using System.Threading;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Handlers;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Commands.Modification;
using CabinetDesigner.Domain.Commands.Structural;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Application.Services;

public sealed class RunServiceTests
{
    [Fact]
    public async Task AddCabinetAsync_ConvertsDecimalInchesToLengthBeforeCommandConstruction()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch));

        await service.AddCabinetAsync(new AddCabinetRequestDto(Guid.NewGuid(), "base-36", 36.5m, "EndOfRun"));

        var command = Assert.IsType<AddCabinetToRunCommand>(handler.LastCommand);
        Assert.Equal(36.5m, command.NominalWidth.Inches);
    }

    [Fact]
    public async Task AddCabinetAsync_SetsOriginToUser()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch));

        await service.AddCabinetAsync(new AddCabinetRequestDto(Guid.NewGuid(), "base-36", 36m, "EndOfRun"));

        var command = Assert.IsType<AddCabinetToRunCommand>(handler.LastCommand);
        Assert.Equal(CommandOrigin.User, command.Metadata.Origin);
    }

    [Fact]
    public async Task AddCabinetAsync_SetsTimestampFromClock()
    {
        var now = new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero);
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(now));

        await service.AddCabinetAsync(new AddCabinetRequestDto(Guid.NewGuid(), "base-36", 36m, "EndOfRun"));

        var command = Assert.IsType<AddCabinetToRunCommand>(handler.LastCommand);
        Assert.Equal(now, command.Metadata.Timestamp);
    }

    [Fact]
    public async Task ResizeCabinetAsync_CapturesPreviousWidthFromRequest()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch));

        await service.ResizeCabinetAsync(new ResizeCabinetRequestDto(Guid.NewGuid(), 30m, 33m));

        var command = Assert.IsType<ResizeCabinetCommand>(handler.LastCommand);
        Assert.Equal(30m, command.PreviousNominalWidth.Inches);
        Assert.Equal(33m, command.NewNominalWidth.Inches);
    }

    [Fact]
    public async Task CreateRunAsync_ConstructsPoint2DFromRawCoordinates()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch));

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
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch));

        await service.InsertCabinetAsync(
            new InsertCabinetRequestDto(Guid.NewGuid(), "base-24", 24m, 1, Guid.NewGuid(), Guid.NewGuid()));

        Assert.IsType<InsertCabinetIntoRunCommand>(handler.LastCommand);
        Assert.Equal(1, handler.ExecuteCalls);
    }

    [Fact]
    public async Task MoveCabinetAsync_UsesHandlerDispatchPath()
    {
        var handler = new RecordingDesignCommandHandler();
        var service = new RunService(handler, new FixedClock(DateTimeOffset.UnixEpoch));

        await service.MoveCabinetAsync(
            new MoveCabinetRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "AtIndex", 2));

        var command = Assert.IsType<MoveCabinetCommand>(handler.LastCommand);
        Assert.Equal(2, command.TargetIndex);
        Assert.Equal(1, handler.ExecuteCalls);
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
}
