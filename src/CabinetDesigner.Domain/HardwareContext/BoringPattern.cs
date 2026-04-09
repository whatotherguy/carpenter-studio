using System;
using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Domain.HardwareContext;

public sealed record BoringPattern
{
    public Length HoleSpacing { get; init; }
    public Length EdgeSetback { get; init; }
    public int HoleCount { get; init; }

    public BoringPattern(Length holeSpacing, Length edgeSetback, int holeCount)
    {
        if (holeSpacing <= Length.Zero)
            throw new InvalidOperationException("Hole spacing must be positive.");
        if (edgeSetback <= Length.Zero)
            throw new InvalidOperationException("Edge setback must be positive.");
        if (holeCount <= 0)
            throw new InvalidOperationException("Hole count must be positive.");

        HoleSpacing = holeSpacing;
        EdgeSetback = edgeSetback;
        HoleCount = holeCount;
    }
}
