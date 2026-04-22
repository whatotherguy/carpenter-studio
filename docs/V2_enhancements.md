# V2 Enhancements â€” Deferred After V1

This file is the authoritative list of Carpenter Studio features that are
**out of scope for V1** and deferred to V2. Every V1 code prompt must:

1. Ensure the V1 pipeline and UI work correctly without any V2 feature.
2. Mark the extension point in code with a `// V2:` comment naming the
   feature.
3. Add or update the corresponding bullet below.

V1 focus: rooms, visual editor, cabinet CRUD, cut list export. See
`docs/ai/v1_finish/v1_universal_rules.md` for the full definition.

---

## Catalog & Pricing

- External catalog API integration (Richelieu, Blum, Hafele, local mill
  vendors). V1 uses a local `CatalogService` with seeded shop defaults for
  **material/thickness only** â€” no prices, no vendor identifiers.
- Real material pricing (per sqft, per sheet) driven by live vendor data
  or shop-managed price lists.
- Hardware pricing (hinges, slides, pulls) and vendor SKUs.
- Labor and install rate management (policy, per-operator, per-shop).
- Cost totals, markup, tax, and revision cost deltas. The `CostingStage`
  must be fully decoupled in V1 (see `v1_prompt_pack.md#P02`): when
  pricing is not configured, the stage emits a non-blocking
  `COSTING_NOT_CONFIGURED` informational result and the pipeline still
  succeeds.
- Cost breakdown panel in the UI. V1 shows cost as "N/A â€” pricing not
  configured".

## Bid / Quote Generation

- Customer-facing bid documents, pricing tiers, discounts, deposit logic.
- Quote acceptance / e-sign flow.
- PDF bid templates with shop branding.

## Installation Planning

- Step-by-step, site-specific install plans for carpenters in the field.
  V1 emits engineering-level install steps internally for determinism,
  but V2 adds:
  - Site constraints (ceiling irregularity, floor slope, scribe needs).
  - Install sequencing across multiple rooms.
  - Printable, crew-ready install docs.

## Vendor / Manufacturing Details

- Per-vendor boring patterns for hinges and slides.
- Vendor-specific clearance rules (door overlay, drawer box clearance).
- Hinge/slide product systems (soft-close, full-extension, concealed vs
  European, etc.).
- Grain policy beyond V1's `Lengthwise` / `None` defaults: book-matching,
  slip-matching, veneer sequencing.
- Material-yield optimization / nesting layout for sheet goods.
- G-code / CNC post export.
- Cut-list row collapsing and quantity aggregation. V1 exports one row per
  generated part for determinism; V2 can add duplicate-part consolidation
  once the shop wants summarized counts.

## Revisions & Approvals Polish

- Visual revision compare (side-by-side canvas diff).
- Approval workflow with roles, comments, sign-off audit trail. V1
  packaging produces a deterministic, hash-addressed snapshot and rejects
  invalid designs, but does not surface a full review UI.
- Cost delta reporting between approved revisions.

## Collaboration & Platform

- Cloud project sync, multi-user editing, comment threads.
- Plug-in system for custom cabinet templates and constraint rules.
- Non-Windows host (macOS, web) support beyond the WPF shell.

---

## V1 Extension Points to Watch

Whenever a `// V2:` comment is added to the codebase, list the source
location below so the V2 cut-over is discoverable:

- `src/CabinetDesigner.Application/Services/CatalogService.cs:31` `IsPricingConfigured` pricing reactivation hook — seed real vendor prices here.
- `src/CabinetDesigner.Application/Services/CatalogService.cs:106` `ResolveHardwareForOpening` returns empty list in V1; V2 will return vendor hinge/slide IDs per opening type (Door → hinge, Drawer → slide).
- `src/CabinetDesigner.Application/Projection/ManufacturingProjector.cs:186` hardware readiness — V2 vendor hardware catalog will surface hinge/slide blockers here.
- `src/CabinetDesigner.Application/Pipeline/Parts/PartGeometry.cs:17` drawer box clearance constants (`DrawerBoxHeightClearance`, `DrawerBoxDepthClearance`, `DrawerBoxWidthClearance`) — V1 shop defaults; V2 derives from slide manufacturer specs.
- `src/CabinetDesigner.Application/Pipeline/Parts/PartGeometry.cs:21` drawer box panel thickness constants (`DrawerBoxBottomThickness`, `DrawerBoxSideThickness`) — V1 shop defaults; V2 pulls from vendor material specs.
- `src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs:215` calculated costing branch — holds V2 vendor pricing and cost totals once pricing is configured.
- `src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs:110` costing serialization branch — swap the canonical no-pricing snapshot for vendor-priced details so the packaging hash reflects real costs.
- `src/CabinetDesigner.Application/Export/CutListExporter.cs:92` quantity column stays `1` per generated part in V1; V2 can collapse identical rows into aggregated counts.
