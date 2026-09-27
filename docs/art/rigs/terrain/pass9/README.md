# Terrain pass 9: the kit the ground is baked from

Terrain pass 9, PR 2 (the ground; `docs/design/st-peters-terrain-pass-9.md` §11). This folder holds two
drops' JavaScript as bytes and one driver that bakes the ground's maps from them. The splat shader's
relight (`Assets/_Project/Art/Shaders/Include/TerrainLight6.hlsl`) and its C# twin
(`Assets/_Project/Code/Art/TerrainLight6.cs`) are ports of `terrainLight6.js` here.

**Never edit a drop's file.** A new drop replaces the files, this table and `SHA256SUMS.txt` together.

## Provenance

Every file below is a byte copy: its sha256 here equals its sha256 inside the zip.

| File | Zip | Path in the zip | sha256 |
|---|---|---|---|
| `terrainLight6.js` | cliff & rock kit v6 | `export/cliff-rock-kit-v6/terrainLight6.js` | `f1fd93c0eeb85385182e3ed545dcd6a430f8a33341c52f729f58e6519bcc80f1` |
| `terrainLight5.js` | pixel terrain pass 9 | `Export/pixel-terrain-pass-9/kit/terrainLight5.js` | `7a2999ce088182560b8bafbae85bd13a95fdb2fb32d8f824269d7214a9797385` |
| `pxKit9.js` | pixel terrain pass 9 | `Export/pixel-terrain-pass-9/kit/pxKit9.js` | `68c6537a0a425103de9e9b1ab7ca28ef2585414ced08fdffa53ded338de7dd92` |
| `lib/pixelLanguage.js` | pixel terrain pass 9 | `Export/pixel-terrain-pass-9/kit/lib/pixelLanguage.js` | `2d6fb8201e8454670aeec0abbe4cda2ae744f6a2902924da9357ce09b8242a4d` |
| `lib/plantIsoRig4.js` | pixel terrain pass 9 | `Export/pixel-terrain-pass-9/kit/lib/plantIsoRig4.js` | `1e0916cc2cc7596871ff5fa985a1f334e2c0a67fe5c380140a6a377e63f08496` |
| `lib/pxKit.js` | pixel terrain pass 9 | `Export/pixel-terrain-pass-9/kit/lib/pxKit.js` | `d13d65372cc968d8b9760b324feca2d8995479a7d483cd173cebeb8ec64c0a46` |
| `lib/pxKit2.js` | pixel terrain pass 9 | `Export/pixel-terrain-pass-9/kit/lib/pxKit2.js` | `97e71d92029eebb813bd3d179da334a5b96da1614fb5178fbf119692bb53ac26` |
| `lib/pxKit8.js` | pixel terrain pass 9 | `Export/pixel-terrain-pass-9/kit/lib/pxKit8.js` | `87b607ed2317d5cc32981c330cf6d9e5126cc2e4c1efac12f331284b0daeadf3` |
| `lib/terrainLight4.js` | pixel terrain pass 9 | `Export/pixel-terrain-pass-9/kit/lib/terrainLight4.js` | `6e3a45ef9e45ec5b0d0be579605769b9b2f31ba1a07de2f3694472449e3581f8` |
| `lib/treeIsoRig4.js` | pixel terrain pass 9 | `Export/pixel-terrain-pass-9/kit/lib/treeIsoRig4.js` | `a25cfba1e79d1a2b46a4a92a56ff4877237f3f38f908eb3b02453efe3308e18b` |
| `lib/weatherSky.js` | pixel terrain pass 9 | `Export/pixel-terrain-pass-9/kit/lib/weatherSky.js` | `38300697d5fe685b4800b2a5252aa3c9f0fbcb08bad40e80f6ce3aee1e9c7062` |

The zips, as the owner delivered them:

| Zip | File | Bytes | sha256 |
|---|---|---|---|
| pixel terrain pass 9 | `Pixel art capabilitiesterrainpass9.zip` | 16,163,044 | `5e69ecbf7f8c32fbfb5c8a76199391be03d0ce338beff31b13a79924e476c388` |
| cliff & rock kit v6 | `Pixel art capabilitiescliffrockkitv6.zip` | 3,681,139 | `aa632e3cfc899eef274963ee2a70fbe399f06c010e3224a2a53ae9dbb232bb20` |

`bakePass9.js` and `twinFixture.js` are this repo's own: they call the kits' public API and nothing
else. `SHA256SUMS.txt` covers every file in the folder, both kinds:

```bash
cd docs/art/rigs/terrain/pass9 && sha256sum -c SHA256SUMS.txt
```

`.gitattributes` pins the folder's text files to LF, the bytes as written, so the check holds on any
checkout.

## Load order

The files are IIFEs that register on `globalThis`. Load them in this order:

`lib/pixelLanguage.js`, `lib/pxKit.js`, `lib/pxKit2.js`, `lib/weatherSky.js`, `lib/terrainLight4.js`,
`lib/pxKit8.js`, `terrainLight5.js`, `terrainLight6.js`.

`terrainLight4.js` must load before `pxKit8.js` and `terrainLight6.js`: PxKit8 registers every palette
it paints with in TerrainLight4's `CLASS_OF`, and TerrainLight6 classifies through the same map.
`pxKit9.js`, `plantIsoRig4.js` and `treeIsoRig4.js` build the drop's five example scenes; the bake
does not load them.

## TerrainLight6 and TerrainLight5

TerrainLight6 is TerrainLight5 plus three optional inputs for a floor under a cliff: `o.occ`,
`o.skyv` and `o.seaDir`. With none of the three set it is TerrainLight5 exactly, and the bake sets
none of them, so `--light tl5` writes the same bytes.

## The bake

```bash
node docs/art/rigs/terrain/pass9/bakePass9.js <outDir> [--light tl6|tl5]
```

It bakes every material in `../materials.json` whose `"px"` is true (21 of them) at the ladder's
three steps: 63 tiles, four 256 × 256 maps each, and `TerrainRelight.json`. It takes about 50 s on
Node 24 and is deterministic: a second bake writes the same bytes.

| Map | RGB | A |
|---|---|---|
| `<Name><Step>.png` | the albedo: TerrainLight6's `unlit` view | 255 |
| `_normal` | the contract's normal: R x, G y-up, B up, in floor space | the pond depth over the tile's largest, × 255 |
| `_light` | the contract's R sky visibility, G porosity, B height over the tile's range | palette × 8 + authored band |
| `_detail` | the contract's RG mark id, B surface class × 28 | bit d: the texel is its mark's tip toward direction d |

RGB is the engine contract's, byte for byte. The alphas carry what `TerrainLight6.relight` reads
from a tile besides the contract, so a shader can relight from the maps alone; `bakePass9.js`'s
header defines each one. `TerrainRelight.json` holds, per tile, its palettes (16 at most, five bands
each), its height range and its largest pond depth, and a sha256 over each map's raw RGBA.

The maps are not kept here. `Art/Editor/TerrainTexArrayBuilder.cs` packs them into the splat
shader's arrays, and they are committed under `Assets/_Project/Art/Terrain/` together with those
arrays: a map committed without its array would leave the two out of step.
`TerrainKitPass9BytesTests` holds each array slice and each ramp row to the manifest, and
`TerrainKitAlbedoBytesTests` holds the live albedo PNGs to it.

## The twin's fixture

```bash
node docs/art/rigs/terrain/pass9/twinFixture.js
```

writes `fixtures/terrainLight6.twin.json`: small G-buffers built by the kit's own `gbuf`, the skies
and options they are relit under, and the kit's relit bytes at sampled texels.
`Assets/Tests/EditMode/TerrainLight6TwinTests.cs` relights the same texels through the C# twin and
compares them. The expected bytes come only from the kit; the twin never computes them.
Part 2's controls set each of TerrainLight6's inputs alone: `occ` over half the frame, `skyv` = 0.5,
and `seaDir` = -1 at frame 7 under the gale, whose swell is what `seaDir` turns. A case whose label
names none of the three leaves all three unset.
