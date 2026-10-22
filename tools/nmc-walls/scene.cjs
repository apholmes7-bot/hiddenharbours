// NMC walls (H3): the CliffWalls root of NineMileCreek.unity, read by text. Each wall's four documents (GameObject,
// Transform, SortingGroup, CliffWallSurface) by fileID, in the root's child order, with its lines as numbers AND as the
// scene wrote them (a kept station is carried into its Def verbatim, so the two agree to the bit). Node built-ins only.
'use strict';
const SURFACE_GUID = '54da58bd612c4da42b8ec4712a70b75c';

function docs(text) {
  const out = new Map(), re = /^--- !u!(\d+) &(-?\d+)( stripped)?\r?$/gm;
  let m, prev = null;
  while ((m = re.exec(text))) {
    if (prev) prev.body = text.slice(prev.start, m.index);
    prev = { cls: +m[1], id: m[2], start: m.index };
    out.set(prev.id, prev);
  }
  if (prev) prev.body = text.slice(prev.start);
  return out;
}

const field = (body, name) => { const m = body.match(new RegExp('^  ' + name + ': ?(.*)$', 'm')); return m ? m[1].trim() : null; };
// a list field's items as the scene wrote them; [] where it says so; null where it is absent
function items(body, name) {
  const m = body.match(new RegExp('^  ' + name + ':\\r?\\n((?:  - [^\\r\\n]*\\r?\\n)*)', 'm'));
  if (!m) return field(body, name) === '[]' ? [] : null;
  return [...m[1].matchAll(/^  - ([^\r\n]*)$/gm)].map(v => v[1]);
}
const vecText = (s) => { const m = s.match(/^\{x: ([^,]+), y: ([^}]+)\}$/); if (!m) throw new Error('not a Vector2: ' + s); return [m[1], m[2]]; };
const refs = (body, name) => [...body.matchAll(new RegExp('^  - ' + name + ': \\{fileID: (-?\\d+)\\}', 'gm'))].map(m => m[1]);

/** The walls under the CliffWalls root, in its child order. */
function cliffWalls(text) {
  const all = docs(text);
  const rootGo = [...all.values()].find(d => d.cls === 1 && /^  m_Name: CliffWalls\r?$/m.test(d.body));
  if (!rootGo) throw new Error('no CliffWalls root');
  const rootTr = refs(rootGo.body, 'component').map(id => all.get(id)).find(d => d.cls === 4);
  const kids = [...rootTr.body.matchAll(/^  - \{fileID: (-?\d+)\}/gm)].map(m => m[1]);
  const walls = kids.map((trId, order) => {
    const tr = all.get(trId), goId = field(tr.body, 'm_GameObject').match(/-?\d+/)[0], go = all.get(goId);
    const comps = refs(go.body, 'component').map(id => all.get(id));
    const sg = comps.find(d => d.cls === 210), sf = comps.find(d => d.cls === 114 && d.body.includes(SURFACE_GUID));
    const name = field(go.body, 'm_Name');
    const mm = name.match(/^CliffWall_(\w+?)_(W|SW|S|SE|E)_(wall|steep|ramp|bank)_(\d{3})$/);
    if (!mm) throw new Error('not a wall name: ' + name);
    const b = sf.body;
    const raw = { brow: items(b, '_browPlan').map(vecText), toe: items(b, '_toePlan').map(vecText),
      drop: items(b, '_dropMetres'), toeZ: items(b, '_toeElevations') };
    return {
      order, name, cls: mm[1], aspect: mm[2], batter: mm[3], realId: mm[4],
      active: field(go.body, 'm_IsActive') === '1',
      ids: { go: goId, transform: trId, sortingGroup: sg.id, surface: sf.id },
      sortingOrder: +field(sg.body, 'm_SortingOrder'),
      raw,
      brow: raw.brow.map(([x, y]) => [+x, +y]), toe: raw.toe.map(([x, y]) => [+x, +y]),
      drop: raw.drop.map(Number), toeZ: raw.toeZ.map(Number),
      along: +field(b, '_alongOffsetMetres'), rows: +field(b, '_rowsBasisSurfaceMetres'),
      azimuth: +field(b, '_wallAzimuth'), batterDegrees: +field(b, '_batter'),
    };
  });
  return { root: { go: rootGo.id, transform: rootTr.id }, walls };
}

/** The runs a root's PropertyBoundary holds (QuayWalls': the quay's faces, as the wharf's step re-lined them). */
function boundaryRuns(text, rootName) {
  const all = docs(text);
  const go = [...all.values()].find(d => d.cls === 1 && new RegExp('^  m_Name: ' + rootName + '\\r?$', 'm').test(d.body));
  if (!go) throw new Error('no ' + rootName + ' root');
  const mb = refs(go.body, 'component').map(id => all.get(id)).find(d => d.cls === 114 && /^  _runs:/m.test(d.body));
  if (!mb) throw new Error(rootName + ' holds no boundary runs');
  const block = mb.body.split(/^  _runs:\r?\n/m)[1].split(/^  [^ -]/m)[0];
  return block.split(/^  - Points:\r?\n/m).slice(1).map(r => [...r.matchAll(/\{x: ([^,]+), y: ([^}]+)\}/g)].map(v => [+v[1], +v[2]]));
}

module.exports = { cliffWalls, boundaryRuns, docs, field, items };
