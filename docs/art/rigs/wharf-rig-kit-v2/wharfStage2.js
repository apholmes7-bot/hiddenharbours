/* Hidden Harbours — WHARF STAGE 2 (globalThis.WharfStage2): a harbour or one module on the pixel terrain and the water
   shader, lit by one WeatherSky, with character v9.2 figures working its fixtures. The viewer's compositor, and the
   reference for the engine's: nothing here is baked into the rig.

   The floor is a PLAN tile (32 px/m across, 32·sin40 px/m down) painted by PxKit8.floor from the bed's height and lit
   by TerrainLight6 with the tide, the swash and its foam lace, the waves on the 16-frame loop and the structure's shade
   (WharfRig2.shadeAt at the surface). Each screen column then takes, row by row, the first surface its ray meets —
   the water at the tide or the bed above it — so the water sits at the height it is at and the rig's waterline, its
   contact() mask and its shadow all land where the water is. Structure pixels under the tide take the water over them
   through three steps (0.04 · 0.09 · 0.16 m, the shallows rule); under the bed they are the ground's.
   Needs PxLang, PxKit, PxKit8, TerrainLight6, WeatherSky, WharfRig2 (+Kit, Verbs); CharacterIso9 for figures.           */
(function (root) {
  'use strict';
  const PPU = 32, D2R = Math.PI / 180, SE = Math.sin(40 * D2R), CE = Math.cos(40 * D2R), KZ = PPU * CE;
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const ZONES = ['grass', 'path', 'shingle', 'sand', 'foreshore', 'ripple', 'silt'], FLOORS = new Map();
  function zoneOf(e, R, X, Y, road) {
    if (e >= R + 0.75) return road && road(X, Y) ? 'path' : 'grass';
    if (e >= R + 0.20) return 'shingle'; if (e >= R * 0.62) return 'sand'; if (e >= 0.05) return 'foreshore'; if (e >= -0.9) return 'ripple'; return 'silt';
  }
  // a scene: which model, the camera, the canvas, the bed and the fetch
  function scene(key, o) {
    o = o || {}; const W = root.WharfRig2, harbour = /^harbour:/.test(key), dir = harbour ? 0 : (o.dir | 0);
    const mo = Object.assign({}, o.model || {}), fr0 = W.frame(key, Object.assign({}, mo, { dir, tide: 0, frame: 0 })), mdl = fr0.mdl, C = fr0.C;
    let ox, oy, Wc, Hc, bed, fetch, road = null;
    if (harbour) { const H = mdl.harbour, [x0, y0, x1, y1] = H.extent;
      ox = Math.max(fr0.px, Math.round(-x0 * PPU)) + 4; oy = Math.max(fr0.py, Math.round((y1 * SE + 1.2 * CE) * PPU)) + 4;
      Wc = Math.max(ox + Math.round(x1 * PPU), ox - fr0.px + fr0.w) + 4; Hc = Math.max(oy + Math.round(-y0 * SE * PPU), oy - fr0.py + fr0.h) + 4;
      bed = (x, y) => H.bed(x, y); fetch = H.fetch || ((x, y) => clamp(0.35 + (y - 8) / 18, 0.3, 1)); road = H.road !== undefined ? H.road : (x, y) => Math.abs(x + 4) < 2.2 && y < -1.5;
    } else { const M = 56; ox = fr0.px + M; oy = fr0.py + M; Wc = fr0.w + 2 * M; Hc = fr0.h + 2 * M; bed = (x, y) => mdl.s.bedAt(x, y); fetch = () => 0.8; }
    return { key, harbour, dir, mo, C, mdl, ox, oy, Wc, Hc, bed, fetch, road, R: mdl.tideRange };
  }

  class Stage {
    constructor(key, o) {
      const t0 = Date.now(), S = this.S = scene(key, o), W = root.WharfRig2, TL = root.TerrainLight6, K8 = root.PxKit8;
      const zTop = Math.max(S.R + 0.6, 3.2), PH = this.PH = S.Hc + Math.ceil(zTop * KZ) + 2, Wc = S.Wc, N = Wc * PH, fk = [S.key, S.dir, Wc, PH, S.ox, S.oy, S.R].join('|'), hit = FLOORS.get(fk);
      if (hit) { Object.assign(this, hit); this.cache = new Map(); this.figs = []; this.buildMs = Date.now() - t0; this.floorCached = true; return; }
      const PX = this.PX = new Float32Array(N), PY = this.PY = new Float32Array(N), E = this.E = new Float32Array(N), Z = new Uint8Array(N), fe = new Float32Array(N);
      for (let py = 0; py < PH; py++) for (let x = 0; x < Wc; x++) { const i = py * Wc + x, p = W.unproj(x + 0.5 - S.ox, py + 0.5 - S.oy, 0, S.C);
        PX[i] = p[0]; PY[i] = p[1]; E[i] = S.bed(p[0], p[1]); fe[i] = S.fetch(p[0], p[1]); Z[i] = ZONES.indexOf(zoneOf(E[i], S.R, p[0], p[1], S.road)); }
      const seen = ZONES.filter((k, j) => { for (let i = 0; i < N; i += 7) if (Z[i] === j) return true; return false; });
      const defs = seen.map((k) => { const j = ZONES.indexOf(k); return { key: k, step: 1, seed: 7 + j * 31, R: (x, y) => x >= 0 && y >= 0 && x < Wc && y < PH && Z[y * Wc + x] === j }; });
      const fl = K8.floor(Wc, PH, defs, 5), tile = fl.tile || fl, elev = new Float32Array(N);
      for (let i = 0; i < N; i++) elev[i] = E[i] + (tile.h ? tile.h[i] * 0.01 : 0);
      this.tile = tile; this.G = TL.gbuf(tile, { relief: 1.2, wrap: false, elev, far: (y) => clamp(1 - y / PH, 0, 1), fetch: fe });
      FLOORS.set(fk, { PX, PY, E, tile, G: this.G, PH }); if (FLOORS.size > 6) FLOORS.delete(FLOORS.keys().next().value);
      this.cache = new Map(); this.figs = []; this.buildMs = Date.now() - t0;
    }
    get W() { return this.S.Wc; } get H() { return this.S.Hc; }
    frameOf(fi) { const s = this.st; return root.WharfRig2.frame(this.S.key, Object.assign({}, this.S.mo, { dir: this.S.dir, tide: s.tide, frame: fi, wind: this.sky.wind, sea: s.sea || null })); }
    /* land: a new sky, tide or channel. The shade the structure throws, the floor lit once, the row map for this tide */
    land(sky, st) {
      const t0 = Date.now(), S = this.S, W = root.WharfRig2, Wc = S.Wc, PH = this.PH, N = Wc * PH, T = st.tide;
      this.sky = sky; this.st = Object.assign({}, st); this.cache.clear();
      const fr = this.frameOf(0), lv = new Uint8Array(N), lit = st.ch === 'lit';
      if (lit && (sky.sunI > 0.02 || sky.skyI > 0)) { const x0 = S.ox - fr.px - 60, x1 = S.ox - fr.px + fr.w + 60;
        for (let py = 0; py < PH; py++) for (let x = Math.max(0, x0); x < Math.min(Wc, x1); x++) { const i = py * Wc + x, hs = Math.max(this.E[i], T); lv[i] = W.shadeAt(fr, sky, this.PX[i], this.PY[i], hs); } }
      this.lv = lv;
      const map = this.map = new Int32Array(Wc * S.Hc).fill(-1);                     // screen pixel → plan texel: the first surface on the ray
      for (let x = 0; x < Wc; x++) { let last = -1;
        for (let py = 0; py < PH; py++) { const i = py * Wc + x, sy = Math.round(py - Math.max(this.E[i], T) * KZ);
          if (sy <= last) continue; for (let y = Math.max(0, last + 1); y <= Math.min(S.Hc - 1, sy); y++) map[y * Wc + x] = i; last = sy; if (last >= S.Hc - 1) break; } }
      this.lo = { frame: 0, tide: T, tideHigh: T + 0.35, lv, seaDir: 1 };
      const TL = root.TerrainLight6;
      this.plan = lit ? TL.relight(this.G, sky, this.lo) : TL.view(this.G, st.ch === 'unlit' ? 'unlit' : ({ normal: 'normal', ao: 'ao', sun: 'sun', snow: 'snow', height: 'height' }[st.ch] || 'unlit'), sky, Object.assign({}, this.lo, { tide: st.ch === 'unlit' ? null : T }));
      this.dyn = lit ? TL.dynamic(this.G, sky, { tide: T }) : null; this.landMs = Date.now() - t0;
    }
    /* one frame of the loop: the water's moving texels relit, the floats rocked, the structure laid in, the foam lace at
       its waterline; then the figures on their own clocks (tMs). Composites are cached per frame; figures are not. */
    base(fi) {
      let B = this.cache.get(fi); if (B) return B;
      const S = this.S, Wc = S.Wc, Hc = S.Hc, st = this.st, sky = this.sky, W = root.WharfRig2, TL = root.TerrainLight6, lit = st.ch === 'lit';
      let plan = this.plan;
      if (lit && this.dyn && this.dyn.length && fi) { plan = this.plan.slice(); TL.relight(this.G, sky, Object.assign({}, this.lo, { frame: fi, only: this.dyn, out: plan })); }
      const out = new Uint8ClampedArray(Wc * Hc * 4), map = this.map;
      for (let i = 0; i < Wc * Hc; i++) { const j = map[i]; if (j < 0) continue; const o4 = i * 4, p4 = j * 4; out[o4] = plan[p4]; out[o4 + 1] = plan[p4 + 1]; out[o4 + 2] = plan[p4 + 2]; out[o4 + 3] = 255; }
      const fr = this.frameOf(fi), ch = st.ch, px = lit ? W.relight(fr, sky, { outline: !!st.outline }) : W.view(fr, ch === 'facets' ? 'parts' : ch, sky), gb = fr.gb, T = fr.tide, dx = S.ox - fr.px, dy = S.oy - fr.py;
      for (let y = 0; y < fr.h; y++) { const Y = y + dy; if (Y < 0 || Y >= Hc) continue;
        for (let x = 0; x < fr.w; x++) { const i = y * fr.w + x; if (!gb.a[i]) continue; const X = x + dx; if (X < 0 || X >= Wc) continue;
          const z = gb.Z[i]; if (z < S.bed(gb.X[i], gb.Y[i]) - 0.03) continue;
          let f = 0; if (lit) { const d = T - z; if (d > 0) f = d < 0.04 ? 0.3 : d < 0.09 ? 0.5 : d < 0.16 ? 0.7 : 1; } if (f >= 1) continue;
          const o4 = (Y * Wc + X) * 4, s4 = i * 4; for (let k = 0; k < 3; k++) out[o4 + k] = px[s4 + k] + (out[o4 + k] - px[s4 + k]) * f; out[o4 + 3] = 255; } }
      if (lit) { const m = W.contact(fr), FR = TL.FOAMR, ph = 2 * Math.PI * fi / 16, E = this.E;
        for (let y = 0; y < fr.h; y++) for (let x = 0; x < fr.w; x++) { const v = m[y * fr.w + x]; if (!v) continue; const X = x + dx, Y = y + dy; if (X < 0 || Y < 0 || X >= Wc || Y >= Hc) continue;
          const pj = map[Y * Wc + X]; if (pj < 0 || E[pj] >= T) continue;
          const g = TL.vn(X / 3.1 + 1.4 * Math.cos(ph), Y / 1.7 + 1.4 * Math.sin(ph), 61); if (g < (v === 2 ? 0.34 : 0.58)) continue;
          const c = FR[v === 2 ? (g > 0.7 ? 4 : 3) : 2], o4 = (Y * Wc + X) * 4; for (let k = 0; k < 3; k++) out[o4 + k] = c[k] * 0.68 + out[o4 + k] * 0.32; } }
      B = { out, fr, dx, dy }; this.cache.set(fi, B); if (this.cache.size > 16) this.cache.delete(this.cache.keys().next().value);
      return B;
    }
    frame(fi, tMs) {
      const B = this.base(fi), ch = this.st.ch;
      if (!this.figs.length || !root.CharacterIso9 || !(ch === 'lit' || ch === 'unlit')) return B.out;
      const out = B.out.slice(); for (const F of this.figs) this.blitFig(out, F, B, tMs || 0); return out;
    }
    /* a figure: {build, clip, opts, at:[x,y,z] model (a float figure: x, y on the group, z ignored), grp, yaw (model, 0 = +y),
       move:{d:[dx,dy,dz], len, rate} root motion that wraps} — drawn behind any structure pixel nearer than its root by 0.12 m */
    blitFig(out, F, B, tMs) {
      const CI = root.CharacterIso9, W = root.WharfRig2, S = this.S, fr = B.fr, sky = this.sky, cd = CI.clipDef(F.clip); if (!cd) return;
      const A = CI.ANIMS[cd.anim], k0 = Math.floor(tMs / A.ms) % A.frames, k = F.reverse ? A.frames - 1 - k0 : k0;
      let p = F.at.slice(), roll = 0, pitch = 0;
      if (F.move) { const u = ((tMs / 1000) * F.move.rate / F.move.len + (F.phase || 0)) % 1; for (let a = 0; a < 3; a++) p[a] += F.move.d[a] * u * F.move.len; }
      if (F.grp != null && fr.poses[F.grp]) { p = fr.poses[F.grp].fwd([p[0], p[1], fr.mdl.groups[F.grp].fb]); const r = W.rockFor(fr, F.grp, F.yaw); roll = r.roll; pitch = r.pitch; }
      const yaw = Math.round(((F.yaw - S.dir * 45) % 360 + 360) % 360), key = F.build + '|' + F.clip + '|' + k + '|' + yaw + '|' + roll.toFixed(1) + '|' + pitch.toFixed(1) + '|' + JSON.stringify(F.opts || {});
      this.figCache = this.figCache || new Map(); let R = this.figCache.get(key);
      if (!R) { R = CI.render({ clip: F.clip, frame: k, yaw, build: F.build, opts: F.opts || {}, roll, pitch }); this.figCache.set(key, R); if (this.figCache.size > 900) this.figCache.delete(this.figCache.keys().next().value); }
      const q = W.proj(p, fr.C), X0 = Math.round(S.ox + q[0]) - 32, Y0 = Math.round(S.oy + q[1]) - 82, fd = q[2], gb = fr.gb, T = fr.tide, Wc = S.Wc, Hc = S.Hc;
      const gl = clamp(0.3 + 0.95 * (0.6 * (sky.sunI || 0) * Math.max(0, sky.sunW ? sky.sunW[2] : 0.5) + 0.5 * (sky.skyI == null ? 0.6 : sky.skyI)) * (sky.expo || 1), 0.3, 1);
      for (let y = 0; y < R.H; y++) for (let x = 0; x < R.W; x++) { const a = (y * R.W + x) * 4; if (!R.rgba[a + 3]) continue; const X = X0 + x, Y = Y0 + y; if (X < 0 || Y < 0 || X >= Wc || Y >= Hc) continue;
        const cx = X - B.dx, cy = Y - B.dy; if (cx >= 0 && cy >= 0 && cx < fr.w && cy < fr.h) { const i = cy * fr.w + cx; if (gb.a[i] && gb.d[i] < fd - 0.12) continue; }
        const zp = p[2] + (82 - y) / KZ, d = T - zp, f = d > 0 ? (d < 0.04 ? 0.3 : d < 0.09 ? 0.5 : d < 0.16 ? 0.7 : 1) : 0; if (f >= 1) continue;
        const o4 = (Y * Wc + X) * 4; for (let c = 0; c < 3; c++) { const v = R.rgba[a + c] * gl; out[o4 + c] = v + (out[o4 + c] - v) * f; } }
    }
  }
  /* a cast that works the harbour's verbs, one figure per verb, straight from the gameplay sidecar */
  function cast(gp, mdl, tide) {
    const F = [], B = ['fisher', 'ginny', 'skipper', 'deckboss', 'packer', 'hand', 'cutter', 'nan'], V = gp.VERBS; let b = 0;
    const one = (verb, pred) => V.find(v => v.verb === verb && (!pred || pred(v))), grp = (v) => typeof v.rides === 'number' ? v.rides : null, add = (o) => F.push(Object.assign({ build: B[b++ % B.length] }, o));
    let v;
    if ((v = one('gut'))) add({ clip: 'chop', opts: v.seq[0].opts, at: v.stand, yaw: v.yaw, grp: grp(v), verb: 'gut' });
    if ((v = one('haulTrap'))) add({ clip: 'hauler', opts: v.seq[0].opts, at: v.stand, yaw: v.yaw, grp: grp(v), verb: 'haulTrap' });
    if ((v = one('fish'))) add({ clip: 'reel', at: v.stand, yaw: v.yaw, grp: grp(v), verb: 'fish' });
    if ((v = one('stow'))) add({ clip: 'place', opts: v.seq[0].opts, at: v.stand, yaw: v.yaw, grp: grp(v), verb: 'stow' });
    if ((v = one('throwRing'))) add({ clip: 'toss', opts: { workZ: 0.93 }, at: v.stand, yaw: v.yaw, grp: grp(v), verb: 'throwRing' });
    if ((v = one('tieUp', (q) => q.rides != null))) add({ clip: 'haul', at: v.stand, yaw: v.yaw, grp: grp(v), verb: 'tieUp' });
    const L = gp.CLIMB.find(c => c.kind === 'wharf ladder' && c.rungZ.filter(z => z > tide + 0.3).length >= 5);
    if (L) { const top = L.topZ - 0.72, bot = Math.max(L.bottomRungZ, Math.ceil((tide + 0.25 - L.bottomRungZ) / L.rung) * L.rung + L.bottomRungZ);
      if (top - bot > 0.3) add({ clip: 'ladderDown', reverse: true, opts: L.opts, at: [L.root.x, L.root.y, bot], yaw: L.faceYaw, move: { d: [0, 0, 1], len: top - bot, rate: 2 * L.rung / 1.1 }, verb: 'climbUp' }); }
    const S = gp.CLIMB.find(c => c.kind === 'swim ladder'); if (S) add({ clip: 'tread', at: [S.root.x, S.root.y, tide - 1.15], yaw: S.faceYaw, verb: 'swimOut' });
    const gw = mdl.gangs && mdl.gangs[0], Wk = gp.WALK.find(w => w.kind === 'gangway');
    if (gw && Wk) { const h = gw.hinge, t = Wk.toe, d = [t[0] - h[0], t[1] - h[1], t[2] - h[2]], len = Math.hypot(d[0], d[1], d[2]);
      add({ clip: 'walk', at: h.slice(), yaw: Math.atan2(d[0], d[1]) / D2R, move: { d: d.map(q => q / len), len, rate: 0.727 }, verb: 'walkGangway' }); }
    return F;
  }
  root.WharfStage2 = { Stage, scene, zoneOf, cast, ZONES, KZ };
})(typeof globalThis !== 'undefined' ? globalThis : window);
