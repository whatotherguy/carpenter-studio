using CabinetDesigner.Application.Export;

namespace CabinetDesigner.Application.Services;

public sealed record CutListWorkflowResult(
    bool Success,
    CutListExportResult? Export,
    string? FileStem,
    string? FailureMessage);
