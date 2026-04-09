using System;

namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct WallOpeningId(Guid Value)
{
    public static WallOpeningId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
