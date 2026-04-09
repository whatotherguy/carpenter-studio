namespace CabinetDesigner.Presentation.ViewModels;

public sealed record IssueRowViewModel(
    string Severity,
    string Code,
    string Message,
    IReadOnlyList<string> AffectedEntityIds);
