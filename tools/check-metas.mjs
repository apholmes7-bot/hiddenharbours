#!/usr/bin/env node
//   node tools/check-metas.mjs          from the repository root: checks the tracked .metas in Assets/ and Packages/
//   node tools/check-metas.mjs <root>   checks the project at <root> instead; exits 1 naming each offender and its rule
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

// The four rules, worded and ordered as Assets/Tests/EditMode/AssetMetaGuidTests.cs words them. The test and this
// script must agree on every rule and every message: change one, change the other.
const RULES = {
  a: 'one guid: line',
  b: '32 characters, each 0-9 or a-f',
  c: 'no shared guid',
  d: 'every file and folder has its meta, every meta its file or folder',
};
const ROOTS = ['Assets', 'Packages'];

const args = process.argv.slice(2);
if (args.length > 1 || args[0]?.startsWith('-')) {
  console.error('usage: node tools/check-metas.mjs [root]');
  process.exit(2);
}
const root = path.resolve(args[0] ?? '.');
const GIT_TIMEOUT_MS = 60_000;
const firstLine = text => String(text).trim().split('\n')[0].trim();
const isMeta = p => p.endsWith('.meta');

// What is tracked: `git ls-files` from the root. If git cannot say, walk Assets/ and Packages/ on disk instead.
function snapshot() {
  let why;
  try {
    const out = execFileSync('git', ['ls-files', '-z', '--', ...ROOTS], {
      cwd: root, encoding: 'utf8', maxBuffer: 1 << 30, timeout: GIT_TIMEOUT_MS, stdio: ['ignore', 'pipe', 'pipe'],
    });
    const files = out.split('\0').filter(Boolean).sort();
    if (files.some(f => f.startsWith('Assets/')))
      return { files, folders: impliedFolders(files), source: `git ls-files, ${files.length} tracked paths` };
    why = 'git ls-files listed nothing under Assets/';
  } catch (e) {
    why = e.code === 'ETIMEDOUT'
      ? `git ls-files did not finish in ${GIT_TIMEOUT_MS / 1000} s`
      : `git could not run (${firstLine(e.stderr?.toString() || e.message)})`;
  }
  return walk(why);
}

// Every folder above a tracked path. Git tracks no folders, so these are the folders a clone will have.
function impliedFolders(files) {
  const folders = new Set();
  for (const file of files)
    for (let slash = file.lastIndexOf('/'); slash > 0; slash = file.lastIndexOf('/', slash - 1)) {
      if (folders.has(file.slice(0, slash))) break;
      folders.add(file.slice(0, slash));
    }
  return folders;
}

// Everything in Assets/ and Packages/, hidden and `~` names included: the rules decide what Unity ignores. Library,
// Temp, Logs, obj and build output sit beside them at the root, so the walk never reads them.
function walk(why) {
  const files = [], folders = new Set();
  const isDir = rel => fs.statSync(path.join(root, rel), { throwIfNoEntry: false })?.isDirectory() ?? false;
  const visit = rel => {
    folders.add(rel);
    for (const entry of fs.readdirSync(path.join(root, rel), { withFileTypes: true })) {
      const child = `${rel}/${entry.name}`;
      if (entry.isDirectory() || (entry.isSymbolicLink() && isDir(child))) visit(child);
      else files.push(child);
    }
  };
  for (const top of ROOTS) if (isDir(top)) visit(top);
  return {
    files: files.sort(), folders,
    source: `walked Assets/ and Packages/ on disk because ${why}; Library, Temp, Logs, obj and build output sit ` +
            'outside them and are not read',
  };
}

// Unity imports nothing whose name starts with '.', ends with '~' or is cvs, and no file ending .tmp, so none of
// those has a meta. parts[0] is the root folder itself.
function unityIgnores(p, isFile) {
  const parts = p.split('/');
  for (let i = 1; i < parts.length; i++)
    if (parts[i].startsWith('.') || parts[i].endsWith('~') || parts[i].toLowerCase() === 'cvs') return true;
  return isFile && parts[parts.length - 1].toLowerCase().endsWith('.tmp');
}

// The value of every line that starts `guid:`, a line ending where YAML ends one: CRLF, CR or LF. An indented or
// inline `guid:` is a reference to another asset.
function guidValues(text) {
  return text.replace(/^\uFEFF/, '').split(/\r\n|\r|\n/)
    .filter(line => line.startsWith('guid:'))
    .map(line => line.slice(5).replace(/^[ \t]+|[ \t]+$/g, ''));
}

function malformed(guid) {
  let bad = -1;
  for (let i = 0; i < guid.length && bad < 0; i++)
    if (!((guid[i] >= '0' && guid[i] <= '9') || (guid[i] >= 'a' && guid[i] <= 'f'))) bad = i;
  if (guid.length === 32 && bad < 0) return null;
  return `guid '${guid}' has ${guid.length} characters` +
    (bad < 0 ? '' : `, and character ${bad + 1}, '${guid[bad]}', is not 0-9 or a-f`) +
    '; Unity needs exactly 32, each 0-9 or a-f';
}

// A meta that fails (a) is not read for (b), and only a well-formed guid is compared for (c), so one defect is
// named once, under the first rule it breaks.
function check({ files, folders }, read) {
  const report = { a: [], b: [], c: [], d: [] };
  const metas = files.filter(isMeta);
  const metaSet = new Set(metas);
  const assets = new Set(files.filter(f => !isMeta(f)));
  const byGuid = new Map();
  for (const meta of metas) {
    let values;
    try {
      values = guidValues(read(meta));
    } catch (e) {
      report.a.push(`${meta}: could not be read (${firstLine(e.message)}), want exactly 1 guid: line`);
      continue;
    }
    if (values.length !== 1) {
      report.a.push(`${meta}: ${values.length} guid: lines, want exactly 1` +
                    (values.length ? ': ' + values.map(v => `'${v}'`).join(', ') : ''));
      continue;
    }
    const bad = malformed(values[0]);
    if (bad) {
      report.b.push(`${meta}: ${bad}`);
      continue;
    }
    if (!byGuid.has(values[0])) byGuid.set(values[0], []);
    byGuid.get(values[0]).push(meta);
  }
  for (const guid of [...byGuid.keys()].sort()) {
    const carriers = byGuid.get(guid);
    if (carriers.length > 1) report.c.push(`guid ${guid} is in ${carriers.length} metas: ${carriers.join(', ')}`);
  }
  for (const file of files)
    if (!isMeta(file) && file.startsWith('Assets/') && !unityIgnores(file, true) && !metaSet.has(file + '.meta'))
      report.d.push(`${file}: no meta beside it (want ${file}.meta)`);
  for (const folder of [...folders].sort())
    if (folder.startsWith('Assets/') && !unityIgnores(folder, false) && !metaSet.has(folder + '.meta'))
      report.d.push(`${folder}/: no meta beside the folder (want ${folder}.meta)`);
  for (const meta of metas) {
    const asset = meta.slice(0, -'.meta'.length);
    if (!assets.has(asset) && !folders.has(asset))
      report.d.push(`${meta}: no file or folder beside it (want ${asset})`);
  }
  return { report, metas: metas.length };
}

const tree = snapshot();
const { report, metas } = check(tree, meta => fs.readFileSync(path.join(root, meta), 'utf8'));
const where = `${metas} metas under Assets/ and Packages/ (${tree.source})`;
if (metas === 0) {
  console.error(`check-metas: no .meta under Assets/ or Packages/ (${tree.source}); checking nothing is a failure`);
  process.exit(1);
}
const fired = Object.keys(RULES).filter(rule => report[rule].length);
for (const rule of fired)
  for (const message of report[rule]) console.error(`rule (${rule}) ${RULES[rule]}: ${message}`);
if (!fired.length) {
  console.log(`check-metas: ${where}: all four rules pass`);
  process.exit(0);
}
const count = fired.reduce((sum, rule) => sum + report[rule].length, 0);
console.error(`check-metas: ${where}: FAIL, ${count} offender${count === 1 ? '' : 's'} under ` +
              `rule${fired.length === 1 ? '' : 's'} ${fired.map(rule => `(${rule})`).join(', ')}`);
process.exit(1);
