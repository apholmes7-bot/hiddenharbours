/* Character face composition — the production form of the workbench cast engine.

   The workbench (docs/art/character-workbench/cast-engine.js) proved the pass-05 body finish and
   the pass-06 head study offline. This module is the SAME composition, in production form.

   ⚠️ It PATCHES NOTHING. An earlier draft of this file redefined CharacterIso7.bindMesh, which
   reads like the tidiest possible seam and is wrong: the skinned bake does not take its geometry
   from bindMesh. It takes geometry from rig 6 (CharacterPoseMeshExtractor.ExtractPose at idle 0)
   and only the WEIGHTS from bindMesh, then AssertBindAgrees requires the two to agree corner for
   corner within the rig's own 1e-4 m. Composing one side silently would not produce a new face; it
   would produce a bake that throws. So the composition is offered as plain functions and the C#
   caller opts in at a named step, where a reviewer can see it.

   What is preserved, and why it is provable by reading rather than by measuring: skeleton(),
   skeletonWorld(), clip() and clips() are never called here and never wrapped, so the rig's metre
   scale, its skeleton, its physical heights and its hand/foot/attachment anchors cannot move. The
   study head is bound rigidly to the head bone, so it rides the existing skeleton and adds no bone,
   no joint and no influence beyond width 1. Measured anyway on the standalone V8 harness, all ten
   presets: skeletonWorld identical, local bind skeleton identical, all 350 preset x animation clip
   rows identical, max influences still 2.

   What it does NOT do. It does not implement the in-game creator, the wardrobe, purchases or
   appearance save, and it does not give the SPRITE flipbook this face — the sprite still draws
   headIsoRig3's head. That divergence is deliberate and is declared, not hidden: see bodyFaces()
   below, which exists so the bake's cross-check can still see an UNINTENDED mismatch. */
(function(root){
  'use strict';
  const R=root.CharacterIso6, S=root.CharacterIso7, H=root.CharacterHeadStudy,
        F=root.CharacterFinish, A=root.CharacterArtStudy;
  const missing=['CharacterIso6','CharacterIso7','CharacterHeadStudy','CharacterFinish','CharacterArtStudy']
    .filter(n=>!root[n]);
  if(missing.length)throw Error('Character face composition: '+missing.join(', ')+' absent. '+
    'Install the rig chain and the study layers before the composition.');

  // The seven head part-groups rig 6 authors natively, replaced wholesale by the study head, plus
  // the inseam the finish re-tailors. Identical to cast-engine.js line 3 and line 25.
  const oldHead=new Set(['head','hair','eye','brow','ear','nose','mouth']);
  const buildOf=b=>typeof b==='string'?R.resolveBuild({build:{preset:b}}):
                   (b&&b.preset&&b.sex===undefined?R.resolveBuild({build:b}):b);

  /* The body half alone: source faces with the finish applied and the old head dropped. The bake's
     kit cross-check compares THIS against rig 6's own idle pose, because the head is expected to
     differ and the body is not. */
  function bodyFaces(build){
    const b=buildOf(build), bind=S.bindOf(b);
    let source=b.preset==='fisher'
      ? A.create({build:b,skeletonWorld:S.skeletonWorld(b),bindMesh:bind.faces})
      : bind.faces;
    source=F.body(source,b,bind);
    return source.filter(f=>!oldHead.has(f.part)&&f.part!=='inseam');
  }

  /* Body + the study head, rebound rigidly to the head bone. Mirrors cast-engine.js create(). */
  function composed(build){
    const b=buildOf(build), bind=S.bindOf(b);
    const hi=bind.bones.findIndex(x=>x.id==='head');
    if(hi<0)throw Error('Character face composition: no bone id "head" in the bind skeleton.');
    const hc=bind.bones[hi].p;
    const headBuild={...b,headSize:R.propsOf(b).headK};
    const head=H.createHead(headBuild,hc).map(f=>({...f,bone:f.v.map(()=>[[hi,1]])}));
    return bodyFaces(b).concat(head);
  }

  /* Rig 7's own untouched export, kept under a name of its own so the guards can still address the
     ORIGINAL premise — "rig 7 re-expresses rig 6's build" — after the face layer exists. That
     equality is not weakened by this module; it is simply no longer the same call the bake reads. */
  const baseBindMesh=build=>S.bindMesh(buildOf(build));

  root.CharacterFaceComposition={composed,bodyFaces,baseBindMesh,oldHeadParts:[...oldHead],
    revision:'face-composition-1 (pass05 finish + pass06 head)'};
})(globalThis);
