using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Domain.Validation;

namespace CabinetDesigner.Application.State;

public sealed class InMemoryValidationResultStore : IValidationResultStore
{
    private volatile FullValidationResult? _current;

    public FullValidationResult? Current => _current;

    public void Update(FullValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _current = result;
    }
}
