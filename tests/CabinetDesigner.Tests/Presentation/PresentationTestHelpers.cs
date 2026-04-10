using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Services;

namespace CabinetDesigner.Tests.Presentation;

internal sealed class CapturingAppLogger : IAppLogger
{
    public List<LogEntry> Entries { get; } = [];

    public void Log(LogEntry entry) => Entries.Add(entry);
}

internal sealed class ThrowingValidationSummaryService : IValidationSummaryService
{
    public IReadOnlyList<ValidationIssueSummaryDto> GetAllIssues() => throw new NotImplementedException();

    public IReadOnlyList<ValidationIssueSummaryDto> GetIssuesFor(string entityId) => throw new NotImplementedException();

    public bool HasManufactureBlockers => throw new NotImplementedException();
}
