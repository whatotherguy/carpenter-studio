using System;
using System.Collections.Generic;
using System.Linq;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Domain.Validation;

public sealed class ValidationEngine : IValidationEngine
{
    private readonly IReadOnlyList<IValidationRule> _rules;
    private readonly IReadOnlyList<IValidationRule> _previewSafeRules;
    private readonly IReadOnlyDictionary<ValidationRuleCategory, IReadOnlyList<IValidationRule>> _rulesByCategory;

    public ValidationEngine(IEnumerable<IValidationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = rules
            .OrderBy(rule => rule.RuleCode, StringComparer.Ordinal)
            .ToArray();
        _previewSafeRules = _rules
            .Where(rule => rule.PreviewSafe)
            .ToArray();
        _rulesByCategory = _rules
            .GroupBy(rule => rule.Category)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<IValidationRule>)group
                    .OrderBy(rule => rule.RuleCode, StringComparer.Ordinal)
                    .ToArray());
    }

    public IReadOnlyList<IValidationRule> RegisteredRules => _rules;

    public FullValidationResult Validate(ValidationContext context) =>
        new()
        {
            CrossCuttingIssues = EvaluateRules(_rules, context),
            ContextualIssues = []
        };

    public IReadOnlyList<ValidationIssue> ValidatePreview(ValidationContext context) =>
        _previewSafeRules
            .SelectMany(rule => rule.Evaluate(context))
            .ToArray();

    public IReadOnlyList<ExtendedValidationIssue> ValidateCategory(
        ValidationContext context,
        ValidationRuleCategory category) =>
        _rulesByCategory.TryGetValue(category, out var rules)
            ? EvaluateRules(rules, context)
            : [];

    private static IReadOnlyList<ExtendedValidationIssue> EvaluateRules(
        IReadOnlyList<IValidationRule> rules,
        ValidationContext context)
    {
        var results = new List<ExtendedValidationIssue>();
        var seenIssueIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in rules)
        {
            foreach (var issue in rule.Evaluate(context))
            {
                var issueId = new ValidationIssueId(rule.RuleCode, issue.AffectedEntityIds ?? []);
                if (!seenIssueIds.Add(issueId.Value))
                {
                    continue;
                }

                results.Add(new ExtendedValidationIssue
                {
                    IssueId = issueId,
                    Issue = issue,
                    RuleCode = rule.RuleCode,
                    Category = rule.Category,
                    Scope = rule.Scope,
                    SuggestedFixes = GenerateFixes(rule, issue, context)
                });
            }
        }

        return results;
    }

    private static IReadOnlyList<SuggestedFix> GenerateFixes(
        IValidationRule rule,
        ValidationIssue issue,
        ValidationContext context) =>
        rule is IFixSuggestingRule fixSuggestingRule
            ? fixSuggestingRule
                .SuggestFixes(issue, context)
                .OrderByDescending(fix => fix.Confidence)
                .ThenBy(fix => fix.CommandType, StringComparer.Ordinal)
                .ToArray()
            : [];
}
