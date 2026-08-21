// Reads a committed sheet's REAL bytes, whether the checkout has them or only an LFS pointer.
//
// Sprite sheets are `lfs` in .gitattributes (CLAUDE.md rule 9). On a normal checkout — the owner's
// machine, or any container that has run `git lfs pull` — nothing here does any work: the file on
// disk IS the PNG. On a container that has not (the cloud lanes), the file is a ~130-byte pointer,
// and comparing a render against a pointer would "fail" every sheet for a reason that has nothing
// to do with the recipe. So the pointer is resolved through the repo's own LFS endpoint and cached
// OUTSIDE the working tree.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { REPO } from './csharp.mjs';

const POINTER = /^version https:\/\/git-lfs\.github\.com\/spec\/v1\noid sha256:([0-9a-f]{64})\nsize (\d+)\n$/;

export function pointerOf(relPath) {
  const buf = fs.readFileSync(path.join(REPO, relPath));
  if (buf.length > 200) return null;
  const m = POINTER.exec(buf.toString('utf8'));
  return m ? { oid: m[1], size: Number(m[2]) } : null;
}

/** sha256 of the file's real content. An LFS pointer's `oid sha256` IS that hash, so a pointer
 *  needs no download to be named. */
export function contentSha256(relPath) {
  const ptr = pointerOf(relPath);
  if (ptr) return ptr.oid;
  return crypto.createHash('sha256').update(fs.readFileSync(path.join(REPO, relPath))).digest('hex');
}

function cacheDir() {
  // Outside the repo by default: a cache inside the working tree would show up as untracked noise
  // in `git status` on a machine that already has the objects and never needed it.
  const dir = process.env.HH_LFS_CACHE || path.join(os.tmpdir(), 'hh-lfs-cache');
  fs.mkdirSync(dir, { recursive: true });
  return dir;
}

function batchUrl() {
  const url = execFileSync('git', ['-C', REPO, 'remote', 'get-url', 'origin'], { encoding: 'utf8' }).trim();
  return url.replace(/\.git$/, '') + '.git/info/lfs/objects/batch';
}

// ⚠️ curl, not node's fetch. Measured in the cloud container: the same Basic credentials that curl
// authenticates with come back 403 "Maximum number of login attempts exceeded" through undici,
// which shares this container's proxy differently. This path is a FALLBACK for a checkout without
// LFS objects; a normal checkout never reaches it.
function post(url, body, auth) {
  return JSON.parse(execFileSync('curl', [
    '-sS', '-X', 'POST', url,
    '-H', 'Accept: application/vnd.git-lfs+json',
    '-H', 'Content-Type: application/vnd.git-lfs+json',
    '-u', auth, '--data-binary', '@-',
  ], { input: body, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 }));
}

function download(missing) {
  const token = process.env.GH_TOKEN || process.env.GITHUB_TOKEN;
  if (!token)
    throw new Error(`${missing.length} sheet(s) are LFS pointers in this checkout. Run 'git lfs pull', ` +
                    'or set GH_TOKEN so they can be fetched from the repo\'s LFS endpoint.');
  const json = post(batchUrl(), JSON.stringify({ operation: 'download', transfers: ['basic'], objects: missing }),
                    `x-access-token:${token}`);
  if (json.message) throw new Error(`LFS batch API refused: ${json.message}`);
  for (const o of json.objects ?? []) {
    if (!o.actions?.download) throw new Error(`LFS refused ${o.oid}: ${JSON.stringify(o.error ?? {})}`);
    const tmp = path.join(cacheDir(), `${o.oid}.part`);
    execFileSync('curl', ['-sS', '-L', o.actions.download.href, '-o', tmp]);
    const got = crypto.createHash('sha256').update(fs.readFileSync(tmp)).digest('hex');
    if (got !== o.oid) { fs.unlinkSync(tmp); throw new Error(`LFS object ${o.oid} came back hashing ${got}`); }
    fs.renameSync(tmp, path.join(cacheDir(), o.oid));
  }
}

/** Warms the cache for every pointer among `relPaths`, batched. A no-op on a full checkout. */
export function prefetch(relPaths) {
  const missing = [];
  for (const p of relPaths) {
    if (!fs.existsSync(path.join(REPO, p))) continue;
    const ptr = pointerOf(p);
    if (!ptr) continue;
    if (fs.existsSync(path.join(cacheDir(), ptr.oid))) continue;
    if (!missing.some(o => o.oid === ptr.oid)) missing.push({ oid: ptr.oid, size: ptr.size });
  }
  for (let i = 0; i < missing.length; i += 50) download(missing.slice(i, i + 50));
  return missing.length;
}

/** The file's real bytes. */
export function read(relPath) {
  const ptr = pointerOf(relPath);
  if (!ptr) return fs.readFileSync(path.join(REPO, relPath));
  const cached = path.join(cacheDir(), ptr.oid);
  if (!fs.existsSync(cached))
    throw new Error(`${relPath} is an LFS pointer and ${ptr.oid} is not cached — call prefetch() first`);
  return fs.readFileSync(cached);
}
