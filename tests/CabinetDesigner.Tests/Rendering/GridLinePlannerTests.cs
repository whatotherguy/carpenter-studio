using CabinetDesigner.Rendering;
using Xunit;

namespace CabinetDesigner.Tests.Rendering;

public sealed class GridLinePlannerTests
{
    // -----------------------------------------------------------------------
    // EstimateLineCount — normal cases
    // -----------------------------------------------------------------------

    [Fact]
    public void EstimateLineCount_AlignedRange_ReturnsCorrectCount()
    {
        // 0 to 12 inches in 3-inch steps: lines at 0, 3, 6, 9, 12 → 5 lines
        var count = GridLinePlanner.EstimateLineCount(3m, 0m, 12m);
        Assert.Equal(5, count);
    }

    [Fact]
    public void EstimateLineCount_UnalignedRange_IncludesOuterLines()
    {
        // 1 to 11 inches in 3-inch steps:
        // start = floor(1/3)*3 = 0, end = ceil(11/3)*3 = 12
        // lines at 0, 3, 6, 9, 12 → 5 lines
        var count = GridLinePlanner.EstimateLineCount(3m, 1m, 11m);
        Assert.Equal(5, count);
    }

    [Fact]
    public void EstimateLineCount_RangeExactlyOneSpacing_ReturnsTwo()
    {
        // From 0 to 6 with 6-inch spacing: lines at 0 and 6 → 2 lines
        var count = GridLinePlanner.EstimateLineCount(6m, 0m, 6m);
        Assert.Equal(2, count);
    }

    [Fact]
    public void EstimateLineCount_LargeNormalSpacing_ReturnsReasonableCount()
    {
        // 120 inches (10 ft) at 12-inch (1 ft) spacing → 11 lines
        var count = GridLinePlanner.EstimateLineCount(12m, 0m, 120m);
        Assert.Equal(11, count);
    }

    // -----------------------------------------------------------------------
    // EstimateLineCount — zero / invalid inputs (no divide-by-zero)
    // -----------------------------------------------------------------------

    [Fact]
    public void EstimateLineCount_ZeroSpacing_ReturnsZero()
    {
        var count = GridLinePlanner.EstimateLineCount(0m, 0m, 100m);
        Assert.Equal(0, count);
    }

    [Fact]
    public void EstimateLineCount_NegativeSpacing_ReturnsZero()
    {
        var count = GridLinePlanner.EstimateLineCount(-1m, 0m, 100m);
        Assert.Equal(0, count);
    }

    [Fact]
    public void EstimateLineCount_EmptyRange_ReturnsZero()
    {
        // max < min — inverted range should return 0, not negative
        var count = GridLinePlanner.EstimateLineCount(3m, 50m, 10m);
        Assert.Equal(0, count);
    }

    [Fact]
    public void EstimateLineCount_EqualBounds_IncludesOuterGridLines()
    {
        // Degenerate range (point) expands to surrounding grid boundaries:
        // start = floor(10/3)*3 = 9, end = ceil(10/3)*3 = 12
        // lines at 9" and 12" → 2 lines, matching BackgroundLayer.DrawGrid's floor/ceil bounds
        var count = GridLinePlanner.EstimateLineCount(3m, 10m, 10m);
        Assert.Equal(2, count);
    }

    // -----------------------------------------------------------------------
    // EstimateLineCount — extreme/pathological inputs (no overflow/freeze)
    // -----------------------------------------------------------------------

    [Fact]
    public void EstimateLineCount_TinySpacingHugeBounds_ReturnsIntMaxOrLargeValue()
    {
        // 10,000 inches with 0.001-inch spacing → ~10,000,000 lines
        var count = GridLinePlanner.EstimateLineCount(0.001m, 0m, 10_000m);
        Assert.True(count > GridLinePlanner.MaxLinesPerAxis);
    }

    [Fact]
    public void EstimateLineCount_ExtremeBounds_DoesNotThrow()
    {
        // Should not throw, even for absurdly large world extents
        var exception = Record.Exception(() =>
            GridLinePlanner.EstimateLineCount(0.0001m, -1_000_000m, 1_000_000m));
        Assert.Null(exception);
    }

    [Fact]
    public void EstimateLineCount_SubEpsilonSpacing_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            GridLinePlanner.EstimateLineCount(0.000000001m, 0m, 100m));
        Assert.Null(exception);
    }

    // -----------------------------------------------------------------------
    // ExceedsLimit
    // -----------------------------------------------------------------------

    [Fact]
    public void ExceedsLimit_NormalBoundsAndSpacing_ReturnsFalse()
    {
        // Typical design: 240" × 120" room, 3" minor grid → ~80 V + ~41 H lines — well within limit
        var exceeds = GridLinePlanner.ExceedsLimit(3m, 0m, 240m, 0m, 120m);
        Assert.False(exceeds);
    }

    [Fact]
    public void ExceedsLimit_TinySpacingHugeBounds_ReturnsTrue()
    {
        // 100,000" range with 0.1" spacing → 1,000,001 lines per axis — exceeds limit
        var exceeds = GridLinePlanner.ExceedsLimit(0.1m, 0m, 100_000m, 0m, 100_000m);
        Assert.True(exceeds);
    }

    [Fact]
    public void ExceedsLimit_XAxisExceeds_ReturnsTrue()
    {
        // Only the X axis exceeds; Y is fine
        var exceeds = GridLinePlanner.ExceedsLimit(0.1m, 0m, 100_000m, 0m, 1m);
        Assert.True(exceeds);
    }

    [Fact]
    public void ExceedsLimit_YAxisExceeds_ReturnsTrue()
    {
        // Only the Y axis exceeds; X is fine
        var exceeds = GridLinePlanner.ExceedsLimit(0.1m, 0m, 1m, 0m, 100_000m);
        Assert.True(exceeds);
    }

    [Fact]
    public void ExceedsLimit_BothAxesBelowLimit_ReturnsFalse()
    {
        // Both axes within MaxLinesPerAxis
        var exceeds = GridLinePlanner.ExceedsLimit(12m, 0m, 240m, 0m, 120m);
        Assert.False(exceeds);
    }

    [Fact]
    public void ExceedsLimit_ExactlyAtLimit_ReturnsFalse()
    {
        // spacingInches = 1, range = MaxLinesPerAxis - 1 inches → count = MaxLinesPerAxis
        var rangeMax = (decimal)(GridLinePlanner.MaxLinesPerAxis - 1);
        var exceeds = GridLinePlanner.ExceedsLimit(1m, 0m, rangeMax, 0m, rangeMax);
        Assert.False(exceeds);
    }

    [Fact]
    public void ExceedsLimit_OneBeyondLimit_ReturnsTrue()
    {
        // spacingInches = 1, range = MaxLinesPerAxis inches → count = MaxLinesPerAxis + 1
        var rangeMax = (decimal)GridLinePlanner.MaxLinesPerAxis;
        var exceeds = GridLinePlanner.ExceedsLimit(1m, 0m, rangeMax, 0m, rangeMax);
        Assert.True(exceeds);
    }

    [Fact]
    public void ExceedsLimit_ZeroSpacing_ReturnsFalse()
    {
        // Zero spacing produces no lines, so it never exceeds
        var exceeds = GridLinePlanner.ExceedsLimit(0m, 0m, 10_000m, 0m, 10_000m);
        Assert.False(exceeds);
    }
}
