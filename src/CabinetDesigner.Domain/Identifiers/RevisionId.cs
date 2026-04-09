using System;

namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct RevisionId(Guid Value)
{
    public static RevisionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
