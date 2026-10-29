/* NMC roads (H4): bake the road floor's chunks from road / path kit v4, through the kit's public API only.

     node tools/nmc-roads/bake-chunks.cjs <outDir> [--window <chunks>] [--only <cx0>,<cy0>,<cx1>,<cy1>] [--measure] [--albedo]

   The owner's ruling (Q1 a): bake v4's chunks with the kit as is, stream the ones the camera sees. The kit
   is loaded byte for byte from docs/art/rigs/road-path-kit-v4/ and run as its own scenes run it
   (roadScenes4.js build, then its README's pipeline):

     net = RoadKit4.network(routes, {w, h, seed}); RoadKit4.zones(net, zone, zi); fl = PxKit8.floor(w, h, defs, seed + 5)
     RoadKit4.paint(fl.tile, net); G = TerrainLight5.gbuf(fl.tile, {relief, wrap: false}); RoadKit4.patchG(G, net)
     RoadKit4.faces(net, fl.tile, G)

   over windows of whole chunks with a one-chunk apron, and writes the four maps of every 256 px chunk that
   shows a road, in screen space (the floor texel each pixel shows after the faces' projection, or the face):

     c<cx>_<cy>_normal.png   TerrainLight5.view 'normal' RGB; A = G.pond over the chunk's largest, x 255
     c<cx>_<cy>_light.png    R ao (view 'light'), B the height over the chunk's range; A = the chunk's
                             palette x 8 + the authored band (32 palettes at most). G the ROLE: 255 where a
                             route's floor covers the pixel, 170 a face, 85 a face under a deck (the kit's air
                             face, lit at shade level 3), else 0 (kept for its neighbours' relight). The view's
                             G is porosity, which the relight never reads (the class carries it).
     c<cx>_<cy>_detail.png   RG the mark (the kit's tile holds marks in 16 bits), B the class x 28 (view 'detail');
                             A bit d: the texel is its mark's tip toward TIP_DIRS[d] and stands proud.
                             A face has mark 0 and no tips, as the kit's face G-buffer does. (The face flag
                             once rode here, as A 255 with mark 0; a one-texel mark 1 proud in all eight
                             directions read the same, so the flag is the light map's G.)
     albedo/c<cx>_<cy>.png   with --albedo, a preview, never streamed (the shader relights every texel): TerrainLight5's unlit
                             view (faces: relightFaces under UNLIT), A = 255 where the road covers the pixel
     RoadFloor.json          per chunk: its palettes, height range, largest pond, the largest height within
                             REACH px of it (the sun march's early out), and each map's sha256

   A pixel further than REACH px from every covered pixel is written as 0 in all four maps: the relight
   reads a covered pixel's neighbours no further than its sun march's last step (TerrainLight5 SSTEP, 36).
   For the same reason a chunk no road covers is baked when any pixel of it is that near one (covered 0 in
   RoadFloor.json), and a route's reach for the windows runs NEAR_METRES further: the shader reads a chunk
   it has not got as nothing there, so every chunk a covered pixel's relight reads is one it can have.

   Chunk (cx, cy) covers x ORIGIN.x + 8 cx .. + 8, y ORIGIN.y - 8 cy - 8 .. ORIGIN.y - 8 cy (y north): G3's
   ground grid, 95 x 70 chunks of 8 m. Pixel rows run from the north, 32 px = 1 m, as the kit's scenes do.

   Every window is given every route, in the network's order: a route's seed is its index (prep: seed + k
   * 131 + 17), so the same order gives the same pattern laid in (s, d) in every window. A route whose reach
   stays STUB_METRES clear of a window is passed as a two-point stub far outside it, keeping its index and
   class. Patterns the kit keys to window pixels (hash2(x, y), fb(x / n, y / n)) cannot match across a
   window's edge, so the windows are cut where the fewest road texels lie (partition) and --measure
   reports each seam.                                                                                       */
'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm'), zlib = require('zlib'), crypto = require('crypto');

const ROOT = path.resolve(__dirname, '..', '..');
const KIT = path.join(ROOT, 'docs/art/rigs/road-path-kit-v4');
const NETWORK = path.join(ROOT, 'Assets/_Project/Data/Terrain/NineMileCreekRoads/NineMileCreekRoadNetwork.json');
const LOAD = ['lib/pixelLanguage.js', 'lib/pxKit.js', 'lib/pxKit2.js', 'lib/weatherSky.js', 'lib/terrainLight4.js', 'lib/pxKit8.js', 'lib/terrainLight5.js', 'roadPathRig4.js'];

// G3's ground grid (tools/nmc-walls/g3.cjs: 1520 x 1120 texels at 2 a metre, x from -380, rows from y 280)
const ORIGIN = { x: -380, y: 280 }, CHUNK_M = 8, GRID = { w: 95, h: 70 };
const PPM = 32, SIZE = CHUNK_M * PPM, SEED = 7;
const TUNABLES = {
  windowChunks: 8,       // a window's owned chunks per side at most (memory: about 2.3 GB a window at 8)
  apronChunks: 1,        // the apron round a window, in chunks: the kit's junction marks, ponds and sun march stay whole
  stubMetres: 20,        // a route whose reach stays this far clear of a window is passed as a stub
  coverCellMetres: 0.5,  // the partition's coverage estimate, per cell
  maxPalettes: 32,       // the road ramp's row (the ruling: 32 palettes, five bands each)
};
// floor space: x east, y south. Directions 0 to 7 run clockwise from east, 45 degrees apart (bakePass9.js).
const R = Math.SQRT1_2;
const TIP_DIRS = [[1, 0], [R, R], [0, 1], [-R, R], [-1, 0], [-R, -R], [0, -1], [R, -R]];
const PROUD = 0.12;                 // relight: `lit && G.proud[i] > 0.12` adds the tip's band
const REACH = 36;                   // relight: sunVis's last step, SSTEP[10]
const LIFT_PX = 9;                  // the most a face rises on screen: the boardwalk's lift, 0.35 m x the kit's 24.5 px
const NEAR_METRES = (REACH + LIFT_PX) / PPM;   // a chunk the relight reads lies this far past a route's reach at most
const FACE = 170, FACE_AIR = 85;    // the role (light G) of a face, and of one under a deck (floor 255, none 0)

// ---- the kit ------------------------------------------------------------------------------------------------
function loadKit() {
  const rigSha256 = {};
  for (const f of LOAD) {
    const file = path.join(KIT, f), src = fs.readFileSync(file);
    rigSha256[f] = crypto.createHash('sha256').update(src).digest('hex');
    vm.runInThisContext(src.toString('utf8'), { filename: file });
  }
  const g = globalThis, kit = { RK: g.RoadKit4, K8: g.PxKit8, TL: g.TerrainLight5, K: g.PxKit, rigSha256 };
  if (!kit.RK || !kit.K8 || !kit.TL || !kit.K) throw new Error('the kit did not load');
  return kit;
}

// ---- PNG, 8-bit RGBA, filter 0 (bakePass9.js) ---------------------------------------------------------------
const CRC = new Uint32Array(256);
for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? (0xedb88320 ^ (c >>> 1)) >>> 0 : c >>> 1; CRC[n] = c; }
function crc32(buf) { let c = 0xffffffff; for (let i = 0; i < buf.length; i++) c = (CRC[(c ^ buf[i]) & 255] ^ (c >>> 8)) >>> 0; return (c ^ 0xffffffff) >>> 0; }
function pngChunk(type, data) {
  const head = Buffer.alloc(4), tail = Buffer.alloc(4), td = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  head.writeUInt32BE(data.length); tail.writeUInt32BE(crc32(td)); return Buffer.concat([head, td, tail]);
}
function png(rgba, w, h) {
  const row = w * 4 + 1, raw = Buffer.alloc(row * h);
  for (let y = 0; y < h; y++) Buffer.from(rgba.buffer, rgba.byteOffset + y * w * 4, w * 4).copy(raw, y * row + 1);
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 6;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), pngChunk('IHDR', ihdr), pngChunk('IDAT', zlib.deflateSync(raw, { level: 9 })), pngChunk('IEND', Buffer.alloc(0))]);
}
const sha = (a) => crypto.createHash('sha256').update(Buffer.from(a.buffer, a.byteOffset, a.byteLength)).digest('hex');
const hex = (c) => '#' + c.map((v) => v.toString(16).padStart(2, '0')).join('');

// ---- the routes, in world metres ----------------------------------------------------------------------------
// the network's own fields the kit reads; part, from and clip are the network's notes. The corner flares (the owner's
// Q4, 10-24) follow the routes, so a route's index in the kit, and with it its seed, is the same with or without them
const KIT_FIELDS = ['id', 'cls', 'w', 'surf', 'bands', 'ends'];
function readRoutes(RK) {
  const doc = JSON.parse(fs.readFileSync(NETWORK, 'utf8'));
  const routes = doc.routes.concat(doc.flares || []).map((r) => {
    const o = {}; for (const k of KIT_FIELDS) if (r[k] !== undefined) o[k] = r[k];
    o.pts = r.pts;
    const C = RK.CLASSES[r.cls]; if (!C) throw new Error(`${r.id}: the kit has no class ${r.cls}`);
    // the kit's reach (prep): the carriage half-width with its wave, the bands, the rag, 4 px
    const bands = r.bands || C.bands;
    o.reachMetres = (r.w || C.w) / 2 * (1 + (C.wv || 0)) + bands.reduce((a, b) => a + b.w, 0) + (C.rag || 0) + 4 / PPM;
    return o;
  });
  return { doc, routes, sha256: crypto.createHash('sha256').update(fs.readFileSync(NETWORK)).digest('hex') };
}
const segDist = (px, py, ax, ay, bx, by) => {
  const dx = bx - ax, dy = by - ay, L2 = dx * dx + dy * dy || 1e-12;
  const t = Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / L2));
  return Math.hypot(ax + dx * t - px, ay + dy * t - py);
};
// a rectangle's distance to a polyline, in metres (0 inside)
function rectToLine(x0, y0, x1, y1, pts) {
  let best = Infinity;
  for (let k = 0; k < pts.length - 1; k++) {
    const [ax, ay] = pts[k], [bx, by] = pts[k + 1];
    if (Math.max(ax, bx) >= x0 && Math.min(ax, bx) <= x1 && Math.max(ay, by) >= y0 && Math.min(ay, by) <= y1) {
      // the segment's box meets the rectangle: sample it (the windows are tens of metres; a metre's step)
      const n = Math.max(1, Math.ceil(Math.hypot(bx - ax, by - ay)));
      for (let j = 0; j <= n; j++) { const x = ax + (bx - ax) * j / n, y = ay + (by - ay) * j / n; if (x >= x0 && x <= x1 && y >= y0 && y <= y1) return 0; }
    }
    for (const [cx, cy] of [[x0, y0], [x1, y0], [x0, y1], [x1, y1]]) best = Math.min(best, segDist(cx, cy, ax, ay, bx, by));
    for (const [px, py] of [[ax, ay], [bx, by]]) {
      const dx = px < x0 ? x0 - px : px > x1 ? px - x1 : 0, dy = py < y0 ? y0 - py : py > y1 ? py - y1 : 0;
      best = Math.min(best, Math.hypot(dx, dy));
    }
  }
  return best;
}

// ---- the partition: windows of whole chunks, cut where the fewest road texels lie ---------------------------
// The coverage estimate: a cell is covered when its centre lies within a route's reach of its control
// polyline (the spline runs within centimetres of it). A cut's cost is the covered cells along it.
function coverage(routes) {
  const c = TUNABLES.coverCellMetres, nx = Math.round(GRID.w * CHUNK_M / c), ny = Math.round(GRID.h * CHUNK_M / c);
  const cov = new Uint8Array(nx * ny);
  for (const r of routes) {
    for (let k = 0; k < r.pts.length - 1; k++) {
      const [ax, ay] = r.pts[k], [bx, by] = r.pts[k + 1], m = r.reachMetres + NEAR_METRES;
      const i0 = Math.max(0, Math.floor((Math.min(ax, bx) - m - ORIGIN.x) / c)), i1 = Math.min(nx - 1, Math.ceil((Math.max(ax, bx) + m - ORIGIN.x) / c));
      const j0 = Math.max(0, Math.floor((ORIGIN.y - Math.max(ay, by) - m) / c)), j1 = Math.min(ny - 1, Math.ceil((ORIGIN.y - Math.min(ay, by) + m) / c));
      for (let j = j0; j <= j1; j++) for (let i = i0; i <= i1; i++) {
        const x = ORIGIN.x + (i + 0.5) * c, y = ORIGIN.y - (j + 0.5) * c;
        if (segDist(x, y, ax, ay, bx, by) <= m) cov[j * nx + i] = 1;
      }
    }
  }
  const per = CHUNK_M / c, chunk = new Uint32Array(GRID.w * GRID.h);
  for (let j = 0; j < ny; j++) for (let i = 0; i < nx; i++) if (cov[j * nx + i]) chunk[Math.floor(j / per) * GRID.w + Math.floor(i / per)]++;
  // cutX[cx][cy]: covered cells on the west edge of chunk (cx, cy), the column x = cx; cutY likewise on the north edge
  const cutX = (cx, cy) => { if (cx <= 0 || cx >= GRID.w) return 0; let n = 0; const i = cx * per; for (let j = cy * per; j < (cy + 1) * per; j++) n += cov[j * nx + i - 1] && cov[j * nx + i] ? 1 : 0; return n; };
  const cutY = (cx, cy) => { if (cy <= 0 || cy >= GRID.h) return 0; let n = 0; const j = cy * per; for (let i = cx * per; i < (cx + 1) * per; i++) n += cov[(j - 1) * nx + i] && cov[j * nx + i] ? 1 : 0; return n; };
  return { chunk, cutX, cutY };
}
function partition(cov, only) {
  const N = TUNABLES.windowChunks, out = [];
  const used = (x0, y0, x1, y1) => { for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) if (cov.chunk[y * GRID.w + x] && (!only || (x >= only[0] && y >= only[1] && x < only[2] && y < only[3]))) return true; return false; };
  const trim = (x0, y0, x1, y1) => {   // the box of a region's road chunks
    let a = Infinity, b = Infinity, c = -1, d = -1;
    for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) if (cov.chunk[y * GRID.w + x]) { a = Math.min(a, x); b = Math.min(b, y); c = Math.max(c, x + 1); d = Math.max(d, y + 1); }
    return c < 0 ? null : [a, b, c, d];
  };
  (function split(x0, y0, x1, y1) {
    const t = trim(x0, y0, x1, y1); if (!t) return; [x0, y0, x1, y1] = t;
    const w = x1 - x0, h = y1 - y0;
    if (w <= N && h <= N) { if (used(x0, y0, x1, y1)) out.push([x0, y0, x1, y1]); return; }
    // cut the longer side at the line of least road that adds no window: ceil(a / N) + ceil(b / N) = ceil(w / N)
    const vertical = w >= h, L = vertical ? w : h, need = Math.ceil(L / N);
    let best = null;
    for (let k = 1; k < L; k++) {
      if (Math.ceil(k / N) + Math.ceil((L - k) / N) !== need) continue;
      let cost = 0;
      if (vertical) for (let y = y0; y < y1; y++) cost += cov.cutX(x0 + k, y); else for (let x = x0; x < x1; x++) cost += cov.cutY(x, y0 + k);
      if (!best || cost < best.cost) best = { k, cost };
    }
    if (vertical) { split(x0, y0, x0 + best.k, y1); split(x0 + best.k, y0, x1, y1); }
    else { split(x0, y0, x1, y0 + best.k); split(x0, y0 + best.k, x1, y1); }
  })(0, 0, GRID.w, GRID.h);
  return out;
}

// a pixel within r (a square) of a set one
function dilate(M, w, h, r) {
  const T = new Uint8Array(w * h), O = new Uint8Array(w * h);
  for (let y = 0; y < h; y++) { const o = y * w; let c = 0; for (let x = 0; x < Math.min(w, r); x++) c += M[o + x]; for (let x = 0; x < w; x++) { if (x + r < w) c += M[o + x + r]; if (x - r - 1 >= 0) c -= M[o + x - r - 1]; T[o + x] = c > 0 ? 1 : 0; } }
  for (let x = 0; x < w; x++) { let c = 0; for (let y = 0; y < Math.min(h, r); y++) c += T[y * w + x]; for (let y = 0; y < h; y++) { if (y + r < h) c += T[(y + r) * w + x]; if (y - r - 1 >= 0) c -= T[(y - r - 1) * w + x]; O[y * w + x] = c > 0 ? 1 : 0; } }
  return O;
}

// ---- one window ----------------------------------------------------------------------------------------------
// win = [cx0, cy0, cx1, cy1], the chunks it owns. Returns its chunks' maps and what the measures need.
function bakeWindow(kit, routes, win, opts) {
  const { RK, K8, TL, K } = kit, A = TUNABLES.apronChunks, t0 = Date.now();
  const ax0 = win[0] - A, ay0 = win[1] - A, W = (win[2] - win[0] + 2 * A) * SIZE, H = (win[3] - win[1] + 2 * A) * SIZE, N = W * H;
  const wx = ORIGIN.x + ax0 * CHUNK_M, wy = ORIGIN.y - ay0 * CHUNK_M;          // the window's north-west corner, metres
  const ex = wx + W / PPM, sy = wy - H / PPM;
  let stubs = 0;
  const kr = routes.map((r) => {
    const o = {}; for (const k of KIT_FIELDS) if (r[k] !== undefined) o[k] = r[k];
    if (rectToLine(wx, sy, ex, wy, r.pts) > r.reachMetres + TUNABLES.stubMetres) { stubs++; o.pts = [[-1e5, -1e5], [-1e5 + 2 * PPM, -1e5]]; return o; }
    o.pts = r.pts.map((p) => [(p[0] - wx) * PPM, (wy - p[1]) * PPM]);
    return o;
  });
  // roadScenes4.build: the zones (the base is grass: only the routes' own roles ship), the floor, the paint
  const net = RK.network(kr, { w: W, h: H, seed: SEED });
  const Z = ['grass', 'dirt', 'path', 'sedge', 'sand', 'marram', 'mud'], zi = {}; Z.forEach((k, i) => { zi[k] = i; });
  const zone = new Uint8Array(N); RK.zones(net, zone, zi);
  const seen = new Uint8Array(Z.length); for (let j = 0; j < N; j++) seen[zone[j]] = 1;
  const MATS = K.MATS, defs = Z.filter((k, i) => seen[i] && MATS[k]).map((k) => ({ key: k, step: 1, seed: (MATS[k].seed || 1) + SEED * 13, R: (x, y) => x >= 0 && y >= 0 && x < W && y < H && zone[y * W + x] === zi[k] }));
  const fl = K8.floor(W, H, defs, SEED + 5);
  RK.paint(fl.tile, net);
  const t = fl.tile, G = TL.gbuf(t, { relief: 1.2 * K.DETAIL, wrap: false });
  RK.patchG(G, net);
  const F = RK.faces(net, t, G), tBuild = Date.now() - t0;
  // the maps, in plan space (the floor) and in screen space (the faces)
  const unlit = TL.view(G, 'unlit', TL.UNLIT), normal = TL.view(G, 'normal', TL.UNLIT), light = TL.view(G, 'light', TL.UNLIT), detail = TL.view(G, 'detail', TL.UNLIT);
  const fUnlit = RK.relightFaces(net, TL.UNLIT, {}).slice();
  const fNormal = TL.view(F.G, 'normal', TL.UNLIT), fLight = TL.view(F.G, 'light', TL.UNLIT), fDetail = TL.view(F.G, 'detail', TL.UNLIT);
  // the tips, as relight finds them (terrainLight5 relight, `if (!only)`): per mark, the texel furthest toward the
  // direction over the whole window, the first in row order on a tie
  const tips = TIP_DIRS.map(([dx, dy]) => {
    const ll = Math.hypot(dx, dy), lx = dx / ll, ly = dy / ll, m = new Map();
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x, k = t.mark[i]; if (!k) continue;
      const v = x * lx + y * ly, q = m.get(k); if (!q || v > q[0]) m.set(k, [v, i]);
    }
    return m;
  });
  if (opts.verifyTips) {
    for (let d = 0; d < 8; d++) {
      const o = {}; TL.relight(G, { sunF: [TIP_DIRS[d][0], TIP_DIRS[d][1], 1], sunI: 0, grade: false }, o);
      if (o._tips.size !== tips[d].size) throw new Error(`tips ${d}: relight finds ${o._tips.size} marks, the driver ${tips[d].size}`);
      for (const [k, v] of o._tips) { const q = tips[d].get(k); if (!q || q[1] !== v[1]) throw new Error(`tips ${d}: mark ${k} tips at ${v[1]} in relight, ${q && q[1]} here`); }
    }
  }
  const tBake = Date.now() - t0 - tBuild;
  // screen space: covered (a route's role, or a face), the height each pixel shows, and REACH round the covered
  const cover = new Uint8Array(N), sH = new Float32Array(N);
  for (let p = 0; p < N; p++) { const s = F.SRC[p]; cover[p] = s >= 0 ? (net.role[s] ? 1 : 0) : 1; sH[p] = s >= 0 ? G.H[s] : G.H[F.foot[p]]; }
  const near = dilate(cover, W, H, REACH);
  let lift = 0; for (let i = 0; i < net.zr.length; i++) lift = Math.max(lift, Math.round(net.zr[i] * 24.5));
  if (lift > LIFT_PX) throw new Error(`a face rises ${lift} px, past LIFT_PX ${LIFT_PX}: the coverage's near band is too narrow`);
  // ---- the chunks: screen space, the faces' projection (F.SRC) -------------------------------------------------
  const chunks = [], apron = [];
  for (let cy = win[1]; cy < win[3]; cy++) for (let cx = win[0]; cx < win[2]; cx++) {
    const px0 = (cx - ax0) * SIZE, py0 = (cy - ay0) * SIZE, M = SIZE * SIZE;
    const maps = { albedo: new Uint8ClampedArray(M * 4), normal: new Uint8ClampedArray(M * 4), light: new Uint8ClampedArray(M * 4), detail: new Uint8ClampedArray(M * 4) };
    const hgt = new Float32Array(M), pond = new Float32Array(M), pal = new Int32Array(M), band = new Uint8Array(M);
    let covered = 0, faces = 0;
    for (let y = 0; y < SIZE; y++) for (let x = 0; x < SIZE; x++) {
      const p = (py0 + y) * W + px0 + x, s = F.SRC[p], c = y * SIZE + x, c4 = c * 4, p4 = p * 4;
      if (s >= 0) {
        const s4 = s * 4;
        for (let k = 0; k < 3; k++) { maps.albedo[c4 + k] = unlit[s4 + k]; maps.normal[c4 + k] = normal[s4 + k]; maps.light[c4 + k] = light[s4 + k]; maps.detail[c4 + k] = detail[s4 + k]; }
        maps.albedo[c4 + 3] = net.role[s] ? 255 : 0; maps.light[c4 + 1] = net.role[s] ? 255 : 0; if (net.role[s]) covered++;
        const k = t.mark[s];
        let bits = 0;
        if (k && G.proud[s] > PROUD) for (let d = 0; d < 8; d++) { const q = tips[d].get(k); if (q && q[1] === s) bits |= 1 << d; }
        maps.detail[c4 + 3] = bits;
        hgt[c] = G.H[s]; pond[c] = G.pond[s]; pal[c] = t.pal[s]; band[c] = G.band0[s];
      } else {
        for (let k = 0; k < 3; k++) { maps.albedo[c4 + k] = fUnlit[p4 + k]; maps.normal[c4 + k] = fNormal[p4 + k]; maps.light[c4 + k] = fLight[p4 + k]; maps.detail[c4 + k] = fDetail[p4 + k]; }
        maps.albedo[c4 + 3] = 255; maps.light[c4 + 1] = F.air[p] ? FACE_AIR : FACE; covered++; faces++;
        maps.detail[c4] = 0; maps.detail[c4 + 1] = 0; maps.detail[c4 + 3] = 0;
        hgt[c] = G.H[F.foot[p]]; pond[c] = 0; pal[c] = F.G.t.pal[p]; band[c] = F.G.band0[p];
      }
    }
    // a chunk no road covers is still baked when the relight reads it: a covered pixel's sun march, seam or pond
    // test lands in it (within REACH), and the shader reads an absent chunk as nothing there
    let nearAny = covered > 0;
    for (let y = 0; y < SIZE && !nearAny; y++) for (let x = 0; x < SIZE; x++) if (near[(py0 + y) * W + px0 + x]) { nearAny = true; break; }
    if (!nearAny) continue;
    // the chunk's palettes, in first-seen order
    const local = new Map(), pals = [];
    for (let c = 0; c < M; c++) { const p = pal[c]; if (!local.has(p)) { local.set(p, pals.length); pals.push(p); } }
    if (pals.length > TUNABLES.maxPalettes) throw new Error(`chunk ${cx},${cy}: ${pals.length} palettes, the road ramp's row holds ${TUNABLES.maxPalettes}`);
    let h0 = Infinity, h1 = -Infinity, pm = 0;
    for (let c = 0; c < M; c++) { if (hgt[c] < h0) h0 = hgt[c]; if (hgt[c] > h1) h1 = hgt[c]; if (pond[c] > pm) pm = pond[c]; }
    const range = Math.max(1e-3, h1 - h0), pondMax = Math.max(1e-3, pm);
    let blank = 0;
    for (let y = 0; y < SIZE; y++) for (let x = 0; x < SIZE; x++) {
      const c = y * SIZE + x, c4 = c * 4;
      if (!near[(py0 + y) * W + px0 + x]) { maps.albedo.fill(0, c4, c4 + 4); maps.normal.fill(0, c4, c4 + 4); maps.light.fill(0, c4, c4 + 4); maps.detail.fill(0, c4, c4 + 4); blank++; continue; }
      maps.light[c4 + 2] = (hgt[c] - h0) / range * 255;
      maps.light[c4 + 3] = local.get(pal[c]) * 8 + band[c];
      maps.normal[c4 + 3] = pond[c] / pondMax * 255;
    }
    // the largest height a sun march from this chunk can meet: the chunk and REACH px round it
    let hn = -Infinity;
    for (let y = Math.max(0, py0 - REACH); y < Math.min(H, py0 + SIZE + REACH); y++) for (let x = Math.max(0, px0 - REACH); x < Math.min(W, px0 + SIZE + REACH); x++) if (sH[y * W + x] > hn) hn = sH[y * W + x];
    chunks.push({ cx, cy, maps, covered, faces, blank, palettes: pals.map((p) => t.pals[p].map(hex)), heightMin: h0, heightMax: h1, heightRange: range, heightMaxNear: hn, pondMax });
  }
  // the apron's road chunks, as this window paints them: the seam measure compares them with their owners'
  if (opts.apron) for (let cy = ay0; cy < ay0 + H / SIZE; cy++) for (let cx = ax0; cx < ax0 + W / SIZE; cx++) {
    if (cx >= win[0] && cx < win[2] && cy >= win[1] && cy < win[3]) continue;
    const px0 = (cx - ax0) * SIZE, py0 = (cy - ay0) * SIZE, rgba = new Uint8ClampedArray(SIZE * SIZE * 4);
    let any = 0;
    for (let y = 0; y < SIZE; y++) for (let x = 0; x < SIZE; x++) {
      const p = (py0 + y) * W + px0 + x, s = F.SRC[p], c4 = (y * SIZE + x) * 4;
      if (!cover[p]) continue; any = 1;
      const src = s >= 0 ? unlit : fUnlit, o4 = (s >= 0 ? s : p) * 4;
      rgba[c4] = src[o4]; rgba[c4 + 1] = src[o4 + 1]; rgba[c4 + 2] = src[o4 + 2]; rgba[c4 + 3] = 255;
    }
    if (any) apron.push({ cx, cy, albedo: rgba });
  }
  const mem = process.memoryUsage();
  return { win, W, H, stubs, chunks, apron, palettes: t.pals.length, tBuild, tBake, tChunks: Date.now() - t0 - tBuild - tBake, rssMB: Math.round(mem.rss / 1048576), abMB: Math.round(mem.arrayBuffers / 1048576),
    // with opts.keep, the kit's own buffers (the read-model measure relights them)
    keep: opts.keep ? { net, G, F, tile: t, tips, ax0, ay0 } : null };
}

// ---- the seams ---------------------------------------------------------------------------------------------------
/* Where two windows' road chunks meet (a | b), each window has painted the other's edge in its apron. Across
   the edge, pairs of 8 x 8 blocks facing each other (BLOCK px deep, the pixels both sides cover) give a step
   in mean luma. The seam's step is a as its window paints it against b as b's window paints it; each window's
   own step is a against b as a's window paints both (and b against a in b's). A seam shows as a line where
   its step stands above the windows' own (the kit's low-frequency patterns, fb(x / 26, ...), step there).
   Also counted: the share of the edge's pixels that are the same bytes in both windows' paint. */
const BLOCK = 8;
const luma = (a, i) => 0.2126 * a[i] + 0.7152 * a[i + 1] + 0.0722 * a[i + 2];
// the mean luma of a block, the pixels covered in both maps given; null under half covered
function blockMean(m, also, x0, y0) {
  let n = 0, l = 0;
  for (let y = y0; y < y0 + BLOCK; y++) for (let x = x0; x < x0 + BLOCK; x++) { const i = (y * SIZE + x) * 4; if (m[i + 3] !== 255 || also[i + 3] !== 255) continue; n++; l += luma(m, i); }
  return n >= BLOCK * BLOCK / 2 ? l / n : null;
}
function seams(owned, aprons) {
  const out = [];
  for (const [key, b] of owned) {
    const [cx, cy] = key.split(',').map(Number);
    for (const [dx, dy, edge] of [[-1, 0, 'west'], [0, -1, 'north']]) {
      const a = owned.get(`${cx + dx},${cy + dy}`); if (!a || a.win === b.win) continue;
      const bInA = aprons.get(`${cx},${cy},${a.win}`), aInB = aprons.get(`${cx + dx},${cy + dy},${b.win}`);
      if (!bInA || !aInB) continue;
      let pairs = 0, seam = 0, seamMax = 0, own = 0, ownMax = 0, both = 0, same = 0;
      for (let k = 0; k < SIZE; k += BLOCK) {
        // a's block at the edge and b's facing it
        const [ax, ay, bx, by] = dx ? [SIZE - BLOCK, k, 0, k] : [k, SIZE - BLOCK, k, 0];
        const aa = blockMean(a.albedo, aInB, ax, ay), ab = blockMean(aInB, a.albedo, ax, ay);
        const bb = blockMean(b.albedo, bInA, bx, by), ba = blockMean(bInA, b.albedo, bx, by);
        if (aa == null || ab == null || bb == null || ba == null) continue;
        pairs++;
        const sStep = Math.abs(aa - bb), oStep = Math.max(Math.abs(aa - ba), Math.abs(ab - bb));
        seam += sStep; seamMax = Math.max(seamMax, sStep); own += oStep; ownMax = Math.max(ownMax, oStep);
      }
      for (let k = 0; k < SIZE; k++) for (let j = 0; j < BLOCK; j++) {
        const [x, y] = dx ? [j, k] : [k, j], i = (y * SIZE + x) * 4;
        if (b.albedo[i + 3] === 255 && bInA[i + 3] === 255) { both++; if (b.albedo[i] === bInA[i] && b.albedo[i + 1] === bInA[i + 1] && b.albedo[i + 2] === bInA[i + 2]) same++; }
        const [x2, y2] = dx ? [SIZE - 1 - j, k] : [k, SIZE - 1 - j], i2 = (y2 * SIZE + x2) * 4;
        if (a.albedo[i2 + 3] === 255 && aInB[i2 + 3] === 255) { both++; if (a.albedo[i2] === aInB[i2] && a.albedo[i2 + 1] === aInB[i2 + 1] && a.albedo[i2 + 2] === aInB[i2 + 2]) same++; }
      }
      if (!pairs) continue;
      out.push({ edge: `c${cx + dx}_${cy + dy} | c${cx}_${cy} (${edge} edge of c${cx}_${cy})`, pairs, both, same: same / Math.max(1, both), seamStep: seam / pairs, seamMax, ownStep: own / pairs, ownMax });
    }
  }
  return out;
}

// ---- main ------------------------------------------------------------------------------------------------------
function main() {
  const args = process.argv.slice(2), outDir = args.find((a) => !a.startsWith('--') && !/^-?\d/.test(a));
  const arg = (k) => (args.includes(k) ? args[args.indexOf(k) + 1] : null);
  if (!outDir) { console.error('usage: node tools/nmc-roads/bake-chunks.cjs <outDir> [--window <chunks>] [--only cx0,cy0,cx1,cy1] [--measure] [--verify-tips] [--albedo]'); process.exit(2); }
  if (arg('--window')) TUNABLES.windowChunks = +arg('--window');
  const only = arg('--only') ? arg('--only').split(',').map(Number) : null;
  const kit = loadKit(), { routes, sha256: networkSha256 } = readRoutes(kit.RK);
  const cov = coverage(routes), wins = partition(cov, only);
  const roadChunks = cov.chunk.reduce((a, v) => a + (v ? 1 : 0), 0);
  console.log(`network ${networkSha256.slice(0, 12)}: ${routes.length} routes; ${roadChunks} chunks within a route's reach; ${wins.length} windows of ${TUNABLES.windowChunks} chunks at most`);
  const albedo = args.includes('--albedo');
  fs.mkdirSync(albedo ? path.join(outDir, 'albedo') : outDir, { recursive: true });
  const rows = [], measure = args.includes('--measure'), owned = new Map(), aprons = new Map();
  for (let wi = 0; wi < wins.length; wi++) {
    const win = wins[wi], r = bakeWindow(kit, routes, win, { verifyTips: args.includes('--verify-tips'), apron: measure });
    if (measure) {
      for (const c of r.chunks) owned.set(`${c.cx},${c.cy}`, { win: wi, albedo: c.maps.albedo });
      for (const a of r.apron) aprons.set(`${a.cx},${a.cy},${wi}`, a.albedo);
    }
    for (const c of r.chunks) {
      if (only && !(c.cx >= only[0] && c.cy >= only[1] && c.cx < only[2] && c.cy < only[3])) continue;
      const name = `c${c.cx}_${c.cy}`, row = { name, cx: c.cx, cy: c.cy, covered: c.covered, faces: c.faces, blank: c.blank, palettes: c.palettes.flat(), heightMin: c.heightMin, heightMax: c.heightMax, heightRange: c.heightRange, heightMaxNear: c.heightMaxNear, pondMax: c.pondMax, sha256: {}, bytes: 0 };
      for (const [kind, a] of Object.entries(c.maps)) {
        const b = png(a, SIZE, SIZE); if (kind !== 'albedo') row.bytes += b.length;   // the streamed maps' bytes
        if (kind !== 'albedo' || albedo) fs.writeFileSync(kind === 'albedo' ? path.join(outDir, 'albedo', name + '.png') : path.join(outDir, name + '_' + kind + '.png'), b);
        row.sha256[kind] = sha(a);
      }
      rows.push(row);
    }
    console.log(`window ${win.join(',')} ${r.W}x${r.H}: stubs ${r.stubs}, palettes ${r.palettes}, chunks ${r.chunks.length}; build ${r.tBuild} ms, maps ${r.tBake} ms, chunks ${r.tChunks} ms; rss ${r.rssMB} MB, buffers ${r.abMB} MB`);
  }
  const out = {
    kit: 'Hidden Harbours road floor (road / path kit v4)', version: 1, driver: 'tools/nmc-roads/bake-chunks.cjs', rigSha256: kit.rigSha256,
    network: path.relative(ROOT, NETWORK).replace(/\\/g, '/'), networkSha256, seed: SEED, size: SIZE, metresPerChunk: CHUNK_M, origin: ORIGIN, grid: GRID, tunables: TUNABLES,
    rows: 'every sha256 is over raw RGBA, rows top-down as the PNG stores them',
    palettes: 'each chunk\'s palettes in its own order, five bands each, flat: palette p band b at p * 5 + b',
    role: { channel: 'light G', floor: 255, face: FACE, airFace: FACE_AIR, none: 0 },
    alpha: { albedo: '255 where the road covers the pixel (a preview, under albedo/)', normal: 'G.pond / pondMax * 255', light: 'chunk palette * 8 + band0', detail: 'bit d: the texel is its mark\'s tip toward tipDirections[d] and G.proud > ' + PROUD },
    tipDirections: TIP_DIRS.flat(), chunks: rows,
  };
  if (measure) {
    const sm = seams(owned, aprons).sort((a, b) => (b.seamMax - b.ownMax) - (a.seamMax - a.ownMax));
    const pairs = sm.reduce((a, e) => a + e.pairs, 0), pix = sm.reduce((a, e) => a + e.both, 0), same = sm.reduce((a, e) => a + e.same * e.both, 0);
    const mean = (k) => +(sm.reduce((a, e) => a + e[k] * e.pairs, 0) / Math.max(1, pairs)).toFixed(2), r2 = (v) => +v.toFixed(2);
    out.seams = { block: BLOCK, edges: sm.length, blockPairs: pairs, seamStepMean: mean('seamStep'), ownStepMean: mean('ownStep'), sameShare: pix ? +(same / pix).toFixed(4) : null,
      worst: sm.slice(0, 12).map((e) => ({ edge: e.edge, pairs: e.pairs, same: +e.same.toFixed(4), seamStep: r2(e.seamStep), seamMax: r2(e.seamMax), ownStep: r2(e.ownStep), ownMax: r2(e.ownMax) })) };
    console.log(`seams: ${sm.length} edges, ${pairs} block pairs; mean luma step at a seam ${out.seams.seamStepMean}, across the same line in one window ${out.seams.ownStepMean}; ${(100 * same / Math.max(1, pix)).toFixed(1)}% of the edge pixels the same bytes. The seams that step most over their own:`);
    for (const e of sm.slice(0, 8)) console.log(`  ${e.edge}: ${e.pairs} pairs, seam step ${e.seamStep.toFixed(1)} (max ${e.seamMax.toFixed(1)}), own ${e.ownStep.toFixed(1)} (max ${e.ownMax.toFixed(1)}), ${(100 * e.same).toFixed(1)}% same`);
  }
  fs.writeFileSync(path.join(outDir, 'RoadFloor.json'), JSON.stringify(out, null, 1) + '\n');
  console.log(`${rows.length} chunks, ${rows.length * 3} streamed maps, ${(rows.reduce((a, r) => a + r.bytes, 0) / 1048576).toFixed(1)} MB${albedo ? ' (and the albedo previews)' : ''} -> ${outDir}`);
}

if (require.main === module) main();
module.exports = { ORIGIN, CHUNK_M, GRID, PPM, SIZE, SEED, TUNABLES, TIP_DIRS, REACH, FACE, FACE_AIR, loadKit, readRoutes, coverage, partition, bakeWindow };
