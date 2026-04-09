using CabinetDesigner.Editor;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Rendering;

public sealed class DefaultHitTester : IHitTester
{
    private const double HandleRadiusPixels = 8d;
    private const double WallTolerancePixels = 6d;

    public HitTestResult HitTest(double screenX, double screenY, RenderSceneDto scene, ViewportTransform viewport)
    {
        ArgumentNullException.ThrowIfNull(scene);

        foreach (var cabinet in scene.Cabinets)
        {
            foreach (var handle in cabinet.Handles)
            {
                var projectedHandle = ScreenProjection.ToScreen(handle.WorldPosition, viewport);
                var dx = projectedHandle.X - screenX;
                var dy = projectedHandle.Y - screenY;
                if ((dx * dx) + (dy * dy) <= HandleRadiusPixels * HandleRadiusPixels)
                {
                    return new HitTestResult(HitTestTarget.Handle, cabinet.CabinetId, handle.HandleId);
                }
            }
        }

        foreach (var cabinet in scene.Cabinets)
        {
            var rect = ScreenProjection.ToScreen(cabinet.WorldBounds, viewport);
            if (rect.Contains(screenX, screenY))
            {
                return new HitTestResult(HitTestTarget.Cabinet, cabinet.CabinetId, null);
            }
        }

        foreach (var run in scene.Runs)
        {
            var rect = ScreenProjection.ToScreen(run.BoundingRect, viewport);
            if (rect.Contains(screenX, screenY))
            {
                return new HitTestResult(HitTestTarget.Run, run.RunId, null);
            }
        }

        foreach (var wall in scene.Walls)
        {
            var line = ScreenProjection.ToScreen(wall.Segment, viewport);
            if (line.DistanceTo(screenX, screenY) <= WallTolerancePixels)
            {
                return new HitTestResult(HitTestTarget.Wall, wall.WallId, null);
            }
        }

        return new HitTestResult(HitTestTarget.None, null, null);
    }
}
