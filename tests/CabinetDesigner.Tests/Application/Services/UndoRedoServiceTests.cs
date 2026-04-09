using CabinetDesigner.Application;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Application.Services;

public sealed class UndoRedoServiceTests
{
    [Fact]
    public void Undo_WhenNothingToUndo_ReturnsNoOpDto()
    {
        var service = new UndoRedoService(new RecordingOrchestrator(), new RecordingEventBus(), new RecordingUndoStack());

        var result = service.Undo();

        Assert.False(result.Success);
        Assert.Equal("undo", result.CommandType);
    }

    [Fact]
    public void Undo_WhenSuccessful_PublishesUndoAppliedEvent()
    {
        var orchestrator = new RecordingOrchestrator
        {
            UndoResult = CommandResult.Succeeded(CreateMetadata(), [], [])
        };
        var eventBus = new RecordingEventBus();
        var service = new UndoRedoService(orchestrator, eventBus, new RecordingUndoStack { CanUndoValue = true });

        var result = service.Undo();

        Assert.True(result.Success);
        Assert.IsType<UndoAppliedEvent>(Assert.Single(eventBus.PublishedEvents));
    }

    [Fact]
    public void Redo_WhenNothingToRedo_ReturnsNoOpDto()
    {
        var service = new UndoRedoService(new RecordingOrchestrator(), new RecordingEventBus(), new RecordingUndoStack());

        var result = service.Redo();

        Assert.False(result.Success);
        Assert.Equal("redo", result.CommandType);
    }

    [Fact]
    public void Redo_WhenSuccessful_PublishesRedoAppliedEvent()
    {
        var orchestrator = new RecordingOrchestrator
        {
            RedoResult = CommandResult.Succeeded(CreateMetadata(), [], [])
        };
        var eventBus = new RecordingEventBus();
        var service = new UndoRedoService(orchestrator, eventBus, new RecordingUndoStack { CanRedoValue = true });

        var result = service.Redo();

        Assert.True(result.Success);
        Assert.IsType<RedoAppliedEvent>(Assert.Single(eventBus.PublishedEvents));
    }

    [Fact]
    public void CanUndo_DelegatesToUndoStack()
    {
        var undoStack = new RecordingUndoStack { CanUndoValue = true };
        var service = new UndoRedoService(new RecordingOrchestrator(), new RecordingEventBus(), undoStack);

        Assert.True(service.CanUndo);
    }

    private static CommandMetadata CreateMetadata() =>
        CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.Undo, "Undo", [RunId.New().Value.ToString()]);

    private sealed class RecordingOrchestrator : IResolutionOrchestrator
    {
        public CommandResult? UndoResult { get; set; }

        public CommandResult? RedoResult { get; set; }

        public CommandResult Execute(IDesignCommand command) => throw new NotSupportedException();

        public CabinetDesigner.Application.Pipeline.PreviewResult Preview(IDesignCommand command) => throw new NotSupportedException();

        public CommandResult? Undo() => UndoResult;

        public CommandResult? Redo() => RedoResult;
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

    private sealed class RecordingUndoStack : IUndoStack
    {
        public bool CanUndoValue { get; set; }

        public bool CanRedoValue { get; set; }

        public bool CanUndo => CanUndoValue;

        public bool CanRedo => CanRedoValue;

        public IReadOnlyList<UndoEntry> Journal => [];

        public void Push(UndoEntry entry) => throw new NotSupportedException();

        public UndoEntry? Undo() => throw new NotSupportedException();

        public UndoEntry? Redo() => throw new NotSupportedException();

        public void Clear()
        {
        }
    }
}
