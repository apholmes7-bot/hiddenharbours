HIDDEN HARBOURS — SKIFF FLEET KIT
The two 7-metre centre-console skiffs + the shared remote-steer outboard.
Game-ready sprites. PNG, transparent background, no anti-aliasing, upper-left
key light. Scale: 32 px = 1 metre. Fixed 3/4 iso camera, elev 40deg, 45deg steps.

Two hulls off one keel:
  CONSOLE SKIFF — the workboat. Wood sole, painted liner, gabled teal canopy.
  SPORT SKIFF   — the glass sister. Gelcoat white, twin teal stripes, stainless
                  rails + pulpit, domed bimini, raked sport bow.
Both share the same ~7.0 m envelope, transom, pivot and mount anchors, so the
outboard and operator layers drop onto either one unchanged.

====================================================================
COORDINATES  (read this first — everything pins by PIVOT, not corners)
====================================================================
  Hull cell   244 x 216   pivot (122,120) = boat origin (amidships,
                          keel bottom, centreline).
  Motor cell  272 x 216   pivot (136,120) = the SAME world origin.
The motor cell is wider than the hull on purpose, so hard-over and raised
poses never clip. Composite every layer by pinning its pivot to one screen
point. Do NOT align by the top-left corner.

Sheets slice into equal cells. All rows run the 8 headings in this order:
  0 N   1 NE   2 E   3 SE   4 S   5 SW   6 W   7 NW

====================================================================
HULLS
====================================================================
  ConsoleIso.png        1952 x 216    8 x (244x216)   console skiff, 8 dir
  SportSkiffIso.png     1952 x 216    8 x (244x216)   sport skiff, 8 dir
  ConsoleIsoRock.png    1952 x 1728   8 rows x 8 cols  wave-rock loop
  SportSkiffIsoRock.png 1952 x 1728   8 rows x 8 cols  wave-rock loop
      Rock sheets: rows = heading, cols = 8-frame wave loop (roll+pitch+heave).
      Play ~7 fps to idle on the water. Sport rocks livelier (light glass hull);
      console is stiffer. If you'd rather transform at runtime than blit the
      baked loop, apply the same wave to the WHOLE layer stack (see rig rock(i)).

====================================================================
OUTBOARD  (one engine, two paint builds, ships in two layers)
====================================================================
  SkiffMotorUpper-Work.png    2448 x 1728   8 rows x 9 cols   bracket + cowl
  SkiffMotorLower-Work.png    2448 x 1728   8 rows x 9 cols   leg + plate + skeg + prop
  SkiffMotorUpper-Sport.png   2448 x 1728   8 rows x 9 cols
  SkiffMotorLower-Sport.png   2448 x 1728   8 rows x 9 cols
      Rows  = the 8 headings.
      Cols  = steer, 9 frames: col 0 = -30deg (full port) ... col 4 = dead
              ahead ... col 8 = +30deg (full starboard), 7.5deg steps.
      Build: WORK -> console skiff (graphite cowl, brushed badge).
             SPORT -> sport skiff (white cowl, teal side flash, stainless prop).
      There is NO tiller. Steering is remote from the console wheel; the whole
      engine swivels on its clamp. Tie the steer column to the wheel/rudder
      state and step columns at ~8 fps.

  DRAW ORDER (per heading):
      UPPER always composites OVER the hull.
      LOWER goes UNDER the hull for the stern-away headings SE, S, SW
        (indices 3,4,5), and over it everywhere else.
      So: lower -> hull -> upper for SE/S/SW; hull -> lower -> upper otherwise.

  RAISED / TILT pose (prop clear, parked/beaching) is not on the sheet — bake
  on demand from the rig with tilt 0..40.

====================================================================
TWIN FIT  (sport skiff optional upgrade)
====================================================================
Reuse the SAME sport motor sheets — no extra art. Both engines steer together
off the one wheel. The bake is orthographic, so a lateral clamp shift is an
exact per-heading screen offset: blit each layer twice at
  SkiffMotor.MOTOR.mountOffset(dir, mx)   for mx in MOTOR.mounts.dual = [-0.34, +0.34]
Draw the FAR engine first within each layer. Twin is sport-only; the console
workboat is single-engine.
See _preview-sport-twin.png for the assembled result across all 8 headings.

====================================================================
PREVIEWS  (reference only — not for import)
====================================================================
  _preview-work-single.png    console skiff + single outboard, 8 headings
  _preview-sport-single.png   sport skiff + single outboard, 8 headings
  _preview-sport-twin.png     sport skiff + twin outboards, 8 headings
Assembled at native res with the exact pivots and draw order above — use them
to sanity-check your own compositing.

====================================================================
SOURCE RIGS  (parametric, re-bakeable — JS, no deps)
====================================================================
  consoleIsoRig.js     globalThis.ConsoleIso
  sportSkiffIsoRig.js  globalThis.SportSkiffIso
  skiffMotorRig.js     globalThis.SkiffMotor
Each hull rig: render(dir,{elev,roll,pitch,heave}) -> RGBA; rock(i) -> wave
values; motorMount(dir,opts) -> transom clamp point (cell x,y); helmSeat(dir,
opts) -> operator seat anchor; tubMounts(dir,opts) -> 3 fish-tub deck anchors.
Motor rig: renderMotor(dir,{steer|steerDeg, tilt, mx, part, variant, roll,
pitch, heave}) re-bakes any pose; clampPoint(dir,opts) -> clamp-top FX anchor.
Pass a hull's rock(i) values straight into renderMotor so the layers never
shear apart under the wave.

Anchors you get for free (all in hull-cell coords, per heading):
  motor clamp  — where the outboard layer pins (already used by the sheets)
  helm seat    — seat a rider/operator sprite here, facing the bow
  3 tub mounts — two aft quarters + one forward of the console

Questions -> art. The interactive turntables (Console Skiff Iso, Sport Skiff
Iso, Skiff Motor) live in the project if you want to spin any pose live.
