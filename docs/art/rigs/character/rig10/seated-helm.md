# Calibrated seated helms

The unchanged character 10.3 rig already accepts `drive` wheel/seat options. Three named
variants use the actual Go Fast, bowrider and express-cruiser fittings, exported by
`node tools/rig-recipes/seated-helm.mjs --write`. Without `--write`, the tool checks that the
committed contract is current. It pins both character sources and each hull walk by LF SHA256.

The contract's `floorOrigin` is the figure's ground pivot. `seat` is its cushion contact;
they are different points. The Go Fast keeps its standing anchor `(0.42,-1.86,0.62)` and
selects `(0.42,-2.18,0.62)` only for its seated state. Her local seat pin `(0,-0.02,0.56)`
then meets the cushion at `(0.42,-2.20,1.18)`. Standing anchors and hull geometry do not move.

`CharacterSkinAssetBaker.BakeSeatedHelmsCli` selectively adds the three variants to all ten
committed skins and enrolls them in MeshStates. It keeps the ordinary `drive` clip, existing
states, bind meshes and materials. Each calibrated clip carries its contract hash and six
frames of seat, palm and foot pins in figure-frame metres. Feet are the source's footrest
pose, about 0.10m above the floor; they are not claimed to be planted on the cockpit sole.
An ordinary full skin refresh also appends the variants, while retaining the existing per-state switch.

The three BoatVisualDef assets select their state and floor origin. DeckRiderVisual publishes
that origin to both placement and deck occlusion, and DeckRiderMeshPresenter plays that state.
Carried/deck modes, disabled mesh presentation and unavailable calibrated art retain the
standing/deck route. A seated state is never applied to an unrelated hull by a code name lookup.

The real-region plate suite boards each hull through the St Peters picker and helm dispatcher,
then captures all eight headings with the scene camera. The source replay guard checks every
calibrated frame and contact pin on all ten persisted skins; the legacy 27 mesh states remain
guarded separately from the three new states. Other hulls in the fleet census remain findings.
