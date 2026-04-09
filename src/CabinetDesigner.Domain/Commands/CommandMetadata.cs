using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Commands;

public sealed record CommandMetadata
{
    public required CommandId CommandId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required CommandOrigin Origin { get; init; }
    public required string IntentDescription { get; init; }
    public required IReadOnlyList<string> AffectedEntityIds { get; init; }
    public CommandId? ParentCommandId { get; init; }

    public static CommandMetadata Create(
        DateTimeOffset timestamp,
        CommandOrigin origin,
        string intentDescription,
        IReadOnlyList<string> affectedEntityIds,
        CommandId? parentCommandId = null) => new()
    {
        CommandId = CommandId.New(),
        Timestamp = timestamp,
        Origin = origin,
        IntentDescription = intentDescription,
        AffectedEntityIds = affectedEntityIds,
        ParentCommandId = parentCommandId
    };
}
