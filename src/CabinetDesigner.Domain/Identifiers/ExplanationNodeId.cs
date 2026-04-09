using System;

namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct ExplanationNodeId(Guid Value)
{
    public static ExplanationNodeId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
