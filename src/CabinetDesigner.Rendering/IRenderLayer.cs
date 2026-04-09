#if WINDOWS
using System.Windows.Media;
using CabinetDesigner.Editor;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Rendering;

public interface IRenderLayer
{
    void Draw(DrawingContext drawingContext, RenderSceneDto scene, ViewportTransform viewport);
}
#endif
