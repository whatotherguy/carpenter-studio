using CabinetDesigner.Domain.Validation;

namespace CabinetDesigner.Application.Pipeline.StageResults;

public sealed record ValidationResult
{
    public required FullValidationResult Result { get; init; }

    public bool IsValid => Result.IsValid;
}
