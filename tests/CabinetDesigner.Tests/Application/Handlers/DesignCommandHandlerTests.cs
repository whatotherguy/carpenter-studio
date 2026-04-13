using System.Threading;
using CabinetDesigner.Application;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Handlers;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using System.Threading.Tasks;
using Xunit;

namespace CabinetDesigner.Tests.Application.Handlers;

public sealed class DesignCommandHandlerTests
{
    [Fact]
    public async Task Execute_WithBlockingStructuralIssue_ReturnsRejectedDto_ViaOrchestrator()
    {
        var orchestrator = new RecordingOrchestrator
        {
            ExecuteResult = CommandResult.Rejected(
                CreateMetadata(),
                [new ValidationIssue(ValidationSeverity.Error, "INVALID", "Invalid command.")])
        };
        var eventBus = new RecordingEventBus();
        var persistence = new RecordingPersistencePort();
        var handler = new DesignCommandHandler(orchestrator, eventBus, persistence);
        var command = new TestDesignCommand(
            "layout.test",
            [new ValidationIssue(ValidationSeverity.Error, "INVALID", "Invalid command.")]);

        var result = await handler.ExecuteAsync(command);

        Assert.False(result.Success);
        Assert.Equal("layout.test", result.CommandType);
        Assert.Equal(1, orchestrator.ExecuteCalls);
        Assert.Equal(0, persistence.CommitCalls);
        Assert.Equal(0, orchestrator.PreviewCalls);
        Assert.Empty(eventBus.PublishedEvents);
    }

    [Fact]
    public async Task Execute_WithStructuralIssueInCommand_OrchestratorPerformsValidation_Regression()
    {
        var structuralIssue = new ValidationIssue(ValidationSeverity.Error, "STRUCT_ERROR", "Structural issue in command");
        var orchestrator = new RecordingOrchestrator
        {
            ExecuteResult = CommandResult.Rejected(
                CreateMetadata(),
                [structuralIssue])
        };
        var eventBus = new RecordingEventBus();
        var persistence = new RecordingPersistencePort();
        var handler = new DesignCommandHandler(orchestrator, eventBus, persistence);
        var command = new TestDesignCommand("layout.test", [structuralIssue]);

        var result = await handler.ExecuteAsync(command);

        Assert.False(result.Success);
        Assert.Equal("STRUCT_ERROR", Assert.Single(result.Issues).Code);
        Assert.Equal(1, orchestrator.ExecuteCalls);
        Assert.Equal(0, persistence.CommitCalls);
        Assert.Empty(eventBus.PublishedEvents);
    }

    [Fact]
    public async Task Execute_WhenOrchestratorSucceeds_PublishesDesignChangedEvent()
    {
        var orchestrator = new RecordingOrchestrator
        {
            ExecuteResult = CommandResult.Succeeded(CreateMetadata(), [], [])
        };
        var eventBus = new RecordingEventBus();
        var persistence = new RecordingPersistencePort();
        var handler = new DesignCommandHandler(orchestrator, eventBus, persistence);
        var command = new TestDesignCommand("layout.test", []);

        var result = await handler.ExecuteAsync(command);

        Assert.True(result.Success);
        Assert.Equal(1, orchestrator.ExecuteCalls);
        Assert.Equal(1, persistence.CommitCalls);
        Assert.Equal(0, orchestrator.PreviewCalls);
        var published = Assert.Single(eventBus.PublishedEvents);
        var designChanged = Assert.IsType<DesignChangedEvent>(published);
        Assert.Equal(result, designChanged.Result);
    }

    [Fact]
    public async Task Execute_WhenOrchestratorFails_DoesNotPublishEvent()
    {
        var orchestrator = new RecordingOrchestrator
        {
            ExecuteResult = CommandResult.Failed(
                CreateMetadata(),
                [new ValidationIssue(ValidationSeverity.Error, "FAIL", "Pipeline failed.")])
        };
        var eventBus = new RecordingEventBus();
        var persistence = new RecordingPersistencePort();
        var handler = new DesignCommandHandler(orchestrator, eventBus, persistence);

        var result = await handler.ExecuteAsync(new TestDesignCommand("layout.test", []));

        Assert.False(result.Success);
        Assert.Equal(1, orchestrator.ExecuteCalls);
        Assert.Equal(0, persistence.CommitCalls);
        Assert.Empty(eventBus.PublishedEvents);
    }

    [Fact]
    public async Task Execute_WhenOrchestratorFails_ReturnsDtoWithSuccessFalse()
    {
        var orchestrator = new RecordingOrchestrator
        {
            ExecuteResult = CommandResult.Failed(
                CreateMetadata(),
                [new ValidationIssue(ValidationSeverity.Error, "FAIL", "Pipeline failed.")])
        };
        var handler = new DesignCommandHandler(orchestrator, new RecordingEventBus(), new RecordingPersistencePort());

        var result = await handler.ExecuteAsync(new TestDesignCommand("layout.test", []));

        Assert.False(result.Success);
        Assert.Equal("FAIL", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public async Task Execute_NullCommand_ThrowsArgumentNullException()
    {
        var handler = new DesignCommandHandler(new RecordingOrchestrator(), new RecordingEventBus(), new RecordingPersistencePort());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.ExecuteAsync(null!));
    }

    [Fact]
    public async Task Execute_WhenPersistenceThrows_LogsErrorAndRethrows()
    {
        var orchestrator = new RecordingOrchestrator
        {
            ExecuteResult = CommandResult.Succeeded(CreateMetadata(), [], [])
        };
        var persistence = new ThrowingPersistencePort();
        var logger = new CapturingLogger();
        var handler = new DesignCommandHandler(orchestrator, new RecordingEventBus(), persistence, logger);
        var command = new TestDesignCommand("layout.test", []);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecuteAsync(command));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Persistence", entry.Category);
        Assert.NotNull(entry.Exception);
    }

    private static CommandMetadata CreateMetadata() =>
        CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Test", ["entity-1"]);

    private sealed class RecordingOrchestrator : IResolutionOrchestrator
    {
        public int ExecuteCalls { get; private set; }

        public int PreviewCalls { get; private set; }

        public CommandResult ExecuteResult { get; set; } = CommandResult.Succeeded(CreateMetadata(), [], []);

        public PreviewResult PreviewResult { get; set; } = PreviewResult.Succeeded(
            CreateMetadata(),
            new CabinetDesigner.Application.Pipeline.StageResults.SpatialResolutionResult
            {
                SlotPositionUpdates = [],
                AdjacencyChanges = [],
                RunSummaries = [],
                Placements = []
            });

        public CommandResult Execute(IDesignCommand command)
        {
            ExecuteCalls++;
            return ExecuteResult;
        }

        public PreviewResult Preview(IDesignCommand command)
        {
            PreviewCalls++;
            return PreviewResult;
        }

        public CommandResult? Undo() => null;

        public CommandResult? Redo() => null;
    }

    private sealed class RecordingEventBus : IApplicationEventBus
    {
        public List<IApplicationEvent> PublishedEvents { get; } = [];

        public void Publish<TEvent>(TEvent @event) where TEvent : IApplicationEvent => PublishedEvents.Add(@event);

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IApplicationEvent
        {
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IApplicationEvent
        {
        }
    }

    private sealed class RecordingPersistencePort : ICommandPersistencePort
    {
        public int CommitCalls { get; private set; }

        public Task CommitCommandAsync(IDesignCommand command, CommandResult result, CancellationToken ct = default)
        {
            CommitCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPersistencePort : ICommandPersistencePort
    {
        public Task CommitCommandAsync(IDesignCommand command, CommandResult result, CancellationToken ct = default) =>
            Task.FromException(new InvalidOperationException("simulated persistence failure"));
    }

    private sealed class CapturingLogger : IAppLogger
    {
        public List<LogEntry> Entries { get; } = [];

        public void Log(LogEntry entry) => Entries.Add(entry);
    }

    private sealed record TestDesignCommand(string Type, IReadOnlyList<ValidationIssue> StructuralIssues) : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Test", [CabinetId.New().Value.ToString()]);

        public string CommandType => Type;

        public IReadOnlyList<ValidationIssue> ValidateStructure() => StructuralIssues;
    }
}
