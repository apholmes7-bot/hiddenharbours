// NMC walls (H3): a CliffWallDef asset and its .meta, as Unity's text serialiser writes them (Force Text, LF), field for
// field in the class's order. Floats are the shortest text that reads back to the same float32, which is what the editor
// writes; a station kept from the scene carries the scene's own text, so the two agree to the bit.
'use strict';
const crypto = require('crypto');
const SCRIPT_GUID = '70c5b8a6bbfa195e87b8e0921fd70753';      // CliffWallDef.cs.meta
const STATUS = { Kept: 0, Relined: 1, Banked: 2, Return: 3, ToeLifted: 4, Retired: 5, Held: 6 };
const SOURCE = { Kept: 0, Measured: 1, Interpolated: 2 };
const CLASS = { Beach: 0, Dune: 1, Access: 2, Cliff: 3, DeepShoreCliff: 4, LedgeCliff: 5 };

/** The shortest decimal that reads back to the same float32 (the editor's form: 6, -1.6500001, 9.190001). */
function f32s(x) {
  const v = Math.fround(x);
  if (!Number.isFinite(v)) throw new Error('not a finite float: ' + x);
  if (Object.is(v, -0)) return '-0';
  for (let p = 1; p <= 9; p++) {
    const s = String(+v.toPrecision(p));
    if (Math.fround(+s) === v) { if (/e/i.test(s)) throw new Error('exponent form: ' + s); return s; }
  }
  throw new Error('no 9-digit form for ' + x);
}
/** A YAML scalar string as Unity writes one: bare where it can be, else single-quoted, an apostrophe doubled. */
function str(s) {
  if (s === '') return '';
  if (/^[A-Za-z_][A-Za-z0-9_.-]*( [A-Za-z0-9_.-]+)*$/.test(s) && !/^(true|false|null|yes|no|on|off)$/i.test(s)) return s;
  return "'" + s.replace(/'/g, "''") + "'";
}
const num = (x, text) => (text != null ? text : f32s(x));
const list = (name, xs) => xs.length ? `  ${name}:\n` + xs.map(x => `  - ${x}\n`).join('') : `  ${name}: []\n`;
const vec = (p, t) => `{x: ${num(p[0], t && t[0])}, y: ${num(p[1], t && t[1])}}`;

/**
 * The asset text of one Def. d: { id, name, status, splitFrom, follows, rock, cls, aspect, batter, stations: [{ brow,
 * toe, drop, toeZ, k, browFrom, toeFrom, browPass9, toePass9, text? }], pass9: bool, sourceList, sourceListSha256,
 * sourceRow, measuredOn, faceNeeded, why }. A station's text (as the scene wrote it) wins over its numbers.
 */
function assetText(d) {
  const st = d.stations, t = (s) => s.text || {};
  return '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n' +
    '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n' +
    '  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n' +
    `  m_Script: {fileID: 11500000, guid: ${SCRIPT_GUID}, type: 3}\n` +
    `  m_Name: ${d.name}\n` +
    '  m_EditorClassIdentifier: HiddenHarbours.World::HiddenHarbours.World.CliffWallDef\n' +
    `  Id: ${d.id}\n  Status: ${STATUS[d.status]}\n  SplitFrom: ${d.splitFrom || ''}\n  Follows: ${d.follows || ''}\n` +
    `  Rock: ${d.rock}\n  Class: ${CLASS[d.cls]}\n  Aspect: ${d.aspect}\n  Batter: ${d.batter}\n` +
    list('Brow', st.map(s => vec(s.brow, t(s).brow))) +
    list('Toe', st.map(s => vec(s.toe, t(s).toe))) +
    list('DropMetres', st.map(s => num(s.drop, t(s).drop))) +
    list('ToeElevations', st.map(s => num(s.toeZ, t(s).toeZ))) +
    list('TextureBrow', []) +
    list('Stations', st.map(s => f32s(s.k))) +
    list('BrowFrom', st.map(s => SOURCE[s.browFrom])) +
    list('ToeFrom', st.map(s => SOURCE[s.toeFrom])) +
    list('BrowPass9', d.pass9 ? st.map(s => f32s(s.browPass9)) : []) +
    list('ToePass9', d.pass9 ? st.map(s => f32s(s.toePass9)) : []) +
    `  SourceList: ${str(d.sourceList)}\n  SourceListSha256: ${d.sourceListSha256}\n  SourceRow: ${d.sourceRow}\n` +
    `  MeasuredOn: ${str(d.measuredOn)}\n  FaceNeeded: ${str(d.faceNeeded)}\n  Why: ${str(d.why)}\n`;
}

/** A Def's guid: stable from its id, so a re-run writes the same .meta. */
const guidOf = (id) => crypto.createHash('sha256').update('nmc-walls/' + id).digest('hex').slice(0, 32);
const metaText = (guid) => `fileFormatVersion: 2\nguid: ${guid}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n` +
  '  userData: \n  assetBundleName: \n  assetBundleVariant: \n';
const folderMetaText = (guid) => `fileFormatVersion: 2\nguid: ${guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n` +
  '  userData: \n  assetBundleName: \n  assetBundleVariant: \n';

module.exports = { f32s, str, assetText, guidOf, metaText, folderMetaText, STATUS, SOURCE, CLASS, SCRIPT_GUID };
