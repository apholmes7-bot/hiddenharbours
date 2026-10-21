/* Hidden Harbours — gameplay-sidecar export tooling (art side).
   globalThis.SidecarExport

   Every sidecar this tooling writes carries derivedFromRigSha256, hashed from the rig source AS
   SERVED — never typed in by hand. That is the D1 fix: the generator used to emit a sidecar with no
   hash at all, so the import PR had to stamp it, and the stamp was only as honest as the person
   doing it. Now the number and the file it describes cannot come apart: the hash is taken from the
   same bytes the browser loaded to generate the geometry.

   The rig source is never modified here. Hashes are always of the pristine text (an extractor that
   needs internals the rig does not export shims a COPY — see _sportSkiffGameplay.js).

     await SidecarExport.rigSha256('Art/someRig.js')          -> hex
     SidecarExport.stampRigSha(obj, sha)                      -> obj with the field in canonical place
     await SidecarExport.stampFromRig(obj, 'Art/someRig.js')  -> the same, hashed for you
     await SidecarExport.verify(obj, 'Art/someRig.js')        -> { claimed, actual, ok }  (tripwire)
     await SidecarExport.downloadSidecar(obj, name, rigUrl)   -> stamps, then saves

   Strict by design: if the rig cannot be fetched or hashed, downloadSidecar throws rather than
   writing an unstamped file. An unstamped sidecar is the defect.

   NOT CACHED, deliberately (D2 fix, 2026-08-19). Source text and hashes used to be memoised per URL
   for the life of the page. Editing a rig with the export harness open then stamped every later
   sidecar with the hash of the bytes fetched BEFORE the edit, while the geometry came from the
   hot-reloaded rig — a file whose stamp and content described different renderers, and the mixed
   set that shipped in boat-interiors tranche 4. Every call now re-fetches and re-hashes: the stamp
   is taken from the bytes on disk at the moment of writing. A few extra fetches per export is the
   whole cost. */
(function (root) {
  function rigSource(url) {
    return fetch(url, { cache: 'no-store' }).then(r => {
      if (!r.ok) throw new Error('sidecar export: ' + url + ' HTTP ' + r.status);
      return r.text();
    });
  }

  async function sha256Hex(text) {
    if (!(root.crypto && root.crypto.subtle)) throw new Error('sidecar export: no SubtleCrypto — cannot hash the rig');
    const d = await root.crypto.subtle.digest('SHA-256', new TextEncoder().encode(text));
    return Array.from(new Uint8Array(d)).map(b => b.toString(16).padStart(2, '0')).join('');
  }

  function rigSha256(url) {
    return rigSource(url).then(sha256Hex);
  }

  // Canonical position: straight after variant (generated sidecars) or exportSymbol (hand-authored
  // ones), so a generated file diffs cleanly against the set already in Art/gameplay/.
  function stampRigSha(obj, sha) {
    const out = {};
    let placed = false;
    const anchor = ('variant' in obj) ? 'variant' : 'exportSymbol';
    for (const k of Object.keys(obj)) {
      if (k === 'derivedFromRigSha256') continue;
      out[k] = obj[k];
      if (!placed && k === anchor) { out.derivedFromRigSha256 = sha; placed = true; }
    }
    if (!placed) out.derivedFromRigSha256 = sha;
    return out;
  }

  async function stampFromRig(obj, rigUrl) { return stampRigSha(obj, await rigSha256(rigUrl)); }

  async function verify(obj, rigUrl) {
    const actual = await rigSha256(rigUrl);
    const claimed = obj && obj.derivedFromRigSha256 || null;
    return { claimed, actual, ok: claimed === actual };
  }

  function saveJSON(obj, filename) {
    const a = document.createElement('a');
    a.download = filename; a.rel = 'noopener';
    a.href = 'data:application/json;charset=utf-8,' + encodeURIComponent(JSON.stringify(obj, null, 2));
    document.body.appendChild(a); a.click(); document.body.removeChild(a);
  }

  async function downloadSidecar(obj, filename, rigUrl) {
    const stamped = await stampFromRig(obj, rigUrl);
    saveJSON(stamped, filename);
    return stamped;
  }

  root.SidecarExport = { rigSource, sha256Hex, rigSha256, stampRigSha, stampFromRig, verify, saveJSON, downloadSidecar };
})(typeof globalThis !== 'undefined' ? globalThis : window);
