// NMC roads (H4): G3's plan as the game committed it. Node built-ins only.
// read(asset): the nmc.plan.* options of KeyScene_nmc_wharf.asset, each section parsed back to JSON exactly as
// KeySceneImport.Nmc wrote it (a YAML single-quoted scalar: folded lines join with one space, an empty line is a
// newline, '' is a quote). source(asset): the nmc.ground.source line, the plan file's path and sha256.
'use strict';
const fs = require('fs');

const ASSET = 'Assets/_Project/Data/KeyScenes/KeyScene_nmc_wharf.asset';

function options(asset) {
  const lines = fs.readFileSync(asset, 'utf8').replace(/\r\n/g, '\n').split('\n'), out = {};
  for (let i = 0; i < lines.length; i++) {
    const m = lines[i].match(/^\s+- Key: (nmc\.[\w.]+)$/); if (!m) continue;
    const v = lines[i + 1].match(/^\s+Value: (.*)$/); if (!v) continue;
    let text = v[1];
    if (!text.startsWith("'")) {
      // a plain scalar: folded lines run on until the next key
      let j = i + 2; while (j < lines.length && !/^\s+- Key: /.test(lines[j]) && /^\s{8,}\S/.test(lines[j])) text += ' ' + lines[j++].trim();
      out[m[1]] = text; continue;
    }
    text = text.slice(1);
    const parts = []; let j = i + 1;
    while (!/(^|[^'])('')*'$/.test(text)) {
      parts.push(text); j++; text = lines[j].replace(/^\s+/, '');
      if (text === '') parts.push('\n');
    }
    parts.push(text.slice(0, -1));
    let s = ''; for (const p of parts) s += p === '\n' ? '\n' : (s && !s.endsWith('\n') ? ' ' : '') + p;
    out[m[1]] = s.replace(/''/g, "'");
  }
  return out;
}

function read(asset = ASSET) {
  const o = options(asset), plan = {};
  for (const k of Object.keys(o)) {
    if (!k.startsWith('nmc.plan.')) continue;
    const name = k.slice('nmc.plan.'.length);
    try { plan[name] = JSON.parse(o[k]); } catch { plan[name] = o[k]; }
  }
  return plan;
}

function source(asset = ASSET) {
  const s = options(asset)['nmc.ground.source'] || '';
  const m = s.match(/^(.*), sha256 ([0-9a-f]{64})$/);
  if (!m) throw new Error('no nmc.ground.source in ' + asset);
  return { file: m[1], sha256: m[2] };
}

module.exports = { ASSET, options, read, source };
