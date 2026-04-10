using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Presentation.ViewModels;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class IssuePanelViewModelTests
{
    [Fact]
    public void InitialState_ShowsEmptyStateWhenValidationIsUnavailable()
    {
        var eventBus = new ApplicationEventBus();
        using var viewModel = new IssuePanelViewModel(new ThrowingValidationSummaryService(), eventBus);

        Assert.True(viewModel.IsPlaceholderData);
        Assert.Equal("Placeholder validation data", viewModel.SourceLabel);
        Assert.Equal("0E 0W 0I", viewModel.CountSummaryDisplay);
        Assert.False(viewModel.HasManufactureBlockers);
        Assert.Equal("No validation issues are available yet.", viewModel.EmptyStateText);
        Assert.Empty(viewModel.AllIssues);
        Assert.Empty(viewModel.FilteredIssues);
        Assert.False(viewModel.HasFilteredIssues);
        Assert.Equal("Validation issues are not available yet.", viewModel.StatusMessage);
    }

    [Fact]
    public void EmptyValidationData_StillShowsEmptyState()
    {
        var eventBus = new ApplicationEventBus();
        using var viewModel = new IssuePanelViewModel(new EmptyValidationSummaryService(), eventBus);

        Assert.False(viewModel.IsPlaceholderData);
        Assert.Equal("Validation service data", viewModel.SourceLabel);
        Assert.Equal("0E 0W 0I", viewModel.CountSummaryDisplay);
        Assert.Equal("No validation issues.", viewModel.EmptyStateText);
        Assert.Empty(viewModel.AllIssues);
        Assert.Empty(viewModel.FilteredIssues);
        Assert.False(viewModel.HasManufactureBlockers);
        Assert.Equal("No validation issues.", viewModel.StatusMessage);
    }

    [Fact]
    public void PopulatedValidationData_ProducesSeverityCountsAndSelectionNavigation()
    {
        var eventBus = new ApplicationEventBus();
        var errorId = Guid.NewGuid().ToString();
        var warningId = Guid.NewGuid().ToString();
        var blockerId = Guid.NewGuid().ToString();
        using var viewModel = new IssuePanelViewModel(
            new RecordingValidationSummaryService(
                [
                    new ValidationIssueSummaryDto("Error", "E-1", "Broken", [errorId]),
                    new ValidationIssueSummaryDto("Warning", "W-1", "Careful", [warningId]),
                    new ValidationIssueSummaryDto("Info", "I-1", "FYI", []),
                    new ValidationIssueSummaryDto("ManufactureBlocker", "MB-1", "Stop", [blockerId])
                ],
                hasBlockers: true),
            eventBus);

        IReadOnlyList<Guid>? selected = null;
        viewModel.SetSelectionCallback(ids => selected = ids.ToArray());

        Assert.False(viewModel.IsPlaceholderData);
        Assert.Equal("Validation service data", viewModel.SourceLabel);
        Assert.Equal("1E 1W 1I", viewModel.CountSummaryDisplay);
        Assert.True(viewModel.HasManufactureBlockers);
        Assert.Equal(4, viewModel.AllIssues.Count);
        Assert.Equal(4, viewModel.FilteredIssues.Count);

        var issue = viewModel.FilteredIssues.First(item => item.Severity == "Error");
        viewModel.GoToEntityCommand.Execute(issue);

        Assert.NotNull(selected);
        Assert.Single(selected!);
        Assert.Equal("Selected 1 affected entity.", viewModel.StatusMessage);

        viewModel.SeverityFilter = "Warning";

        Assert.Single(viewModel.FilteredIssues);
        Assert.All(viewModel.FilteredIssues, item => Assert.Equal("Warning", item.Severity));
    }

    [Fact]
    public void ProjectClosedEvent_ClearsValidationFilterAndRefreshesCommandState()
    {
        var eventBus = new ApplicationEventBus();
        using var viewModel = new IssuePanelViewModel(
            new RecordingValidationSummaryService(
                [
                    new ValidationIssueSummaryDto("Error", "E-1", "Broken", [Guid.NewGuid().ToString()])
                ],
                hasBlockers: true),
            eventBus);

        var canExecuteChangedCount = 0;
        viewModel.GoToEntityCommand.CanExecuteChanged += (_, _) => canExecuteChangedCount++;

        viewModel.SeverityFilter = "Error";
        eventBus.Publish(new ProjectClosedEvent(Guid.NewGuid()));

        Assert.True(canExecuteChangedCount > 0);
        Assert.True(viewModel.IsPlaceholderData);
        Assert.Equal("Placeholder validation data", viewModel.SourceLabel);
        Assert.Null(viewModel.SeverityFilter);
        Assert.Empty(viewModel.FilteredIssues);
        Assert.Equal("Validation issues are not available while no project is open.", viewModel.StatusMessage);
    }

    [Fact]
    public void RefreshIssues_WhenServiceThrowsNotImplementedException_LogsWarningAndShowsPlaceholder()
    {
        var logger = new CapturingAppLogger();
        var eventBus = new ApplicationEventBus();
        using var viewModel = new IssuePanelViewModel(new ThrowingValidationSummaryService(), eventBus, logger);

        Assert.True(viewModel.IsPlaceholderData);
        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, logger.Entries[0].Level);
        Assert.Equal("IssuePanelViewModel", logger.Entries[0].Category);
        Assert.NotNull(logger.Entries[0].Exception);
        Assert.IsType<NotImplementedException>(logger.Entries[0].Exception);
    }

    [Fact]
    public void DesignChangedEvent_WhenServiceThrowsNotImplementedException_LogsWarningOnEachRefresh()
    {
        var logger = new CapturingAppLogger();
        var eventBus = new ApplicationEventBus();
        using var viewModel = new IssuePanelViewModel(new ThrowingValidationSummaryService(), eventBus, logger);

        // Initial construction already triggers one refresh (one log entry already recorded).
        var countAfterConstruct = logger.Entries.Count;

        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        Assert.Equal(countAfterConstruct + 1, logger.Entries.Count);
        Assert.All(logger.Entries, entry => Assert.Equal(LogLevel.Warning, entry.Level));
    }

    private sealed class CapturingAppLogger : IAppLogger
    {
        public List<LogEntry> Entries { get; } = [];

        public void Log(LogEntry entry) => Entries.Add(entry);
    }

    private sealed class ThrowingValidationSummaryService : IValidationSummaryService
    {
        public IReadOnlyList<ValidationIssueSummaryDto> GetAllIssues() => throw new NotImplementedException();

        public IReadOnlyList<ValidationIssueSummaryDto> GetIssuesFor(string entityId) => throw new NotImplementedException();

        public bool HasManufactureBlockers => throw new NotImplementedException();
    }

    private sealed class EmptyValidationSummaryService : IValidationSummaryService
    {
        public IReadOnlyList<ValidationIssueSummaryDto> GetAllIssues() => [];

        public IReadOnlyList<ValidationIssueSummaryDto> GetIssuesFor(string entityId) => [];

        public bool HasManufactureBlockers => false;
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
