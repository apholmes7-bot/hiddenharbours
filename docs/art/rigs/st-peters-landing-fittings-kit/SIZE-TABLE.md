# Phase D re-measure

Authored 3D metres, PPU 32. H includes short suspension; tyre body diameter/depth are separately exact. New geometry is not inferred from projected alpha pixels. The before tyre has only 0.4375 m image height and 0.34375 m image width, with no authored depth: its maximum 42.71% deviation is the width comparison, while its height comparison is 27.08%.

|Stable art ID|Existing scene anchor x,y|Assembly H (m)|Foot x,depth (m)|H / 1.8 m figure|Maximum reference deviation|
|---|---|---:|---|---:|---|
|prop.stp_landing_tyre_west|190, 2.533452|0.619388|0.600000 × 0.205328|0.3441|3.23% · within 15%|
|prop.stp_landing_tyre_east|208, 2.533452|0.619388|0.600000 × 0.205328|0.3441|3.23% · within 15%|
|prop.stp_landing_rope|206.5, 3.283452|0.073085|1.141783 × 0.812115|0.0406|4.46% · within 15%|

References: each tyre body Ø0.60 m, axial section 0.20 m; coil assembly H0.075 m, foot 1.16 × 0.85 m and line Ø20–30 mm. Both new tyre bodies are exactly Ø0.60 × 0.20 m. The full suspension/beads remain within 15%. The coil and its loose tail are Ø25 mm, and the tail retains 0.32 m horizontal reach. The old coil rig hardcodes Ø60 mm tubes and a Ø56 mm tail; its 22.8% audit flag compared overall coil height, not the actual line diameter.

The current scene anchors are (190, 2.533452), (208, 2.533452) and (206.5, 3.283452). The old tyre audit coordinates were half a metre farther east. These recipes preserve the scene's actual anchors. The tyres' lower body datum is approximately 4.734 m at the unchanged deck hanger, above the +2.20 m high-water bound: no visible weed/wet band is falsely added there. A hanging-height change would need a separate placement ruling.
