namespace CabinetDesigner.Rendering;

/// <summary>
/// Estimates the number of grid lines that would be drawn for a given spacing and world-space range,
/// and enforces a hard cap so that pathological inputs (tiny spacing + huge bounds) cannot freeze the UI.
/// </summary>
public static class GridLinePlanner
{
    /// <summary>
    /// Maximum number of grid lines allowed per axis (vertical or horizontal).
    /// If either axis would exceed this count the entire grid level is skipped.
    /// At typical zoom levels this limit is never reached.
    /// </summary>
    public const int MaxLinesPerAxis = 500;

    /// <summary>
    /// Returns the number of grid lines that <c>DrawGrid</c> would draw along one axis for the
    /// given <paramref name="spacingInches"/> and world-space range [<paramref name="rangeMin"/>,
    /// <paramref name="rangeMax"/>].
    /// </summary>
    /// <remarks>
    /// Matches the loop bounds used in <c>BackgroundLayer.DrawGrid</c>:
    /// <c>start = floor(min/spacing)*spacing</c>, <c>end = ceil(max/spacing)*spacing</c>.
    /// </remarks>
    public static int EstimateLineCount(decimal spacingInches, decimal rangeMin, decimal rangeMax)
    {
        if (spacingInches <= 0m || rangeMax <= rangeMin)
        {
            return 0;
        }

        try
        {
            var startIndex = Math.Floor((double)(rangeMin / spacingInches));
            var endIndex = Math.Ceiling((double)(rangeMax / spacingInches));
            var count = endIndex - startIndex + 1d;
            if (!double.IsFinite(count) || count > int.MaxValue)
            {
                return int.MaxValue;
            }

            return Math.Max(0, (int)count);
        }
        catch (OverflowException)
        {
            return int.MaxValue;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when drawing the grid at <paramref name="spacingInches"/> over
    /// the supplied 2-D bounds would exceed <see cref="MaxLinesPerAxis"/> on either axis.
    /// </summary>
    public static bool ExceedsLimit(decimal spacingInches, decimal xMin, decimal xMax, decimal yMin, decimal yMax)
    {
        return EstimateLineCount(spacingInches, xMin, xMax) > MaxLinesPerAxis
            || EstimateLineCount(spacingInches, yMin, yMax) > MaxLinesPerAxis;
    }
}
