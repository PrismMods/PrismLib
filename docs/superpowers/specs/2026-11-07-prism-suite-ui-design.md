# Prism suite UI and the new Quartz menu: design

Status: approved by the user (2026-11-07). It builds on the Facet engine spec
(`2026-11-06-facet-ui-engine-design.md`) and on plan 6, the Quartz Effect Remover preview, which sits on
Quartz branch `facet-preview`. Mockups of the chosen designs are in `2026-11-07-prism-suite-ui/` next to
this file. They are HTML fragments; open them in a browser. Each file shows the options as offered,
and option A is the one chosen in all three. In `home.html`, ignore the "Your screen" preview: the user
dropped it.

## What it is

One look and one way of working for every Prism mod (Quartz, Topaz, Sapphire, CamDisplay), and a new Quartz
menu built with it. The new menu ships beside the old one, hidden behind an experimental switch, until every
page has moved.

## Why

The user, after a screenshot tour of all 40 Quartz menu pages (2026-11-07):

- Quartz v3's menu "has been greatly improved but it is very intimidating for a new user. There needs to be
  clear separation of settings sections."
- "The basic color scheme is too bright and distracting, and the roundness and the slowness of the
  animations make it feel sloppy. I want a clean, sharp UI that allows people to find stuff."
- "A balance of Topaz and Quartz."
- "One look across the suite is the ultimate goal."
- Facet should become "a powerful tool for creating such custom UI".
- A long-requested animation-duration slider.

What the tour found (screenshots and per-page inventory are in the session scratchpad, summarised here):

1. The menu hotkey lands on the layout editor. It shows every on-screen element outlined and labelled, with a
   Settings button in the middle, and a calibration prompt was drawn over those buttons.
2. The sidebar is 12 icons with no labels, and each icon opens a second row of tabs (Overlay has 8).
3. Every row is the same full-width bar: toggles, dropdowns, sliders and text fields look alike. Section
   headings are only larger text. The accent fills whole slider bars and buttons.
4. Long pages have no basic/advanced split: Combo is 38 rows across 3 screens, Planet Colors 47, Effect
   Remover 41, Key Limiter 30. Almost no setting has an explanation.
5. Settings files unrelated things under "Overlayer". About a quarter of the window's width sits empty while
   pages still scroll.

## Goals

- **People find things.** A new player opening Quartz for the first time can find any setting from the
  sidebar in two clicks, or by search.
- **Clean and sharp.** Neutral dark surfaces, small corners, thin lines, and the accent only where something
  is on or active. Motion is short and plain.
- **One suite.** Topaz, Sapphire, CamDisplay and Quartz share the same neutrals, shapes, components, spacing
  and motion. Each mod keeps its own accent.
- **See the effect.** Every feature with something on screen gets a live preview pane beside its settings.
- **Use the width.** At the window's default size the page fills the width; the preview pane takes the space
  the old layout left empty.
- **Facet as the toolkit.** Every building block the new menu needs is a reusable Facet part that any mod
  can restyle. No look is baked into Facet.
- **One animation-duration setting for the whole suite.**
- **Safe rollout.** The old menu stays the default and keeps working while the new one grows behind a
  switch.

## Non-goals

- Changing the old Quartz menu: not its look, and not its animation timings. It stays as it is until it is
  removed.
- New features, or changes to what any setting does.
- Addon-registered pages in the new menu. Addons build uGUI pages through Quartz's `GenerateUI`; the new menu
  links to them in the old menu for now.
- Rebuilding the pages that aren't settings forms (decision 11). That is a later plan.
- Rewriting the Key Viewer's canvas editor in this program. Its page links out to the old editor until a
  later plan.
- IL2CPP builds and Unity 2022 game builds. Prism's UI Toolkit windows stay Unity 6 only.

## Anti-goals (a failure even if it technically works)

- Rounded, slow or bright: anything that brings back the "sloppy" feel.
- A wall of identical rows, or a page where the layout editor is the first thing a new player meets.
- Facet turning into "the Quartz look": a style baked into the engine instead of supplied by PrismLib.UI.
- The new menu breaking the old one, or a player losing a setting by switching between them.

## Decided (by the user)

1. **Look A, Topaz-leaning.** Neutral dark greys, 2px corners, rows on a barely visible fill, and the accent
   only on state: selected nav item and tab, switches that are on, slider fills, links. Mockup:
   `look-a.html`.
2. **Features page as compact tiles**, five across. Each tile has a name and an on/off switch, grouped under
   category headers, with a filter box and All/On/Off chips. Clicking a tile opens its settings; the switch
   only turns it on or off. Mockup: `features-grid-a.html`.
3. **Settings first.** The menu hotkey opens the settings window: on Home the first time, and after that on
   the page you left. The layout editor is a mode you choose, from an "Edit layout" button on Home and an
   "Edit layout →" link on every feature page that has an on-screen element.
4. **Home has no screen miniature.** It holds quick switches (compact tiles), a few status lines and the Edit
   layout button. Mockup: `home.html`, option A, without its screen preview.
5. **Judgement choices as a grid of tiles in each judgement's colour**, not a list. The same tile grid serves
   other many-option choices.
6. **A preview pane for every feature that has something on screen**, like the Key Viewer page has today.
7. **Rollout: ship beside the old menu, hidden away.** The old menu stays the default.
8. **Animation duration is a PrismLib setting** that every mod inherits. It affects only Facet-built windows,
   never Quartz's old menu.
9. **Quartz joins PrismLib fully**, like Topaz and Sapphire. It ships PrismLib, and appears in Prism ·
   Settings and in the Alt+D debug window.
10. **Order:** Facet grows the parts, then PrismLib.UI gets the suite look, then Quartz joins PrismLib, then
    the new Quartz menu. One design document, split into milestone plans.
11. **Pages that aren't settings forms are rebuilt later, not in this program.** That covers the TUF Levels
    and Packs browsers, Panels' drag-to-reorder list, and the Profiles, Import, Addons-manager and
    Calibration flows. Until then they open in the classic menu. A later plan rebuilds them with new Facet
    parts: an image card grid and a reorder list. The classic menu can be removed only after that plan.
12. **"Edit layout" opens PrismLib's shared layout editor**, the one CamDisplay uses. Quartz registers its
    on-screen elements through `ModHandle.DescribeLayout`, so Quartz's and CamDisplay's elements move in one
    session.
13. **Quartz work continues on branch `facet-preview`.** The user takes it to the Quartz team as one pull
    request when ready.

## The suite look

**Tokens.** The neutral base is Topaz's palette (`Topaz/UI/Theme.cs`), the "Topaz" half of the balance:

| Token | Value |
|---|---|
| Window | `#1A1A1F` |
| Title bar | `#141418` |
| Sidebar | `#111115` |
| Row fill | white at 2.5% |
| Row hover | white at 5% |
| Line | white at 7–10% |
| Text | `#EBEBF0` |
| Muted text | `#94949E` |
| Switch off | white at 18% |
| Danger | `#E2676D` |

The accent comes from each mod: Quartz's configured accent, Topaz periwinkle, Sapphire blue. These are a
starting point; the builder tunes them on screen.

**Shape.** Corner radius 2px everywhere (switch knobs included). Lines are 1px. No shadows except under
popups.

**Type.** One sans face, two weights. Page title 17px, row label 13px, help text 11–12px muted, card headers
11px uppercase with letter spacing. Sizes are at UI scale 1.0.

**Motion.** Fades and slides only, no bounce or overshoot.
- **The setting is in milliseconds:** the length of a standard transition such as a tab switch or a fold.
  The default is 120 ms, and the range runs from 0 ms (instant) to 400 ms (the user's decision: ms, not a
  multiplier).
- **Every other animation keeps its ratio to the standard one,** so hover feedback runs at half the setting
  (60 ms at the default).
- Internally, Facet's shared speed is the setting divided by 120.

**Components.**

| Component | What it is |
|---|---|
| Sidebar | Labelled entries with an accent bar on the active one. Two groups split by a thin rule: features (Overlay, Gameplay, Visuals, Tweaks, Editor, TUF) and app (Profiles, Import, Settings). |
| Tab row | Text tabs with an accent underline on the active one. |
| Page header | Title, one-line description, and the feature's on/off switch on the right. It replaces today's first row, "Use this feature". |
| Card | A titled group of rows. The header is uppercase and muted, with optional actions on the right ("All · None"). Rows are split by lines inside the card. |
| Row | Label, optional muted help line, and the control on the right. |
| Switch | A sharp-cornered switch, not a dot. |
| Slider | A thin track with a typed value field. The label sits outside the bar. |
| Dropdown | A compact box on the right, never "Label: Value" across the row. |
| Tile grid | Toggle tiles, optionally colour-coded with a coloured left edge and a tint when on. Used for judgements, the Features page and Home's quick switches. |
| Advanced fold | "▸ Advanced (n)" at the bottom of a card. It holds rows the writer marks advanced. |
| Preview pane | The right column on a feature page. It shows the feature's on-screen element live, with an "Edit layout →" link. |

**Who wears it.** Prism · Settings, the debug window, the layout editor chrome and Toast, then the new
Quartz menu. Topaz's and Sapphire's own uGUI menus are out of scope for this program, like Quartz's old menu.
Moving them onto the suite look is the next task after it.

## The new Quartz menu

**Window.**
- Title bar: logo, QUARTZ, search box and close.
- Body: sidebar, tab row, page.
- The window opens at a size that fits about 80% of the screen. It keeps Facet's drag, resize and remembered
  geometry.

**Home.**
- Quick switches: compact tiles for chosen features, with an "Edit…" action to pick which.
- Status lines: Key Limiter profile, TUF levels installed, Optimizer options, and others a feature offers.
- The Edit layout button.
- Version and profile in the header line.
- Reset and Clean move to Settings ▸ Maintenance.

**Features page.**
- All features as compact tiles, five across, under category headers.
- A filter box and All/On/Off chips.
- Today's per-category switch, which hides a whole category from the sidebar, moves into a small menu on the
  category header.

**Feature pages.**
- A page header with the feature's switch.
- Cards in a left column.
- A preview pane in the right column, for features with an on-screen element. Features without one (Key
  Limiter, Optimizer, Chatter Blocker and others) let the settings column take the full width, or show live
  status in the pane where that helps, such as Key Limiter's active keys.
- Writers mark rows as basic or advanced. A page should show its common settings without scrolling.

**Settings page.** It is regrouped:
- **Menu:** hotkey, show at startup, block game input.
- **Appearance:** accent, UI scale, window opacity, wide colour picker.
- **Behaviour:** tooltips, middle-click resets, scroll speed.
- **Language.**
- **Maintenance:** reset, clean, reload.
- **Experimental:** the new-menu switch.
- **Credits.**

The suite's animation setting lives in Prism · Settings, not here. Quartz's Settings links to it.

**Pages not moved yet.**
- In the new menu, a page that has not moved shows a one-line card: "This page is still in the classic
  menu", with an "Open it" button.
- Open it hides the new window and opens the old menu on that page. Closing the old menu brings the new
  window back (plan 6's hand-off).
- Addon pages, the Key Viewer editor and the pages of decision 11 use the same card.

**Search.** It is Facet's search, over every moved page's rows, with Korean initials matching. Results for
pages not yet moved jump to the old menu.

**Rollout.**
- A switch, "Experimental: new menu", lives in Quartz's old Settings page. It is off by default. When on, the
  menu hotkey opens the new menu, and the old menu stays reachable from it.
- Plan 6's visible "Open in the new menu (preview)" button on the Effect Remover page goes behind that switch.
- **Order of pages:** the shell first (sidebar, tabs, search, Home, Features, Settings), then Overlay pages,
  where previews matter most, then Visuals, Gameplay, Tweaks, Editor and TUF.
- When every page has moved, a later release makes the new menu the default and then removes the old one.
  That release is the user's call.

## What Facet grows

Facet stays look-neutral: it provides parts, roles and hooks, and PrismLib.UI supplies the suite look.

1. **Parts:**
   - card
   - page header with switch
   - Advanced fold
   - slider with typed value
   - compact dropdown
   - tile grid (toggle tiles with an optional per-tile colour)
   - two-column page with a sticky right pane
   - labelled sidebar
   - tab row

   Each part takes `Bind`/`Options` like today's controls, joins the Refresher and search index, and is
   styled only through roles and looks.
2. **Skinning hooks.** A consumer can supply looks and sizes for every part, including radius, spacing and
   type scale, without forking the part. The suite look is one such skin, defined in PrismLib.UI.
3. **A preview host.** A pane that shows a consumer-provided live view of an on-screen element. Facet owns
   the pane, its sizing and its lifetime; the consumer owns what is drawn in it (see "Left to the builder").
4. **The animation speed is shared across Facet versions.** `Anim.Speed` is a plain static today, one per
   Facet copy. It moves into the cross-version shared state, so a single PrismLib setting governs every
   loaded Facet minor.
5. **Wheel ticks count once per frame across mods**, as Escape already does. Today every Prism mod ticks the
   wheel, and a window scrolls one step per mod.
6. **Fonts for Chinese and Japanese.** Facet's CJK fallback covers zh and ja the way it covers ko, using
   PingFang or Hiragino on macOS, Microsoft YaHei or Yu Gothic on Windows, and Noto CJK on Linux. Without it,
   Quartz's Chinese players would get English in the new menu, a step back from the old one.

**Version.** The new parts and the shared animation speed go into the next Facet minor, `Facet.0.2.dll`,
under Facet's rule R21: any new public member means a new minor. A `Facet.0.1.dll` already on players'
machines keeps working beside it, and its windows run at the default animation speed. Every consumer's
`lib/`, Quartz's `FacetBridge` probe, LayoutProbe's single-copy check and the bootstrap's copy-syncing move to
0.2 together. (Writer's call; release timing stays the user's.)

## PrismLib's part

- **The suite skin** for every Facet part. Prism · Settings, the debug window, the layout editor chrome and
  Toast move onto it.
- **A setting "Animation duration"** in Prism · Settings: `anim.duration`, in milliseconds, 0–400, default
  120. It writes Facet's shared animation speed (the setting divided by 120) on load and on change.
- **Quartz as a Prism mod.** PrismLib's bootstrap, shared-folder rules and bridge landmines (`CLAUDE.md`)
  apply to Quartz as they do to Topaz.

## Quartz joining PrismLib

- Quartz gets a bridge (`PrismBridge`) behind NoInlining guards, like Topaz's. No PrismLib type appears in a
  field or outside the bridge.
- Quartz registers its log and fields in the Alt+D debug window, and its settings in Prism · Settings: at
  least the menu hotkey and the experimental switch.
- **Shipping.**
  - Quartz ships `PrismLib.bundle`, `PrismLib.UI.dll` and `Facet.0.1.dll`.
  - Quartz loads from a versioned runtime store (`UserData/Quartz/Runtime/versions/<ver>/`), not a mod
    folder. PrismLib.UI's "first loaded copy wins" and the bootstrap's copy-syncing cover mod folders and
    `UserLibs/` only.
  - The builder extends the bootstrap or Quartz's loader so the runtime store takes part. Exactly one,
    newest, PrismLib.UI copy is loaded, verified on both loaders.
- **Layout.** Each Quartz on-screen element becomes a `LayoutTarget`: Key Viewer, Progress Bar, Combo,
  Judgement, Song Title, each Panel, Calibration, attempts and the others the reorganizer moves today.
  - A target reads and writes the same settings fields the old reorganizer does, so either editor's change
    shows in the other.
  - "Edit layout" in the new menu opens the shared editor, and the old menu keeps its own reorganizer.
  - Box conventions follow PrismLib's: screen pixels, origin bottom-left, degrees counter-clockwise, anchors
    0–8.
  - Some Quartz elements have settings the shared editor lacks (per-element font, Panels' grouping). Those
    stay on the element's settings page, not in the editor.
- **Keys.** Quartz's menu hotkey (Alt+K by default) does not collide with Prism's chords (Alt+D, Alt+P,
  Alt+T, Alt+C). On a Mac it is read through `MacKeys`.
- Quartz is GPL-3.0 and PrismLib is MIT. Quartz using PrismLib is fine; no Quartz code goes into PrismLib
  or Facet without its author's say.

## Constraints

- Unity 6 runtime UI Toolkit, inline C# styles, no USS or UXML (the Facet spec).
- No regression in the old Quartz menu. Switching menus never loses a setting, because both edit the same
  settings objects.
- **Performance:**
  - the first open of the new menu ≤ 50 ms after Facet's warm-up, on a Quartz-only install (plan 6 measured
    70.6 ms there, in one noisy run);
  - no per-frame cost while closed;
  - a preview pane costs nothing while its page is hidden.
- **i18n.**
  - Every new user-facing string goes through Quartz's or PrismLib's tables. en-US and ko-KR are authored
    with each change; other languages come from translators.
  - Labels in the new menu are Lang keys, so a language change relabels live (plan 6, ruling P16).
- Commits follow each repo's rules: Conventional, split finely, and no AI co-author lines.

## Testing

- Facet parts: in the Gallery and its in-game autotest, plus offline tests for any BCL-only logic.
- The suite skin: PrismLib's LayoutProbe autotest and the `[perf]` lines.
- Quartz:
  - extend plan 6's DEBUG self-test: hand-off between menus, the experimental switch, search reaching pages
    not yet moved, and preview panes updating within 150 ms of a change;
  - run once on a Quartz-only install (no other Prism mods) and once beside Topaz and Sapphire, since
    plan 6 showed the two differ;
  - for every Quartz layout target, check that a move in the shared editor lands in the setting the old
    reorganizer reads, and the other way round.
- The screenshot tour from 2026-11-07 is the before picture. Rerun it on the new menu for the user to
  compare.

## Left to the builder

- How a preview pane draws a live element. Quartz's overlay elements are uGUI and TextMeshPro on the game's
  canvases; a UI Toolkit pane cannot host them directly. Candidates:
  - a camera rendering the real element to a texture shown in the pane;
  - a second instance of the element drawn into a texture;
  - a Facet-drawn stand-in that mirrors font, colours and layout.

  The pane must look like the real element (font, colours, size relationships) and update live.
- The judgement colours: take them from the game's own palette, not from the mockups.
- Exact token values and durations after seeing them in game. Keep within the look: 2px, neutral, accent
  only on state, no bounce.
- Which rows of each page are advanced. The page writer decides, with "common settings visible without
  scrolling" as the bar.
- How the runtime store joins PrismLib.UI's copy rules.
- File layout inside Facet and PrismLib.UI for the new parts and the skin.

## Calls the writer made (check these)

1. The suite's neutral base is Topaz's existing palette; each mod keeps its own accent.
2. Default motion: the standard transition is 120 ms, settable from 0 to 400 ms. Other animations keep
   their ratio to it.
3. Credits moves under Settings, and Reset/Clean move from Home to Settings ▸ Maintenance.
4. The sidebar groups features above app pages, and Credits leaves the sidebar.
5. Pages not moved yet show an "Open it in the classic menu" card. Addon pages and the Key Viewer editor do
   the same for this whole program.
6. The experimental switch lives in the old menu's Settings page and also appears in Prism · Settings.
7. Facet's CJK font fallback grows to cover zh and ja, so the new menu does not regress Chinese players.
8. Topaz's and Sapphire's own uGUI menus stay as they are in this program; only Facet-built windows wear the
   suite look. **Moving them onto the suite look is the next task after this program** (user, 2026-11-07);
   see `../plans/roadmap.md`.
9. Page order: shell, Overlay, Visuals, Gameplay, Tweaks, Editor, TUF.
10. The new Facet parts ship as Facet 0.2 under R21. Facet 0.1 windows keep the default animation speed.
11. Quartz's layout targets live in the "Quartz joins PrismLib" milestone. Elements the shared editor can't
    express yet (none known) keep using the old reorganizer.
