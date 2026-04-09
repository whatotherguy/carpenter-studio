using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;
using Xunit;

namespace CabinetDesigner.Tests.Rendering;

public sealed class SelectionOverlayFactoryTests
{
    [Fact]
    public void Create_SingleSelection_ReturnsHandlesWithoutMultiBounds()
    {
        var cabinetId = Guid.NewGuid();
        var bounds = new Rect2D(Point2D.Origin, Length.FromInches(24m), Length.FromInches(12m));
        var cabinet = new CabinetRenderDto(
            cabinetId,
            Guid.NewGuid(),
            bounds,
            "cab",
            "base",
            CabinetRenderState.Normal,
            CabinetRenderModelFactory.CreateHandles(cabinetId, bounds));

        var overlay = SelectionOverlayFactory.Create([cabinet], [cabinetId]);

        Assert.NotNull(overlay);
        Assert.Null(overlay!.MultiSelectionBounds);
        Assert.Equal(2, overlay.Handles.Count);
    }

    [Fact]
    public void Create_MultiSelection_ReturnsCombinedBounds()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var first = CreateCabinet(firstId, new Rect2D(Point2D.Origin, Length.FromInches(24m), Length.FromInches(12m)));
        var second = CreateCabinet(secondId, new Rect2D(new Point2D(30m, 6m), Length.FromInches(18m), Length.FromInches(12m)));

        var overlay = SelectionOverlayFactory.Create([first, second], [firstId, secondId]);

        Assert.NotNull(overlay);
        Assert.Equal(new Point2D(0m, 0m), overlay!.MultiSelectionBounds!.Value.Min);
        Assert.Equal(new Point2D(48m, 18m), overlay.MultiSelectionBounds.Value.Max);
        Assert.Equal(4, overlay.Handles.Count);
    }

    [Fact]
    public void CalculateSceneBounds_IncludesRunsCabinetsAndWalls()
    {
        var scene = new RenderSceneDto(
            [new WallRenderDto(Guid.NewGuid(), new LineSegment2D(new Point2D(-12m, -4m), new Point2D(0m, -4m)), false)],
            [new RunRenderDto(Guid.NewGuid(), new LineSegment2D(Point2D.Origin, new Point2D(24m, 0m)), new Rect2D(Point2D.Origin, Length.FromInches(24m), Length.FromInches(24m)), false)],
            [CreateCabinet(Guid.NewGuid(), new Rect2D(new Point2D(30m, 10m), Length.FromInches(12m), Length.FromInches(12m)))],
            null,
            new GridSettingsDto(true, Length.FromInches(12m), Length.FromInches(3m)));

        var bounds = RenderSceneBoundsCalculator.Calculate(scene);

        Assert.NotNull(bounds);
        Assert.Equal(new Point2D(-12m, -4m), bounds!.Value.Min);
        Assert.Equal(new Point2D(42m, 24m), bounds.Value.Max);
    }

    private static CabinetRenderDto CreateCabinet(Guid cabinetId, Rect2D bounds) =>
        new(
            cabinetId,
            Guid.NewGuid(),
            bounds,
            "cab",
            "base",
            CabinetRenderState.Normal,
            CabinetRenderModelFactory.CreateHandles(cabinetId, bounds));
}
