using System;
using System.Collections.Generic;

namespace CabinetDesigner.Domain.TemplateContext;

public sealed record ShopStandard
{
    public string StandardId { get; init; }
    public string Name { get; init; }
    public IReadOnlyDictionary<string, OverrideValue> Parameters { get; init; }

    public ShopStandard(string standardId, string name, IReadOnlyDictionary<string, OverrideValue> parameters)
    {
        if (string.IsNullOrWhiteSpace(standardId))
            throw new InvalidOperationException("Shop standard identifier is required.");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Shop standard name is required.");

        StandardId = standardId;
        Name = name;
        Parameters = parameters;
    }
}
