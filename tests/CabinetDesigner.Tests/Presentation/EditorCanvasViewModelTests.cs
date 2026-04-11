using System.Threading;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Presentation.Projection;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Editor;
using CabinetDesigner.Presentation.ViewModels;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class EditorCanvasViewModelTests
{
    [Fact]
    public async Task AddCabinetToRunAsync_DelegatesToRunService()
    {
        var runService = new RecordingRunService();
        using var viewModel = CreateViewModel(runService, out _, out _, out _, out _);

        await viewModel.AddCabinetToRunAsync(Guid.NewGuid(), "base-36", 36m);

        Assert.NotNull(runService.LastAddRequest);
        Assert.Equal("base-36", runService.LastAddRequest!.CabinetTypeId);
        Assert.Equal("Cabinet added.", viewModel.StatusMessage);
    }

    [Fact]
    public void DesignChangedEvent_RefreshesProjectedScene()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out var canvasHost, out _);
        projector.Scene = new RenderSceneDto(
            [],
            [],
            [new CabinetRenderDto(Guid.NewGuid(), Guid.NewGuid(), new Rect2D(Point2D.Origin, Length.FromInches(10m), Length.FromInches(10m)), "cab", "cab", CabinetRenderState.Normal, [])],
            null,
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        Assert.Single(viewModel.Scene!.Cabinets);
        Assert.Same(viewModel.Scene, canvasHost.Scene);
        Assert.Equal("Design updated.", viewModel.StatusMessage);
    }

    [Fact]
    public void OnMouseDown_SelectsCabinetAndRaisesPropertyChanged()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out _, out _);
        var selectedCabinetId = Guid.NewGuid();
        projector.Scene = new RenderSceneDto(
            [],
            [],
            [new CabinetRenderDto(selectedCabinetId, Guid.NewGuid(), new Rect2D(Point2D.Origin, Length.FromInches(10m), Length.FromInches(10m)), "cab", "cab", CabinetRenderState.Normal, [])],
            null,
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
            {
                changedProperties.Add(args.PropertyName);
            }
        };

        viewModel.OnMouseDown(5d, 5d);

        Assert.Contains(selectedCabinetId, viewModel.SelectedCabinetIds);
        Assert.Contains(nameof(EditorCanvasViewModel.SelectedCabinetIds), changedProperties);
        Assert.Equal("Cabinet selected.", viewModel.StatusMessage);
    }

    [Fact]
    public void OnMouseDown_OnEmptyArea_ClearsSelection()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out _, out _, out _, out _);

        viewModel.OnMouseDown(9999d, 9999d);

        Assert.Empty(viewModel.SelectedCabinetIds);
        Assert.Equal("Selection cleared.", viewModel.StatusMessage);
    }

    [Fact]
    public void OnMouseDown_WithCtrlHeld_AddsSecondCabinetToSelection()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out var canvasHost, out _);
        var cabinetIdA = Guid.NewGuid();
        var cabinetIdB = Guid.NewGuid();
        projector.Scene = MakeTwoCabinetScene(cabinetIdA, cabinetIdB);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));
        var (aHitX, aHitY) = ViewportTransform.Default.ToScreen(new Point2D(5m, 5m));
        var (bHitX, bHitY) = ViewportTransform.Default.ToScreen(new Point2D(25m, 5m));

        // First click selects cabinet A without Ctrl.
        canvasHost.IsCtrlHeld = false;
        viewModel.OnMouseDown(aHitX, aHitY);
        Assert.Single(viewModel.SelectedCabinetIds);
        Assert.Contains(cabinetIdA, viewModel.SelectedCabinetIds);

        // Second click on cabinet B with Ctrl held adds it to the selection.
        canvasHost.IsCtrlHeld = true;
        viewModel.OnMouseDown(bHitX, bHitY);

        Assert.Equal(2, viewModel.SelectedCabinetIds.Count);
        Assert.Contains(cabinetIdA, viewModel.SelectedCabinetIds);
        Assert.Contains(cabinetIdB, viewModel.SelectedCabinetIds);
        Assert.Equal("2 cabinets selected.", viewModel.StatusMessage);
    }

    [Fact]
    public void OnMouseDown_WithCtrlHeld_OnAlreadySelectedCabinet_TogglesItOff()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out var canvasHost, out _);
        var cabinetIdA = Guid.NewGuid();
        var cabinetIdB = Guid.NewGuid();
        projector.Scene = MakeTwoCabinetScene(cabinetIdA, cabinetIdB);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));
        var (aHitX, aHitY) = ViewportTransform.Default.ToScreen(new Point2D(5m, 5m));
        var (bHitX, bHitY) = ViewportTransform.Default.ToScreen(new Point2D(25m, 5m));

        // Select both cabinets via Ctrl+Click.
        canvasHost.IsCtrlHeld = false;
        viewModel.OnMouseDown(aHitX, aHitY);
        canvasHost.IsCtrlHeld = true;
        viewModel.OnMouseDown(bHitX, bHitY);
        Assert.Equal(2, viewModel.SelectedCabinetIds.Count);

        // Ctrl+Click cabinet A again toggles it off.
        viewModel.OnMouseDown(aHitX, aHitY);

        Assert.Single(viewModel.SelectedCabinetIds);
        Assert.DoesNotContain(cabinetIdA, viewModel.SelectedCabinetIds);
        Assert.Contains(cabinetIdB, viewModel.SelectedCabinetIds);
        Assert.Equal("Cabinet selected.", viewModel.StatusMessage);
    }

    [Fact]
    public void OnMouseDown_WithoutCtrl_AfterMultiSelect_ReplacesPreviousSelection()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out var canvasHost, out _);
        var cabinetIdA = Guid.NewGuid();
        var cabinetIdB = Guid.NewGuid();
        projector.Scene = MakeTwoCabinetScene(cabinetIdA, cabinetIdB);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));
        var (aHitX, aHitY) = ViewportTransform.Default.ToScreen(new Point2D(5m, 5m));
        var (bHitX, bHitY) = ViewportTransform.Default.ToScreen(new Point2D(25m, 5m));

        // Build up a multi-selection with Ctrl.
        canvasHost.IsCtrlHeld = false;
        viewModel.OnMouseDown(aHitX, aHitY);
        canvasHost.IsCtrlHeld = true;
        viewModel.OnMouseDown(bHitX, bHitY);
        Assert.Equal(2, viewModel.SelectedCabinetIds.Count);

        // Click cabinet A without Ctrl — only cabinet A should remain selected.
        canvasHost.IsCtrlHeld = false;
        viewModel.OnMouseDown(aHitX, aHitY);

        Assert.Single(viewModel.SelectedCabinetIds);
        Assert.Contains(cabinetIdA, viewModel.SelectedCabinetIds);
        Assert.Equal("Cabinet selected.", viewModel.StatusMessage);
    }

    [Fact]
    public void OnMouseMove_BelowThreshold_DoesNotStartDrag()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out _, out var interactionService);
        var cabinetId = Guid.NewGuid();
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        // Mouse down on cabinet.
        viewModel.OnMouseDown(5d, 5d);

        // Move less than the 4 px drag threshold.
        viewModel.OnMouseMove(6d, 5d);

        Assert.Equal(0, interactionService.BeginMoveCabinetCallCount);
        Assert.Equal(0, interactionService.BeginResizeCabinetCallCount);
    }

    [Fact]
    public void OnMouseMove_ExceedsThreshold_StartsMoveOnCabinetBody()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out _, out var interactionService);
        var cabinetId = Guid.NewGuid();
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        viewModel.OnMouseDown(5d, 5d);
        // Move more than the 4 px threshold.
        viewModel.OnMouseMove(10d, 5d);

        Assert.Equal(1, interactionService.BeginMoveCabinetCallCount);
        Assert.Equal(new CabinetId(cabinetId), interactionService.LastMoveCabinetId);
    }

    [Fact]
    public void OnMouseMove_WhileDragActive_CallsOnDragMoved()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out _, out var interactionService);
        var cabinetId = Guid.NewGuid();
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        viewModel.OnMouseDown(5d, 5d);
        viewModel.OnMouseMove(10d, 5d); // starts drag
        viewModel.OnMouseMove(15d, 5d); // updates drag

        Assert.True(interactionService.OnDragMovedCallCount >= 1);
    }

    [Fact]
    public void OnMouseUp_WhenDragActive_CommitsDrag()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out _, out var interactionService);
        var cabinetId = Guid.NewGuid();
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        viewModel.OnMouseDown(5d, 5d);
        viewModel.OnMouseMove(10d, 5d); // starts drag
        viewModel.OnMouseUp(10d, 5d);

        // CommitDragAsync fires synchronously (RecordingInteractionService uses Task.FromResult)
        // but is launched fire-and-forget; spin until the counter increments.
        SpinWait.SpinUntil(() => interactionService.CommitCallCount == 1, TimeSpan.FromSeconds(1));

        Assert.Equal(1, interactionService.CommitCallCount);
    }

    [Fact]
    public void OnMouseUp_WithoutDrag_DoesNotCommit()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out _, out _, out _, out var interactionService);

        // Click without drag.
        viewModel.OnMouseDown(5d, 5d);
        viewModel.OnMouseUp(5d, 5d);

        Assert.Equal(0, interactionService.CommitCallCount);
    }

    [Fact]
    public void OnMouseWheel_ScrollUp_UpdatesViewportScale()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out _, out var canvasHost, out _);
        projector.Scene = new RenderSceneDto([], [], [], null, new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));
        var initialScale = canvasHost.Viewport.ScalePixelsPerInch;

        viewModel.OnMouseWheel(100d, 100d, 120d); // positive delta = zoom in

        Assert.True(canvasHost.Viewport.ScalePixelsPerInch > initialScale);
    }

    [Fact]
    public void OnMouseWheel_ScrollDown_DecreasesViewportScale()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out _, out var canvasHost, out _);
        projector.Scene = new RenderSceneDto([], [], [], null, new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));
        var initialScale = canvasHost.Viewport.ScalePixelsPerInch;

        viewModel.OnMouseWheel(100d, 100d, -120d); // negative delta = zoom out

        Assert.True(canvasHost.Viewport.ScalePixelsPerInch < initialScale);
    }

    [Fact]
    public void OnPanStart_SetsPanningMode_AndPanMoveUpdatesOffset()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out _, out var canvasHost, out _);
        projector.Scene = new RenderSceneDto([], [], [], null, new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

        viewModel.OnPanStart(100d, 200d);
        Assert.Equal(EditorMode.PanningViewport.ToString(), viewModel.CurrentMode);

        viewModel.OnPanMove(130d, 220d); // pan by (+30, +20)

        Assert.True(canvasHost.Viewport.OffsetXPixels > 0m);
        Assert.True(canvasHost.Viewport.OffsetYPixels > 0m);
    }

    [Fact]
    public void OnPanStart_WhenSceneIsNull_DoesNotEnterPanningMode()
    {
        // Scene is null when no project is open. BeginPan must not be called to avoid
        // corrupting the session mode (e.g. PanningViewport with no active content).
        using var viewModel = CreateViewModel(new RecordingRunService(), out _, out _, out _, out _);

        viewModel.OnPanStart(0d, 0d);

        Assert.Equal(EditorMode.Idle.ToString(), viewModel.CurrentMode);
    }

    [Fact]
    public void ResetZoomCommand_WhenSceneIsNull_DoesNotRepopulateScene()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out _, out _, out var canvasHost, out _);
        // Deliberately leave Scene null (no project open).

        viewModel.ResetZoomCommand.Execute(null);

        Assert.Null(viewModel.Scene);
        Assert.Equal(ViewportTransform.Default, canvasHost.Viewport);
        Assert.Equal("Zoom reset.", viewModel.StatusMessage);
    }

    [Fact]
    public void OnPanEnd_RestoresIdleMode()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out _, out _, out _);
        projector.Scene = new RenderSceneDto([], [], [], null, new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

        viewModel.OnPanStart(0d, 0d);
        viewModel.OnPanEnd();

        Assert.Equal(EditorMode.Idle.ToString(), viewModel.CurrentMode);
    }

    [Fact]
    public void ResetZoomCommand_ResetsViewportToDefault()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out _, out var canvasHost, out _);
        projector.Scene = new RenderSceneDto([], [], [], null, new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));
        // Pan and zoom first.
        viewModel.OnMouseWheel(0d, 0d, 120d);
        viewModel.OnPanStart(0d, 0d);
        viewModel.OnPanMove(50d, 40d);
        viewModel.OnPanEnd();

        viewModel.ResetZoomCommand.Execute(null);

        Assert.Equal(ViewportTransform.Default, canvasHost.Viewport);
        Assert.Equal("Zoom reset.", viewModel.StatusMessage);
    }

    [Fact]
    public void FitToViewCommand_WhenSceneHasCabinets_UpdatesViewportToFrameContent()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out var canvasHost, out _);
        var cabinetId = Guid.NewGuid();
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        viewModel.FitToViewCommand.Execute(null);

        // Viewport must have changed from default so content is framed.
        Assert.NotEqual(ViewportTransform.Default, canvasHost.Viewport);
        Assert.Equal("Fit to view.", viewModel.StatusMessage);
    }

    [Fact]
    public void FitToViewCommand_WhenSceneIsNull_DoesNothing()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out _, out _, out var canvasHost, out _);
        // Scene is null — no project open.

        viewModel.FitToViewCommand.Execute(null);

        Assert.Null(viewModel.Scene);
        Assert.Equal(ViewportTransform.Default, canvasHost.Viewport);
    }

    [Fact]
    public void FitToViewCommand_WhenSceneIsEmpty_SetsEmptyStatusMessage()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out var canvasHost, out _);
        projector.Scene = new RenderSceneDto([], [], [], null, new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        viewModel.FitToViewCommand.Execute(null);

        // No content bounds — viewport should remain at default and a status message is set.
        Assert.Equal(ViewportTransform.Default, canvasHost.Viewport);
        Assert.Equal("Nothing to fit — canvas is empty.", viewModel.StatusMessage);
    }

    [Fact]
    public void FitToViewCommand_WhenCanvasNotReady_SetsNotReadyStatusMessage()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out var canvasHost, out _);
        var cabinetId = Guid.NewGuid();
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));
        canvasHost.CanvasWidth = 0;
        canvasHost.CanvasHeight = 0;

        viewModel.FitToViewCommand.Execute(null);

        Assert.Equal(ViewportTransform.Default, canvasHost.Viewport);
        Assert.Equal("Unable to fit to view — canvas is not ready.", viewModel.StatusMessage);
    }

    [Fact]
    public void SelectAllCommand_SelectsAllCabinetsInScene()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out _, out _);
        var cabinetIdA = Guid.NewGuid();
        var cabinetIdB = Guid.NewGuid();
        projector.Scene = MakeTwoCabinetScene(cabinetIdA, cabinetIdB);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        viewModel.SelectAllCommand.Execute(null);

        Assert.Equal(2, viewModel.SelectedCabinetIds.Count);
        Assert.Contains(cabinetIdA, viewModel.SelectedCabinetIds);
        Assert.Contains(cabinetIdB, viewModel.SelectedCabinetIds);
        Assert.Equal("2 cabinets selected.", viewModel.StatusMessage);
    }

    [Fact]
    public void SelectAllCommand_WithSingleCabinet_ReportsCorrectStatusMessage()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out _, out _);
        var cabinetId = Guid.NewGuid();
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        viewModel.SelectAllCommand.Execute(null);

        Assert.Single(viewModel.SelectedCabinetIds);
        Assert.Equal("1 cabinet selected.", viewModel.StatusMessage);
    }

    [Fact]
    public void SelectAllCommand_WhenSceneIsNull_CannotExecute()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out _, out _, out _, out _);
        // Scene is null — no project open.

        Assert.False(viewModel.SelectAllCommand.CanExecute(null));
    }

    [Fact]
    public void SelectAllCommand_WhenSceneIsLoaded_CanExecute()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out _, out _);
        projector.Scene = MakeSingleCabinetScene(Guid.NewGuid());
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        Assert.True(viewModel.SelectAllCommand.CanExecute(null));
    }

    [Fact]
    public void SelectNoneCommand_ClearsExistingSelection()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out _, out _);
        var cabinetIdA = Guid.NewGuid();
        var cabinetIdB = Guid.NewGuid();
        projector.Scene = MakeTwoCabinetScene(cabinetIdA, cabinetIdB);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));
        viewModel.SelectAllCommand.Execute(null);
        Assert.Equal(2, viewModel.SelectedCabinetIds.Count);

        viewModel.SelectNoneCommand.Execute(null);

        Assert.Empty(viewModel.SelectedCabinetIds);
        Assert.Equal("Selection cleared.", viewModel.StatusMessage);
    }

    [Fact]
    public void SelectNoneCommand_WhenNothingSelected_SetsStatusMessage()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out _, out _);
        projector.Scene = MakeSingleCabinetScene(Guid.NewGuid());
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        viewModel.SelectNoneCommand.Execute(null);

        Assert.Empty(viewModel.SelectedCabinetIds);
        Assert.Equal("Selection cleared.", viewModel.StatusMessage);
    }

    [Fact]
    public void SelectNoneCommand_WhenSceneIsNull_CannotExecute()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out _, out _, out _, out _);

        Assert.False(viewModel.SelectNoneCommand.CanExecute(null));
    }

    [Fact]
    public void BeginDrag_WhenServiceThrowsInvalidOperationException_LogsWarning()
    {
        var logger = new CapturingAppLogger();
        var throwingInteraction = new ThrowingOnBeginInteractionService();
        using var viewModel = CreateViewModelWithLogger(new RecordingRunService(), throwingInteraction, logger, out var projector, out var eventBus);
        var cabinetId = Guid.NewGuid();
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        // Mouse down then move past the 4 px drag threshold to trigger BeginDrag.
        viewModel.OnMouseDown(5d, 5d);
        viewModel.OnMouseMove(10d, 5d);

        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, logger.Entries[0].Level);
        Assert.Equal("EditorCanvasViewModel", logger.Entries[0].Category);
        Assert.NotNull(logger.Entries[0].Exception);
        Assert.IsType<InvalidOperationException>(logger.Entries[0].Exception);
    }

    [Fact]
    public async Task CommitDrag_WhenServiceThrows_LogsErrorAndSetsFailedStatus()
    {
        var logger = new CapturingAppLogger();
        var throwingInteraction = new ThrowingOnCommitInteractionService();
        using var viewModel = CreateViewModelWithLogger(new RecordingRunService(), throwingInteraction, logger, out var projector, out var eventBus);
        var cabinetId = Guid.NewGuid();
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        // Start a drag then commit it; the commit will throw.
        viewModel.OnMouseDown(5d, 5d);
        viewModel.OnMouseMove(10d, 5d); // start drag
        viewModel.OnMouseUp(10d, 5d);   // triggers CommitDragAsync

        // CommitDragAsync is fire-and-forget; wait deterministically for the log entry.
        var entry = await logger.WaitForEntryAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("EditorCanvasViewModel", entry.Category);
        Assert.NotNull(entry.Exception);
        Assert.Equal("Drag failed.", viewModel.StatusMessage);
    }

    [Fact]
    public void CommitDrag_WhenServiceReturnsFailureWithReason_ShowsReasonInStatusMessage()
    {
        const string rejectionReason = "Cabinet must be dragged onto a wall run to move it.";
        var failingInteraction = new FailingCommitInteractionService(rejectionReason);
        using var viewModel = CreateViewModelWithLogger(new RecordingRunService(), failingInteraction, new CapturingAppLogger(), out var projector, out var eventBus);
        var cabinetId = Guid.NewGuid();
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        viewModel.OnMouseDown(5d, 5d);
        viewModel.OnMouseMove(10d, 5d); // start drag
        viewModel.OnMouseUp(10d, 5d);   // triggers CommitDragAsync

        // CommitDragAsync is fire-and-forget; spin until the status is updated.
        var completed = SpinWait.SpinUntil(() => viewModel.StatusMessage == rejectionReason, TimeSpan.FromSeconds(5));
        Assert.True(completed, $"Timed out waiting for StatusMessage to become \"{rejectionReason}\".");

        Assert.Equal(rejectionReason, viewModel.StatusMessage);
    }

    [Fact]
    public void CommitDrag_WhenServiceReturnsFailureWithNoReason_ShowsFallbackMessage()
    {
        var failingInteraction = new FailingCommitInteractionService(null);
        using var viewModel = CreateViewModelWithLogger(new RecordingRunService(), failingInteraction, new CapturingAppLogger(), out var projector, out var eventBus);
        var cabinetId = Guid.NewGuid();
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        viewModel.OnMouseDown(5d, 5d);
        viewModel.OnMouseMove(10d, 5d);
        viewModel.OnMouseUp(10d, 5d);

        const string expected = "Placement rejected — check validation issues.";
        var completed = SpinWait.SpinUntil(() => viewModel.StatusMessage == expected, TimeSpan.FromSeconds(5));
        Assert.True(completed, $"Timed out waiting for StatusMessage to become \"{expected}\".");

        Assert.Equal(expected, viewModel.StatusMessage);
    }

    private static EditorCanvasViewModel CreateViewModelWithLogger(
        RecordingRunService runService,
        IEditorInteractionService interactionService,
        IAppLogger logger,
        out RecordingSceneProjector projector,
        out ApplicationEventBus eventBus)
    {
        projector = new RecordingSceneProjector();
        eventBus = new ApplicationEventBus();
        return new EditorCanvasViewModel(
            runService,
            eventBus,
            projector,
            new TestEditorCanvasSession(),
            new DefaultHitTester(),
            new RecordingCanvasHost(),
            interactionService,
            logger);
    }

    private sealed class ThrowingOnBeginInteractionService : IEditorInteractionService
    {
        private EditorMode _mode = EditorMode.Idle;

        public EditorMode CurrentMode => _mode;

        public void BeginPlaceCabinet(string cabinetTypeId, Length nominalWidth, Length nominalDepth, double screenX, double screenY) { }

        public void BeginMoveCabinet(CabinetId cabinetId, double screenX, double screenY) =>
            throw new InvalidOperationException("Cabinet no longer exists.");

        public void BeginResizeCabinet(CabinetId cabinetId, double screenX, double screenY) =>
            throw new InvalidOperationException("Cabinet no longer exists.");

        public DragPreviewResult OnDragMoved(double screenX, double screenY) =>
            new DragPreviewResult(true, null, null);

        public Task<DragCommitResult> OnDragCommittedAsync(CancellationToken ct = default) =>
            Task.FromResult(new DragCommitResult(true, null, null));

        public void OnDragAborted() => _mode = EditorMode.Idle;
    }

    private sealed class ThrowingOnCommitInteractionService : IEditorInteractionService
    {
        private EditorMode _mode = EditorMode.Idle;

        public EditorMode CurrentMode => _mode;

        public void BeginPlaceCabinet(string cabinetTypeId, Length nominalWidth, Length nominalDepth, double screenX, double screenY) { }

        public void BeginMoveCabinet(CabinetId cabinetId, double screenX, double screenY) =>
            _mode = EditorMode.MovingCabinet;

        public void BeginResizeCabinet(CabinetId cabinetId, double screenX, double screenY) =>
            _mode = EditorMode.ResizingCabinet;

        public DragPreviewResult OnDragMoved(double screenX, double screenY) =>
            new DragPreviewResult(true, null, null);

        public Task<DragCommitResult> OnDragCommittedAsync(CancellationToken ct = default) =>
            Task.FromException<DragCommitResult>(new InvalidOperationException("Simulated commit failure."));

        public void OnDragAborted() => _mode = EditorMode.Idle;
    }

    private sealed class FailingCommitInteractionService : IEditorInteractionService
    {
        private readonly string? _failureReason;
        private EditorMode _mode = EditorMode.Idle;

        public FailingCommitInteractionService(string? failureReason) => _failureReason = failureReason;

        public EditorMode CurrentMode => _mode;

        public void BeginPlaceCabinet(string cabinetTypeId, Length nominalWidth, Length nominalDepth, double screenX, double screenY) { }

        public void BeginMoveCabinet(CabinetId cabinetId, double screenX, double screenY) =>
            _mode = EditorMode.MovingCabinet;

        public void BeginResizeCabinet(CabinetId cabinetId, double screenX, double screenY) =>
            _mode = EditorMode.ResizingCabinet;

        public DragPreviewResult OnDragMoved(double screenX, double screenY) =>
            new DragPreviewResult(true, null, null);

        public Task<DragCommitResult> OnDragCommittedAsync(CancellationToken ct = default)
        {
            _mode = EditorMode.Idle;
            return Task.FromResult(DragCommitResult.Failed(_failureReason ?? string.Empty));
        }

        public void OnDragAborted() => _mode = EditorMode.Idle;
    }

    private static RenderSceneDto MakeSingleCabinetScene(Guid cabinetId) =>
        new RenderSceneDto(
            [],
            [],
            [new CabinetRenderDto(cabinetId, Guid.NewGuid(), new Rect2D(Point2D.Origin, Length.FromInches(10m), Length.FromInches(10m)), "cab", "cab", CabinetRenderState.Normal, [])],
            null,
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

    /// <summary>
    /// Cabinet A at world (0,0)–(10,10); cabinet B at world (20,0)–(30,10).
    /// Use <see cref="ViewportTransform.Default"/> to compute hit screen coordinates
    /// (e.g. <c>ViewportTransform.Default.ToScreen(new Point2D(5m, 5m))</c> for cabinet A centre).
    /// </summary>
    private static RenderSceneDto MakeTwoCabinetScene(Guid cabinetIdA, Guid cabinetIdB) =>
        new RenderSceneDto(
            [],
            [],
            [
                new CabinetRenderDto(cabinetIdA, Guid.NewGuid(), new Rect2D(Point2D.Origin, Length.FromInches(10m), Length.FromInches(10m)), "cab-a", "cab-a", CabinetRenderState.Normal, []),
                new CabinetRenderDto(cabinetIdB, Guid.NewGuid(), new Rect2D(new Point2D(20m, 0m), Length.FromInches(10m), Length.FromInches(10m)), "cab-b", "cab-b", CabinetRenderState.Normal, [])
            ],
            null,
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

    private static EditorCanvasViewModel CreateViewModel(
        RecordingRunService runService,
        out RecordingSceneProjector projector,
        out ApplicationEventBus eventBus,
        out RecordingCanvasHost canvasHost,
        out RecordingInteractionService interactionService)
    {
        projector = new RecordingSceneProjector();
        eventBus = new ApplicationEventBus();
        canvasHost = new RecordingCanvasHost();
        interactionService = new RecordingInteractionService();
        return new EditorCanvasViewModel(
            runService,
            eventBus,
            projector,
            new TestEditorCanvasSession(),
            new DefaultHitTester(),
            canvasHost,
            interactionService);
    }

    private sealed class RecordingSceneProjector : ISceneProjector
    {
        public RenderSceneDto Scene { get; set; } = new([], [], [], null, new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

        public RenderSceneDto Project() => Scene;
    }

    private sealed class RecordingRunService : IRunService
    {
        public AddCabinetRequestDto? LastAddRequest { get; private set; }

        public Task<CommandResultDto> CreateRunAsync(CreateRunRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> DeleteRunAsync(RunId runId) => throw new NotImplementedException();

        public Task<CommandResultDto> AddCabinetAsync(AddCabinetRequestDto request)
        {
            LastAddRequest = request;
            return Task.FromResult(new CommandResultDto(Guid.NewGuid(), "layout.add_cabinet_to_run", true, [], [request.RunId.ToString()], []));
        }

        public Task<CommandResultDto> InsertCabinetAsync(InsertCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> MoveCabinetAsync(MoveCabinetRequestDto request) =>
            Task.FromResult(new CommandResultDto(Guid.NewGuid(), "layout.move_cabinet", true, [], [request.CabinetId.ToString()], []));

        public Task<CommandResultDto> ResizeCabinetAsync(ResizeCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> SetCabinetOverrideAsync(SetCabinetOverrideRequestDto request) => throw new NotImplementedException();

        public RunSummaryDto GetRunSummary(RunId runId) => throw new NotImplementedException();
    }

    private sealed class RecordingCanvasHost : IEditorCanvasHost
    {
        public object View => new();

        public bool IsCtrlHeld { get; set; }

        public double CanvasWidth { get; set; } = 800.0;

        public double CanvasHeight { get; set; } = 600.0;

        public RenderSceneDto? Scene { get; private set; }

        public ViewportTransform Viewport { get; private set; } = ViewportTransform.Default;

        public void UpdateScene(RenderSceneDto scene) => Scene = scene;

        public void UpdateViewport(ViewportTransform viewport) => Viewport = viewport;

        public void SetMouseDownHandler(Action<double, double> handler)
        {
        }

        public void SetMouseMoveHandler(Action<double, double> handler)
        {
        }

        public void SetMouseUpHandler(Action<double, double> handler)
        {
        }

        public void SetMouseWheelHandler(Action<double, double, double> handler)
        {
        }

        public void SetMiddleButtonDragHandler(Action<double, double> onStart, Action<double, double> onMove, Action onEnd)
        {
        }
    }

    private sealed class TestEditorCanvasSession : IEditorCanvasSession
    {
        public EditorMode CurrentMode { get; private set; } = EditorMode.Idle;

        public IReadOnlyList<Guid> SelectedCabinetIds { get; private set; } = [];

        public Guid? HoveredCabinetId { get; private set; }

        public ViewportTransform Viewport { get; private set; } = ViewportTransform.Default;

        public void SetSelectedCabinetIds(IReadOnlyList<Guid> cabinetIds)
        {
            SelectedCabinetIds = cabinetIds.ToArray();
        }

        public void SetHoveredCabinetId(Guid? cabinetId)
        {
            HoveredCabinetId = cabinetId;
        }

        public void ZoomAt(double screenX, double screenY, double scaleFactor)
        {
            var currentScale = (double)Viewport.ScalePixelsPerInch;
            var newScale = Math.Clamp(currentScale * scaleFactor, 2.0, 200.0);
            var actualFactor = newScale / currentScale;
            var newOriginX = screenX - actualFactor * (screenX - (double)Viewport.OffsetXPixels);
            var newOriginY = screenY - actualFactor * (screenY - (double)Viewport.OffsetYPixels);
            Viewport = new ViewportTransform((decimal)newScale, (decimal)newOriginX, (decimal)newOriginY);
        }

        public void PanBy(double dx, double dy)
        {
            Viewport = Viewport.Panned((decimal)dx, (decimal)dy);
        }

        public void BeginPan() => CurrentMode = EditorMode.PanningViewport;

        public void EndPan() => CurrentMode = EditorMode.Idle;

        public void ResetViewport() => Viewport = ViewportTransform.Default;

        public void FitViewport(CabinetDesigner.Domain.Geometry.Rect2D contentBounds, double canvasWidth, double canvasHeight)
        {
            if (canvasWidth <= 0 || canvasHeight <= 0)
            {
                return;
            }

            var contentWidthInches = (double)(contentBounds.Max.X - contentBounds.Min.X);
            var contentHeightInches = (double)(contentBounds.Max.Y - contentBounds.Min.Y);
            if (contentWidthInches <= 0 || contentHeightInches <= 0)
            {
                return;
            }

            const double marginFactor = 0.8;
            var scaleX = canvasWidth * marginFactor / contentWidthInches;
            var scaleY = canvasHeight * marginFactor / contentHeightInches;
            var scale = Math.Clamp(Math.Min(scaleX, scaleY), 2.0, 200.0);
            var centreWorldX = (double)((contentBounds.Min.X + contentBounds.Max.X) / 2);
            var centreWorldY = (double)((contentBounds.Min.Y + contentBounds.Max.Y) / 2);
            Viewport = new ViewportTransform((decimal)scale, (decimal)((canvasWidth / 2) - (centreWorldX * scale)), (decimal)((canvasHeight / 2) - (centreWorldY * scale)));
        }
    }

    private sealed class RecordingInteractionService : IEditorInteractionService
    {
        public int BeginMoveCabinetCallCount { get; private set; }
        public CabinetId? LastMoveCabinetId { get; private set; }

        public int BeginResizeCabinetCallCount { get; private set; }

        public int OnDragMovedCallCount { get; private set; }

        public int CommitCallCount { get; private set; }

        private EditorMode _mode = EditorMode.Idle;

        public EditorMode CurrentMode => _mode;

        public void BeginPlaceCabinet(string cabinetTypeId, Length nominalWidth, Length nominalDepth, double screenX, double screenY)
        {
            _mode = EditorMode.PlacingCabinet;
        }

        public void BeginMoveCabinet(CabinetId cabinetId, double screenX, double screenY)
        {
            BeginMoveCabinetCallCount++;
            LastMoveCabinetId = cabinetId;
            _mode = EditorMode.MovingCabinet;
        }

        public void BeginResizeCabinet(CabinetId cabinetId, double screenX, double screenY)
        {
            BeginResizeCabinetCallCount++;
            _mode = EditorMode.ResizingCabinet;
        }

        public DragPreviewResult OnDragMoved(double screenX, double screenY)
        {
            OnDragMovedCallCount++;
            return new DragPreviewResult(true, null, null);
        }

        public Task<DragCommitResult> OnDragCommittedAsync(CancellationToken ct = default)
        {
            CommitCallCount++;
            _mode = EditorMode.Idle;
            return Task.FromResult(new DragCommitResult(true, null, null));
        }

        public void OnDragAborted()
        {
            _mode = EditorMode.Idle;
        }
    }
}

/// <summary>
/// Tests that exercise the full host-forwarding path:
/// host.Simulate* → registered handler → EditorCanvasViewModel logic.
/// Uses <see cref="ForwardingCanvasHost"/> so no WPF infrastructure is required.
/// </summary>
public sealed class EditorCanvasViewModelForwardingTests
{
    [Fact]
    public void HostMouseDown_OnCabinet_SelectsCabinet()
    {
        var host = new ForwardingCanvasHost();
        var cabinetId = Guid.NewGuid();
        using var viewModel = CreateViewModelWithForwardingHost(host, out var projector, out var eventBus, out _);
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        host.SimulateMouseDown(5d, 5d);

        Assert.Contains(cabinetId, viewModel.SelectedCabinetIds);
        Assert.Equal("Cabinet selected.", viewModel.StatusMessage);
    }

    [Fact]
    public void HostMouseDown_OnEmptyArea_ClearsSelection()
    {
        var host = new ForwardingCanvasHost();
        var cabinetId = Guid.NewGuid();
        using var viewModel = CreateViewModelWithForwardingHost(host, out var projector, out var eventBus, out _);
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));
        host.SimulateMouseDown(5d, 5d);
        Assert.Single(viewModel.SelectedCabinetIds);

        host.SimulateMouseDown(9999d, 9999d);

        Assert.Empty(viewModel.SelectedCabinetIds);
        Assert.Equal("Selection cleared.", viewModel.StatusMessage);
    }

    [Fact]
    public void HostMouseMove_BelowThreshold_DoesNotStartDrag()
    {
        var host = new ForwardingCanvasHost();
        var cabinetId = Guid.NewGuid();
        using var viewModel = CreateViewModelWithForwardingHost(host, out var projector, out var eventBus, out var interactionService);
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        host.SimulateMouseDown(5d, 5d);
        host.SimulateMouseMove(6d, 5d); // 1 px — below the 4 px threshold

        Assert.Equal(0, interactionService.BeginMoveCabinetCallCount);
    }

    [Fact]
    public void HostMouseMove_ExceedsThreshold_StartsDrag()
    {
        var host = new ForwardingCanvasHost();
        var cabinetId = Guid.NewGuid();
        using var viewModel = CreateViewModelWithForwardingHost(host, out var projector, out var eventBus, out var interactionService);
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        host.SimulateMouseDown(5d, 5d);
        host.SimulateMouseMove(10d, 5d); // 5 px — exceeds the 4 px threshold

        Assert.Equal(1, interactionService.BeginMoveCabinetCallCount);
    }

    [Fact]
    public void HostMouseMove_WhileDragActive_UpdatesDragPreview()
    {
        var host = new ForwardingCanvasHost();
        var cabinetId = Guid.NewGuid();
        using var viewModel = CreateViewModelWithForwardingHost(host, out var projector, out var eventBus, out var interactionService);
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        host.SimulateMouseDown(5d, 5d);
        host.SimulateMouseMove(10d, 5d); // start drag
        host.SimulateMouseMove(15d, 5d); // update drag

        Assert.True(interactionService.OnDragMovedCallCount >= 1);
    }

    [Fact]
    public void HostMouseUp_AfterDrag_CommitsDrag()
    {
        var host = new ForwardingCanvasHost();
        var cabinetId = Guid.NewGuid();
        using var viewModel = CreateViewModelWithForwardingHost(host, out var projector, out var eventBus, out var interactionService);
        projector.Scene = MakeSingleCabinetScene(cabinetId);
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        host.SimulateMouseDown(5d, 5d);
        host.SimulateMouseMove(10d, 5d); // start drag
        host.SimulateMouseUp(10d, 5d);

        SpinWait.SpinUntil(() => interactionService.CommitCallCount == 1, TimeSpan.FromSeconds(1));

        Assert.Equal(1, interactionService.CommitCallCount);
    }

    [Fact]
    public void HostMouseUp_WithoutDrag_DoesNotCommit()
    {
        var host = new ForwardingCanvasHost();
        using var viewModel = CreateViewModelWithForwardingHost(host, out _, out _, out var interactionService);

        host.SimulateMouseDown(5d, 5d);
        host.SimulateMouseUp(5d, 5d);

        Assert.Equal(0, interactionService.CommitCallCount);
    }

    private static RenderSceneDto MakeSingleCabinetScene(Guid cabinetId) =>
        new RenderSceneDto(
            [],
            [],
            [new CabinetRenderDto(cabinetId, Guid.NewGuid(), new Rect2D(Point2D.Origin, Length.FromInches(10m), Length.FromInches(10m)), "cab", "cab", CabinetRenderState.Normal, [])],
            null,
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

    private static EditorCanvasViewModel CreateViewModelWithForwardingHost(
        ForwardingCanvasHost host,
        out RecordingSceneProjector projector,
        out ApplicationEventBus eventBus,
        out RecordingInteractionService interactionService)
    {
        projector = new RecordingSceneProjector();
        eventBus = new ApplicationEventBus();
        interactionService = new RecordingInteractionService();
        return new EditorCanvasViewModel(
            new RecordingRunService(),
            eventBus,
            projector,
            new TestEditorCanvasSession(),
            new DefaultHitTester(),
            host,
            interactionService);
    }

    private sealed class RecordingSceneProjector : ISceneProjector
    {
        public RenderSceneDto Scene { get; set; } = new([], [], [], null, new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

        public RenderSceneDto Project() => Scene;
    }

    private sealed class RecordingRunService : IRunService
    {
        public Task<CommandResultDto> CreateRunAsync(CreateRunRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> DeleteRunAsync(RunId runId) => throw new NotImplementedException();

        public Task<CommandResultDto> AddCabinetAsync(AddCabinetRequestDto request) =>
            Task.FromResult(new CommandResultDto(Guid.NewGuid(), "layout.add_cabinet_to_run", true, [], [request.RunId.ToString()], []));

        public Task<CommandResultDto> InsertCabinetAsync(InsertCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> MoveCabinetAsync(MoveCabinetRequestDto request) =>
            Task.FromResult(new CommandResultDto(Guid.NewGuid(), "layout.move_cabinet", true, [], [request.CabinetId.ToString()], []));

        public Task<CommandResultDto> ResizeCabinetAsync(ResizeCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> SetCabinetOverrideAsync(SetCabinetOverrideRequestDto request) => throw new NotImplementedException();

        public RunSummaryDto GetRunSummary(RunId runId) => throw new NotImplementedException();
    }

    private sealed class TestEditorCanvasSession : IEditorCanvasSession
    {
        public EditorMode CurrentMode { get; private set; } = EditorMode.Idle;

        public IReadOnlyList<Guid> SelectedCabinetIds { get; private set; } = [];

        public Guid? HoveredCabinetId { get; private set; }

        public ViewportTransform Viewport { get; } = ViewportTransform.Default;

        public void SetSelectedCabinetIds(IReadOnlyList<Guid> cabinetIds) => SelectedCabinetIds = cabinetIds.ToArray();

        public void SetHoveredCabinetId(Guid? cabinetId) => HoveredCabinetId = cabinetId;

        public void ZoomAt(double screenX, double screenY, double scaleFactor) { }

        public void PanBy(double dx, double dy) { }

        public void BeginPan() => CurrentMode = EditorMode.PanningViewport;

        public void EndPan() => CurrentMode = EditorMode.Idle;

        public void ResetViewport() { }

        public void FitViewport(CabinetDesigner.Domain.Geometry.Rect2D contentBounds, double canvasWidth, double canvasHeight) { }
    }

    private sealed class RecordingInteractionService : IEditorInteractionService
    {
        public int BeginMoveCabinetCallCount { get; private set; }

        public int BeginResizeCabinetCallCount { get; private set; }

        public int OnDragMovedCallCount { get; private set; }

        public int CommitCallCount { get; private set; }

        public void BeginPlaceCabinet(string cabinetTypeId, Length nominalWidth, Length nominalDepth, double screenX, double screenY) { }

        public void BeginMoveCabinet(CabinetId cabinetId, double screenX, double screenY) => BeginMoveCabinetCallCount++;

        public void BeginResizeCabinet(CabinetId cabinetId, double screenX, double screenY) => BeginResizeCabinetCallCount++;

        public DragPreviewResult OnDragMoved(double screenX, double screenY)
        {
            OnDragMovedCallCount++;
            return new DragPreviewResult(true, null, null);
        }

        public Task<DragCommitResult> OnDragCommittedAsync(CancellationToken ct = default)
        {
            CommitCallCount++;
            return Task.FromResult(new DragCommitResult(true, null, null));
        }

        public void OnDragAborted() { }
    }

    private sealed class ForwardingCanvasHost : IEditorCanvasHost
    {
        private readonly object _view = new();
        private Action<double, double>? _mouseDownHandler;
        private Action<double, double>? _mouseMoveHandler;
        private Action<double, double>? _mouseUpHandler;
        private Action<double, double, double>? _mouseWheelHandler;
        private Action<double, double>? _panStartHandler;
        private Action<double, double>? _panMoveHandler;
        private Action? _panEndHandler;

        public object View => _view;

        public bool IsCtrlHeld { get; set; }

        public double CanvasWidth { get; set; } = 800.0;

        public double CanvasHeight { get; set; } = 600.0;

        public RenderSceneDto? Scene { get; private set; }

        public ViewportTransform Viewport { get; private set; } = ViewportTransform.Default;

        public void UpdateScene(RenderSceneDto scene) => Scene = scene;

        public void UpdateViewport(ViewportTransform viewport) => Viewport = viewport;

        public void SetMouseDownHandler(Action<double, double> handler) => _mouseDownHandler = handler;

        public void SetMouseMoveHandler(Action<double, double> handler) => _mouseMoveHandler = handler;

        public void SetMouseUpHandler(Action<double, double> handler) => _mouseUpHandler = handler;

        public void SetMouseWheelHandler(Action<double, double, double> handler) => _mouseWheelHandler = handler;

        public void SetMiddleButtonDragHandler(
            Action<double, double> onStart,
            Action<double, double> onMove,
            Action onEnd)
        {
            _panStartHandler = onStart;
            _panMoveHandler = onMove;
            _panEndHandler = onEnd;
        }

        public void SimulateMouseDown(double x, double y) => _mouseDownHandler?.Invoke(x, y);

        public void SimulateMouseMove(double x, double y) => _mouseMoveHandler?.Invoke(x, y);

        public void SimulateMouseUp(double x, double y) => _mouseUpHandler?.Invoke(x, y);

        public void SimulateMouseWheel(double x, double y, double delta) => _mouseWheelHandler?.Invoke(x, y, delta);

        public void SimulatePanStart(double x, double y) => _panStartHandler?.Invoke(x, y);

        public void SimulatePanMove(double x, double y) => _panMoveHandler?.Invoke(x, y);

        public void SimulatePanEnd() => _panEndHandler?.Invoke();
    }
}
