using System;

namespace CabinetDesigner.Domain;

public interface IClock
{
    DateTimeOffset Now { get; }
}
