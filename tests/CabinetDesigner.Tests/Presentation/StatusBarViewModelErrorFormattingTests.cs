using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Presentation.ViewModels;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class StatusBarViewModelErrorFormattingTests
{
    [Fact]
    public void CommandExecutionFailedEvent_FormatsStatusMessageWithCommandNameAndReference()
    {
        var eventBus = new ApplicationEventBus();
        var validation = new RecordingValidationSummaryService([], hasBlockers: false);

        using var viewModel = new StatusBarViewModel(eventBus, validation);
        var correlationId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

        eventBus.Publish(new CommandExecutionFailedEvent(
            "cabinet.resize",
            "bad width",
            new InvalidOperationException("bad width"),
            correlationId));

        Assert.Equal("Error in cabinet.resize: bad width (ref: 01234567)", viewModel.StatusMessage);
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
