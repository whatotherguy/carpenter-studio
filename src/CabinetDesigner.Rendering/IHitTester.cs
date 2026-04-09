using CabinetDesigner.Editor;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Rendering;

public interface IHitTester
{
    HitTestResult HitTest(double screenX, double screenY, RenderSceneDto scene, ViewportTransform viewport);
}
