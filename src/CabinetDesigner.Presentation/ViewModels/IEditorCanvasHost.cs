using CabinetDesigner.Editor;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Presentation.ViewModels;

public interface IEditorCanvasHost
{
    object View { get; }

    void UpdateScene(RenderSceneDto scene);

    void UpdateViewport(ViewportTransform viewport);
}
