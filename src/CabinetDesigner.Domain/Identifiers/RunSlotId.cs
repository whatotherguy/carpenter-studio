using System;

namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct RunSlotId(Guid Value)
{
    public static RunSlotId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
