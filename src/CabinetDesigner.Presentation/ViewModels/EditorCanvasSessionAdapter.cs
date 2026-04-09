using CabinetDesigner.Editor;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class EditorCanvasSessionAdapter : IEditorCanvasSession
{
    private readonly EditorSession _session;

    public EditorCanvasSessionAdapter(EditorSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public EditorMode CurrentMode => _session.Mode;

    public IReadOnlyList<Guid> SelectedCabinetIds => _session.SelectedCabinetIds.Select(id => id.Value).ToArray();

    public Guid? HoveredCabinetId => _session.HoveredCabinetId?.Value;

    public ViewportTransform Viewport => _session.Viewport;

    public void SetSelectedCabinetIds(IReadOnlyList<Guid> cabinetIds)
    {
        ArgumentNullException.ThrowIfNull(cabinetIds);
        _session.SetSelection(cabinetIds);
    }
}
