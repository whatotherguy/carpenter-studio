using System;

namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct WallId(Guid Value)
{
    public static WallId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
