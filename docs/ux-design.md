# TreeDistrict UX and UI decision

Status: agreed workflow, 2026-10-26. Implementation details and recommendations
are labelled below. A local editor and native UI binding scaffold are now available;
see [implementation status and host contract](ui-scaffold.md). In-game integration
has not yet been tested. Overlapping districts are allowed in v1; handling them is
a user responsibility until v2.

## Agreed technology

Use the official CS2 React/TypeScript UI template, native `cs2/ui` components and
**Unified Icon Library**. Declare UIL as a dependency when integrating the UI.
Native controls supply panels, buttons, dialogs, tooltips and focus behaviour;
UIL supplies matching icons. No additional general-purpose UI framework is planned.
Verify exact APIs, theme support and registration points against the installed
Windows toolchain. Earlier research considered Gooee and UrbanDevKit; neither is
selected. The source list below preserves the basis for that comparison.

## Set first, then draw

Open Vegetation Districts, choose a set, and immediately draw. No mandatory library
window or settings wizard sits between selecting a set and starting a district.
Use a sensible last-used/default set on opening the tool.

Recommended picker: a searchable dropdown with thumbnail/icon **and name**, grouped
into Built-in and My sets. It scales better than an icon-only strip for similarly
named or numerous sets. Optional favourite tiles can come later. New set and
Duplicate are available from the same picker/menu.

Ship curated real-world-inspired sets. Candidate themes include temperate mixed
woodland, conifer woodland, Mediterranean scrub and urban mixed planting; these
are starting content ideas, not validated botanical species lists. Select actual
assets after surveying the installed game. Built-ins are recoverable/read-only;
editing one creates a working copy that can be saved as a user set. Missing assets
are visibly flagged, never silently substituted.

## One window, progressively expanded

Selecting an existing district populates **the same panel** with its current
settings. The district name and a clear New district/Selected district indicator
prevent edits being applied to the wrong target. Choosing a different set replaces
that district's working set; it does not affect other districts.

| Depth | Visible content |
|---|---|
| Top level | Set picker/name and `*`, overall density, drawing/selection modes, generation mode/status, district actions, Save set / Save as |
| Expand set settings | Climate inheritance/overrides, clumping, optional hard limits; vegetation asset list with density %, age choices, Add/Remove |
| Expand an asset | Age-specific footprint, height/canopy, slope and temperature responses, shade settings and hard limits |

Use an age selector within the asset's expanded section. Defaults populate every
asset/age; users can edit the values here. Use small curve previews and numeric
fields with units. Avoid unexplained Coverage percentages: distinguish density,
canopy extent and canopy opacity.

**Only whole vegetation sets are saved.** No separate asset-profile library,
Save profile or Set asset default actions in this version. Per-asset and per-age
edits travel inside the saved set. Reset an asset/age to its built-in values remains
useful, but is an edit to the current working set until Save set is used.

## Settings, library saving and generation are distinct

A valid parameter edit immediately updates the selected district's settings and
is retained when the city is saved. There is no Apply settings step. Invalid or
incomplete numeric input stays in the field with an error until valid.

The `*` means **the working set differs from its reusable library version**, not
that district data will be lost or that vegetation needs regeneration. Tooltip:
“Modified set — saved with this district; save to reuse elsewhere.” Reverting to
the saved values clears the star. A new unnamed set stays marked until saved.

- Save set updates an existing user library entry; Save as creates a new one.
- Editing a built-in offers Save as. Never overwrite shipped content.
- Saving includes nested asset/age edits and climate override flags/values.
- Other districts retain their snapshots when a library set is changed.
- Boundary, district name, seed, generation mode and ownership are district data,
  not vegetation-set data. They do not mark the set with `*`.
- Generated-state freshness is a separate label: Not generated / Up to date /
  Needs generation / Generating / Failed.

Changing the set on an edited district should offer Save as / Replace / Cancel
when replacement would discard a modified working set. Closing a selected district
panel needs no save prompt: its settings already belong to the district. Retain the
new-district working set while switching tools; only prompt if actually discarding it.

## City climate with set overrides

The city defines default reference temperature, reference elevation and cooling
rate. Temperature may use compatible stable game climate or manual input.

Each set field defaults to **Use city default**, displaying the resolved value
and source. It may be overridden independently. Reset returns the field to
inheritance. Applying a set to another city resolves inherited fields in that city;
explicit overrides remain unchanged. Do not accidentally save resolved city values
as overrides when saving a set.

Recommended semantics: inherited fields remain linked to city defaults. Editing a
city default changes effective inputs only for inheriting districts; auto districts
regenerate and on-demand districts become Needs generation. A new effective value
must not mark a set `*` when its inheritance flags have not changed. Show reference
temperature and reference elevation together, since they describe a paired baseline.
Never update from momentary weather. Exact game climate extraction needs verification.

## Generation mode

Default for each new district: **On demand**. Settings and boundary edits immediately
change district data, but vegetation changes only on Generate/Regenerate. Existing
plants remain until then; show Needs generation. Removal actions still work immediately.

**Auto-generate** updates the district's owned vegetation after each committed node
or settings change when a valid temporary polygon is available. Otherwise generate
when drawing finishes. Do not generate for every mouse movement. A generation may
re-evaluate every owned plant without deleting/recreating unchanged entities.

Recommended safeguards:

- Briefly debounce rapid edits/slider drags; only the newest revision may commit.
- At most one generation job per district. Cancel/coalesce outdated work.
- Wait for at least three usable nodes and a valid non-self-intersecting polygon.
- Keep the last valid vegetation when the intermediate boundary is invalid; show why.
- Cancel drawing removes draft-generated plants and the draft district. Cancelling
  an existing boundary edit restores its prior state, including owned plants.
- Switching auto on generates if needed; switching off cancels pending auto work
  without erasing the last committed layout.
- Auto is event-driven, not continuous regrowth after individual trees are bulldozed.
  A later settings/boundary regeneration can recreate them; users can use on-demand
  mode or bake when manual detailing should be preserved.

## District actions

| Action | Result |
|---|---|
| Generate / Regenerate | Populate or recompute owned vegetation from current settings; preserve unrelated plants |
| Ungenerate | Remove owned plants, retain district, boundary and settings |
| Bake + delete | Keep plants as ordinary vegetation; remove ownership and district |
| Delete | Remove owned vegetation and district |

Recommendation: Ungenerate also switches the district to On demand, with that
consequence stated in its tooltip, so auto mode cannot immediately fill it again.
Bake works on the currently placed plants, including a stale on-demand layout;
it does not secretly regenerate first. Show that fact when settings have changed.
Stop pending jobs before Ungenerate/Bake/Delete so late results cannot resurrect
removed plants. These operations need undo. Delete/Bake dialogs name the action
and affected count; avoid an ambiguous persistent “keep trees” checkbox.

## Input, performance and recovery

- UI clicks/scrolling never leak into drawing or map camera controls.
- Escape cancels drawing or closes the top dialog; it does not generate or delete.
- Native focus/scale/localisation conventions; readable labels alongside icons.
- Visible counts, progress/cancel and bounded candidate/instance limits. No VRAM
  promise based only on density percentage.
- Save/load retains working settings, set comparison baseline, generated entities,
  freshness, mode and ownership. Reload does not regenerate a completed district.
- A generation interrupted by save/load remains explicitly incomplete/needs
  generation unless a safe resume mechanism is implemented; never duplicate entities.
- Recover from missing assets and generation failure without losing the last valid
  district configuration. Failures explain what needs fixing.
- Test tree tools, small resolutions, high UI scale, long names and repeated rapid
  edits in the real game; browser mocks cannot establish integration compatibility.

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
