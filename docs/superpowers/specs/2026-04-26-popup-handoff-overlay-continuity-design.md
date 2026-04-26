# Popup Handoff Overlay Continuity Design

Date: 2026-04-26
Status: Approved for planning
Scope: Keep blur and dim visually continuous only for popup handoff flows (one popup closes and opens another popup from that popup). Do not change general popup stacking behavior.

## Problem
When popup A opens from MainWindow, MainWindow blur+dim appears as expected. During popup handoff flows (example: Quick Add -> Add New Transaction), popup A closes and popup B opens, but the dim overlay can drop out briefly while blur remains. This creates a visible flicker and state mismatch.

## Goals
- Preserve blur+dim continuity during popup handoff transitions.
- Apply continuity for any handoff chain length (A -> B -> C -> ...).
- Keep existing visual values unchanged (blur radius, dim opacity, timing constants).

## Non-Goals
- No global behavior change for all nested popup overlays.
- No redesign of popup visual styling or animation curves.
- No dependency injection or coordinator-service refactor.

## Architecture
Use host-local overlay state in `IPopupHost` implementations (`MainWindow`, `BasePopup`) with two concepts:

1. Overlay reference count
- `ShowPopupOverlay()` increments active overlay count.
- `HidePopupOverlay()` decrements active overlay count (clamped at zero).
- Visual transitions occur only at boundaries:
  - count `0 -> 1`: apply blur + show dim (existing fade-in)
  - count `1 -> 0`: clear blur + hide dim (existing fade-out)
- Intermediate transitions do not animate/restart.

2. Handoff hold marker (handoff-only continuity)
- Add a host marker (for example `_pendingPopupHandoffCount`) to represent intentional close-then-open handoff.
- In handoff flows, current popup signals owner host before closing.
- If active overlay count reaches zero while handoff marker is active, defer hide instead of fading out immediately.
- When next popup loads and calls `ShowPopupOverlay()`, it consumes the handoff marker and keeps the visual state continuous.
- If a marked handoff does not materialize within a bounded grace period, deferred hide executes to avoid stuck overlay.
  - Grace period: one popup fade duration for that host (use existing host constant, no new timing constant).

This keeps the fix explicit to handoff paths while preserving existing non-handoff behavior.

## Components and File Impact
- `Fluxo/Views/CustomControls/IPopupHost.cs`
  - Add handoff methods/properties used by popup-switch flows and hosts.
- `Fluxo/Views/CustomControls/BasePopup.cs`
  - Implement overlay count + handoff hold logic as an `IPopupHost`.
  - Keep current template part usage (`PART_ContentRoot`, `PART_PopupOverlay`).
- `Fluxo/Views/Shell/Main/MainWindow.xaml.cs`
  - Implement same overlay count + handoff hold semantics for window-level overlay.
- Popup handoff callers (for example `QuickAddPopup`, `ExpenseDetailPopup`, `SpendingSourcesListPopup`, `SpendingSourceDetailPopup`, and any similar close-then-open patterns)
  - Signal handoff start on owner host immediately before `Close()`.

## Data Flow
Normal popup open:
1. Popup loads -> owner `ShowPopupOverlay()`
2. Host count increments; if first popup, show blur+dim.

Normal popup close (no handoff):
1. Popup closing/closed -> owner `HidePopupOverlay()`
2. Host count decrements; if reaches zero and no handoff marker, hide blur+dim.

Handoff close/open:
1. Popup A signals `BeginPopupHandoff()` on owner host.
2. Popup A closes -> `HidePopupOverlay()` decrements count.
3. If count reaches zero while handoff marker active, host defers hide.
4. Popup B opens shortly after -> `ShowPopupOverlay()` increments count and consumes marker.
5. Blur+dim remain visually unchanged throughout.

## Error Handling and Safety
- Clamp active count and handoff marker counts to zero minimum.
- Cancel opposite animations before starting new ones.
- In hide completion callbacks, re-check current state before collapsing overlay.
- Use duration-bound deferred hide guard so failed handoffs do not leave permanent dim.

## Testing Strategy
Manual verification focus:
- `Quick Add -> Add New Transaction` handoff: no dim/blur change.
- Repeated chain: `A -> B -> C` where each step closes current popup and opens next.
- Non-handoff close: last popup closes and overlay clears normally.
- Existing non-handoff nested popup paths remain unchanged.

Build verification:
- `dotnet build` for solution after implementation.

## Risks and Tradeoffs
- Risk: marker mismatch could hold overlay longer than intended.
  - Mitigation: short deferred-hide fallback and state re-check before collapse.
- Tradeoff: adds small host state complexity.
  - Benefit: precise handoff continuity without broad behavior changes.
