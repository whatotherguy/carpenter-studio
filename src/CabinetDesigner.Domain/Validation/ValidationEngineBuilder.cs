using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CabinetDesigner.Domain.Validation;

public sealed class ValidationEngineBuilder
{
    private readonly List<IValidationRule> _rules = [];

    public ValidationEngineBuilder AddRule(IValidationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (_rules.Any(existing => string.Equals(existing.RuleCode, rule.RuleCode, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Duplicate rule code: {rule.RuleCode}. Each rule must have a unique code.");
        }

        _rules.Add(rule);
        return this;
    }

    public ValidationEngineBuilder AddRulesFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var ruleTypes = assembly.GetTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                typeof(IValidationRule).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var ruleType in ruleTypes)
        {
            var rule = (IValidationRule?)Activator.CreateInstance(ruleType);
            if (rule is null)
            {
                throw new InvalidOperationException($"Could not create validation rule {ruleType.FullName}.");
            }

            AddRule(rule);
        }

        return this;
    }

    public IValidationEngine Build() => new ValidationEngine(_rules);
}
