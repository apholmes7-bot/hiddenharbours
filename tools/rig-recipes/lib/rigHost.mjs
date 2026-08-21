// Runs the art director's rigs in node's V8 — the same engine ClearScript gives the in-editor baker
// (ADR 0021), with no Unity and no shims. The files are read and evaluated VERBATIM: ADR 0021 §5
// makes "his file is what runs" the whole point, and this harness has no more licence to patch one
// than the baker does.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { REPO, rigCatalog } from './csharp.mjs';

/**
 * sha256 of a rig source with CRLF folded to LF — the repo's own convention for pinning rig bytes
 * (`deckIsoSolid` is f7fc9db5…). ⚠️ core.autocrlf is on for some checkouts, so a raw hash of the
 * working tree reports a drift that does not exist.
 */
export function rigSha256(relPath) {
  const bytes = fs.readFileSync(path.join(REPO, relPath));
  return crypto.createHash('sha256').update(Buffer.from(bytes.toString('binary').replace(/\r\n/g, '\n'), 'binary')).digest('hex');
}

/**
 * Loads `key` and everything it declares as a prerequisite, depth-first and in order — the exact
 * order `RigCatalog.InstallPrerequisites` uses, including its skip of an already-installed global.
 *
 * ⚠️ A missing prerequisite does not throw in these rigs; it renders the WRONG ART (the pass-6 body
 * falls back to its own hat table when the head rig is absent). That is why the dependency is
 * declared in the catalog and read from there, never remembered per caller.
 *
 * Returns the flat load order, so a recipe can record what it took to draw the sheet.
 */
export function install(key, catalog = rigCatalog(), loaded = new Set(), order = []) {
  const entry = catalog[key];
  if (!entry) throw new Error(`no rig catalog entry for '${key}'`);
  if (loaded.has(key)) return order;

  for (const dep of entry.prerequisites) {
    const d = catalog[dep];
    if (!d) throw new Error(`rig '${key}' declares prerequisite '${dep}', which the catalog does not have`);
    if (globalThis[d.global] != null) { loaded.add(dep); continue; }
    install(dep, catalog, loaded, order);
  }

  if (globalThis[entry.global] == null) {
    const src = fs.readFileSync(path.join(REPO, entry.source), 'utf8');
    (0, eval)(src);
  }
  if (globalThis[entry.global] == null)
    throw new Error(`${entry.source} ran but did not install globalThis.${entry.global}`);

  loaded.add(key);
  order.push(entry);
  return order;
}

/**
 * The rig `dir` argument for output cell k — `RigBaker.DirForCell`, transcribed. One dir unit is
 * 45°, so an N-facing bake steps by 8/N; a COUNTER-CLOCKWISE rig emits `(N−k) % N` so the sheet on
 * disk is genuinely clockwise, and a clockwise rig emits k unchanged.
 */
export function dirForCell(cell, facings, convention) {
  if (facings <= 0) throw new Error('facings must be positive');
  const k = ((cell % facings) + facings) % facings;
  const index = convention === 'CounterClockwise' ? (facings - k) % facings : k;
  return index * (8 / facings);
}

/** A rig `render()` result as a plain RGBA Buffer view — the rigs return a bare Uint8ClampedArray. */
export function bytes(result) {
  if (!result || result.length === undefined)
    throw new Error('render() did not return an array — the rig changed return shape');
  return Buffer.from(result.buffer, result.byteOffset, result.length);
}
