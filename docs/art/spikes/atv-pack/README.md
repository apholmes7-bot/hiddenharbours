# ATV pack — intake plates (2026-09-06)

Written by `AtvPackMeshParityTests` (EditMode). Everything here is a measurement with a picture
attached, not a picture with a claim attached — the numbers are in the fixture and in
[the PR](../../../../pull/PRNUM), and each plate says which number it shows.

## `<body>-rig-over-mesh-8dir.png`

Top row: the **rig's own `render()`** at rest, all eight facings, `N NE E SE S SW W NW`.
Bottom row: the **extracted mesh**, rasterised by the repo's CPU oracle at the same eight views.

This is the parity the whole hull fleet is held to (`RigMeshMenu.Verify`'s 0.5% bar), and on this
drop it is doing a second job: the ATV rig exports no `MATS`, so its ramp table is **reconstructed**
(`RigMeshSymbols.Reconstructions["atvIsoRig.js"]` — the union of what all three bodies' faces name,
in the rig's own key order). The truth row picks its colours the rig's inline way; the mesh row goes
through the reconstruction. A dropped ramp, or the wrong key first — which the face packer resolves
to index 0 — would re-paint every lit pixel, and it would read in whole percent rather than in
hundredths.

⚠️ Both rows name the body. `AtvIso.resolve` falls back to the **quad** for an unknown or absent
body, at **zero pixels** difference from a real quad, so a truth render without the pick would
compare all three machines against a quad: two would read enormous and the third would pass for
entirely the wrong reason.

⚠️⚠️ **Both rows carry a 1-px keyline, and the shipped machine does not.** The CPU oracle draws its
outline **unconditionally** — it predates ADR 0031 — while these rigs are RINGLESS by default
(`KEYLINE_DEFAULT = false`, as the Dually's is). Against a bare rig render the mesh therefore paints
a perfect one-pixel border the rig does not: measured **351 surplus pixels at E on the bike, and
ZERO missing ones**, which is what identified it. The truth row is rendered with the rig's own
`outline:true` so both sides are in one style; the runtime draws no ring either way
(`GameConfig.DefaultHullKeylineFlood = false`). The hull rigs draw theirs unconditionally, which is
why the hull fleet has never seen this — **no road-fleet body has ever been parity-checked**, and
anyone who runs this oracle over the trucks will meet the same ~20% and should read it here first.

## `enduro-parked-vs-ridden.png`

Left: `render(SE, {body:'dirtbike'})` — the rig's **default**, which is `stand:1`: parked, leaned
12° onto her side stand, 502 faces. Right: the same build at `stand:0` — upright, 497 faces, and the
state a rider mounts into.

**1204 pixels differ.** This is why the fleet entry's rest pose carries `stand:0` in all four places
that select a body: a mesh taken from the default is a bike nobody can ride, and it looks entirely
correct in every still. The kit's own README calls the default a sprite-pooling trap; the same trap
reaches a mesh bake, one layer further in.

The five faces that appear at `stand:1` are the deployed leg. They are **deferred, not faked** — the
stand is a whole-machine lean *plus* a topology change, so it is neither a rotation nor a translation
nor a pose of one build. It belongs with PR 1's mount, where the lean is a transform of the whole
machine and the leg is a state swap.

## `quad-fitted-vs-bare.png`

Left: racks front and rear, hitch and winch. Right: nothing fitted.

The fittings are **BUILD** variants, not poses — the face list changes length — so the bake takes one
of them, and this plate is which: the rig's own defaults (`rackF`, `rackR` and `hitch` on, `winch`
off), which is also what her `wharfQuad` preset dresses. A game that wants a bare quad needs a second
mesh, not a pose.

---

## What these plates deliberately do NOT show

- **A rider.** Nobody is on any of these machines in any sheet, by design: the rider is the
  CHARACTER rig, mounted at `anchors()` / the sidecar's `SADDLE`. Until the astride stance lands
  upstream, a placed machine is a parked machine. That is PR 1.
- **A machine in the world.** Nothing is placed in a scene by this PR. St Peters gets her riders in
  PR 2, after the owner's rulings on who owns what and where it is parked.
