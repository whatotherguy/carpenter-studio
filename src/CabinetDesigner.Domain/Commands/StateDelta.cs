using System.Collections.Generic;

namespace CabinetDesigner.Domain.Commands;

public sealed record StateDelta(
    string EntityId,
    string EntityType,
    DeltaOperation Operation,
    IReadOnlyDictionary<string, DeltaValue>? PreviousValues = null,
    IReadOnlyDictionary<string, DeltaValue>? NewValues = null);
