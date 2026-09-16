/* Review-only tailoring; source rigs, skeletons and contact anchors remain immutable.
   Every shape/value decision is authored per preset in character-finish.json. */
(function(root){
  'use strict';
  const C=root.CharacterFinishConfig;
  const clamp=x=>Math.max(0,Math.min(1,x)),smooth=x=>{x=clamp(x);return x*x*(3-2*x);};
  const mix=(a,b,t)=>a+(b-a)*t;
  const fail=message=>{throw Error('Character finish: '+message);};
  const garmentMaterials=new Set(['over','overD','overL','shirt','shirtD','shirtL','sleeve','collar','boot','bootL','sole','belt','brass','hat','hatD','apron','apronD']);
  const protectedPart=part=>C.ProtectedParts.includes(part)||C.ProtectedPartPrefixes.some(p=>part.startsWith(p));
  function recipe(build,bind){
    if(C.SchemaVersion!==1)fail('unsupported config version');
    const r=C.Presets[build.preset];if(!r)fail('unsupported preset '+build.preset);
    for(const axis of ['Sex','Age','Garment'])if(build[axis.toLowerCase()]!==r[axis])fail('unsupported '+axis+' for '+build.preset);
    for(const axis of ['height','weight','headSize'])if(build[axis]!==1)fail('unsupported proportion '+axis+' for '+build.preset);
    if(bind&&JSON.stringify(bind.bones.map(b=>b.id))!==JSON.stringify(C.BoneLayouts[r.BoneLayout]))fail('unexpected bone layout for '+build.preset);
    for(const key of ['TorsoChestWidth','TorsoWaistWidth','TorsoDepth','PelvisWidth','PelvisDepth','UpperRadius','ForeRadius','ThighRadius','ShinRadius','BootUpperRadius','SkirtHemWidth','ApronWidth'])
      if(!Number.isFinite(r[key])||r[key]<.8||r[key]>1.35)fail('out-of-range tailoring '+key);
    return r;
  }
  function body(source,build,bind){
    const r=recipe(build,bind),bones=Object.fromEntries(bind.bones.map(b=>[b.id,b]));
    const center=bones.torso.p,hip=bones.pelvis.p,neck=bones.neck.p;
    const torsoParts=new Set(['torso','collar','bib','strap_front','strap_back','strap_shoulder','buckle','bib_pocket','bib_pocket_welt']);
    const skirtFaces=source.filter(f=>f.part==='skirt'||f.part==='skirt_hem');
    const skirtMin=skirtFaces.length?Math.min(...skirtFaces.flatMap(f=>f.v.map(p=>p[2]))):0;
    const skirtMax=skirtFaces.length?Math.max(...skirtFaces.flatMap(f=>f.v.map(p=>p[2]))):1;
    function torso(p){
      const h=clamp((p[2]-hip[2])/Math.max(.01,neck[2]-hip[2]));
      const opening=1-smooth((h-.85)/.15),width=mix(r.TorsoWaistWidth,r.TorsoChestWidth,smooth(h/.75));
      return [center[0]+(p[0]-center[0])*mix(1,width,opening),center[1]+(p[1]-center[1])*mix(1,r.TorsoDepth,opening),p[2]];
    }
    function radial(p,id,scaleAt){
      const a=bones[id]?.p,b=bones[id+'_tip']?.p;if(!a||!b)fail('missing limb endpoints '+id);
      const axis=b.map((v,i)=>v-a[i]),length2=axis.reduce((s,v)=>s+v*v,0);if(length2<1e-10)fail('collapsed rest limb '+id);
      const t=p.reduce((s,v,i)=>s+(v-a[i])*axis[i],0)/length2,s=scaleAt(t),origin=a.map((v,i)=>v+axis[i]*t);
      return p.map((v,i)=>origin[i]+(v-origin[i])*s);
    }
    return source.map(f=>{
      if(protectedPart(f.part))return f;
      let transform=null;const side=f.part.at(-1);
      if(torsoParts.has(f.part))transform=torso;
      else if(f.part==='pelvis')transform=p=>[hip[0]+(p[0]-hip[0])*r.PelvisWidth,hip[1]+(p[1]-hip[1])*r.PelvisDepth,p[2]];
      else if(/^upper_[LR]$/.test(f.part))transform=p=>radial(p,'shoulder_'+side,()=>r.UpperRadius);
      else if(/^fore_[LR]$/.test(f.part))transform=p=>radial(p,'elbow_'+side,t=>mix(r.ForeRadius,1,smooth((t-.60)/.32)));
      else if(/^thigh_[LR]$/.test(f.part))transform=p=>radial(p,'hip_'+side,()=>r.ThighRadius);
      else if(/^shin_[LR]$/.test(f.part))transform=p=>radial(p,'knee_'+side,t=>mix(r.ShinRadius,1,smooth((t-.30)/.70)));
      else if(/^boot(_cuff)?_[LR]$/.test(f.part))transform=p=>radial(p,'ankle_'+side,t=>mix(r.BootUpperRadius,1,smooth(t)));
      else if(f.part==='knee_patch')transform=p=>radial(p,'knee_L',t=>mix(r.ShinRadius,1,smooth((t-.30)/.70)));
      else if(f.part==='skirt'||f.part==='skirt_hem')transform=p=>{
        const t=clamp((p[2]-skirtMin)/(skirtMax-skirtMin)),scale=mix(r.SkirtHemWidth,r.PelvisWidth,smooth(t));
        return [hip[0]+(p[0]-hip[0])*scale,center[1]+(p[1]-center[1])*mix(r.SkirtHemWidth,r.TorsoDepth,smooth(t)),p[2]];
      };
      else if(f.part==='apron'||f.part==='apron_hem')transform=p=>[center[0]+(p[0]-center[0])*r.ApronWidth,center[1]+(p[1]-center[1])*r.TorsoDepth,p[2]];
      // Cuffs, source-only parked inseams and any non-clothing attachment stay exact.
      const v=transform?f.v.map(transform):f.v;
      if(v.some(p=>p.some(x=>!Number.isFinite(x))))fail('nonfinite face '+f.part);
      let mat=f.mat;
      for(const map of r.MaterialRemaps||[])if(map.From===mat&&map.Parts.includes(f.part))mat=map.To;
      return v===f.v&&mat===f.mat?f:{...f,v,mat};
    });
  }
  function materials(source,build){
    const r=recipe(build),out={...source};
    for(const [name,palette] of Object.entries({...C.CommonMaterials,...r.Materials})){
      if(!garmentMaterials.has(name))fail('identity material or unknown garment role cannot be tailored '+name);
      if(!out[name])continue;
      const ramp=C.Palettes[palette];if(!ramp||ramp.length!==4||ramp.some(x=>!/^#[0-9a-f]{6}$/i.test(x)))fail('invalid palette '+palette);
      out[name]={...out[name],ramp:ramp.slice(),paintGain:C.PaintGain,paintBias:C.PaintBias};
    }
    return out;
  }
  root.CharacterFinish={body,materials,protectedPart};
})(globalThis);
