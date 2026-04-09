using System;

namespace CabinetDesigner.Domain.Geometry;

/// <summary>
/// Tolerance constants and comparison utilities for shop-grade geometry.
/// Keeps tolerance semantics centralized and testable without polluting value objects.
/// </summary>
public static class GeometryTolerance
{
    /// <summary>
    /// Standard shop comparison tolerance: 1/64 of an inch.
    /// Use for wall-alignment checks, snap proximity, and closed-loop verification.
    /// </summary>
    public static readonly Length DefaultShopTolerance = Length.FromInches(1m / 64m);

    /// <summary>
    /// Tighter tolerance for manufactured part dimensions where 1/32" matters.
    /// </summary>
    public static readonly Length PartTolerance = Length.FromInches(1m / 32m);

    /// <summary>
    /// True if the absolute difference between a and b is within the given tolerance.
    /// Symmetric: ApproximatelyEqual(a, b, t) == ApproximatelyEqual(b, a, t).
    /// </summary>
    public static bool ApproximatelyEqual(Length a, Length b, Length tolerance)
    {
        // (a − b) and (b − a) are Offsets (signed). Both conditions together enforce
        // that the signed difference in each direction is within the tolerance bound,
        // which is equivalent to |a − b| <= tolerance without using Math.Abs on Length.
        var ab = (a - b).Inches;
        var ba = (b - a).Inches;
        return ab <= tolerance.Inches && ba <= tolerance.Inches;
    }

    /// <summary>
    /// True if the Euclidean distance between a and b is within the given tolerance.
    /// </summary>
    public static bool ApproximatelyEqual(Point2D a, Point2D b, Length tolerance)
        => a.DistanceTo(b) <= tolerance;

    /// <summary>
    /// True if the value is less than or equal to the tolerance — i.e., effectively zero
    /// for practical engineering purposes.
    /// </summary>
    public static bool IsEffectivelyZero(Length value, Length tolerance)
        => value <= tolerance;
}
