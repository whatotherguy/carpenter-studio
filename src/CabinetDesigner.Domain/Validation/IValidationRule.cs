using System.Collections.Generic;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Domain.Validation;

public interface IValidationRule
{
    string RuleCode { get; }

    string RuleName { get; }

    string Description { get; }

    ValidationRuleCategory Category { get; }

    ValidationRuleScope Scope { get; }

    bool PreviewSafe { get; }

    IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context);
}
