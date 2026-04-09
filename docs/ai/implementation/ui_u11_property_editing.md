# UI U11 Property Editing

Date: 2026-04-09
Scope: `CabinetDesigner.Application`, `CabinetDesigner.Presentation`, and `tests/CabinetDesigner.Tests/Presentation`

## What Changed

- Replaced the property inspector's read-only placeholder copy with a single supported edit flow: nominal width resize for a single selected cabinet.
- Kept the inspector selection-aware and left multi-selection as a clear fallback instead of pretending broader editing exists.
- Wired the inspector to the existing application-layer resize command, so the first editable field now commits through real application behavior.
- Strengthened presentation tests for no selection, single selection, multi-selection, resize application, and project-close reset behavior.

## Implementation Notes

- `PropertyInspectorViewModel` now tracks project state, selection state, projected cabinet width, and the width edit lifecycle.
- The inspector only enables editing when there is a single selected cabinet with a projected scene entry.
- The read-only property rows remain for cabinet identity and selection context, while the width editor is shown as a separate card so unsupported fields stay read-only.
- `RunService.ResizeCabinetAsync` now dispatches the existing resize command instead of throwing, which makes the width edit flow real end-to-end.
- Project close still clears selection-facing state and returns the inspector to the no-project empty state.

## What Is Now Visibly Usable

- A property inspector that shows selected cabinet details and a real editable nominal-width control for single selection.
- A working width resize flow that commits through the application layer.
- Honest empty states for no project and no selection, plus a clear multi-selection fallback.

## What Is Still Placeholder

- Only cabinet width is editable right now.
- Multi-selection editing is still not supported.
- Inline override editing, richer property metadata, and reverse navigation are still out of scope.

## Next Prompt Should Tackle

- Add the next supported edit field only if the application layer exposes it cleanly.
- Replace the remaining selection-summary fallback with richer editable property metadata if that becomes available.
- Keep the shell layout stable while expanding the inspector one supported field at a time.
