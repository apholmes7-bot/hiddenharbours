/* Hidden Harbours — WEATHER SKY.  One sky for everything lit live: the tree rig (TreeRig4), the pixel
   terrain (PxLang tiles, relit by lightTile below), and — through toHarmony() — the harmony compositor.

     WeatherSky.at({ time, cloud, rain, fog, snow, wind, gust, dir })  ->  sky

     time    hours, 4.5–22.75. Seven keyframes (blue hour · dawn · morning · midday · afternoon ·
             golden hour · dusk) and a moon. Dawn, golden hour and dusk are harmony's scenarios in
             colour; the afternoon is the key every rig was authored against.
     cloud   0 clear – 1 overcast. Takes the sun out, lifts and greys the sky term, flattens the grade.
             Cast shadows go with the sun; the shade UNDER a canopy stays (it is sky occlusion).
     rain    0–1. Forces cloud ≥ 0.55 + 0.45·rain and wets every surface.
     fog     0–1. Haze from the hour's sky colour, heavier near the ground; dims the sun.
     snow    0–1 snow COVER on up-facing surfaces. Independent of cloud — a clear morning after a
             snowfall is the best light in the set.
     wind    0 calm – 1 gale · gust 0–1 · dir ±1 (screen x). Motion only; the rigs read it.

   THE SUN is a world vector [east, south, up] (az from north, clockwise). The path is the stage's,
   not the almanac's: it rises in the east, stands near the zenith before noon and sets west-north-
   west, so cast shadows fall toward the camera as harmony's always have. The afternoon keyframe
   (az 290°, el 55°) is the authored upper-left key: in a tree's view space it is [-0.54, -0.75, 0.38]
   against the authored [-0.55, -0.66, 0.52].

     sunW world · sunV sprite view space (x right, y down, z to camera) · sunF floor texture space
     (x east, y south, z up — what PxLang tile normals are in) · sunI / skyI direct and diffuse ·
     sunI0 the clear-sky sun, which sets exposure · expo · kc ac rc wash fogC · ka aa ra wa amb tgt
     (harmony's grade, same meaning) · shear [sx, sy] where a point's shadow lands per SCREEN px of
     height, for engines that skew a flat shadow sprite · wet fog snow · wind {w, gust, dir}

     WeatherSky.lightTile(tile, normals, sky, o)   relight a PxLang tile region (the floor);
                                                   o.level 0–3 = uniform shade (see TreeRig4.castShadow)
     WeatherSky.toHarmony(sky)                      a harmony LIGHTS entry for Harmony.relight
     WeatherSky.PRESETS                             named weathers                                     */
(function (root) {
  'use strict';
  const ELEV = 40, CE = Math.cos(ELEV * Math.PI / 180), SE = Math.sin(ELEV * Math.PI / 180), D2R = Math.PI / 180;
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const lerp = (a, b, t) => a + (b - a) * t;
  const smooth = (e0, e1, x) => { const t = clamp((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t); };
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const r2h = (r) => '#' + r.map(v => clamp(Math.round(v), 0, 255).toString(16).padStart(2, '0')).join('');
  const mix = (a, b, t) => { const A = h2r(a), B = h2r(b); return r2h([0, 1, 2].map(i => A[i] + (B[i] - A[i]) * t)); };
  const nrm = (v) => { const L = Math.hypot(v[0], v[1], v[2]) || 1; return [v[0] / L, v[1] / L, v[2] / L]; };
  const dirOf = (az, el) => { const c = Math.cos(el * D2R); return [Math.sin(az * D2R) * c, -Math.cos(az * D2R) * c, Math.sin(el * D2R)]; };
  const toView = (w) => [w[0], -w[2] * CE + w[1] * SE, w[2] * SE + w[1] * CE];
  function slerp(a, b, t) {
    const d = clamp(a[0] * b[0] + a[1] * b[1] + a[2] * b[2], -1, 1), th = Math.acos(d);
    if (th < 1e-4) return a.slice();
    const s = Math.sin(th), k0 = Math.sin((1 - t) * th) / s, k1 = Math.sin(t * th) / s;
    return nrm([a[0] * k0 + b[0] * k1, a[1] * k0 + b[1] * k1, a[2] * k0 + b[2] * k1]);
  }

  const KEYS = [
    { t: 4.50, name: 'Blue hour', az: 55, el: -8, sunI: 0.00, skyI: 0.42, kc: '#9aa9d6', ac: '#1f2a4c', ka: 0.10, aa: 0.55, tgt: 0.30, rc: '#8fa8d8', ra: 0.30, amb: 0.40, wash: '#2c3c6c', wa: 0.32, skyC: '#56689a' },
    { t: 5.67, name: 'Dawn', az: 70, el: 8, sunI: 0.80, skyI: 0.52, kc: '#f7b183', ac: '#2b3f66', ka: 0.30, aa: 0.42, tgt: 0.54, rc: '#8fb6d8', ra: 0.26, amb: 0.52, wash: '#3d4f80', wa: 0.20, skyC: '#d9a58f' },
    { t: 8.50, name: 'Morning', az: 95, el: 36, sunI: 1.00, skyI: 0.58, kc: '#ffe3bd', ac: '#22405a', ka: 0.20, aa: 0.32, tgt: 0.60, rc: '#b8d0e0', ra: 0.16, amb: 0.58, wash: '#ffe9c8', wa: 0.05, skyC: '#a9c3d6' },
    { t: 11.5, name: 'Midday', az: 30, el: 70, sunI: 1.00, skyI: 0.62, kc: '#fff6e0', ac: '#1d3b4a', ka: 0.12, aa: 0.28, tgt: 0.64, rc: '#c4dbe6', ra: 0.10, amb: 0.64, wash: '#fff8e8', wa: 0.02, skyC: '#b4cfe0' },
    { t: 14.0, name: 'Afternoon', az: 290, el: 55, sunI: 1.00, skyI: 0.60, kc: '#fff0cf', ac: '#1d3b4a', ka: 0.16, aa: 0.30, tgt: 0.62, rc: '#bcd6e2', ra: 0.14, amb: 0.62, wash: '#fff4dd', wa: 0.03, skyC: '#b0c9d8' },
    { t: 19.08, name: 'Golden hour', az: 282, el: 13, sunI: 0.94, skyI: 0.50, kc: '#ffae55', ac: '#3a3358', ka: 0.34, aa: 0.40, tgt: 0.56, rc: '#ffc98a', ra: 0.30, amb: 0.50, wash: '#ff9a44', wa: 0.17, skyC: '#e2a878' },
    { t: 21.25, name: 'Dusk', az: 298, el: 2, sunI: 0.34, skyI: 0.44, kc: '#d98a5a', ac: '#22304e', ka: 0.20, aa: 0.62, tgt: 0.38, rc: '#9fc0e8', ra: 0.42, amb: 0.44, wash: '#25355c', wa: 0.34, skyC: '#6d6f98' },
    { t: 22.75, name: 'Night', az: 125, el: 42, sunI: 0.22, skyI: 0.30, kc: '#a8bce0', ac: '#101a33', ka: 0.22, aa: 0.62, tgt: 0.34, rc: '#7f98c8', ra: 0.30, amb: 0.36, wash: '#16223f', wa: 0.46, skyC: '#27365a', moon: true },
  ];
  const hhmm = (t) => { const h = Math.floor(t), m = Math.round((t - h) * 60); return String(m === 60 ? h + 1 : h).padStart(2, '0') + ':' + String(m === 60 ? 0 : m).padStart(2, '0'); };

  function at(o) {
    o = o || {};
    const t = clamp(o.time == null ? 14 : +o.time, KEYS[0].t, KEYS[KEYS.length - 1].t);
    let i = 0; while (i < KEYS.length - 2 && t > KEYS[i + 1].t) i++;
    const A = KEYS[i], B = KEYS[i + 1], f = clamp((t - A.t) / (B.t - A.t), 0, 1), fs = f * f * (3 - 2 * f);
    const c = (k) => mix(A[k], B[k], fs), s = (k) => lerp(A[k], B[k], fs);
    let sunW, key = 'sun', sunI;
    if (B.moon) {        // the sun sets and fades, THEN the moon rises — never a key swinging across the sky
      const tm = (A.t + B.t) / 2;
      if (t < tm) { sunW = dirOf(A.az, lerp(A.el, -4, (t - A.t) / (tm - A.t))); sunI = A.sunI * (1 - smooth(A.t, tm, t)); }
      else { sunW = dirOf(B.az, B.el); key = 'moon'; sunI = B.sunI * smooth(tm, B.t, t); }
    } else { sunW = slerp(dirOf(A.az, A.el), dirOf(B.az, B.el), f); sunI = s('sunI'); }
    const el = Math.asin(clamp(sunW[2], -1, 1)) / D2R;
    if (key === 'sun') sunI *= smooth(-1.5, 3, el);
    const sunI0 = sunI;
    const rain = clamp(+o.rain || 0, 0, 1), fog = clamp(+o.fog || 0, 0, 1), snow = clamp(+o.snow || 0, 0, 1);
    const cloud = Math.max(clamp(+o.cloud || 0, 0, 1), rain > 0 ? 0.55 + 0.45 * rain : 0), cd = Math.pow(cloud, 1.15);
    sunI *= (1 - 0.9 * cd) * (1 - 0.55 * fog);
    const skyI = s('skyI') * (1 + 0.45 * cloud) * (1 - 0.15 * rain);
    const grey = key === 'moon' || t > 21.6 || t < 5 ? '#3a4660' : '#8e9aa2';
    const kc = mix(c('kc'), '#e2e6e6', 0.65 * cd), ac = mix(c('ac'), mix(grey, c('ac'), 0.5), 0.7 * cd), wash = mix(c('wash'), grey, 0.5 * cd);
    const skyC = c('skyC'), fogC = mix(mix(skyC, '#c3cdce', 0.55), grey, 0.3 * cd);
    let name = (f < 0.5 ? A : B).name;
    if (cloud > 0.6) name += rain > 0.3 ? ' · rain' : ' · overcast';
    if (fog > 0.5) name += ' · fog';
    if (snow > 0.4) name += ' · snow';
    let shear = [0, 0];
    if (sunW[2] > 0.02) { const k = 1 / Math.max(sunW[2], 0.14); shear = [-sunW[0] * k / CE, -sunW[1] * k * SE / CE]; }
    return {
      time: t, clock: hhmm(t), name, key, az: (Math.atan2(sunW[0], -sunW[1]) / D2R + 360) % 360, el,
      sunW, sunV: toView(sunW), sunF: sunW.slice(), sunI, sunI0, skyI, expo: 1 + 0.30 * cd + 0.15 * fog,
      kc, ac, ka: s('ka') * (1 - 0.6 * cd), aa: s('aa') * (1 - 0.25 * cd), rc: c('rc'), ra: s('ra') * (1 - 0.7 * cd), amb: s('amb'),
      wash, wa: Math.max(s('wa'), 0.14 * cd + 0.10 * rain), tgt: lerp(s('tgt'), 0.46, cd) * (1 - 0.1 * rain),
      skyC, fogC, cloud, rain, wet: Math.max(rain, clamp(+o.wet || 0, 0, 1)), fog, snow, shear,
      wind: { w: clamp(+o.wind || 0, 0, 1), gust: o.gust == null ? 0.4 : clamp(+o.gust, 0, 1), dir: o.dir < 0 ? -1 : 1 },
    };
  }

  function toHarmony(sky) {
    const L = sky.sunI > 0.02 ? sky.sunF : [0, 0, 1];
    return { key: 'weather', name: sky.name, time: sky.clock, note: '', L, sx: sky.shear[0], sy: sky.shear[1],
      kc: sky.kc, ac: sky.ac, ka: sky.ka, aa: sky.aa, tgt: sky.tgt, rim: nrm([-L[0] * 0.6, -0.3, -0.75]), rc: sky.rc, ra: sky.ra,
      amb: sky.amb, wash: sky.wash, wa: sky.wa };
  }

  // ---- the floor, relit ------------------------------------------------------------------------
  /* harmony's floor mapping — N·L quantised to five steps, one band step up or down, then the grade —
     with the weather in it: a shade level takes the sun out (3) or half of it (2) and one level of
     the sky (1+); snow cover takes up-facing texels over to a snow ramp that is banded by the same
     light, so the snow in a tree's shadow goes blue; wet darkens; fog pulls to the haze. One flat
     result per (palette, band, step, snow) — the floor stays a finite palette. */
  const SNOWR = ['#5c7180', '#7d93a0', '#a8bcc4', '#cfdde1', '#eef4f4'].map(h2r);
  function vh(x, y, s) { let h = (Math.imul(x | 0, 374761393) + Math.imul(y | 0, 668265263) + Math.imul(s | 0, 1442695041)) | 0; h ^= h >>> 13; h = Math.imul(h, 1274126177) | 0; h ^= h >>> 16; return (h >>> 0) / 4294967296; }
  function vn(x, y, s) {
    const xi = Math.floor(x), yi = Math.floor(y), fx = x - xi, fy = y - yi, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
    const a = vh(xi, yi, s), b = vh(xi + 1, yi, s), c = vh(xi, yi + 1, s), d = vh(xi + 1, yi + 1, s);
    return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
  }
  function lightTile(t, nr, sky, o) {
    o = o || {};
    const X0 = o.x0 | 0, Y0 = o.y0 | 0, W = o.w || t.n, H = o.h || t.m, lvl = o.level | 0;
    const out = o.out || new Uint8ClampedArray(W * H * 4);
    const L = sky.sunF, kc = h2r(sky.kc), ac = h2r(sky.ac), wc = h2r(sky.wash), fc = h2r(sky.fogC);
    const skyFlat = sky.skyI * 0.36 * sky.expo;
    const sun0 = (sky.sunI0 == null ? sky.sunI : sky.sunI0) * Math.max(0.02, L[2]);
    const ex = sun0 > 0.015 ? Math.max(0.3, (sky.tgt - skyFlat) / sun0) : 0;
    const sunK = lvl >= 3 ? 0 : lvl === 2 ? 0.45 : 1, skyK = lvl >= 1 ? 0.78 : 1;
    const snow = sky.snow || 0, wet = sky.wet || 0, fog = sky.fog || 0, cache = new Map();
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const tx = X0 + x, ty = Y0 + y, i = t.i(tx, ty);
      const nl = sky.sunI * Math.max(0, nr.nx[i] * L[0] + nr.ny[i] * L[1] + nr.nz[i] * L[2]) * ex * sunK + skyFlat * (0.55 + 0.45 * nr.nz[i]) * skyK;
      const q = Math.round(clamp(nl, 0, 1) * 4);
      let sn = 0;
      if (snow > 0.02) { const f = 0.62 * vn(tx / 11, ty / 8, 17) + 0.38 * vn(tx / 2.5, ty / 2.5, 29); if (f < snow * 1.18 - 0.1) sn = 1; }
      const ck = ((t.pal[i] * 5 + t.band[i]) * 5 + q) * 2 + sn;
      let c = cache.get(ck);
      if (!c) {
        const db = q >= 4 ? 1 : q >= 2 ? 0 : q >= 1 ? -1 : -2;
        const base = sn ? SNOWR[clamp(2 + db, 0, 4)] : t.pals[t.pal[i]][clamp(t.band[i] + db, 0, 4)];
        const r = base.slice(), u = q / 4, ta = sky.aa * (1 - u) * (1 - sky.amb * 0.35), tk = sky.ka * u;
        for (let k = 0; k < 3; k++) {
          r[k] += (ac[k] - r[k]) * ta; r[k] += (kc[k] - r[k]) * tk;
          if (wet && !sn) r[k] *= 1 - (k === 2 ? 0.10 : 0.15) * wet;
          if (sky.wa) r[k] += (wc[k] - r[k]) * sky.wa;
          if (fog) r[k] += (fc[k] - r[k]) * fog * 0.5;
        }
        c = r.map(v => clamp(Math.round(v), 0, 255)); cache.set(ck, c);
      }
      const o4 = (y * W + x) * 4; out[o4] = c[0]; out[o4 + 1] = c[1]; out[o4 + 2] = c[2]; out[o4 + 3] = 255;
    }
    return out;
  }

  const PRESETS = [
    { key: 'afternoon', name: 'Clear afternoon', time: 14, cloud: 0, rain: 0, fog: 0, snow: 0, wind: 0.22, gust: 0.3 },
    { key: 'dawn', name: 'Dawn mist', time: 5.95, cloud: 0.1, rain: 0, fog: 0.4, snow: 0, wind: 0.06, gust: 0.2 },
    { key: 'morning', name: 'Morning', time: 8.6, cloud: 0.05, rain: 0, fog: 0, snow: 0, wind: 0.3, gust: 0.4 },
    { key: 'golden', name: 'Golden hour', time: 19.1, cloud: 0.05, rain: 0, fog: 0, snow: 0, wind: 0.18, gust: 0.3 },
    { key: 'overcast', name: 'Overcast', time: 12.5, cloud: 1, rain: 0, fog: 0, snow: 0, wind: 0.35, gust: 0.4 },
    { key: 'squall', name: 'Squall', time: 16, cloud: 1, rain: 0.9, fog: 0.1, snow: 0, wind: 0.85, gust: 0.9 },
    { key: 'fog', name: 'Fog bank', time: 9.5, cloud: 0.5, rain: 0, fog: 0.9, snow: 0, wind: 0.04, gust: 0.1 },
    { key: 'snow', name: 'After snowfall', time: 10, cloud: 0.1, rain: 0, fog: 0, snow: 0.85, wind: 0.08, gust: 0.2 },
    { key: 'dusk', name: 'Dusk', time: 21.2, cloud: 0.15, rain: 0, fog: 0.1, snow: 0, wind: 0.12, gust: 0.3 },
    { key: 'night', name: 'Moonlight', time: 22.75, cloud: 0.05, rain: 0, fog: 0, snow: 0, wind: 0.1, gust: 0.3 },
    { key: 'gale', name: 'Gale', time: 13, cloud: 0.55, rain: 0, fog: 0, snow: 0, wind: 1, gust: 1 },
  ];

  root.WeatherSky = { ELEV, CE, SE, KEYS, PRESETS, at, toHarmony, lightTile, toView, dirOf, slerp, mix, h2r, r2h, hhmm };
})(typeof globalThis !== 'undefined' ? globalThis : window);
