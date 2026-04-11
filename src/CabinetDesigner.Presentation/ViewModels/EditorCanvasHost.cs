using CabinetDesigner.Rendering;

namespace CabinetDesigner.Presentation.ViewModels;

/// <summary>
/// Preserved for backward compatibility.  New code should use
/// <see cref="WpfEditorCanvasHost"/> directly.
/// </summary>
[Obsolete("Use WpfEditorCanvasHost instead.")]
public sealed class EditorCanvasHost : WpfEditorCanvasHost
{
    public EditorCanvasHost(EditorCanvas canvas) : base(canvas)
    {
    }
}
