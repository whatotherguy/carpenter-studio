using System;
using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Domain.RunContext;

public sealed record EndCondition
{
    public EndConditionType Type { get; init; }
    public Length? FillerWidth { get; init; }

    public EndCondition(EndConditionType type, Length? fillerWidth = null)
    {
        if (type is EndConditionType.Filler or EndConditionType.Scribe)
        {
            if (fillerWidth is null)
                throw new ArgumentException($"{type} end condition requires a width.", nameof(fillerWidth));
            if (fillerWidth <= Length.Zero)
                throw new ArgumentException($"{type} end condition width must be greater than zero.", nameof(fillerWidth));
        }

        Type = type;
        FillerWidth = fillerWidth;
    }

    public static EndCondition Open() => new(EndConditionType.Open);
    public static EndCondition AgainstWall() => new(EndConditionType.AgainstWall);
    public static EndCondition AdjacentCabinet() => new(EndConditionType.AdjacentCabinet);
    public static EndCondition WithFiller(Length width) => new(EndConditionType.Filler, width);
    public static EndCondition WithScribe(Length width) => new(EndConditionType.Scribe, width);
}
