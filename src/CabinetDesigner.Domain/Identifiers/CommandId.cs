using System;

namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct CommandId(Guid Value)
{
    public static CommandId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
