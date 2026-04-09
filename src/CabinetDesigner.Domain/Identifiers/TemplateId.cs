using System;

namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct TemplateId(Guid Value)
{
    public static TemplateId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
