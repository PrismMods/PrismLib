# Prism UI roadmap

The long-term checklist for Prism's UI work, in order. Specs are in `../specs/`, plans in this folder.
Update this file when a plan is written, finished, or reordered. Versions and releases are always the user's
call.

## Done

- [x] **Plan 4: the Facet engine.** Spec `2026-11-06-facet-ui-engine-design.md`. Merged to Facet `main`
  (local repo, no remote yet).
- [x] **Plan 5: PrismLib.UI stands on Facet.** On `main` in PrismLib, Topaz, Sapphire and CamDisplay.
  Unpushed except CamDisplay.
- [x] **Plan 6: Quartz's Effect Remover in a Facet window.** Quartz branch `facet-preview`, 38 commits,
  unmerged and unpushed.

## Now: the suite look and the new Quartz menu

Spec `2026-11-07-prism-suite-ui-design.md`, approved 2026-11-07.

- [ ] **Plan 7: Facet 0.2.** The new parts, skinning hooks, the animation speed shared across versions,
  wheel ticks counted once per frame, and zh/ja font fallback.
- [ ] **Plan 8: the PrismLib.UI suite skin,** worn by Prism · Settings, the debug window, the layout editor
  chrome and Toast. Also the `anim.duration` setting, in ms.
- [ ] **Plan 9: Quartz joins PrismLib.** Bridge, shipping, the runtime store joining the copy rules, and a
  layout target for every on-screen element. Can run alongside plans 7 and 8.
- [ ] **Plan 10: the new Quartz menu shell** behind "Experimental: new menu": sidebar, tabs, search, Home,
  the Features grid and Settings. Plan 6's preview button goes behind the switch.
- [ ] **Plan 11 and on: Quartz pages move to the new menu,** in this order: Overlay (with preview panes),
  Visuals, Gameplay, Tweaks, Editor, TUF settings.

## Next (user, 2026-11-07: "This will be the next task")

- [ ] **Topaz's and Sapphire's own uGUI menus move onto the suite look,** so every Prism mod looks the
  same.

## Later

- [ ] **Rebuild Quartz's pages that aren't settings forms** with new Facet parts (an image card grid and a
  reorder list): the TUF Levels and Packs browsers, Panels' reorder list, and the Profiles, Import,
  Addons-manager and Calibration flows.
- [ ] **The Key Viewer canvas editor** on Facet.
- [ ] **A way for addons to add pages to the new menu.**
- [ ] **New menu becomes the default, then the classic menu is removed.** This can only happen once every
  page above has moved, and it is the user's call.

## Open items carried over

- [ ] **Quartz-only first open is 70.6 ms** against a 50 ms target: one noisy sample, plan 6 residual P27.
  The idea is to pre-build the window hidden after warm-up.
- [ ] **PrismLib's next release ships Facet in its feed** before any mod release that needs it.
- [ ] **Facet's GitHub repository and the pushes:** the user's call.
