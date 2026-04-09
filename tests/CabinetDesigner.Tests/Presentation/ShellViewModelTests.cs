using System.Threading;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Presentation.Projection;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Editor;
using CabinetDesigner.Presentation.ViewModels;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class ShellViewModelTests
{
    [Fact]
    public async Task SaveCommand_DelegatesToProjectService_AndRaisesPropertyChanges()
    {
        using var shell = CreateShellViewModel(out var projectService, out _, out var eventBus);
        var project = new ProjectSummaryDto(Guid.NewGuid(), "Shop A", "C:\\shop.cab", DateTimeOffset.UtcNow, "Rev 1", true);
        projectService.SeedCurrentProject(project);
        eventBus.Publish(new ProjectOpenedEvent(project));

        var changedProperties = new List<string>();
        shell.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
            {
                changedProperties.Add(args.PropertyName);
            }
        };

        await shell.SaveCommand.ExecuteAsync();

        Assert.True(projectService.SaveCalled);
        Assert.Contains(nameof(ShellViewModel.ActiveProject), changedProperties);
        Assert.False(shell.ActiveProject!.HasUnsavedChanges);
    }

    [Fact]
    public void ProjectOpenedEvent_UpdatesWindowTitle_AndCommandState()
    {
        using var shell = CreateShellViewModel(out _, out var undoRedoService, out var eventBus);
        undoRedoService.CanUndoValue = true;

        eventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 3",
            false)));

        Assert.True(shell.HasActiveProject);
        Assert.Equal("Demo Project - Carpenter Studio", shell.WindowTitle);
        Assert.True(shell.SaveCommand.CanExecute(null));
        Assert.True(shell.UndoCommand.CanExecute(null));
    }

    private static ShellViewModel CreateShellViewModel(
        out RecordingProjectService projectService,
        out RecordingUndoRedoService undoRedoService,
        out ApplicationEventBus eventBus)
    {
        projectService = new RecordingProjectService();
        undoRedoService = new RecordingUndoRedoService();
        eventBus = new ApplicationEventBus();

        var canvas = new EditorCanvasViewModel(
            new RecordingRunService(),
            eventBus,
            new RecordingSceneProjector(),
            new TestEditorCanvasSession(),
            new DefaultHitTester(),
            new RecordingCanvasHost());

        return new ShellViewModel(projectService, undoRedoService, eventBus, canvas);
    }

    private sealed class RecordingProjectService : IProjectService
    {
        public ProjectSummaryDto? CurrentProject { get; private set; }

        public bool SaveCalled { get; private set; }

        public Task<ProjectSummaryDto> OpenProjectAsync(string filePath, CancellationToken ct = default) =>
            Task.FromResult(CurrentProject = new ProjectSummaryDto(Guid.NewGuid(), "Opened", filePath, DateTimeOffset.UtcNow, "Rev 1", false));

        public Task<ProjectSummaryDto> CreateProjectAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(CurrentProject = new ProjectSummaryDto(Guid.NewGuid(), name, string.Empty, DateTimeOffset.UtcNow, "Rev 1", false));

        public Task SaveAsync(CancellationToken ct = default)
        {
            SaveCalled = true;
            if (CurrentProject is not null)
            {
                CurrentProject = CurrentProject with { HasUnsavedChanges = false };
            }

            return Task.CompletedTask;
        }

        public Task<RevisionDto> SaveRevisionAsync(string label, CancellationToken ct = default) => throw new NotImplementedException();

        public Task CloseAsync()
        {
            CurrentProject = null;
            return Task.CompletedTask;
        }

        public void SeedCurrentProject(ProjectSummaryDto project) => CurrentProject = project;
    }

    private sealed class RecordingUndoRedoService : IUndoRedoService
    {
        public bool CanUndoValue { get; set; }

        public bool CanRedoValue { get; set; }

        public bool CanUndo => CanUndoValue;

        public bool CanRedo => CanRedoValue;

        public CommandResultDto Undo() => new(Guid.NewGuid(), "undo", true, [], [], []);

        public CommandResultDto Redo() => new(Guid.NewGuid(), "redo", true, [], [], []);

        public void Clear()
        {
        }
    }

    private sealed class RecordingRunService : IRunService
    {
        public Task<CommandResultDto> CreateRunAsync(CreateRunRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> DeleteRunAsync(RunId runId) => throw new NotImplementedException();

        public Task<CommandResultDto> AddCabinetAsync(AddCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> InsertCabinetAsync(InsertCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> MoveCabinetAsync(MoveCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> ResizeCabinetAsync(ResizeCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> SetCabinetOverrideAsync(SetCabinetOverrideRequestDto request) => throw new NotImplementedException();

        public RunSummaryDto GetRunSummary(RunId runId) => throw new NotImplementedException();
    }

    private sealed class RecordingSceneProjector : ISceneProjector
    {
        public RenderSceneDto Project() => new([], [], [], null, new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));
    }

    private sealed class RecordingCanvasHost : IEditorCanvasHost
    {
        public object View => new();

        public void UpdateScene(RenderSceneDto scene)
        {
        }

        public void UpdateViewport(ViewportTransform viewport)
        {
        }
    }

    private sealed class TestEditorCanvasSession : IEditorCanvasSession
    {
        public EditorMode CurrentMode => EditorMode.Idle;

        public IReadOnlyList<Guid> SelectedCabinetIds => [];

        public Guid? HoveredCabinetId => null;

        public ViewportTransform Viewport => ViewportTransform.Default;

        public void SetSelectedCabinetIds(IReadOnlyList<Guid> cabinetIds)
        {
        }
    }
}
