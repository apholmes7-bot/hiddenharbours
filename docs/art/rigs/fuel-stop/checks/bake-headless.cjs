'use strict';
const fs = require('node:fs'), path = require('node:path'), http = require('node:http'),
  {spawn} = require('node:child_process'), crypto = require('node:crypto');
const {ROOT} = require('./write-today.cjs');
const [input, output] = process.argv.slice(2).map(p => path.resolve(p));
if (!input || !output) throw Error('Pass today.json and output directory');
fs.mkdirSync(output, {recursive: true});
const todayText = fs.readFileSync(input, 'utf8').replaceAll('\r\n', '\n');
let browser, timer;
const server = http.createServer(async (req, res) => {
  try {
    if (req.url === '/input') { res.end(todayText); return; }
    if (req.url === '/favicon.ico') { res.statusCode = 204; res.end(); return; }
    if (req.method === 'POST') {
      const buffers = []; for await (const b of req) buffers.push(b);
      const text = Buffer.concat(buffers).toString();
      if (req.url === '/piece') {
        const {piece, png} = JSON.parse(text);
        if (!/^[A-Za-z]+$/.test(piece.key) || !png.startsWith('data:image/png;base64,')) throw Error('Invalid output');
        fs.writeFileSync(path.join(output, piece.sheet + '.png'), Buffer.from(png.split(',')[1], 'base64'));
        console.log(piece.key + ' ' + piece.sheetW + 'x' + piece.sheetH + ' frames ' + piece.frames.length);
      } else if (req.url === '/done') {
        const contract = JSON.parse(text);
        contract.todaySha256Lf = crypto.createHash('sha256').update(todayText).digest('hex');
        fs.writeFileSync(path.join(output, 'fuelStop.contract.json'), JSON.stringify(contract, null, 1) + '\n');
        console.log('DONE: ' + contract.pieces.length + ' sheets; native cells match gameplay');
        res.end('OK'); finish(0); return;
      } else if (req.url === '/error') { console.error(text); res.end('ERROR'); finish(1); return; }
      res.end('OK'); return;
    }
    const file = path.resolve(ROOT, '.' + decodeURIComponent(req.url));
    if (!file.startsWith(ROOT + path.sep)) throw Error('Outside repository');
    res.setHeader('Content-Type', file.endsWith('.js') ? 'text/javascript' : 'text/html');
    res.end(fs.readFileSync(file));
  } catch (e) { res.statusCode = 500; res.end(e.message); console.error(e.message); }
});
function finish(code) { clearTimeout(timer); if (browser) browser.kill(); server.close(); process.exitCode = code; }
server.listen(0, '127.0.0.1', () => {
  const executable = process.env.H9_BROWSER || path.join(process.env.PROGRAMFILES, 'Google/Chrome/Application/chrome.exe');
  console.log('Headless Chrome; canvas ImageData from FuelStop.render; cap 4096');
  browser = spawn(executable, ['--headless', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
    '--user-data-dir=' + path.join(output, 'browser-profile'),
    'http://127.0.0.1:' + server.address().port + '/docs/art/rigs/fuel-stop/fuelStopBake.html'], {windowsHide: true, stdio: 'ignore'});
  browser.on('error', e => {console.error(e.message); finish(1);});
  timer = setTimeout(() => {console.error('STOP: bake timeout after 10 minutes'); finish(1);}, 600000);
});
