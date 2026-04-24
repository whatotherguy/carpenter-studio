using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Presentation.ViewModels;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class StatusBarViewModelAlphaTests
{
    [Fact]
    public void AlphaLimitationEncounteredEvent_UpdatesStatusMessage()
    {
        var eventBus = new ApplicationEventBus();
        var validation = new RecordingValidationSummaryService([], hasBlockers: false);

        using var viewModel = new StatusBarViewModel(eventBus, validation);
        var limitation = AlphaLimitations.AllByCode["ALPHA-PROPERTIES-NOOP-FALLBACK"];

        eventBus.Publish(new AlphaLimitationEncounteredEvent(limitation, "cabinet:123"));

        Assert.Equal(
            $"Not yet in alpha: {limitation.Title}. {limitation.UserFacingMessage}",
            viewModel.StatusMessage);
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
