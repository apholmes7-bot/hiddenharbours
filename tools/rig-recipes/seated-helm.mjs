// Derive the approved seated calibrations from each hull's own walk: three cockpit helms (the rig's
// `drive` clip at a wheel) and one saddle (the rig's `astride` clip on the jetski's seat, grips and footwells).
import fs from 'node:fs';
import path from 'node:path';
import { ROOT, BOATS, loadRig, lfSha, CHARACTER, serialize, walkPath } from './hull-walk.mjs';

const names = { go_fast: 'GoFastIso', bowrider: 'BowriderIso', express_cruiser: 'ExpressCruiserIso' };
const saddles = { jet_ski: 'JetSkiIso' };
const r4 = x => Math.round(x * 10000) / 10000;
const side = (list, id) => list.find(p => p.id === id);
// A machine you sit astride: the figure's ground pivot is under the seat at footwell height, and the
// rig's saddle option is handed the hull's own seat, grips and footwells in that figure frame.
function saddleRow(boat) {
  const { H } = loadRig(boat), a = H.walk().anchors;
  const gL = side(a.grips, 'port'), gR = side(a.grips, 'stbd'), fL = side(a.feet, 'port'), fR = side(a.feet, 'stbd');
  const floorOrigin = { x: a.helm.x, y: r4(a.helm.y + .02), z: fL.z };
  const rel = p => ({ x: r4(p.x - floorOrigin.x), y: r4(p.y - floorOrigin.y), z: r4(p.z - floorOrigin.z) });
  const wheel = { x: r4((gL.x + gR.x) / 2), y: r4((gL.y + gR.y) / 2), z: r4((gL.z + gR.z) / 2) };
  return {
    hull: 'boat.' + boat.hull,
    visual: 'Assets/_Project/Data/Boats/Visuals/' + saddles[boat.hull] + '.asset',
    state: 'astride_helm_' + boat.hull,
    clip: 'astride',
    floorOrigin, seat: a.helm, wheel,
    seatZ: r4(a.helm.z - floorOrigin.z),
    wheelZ: r4(wheel.z - floorOrigin.z),
    wheelY: r4(wheel.y - floorOrigin.y),
    saddle: { seat: rel(a.helm), gripL: rel(gL), gripR: rel(gR), pegL: rel(fL), pegR: rel(fR) },
    walk: path.relative(ROOT, walkPath(boat)).replaceAll('\\', '/'),
    walkSha256: lfSha(walkPath(boat))
  };
}
const file = path.join(ROOT, 'docs/art/rigs/character/rig10/seated-helm.contract.json');
const contract = {
  schema: 'hidden-harbours/seated-helm@1',
  rig: CHARACTER.rig, poses: CHARACTER.poses,
  rigSha256: lfSha(path.join(ROOT, CHARACTER.rig)),
  posesSha256: lfSha(path.join(ROOT, CHARACTER.poses)),
  rows: BOATS.filter(boat => names[boat.hull] || saddles[boat.hull]).map(boat => {
    if (saddles[boat.hull]) return saddleRow(boat);
    const { H } = loadRig(boat), a = H.walk().anchors;
    const floorOrigin = { x: a.helm.x, y: r4(a.helm.y + .02), z: a.pilot.z };
    return {
      hull: 'boat.' + boat.hull,
      visual: 'Assets/_Project/Data/Boats/Visuals/' + names[boat.hull] + '.asset',
      state: 'drive_helm_' + boat.hull,
      floorOrigin, seat: a.helm, wheel: a.wheel,
      seatZ: r4(a.helm.z - floorOrigin.z),
      wheelZ: r4(a.wheel.z - floorOrigin.z),
      wheelY: r4(a.wheel.y - floorOrigin.y),
      walk: path.relative(ROOT, walkPath(boat)).replaceAll('\\', '/'),
      walkSha256: lfSha(walkPath(boat))
    };
  })
};
const text = serialize(contract);
if (process.argv.includes('--write')) fs.writeFileSync(file, text);
else if (fs.readFileSync(file, 'utf8').replaceAll('\r\n', '\n') !== text) throw Error('Seated helm contract is stale');
console.log('PASS seated helm contract: ' + contract.rows.length + ' hulls');
