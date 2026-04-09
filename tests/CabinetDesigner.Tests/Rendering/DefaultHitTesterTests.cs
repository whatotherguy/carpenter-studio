using CabinetDesigner.Editor;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;
using CabinetDesigner.Domain.Geometry;
using Xunit;

namespace CabinetDesigner.Tests.Rendering;

public sealed class DefaultHitTesterTests
{
    [Fact]
    public void HitTest_EmptyScene_ReturnsNone()
    {
        var scene = new RenderSceneDto([], [], [], null, new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

        var result = new DefaultHitTester().HitTest(10d, 10d, scene, ViewportTransform.Default);

        Assert.Equal(HitTestTarget.None, result.Target);
    }

    [Fact]
    public void HitTest_PointInCabinet_ReturnsCabinet()
    {
        var cabinetId = Guid.NewGuid();
        var scene = new RenderSceneDto(
            [],
            [],
            [new CabinetRenderDto(cabinetId, Guid.NewGuid(), new Rect2D(Point2D.Origin, Length.FromInches(10m), Length.FromInches(10m)), "cab", "cab", CabinetRenderState.Normal, [])],
            null,
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

        var result = new DefaultHitTester().HitTest(50d, 50d, scene, ViewportTransform.Default);

        Assert.Equal(HitTestTarget.Cabinet, result.Target);
        Assert.Equal(cabinetId, result.EntityId);
    }

    [Fact]
    public void HitTest_HandleWinsOverCabinet()
    {
        var cabinetId = Guid.NewGuid();
        var bounds = new Rect2D(Point2D.Origin, Length.FromInches(10m), Length.FromInches(10m));
        var handle = new HandleRenderDto(Guid.NewGuid(), HandleType.MoveOrigin, bounds.Center);
        var scene = new RenderSceneDto(
            [],
            [],
            [new CabinetRenderDto(cabinetId, Guid.NewGuid(), bounds, "cab", "cab", CabinetRenderState.Selected, [handle])],
            new SelectionOverlayDto([cabinetId], null, [handle]),
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

        var result = new DefaultHitTester().HitTest(50d, 50d, scene, ViewportTransform.Default);

        Assert.Equal(HitTestTarget.Handle, result.Target);
        Assert.Equal(cabinetId, result.EntityId);
        Assert.Equal(handle.HandleId, result.HandleId);
    }

    [Fact]
    public void HitTest_ReturnsRunBeforeWall()
    {
        var runId = Guid.NewGuid();
        var wallId = Guid.NewGuid();
        var scene = new RenderSceneDto(
            [new WallRenderDto(wallId, new LineSegment2D(Point2D.Origin, new Point2D(20m, 0m)), false)],
            [new RunRenderDto(runId, new LineSegment2D(Point2D.Origin, new Point2D(20m, 0m)), new Rect2D(Point2D.Origin, Length.FromInches(20m), Length.FromInches(5m)), false)],
            [],
            null,
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

        var result = new DefaultHitTester().HitTest(50d, 20d, scene, ViewportTransform.Default);

        Assert.Equal(HitTestTarget.Run, result.Target);
        Assert.Equal(runId, result.EntityId);
    }

    [Fact]
    public void HitTest_ReturnsWallWhenWithinTolerance()
    {
        var wallId = Guid.NewGuid();
        var scene = new RenderSceneDto(
            [new WallRenderDto(wallId, new LineSegment2D(Point2D.Origin, new Point2D(20m, 0m)), false)],
            [],
            [],
            null,
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

        var result = new DefaultHitTester().HitTest(50d, 4d, scene, ViewportTransform.Default);

        Assert.Equal(HitTestTarget.Wall, result.Target);
        Assert.Equal(wallId, result.EntityId);
    }
}
