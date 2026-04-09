using System.Collections.Generic;

namespace CabinetDesigner.Domain.Validation;

public sealed record SuggestedFix
{
    public required string Description { get; init; }

    public required FixStrategy Strategy { get; init; }

    public required string CommandType { get; init; }

    public required IReadOnlyDictionary<string, string> Parameters { get; init; }

    public required decimal Confidence { get; init; }

    public required IReadOnlyList<string> AffectedEntityIds { get; init; }
}
