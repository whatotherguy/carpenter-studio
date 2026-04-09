using System;

namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct CabinetId(Guid Value)
{
    public static CabinetId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
