using System;

namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct HardwareItemId(Guid Value)
{
    public static HardwareItemId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
