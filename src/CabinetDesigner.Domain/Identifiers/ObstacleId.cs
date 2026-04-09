using System;

namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct ObstacleId(Guid Value)
{
    public static ObstacleId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
