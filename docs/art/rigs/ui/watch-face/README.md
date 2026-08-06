# Watch Face — `WatchRig`

Hidden Harbours · diegetic clock rig · the one screen the world hands the player at the start.

A resin digital watch drawn as a **procedural 7-segment LCD** — every digit, the weekday, the season
tag and the date come from the game clock, so one rig covers every hour, day and season with no bakes.
Day is a positive grey-green panel; night lights the traditional green backlight. The four pusher
legends sit inboard of the bezel screws, each flagged with a chevron notch pointing at its button. This is where time &
date live in-world (the diegetic rule) — not on a floating HUD.

## Quick start

Open **`Watch Face.dc.html`** (double-click). Scrub hour/minute/day, pick a season, toggle 12/24h,
press **LIGHT** for the backlight, or hit **RUN** to let the clock tick.

```
watch-face/
├─ README.md
├─ Watch Face.dc.html     ← interactive preview + PNG export
├─ support.js             ← preview runtime (do not edit)
└─ Art/watchRig.js        ← the rig
```

## Rig API — `window.WatchRig`

```js
WatchRig.render({ h, m, s, use24, dow, date, season, year, market, night, light }) // → HTMLCanvasElement (W×H)
```

| Param | Type | Notes |
|---|---|---|
| `h`,`m`,`s` | int | hour 0–23, minute/second 0–59 |
| `use24` | bool | 24-hour vs 12-hour (AM/PM) |
| `dow` | string | weekday label — pass `WatchRig.WEEKDAYS[wd]` |
| `date` | int | day of season, 1–28 |
| `season` | string | season tag — pass `WatchRig.SEASON_ABBR[idx]` (`SPR`/`SUM`/`FAL`/`WIN`) |
| `year` | int | shown as `Y{n}` |
| `market` | bool | flags market day on the face |
| `night` | bool | green backlight vs day panel |
| `light` | bool | momentary backlight press (independent of `night`) |

Helpers: `WatchRig.paint(ctx, opts)` (draw into your own context), `isNight(hourFloat) → bool`
(`hour < 6 || hour >= 19`), `pad2(n)`, and the label tables `WEEKDAYS` (Mon-first), `SEASON_ABBR`,
`SEASON_FULL` = `['Spring','Summer','Fall','Winter']`. Button hit-boxes for the
preview live at `WatchRig.hit.light` and `WatchRig.hit.mode` (rig-local `{x,y,w,h}`, aligned to the
MODE / LIGHT legends on the bezel).

## Time canon (bind to `IGameClock`)

- **4 seasons (Spring / Summer / Fall / Winter) × 28 days = a 112-day year**; week runs **Mon–Sun**; market day is tunable (preview = Sat).
- **Night** = `hour < 6 || hour >= 19`.
- **SecondsPerDay = 1200** → one in-game day = 20 real minutes.
- Derive weekday & market day from the absolute day index: `di = (year-1)·112 + seasonIdx·28 + (day-1)`.
- New game starts **Spring · Day 1 · 06:00**.

## Export & integration

- **↓ THIS FACE · PNG** bakes the current face. The rig is otherwise live-rendered from the clock.
- Preview state persists to `localStorage['hh.watch']` — preview-only; in game, feed the clock straight in.
- Offline-safe; only the preview heading fonts need the network.
