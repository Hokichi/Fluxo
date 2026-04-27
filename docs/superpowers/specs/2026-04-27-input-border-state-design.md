# Input Border State Behavior Design

Date: 2026-04-27  
Project: Fluxo  
Scope: Rounded corner `TextBox`, `MoneyTextBox`, `DateSelector`, `ComboBox`

## Objective
Add consistent border-color behaviors for input controls:
- Focused/highlighted/tab-focused: border uses `Brush.Mint`
- Invalid/erroneous (`Validation.HasError=True`): border uses `Brush.Danger`
- Precedence: `Invalid > Focused > Default`

## Existing Context
- Rounded input styles are defined in:
  - `Fluxo/Resources/Styles/TextBoxStyles.xaml`
  - `Fluxo/Resources/Styles/GlobalStyles.xaml`
  - `Fluxo/Resources/Styles/ButtonStyles.xaml` (`SelectorButtonStyle` used by `DateSelector`)
- `DateSelector` is a composite `UserControl` (`Fluxo/Views/Components/DateSelector.xaml`) with a `ToggleButton` surface.
- Validation should be driven strictly through WPF validation state (`Validation.HasError`).

## Proposed Design

## 1) Rounded TextBox (`RoundedTextInputStyle`)
- Keep the existing rounded template and hover behavior.
- Add a validation trigger in the template that sets `InputRoot.BorderBrush` to `Brush.Danger` when `Validation.HasError=True`.
- Keep focus trigger setting `InputRoot.BorderBrush` to `Brush.Mint`.
- Declare validation trigger after focus trigger so invalid state wins.

## 2) Rounded MoneyTextBox (`RoundedMoneyTextInputStyle`)
- Keep existing placeholder and zero-value triggers.
- Add validation trigger setting `InputRoot.BorderBrush` to `Brush.Danger` when `Validation.HasError=True`.
- Keep focus trigger setting `InputRoot.BorderBrush` to `Brush.Mint`.
- Declare validation trigger after focus trigger so invalid state wins.

## 3) Rounded ComboBox (`FluxoComboBoxStyle`)
- The visible border is drawn by the `ToggleButton` template, bound to the parent `ComboBox.BorderBrush`.
- Add control/template triggers to set `ComboBox.BorderBrush`:
  - focus/keyboard focus within: `Brush.Mint`
  - validation error (`Validation.HasError=True`): `Brush.Danger`
- Ensure invalid trigger is last to enforce precedence over focus.

## 4) DateSelector (`SelectorButtonStyle` + DateSelector root)
- `DateSelector` visual surface is the `SelectorButton` (`ToggleButton`) styled by `SelectorButtonStyle`.
- Add focus trigger on selector surface to set border to `Brush.Mint`.
- Add validation-state trigger that reads `Validation.HasError` from the parent `DateSelector` and applies `Brush.Danger` to selector border.
- Ensure invalid trigger is declared after focus trigger so invalid wins.

## State Rules
- Default: existing subtle border (`Brush.Border.Subtle`).
- Focused/active keyboard focus: `Brush.Mint`.
- Invalid/erroneous: `Brush.Danger`.
- Combined invalid + focused: `Brush.Danger` (confirmed requirement).

## Data Flow and Binding Behavior
- Error state source: WPF validation system (`Validation.HasError`) from bindings/validation rules.
- Focus source:
  - `TextBox` and `MoneyTextBox`: keyboard focus on control.
  - `ComboBox`: keyboard focus within control/template.
  - `DateSelector`: keyboard focus on selector surface.
- For `DateSelector`, validation-state lookup must target the `DateSelector` parent control, not only the inner toggle button.

## Error Handling and UX Notes
- No changes to validation message presentation are introduced here; this is border-color feedback only.
- Existing hover and disabled visuals remain intact.
- Existing corner radius and dimensions remain unchanged.
- If a control has both focus and validation error, red border is always shown.

## Testing Strategy
- Manual UI checks for each control type:
  - default state border
  - focused/tab-focused border (`Brush.Mint`)
  - invalid border (`Brush.Danger`)
  - invalid + focused combined state stays `Brush.Danger`
- Validate with keyboard tab navigation and mouse focus.
- Validate with bound properties that produce `Validation.HasError=True`.
- Regression check that hover/disabled/placeholder/calendar popup visuals are unaffected.

## Non-Goals
- No refactor of style architecture.
- No change to validation rule definitions.
- No change to popup/calendar internals beyond selector border behavior.
