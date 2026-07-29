# ART BRIEF — the dory's little two-stroke (her first upgrade)

**To:** art-director (`agents/art-director.md` — your lane is `docs/art/rigs/**`)
**From:** the owner, 2026-07-25
**Status:** requested, not started. Nothing in the engine waits on it.

A small two-stroke outboard that clamps on the **dory's** transom. She is the boat you inherit and
row; this is the first engine you ever own, and the first rung of P2 *Dory to Dynasty*.

**It must read as visibly SMALLER and humbler than the punt's `basic` outboard.** The punt is the
boat you *buy*; this is the motor you bolt onto the boat you were *given*. If a player sees the two
side by side, the ladder should be obvious without a stat screen.

## ⭐ It is NED'S OLD MOTOR (owner, 2026-07-25) — this is the whole brief in one fact

It is not bought. It is **inherited**, like the dory and like the uncle.

The opening runs: the player gets the dory at **Nine Mile Creek** and brings her back to St Peters
**under oars** — a deliberate stretch of real rowing — and on arrival **Aunt Ginny presents Ned's
old motor**. The player earns it with their back, not their wallet.

**So author it as an object with a history.** Not a product: a hand-me-down that sat in a shed. Worn
paint, dulled metal, honest wear at the places a hand grips and a rope chafes. It should look like
it was *kept* rather than *bought* — maintained by someone who couldn't afford to replace it. If it
looks brand new, the scene doesn't land.

Design of record for the sequence and the fuel it drinks:
[`../../design/fuel-and-refuelling.md`](../../design/fuel-and-refuelling.md).

⚠️ One open owner question bears on the art: a real two-stroke burns a **petrol/oil premix**, so if
that is modelled there is an oil tin to author too — see that doc §7 Q4 and
[`fuel-and-fuel-storage.md`](fuel-and-fuel-storage.md) §2.4. **Do not author the tin until it is
ruled on.**

---

## 1. Read this first — coordinates

Everything pins by **PIVOT**, never by corners.

|  | cell | pivot | note |
|---|---|---|---|
| dory hull (exists) | 160 × 156 | (80, 88) | `DoryIso`, 32 px/m, elev 40°, 8 dirs |
| **motor (new)** | **188 × 156** | **(94, 88)** | same world origin as the hull |

The motor cell is **+28 px wider than its hull and the same height** — that is the fleet convention,
derived not invented: punt 184→212, skiff 244→272, both +28, both same height, both pivot x +14.
The extra width is swing room so hard-over steer and full tilt never clip. If your engine needs more
than 28 px of swing, widen the cell and say so in your README — do not let it clip.

Her hull constants, for reference: `L = 4.5`, `TH = 0.035`, `FLOOR = 0.06`, `SEAT = 0.30`,
`OARLOCK_U = 0.31`. Transom is the vertical stern board at `y = -L/2`.

**Derive the clamp height from HER transom, do not copy the punt's.** The punt does
`MOUNT = { x:0, y:-L/2, z: T[0][3]+T[0][2] }` off her own transom table; do the equivalent.
Swivel axis sits just aft of the transom — punt `-L/2 - 0.06`, skiff `-L/2 - 0.07`; yours will be
smaller, so slightly tighter is right.

---

## 2. What makes it a two-stroke

The punt's and the skiffs' motors are four-strokes: big smooth cowls. Yours is the older, simpler
machine, and the silhouette should say so:

- **Small cowl**, less bulk — the engine underneath is a fraction of the size.
- **Pull-start rewind** on top is the classic tell.
- **Shorter leg**, smaller skeg, smaller prop.
- **Tiller** — she is tiller-steered, hand on the arm. No console, no wheel, no remote.
- Well-used rather than new. This engine has a history; it was probably Ned's.

Colour: **ramps only** (ADR 0015 palette guard-rail). The punt's motor uses `MOTO` (engine
grey-blacks) with `RED` for her upgrade stripe. Reuse `MOTO` or bring a ramp of the same family —
this engine should look like it belongs to the same world, and older, not like a different palette.

**One paint build is enough** unless you want more. The punt carries `variants:['basic','upgraded']`
because her engine upgrade is real gameplay; the dory's motor is currently a single step.

---

## 3. ⚠️ You do NOT need to bake sprite sheets for this

The whole fleet went mesh on 2026-07-25 (ADR 0022 phase 7, PR #286). Every hull and every fitting —
oars, outboards — is now a real-time mesh extracted from your rig. **The motor will be a mesh
fitting**, so:

- **No steer sheets.** No 9-column steer strip, no 8-row heading sheet, no rock frames.
- **No `parts:['upper','lower']` split.** That split exists only because sprites cannot interleave
  in depth — a mesh fitting shares the hull's depth buffer and occludes correctly per pixel. Include
  it only if you want consistency with the older rigs; nothing consumes it.
- **`renderMotor(dir, opts)` is still REQUIRED** — but as the *acceptance oracle*, not as art. Our
  tests run your rig in V8, render it at matched poses, and compare the mesh against it pixel for
  pixel. It is how we prove the mesh IS your art. It must render the whole motor in one call.

This is a large saving: the side dragger, the trawlers, the packet and the tanker have no sheets at
all and never will.

---

## 4. The export contract

### 4.1 ⭐ Please export these five — you would be the first rig that needs no shim

`F`, `MATS`, `GAIN`, `BIAS`, `LN`.

Every rig we bake today has these closure-private, so our extractor runs an **in-memory widening**
of your exported object literal to reach them (your file on disk is never touched; we SHA-256 it to
prove that). It works, but it is the last hack in the pipeline — ADR 0022 open question #4. A brand
new rig can simply export them, and this one would be the first that needs no shim at all.

Same for the two below: export them and no shim is needed for them either.

### 4.2 Required for the mesh path

| symbol | what it is |
|---|---|
| a **face builder** taking a pose | e.g. `motorFaces({steer, tilt, ...})` → the face list. This is what we extract geometry from. |
| a **pivot** | the swivel point in hull-rig metres — the `(YA, ZT)` clamp axis. Exported as a value or a function. |
| `MOTOR` block | `maxSteer`, `tiltMax`, own cell `W`/`H`/`pivot`, `variants` |
| `renderMotor(dir, opts)` | the acceptance oracle (§3) |
| `MOUNT` + `motorMount(dir, opts)` | where the clamp hangs on the dory's transom |
| `tillerGrip(dir, opts)` | where the operator's aft hand goes |

### 4.3 ⚠️ The canonical pose must be reachable

We extract the mesh **once**, at a canonical pose — **dead ahead, untilted (`steer: 0, tilt: 0`)** —
and then rotate it at runtime about the pivot. So every real pose must be a **pure rotation** away
from that canonical one.

This bit us on the oars: `oarPose('row', t)` traces an ellipse that never passes through
(sweep 0, dip 0), so we had to call the underlying builder directly to get a neutral pose. Make sure
`steer: 0, tilt: 0` is genuinely reachable and genuinely neutral.

---

## 5. ⚠️ Two things we learned the hard way — please build with them in mind

### 5.1 The clamp bracket is NOT part of the swivelling engine

Both existing motor rigs build the bracket through the **identity** placement `I`, not the posed
transform `X` — because the bracket is bolted to the transom while the engine swivels *on* it.
**This is correct. Please keep it, and say so in your header comment** so nobody later "fixes" it.

We measured what happens when the whole assembly rotates together: **489 silhouette differences**
and a 39–53 px patch — and it is completely **invisible dead ahead**, showing only at hard-over
steer. Exactly the kind of defect a playtest never catches.

For reference the split is small: 6 faces of 96 on the punt, 12 of 100 on the skiff.

### 5.2 Avoid zero-thickness double-sided surfaces

Some rigs make a thin surface visible from both sides by pushing the same face **twice** — `q`, then
`[q3,q2,q1,q0]` — identical vertices, identical depth bias, opposite normals. The dory's oar blade
does this at `doryIsoRig.js:241`.

On a sprite that was harmless: the ambiguity got resolved once at bake time and frozen. **On a mesh
it is resolved every frame, and it z-fights** — a shimmering blade. We measured that your own CPU
renderer is genuinely *non-deterministic* at those pixels: its choice agrees with six different
tie-break rules at 44.6%–51.2%, i.e. all of them at chance.

**So: where a surface needs to be seen from both sides — a prop blade, a skeg plate, an anti-vent
plate — give it real thickness instead of doubling the face.** Two triangles of thickness cost
nothing and remove the defect at source.

(The outboards you have already made are clean — zero such pairs across 624 renders. Only the oar
blade has it. A shader fix is available if you would rather keep the technique; ask before assuming
you must avoid it.)

---

## 6. The seating question — please propose, don't assume

A tiller outboard is driven **from the stern**, and the dory currently has exactly one pilot anchor,
at the rowing station:

| rig | pilot anchor | why |
|---|---|---|
| dory | `PILOT = { x:0, y:-0.30 }` | amidships, *"feet planted to work the oars"* |
| punt (tiller) | `PILOT = { x:0, y:-1.25 }` | aft, within reach of the tiller |

So the dory needs a **second anchor** — a helm/tiller position — or `PILOT` becomes a function of
mode. Your call which shape; the engine already has the vocabulary (`BoatAnchorId` carries
`MotorMount`, `HelmSeat`, `TillerGrip`, `PilotStand`).

**Good news on animation: nothing new is needed.** `characterIsoRig` already has the afloat modifier
`opts.carry` with `'helm'` ("both hands forward-low on a wheel/tiller, braced") and `'oars'`, and
they compose with `idle`, `balance` and `stagger`.

⚠️ **The design question we cannot answer for you:** she is **4.5 m**, the punt is 5.2 m. Moving the
operator from −0.30 to roughly −1.25 in a boat that short is a real trim change — she would sit
noticeably down by the stern. And a rowing dory's transom is narrow. **Does she take the motor
directly, or does she want a transom bracket or a motor well?** That changes the geometry and the
mount point, so please settle it before authoring anything. Sketch the options if it helps the owner
choose.

**The oars stay aboard.** She should ship them, not lose them — and the pose already exists:
`oarPose('resting')` is *"shipped fore, lying along the gunwale."* `DoryOarMath` has the resting
column too. Nothing to author.

---

## 7. Deliverables

1. The rig — your lane, `docs/art/rigs/`. New file, or the motor added to `doryIsoRig.js`; your
   call, but a separate file is easier for us to catalogue (`skiffMotorRig.js` is the precedent for
   a motor that lives apart from its hulls).
2. A `README.txt` in `docs/art/dory-outboard-kit/`, in the house format (see
   `docs/art/punt-iso-rig/README.txt`) — coordinates first, then the parts.
3. **No sheets** (§3).
4. Flag loudly in the PR if you change the export object's shape, and update the rig SHA in the same
   PR as any geometry change (charter §3).

**Append-only:** if you touch `doryIsoRig.js`, every existing cell must stay bit-identical. We
pixel-compare old against new and a changed cell fails the import.

Branch `art/<short-desc>`, one concern per PR, `.github/pull_request_template.md`. After it merges
the owner re-runs the in-editor bake — say so in the PR body.

---

## 8. Open questions for the owner

1. Transom direct, bracket, or motor well? (§6 — decide before authoring)
2. Where does she sit under power, and does the trim change bother him? (§6)
3. One paint build or two? (§2)
4. ~~Is this engine Ned's?~~ **ANSWERED 2026-07-25: yes.** It is Ned's old motor, given by Ginny.
   See the banner at the top — author it worn and kept, not new.
5. Does it burn a petrol/oil premix (an oil tin to author), or just gas?
   [`../../design/fuel-and-refuelling.md`](../../design/fuel-and-refuelling.md) §7 Q4.
