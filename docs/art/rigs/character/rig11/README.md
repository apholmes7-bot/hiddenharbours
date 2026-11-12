# Character rig 11.0 (a proposal beside rig 10.3)

A next-iteration look for the mesh figure. Rig 10.3 ships and is untouched; nothing reads this kit yet
(no catalog entry, no baker, no Def, no scene).

What differs from 10.3, all in `Art/characterIsoRig11.js`:

- **Key light.** The key keeps 0.62 of the fleet key's left component (`SIDE`). Rig 10 had none.
- **Proportions.** Head 0.73 (was 0.65), palm 0.076 m (0.065), hand radius x1.16, limb radius x1.12,
  shoulder and hip half-widths 0.192 and 0.090, depth 1.10. Same faces, bones and clips.
- **Tone gain.** 3.0 (was 2.4) on both tone sets; the four-tone window per material is unchanged.

`Art/characterIsoRig11.poses.js` is rig 10.3's poses with the global renamed. `Art/characterIsoRig11.checks.js`
is rig 10.3's checks with the `light` check restated: one outline east and west, a key that leans left, and
screen left the lit side at south. All gates pass for `fisher` and `nan`.

The crown is 3 cm higher (fisher 1.792 m to 1.822 m). Hand and foot sockets do not move.
