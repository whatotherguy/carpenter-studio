# UI U8 Run Summary + Issue Panel

Date: 2026-04-09

## Scope

Implemented the first useful lower shell panels in the WPF shell:

- Run Summary panel
- Issue Panel

## Current behavior

### Run Summary panel

- Renders a visible summary area with slot/list structure and explicit column labels.
- Shows an honest empty state when no active run is available.
- Does not fabricate slot rows or run metrics.
- Keeps selection context visible so the shell can still react to canvas selection changes.

### Issue Panel

- Renders a visible issue list structure with a severity badge area and list headers.
- Shows an empty state when validation issues are unavailable or none are present.
- Uses `IValidationSummaryService` when a test double or future implementation returns real data.
- Falls back to a clearly marked unavailable state when the service throws `NotImplementedException`.

## Notes

- `IRunService.GetRunSummary(RunId)` exists, but there is no active-run lookup path yet, so the run panel stays empty rather than inventing run data.
- `IValidationSummaryService` currently throws in the application layer, so the issue panel exposes structure without fake validation rows.

## Tests

- Empty state coverage for both panels.
- Populated-state coverage for the issue panel via a recording test double.
- Shell selection propagation coverage remains intact.
