using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Pipeline;

public interface IStateManager
{
    void RemoveEntity(string entityId, string entityType);

    void RestoreValues(string entityId, string entityType, IReadOnlyDictionary<string, DeltaValue> values);

    void RestoreEntity(string entityId, string entityType, IReadOnlyDictionary<string, DeltaValue> values);
}
