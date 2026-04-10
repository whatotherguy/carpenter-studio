using CabinetDesigner.Domain.Validation;

namespace CabinetDesigner.Application.Pipeline;

public interface IValidationResultStore
{
    FullValidationResult? Current { get; }

    void Update(FullValidationResult result);
}
