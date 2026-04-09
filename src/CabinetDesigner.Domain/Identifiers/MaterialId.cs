using System;

namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct MaterialId(Guid Value)
{
    public static MaterialId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
