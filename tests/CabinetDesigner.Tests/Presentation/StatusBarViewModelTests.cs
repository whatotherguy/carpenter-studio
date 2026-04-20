using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Presentation.ViewModels;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class StatusBarViewModelTests
{
    [Fact]
    public void InitialState_UsesReadyDefaults()
    {
        var eventBus = new ApplicationEventBus();
        var validation = new RecordingValidationSummaryService(
            [
                new ValidationIssueSummaryDto("Error", "E-1", "Broken", []),
                new ValidationIssueSummaryDto("Warning", "W-1", "Careful", []),
                new ValidationIssueSummaryDto("Info", "I-1", "FYI", [])
            ],
            hasBlockers: true);

        using var viewModel = new StatusBarViewModel(eventBus, validation);

        Assert.Equal(1, viewModel.ErrorCount);
        Assert.Equal(1, viewModel.WarningCount);
        Assert.Equal(1, viewModel.InfoCount);
        Assert.True(viewModel.HasManufactureBlockers);
        Assert.Equal("No revision", viewModel.RevisionLabel);
        Assert.Equal("Saved", viewModel.SaveStateDisplay);
        Assert.Equal("Ready", viewModel.StatusMessage);
        Assert.Equal("1E 1W 1I", viewModel.IssueSummaryDisplay);
    }

    [Fact]
    public void ProjectAndCanvasEvents_UpdateVisibleState()
    {
        var eventBus = new ApplicationEventBus();
        var validation = new RecordingValidationSummaryService([], hasBlockers: false);

        using var viewModel = new StatusBarViewModel(eventBus, validation);

        var project = new ProjectSummaryDto(
            Guid.NewGuid(),
            "Shop A",
            "C:\\shop.cab",
            DateTimeOffset.UtcNow,
            "Draft v4",
            true);

        eventBus.Publish(new ProjectOpenedEvent(project));
        viewModel.SetStatusMessage("Cabinet selected.");

        Assert.Equal("Draft v4", viewModel.RevisionLabel);
        Assert.Equal("Unsaved changes", viewModel.SaveStateDisplay);
        Assert.Equal("Cabinet selected.", viewModel.StatusMessage);

        eventBus.Publish(new ProjectClosedEvent(project.ProjectId));

        Assert.Equal("No revision", viewModel.RevisionLabel);
        Assert.Equal("Saved", viewModel.SaveStateDisplay);
        Assert.Equal("0E 0W 0I", viewModel.IssueSummaryDisplay);
        Assert.False(viewModel.HasManufactureBlockers);
        viewModel.SetStatusMessage("   ");
        Assert.Equal("Ready", viewModel.StatusMessage);
    }

    [Fact]
    public void DesignChangeEvent_RefreshesValidationCounts()
    {
        var eventBus = new ApplicationEventBus();
        var validation = new RecordingValidationSummaryService(
            [
                new ValidationIssueSummaryDto("Error", "E-1", "Broken", []),
                new ValidationIssueSummaryDto("Error", "E-2", "Still broken", []),
                new ValidationIssueSummaryDto("Warning", "W-1", "Careful", []),
                new ValidationIssueSummaryDto("Info", "I-1", "FYI", [])
            ],
            hasBlockers: true);

        using var viewModel = new StatusBarViewModel(eventBus, validation);

        validation.Issues =
        [
            new ValidationIssueSummaryDto("Error", "E-1", "Broken", []),
            new ValidationIssueSummaryDto("Info", "I-1", "FYI", [])
        ];
        validation.HasBlockers = false;

        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "layout.update", true, [], [], [])));

        Assert.Equal(1, viewModel.ErrorCount);
        Assert.Equal(0, viewModel.WarningCount);
        Assert.Equal(1, viewModel.InfoCount);
        Assert.False(viewModel.HasManufactureBlockers);
    }

    private sealed class RecordingValidationSummaryService : IValidationSummaryService
    {
        public RecordingValidationSummaryService(IReadOnlyList<ValidationIssueSummaryDto> issues, bool hasBlockers)
        {
            Issues = issues;
            HasBlockers = hasBlockers;
        }

        public IReadOnlyList<ValidationIssueSummaryDto> Issues { get; set; }

        public bool HasBlockers { get; set; }

        public IReadOnlyList<ValidationIssueSummaryDto> GetAllIssues() => Issues;

        public IReadOnlyList<ValidationIssueSummaryDto> GetIssuesFor(string entityId) => Issues;

        public bool HasManufactureBlockers => HasBlockers;
    }
}
