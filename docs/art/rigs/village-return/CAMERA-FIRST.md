# CAMERA-FIRST · the rules, as built (houses, 2026-09-25)

For whoever draws the next building rig.

1. **The camera is fixed.** It is orthographic, at 40°, looking north, so down the screen is world south. A building turns by using another facing's picture. At facing `d`, model `+Y` points `(−sin d·45°, cos d·45°)` and `+X` points `(cos d·45°, sin d·45°)` in world `[east, north]`. Always measure a direction this way; never read it off a label.
2. **One show face.** Name it (`+Y` or `+X`) and put everything the building is proud of on it or on the corners next to it: porch, bay, wing, cross gable, tower, false front, the good door. Nothing it is proud of goes where the camera never looks.
3. **Doors face S, SE, SW, E or W.** `placement()` lists only facings where that holds. A door on the show face lists the three facings where it shows (`via: 'door'`). A door on a side wall lists its diagonal (`door`) and the square-on facing (`signpost`), and the signpost facing only if it passes.
4. **A side door must read when it is edge-on.** Its steps run out along the normal and its hood stands out past the corner. Put a lamp post at the foot of the steps on the show side. At the square-on facing these pieces must cover at least as many pixels as the leaf does face-on, and be 60 % unhidden.
5. **Keep the line of sight to the door clear.** No post stands in front of a door or a stair. A deep porch roof hides the door at the diagonals: keep the door ≥ 60 % visible there. For a house porch that meant 1.6 m deep, the roof front at `eaveZ − 0.25` and a small overhang. Hoods go on brackets, not posts.
6. **Anchors say where a person goes.**
   - `approach` is a ground point at least 1.5 m out, past every deck and step.
   - `entryPath` starts there and never enters the footprint.
   - `footprint()` gives rectangles with decks and steps marked walkable.
   - Give points in model metres, world metres and screen px.
7. **Verbs are stations.** Every thing a character uses names its v9.2 clip and a fixture height inside the range every creator body fits:
   - bench 0.643–0.853
   - chop 0.598–0.760
   - lift / place / toss 0.718–0.978
   - reach 0–1.012
   - rail 0.549–0.724
   - rung 0.211–0.273
   - bed 0.30

   Each needs a stand point reachable from the approach. A verb with no clip goes in `requests`.
8. **Light is not baked.** Rasterise a G-buffer, tag every face and give glass an emitter id. Relight from `WeatherSky.at()` with the tree rig's law. Use no dither and no keyline by default.
9. **Night is occupancy.** Windows belong to rooms, and rooms light when someone is home and awake after dark. Take the schedule, or the game's own occupancy. Lanterns burn while anyone is up or out. Hand the game every lamp's anchor and ground pool.
10. **The cutaway keeps the outside.** Walls facing the camera become sill-height stubs: siding out, finish in, a pale cap. Keep the plinth, the porch cut to match, and a threshold, jamb stubs and a mat where the door was. Indoors the sun is a window's and the sky a soft fill.
11. **Show the goods and the tools where the camera looks.** Woodpiles, barrels, benches and shelving belong on the seen side.
12. **No sidewalks.** Paths to doors are worn dirt, plank or stepping stones. Lanes are dirt.
