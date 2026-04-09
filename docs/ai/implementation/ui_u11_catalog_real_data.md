# UI U11 Catalog Real Data

Date: 2026-04-09
Scope: `CabinetDesigner.Presentation`, `CabinetDesigner.Application` catalog adapter, and `tests/CabinetDesigner.Tests/Presentation`

## What Changed

- Replaced the catalog panel's placeholder list with a presentation-safe catalog service backed by built-in cabinet template data.
- Kept the catalog searchable in the shell and updated the copy so it no longer claims to be placeholder data.
- Preserved the empty-state behavior and the activation hook for future interactions.
- Updated presentation tests to cover initial load, filtering, empty state, and item activation.

## Implementation Notes

- `CatalogService` now provides a small read-only catalog read model from `CabinetDesigner.Application`.
- `CatalogPanelViewModel` maps those DTOs into presentation rows and filters them in memory.
- The catalog remains visible in the left shell rail without requiring project selection.
- The panel UI still uses the existing shell layout and item card structure.

## What Is Now Visibly Usable

- A searchable catalog in the left shell rail that shows cabinet type ids, names, categories, descriptions, and default widths.
- An honest catalog badge that no longer claims the data is placeholder content.
- A clear empty state when search terms do not match anything.

## What Is Still Placeholder

- Drag-and-drop from the catalog into the canvas is still not implemented.
- The catalog data is still built-in session data rather than a project-backed or database-backed catalog.
- No category picker or activation navigation has been added yet.

## Next Prompt Should Tackle

- Connect the catalog rows to drag/drop or add-to-run behavior once the canvas drop target is ready.
- Replace the built-in catalog seed list with a persisted catalog source if one becomes available.
- Add category filtering only if it can be wired without changing the current shell layout.
