using System.Collections.Generic;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Domain.Validation.Rules;

public sealed class WorkflowUnapprovedChangesRule : IValidationRule
{
    public string RuleCode => "workflow.unapproved_changes";

    public string RuleName => "Unapproved Changes";

    public string Description => "The revision has unapproved changes that have not been re-approved.";

    public ValidationRuleCategory Category => ValidationRuleCategory.Workflow;

    public ValidationRuleScope Scope => ValidationRuleScope.Project;

    public bool PreviewSafe => false;

    public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context)
    {
        if (!context.WorkflowState.HasUnapprovedChanges)
        {
            return [];
        }

        return
        [
            new ValidationIssue(
                ValidationSeverity.Warning,
                RuleCode,
                "Design has unapproved changes. Re-approval is required before release workflows.",
                [])
        ];
    }
}
