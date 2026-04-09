using System;

namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct FillerId(Guid Value)
{
    public static FillerId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
