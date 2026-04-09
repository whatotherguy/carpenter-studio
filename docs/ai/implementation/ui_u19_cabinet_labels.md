# U19 — Render Cabinet Labels on the Canvas

## Context

`CabinetRenderDto` has `Label` and `TypeDisplayName` properties but `CabinetLayer.Draw()` only
draws rectangles — no text.  Users see coloured boxes with no identification.

## Files to Read First

- `src/CabinetDesigner.Rendering/Layers/CabinetLayer.cs`
- `src/CabinetDesigner.Rendering/DTOs/CabinetRenderDto.cs`
- `src/CabinetDesigner.Rendering/ScreenProjection.cs`
- `src/CabinetDesigner.Rendering/EditorCanvas.cs` (to confirm WPF `#if WINDOWS` is in scope)

## Task

### 1. Add text rendering in `CabinetLayer.Draw`

All changes are inside the `#if WINDOWS` block.

Add static fields for text formatting:

```csharp
private static readonly Typeface LabelTypeface = new Typeface("Segoe UI");
private const double LabelFontSize = 10.0;
private static readonly Brush LabelBrush = Brushes.Black;
private static readonly Brush GhostLabelBrush = new SolidColorBrush(Color.FromArgb(180, 80, 80, 80));
```

Freeze `GhostLabelBrush` in a static constructor or inline (call `.Freeze()` after `new SolidColorBrush(...)`).

In `Draw`, after `drawingContext.DrawRectangle(...)`, add label rendering:

```csharp
var labelText = string.IsNullOrWhiteSpace(cabinet.Label)
    ? cabinet.TypeDisplayName
    : cabinet.Label;

if (!string.IsNullOrWhiteSpace(labelText))
{
    var textBrush = cabinet.State == CabinetRenderState.Ghost ? GhostLabelBrush : LabelBrush;
    var formattedText = new FormattedText(
        labelText,
        System.Globalization.CultureInfo.InvariantCulture,
        System.Windows.FlowDirection.LeftToRight,
        LabelTypeface,
        LabelFontSize,
        textBrush,
        VisualTreeHelper.GetDpi(/* ... */).PixelsPerDip);  // see note below

    // Clip label to cabinet bounds; centre it
    var textX = rect.X + (rect.Width - formattedText.Width) / 2.0;
    var textY = rect.Y + (rect.Height - formattedText.Height) / 2.0;

    // Only draw if the cabinet is tall enough to show text
    if (formattedText.Height <= rect.Height - 4 && formattedText.Width <= rect.Width - 4)
    {
        drawingContext.DrawText(formattedText, new System.Windows.Point(textX, textY));
    }
}
```

**Note on `PixelsPerDip`:** `FormattedText` requires a `pixelsPerDip` value.  Since `CabinetLayer`
is a stateless rendering layer (not a `Visual`), the simplest approach is to pass `1.0` (assumes
96 DPI) or read it once from a static field initialised from `CompositionTarget` when the app starts.
For simplicity use `1.0` with a comment:

```csharp
// pixelsPerDip = 1.0 assumes 96 DPI; revisit if high-DPI scaling is needed
var formattedText = new FormattedText(
    labelText,
    System.Globalization.CultureInfo.InvariantCulture,
    System.Windows.FlowDirection.LeftToRight,
    LabelTypeface,
    LabelFontSize,
    textBrush,
    pixelsPerDip: 1.0);
```

### 2. Scale font size with zoom

The font size should scale proportionally with the viewport so labels remain readable at all zoom
levels.  Pass `ScreenProjection.PixelsPerInch(viewport)` divided by a constant baseline to derive
a scaled font size:

```csharp
// Base size at 10 px/inch is LabelFontSize; scale proportionally.
var scaledFontSize = Math.Clamp(LabelFontSize * (ScreenProjection.PixelsPerInch(viewport) / 10.0), 7.0, 24.0);
```

Use `scaledFontSize` instead of the constant `LabelFontSize` when constructing `FormattedText`.

### 3. Use a shorter label when the cabinet is narrow on screen

If `rect.Width < 30` (pixels), use only the first two characters of the label (or skip entirely if
`rect.Width < 12`):

```csharp
var displayLabel = rect.Width < 12 ? string.Empty
    : rect.Width < 30 ? labelText[..Math.Min(2, labelText.Length)]
    : labelText;
```

## Requirements

- Only modify `CabinetLayer.cs` — do not change any DTO, ViewModel, or XAML.
- All changes must be inside the `#if WINDOWS` conditional block.
- Do not break the existing rendering of rectangles — the text draw happens after the rectangle.
- No external font resources needed — use system `"Segoe UI"` or `"Arial"` as fallback.

## End State

- What is now usable: Each cabinet rectangle shows its label or type name centred inside it.
- What is still missing: Unsaved-changes prompt, panel resizing, multi-select, loading indicators.
- Next prompt: U20 — Unsaved-changes prompt on close + SaveStateText staleness fix.
