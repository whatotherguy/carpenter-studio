using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Editor;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class EditorCanvasSessionAdapter : IEditorCanvasSession
{
    private const double MinScalePixelsPerInch = 2.0;
    private const double MaxScalePixelsPerInch = 200.0;

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
        var newScale = Math.Clamp(currentScale * scaleFactor, MinScalePixelsPerInch, MaxScalePixelsPerInch);
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

    public void BeginPan() => _session.BeginPan();

    public void EndPan() => _session.EndPan();

    public void FitViewport(Rect2D contentBounds, double canvasWidth, double canvasHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            return;
        }

        var contentWidthInches = (double)(contentBounds.Max.X - contentBounds.Min.X);
        var contentHeightInches = (double)(contentBounds.Max.Y - contentBounds.Min.Y);

        if (contentWidthInches < 0 || contentHeightInches < 0)
        {
            return;
        }

        // Allow line- or point-like content (zero extent in one or both axes) to be fit
        // by substituting a very small dimension instead of treating zero extent as "nothing to fit".
        const double minimumContentDimensionInches = 1e-6;
        var effectiveContentWidthInches = Math.Max(contentWidthInches, minimumContentDimensionInches);
        var effectiveContentHeightInches = Math.Max(contentHeightInches, minimumContentDimensionInches);

        // Leave a 10 % margin on each side (i.e. use 80 % of the canvas for the content).
        const double marginFactor = 0.8;
        var scaleX = canvasWidth * marginFactor / effectiveContentWidthInches;
        var scaleY = canvasHeight * marginFactor / effectiveContentHeightInches;
        var scale = Math.Clamp(Math.Min(scaleX, scaleY), MinScalePixelsPerInch, MaxScalePixelsPerInch);

        // Centre the content in the canvas.
        var contentCentreWorldX = (double)((contentBounds.Min.X + contentBounds.Max.X) / 2);
        var contentCentreWorldY = (double)((contentBounds.Min.Y + contentBounds.Max.Y) / 2);
        var offsetX = (canvasWidth / 2) - (contentCentreWorldX * scale);
        var offsetY = (canvasHeight / 2) - (contentCentreWorldY * scale);

        _session.SetViewport(new ViewportTransform((decimal)scale, (decimal)offsetX, (decimal)offsetY));
    }

    public void ResetViewport() => _session.ResetViewport();
}
