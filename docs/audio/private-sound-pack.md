# The private sound pack

> **Status:** Working guide, written 2026-10-17.
> **Companion:** [`../../Assets/_Project/Audio/AUDIO-MANIFEST.md`](../../Assets/_Project/Audio/AUDIO-MANIFEST.md)
> — the slots, the buses and the music cycle.

Some sounds the owner picked are licensed to the owner alone. This public repository holds the code
that loads them, never the sounds. A checkout without them plays the public clip set, exactly as before.

## The pack folder

The pack lives outside the repository, on the owner's machine. It holds:

- **One file per slot it fills**, named by its `AudioClipSetDef` field: `<Field>.wav` or `<Field>.ogg`.
  A slot pick is mono at 44,100 Hz, the clip-set bar; the installer refuses anything else, names it, and
  leaves that slot as it was. A slot the pack leaves out plays the public clip, or its placeholder.
- **A `Music` folder** holding the songs of the music cycle, played in file-name order. Songs keep their
  own channels and sample rate.

## Install, update, remove

1. In the editor, outside play mode: **Hidden Harbours ▸ Audio ▸ Install Private Sound Pack**. The first
   time, choose the pack folder; the editor remembers it.
2. The menu copies the pack into `Assets/_Project/Audio/Private/` (gitignored, with its `.meta`) and
   points `Private/Resources/AudioClipSetPrivate.asset` at the copies. Run it again after changing the
   pack: it mirrors the folder, so a file removed from the pack leaves the game too.
3. **Remove Private Sound Pack** deletes `Private/`, and play goes back to the public set.

In a batch editor: `-executeMethod HiddenHarbours.Audio.Editor.PrivateSoundPackInstaller.InstallBatch
-soundPackFolder <folder>` (it exits 1 if it refused a file), and `…PrivateSoundPackInstaller.RemoveBatch`.

## What plays

- Each slot the pack fills plays the pack's clip; every other slot plays the public one
  (`AudioClipSets.Overlay`).
- The music bus plays the songs in order: each one through once, then `SongGapSeconds` of silence (on
  the public `AudioClipSet.asset`), then the next, wrapping after the last. Without songs it is silent.
  Cues still duck it.
- Slot picks import like the public clips (PCM, decompressed on load). Songs import as streaming Vorbis,
  so a five-minute song never sits decoded in memory.
- The log says how many slots the pack overrides and how many songs it holds, never a file name.

## Keeping it private

- Never commit anything under `Private/`. `.gitignore` covers the folder and its `.meta`, and
  `AudioClipSetsTests` fails if those lines go missing.
- Never drag a private clip onto the public `AudioClipSet.asset`: on every other machine that slot would
  point at nothing. The same tests fail if the public set points into `Private/`.
- A build made where the pack is installed carries its sounds; a build made anywhere else does not.
