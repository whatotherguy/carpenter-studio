using System.Collections.Generic;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Domain.Validation;

public interface IValidationEngine
{
    FullValidationResult Validate(ValidationContext context, IReadOnlyList<ValidationIssue>? contextualIssues = null);

    IReadOnlyList<ValidationIssue> ValidatePreview(ValidationContext context);

    IReadOnlyList<ExtendedValidationIssue> ValidateCategory(
        ValidationContext context,
        ValidationRuleCategory category);

    IReadOnlyList<IValidationRule> RegisteredRules { get; }
}
