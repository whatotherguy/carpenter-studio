using CabinetDesigner.Domain;

namespace CabinetDesigner.Application.State;

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}
