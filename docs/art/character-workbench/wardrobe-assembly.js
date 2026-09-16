/* Editor/review only. Resolves the SAME PascalCase clothing/recipe DTOs as Core.
   The mesh stream here is a measured authoring proof, not a Unity runtime renderer. */
(function(root){
  'use strict';
  const fitId='fit.character_fisher_study_v1', R=root.CharacterIso6;
  const catalogue=root.CharacterWardrobeCatalog;
  const clone=value=>JSON.parse(JSON.stringify(value));
  const fail=message=>{throw Error('Wardrobe: '+message);};
  const inverse=(m,p)=>[m[0]*p[0]+m[1]*p[1]+m[2]*p[2],m[3]*p[0]+m[4]*p[1]+m[5]*p[2],m[6]*p[0]+m[7]*p[1]+m[8]*p[2]];
  const cache=new WeakMap();
  const expectedBones=root.CharacterIso7.bindOf(R.resolveBuild({build:{preset:'fisher'}})).bones;
  const slots=new Set(['headwear','top','bottom','outerwear','footwear','accessory']);
  // Mirrors Core.CharacterClothingValidation at the JSON authoring boundary. Additional
  // exact-Fisher mesh checks follow this structural contract; they are not shared DTO rules.
  function contractChecks(errors){
    const text=(v,where)=>{if(typeof v!=='string'||!v.trim())errors.push(where+' is missing');};
    const array=(v,where)=>{if(v==null)return [];if(!Array.isArray(v)){errors.push(where+' must be an array');return [];}return v;};
    const strings=(v,where,required=false)=>{const set=new Set();for(const value of array(v,where)){
      if(typeof value!=='string'||!value.trim())errors.push(where+' contains an empty entry');
      else if(set.has(value))errors.push(where+' repeats '+value);else set.add(value);
    }if(required&&!set.size)errors.push(where+' must not be empty');return set;};
    const version=(v,where)=>{if(v!==1)errors.push(where+' has unsupported schema version '+v);};
    const id=(v,prefix,where)=>{if(typeof v!=='string'||v.trim()!==v||!new RegExp('^'+prefix+'\\.[a-z0-9]+(_[a-z0-9]+)*$').test(v))errors.push(where+' needs stable '+prefix+'.snake_case ID');};
    return {text,array,strings,version,id};
  }
  function validateCatalogue(data=catalogue){
    const errors=[],{text,array,strings,version,id}=contractChecks(errors);
    if(!data){errors.push('catalogue is missing');return errors;}
    version(data.SchemaVersion,'catalogue');const fits=new Map();
    for(const fit of array(data.Fits,'Fits')){
      if(!fit){errors.push('missing fit');continue;}const where='fit '+fit.Id;
      version(fit.SchemaVersion,where);id(fit.Id,'fit',where);
      if(fits.has(fit.Id))errors.push('duplicate fit ID '+fit.Id);else fits.set(fit.Id,fit);
      if(typeof fit.SourceRigSha256!=='string'||fit.SourceRigSha256.length!==64||!/^[a-f0-9]{64}$/.test(fit.SourceRigSha256))errors.push(where+' needs lowercase SHA-256');
      id(fit.SkeletonId,'skeleton',where);strings(fit.BoneIds,where+' BoneIds',true);strings(fit.SourceSectionIds,where+' SourceSectionIds',true);
      strings(fit.CoverageTags,where+' CoverageTags');for(const slot of strings(fit.RequiredSlots,where+' RequiredSlots'))if(!slots.has(slot))errors.push('unknown required slot '+slot);
    }
    if(!fits.size)errors.push('catalogue needs at least one explicit fit');const garmentIds=new Set();
    for(const garment of array(data.Garments,'Garments')){
      if(!garment){errors.push('missing garment');continue;}const where='garment '+garment.Id;
      version(garment.SchemaVersion,where);id(garment.Id,'garment',where);
      if(garmentIds.has(garment.Id))errors.push('duplicate garment ID '+garment.Id);garmentIds.add(garment.Id);
      text(garment.DisplayName,where+' DisplayName');if(!slots.has(garment.Category)&&garment.Category!=='outfit')errors.push(where+' has unknown category');
      if(garment.Price!=null&&(!Number.isInteger(garment.Price)||garment.Price<0))errors.push(where+' price must be a nonnegative integer');
      strings(garment.SellerIds,where+' SellerIds');for(const slot of strings(garment.OccupiedSlots,where+' OccupiedSlots',true))if(!slots.has(slot))errors.push('unknown slot '+slot);
      const coverage=strings(garment.CoverageTags,where+' CoverageTags'),tags=strings(garment.CompatibilityTags,where+' CompatibilityTags'),blocked=strings(garment.IncompatibleTags,where+' IncompatibleTags');
      for(const tag of tags)if(blocked.has(tag))errors.push(where+' both declares and blocks tag '+tag);
      const fitIds=new Set();
      for(const variant of array(garment.Fits,where+' Fits')){
        if(!variant){errors.push(where+' has missing fit variant');continue;}
        if(fitIds.has(variant.FitId))errors.push(where+' repeats fit '+variant.FitId);fitIds.add(variant.FitId);
        const sectionIds=strings(variant.SourceSectionIds,where+' SourceSectionIds',true),fit=fits.get(variant.FitId);
        if(!fit){errors.push(where+' references unknown fit '+variant.FitId);continue;}
        for(const section of sectionIds)if(!array(fit.SourceSectionIds,'fit SourceSectionIds').includes(section))errors.push('unknown source section '+section);
        for(const tag of coverage)if(!array(fit.CoverageTags,'fit CoverageTags').includes(tag))errors.push('unknown coverage '+tag);
      }
      if(!fitIds.size)errors.push(where+' needs fitted geometry');const colourIds=new Set();
      for(const colour of array(garment.Colourways,where+' Colourways')){
        if(!colour){errors.push(where+' has missing colourway');continue;}
        text(colour.Id,where+' colourway ID');text(colour.DisplayName,where+' colourway DisplayName');
        if(colourIds.has(colour.Id))errors.push(where+' repeats colourway '+colour.Id);colourIds.add(colour.Id);
        const materials=new Set();for(const role of array(colour.MaterialRoles,where+' MaterialRoles')){
          if(!role){errors.push(where+' has missing material role');continue;}
          text(role.SourceMaterial,where+' SourceMaterial');text(role.Role,where+' material Role');text(role.RampId,where+' RampId');
          if(materials.has(role.SourceMaterial))errors.push(where+' maps a source material twice');materials.add(role.SourceMaterial);
        }
      }
      if(!colourIds.size)errors.push(where+' needs named colourway');
    }
    return errors;
  }
  function validateRecipe(recipe,data=catalogue){
    const errors=validateCatalogue(data),{array,version,id}=contractChecks(errors);
    if(!recipe){errors.push('recipe is missing');return errors;}version(recipe.SchemaVersion,'recipe');
    if(!recipe.Identity){errors.push('identity is missing');return errors;}
    id(recipe.Identity.BuildId,'char_build','identity');id(recipe.Identity.FitId,'fit','identity');
    const fit=array(data?.Fits,'Fits').find(f=>f&&f.Id===recipe.Identity.FitId);if(!fit)errors.push('unknown identity fit');
    const ids=new Set(),usedSlots=new Set(),usedSections=new Set(),selected=[];
    for(const choice of array(recipe.Garments,'recipe Garments')){
      if(!choice){errors.push('missing garment selection');continue;}
      if(ids.has(choice.GarmentId))errors.push('duplicate garment '+choice.GarmentId);ids.add(choice.GarmentId);
      const garment=array(data?.Garments,'Garments').find(g=>g&&g.Id===choice.GarmentId);
      if(!garment){errors.push('unknown garment '+choice.GarmentId);continue;}
      if(!array(garment.Colourways,'Colourways').some(c=>c&&c.Id===choice.ColourwayId))errors.push('unknown colourway '+choice.ColourwayId);
      const variant=array(garment.Fits,'Fits').find(f=>f&&f.FitId===recipe.Identity.FitId);
      if(!variant)errors.push('garment lacks fit '+garment.Id);else for(const section of array(variant.SourceSectionIds,'SourceSectionIds')){
        if(usedSections.has(section))errors.push('duplicate selected source section '+section);usedSections.add(section);
      }
      for(const slot of array(garment.OccupiedSlots,'OccupiedSlots')){if(usedSlots.has(slot))errors.push('occupied slot conflict '+slot);usedSlots.add(slot);}
      for(const previous of selected)for(const [a,b] of [[garment,previous],[previous,garment]])for(const tag of array(a.IncompatibleTags,'IncompatibleTags'))
        if(array(b.CompatibilityTags,'CompatibilityTags').includes(tag))errors.push('incompatible tag '+tag);
      selected.push(garment);
    }
    for(const slot of array(fit?.RequiredSlots,'RequiredSlots'))if(!usedSlots.has(slot))errors.push('missing required fitted slot '+slot);
    return errors;
  }
  function coverageTag(face){return /^fore_[LR]$/.test(face.part)&&face.mat==='skin'?'identity.forearms':null;}
  function sectionOf(f){
    if(/^(boot|foot)/.test(f.part))return 'fisher_study.boots';
    if(/^(thigh|shin)_/.test(f.part)||['pelvis','knee_patch'].includes(f.part)||(f.part==='torso'&&f.mat==='over'))return 'fisher_study.trousers';
    if(['bib','strap_front','strap_back','strap_shoulder','buckle','bib_pocket','bib_pocket_welt'].includes(f.part))return 'fisher_study.bib';
    if(f.part==='collar'||(f.part==='torso'&&f.mat==='shirt')||/^upper_[LR]$/.test(f.part))return 'fisher_study.shirt_short';
    if(['head','neck'].includes(f.part)||/^(fore|hand|thumb)_[LR]$/.test(f.part))return 'identity';
    return fail('unclassified source face '+f.part+'/'+f.mat);
  }
  function assertBase(base){
    // A matching name alone is insufficient. Structural choices are not free-fit sliders.
    const expected=R.resolveBuild({build:{preset:'fisher'}});
    if(base.key!=='fisher')fail('unsupported body fit: '+base.key);
    for(const axis of ['sex','age','garment','height','weight','headSize','hat','hairStyle','beard'])
      if(base.build[axis]!==expected[axis])fail('unsupported structural change: '+axis);
    const fit=catalogue.Fits.find(f=>f.Id===fitId);
    if(!fit||JSON.stringify(fit.BoneIds)!==JSON.stringify(base.bind.bones.map(b=>b.id)))fail('fit bone IDs/order differ');
    for(let i=0;i<expectedBones.length;i++){
      const expected=[...expectedBones[i].p,...expectedBones[i].R],actual=[...base.bind.bones[i].p,...base.bind.bones[i].R];
      if(expected.some((x,j)=>!Number.isFinite(actual[j])||Math.abs(x-actual[j])>1e-8))fail('fit rest transform differs '+expectedBones[i].id);
    }
    return fit;
  }
  function sections(base){
    assertBase(base);
    if(cache.has(base))return cache.get(base);
    const out={identity:[]};
    for(const f of base.faces){const id=sectionOf(f);(out[id]??=[]).push(f);}
    // A separate authored fitted sleeve stream. Enlarge radially about the actual forearm
    // bind axis; retain the existing endpoint weights and explicit head/hand attachments.
    const sleeves=out.identity.filter(f=>coverageTag(f)).map(f=>{
      const a=base.bind.bones.find(b=>b.id==='elbow_'+f.part.at(-1)).p;
      const b=base.bind.bones.find(b=>b.id==='elbow_'+f.part.at(-1)+'_tip').p;
      const axis=b.map((x,i)=>x-a[i]),length2=axis.reduce((s,x)=>s+x*x,0);
      return {...f,mat:'sleeve',v:f.v.map(p=>{const t=p.reduce((s,x,i)=>s+(x-a[i])*axis[i],0)/length2;
        const c=a.map((x,i)=>x+axis[i]*t);return p.map((x,i)=>c[i]+(x-c[i])*1.16);})};
    });
    out['fisher_study.shirt_long']=out['fisher_study.shirt_short'].concat(sleeves);
    for(const id of catalogue.Fits[0].SourceSectionIds)if(!out[id]?.length)fail('empty source section '+id);
    cache.set(base,out);return out;
  }
  function resolveRamp(id){
    const axes={skin:R.SKINS,hair:R.HAIRS,outfit:R.OUTFITS,shirt:R.SHIRTS,hatCol:R.HATCOLS,apronCol:R.APRONS,eyes:R.EYES};
    const m=/^ramp\.([^.]+)\.([^.]+)$/.exec(id),r=m&&axes[m[1]]?.[m[2]];
    if(!r)fail('unknown palette ramp '+id);
    const ramp=typeof r==='string'?[r]:r;
    return ramp.length>4?[ramp[0],ramp[2],ramp[3],ramp[5]||ramp.at(-1)]:ramp.slice();
  }
  function remapFaces(faces,sourceBones,targetBones){
    const used=new Set(faces.flatMap(f=>f.bone.flatMap(ws=>ws.map(([i])=>i))));
    const index=new Map(targetBones.map((b,i)=>[b.id,i])),map=new Map();
    for(const source of used){
      const b=sourceBones[source],target=index.get(b.id);if(target==null)fail('missing target bone '+b.id);
      const t=targetBones[target],expected=[...b.p,...b.R],actual=[...t.p,...t.R];
      if(expected.some((v,i)=>!Number.isFinite(v)||!Number.isFinite(actual[i])||Math.abs(v-actual[i])>1e-8))fail('rest mismatch '+b.id);
      map.set(source,target);
    }
    return faces.map(f=>({...f,bone:f.bone.map(ws=>ws.map(([i,w])=>[map.get(i),w]))}));
  }
  function prepare(f,targetBones){return {...f,local:f.v.map((p,k)=>f.bone[k].map(([ix,w])=>({ix,w,
    p:inverse(targetBones[ix].R,p.map((v,j)=>v-targetBones[ix].p[j]))})))};}
  function create(base,recipe){
    const errors=validateRecipe(recipe);if(errors.length)fail(errors.join('; '));
    const fit=assertBase(base),source=sections(base);
    if(recipe?.SchemaVersion!==1||catalogue.SchemaVersion!==1)fail('unsupported schema version');
    const identity=recipe.Identity;
    if(!identity||identity.BuildId!=='char_build.fisher'||identity.FitId!==fit.Id)fail('unsupported recipe identity/fit');
    for(const key of ['Skin','Hair','Eyes'])if(identity[key]!==base.build[key.toLowerCase()])fail('identity differs from base '+key);
    if(!Array.isArray(recipe.Garments))fail('missing garment selections');
    const usedSlots=new Set(),covered=new Set(),selected=[],ids=new Set();
    for(const selection of recipe.Garments){
      if(ids.has(selection.GarmentId))fail('duplicate garment '+selection.GarmentId);ids.add(selection.GarmentId);
      const garment=catalogue.Garments.find(g=>g.Id===selection.GarmentId);if(!garment)fail('unknown garment '+selection.GarmentId);
      const variant=garment.Fits.find(f=>f.FitId===fit.Id);if(!variant)fail('garment lacks fit '+garment.Id);
      const colourway=garment.Colourways.find(c=>c.Id===selection.ColourwayId);if(!colourway)fail('unknown colourway '+selection.ColourwayId);
      for(const slot of garment.OccupiedSlots){if(usedSlots.has(slot))fail('occupied slot conflict '+slot);usedSlots.add(slot);}
      for(const tag of garment.CoverageTags||[]){if(!(fit.CoverageTags||[]).includes(tag))fail('unknown coverage '+tag);covered.add(tag);}
      selected.push({garment,variant,colourway});
    }
    // This source has no bare trunk/legs/feet. Decline incomplete combinations explicitly.
    for(const slot of fit.RequiredSlots||[])if(!usedSlots.has(slot))fail('missing required fitted slot '+slot);
    const compatibility=new Set(selected.flatMap(s=>s.garment.CompatibilityTags||[]));
    for(const s of selected)for(const tag of s.garment.IncompatibleTags||[])if(compatibility.has(tag))fail('incompatible tag '+tag);
    const faces=source.identity.filter(f=>!covered.has(coverageTag(f))),mats={...base.mats};
    const drawMaterials=new Map();
    for(const {garment,variant,colourway} of selected.sort((a,b)=>a.garment.Id.localeCompare(b.garment.Id))){
      for(const sectionId of variant.SourceSectionIds){
        if(!source[sectionId])fail('unknown source section '+sectionId);
        for(const face of remapFaces(source[sectionId],base.bind.bones,base.bind.bones)){
          const binding=(colourway.MaterialRoles||[]).find(r=>r.SourceMaterial===face.mat);
          if(!base.mats[face.mat])fail('missing material metadata '+face.mat);
          const material=binding?{...base.mats[face.mat],ramp:resolveRamp(binding.RampId)}:{...base.mats[face.mat]},signature=JSON.stringify(material);
          let key=drawMaterials.get(signature);
          if(!key){key='wardrobe_'+drawMaterials.size;drawMaterials.set(signature,key);mats[key]=material;}
          faces.push(prepare({...face,mat:key,wardrobeGarmentId:garment.Id,wardrobeSectionId:sectionId,wardrobeRole:binding?.Role||''},base.bind.bones));
        }
      }
    }
    const used=Array.from(new Set(faces.map(f=>f.mat)));if(used.length>16)fail('production material ramp cap exceeded: '+used.length);
    return {...base,faces,mats,recipe:clone(recipe),wardrobe:{FitId:fit.Id,OccupiedSlots:[...usedSlots].sort(),CoverageTags:[...covered].sort(),UsedMaterials:used}};
  }
  root.CharacterWardrobeProof={create,sections,remapFaces,resolveRamp,coverageTag,validateCatalogue,validateRecipe,
    presets:root.CharacterWardrobeRecipes.Presets,Catalog:catalogue};
})(globalThis);
