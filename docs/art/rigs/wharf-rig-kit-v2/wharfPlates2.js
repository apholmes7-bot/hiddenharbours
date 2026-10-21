/* Hidden Harbours — WHARF PLATES 2 (globalThis.WharfPlates2): the viewer's drawn plates. The gameplay overlay over a stage,
   and the berth plans to scale (plan view, metres), straight from WharfRig2Kit.gameplay() and WharfGeo2.berthPlan().      */
(function (root) {
  'use strict';
  const CLS = { A: '#8fd6a0', B: '#c9a86a', C: '#d9a27a', D: '#9fb8d6', E: '#d98aa8' };
  function overlay(ctx, stage, gp, z) {
    const S = stage.S, W = root.WharfRig2, fr = stage.base ? stage.frameOf(0) : null; if (!fr || !gp) return;
    const P = (p) => { const q = W.proj(p, fr.C); return [(S.ox + q[0]) * z, (S.oy + q[1]) * z]; }, T = fr.tide;
    ctx.save(); ctx.lineWidth = 1; ctx.font = (z >= 2 ? 10 : 9) + 'px ui-monospace,Menlo,monospace'; ctx.textBaseline = 'top';
    const poly = (pts, zz, col, dash) => { ctx.strokeStyle = col; ctx.setLineDash(dash || []); ctx.beginPath(); pts.forEach((p, i) => { const q = P([p[0], p[1], zz]); i ? ctx.lineTo(q[0] + 0.5, q[1] + 0.5) : ctx.moveTo(q[0] + 0.5, q[1] + 0.5); }); ctx.closePath(); ctx.stroke(); };
    const dot = (p, r, col) => { const q = P(p); ctx.fillStyle = col; ctx.fillRect(Math.round(q[0] - r), Math.round(q[1] - r), 2 * r + 1, 2 * r + 1); return q; };
    const label = (q, t, col) => { const w = ctx.measureText(t).width + 6; ctx.fillStyle = 'rgba(11,20,24,0.82)'; ctx.fillRect(q[0], q[1], w, z >= 2 ? 13 : 12); ctx.fillStyle = col; ctx.fillText(t, q[0] + 3, q[1] + 1); };
    for (const b of gp.BERTHS) { const c = b.footprint.centre, h = b.footprint.heading, L = b.loa / 2, B = b.beam / 2, n = [-h[1], h[0]];
      const pts = [[c[0] - h[0] * L - n[0] * B, c[1] - h[1] * L - n[1] * B], [c[0] + h[0] * L - n[0] * B, c[1] + h[1] * L - n[1] * B], [c[0] + h[0] * L + n[0] * B, c[1] + h[1] * L + n[1] * B], [c[0] - h[0] * L + n[0] * B, c[1] - h[1] * L + n[1] * B]];
      poly(pts, T, CLS[b.cls] || '#fff', [4, 3]); label(P([c[0], c[1], T]), b.label + ' \u00b7 ' + b.cls, CLS[b.cls] || '#fff'); }
    for (const w of gp.WALK) { if (!w.poly) continue; poly(w.poly, w.z != null ? w.z : (w.z0 != null ? (w.z0 + w.z1) / 2 : T), w.walkable === false ? '#6b807c' : w.rides != null ? '#7fd0c0' : '#8fd6a0', w.rides != null ? [2, 2] : null); }
    for (const h of gp.HOLES) poly(h.poly, 0, '#e0604c');
    for (const b of gp.BLOCKERS) { if (b.shape === 'circle') dot([b.x, b.y, 3], 1, '#d9a27a'); else if (b.shape === 'poly') { const f = (gp.FIXTURES || []).find(q => q.footprint === b.poly); poly(b.poly, f ? f.stand[2] : 3, '#7fc7d9'); } }
    for (const c of gp.CLIMB) { const a = P([c.plane[0], c.plane[1], c.topZ + 1.0]), b2 = P([c.plane[0], c.plane[1], c.bottomRungZ]); ctx.setLineDash([]); ctx.strokeStyle = '#f0a050'; ctx.beginPath(); ctx.moveTo(a[0] + 0.5, a[1]); ctx.lineTo(b2[0] + 0.5, b2[1]); ctx.stroke();
      for (const r of c.rungZ) { const q = P([c.plane[0], c.plane[1], r]); ctx.fillStyle = r > T ? '#f0a050' : '#6d8a90'; ctx.fillRect(Math.round(q[0]) - 2, Math.round(q[1]), 5, 1); } dot([c.root.x, c.root.y, c.topZ - 0.72], 1, '#f0a050'); }
    for (const t of gp.TIE) { const q = dot(t.at, 1, '#e8d36a'), s = P(t.stand); ctx.strokeStyle = '#e8d36a'; ctx.setLineDash([]); ctx.beginPath(); ctx.moveTo(s[0], s[1]); ctx.lineTo(q[0], q[1]); ctx.stroke(); }
    for (const f of (gp.FIXTURES || [])) { const q = dot(f.stand, 1, '#7fc7d9'); if (z >= 2) label([q[0] + 4, q[1] - 14], f.kind, '#bfe6ef'); }
    ctx.restore();
  }
  /* rows: [{title, faceLen, x0, list, utilisation}]. Each face at its own scale, the largest that fits the face in the width
     and keeps its widest hull under 40 px, with a scale bar: the face is the line, each berth its slot (dashed: the hull and
     its end clearances) and the hull to scale on it, bow to +x. A label goes inside the hull, else under its slot, else none. */
  const LAB = 176, PADX = 14, RIGHT = 100;
  function plateLayout(W, rows) { const avail = W - LAB - PADX - RIGHT;
    return rows.map(r => { const mb = Math.max(1.5, ...r.list.map(b => b.beam)), k = Math.min(avail / r.faceLen, 40 / mb), hh = Math.max(6, mb * k); return { k, hh, h: hh + 46 }; }); }
  function plateHeight(W, rows) { return 12 + plateLayout(W, rows).reduce((a, l) => a + l.h, 0); }
  const niceBar = (k) => [1, 2, 5, 10, 20, 50, 100].find(m => m * k >= 34) || 100;
  function berthPlate(ctx, W, rows) {
    const Ls = plateLayout(W, rows); let y = 12; ctx.font = '10px ui-monospace,Menlo,monospace'; ctx.textBaseline = 'middle'; ctx.setLineDash([]);
    rows.forEach((r, ri) => { const { k, hh, h } = Ls[ri], x0 = LAB, base = y + hh + 8, fw = r.faceLen * k, bar = niceBar(k);
      ctx.fillStyle = '#cfd6cc'; ctx.fillText(r.title, PADX, base - 8);
      ctx.fillStyle = '#4c6460'; ctx.fillRect(PADX, base + 6, Math.round(bar * k), 2); ctx.fillStyle = '#6b807c'; ctx.fillText(bar + ' m', PADX + Math.round(bar * k) + 5, base + 7);
      ctx.fillStyle = '#35504c'; ctx.fillRect(x0, base, Math.round(fw), 3);
      for (const b of r.list) { const sx = x0 + (b.slot[0] - r.x0) * k, sw = (b.slot[1] - b.slot[0]) * k, hx = x0 + (b.x0 - r.x0) * k, hw = b.loa * k, bh = Math.max(4, b.beam * k), top = base - 2 - bh, col = CLS[b.cls] || '#fff';
        ctx.strokeStyle = '#3d5a55'; ctx.setLineDash([3, 2]); ctx.strokeRect(Math.round(sx) + 0.5, Math.round(top - 3) + 0.5, Math.max(2, Math.round(sw) - 1), Math.round(bh + 4)); ctx.setLineDash([]);
        ctx.fillStyle = col; ctx.beginPath(); ctx.moveTo(hx, top); ctx.lineTo(hx + hw * 0.8, top); ctx.lineTo(hx + hw, top + bh / 2); ctx.lineTo(hx + hw * 0.8, top + bh); ctx.lineTo(hx, top + bh); ctx.closePath(); ctx.fill();
        const tw = ctx.measureText(b.label).width;
        if (bh >= 11 && tw < hw * 0.8 - 6) { ctx.fillStyle = '#0b1418'; ctx.fillText(b.label, hx + 4, top + bh / 2 + 1); }
        else { const t = tw < sw - 3 ? b.label : ctx.measureText(b.hull).width < sw - 3 ? b.hull : null; if (t) { ctx.fillStyle = col; ctx.fillText(t, Math.round(sx) + 2, base + 16); } } }
      ctx.fillStyle = '#8ba39d'; ctx.fillText(r.faceLen.toFixed(1) + ' m \u00b7 ' + Math.round(r.utilisation * 100) + '%', x0 + fw + 10, base + 1);
      y += h; });
    return y;
  }
  function plateRows() {
    const G = root.WharfGeo2, faces = [['finger slip · 6 m', 6, 'finger', 'auto', 1.2], ['float face · 12 m', 12, 'float', 'auto', 2.5], ['pier face · 16.8 m', 16.8, 'pier', 'auto', 3.2],
      ['crib face · 12.8 m', 12.8, 'crib', 'auto', 0.6], ['quay · 48 m', 48, 'quay', 'auto', 5.6], ['quay · 144 m (three joined)', 144, 'quay', 'auto', 5.6], ['jetty · 130 m', 130, 'jetty', 'tanker', 9]];
    return faces.map(([title, L, kind, want, depth]) => { const bp = G.berthPlan(0, L, kind, want, depth); return Object.assign({ title, x0: 0 }, bp); });
  }
  root.WharfPlates2 = { overlay, berthPlate, plateRows, plateHeight, plateLayout, CLS };
})(typeof globalThis !== 'undefined' ? globalThis : window);
