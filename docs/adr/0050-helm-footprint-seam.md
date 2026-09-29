# ADR 0050 — The helm's footprint: one Core seam for what the helm covers at the bottom of the screen

- **Status: PROPOSED** (2026-09-28) — written by the helm dash band's H1 lane
  (`feat/helm-footprint-seam`, `lead-architect`). The charter made the seam's shape, and whether it
  is an ADR or a dated note on ADR 0025 S4.5, `lead-architect`'s call; by this index's conventions it
  is Accepted by `lead-architect` on merge. What the seam carries is the owner's: the band, and the
  camera moving for half of it, ruled 2026-09-28 at 23:47:49Z (*"yes for both recommendations"*).
- **Date:** 2026-09-28
- **Decision owner:** `lead-architect` (the Core contract). The readers are built in the same PR,
  across `UI/`, `Player/`, `World/` and `App/`; H2 (`ui-ux`) builds the band that publishes into it.
- **Serves:**
  - **P1 The Sea Has Moods:** the sea you read stays on screen. Nothing is drawn over the helm, and
    under a band the camera gives half of it back, so the sea ahead of the bow is what the ruled
    strip left.
  - **P5 Cozy but with Teeth:** the teeth are the sea's, not the UI's; a warning is never under a
    toast, and a toast is never under the helm.
- **Amends:** [`docs/architecture/tech-architecture.md`](../architecture/tech-architecture.md)
  gains §4.5. The #416 flag in
  [`docs/design/diegetic-instruments-and-consoles.md`](../design/diegetic-instruments-and-consoles.md)
  (its S4.5 note) is marked fixed.
- **Related:** [ADR 0025](0025-ui-rig-runtime-rendering.md) (the helm's cards are rigs rendered in
  C#) and its S4.5 stage in the canon doc above (FLUSH by default, one EXPANDED at a time, the HUD
  yields the helm); [ADR 0039](0039-quiet-hud.md) (the quiet HUD);
  [ADR 0005](0005-pc-first-target.md) (the mobile port stays viable: a band along the bottom edge is
  the shape a touch screen wants).

---

## 1. Why

#416 (S4.5, merged as `ea1f4baf`) measured four bottom-centre overlays in `Player/` and `World/`
drawing over the helm dash. The UI lane could not move them (rule 4: those modules reference Core,
never UI) and named the seam they would need: a Core statement of what the helm covers.

On 2026-09-28 the owner ruled the helm dash band: one fixed dash across the bottom of the screen,
144 px tall at the whole-number scale k (144·k), and the camera eased up by half of it while it
shows. The band is H2's. This ADR is H1's: the seam, and everything keeping clear of it, with
today's cards (the dash, the lever, the tiller) as its first publisher.

By this base three of #416's four draw nothing of their own. `ControlSwitcher`'s helm hint and
`WorldInteractor`'s prompt went into the interact popup (2026-08-19), and `OnboardingDirector`'s
banner into the quest note (2026-08-22). The dev toast still draws. The readers are therefore the
toast, the popup, the note, the HUD's nav cluster, and the camera.

## 2. The seam — `Core/Boats/HelmFootprint.cs`

- **`HelmFootprintArea`**, a readonly struct: a `Kind` (`None`, `Band`, `Card`) and a `Rect` in
  SCREEN pixels, bottom-left origin, y up.
  - `None` (the default): nothing is covered.
  - `FullWidthBand(heightPx)`: the bottom `heightPx` across the whole width. The column is not
    stored; a height of 0 or less is `None`.
  - `OfCard(rect)`: a card's rect. An empty rect is `None`.
- **`HelmFootprint`**, a Core static: `Current`; `Publish(in area)`, change-detected, where a change
  raises `HelmFootprintChanged { Current, Previous }` on the EventBus at once; `Clear()`; `Reset()`
  (silent, for tests); cleared at `SubsystemRegistration` for play without a domain reload.
- **Two answers every reader shares**, so keeping clear is one rule, not six:
  - `Overlaps(box)`: strict (a shared edge is clear); an empty box never overlaps; a band ignores
    the column.
  - `LiftToClear(home, gap)`: how far a box rises to keep clear. A band raises the floor: its
    height, plus whatever of the box already hangs below the screen's edge, so the box stands on
    the band's top and whatever was stacked above it stays stacked. A card lifts only a box that
    reaches it (with the reader's own gap above the card), and only to the card's top plus the gap.
- **Why a Core static with an event, not a `GameServices` member.** It is one value, with one writer
  and a handful of per-frame readers: a presentation fact like `InteractOffer`, with no lifetime, no
  interface to fake and nothing to register at boot. The event is there for a reader that would
  rather not poll; today's readers compare one struct a frame and re-place only on a change.
- **Why an ADR, not a note on ADR 0025 S4.5.** It is a new Core contract, read by four modules, and
  it moves the camera. The band publishes into it too, so it outlives the card S4.5 was about.
- **It saves nothing** (rule 5): no part of it is in `SaveData`, and a test walks the save for it.
  **It is headless-safe:** with no UI up nothing publishes, `Current` stays `None`, and every
  reader stands exactly where it did.

## 3. Who says it

Only `UI/HelmOverlayHost.cs`, today:

- **The card's WINDOW:** the card as the player's window left it (moved, sized, small or focused;
  the tiller's, the lever's or a dash), plus the title strip above it
  (`BoatUiWindowSettings.TitleBarPx`). The strip counts even while it is hidden: it appears on hover
  and while dragging, and it is all that is left of a BAR-collapsed card.
- **`None`** with no helm in her hand, while the window is hidden (hide-all or its own ×), and when
  the host is disabled or destroyed. A host that does not own the statement never clears it.
- **H2's band host** will say `FullWidthBand(144·k)` while the band shows.

## 4. Who keeps clear, and how

The card rule is **above, or beside then above**: the nav cluster steps beside a card, and above it
only if its clear column still meets it; everything else steps straight up, its column kept.

| Reader | Module | Under a full-width band | A card that reaches it |
|---|---|---|---|
| Dev toast (`DevToast`) | Player | the whole four-line stack rises by the band's height | the stack rises to the card's top |
| Interact popup (`InteractPopup`; the helm hint and the prompt) | UI | rises by the band's height | rises to the card's top plus its own `MarginY` |
| Quest note (`QuestPanelPresenter`; the onboarding step) | World | rises by the band's height | rises to the card's top plus its own margin, in its own pixels |
| Nav cluster (`HudController` via `HelmHudSuppression`) | UI | rises by the band's height | moves to its clear column (`ClearOfTheDash`), now for the tiller's card too; rises only if the column still meets it |

- **With nothing covered, every one stands where it was built, bit-exact.** The tests pin it at
  1280×720, 1280×800, 1920×1080, 2560×1440, 3440×1440 and 3840×2160.
- **Under a band the stacking holds:** the toast still stands above the lifted nav cluster at every
  listed screen.
- **The tiller's ×1 card** reaches the toast only at 1280×720 and 1280×800, and never reaches the
  popup or the note. The nav cluster steps beside it at every listed screen.
- **The nav cluster reads the seam only with the helm in her hand.** The host says nothing
  otherwise, so the HUD's 4 Hz tick never dodges a card that the host has not yet taken back in
  the frame she let go of it.

## 5. The camera — `App/CameraFollow.cs`

- **Where:** in the follow, `goal = target + look-ahead − shift` (on y), ahead of the feel, the
  snap and the clamp. The snap puts the shifted view on the pixel grid; the clamp still wins at a
  region's edge.
- **How much:** `CameraBandShare` × the band's height in screen pixels, only while the covered area
  spans the full width. It is 0 for a card and 0 for nothing covered, hide-all included (the ruling
  moves the camera for the band only).
- **How fast:** a linear clock over `CameraEaseSeconds`, shaped by a smoothstep, in and out. An ease
  of 0 is a cut.
- **In metres:** by the framing on screen that frame. With the Pixel Perfect Camera on, one screen
  pixel is `1/(zoom·ppu)` for the whole-number zoom it picks against its reference; with it off
  (the feel stands it down under way), `2·ortho/H`.
- **Tunables** (rule 6), `GameConfig.HelmFootprint`: `CameraBandShare` 0.5 (ruled) and
  `CameraEaseSeconds` 0.4 (the camera's own framing-tween time, so a band arriving with a framing
  change reads as one move).

**Re-derived for the Cape** (her authored 24 m framing, the 960×540 reference, 72·k px):

| Screen | At rest (Pixel Perfect) | Full ahead, calm (4.20 m/s; the ortho has the view) |
|---|---|---|
| 1280×720 | 72 px × 1/32 m = 2.25 m | 72 × 27.525 / 720 = 2.7525 m |
| 1280×800 | 2.25 m | 2.4773 m |
| 1920×1080 | 72 px × 1/64 m = **1.125 m** (the charter's "about 1.1 m") | 1.8350 m |
| 2560×1440 | 1.125 m | 1.3763 m |
| 3440×1440 | 1.125 m | 1.3763 m |
| 3840×2160 | 144 px × 1/128 m = 1.125 m | 1.8350 m |

27.525 m is the 24 m request times the feel's pull-back at full ahead (1 + 0.1469).

## 6. The arithmetic the ruling rests on

The shift stands her at H/2 + 72·k and the band's top is at 144·k, so the clear sea below her is
**H/2 − 72·k at every listed screen** (asserted).

The ruled strip stood on `MarginY` (y 16 to 88 at 720p), not on the screen's edge, so it left
H/2 − 88 at k = 1: the band and the shift leave 16 px more. Full ahead in a calm sea, heading south,
the Cape's warning time is 2.089 s at 720p and 2.356 s at 1080p under the strip (the audit's 2.09 s
and 2.36 s, re-derived), and 2.234 s and 2.453 s under the band.

## 7. The §9.8 framing under a band

Measured for every hull asset at the six screens, with the camera's own laws (the §9.8 floor, the
ladder, the Pixel Perfect zoom at rest, the feel's pull-back under way, the ruled lead and its cap).

- **At rest,** every hull's stern clears the band's top. The tightest is the skybridge sport fisher
  at 1280×720, 6.2 px clear.
- **With the look-ahead at its most northward,** two sterns drop under the band, both at 1280×720
  and both at 1 m/s. That is the last speed the Pixel Perfect framing holds, where the 0.6 m lead is
  drawn at the rung's tighter scale:
  - `boat.sport_fisher_skybridge`, 13.0 px under;
  - `boat.sloop_88`, 8.9 px under.

The framing rule is not changed without a ruling. The test pins the pair by name, so a change that
widens the set fails, and a change that fixes them is told to shorten the list.

## 8. Consequences

- **H2** publishes the band into the seam and touches none of the readers.
- **A new overlay at the bottom of the screen** reads `HelmFootprint.Current` through Core and keeps
  out with `LiftToClear`; it never references `UI/`.
- **Rule 4 holds:** no file in `Player/` or `World/` references `UI/`, and `App/` reads the seam
  through Core. The tests check the asmdefs and scan the sources.
- **Recorded, not fixed** (with nothing covered, nothing moves):
  - The popup's home is off the bottom of a 3440×1440 screen (y −192 to −90) and 52 px under the
    edge at 1280×800. Its host is pinned top-left at the 1280×720 reference. Under a band it lands
    on the band's top, on screen.
  - The apparent-wind label's box hangs below the screen's edge.
  - Above 1 m/s the feel stands the Pixel Perfect Camera down, so the view pops from the rung
    (16.875 m for the Cape at 1080p) to the raw request (24 m), then pulls back.

## 9. Tests

- EditMode: `HelmFootprintSeamTests` (10), `HelmFootprintOverlayTests` (13),
  `HelmBandCameraTests` (14), `HelmBandFramingTests` (2).
- PlayMode: `HelmFootprintPlayTests` (3), with real frames, the real host, overlays and follow:
  today's cards said and kept clear of with no camera move; hide-all and going ashore saying
  nothing; a band said through the seam lifting every overlay and easing the camera up and back.
