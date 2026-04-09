using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Editor;

public interface IEditorSceneGraph
{
    EditorSceneSnapshot Capture();

    RunId? HitTestRun(Point2D worldPoint, Length hitRadius);
}
