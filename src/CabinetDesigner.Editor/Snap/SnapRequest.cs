namespace CabinetDesigner.Editor.Snap;

public sealed record SnapRequest(
    EditorSceneSnapshot Scene,
    DragContext Drag,
    SnapSettings Settings);
