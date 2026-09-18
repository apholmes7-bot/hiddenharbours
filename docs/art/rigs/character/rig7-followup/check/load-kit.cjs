/*
 * load-kit.cjs — load the character kit the way the game's bake installs it.
 *
 *   const { loadKit } = require('./load-kit.cjs');
 *   const kit = loadKit({ rigsDir, face: false });   // the skinned rig chain only
 *   kit.installFace();                              // then the five face layers, in the same context
 *
 * One shared context, the catalog's order, every file run UNMODIFIED. The workbench loader patches rig 7 before
 * running it (a boot correction and a bones-only shortcut); the bake does not, so this loader does not either.
 * Node only, no dependencies.
 */
'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm');

// The rig chain the bake's "characterSkin" entry installs: each prerequisite first, depth-first.
const BASE = ['eyeIsoRig.js', 'headIsoRig3.js', 'characterIsoRig6.js', 'characterIsoRig7.js'];

// "characterFaceComposition" and its prerequisites, in the order the catalog names them:
// face study, then finish config before finish, then the fisher art study, then the composition itself.
const FACE = ['characterFaceStudy.js', 'characterFinishConfig.js', 'characterFinish.js', 'characterArtStudy.js',
  'characterFaceComposition.js'];

function run(context, dir, names) {
  for (const name of names) {
    const file = path.join(dir, name);
    if (!fs.existsSync(file)) throw new Error('load-kit: missing ' + file);
    vm.runInContext(fs.readFileSync(file, 'utf8'), context, { filename: name });
  }
}

function loadKit({ rigsDir, face = true } = {}) {
  if (!rigsDir) throw new Error('load-kit: rigsDir is required');
  const context = { console };
  context.globalThis = context;
  vm.createContext(context);
  run(context, rigsDir, BASE);
  let faceInstalled = false;
  const installFace = () => {
    if (faceInstalled) return;
    run(context, rigsDir, FACE);
    faceInstalled = true;
  };
  if (face) installFace();
  return { context, installFace };
}

module.exports = { loadKit, BASE, FACE };
