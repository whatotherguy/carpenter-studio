using System.Collections.Generic;

namespace CabinetDesigner.Domain.Commands;

public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message,
    IReadOnlyList<string>? AffectedEntityIds = null);
