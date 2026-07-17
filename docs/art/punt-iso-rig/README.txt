HIDDEN HARBOURS — PUNT ISO KIT
The ~5.2 m tiller punt + her outboard, in two paint builds.
Game-ready sprites. PNG, transparent background, no anti-aliasing, upper-left
key light. Scale: 32 px = 1 metre. Fixed 3/4 iso camera, elev 40deg, 45deg steps.

The Punt: flat-floored, beamier and slightly longer than the dory, wide low
transom cut for an outboard. Painted white topsides, teal sheer band + bottom,
gold cove pinstripe, bare-wood interior — same fleet scheme as her buoy.
Unlike the console/sport skiffs this boat is TILLER-steered: the operator sits
the stern bench, aft hand on the tiller. No console, no wheel, no twin.

====================================================================
COORDINATES  (read this first — everything pins by PIVOT, not corners)
====================================================================
  Hull cell   184 x 168   pivot (92,94)  = boat origin (amidships,
                          keel bottom, centreline).
  Motor cell  212 x 168   pivot (106,94) = the SAME world origin.
The motor cell is wider than the hull on purpose, so hard-over and raised
poses never clip. Composite every layer by pinning its pivot to one screen
point. Do NOT align by the top-left corner.

Sheets slice into equal cells. All rows run the 8 headings in this order:
  0 N   1 NE   2 E   3 SE   4 S   5 SW   6 W   7 NW

====================================================================
HULL
====================================================================
  PuntIso.png       1472 x 168     8 x (184x168)   the punt, 8 dir
  PuntIsoRock.png   1472 x 1344    8 rows x 8 cols  wave-rock loop
      Rock sheet: rows = heading, cols = 8-frame wave loop (roll+pitch+heave).
      Play ~7 fps to idle on the water. The punt is beamier than the dory, so
      she rolls stiffer. If you'd rather transform at runtime than blit the
      baked loop, apply the same wave to the WHOLE layer stack (see rig rock(i)).

====================================================================
OUTBOARD  (one engine, TWO paint builds, ships in two layers each)
====================================================================
  PuntMotorUpper-Basic.png      1908 x 1344   8 rows x 9 cols   bracket + cowl + tiller
  PuntMotorLower-Basic.png      1908 x 1344   8 rows x 9 cols   leg + plate + skeg + prop
  PuntMotorUpper-Upgraded.png   1908 x 1344   8 rows x 9 cols
  PuntMotorLower-Upgraded.png   1908 x 1344   8 rows x 9 cols
      Rows  = the 8 headings.
      Cols  = steer, 9 frames: col 0 = -32deg (full port) ... col 4 = dead
              ahead ... col 8 = +32deg (full starboard), 8deg steps.
      Build: BASIC    -> weathered grey/black starter (paint scuffs, pan rust).
             UPGRADED -> ~15% larger domed cowl, gloss-black pan, white top,
                         red wrap stripe + side flashes, brighter prop.
      Both builds share the SAME cell, pivot, steer cols and grip JSON — the
      sheets are drop-in swaps (rig renderMotor {variant}). Pick one per boat
      instance (starter vs. upgraded engine) and swap the two PNGs.

      This is a TILLER outboard. Steering swings the tiller across the transom
      (the operator's hand follows — see grips). Tie the steer column to the
      helm/rudder state and step columns at ~8 fps, same cadence as the oars.

  DRAW ORDER (per heading):
      UPPER always composites OVER the hull (the tiller arcs inboard, above
        the deck).
      LOWER goes UNDER the hull for the stern-away headings SE, S, SW
        (indices 3,4,5), and over it everywhere else.
      So: lower -> hull -> upper for SE/S/SW; hull -> lower -> upper otherwise.

  TILLER GRIP (operator's aft hand):
  PuntMotorGrips.json   grip x,y per heading x steer frame, in motor-cell
      space. SHARED by both builds (tiller geometry is identical). Snap the
      operator sprite's aft hand here; he sits the stern bench facing aft-ish
      while under way. Keys: cell, pivot, hullPivot, cols, maxSteerDeg, order,
      grips{HEADING:[{x,y}...9]}.

  RAISED / TILT pose (prop clear, parked/beaching) is not on the sheet — bake
  on demand from the rig with tilt 0..40.

====================================================================
PREVIEWS  (reference only — not for import)
====================================================================
  _preview-basic.png      punt + basic (starter) outboard, 8 headings
  _preview-upgraded.png    punt + upgraded (sport) outboard, 8 headings
Assembled at native res with the exact pivots and draw order above, steer dead
ahead — use them to sanity-check your own compositing. (Operator sprite not
shown; seat him from the grip JSON.)

====================================================================
SOURCE RIG  (parametric, re-bakeable — JS, no deps)
====================================================================
  puntIsoRig.js   -> globalThis.PuntIso
      render(dir,{elev,roll,pitch,heave}) -> RGBA hull.
      rock(i) -> {roll,pitch,heave} wave values for frame i.
      renderMotor(dir,{steer -1..1 | steerDeg, tilt, part:'upper'|'lower',
        variant:'basic'|'upgraded', roll,pitch,heave}) -> RGBA motor layer.
      tillerGrip(dir,opts) -> {x,y} tiller-grip (motor-cell coords).
      motorMount(dir,opts) -> transom clamp point (hull-cell coords).
      tubMounts(dir,opts) -> 2 fish-tub deck anchors (hull-cell coords).
  Pass a hull's rock(i) values straight into renderMotor so the two layers
  ride the same wave and never shear apart.

Anchors you get for free (per heading):
  motor clamp  — where the outboard layer pins (already used by the sheets).
  2 tub mounts — one forward, one aft of centre, on the floor.
  tiller grip  — operator's hand, per steer frame (the grips JSON).

Questions -> art. The interactive turntables (Punt Iso, Punt Motor) live in
the project if you want to spin any pose or re-bake a sheet live.
