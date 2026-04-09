using System;

namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct OpeningId(Guid Value)
{
    public static OpeningId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
