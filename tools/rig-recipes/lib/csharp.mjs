// Reads the FACTS a baker states in its own C# source, so the backfilled ledger is DERIVED from the
// baker rather than transcribed beside it.
//
// ⚠️ This is a scraper, not a compiler. It understands exactly four shapes — `const` scalars, a
// `new[] { "a", "b" }` array, a `["k"] = new[] { … }` dictionary, and a `RigEntry` registration —
// and it THROWS on anything it cannot find. A silent empty list would bake a short kit, which is
// this repo's most-repeated failure; every accessor here refuses instead.
import fs from 'node:fs';
import path from 'node:path';

export const REPO = path.resolve(new URL('../../..', import.meta.url).pathname);

const cache = new Map();
export function source(relPath) {
  if (!cache.has(relPath)) cache.set(relPath, fs.readFileSync(path.join(REPO, relPath), 'utf8'));
  return cache.get(relPath);
}

/** Strips `//` and block comments so a commented-out table never reads as live code. */
function strip(src) {
  return src.replace(/\/\*[\s\S]*?\*\//g, ' ').replace(/^[ \t]*\/\/.*$/gm, ' ');
}

function scalar(literal) {
  const t = literal.trim();
  if (t === 'true') return true;
  if (t === 'false') return false;
  if (/^"(.*)"$/s.test(t)) return t.slice(1, -1);
  const n = Number(t.replace(/[fdmFDM]$/, ''));
  if (Number.isNaN(n)) throw new Error(`not a scalar literal: ${literal}`);
  return n;
}

/**
 * The `{ … }` initialiser block that follows `<name> =`, by BRACE MATCHING rather than by an
 * indentation-dependent terminator. Returns the body between the outermost braces.
 */
export function block(relPath, name) {
  const src = strip(source(relPath));
  const at = src.search(new RegExp(`\\b${name}\\s*=`));
  if (at < 0) throw new Error(`${relPath}: no '${name} ='`);
  const open = src.indexOf('{', at);
  if (open < 0) throw new Error(`${relPath}: ${name} has no initialiser block`);
  let depth = 0;
  for (let i = open; i < src.length; i++) {
    if (src[i] === '{') depth++;
    else if (src[i] === '}' && --depth === 0) return src.slice(open + 1, i);
  }
  throw new Error(`${relPath}: ${name}'s initialiser block is unterminated`);
}

/** `public const <type> Name = <literal>;` — the tunables a baker states once and passes on every call. */
export function constant(relPath, name) {
  const m = new RegExp(`\\bconst\\s+\\w+\\s+${name}\\s*=\\s*([^;]+);`).exec(strip(source(relPath)));
  if (!m) throw new Error(`${relPath}: no 'const … ${name} = …;'`);
  return scalar(m[1]);
}

/** The `{ a, b, c }` initialiser of a field, as scalars. Handles `new[] { … }` and `= { … }`. */
export function array(relPath, name) {
  const src = strip(source(relPath));
  const m = new RegExp(`\\b${name}\\s*=\\s*(?:new(?:\\s+[\\w<>,\\[\\]\\s]*?)?\\s*\\[\\s*\\]\\s*)?\\{([^{}]*)\\}`)
    .exec(src);
  if (!m) throw new Error(`${relPath}: no array initialiser for ${name}`);
  return m[1].split(',').map(s => s.trim()).filter(Boolean).map(scalar);
}

/**
 * A `["key"] = new[] { "a", "b" },` dictionary initialiser, in source order.
 * Order matters — several kits declare "the rig's own TYPE_ORDER" and the sheet list follows it.
 */
export function dictionary(relPath, name) {
  const body = block(relPath, name);
  const out = {};
  const re = /\[\s*"([^"]+)"\s*\]\s*=\s*(?:new(?:\s+[\w<>,\[\]\s]*?)?\s*\[\s*\]\s*)?\{([^{}]*)\}/g;
  let m;
  while ((m = re.exec(body))) out[m[1]] = m[2].split(',').map(s => s.trim()).filter(Boolean).map(scalar);
  if (Object.keys(out).length === 0) throw new Error(`${relPath}: ${name} scraped empty`);
  return out;
}

// ---- the rig catalog ------------------------------------------------------------------------

/**
 * Every `RigCatalog.*.cs` registration, keyed as the catalog keys them. The catalog is the one place
 * that says which FILE and which GLOBAL a rig key means and what has to be loaded first, and the
 * ledger must not carry a second copy of that.
 */
export function rigCatalog() {
  const dir = 'Assets/_Project/Code/Tools/Editor/RigBaking';
  const files = fs.readdirSync(path.join(REPO, dir)).filter(f => /^RigCatalog\..*\.cs$/.test(f) || f === 'RigCatalog.cs');
  const entries = {};
  const re = /\[\s*"([^"]+)"\s*\]\s*=\s*new RigEntry\(\s*\$?"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*AzimuthConvention\.(\w+)\s*(?:,\s*prerequisites:\s*new\[\]\s*\{([^}]*)\})?\s*\)/g;
  for (const f of files) {
    const src = strip(source(`${dir}/${f}`));
    let m;
    while ((m = re.exec(src))) {
      const [, key, rawPath, global, convention, prereqs] = m;
      if (entries[key]) throw new Error(`duplicate rig catalog key '${key}' (${f})`);
      entries[key] = {
        key,
        source: rawPath.replace('{RigFolder}', 'docs/art/rigs'),
        global,
        convention,
        prerequisites: (prereqs ?? '').split(',').map(s => s.trim().replace(/^"|"$/g, '')).filter(Boolean),
      };
    }
  }
  if (Object.keys(entries).length === 0) throw new Error('rig catalog scraped empty');
  return entries;
}
