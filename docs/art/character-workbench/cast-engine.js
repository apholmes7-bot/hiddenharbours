(function(root){
  const R=root.CharacterIso6, S=root.CharacterIso7, H=root.CharacterHeadStudy;
  const oldHead=new Set(['head','hair','eye','brow','ear','nose','mouth']);
  const mix=(a,b,t)=>'#'+[1,3,5].map(i=>Math.round(parseInt(a.slice(i,i+2),16)*(1-t)+parseInt(b.slice(i,i+2),16)*t).toString(16).padStart(2,'0')).join('');
  const reduced=m=>({ramp:m.ramp.length>4?[m.ramp[0],m.ramp[2],m.ramp[3],m.ramp[5]||m.ramp.at(-1)]:m.ramp,...(m.idx!=null?{idx:Math.min(m.idx,3)}:{})});
  function materials(b){
    const out=Object.fromEntries(Object.entries(R.makeMats(b).MATS).map(([k,m])=>[k,reduced(m)]));
    if(b.preset==='fisher')Object.assign(out,Object.fromEntries(Object.entries(root.CharacterArtStudy.ramps).map(([k,ramp])=>[k,{ramp,...(ramp.length===1?{idx:0}:{})}])));
    if(b.skin==='fair')out.skin={ramp:root.CharacterArtStudy.ramps.skin};
    if(b.hair==='blond')out.hair={ramp:root.CharacterArtStudy.ramps.hair};
    out.skin={...out.skin,paintGain:.6,paintBias:1.95};
    out.noseLight={ramp:out.skin.ramp,idx:2};out.noseShadow={ramp:out.skin.ramp,idx:1};
    out.beard={ramp:out.hair.ramp};out.beardD=out.beard;
    out.stub={...out.skin,ramp:out.skin.ramp.map(c=>mix(c,out.hair.ramp[0],.18))};
    return out;
  }
  const mul=(m,p)=>[m[0]*p[0]+m[3]*p[1]+m[6]*p[2],m[1]*p[0]+m[4]*p[1]+m[7]*p[2],m[2]*p[0]+m[5]*p[1]+m[8]*p[2]];
  const inverse=(m,p)=>[m[0]*p[0]+m[1]*p[1]+m[2]*p[2],m[3]*p[0]+m[4]*p[1]+m[5]*p[2],m[6]*p[0]+m[7]*p[1]+m[8]*p[2]];
  function create(key){
    const build=R.resolveBuild({build:{preset:key}}),bind=S.bindOf(build),skeleton=S.skeletonWorld(build);
    const hi=bind.bones.findIndex(x=>x.id==='head'),hc=bind.bones[hi].p;
    // Preserve each recipe's own skeleton and clothing. Fisher uses the authored body study.
    const source=key==='fisher'?root.CharacterArtStudy.create({build,skeletonWorld:skeleton,bindMesh:bind.faces}):bind.faces;
    const body=source.filter(f=>!oldHead.has(f.part)&&f.part!=='inseam');
    const headBuild={...build,headSize:R.propsOf(build).headK};
    const head=H.createHead(headBuild,hc).map(f=>({...f,bone:f.v.map(()=>[[hi,1]])}));
    const faces=body.concat(head).map(f=>({...f,local:f.v.map((p,k)=>f.bone[k].map(([ix,w])=>({ix,w,p:inverse(bind.bones[ix].R,p.map((v,j)=>v-bind.bones[ix].p[j]))})))}));
    return {key,build,headBuild,faces,hi,bind,mats:materials(build),colours:H.colours(build)};
  }
  function pose(character,anim,u){
    const solved=S.solve(anim,u,character.build,{bonesOnly:true}),bones=solved.bones;
    const faces=character.faces.map(f=>({...f,v:f.local.map(weights=>{const out=[0,0,0];for(const v of weights){const bone=bones[v.ix],p=mul(bone.R,v.p);for(let j=0;j<3;j++)out[j]+=(p[j]+bone.p[j])*v.w;}return out;})}));
    return {faces,bones,head:bones[character.hi].p};
  }
  root.CastViewerEngine={create,pose,materials,cast:R.CAST,animations:R.ANIMS};
})(globalThis);
