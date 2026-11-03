// Derive the three approved seated calibrations from each hull's own walk.
import fs from 'node:fs';
import path from 'node:path';
import { ROOT, BOATS, loadRig, lfSha, CHARACTER, serialize, walkPath } from './hull-walk.mjs';

const names = { go_fast: 'GoFastIso', bowrider: 'BowriderIso', express_cruiser: 'ExpressCruiserIso' };
const r4 = x => Math.round(x * 10000) / 10000;
const file = path.join(ROOT, 'docs/art/rigs/character/rig10/seated-helm.contract.json');
const contract = {
  schema: 'hidden-harbours/seated-helm@1',
  rig: CHARACTER.rig, poses: CHARACTER.poses,
  rigSha256: lfSha(path.join(ROOT, CHARACTER.rig)),
  posesSha256: lfSha(path.join(ROOT, CHARACTER.poses)),
  rows: BOATS.map(boat => {
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
