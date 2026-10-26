# TreeDistrict UX and UI technology decision

Reviewed 2026-10-26. Design proposal; no UI is implemented or tested in the game yet.

## Decision

Use the official CS2 UI template and the game's native `cs2/ui` components with
React/TypeScript. Use native buttons, panels, dialogs, tooltips and focus behaviour
where exposed. Match the game rather than adding a separate visual language.

Use base-game icons where available. **Unified Icon Library (UIL)** is our chosen
supplement if a needed tool/action icon is missing; it must be declared as a mod
dependency if used. It provides icons, not widgets or settings persistence.
Do not install a general-purpose UI framework for the initial implementation.

The exact components, registration points and theme interfaces must be checked
against the installed toolchain on Windows. Use the versions it supplies; don't
independently upgrade React or assume browser CSS behaves identically in-game.

## Options reviewed

| Option | Evidence | Decision |
|---|---|---|
| Native `cs2/ui` and official UI template | Mirrored stock types expose panels, buttons, dialogs and input/focus support [1] | Primary UI layer; verify against installed types |
| Native mod menu registration | Stock `cs2/modding` declarations include `UniversalModMenu` [2] | Supported entry-point candidate; verify its actual button contract in-game |
| Unified Icon Library | Author supplies icons designed to match the game; Tree Controller declares it [3,4] | Supplemental icons only |
| Gooee | Inspected repository describes an early-alpha framework requiring BepInEx and HookUI [5] | Do not adopt that documented stack for our official-toolchain mod; this is not a claim about every fork |
| UrbanDevKit | Author offers bundled .NET/TypeScript helpers, including UI type packaging [6] | No current requirement justifies adding it; not a replacement design system |
| UnifiedUI/UUI from CS1 | TM:PE's documentation explicitly concerns the first game's toolbar [7] | Do not confuse it with CS2's native mod menu or UIL |

The official wiki page was inaccessible during this review. Public stock-template
mirrors and authors' repositories were inspected; they do not prove compatibility
with the user's exact game build. No blanket claim is made that every mod uses
one UI standard. Other libraries can be reconsidered for a concrete missing need.

## Entry and everyday district panel

Proposed entry: **Vegetation Districts** alongside landscaping/vegetation tools if
that integration is practical. Otherwise use the native mod menu and a rebindable
shortcut. Avoid an always-floating custom launcher. This is a proposed placement,
not a confirmed existing extension point.

Opening it shows a compact tool panel with the map still usable:

- Set picker with name and small vegetation preview.
- Overall density slider plus editable percentage; tooltip: 100% uses set defaults.
- Draw / Select / Edit boundary modes.
- Preview count, Apply and Cancel while an operation is pending.
- A secondary Manage sets action; advanced settings stay collapsed.

Selecting an existing district shows its name, set snapshot, density and generated
count. Provide Regenerate and New variation as distinct actions. Editing settings
marks the district as having unapplied changes; it does not silently replace trees.

Delete opens a native-style choice with explicit labels:

- **Delete district and vegetation**
- **Bake vegetation and delete district**
- **Cancel**

Include the affected plant count and make Cancel the safe dismissal path. Bake
preserves ordinary plants and removes procedural ownership. Both outcomes support
undo. Do not hide the difference behind an unexplained icon or persistent checkbox.

## Set editor

Use one library window, opened only when needed. List searchable saved sets on the
left and the selected set on the right. Provide New, Duplicate, Save and Delete.
Separate Save changes from Use in district so selecting a set doesn't overwrite it.

The set contains a searchable asset picker with thumbnails and rows for selected
vegetation. Each row exposes enabled state, age selection and density percentage.
An Edit profile action opens the profile editor. Overall density is near the top;
clumping and hard limits are advanced options. Relative age weights belong to the
selected species, not a second forest-wide percentage budget.

The district copies effective set settings. Changes to the library never silently
change an existing district; offer an explicit Update from saved set action with
preview when relevant.

## Profile editor: one vegetation asset, one age

Show asset thumbnail/name, age and a Profile selector. Example title:
**London Plane / Young**. Profiles have a visible origin: Built-in, My default,
Named custom or Override. Start with reasonable values; editing is optional.

Use collapsible groups for:

1. Placement: default density and physical clearance.
2. Environment: temperature/slope curves and optional hard limits.
3. Canopy: approximate dimensions, opacity and shade response.

Use small curve previews and ordinary numeric fields before attempting a custom
curve-drawing widget. Always display units. Distinguish canopy opacity/coverage
from density: a generic label such as Coverage 50% would be ambiguous.

Saving choices:

| Action | Scope |
|---|---|
| Use for this set/district | Override in the current editing context only; saved with that context |
| Save as new profile | Named reusable profile for this asset/age |
| Set as my default | Make the named profile the default for future selections of this asset/age |
| Reset to built-in | Restore the shipped baseline in the editor; apply explicitly |

Do not mutate shipped defaults. Save custom profiles in the user library, with
stable IDs/versions. Existing set/district snapshots stay unchanged when a personal
default changes. The editor must identify whether the current context is a set
library entry or a live district; avoid an ambiguous Save button spanning both.

## Climate and application settings

A compact City climate section in the tool/library offers **Use map climate** or
**Manual**, showing reference temperature, reference elevation and cooling rate.
Auto mode uses a stable climate statistic, not current weather. When no compatible
value is available, explain that manual input is needed. Never silently use 0°C.
Record the effective source/value with the city; applying a climate change marks
existing districts for explicit regeneration rather than triggering it.

Use the game's ordinary mod Options integration for application-wide preferences,
such as hotkeys and default panel behaviour. City climate belongs to the city;
profiles and sets belong to the reusable user library. Keep those scopes distinct.

## Interaction and validation requirements

- Clicking or scrolling over panels must not draw on or zoom the map underneath.
- Escape cancels the current interaction or closes the top dialog; it never applies.
- Pending edits get Apply/Discard/Cancel when closing would lose them.
- Sliders also allow numeric entry and show units; validation errors appear inline.
- Disabled actions explain why. Generation progress offers cancellation.
- Preserve focus, selected-state cues, game UI scale and localized labels.
- Test small supported resolutions, large UI scale, long translated names and
  missing thumbnails. Do not rely solely on colour for profile origin or validity.
- Test coexistence with Tree Controller and other tool panels; activating a tool
  should not leave two competing world-input handlers active.
- A browser mock is useful for layout/state checks, but only an in-game test can
  establish bindings, input capture, focus behaviour and rendering compatibility.

## Sources

1. [Stock UI declarations](https://github.com/CitiesSkylinesModding/StockModTemplatesDiffer/blob/main/ui/types/ui.d.ts), community mirror of game template types.
2. [Stock mod registration declarations](https://github.com/CitiesSkylinesModding/StockModTemplatesDiffer/blob/main/ui/types/modding.d.ts).
3. [Unified Icon Library author repository](https://github.com/algernon-A/UnifiedIconLibrary).
4. [Tree Controller author repository](https://github.com/yenyang/Tree_Controller), Dependencies and usage sections.
5. [Gooee author repository](https://github.com/Cities2Modding/Gooee), status and installation requirements.
6. [UrbanDevKit author repository](https://github.com/CitiesSkylinesModding/UrbanDevKit), features and SDK packaging.
7. [TM:PE Unified UI documentation](https://doc.tmpe.me/unified-ui.html), explicitly Cities: Skylines 1.

These sources support the technology comparison. The screen organisation and
saving workflow above are our design recommendations, not copied mod behaviour.
