using System.Collections.Generic;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Commands;

public sealed record UndoEntry(
    CommandMetadata CommandMetadata,
    IReadOnlyList<StateDelta> Deltas,
    IReadOnlyList<ExplanationNodeId> ExplanationNodeIds);
