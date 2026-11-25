'use strict';
/* bake.cjs: the exterior and walk-in sprites of two Nine Mile Creek buildings, the fish plant and the community hall.
     node docs/art/rigs/nmc-buildings-bake/bake.cjs [outDir]
   Node 18+, built-ins only. No Unity, no browser. Renders through the fish plant kit's own rasteriser (the hall kit draws
   through it, see nmc-community-hall-kit/nmcCommunityHall.js) under the bake sky every Nine Mile Creek sheet uses
   (WeatherSky.at({ time: 14, cloud: 0.1, wind: 0.3 }), the fuel stop's today.json), and writes, per building:
     <Name>_exterior.png   the whole building, roof on: the shell's sprite (BuildingInterior hides it while you are inside)
     <Name>_cutaway.png    roof lifted, walls cut at 1.1 m: the room's sprite (shown while you are inside)
     <Name>.json           the sidecar: each sprite's size and pivot, the footprint, the door, the sort offset, the boards
   A sprite's pivot is the projection of the building's centre on the ground (the prefab root of PR 1276's shell), so
   both sprites sit at local (0, 0) under the root. The .meta beside each PNG is written once (fresh GUID and sprite ids)
   and on a re-bake only its rect and pivot change, so references survive. Default outDir: the art folder below. */
const fs = require('fs'), path = require('path'), vm = require('vm'), zlib = require('zlib'), crypto = require('crypto');
const HERE = __dirname, RIGS = path.resolve(HERE, '..'), ROOT = path.resolve(RIGS, '../../..');
const OUT = path.resolve(process.argv[2] || path.join(ROOT, 'Assets/_Project/Art/Sprites/Buildings/NineMileCreek'));

function load() {
  const ctx = vm.createContext({ console });
  for (const f of ['nmc-set-pieces-kit/lib/weatherSky.js', 'nmc-set-pieces-kit/lib/coastalPass.js', 'nmc-set-pieces-kit/nmcSetPieces.js',
    'nmc-fish-plant-kit/nmcFishPlant.js', 'nmc-community-hall-kit/nmcCommunityHall.js'])
    vm.runInContext(fs.readFileSync(path.join(RIGS, f), 'utf8'), ctx, { filename: path.basename(f) });
  ctx.NmcCommunityHall.install(ctx.NmcFishPlant);
  return ctx;
}

// ---- PNG
const crcT = (() => { const t = new Int32Array(256); for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; t[n] = c; } return t; })();
const crc = (b) => { let c = -1; for (let i = 0; i < b.length; i++) c = crcT[(c ^ b[i]) & 255] ^ (c >>> 8); return (c ^ -1) >>> 0; };
const chunk = (type, data) => { const len = Buffer.alloc(4); len.writeUInt32BE(data.length); const td = Buffer.concat([Buffer.from(type), data]), c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([len, td, c]); };
function png(w, h, rgba) { const src = Buffer.from(rgba.buffer, rgba.byteOffset, rgba.byteLength), raw = Buffer.alloc((w * 4 + 1) * h);
  for (let y = 0; y < h; y++) src.copy(raw, y * (w * 4 + 1) + 1, y * w * 4, (y + 1) * w * 4);
  const head = Buffer.alloc(13); head.writeUInt32BE(w); head.writeUInt32BE(h, 4); head[8] = 8; head[9] = 6;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', head), chunk('IDAT', zlib.deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]); }

// ---- the two buildings, as PR 1276's shells place them
const SE = Math.sin(40 * Math.PI / 180);
const BUILDINGS = [
  { name: 'NmcFishPlant', key: 'fishPlant', shell: 'NmcFishPlant', centre: [-265, -40], widthEW: 48, depthNS: 28, door: { wall: 'S', world: [-245, -49], localKey: 'door' },
    exterior: { doors: 'closed' }, cutaway: { cutaway: true } },
  { name: 'NmcCommunityHall', key: 'communityHall', shell: 'NmcCommunityHall', centre: [-153, 62.5], widthEW: 12, depthNS: 18, door: { wall: 'W', world: [-159, 62.5 + 0.5 * SE], localKey: 'door' },
    exterior: {}, cutaway: { cutaway: true } },
];

// ---- metas: a sliced single-sprite sheet like the wharf buildings' (spriteMode 2, PPU 32, point, no compression)
const hex = (n) => crypto.randomBytes(n).toString('hex');
const int64 = () => crypto.randomBytes(8).readBigInt64LE().toString();
function spriteMeta(old, sprite, w, h, pvx, pvy) {
  const guid = old ? old.match(/^guid: ([0-9a-f]{32})/m)[1] : hex(16);
  const spriteID = old ? old.match(/^      spriteID: ([0-9a-f]{32})/m)[1] : hex(16);
  const internalID = old ? old.match(/^      internalID: (-?\d+)/m)[1] : int64();
  const tableID = old ? old.match(/^      213: (-?\d+)/m)[1] : int64();
  const sheetID = old ? old.match(/^    spriteID: ([0-9a-f]{32})/m)[1] : hex(16);
  const plat = (target, max, comp) => ['  - serializedVersion: 4', '    buildTarget: ' + target, '    maxTextureSize: ' + max, '    resizeAlgorithm: 0', '    textureFormat: -1',
    '    textureCompression: ' + comp, '    compressionQuality: 50', '    crunchedCompression: 0', '    allowsAlphaSplitting: 0', '    overridden: 0', '    ignorePlatformSupport: 0',
    '    androidETC2FallbackOverride: 0', '    forceMaximumCompressionQuality_BC6H_BC7: 0'];
  const px = (+pvx).toFixed(8).replace(/0+$/, '').replace(/\.$/, ''), py = (+pvy).toFixed(8).replace(/0+$/, '').replace(/\.$/, '');
  return ['fileFormatVersion: 2', 'guid: ' + guid, 'TextureImporter:', '  internalIDToNameTable:', '  - first:', '      213: ' + tableID, '    second: ' + sprite,
    '  externalObjects: {}', '  serializedVersion: 13', '  mipmaps:', '    mipMapMode: 0', '    enableMipMap: 0', '    sRGBTexture: 1', '    linearTexture: 0', '    fadeOut: 0',
    '    borderMipMap: 0', '    mipMapsPreserveCoverage: 0', '    alphaTestReferenceValue: 0.5', '    mipMapFadeDistanceStart: 1', '    mipMapFadeDistanceEnd: 3', '  bumpmap:',
    '    convertToNormalMap: 0', '    externalNormalMap: 0', '    heightScale: 0.25', '    normalMapFilter: 0', '    flipGreenChannel: 0', '  isReadable: 0', '  streamingMipmaps: 0',
    '  streamingMipmapsPriority: 0', '  vTOnly: 0', '  ignoreMipmapLimit: 0', '  grayScaleToAlpha: 0', '  generateCubemap: 6', '  cubemapConvolution: 0', '  seamlessCubemap: 0',
    '  textureFormat: 1', '  maxTextureSize: 2048', '  textureSettings:', '    serializedVersion: 2', '    filterMode: 0', '    aniso: 1', '    mipBias: 0', '    wrapU: 1', '    wrapV: 1',
    '    wrapW: 1', '  nPOTScale: 0', '  lightmap: 0', '  compressionQuality: 50', '  spriteMode: 2', '  spriteExtrude: 1', '  spriteMeshType: 0', '  alignment: 9',
    '  spritePivot: {x: 0.5, y: 0.5}', '  spritePixelsToUnits: 32', '  spriteBorder: {x: 0, y: 0, z: 0, w: 0}', '  spriteGenerateFallbackPhysicsShape: 1', '  alphaUsage: 1',
    '  alphaIsTransparency: 1', '  spriteTessellationDetail: -1', '  textureType: 8', '  textureShape: 1', '  singleChannelComponent: 0', '  flipbookRows: 1', '  flipbookColumns: 1',
    '  maxTextureSizeSet: 0', '  compressionQualitySet: 0', '  textureFormatSet: 0', '  ignorePngGamma: 0', '  applyGammaDecoding: 0', '  swizzle: 50462976', '  cookieLightType: 0',
    '  platformSettings:', ...plat('DefaultTexturePlatform', 4096, 0), ...plat('Standalone', 4096, 0), ...plat('iOS', 4096, 0),
    '  spriteSheet:', '    serializedVersion: 2', '    sprites:', '    - serializedVersion: 2', '      name: ' + sprite, '      rect:', '        serializedVersion: 2', '        x: 0', '        y: 0',
    '        width: ' + w, '        height: ' + h, '      alignment: 9', '      pivot: {x: ' + px + ', y: ' + py + '}', '      border: {x: 0, y: 0, z: 0, w: 0}', '      customData: ',
    '      outline: []', '      physicsShape: []', '      tessellationDetail: 0', '      bones: []', '      spriteID: ' + spriteID, '      internalID: ' + internalID, '      vertices: []',
    '      indices: ', '      edges: []', '      weights: []', '    outline: []', '    customData: ', '    physicsShape: []', '    bones: []', '    spriteID: ' + sheetID, '    internalID: 0',
    '    vertices: []', '    indices: ', '    edges: []', '    weights: []', '    secondaryTextures: []', '    spriteCustomMetadata:', '      entries: []', '    nameFileIdTable:',
    '      ' + sprite + ': ' + internalID, '  mipmapLimitGroupName: ', '  pSDRemoveMatte: 0', '  userData: ', '  assetBundleName: ', '  assetBundleVariant: ', ''].join('\n');
}
const textMeta = (old) => old || ['fileFormatVersion: 2', 'guid: ' + hex(16), 'TextScriptImporter:', '  externalObjects: {}', '  userData: ', '  assetBundleName: ', '  assetBundleVariant: ', ''].join('\n');
const readOr = (f) => fs.existsSync(f) ? fs.readFileSync(f, 'utf8').replace(/\r\n/g, '\n') : null;

function bake(ctx, b, outDir, writeMetas) {
  const NF = ctx.NmcFishPlant, sky = ctx.WeatherSky.at({ time: 14, cloud: 0.1, wind: 0.3 }), r3 = (v) => +v.toFixed(3);
  const side = { schema: 'nmc-building-art@1', building: b.name, kit: b.key === 'fishPlant' ? 'nmc-fish-plant-kit/nmcFishPlant.js ' + NF.VERSION : 'nmc-community-hall-kit/nmcCommunityHall.js ' + ctx.NmcCommunityHall.VERSION,
    bakeSky: { time: 14, cloud: 0.1, wind: 0.3 }, ppu: NF.PPU, dir: 4, centreWorld: b.centre, footprintMetres: { eastWest: b.widthEW, northSouth: b.depthNS },
    frame: 'local x = -(world east offset), local y = -(world north offset); 1 m north draws sin 40 = ' + r3(SE) + ' units up the screen',
    sortPivotYOffsetUnits: r3(-b.depthNS / 2 * SE), sortNote: 'YSortSprite.SortPivotYOffset on the Shell: the front (south) wall’s foot, as the greybox shell sorts', sprites: {} };
  for (const [kind, opts] of [['exterior', b.exterior], ['cutaway', b.cutaway]]) {
    const r = NF.render(b.key, 4, Object.assign({ sky, lit: 0 }, opts)), name = b.name + '_' + kind, file = path.join(outDir, name + '.png');
    fs.writeFileSync(file, png(r.w, r.h, r.rgba));
    const pvx = r.px / r.w, pvy = (r.h - r.py) / r.h, a = NF.anchors(b.key, 4, opts);
    side.sprites[kind] = { file: name + '.png', sprite: name, w: r.w, h: r.h, pivotPx: [r.px, r.py], pivotUnity: [r3(pvx), r3(pvy)],
      worldUnits: [r3(r.w / NF.PPU), r3(r.h / NF.PPU)], doorPx: a.px && a.px[b.door.localKey], words: (a.words || []).map(w => ({ id: w.id, cornersPx: w.cornersPx })),
      lightsPx: (a.lights || []).map(l => ({ id: l.id, atPx: l.atPx })) };
    if (writeMetas) fs.writeFileSync(file + '.meta', spriteMeta(readOr(file + '.meta'), name, r.w, r.h, pvx, pvy));
    console.log('WROTE ' + name + '.png ' + r.w + 'x' + r.h + ' pivot ' + r.px + ',' + r.py);
  }
  side.door = { wall: b.door.wall, world: b.door.world.map(r3), note: 'the shell’s DoorWorld (NmcBuildingShells): the threshold; the art’s door lands on it' };
  const sf = path.join(outDir, b.name + '.json'); fs.writeFileSync(sf, JSON.stringify(side, null, 1) + '\n');
  if (writeMetas) fs.writeFileSync(sf + '.meta', textMeta(readOr(sf + '.meta')));
  return side;
}

if (require.main === module) {
  fs.mkdirSync(OUT, { recursive: true });
  const ctx = load(), t0 = Date.now(), metas = !process.argv[2];
  for (const b of BUILDINGS) bake(ctx, b, OUT, metas);
  console.log('DONE in ' + ((Date.now() - t0) / 1000).toFixed(1) + ' s' + (metas ? '' : ' (no metas: a scratch out dir)'));
}
module.exports = { load, png, BUILDINGS, bake };
