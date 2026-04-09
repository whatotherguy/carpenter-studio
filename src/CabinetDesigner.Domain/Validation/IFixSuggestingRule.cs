using System.Collections.Generic;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Domain.Validation;

public interface IFixSuggestingRule
{
    IReadOnlyList<SuggestedFix> SuggestFixes(ValidationIssue issue, ValidationContext context);
}
