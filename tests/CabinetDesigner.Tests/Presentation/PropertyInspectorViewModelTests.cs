using System.Threading;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using CabinetDesigner.Presentation.ViewModels;
using CabinetDesigner.Rendering.DTOs;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class PropertyInspectorViewModelTests
{
    [Fact]
    public void InitialState_NoProject_ShowsHonestEmptyState()
    {
        using var viewModel = CreateViewModel(out _);

        Assert.False(viewModel.IsProjectOpen);
        Assert.False(viewModel.HasSelection);
        Assert.False(viewModel.HasSingleSelection);
        Assert.Equal("No cabinet selected", viewModel.SelectedEntityLabel);
        Assert.Equal("Nothing selected", viewModel.SelectionSummaryDisplay);
        Assert.Equal("No project open", viewModel.SourceLabel);
        Assert.Equal("No editable properties", viewModel.EditabilityStatusDisplay);
        Assert.Equal("Open a project to inspect properties.", viewModel.EmptyStateText);
        Assert.Equal("Open a project to inspect properties.", viewModel.StatusMessage);
        Assert.Equal("-", viewModel.NominalWidthDisplay);
        Assert.Equal(string.Empty, viewModel.NominalWidthEditValue);
        Assert.Equal("0 details", viewModel.PropertySummaryDisplay);
        Assert.Empty(viewModel.Properties);
    }

    [Fact]
    public void ProjectOpenedEvent_WithoutSelection_ShowsSelectionPrompt()
    {
        using var viewModel = CreateViewModel(out var eventBus);

        eventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 1",
            false)));

        Assert.True(viewModel.IsProjectOpen);
        Assert.False(viewModel.HasSelection);
        Assert.Equal("No cabinet selected", viewModel.SourceLabel);
        Assert.Equal("No editable properties", viewModel.EditabilityStatusDisplay);
        Assert.Equal("No cabinet selected. Click a cabinet on the canvas to inspect it.", viewModel.EmptyStateText);
        Assert.Equal("Click a cabinet to inspect it.", viewModel.StatusMessage);
        Assert.Empty(viewModel.Properties);
    }

    [Fact]
    public void SelectionChanged_WithSingleCabinet_ShowsEditableWidthAndDetails()
    {
        using var viewModel = CreateViewModel(out var eventBus);
        eventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 1",
            false)));

        var cabinetId = Guid.NewGuid();
        var scene = CreateScene(cabinetId, "base-36", "Base Cabinet 36\"", 36m);

        viewModel.OnSelectionChanged([cabinetId], scene);

        Assert.True(viewModel.HasSelection);
        Assert.True(viewModel.HasSingleSelection);
        Assert.StartsWith("Base Cabinet 36\" (", viewModel.SelectedEntityLabel);
        Assert.Equal("1 selected", viewModel.SelectionSummaryDisplay);
        Assert.Equal("Projected scene data", viewModel.SourceLabel);
        Assert.Equal("Nominal width editable", viewModel.EditabilityStatusDisplay);
        Assert.Equal("Nominal width can be resized here. Other properties remain read-only.", viewModel.EmptyStateText);
        Assert.Equal("Showing projected cabinet details. Nominal width can be resized.", viewModel.StatusMessage);
        Assert.Equal("36\"", viewModel.NominalWidthDisplay);
        Assert.Equal("36", viewModel.NominalWidthEditValue);
        Assert.Equal("6 details", viewModel.PropertySummaryDisplay);
        Assert.Equal(6, viewModel.Properties.Count);
        Assert.Equal("Cabinet ID", viewModel.Properties[0].DisplayName);
        Assert.Equal("Editability", viewModel.Properties[4].DisplayName);
        Assert.Equal("Nominal width editable", viewModel.Properties[4].DisplayValue);
    }

    [Fact]
    public void SelectionChanged_EmptySelection_ReturnsToEmptyState()
    {
        using var viewModel = CreateViewModel(out var eventBus);
        eventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 1",
            false)));

        viewModel.OnSelectionChanged([Guid.NewGuid()], CreateScene(Guid.NewGuid(), "base-36", "Base Cabinet 36\"", 36m));
        viewModel.OnSelectionChanged([]);

        Assert.False(viewModel.HasSelection);
        Assert.False(viewModel.HasSingleSelection);
        Assert.Equal("No cabinet selected", viewModel.SelectedEntityLabel);
        Assert.Equal("Nothing selected", viewModel.SelectionSummaryDisplay);
        Assert.Equal("No cabinet selected", viewModel.SourceLabel);
        Assert.Equal("No cabinet selected. Click a cabinet on the canvas to inspect it.", viewModel.EmptyStateText);
        Assert.Equal("0 details", viewModel.PropertySummaryDisplay);
        Assert.Empty(viewModel.Properties);
    }

    [Fact]
    public void SelectionChanged_WithMultipleCabinets_ShowsSelectionSummaryOnly()
    {
        using var viewModel = CreateViewModel(out var eventBus);
        eventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 1",
            false)));

        var firstCabinetId = Guid.NewGuid();
        var secondCabinetId = Guid.NewGuid();

        viewModel.OnSelectionChanged([firstCabinetId, secondCabinetId], CreateScene(firstCabinetId, "base-36", "Base Cabinet 36\"", 36m));

        Assert.True(viewModel.HasSelection);
        Assert.False(viewModel.HasSingleSelection);
        Assert.Equal("2 cabinets selected", viewModel.SelectedEntityLabel);
        Assert.Equal("2 selected", viewModel.SelectionSummaryDisplay);
        Assert.Equal("Selection summary", viewModel.SourceLabel);
        Assert.Equal("Read-only shell", viewModel.EditabilityStatusDisplay);
        Assert.Equal("Multiple selection is not yet expanded in the property inspector.", viewModel.EmptyStateText);
        Assert.Equal("0 details", viewModel.PropertySummaryDisplay);
        Assert.Equal(3, viewModel.Properties.Count);
        Assert.All(viewModel.Properties, property => Assert.False(property.IsEditable));
    }

    [Fact]
    public async Task ApplyingEdit_ResizesCabinetWidth()
    {
        using var viewModel = CreateViewModel(out var eventBus, out var runService);
        eventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 1",
            false)));

        var cabinetId = Guid.NewGuid();
        var scene = CreateScene(cabinetId, "base-36", "Base Cabinet 36\"", 36m);
        viewModel.OnSelectionChanged([cabinetId], scene);

        viewModel.BeginNominalWidthEditCommand.Execute(null);
        viewModel.NominalWidthEditValue = "42";
        await viewModel.CommitNominalWidthEditCommand.ExecuteAsync();

        Assert.True(runService.LastResizeRequest is not null);
        Assert.Equal(cabinetId, runService.LastResizeRequest!.CabinetId);
        Assert.Equal(36m, runService.LastResizeRequest.CurrentNominalWidthInches);
        Assert.Equal(42m, runService.LastResizeRequest.NewNominalWidthInches);
        Assert.False(viewModel.IsEditingNominalWidth);
        Assert.Equal("42\"", viewModel.NominalWidthDisplay);
        Assert.Equal("42", viewModel.NominalWidthEditValue);
        Assert.Equal("Cabinet width updated.", viewModel.StatusMessage);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public void ProjectClosedEvent_ResetsToNoProjectState()
    {
        using var viewModel = CreateViewModel(out var eventBus);
        eventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 1",
            false)));
        viewModel.OnSelectionChanged([Guid.NewGuid()], CreateScene(Guid.NewGuid(), "base-36", "Base Cabinet 36\"", 36m));

        eventBus.Publish(new ProjectClosedEvent(Guid.NewGuid()));

        Assert.False(viewModel.IsProjectOpen);
        Assert.False(viewModel.HasSelection);
        Assert.False(viewModel.HasSingleSelection);
        Assert.Equal("No project open", viewModel.SourceLabel);
        Assert.Equal("No editable properties", viewModel.EditabilityStatusDisplay);
        Assert.Equal("Open a project to inspect properties.", viewModel.EmptyStateText);
        Assert.Equal("Open a project to inspect properties.", viewModel.StatusMessage);
        Assert.Empty(viewModel.Properties);
        Assert.Equal("-", viewModel.NominalWidthDisplay);
    }

    private static PropertyInspectorViewModel CreateViewModel(out ApplicationEventBus eventBus) =>
        CreateViewModel(out eventBus, out _);

    private static PropertyInspectorViewModel CreateViewModel(out ApplicationEventBus eventBus, out RecordingRunService runService)
    {
        eventBus = new ApplicationEventBus();
        runService = new RecordingRunService();
        return new PropertyInspectorViewModel(runService, eventBus);
    }

    private static RenderSceneDto CreateScene(Guid cabinetId, string label, string typeDisplayName, decimal widthInches) =>
        new(
            [],
            [],
            [
                new CabinetRenderDto(
                    cabinetId,
                    Guid.NewGuid(),
                    new Rect2D(Point2D.Origin, Length.FromInches(widthInches), Length.FromInches(24m)),
                    label,
                    typeDisplayName,
                    CabinetRenderState.Normal,
                    [])
            ],
            null,
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

    private sealed class RecordingRunService : IRunService
    {
        public ResizeCabinetRequestDto? LastResizeRequest { get; private set; }

        public Task<CommandResultDto> CreateRunAsync(CreateRunRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> DeleteRunAsync(RunId runId) => throw new NotImplementedException();

        public Task<CommandResultDto> AddCabinetAsync(AddCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> InsertCabinetAsync(InsertCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> MoveCabinetAsync(MoveCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> ResizeCabinetAsync(ResizeCabinetRequestDto request)
        {
            LastResizeRequest = request;
            return Task.FromResult(new CommandResultDto(Guid.NewGuid(), "resize_cabinet", true, [], [], []));
        }

        public Task<CommandResultDto> SetCabinetOverrideAsync(SetCabinetOverrideRequestDto request) => throw new NotImplementedException();

        public RunSummaryDto GetRunSummary(RunId runId) => throw new NotImplementedException();
    }
}
