using CabinetDesigner.Domain.Identifiers;
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

    public void SetHoveredCabinetId(Guid? cabinetId) => _session.SetHover(cabinetId is null ? null : new CabinetId(cabinetId.Value));

    public void ZoomAt(double screenX, double screenY, double scaleFactor)
    {
        var viewport = Viewport;
        var currentScale = (double)viewport.ScalePixelsPerInch;
        var newScale = Math.Clamp(currentScale * scaleFactor, 2.0, 200.0);
        var actualFactor = newScale / currentScale;
        var newOriginX = screenX - actualFactor * (screenX - (double)viewport.OffsetXPixels);
        var newOriginY = screenY - actualFactor * (screenY - (double)viewport.OffsetYPixels);
        _session.SetViewport(new ViewportTransform((decimal)newScale, (decimal)newOriginX, (decimal)newOriginY));
    }

    public void PanBy(double dx, double dy)
    {
        var viewport = Viewport;
        _session.SetViewport(viewport.Panned((decimal)dx, (decimal)dy));
    }
}
