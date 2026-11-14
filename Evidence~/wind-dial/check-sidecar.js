// Sidecar check for the wind dial rig. No browser, no Unity: node only.
//   node "Evidence~/wind-dial/check-sidecar.js"          -> one line: PASS or FAIL with the first difference
//   node "Evidence~/wind-dial/check-sidecar.js" --write  -> (re)writes windRig.sidecar.json from the rig
// The sidecar is a checked copy of WindRig.layout() for every grade x size, plus the rig's printed
// constants, stamped with the sha256 of windRig.js as LF bytes (the file is pinned eol=lf).
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const DIR = path.join(__dirname, '..', '..', 'docs', 'art', 'rigs', 'ui', 'wind-dial');
const RIG = path.join(DIR, 'Art', 'windRig.js'), SIDE = path.join(DIR, 'windRig.sidecar.json');

const src = fs.readFileSync(RIG, 'utf8');
const sha = crypto.createHash('sha256').update(src.replace(/\r\n/g, '\n'), 'utf8').digest('hex');
const crlf = (src.match(/\r/g) || []).length;
new Function(src).call(globalThis);           // layout() and the constants never touch a canvas
const R = globalThis.WindRig;

function build(){
  const layouts = {};
  for (const grade of ['dial', 'display']) for (const size of ['card', 'piece']) layouts[grade + '.' + size] = R.layout(grade, size);
  return {
    schema: 'hh.ui-rig-sidecar/1',
    rig: 'Art/windRig.js',
    global: 'WindRig',
    derivedFromRigSha256: sha,
    units: { space: 'native sprite px, origin top-left, y down', angle: 'degrees', speed: 'knots' },
    angle: {
      convention: 'SailWind / HelmWindReading: signed, off the bow, positive = wind over the STARBOARD bow',
      range: [-180, 180], zeroAt: 'up (the bow)', positiveTurn: 'clockwise on screen (starboard)',
      screenDirection: '(sin a, -cos a) from the pivot',
    },
    sources: {
      awa: 'HelmWindReading.ApparentAngleDeg', aws: 'HelmWindReading.ApparentKn',
      twa: 'HelmWindReading.TrueAngleDeg (grade 2 only; HasTrue)', tws: 'HelmWindReading.TrueKn (grade 2 only; HasTrue)',
      bft: 'WindReadout.Beaufort(SailDrive.ToMetresPerSecond(TrueKn)) - the game table; the rig keeps a preview copy only',
      follow: "grade 2 only: 'apparent' | 'true' - which pair the pointer and the piece's speed read (the unit's one button)",
      producer: 'HelmWind.TryRead(grade, ...) -> SailDrive.ResolveWind; the rig computes no wind',
    },
    printing: { closeHauledDeg: R.CLOSE_HAULED, calmKn: R.CALM_KN, drumMaxKn: R.KN_MAX, lcdKnMax: 99.9 },
    calm: 'below calmKn the LCD shows --- for that angle and hides the pointer; the analog needle has no off state, so the host holds the last angle it drew above calmKn',
    repaint: { angleStepDeg: 2, speedStepKn: 0.5, note: 'helm instruments plan 3: redraw only on a visible step; faces are cached per size x night' },
    nightFollows: 'the helm NIGHT PANEL flag (as every helm instrument)',
    layouts,
  };
}

const want = JSON.stringify(build(), null, 2) + '\n';
if (process.argv.includes('--write')) { fs.writeFileSync(SIDE, want); console.log('WROTE ' + SIDE + ' sha ' + sha.slice(0, 12)); process.exit(0); }
const have = fs.existsSync(SIDE) ? fs.readFileSync(SIDE, 'utf8').replace(/\r\n/g, '\n') : '';
if (have === want) { console.log('PASS sidecar = layout() for 4 grade x size, sha ' + sha.slice(0, 12) + (crlf ? ' (rig has ' + crlf + ' CR, hashed as LF)' : '')); process.exit(0); }
const a = have.split('\n'), b = want.split('\n'); let i = 0; while (i < a.length && a[i] === b[i]) i++;
console.log('FAIL sidecar line ' + (i + 1) + ': have ' + JSON.stringify(a[i]) + ' want ' + JSON.stringify(b[i])); process.exit(1);
