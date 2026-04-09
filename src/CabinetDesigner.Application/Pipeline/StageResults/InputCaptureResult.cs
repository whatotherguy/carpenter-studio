using CabinetDesigner.Domain;

namespace CabinetDesigner.Application.Pipeline.StageResults;

public interface IDomainEntity
{
    string EntityId { get; }

    string EntityType { get; }
}

public sealed record InputCaptureResult
{
    public required IReadOnlyDictionary<string, IDomainEntity> ResolvedEntities { get; init; }

    public required IReadOnlyDictionary<string, OverrideValue> NormalizedParameters { get; init; }

    public required IReadOnlyList<TemplateExpansion> TemplateExpansions { get; init; }
}

public sealed record TemplateExpansion(
    string TemplateId,
    IReadOnlyDictionary<string, OverrideValue> ExpandedParameters);
