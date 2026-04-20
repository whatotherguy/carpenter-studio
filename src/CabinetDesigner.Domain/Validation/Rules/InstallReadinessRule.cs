using System.Collections.Generic;
using System.Linq;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Domain.Validation.Rules;

public sealed class InstallReadinessRule : IValidationRule
{
    public string RuleCode => "install.readiness";

    public string RuleName => "Install Readiness";

    public string Description => "Install-readiness blockers must be resolved before a revision can be approved.";

    public ValidationRuleCategory Category => ValidationRuleCategory.Installation;

    public ValidationRuleScope Scope => ValidationRuleScope.Project;

    public bool PreviewSafe => false;

    public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context) =>
        context.InstallBlockers
            .Select(blocker => new ValidationIssue(
                ValidationSeverity.Error,
                $"install.{blocker.BlockerCode}",
                blocker.Message,
                blocker.AffectedEntityIds))
            .ToArray();
}
