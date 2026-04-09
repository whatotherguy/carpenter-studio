using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Domain.Commands;

public abstract record DeltaValue
{
    public sealed record OfLength(Length Value) : DeltaValue;

    public sealed record OfThickness(Thickness Value) : DeltaValue;

    public sealed record OfString(string Value) : DeltaValue;

    public sealed record OfBool(bool Value) : DeltaValue;

    public sealed record OfInt(int Value) : DeltaValue;

    public sealed record OfDecimal(decimal Value) : DeltaValue;

    public sealed record Null() : DeltaValue;
}
