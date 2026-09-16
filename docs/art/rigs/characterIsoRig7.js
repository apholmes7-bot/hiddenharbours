/* Hidden Harbours — ISO CHARACTER rig, PASS 7. The export surface for a SKINNED character.
   ---------------------------------------------------------------------------------------------
   Brief 2026-09-09 (characterIsoRig7): one mesh + one skeleton + tiny clips, instead of one mesh
   per animation frame. Rig 7 does not change what the character looks like. It reads rig 6's
   pose() and re-states rig 6's facesOf() as a BIND MESH whose every vertex belongs to a bone, and
   the same pose() as per-frame BONE TRANSFORMS. Loads after Art/characterIsoRig6.js (+ head, eye).
   Exposes globalThis.CharacterIso7 — everything CharacterIso6 exports, plus:

     skeleton(build)                 -> [{ id, parent, rest:{pos,rot,rotEuler} }]  parent = index, -1 at root; rest LOCAL
     skeletonWorld(build)            -> the same bones in the figure frame (bindposes = inverse of these)
     bindMesh(build)                 -> rig 6 faces { v, mat, b, db } + part + bone[k] = [[boneIndex, weight], ...]
                                        per vertex: one pair at weight 1, two on the BLENDED rings
     clip(anim, build, frames, opts) -> { frames, ms, loop, bones:[ids], parked, tracks:[{u, ms, bones:{id:{pos,rot,rotEuler}}, face, tool}] }
     clips(build, opts) / exportBuild(build) -> the whole set for one preset, one JSON
     goldenDiff(anim, u, build, opts) -> the max vertex distance in metres (the brief's checker)
     goldenDiffDetail(anim, u, build, opts) -> { max, at, faces, compared, parked, err, ok }
     goldenRows(build) / goldenRow(row, build) / goldenReport(build)          the whole flipbook set
     renderSkinned(dir, opts) / diffPixels(dir, opts)   the bind mesh posed by clip() through rig 6's
                                        own facet shader, and the pixel count where it differs (0)

   FRAME. Right-handed, metres: +x right (curb), +y forward (nose), +z up. Origin = the cell pivot
   on the ground under the figure (the point every sheet already pins to). No camera, no facing, no
   deck-rock: those stay live transforms (ADR 0024). Unity (left-handed, y-up): pos (x, z, y),
   quaternion (-x, -z, -y, w).

   ROT IS A UNIT QUATERNION [x,y,z,w]. The brief wrote rot:[x,y,z]; every transform also carries
   rotEuler [x,y,z] in degrees, intrinsic Z-Y-X (yaw about z, then pitch about y, then roll about x),
   for readers that want the triple. The quaternion is the truth: the tube frames pass through vertical
   on every walk cycle, where any Euler order has a pole. Quaternions land in Unity after the axis swap.

   BONE IDS follow the brief: pelvis, torso (the spine), neck, head; per side hip, knee, ankle, foot,
   shoulder, elbow, wrist, hand; tool_L/R (+ _1, _2 for the bend), carry_L/R/mid. hip_L is the bone the
   thigh tube hangs from (it sits at the hip joint), knee_L the shin tube, ankle_L the boot shaft,
   wrist_L the cuff ball (an empty bone on garments without a cuff, so the count is fixed per garment).

   WHY THE SKELETON HAS TIP BONES. Rig 6 draws every limb as limb(A, B): a ring at A and a ring at
   B in the frame of the A->B axis. The lengths are NOT constant — the drawn shoulder is dropped
   0.098 m and the drawn elbow 0.039 m, the knee splays on the ladder, the shin stretches for a child
   in walk, the boot top slides along the shin with its tilt. So each tube is a bone at A (rotation =
   the tube's own frame) plus a TIP bone at B, a child with a pure translation along local z. The A
   ring binds to the bone, the B ring to the tip; both weights are 1.0. Between frames the tip lerps,
   which is exactly how a stretching tube should blend.

   THE ONE TORSO BONE. Every torso piece — lathes, bib arcs, straps, buckles, belt, vest, shawl,
   collar, shoulder caps — goes through one torsoXf (sway · list · lean+stoop · yaw), so the torso is
   ONE rigid body in rig 6 and one bone here. There is no torso twist to split; the stoop is inside
   the same rigid transform. The shoulders are NOT rigid with it (they yaw at 1/0.6 of the torso's
   yaw and breathe) — the arm chains hang off the torso bone with animated locals.

   2-WEIGHT VERTICES (the only ones; listed in BLENDED). Rings that rig 6 interpolates between two
   anchors: apron rows 1-4 (hem <-> torso), skirt rows 1-4 (hem <-> torso), the short sleeve's hem
   ring at 0.62 of the upper arm, the long sleeve's sleeve-end (0.80/0.90) and skin-start
   (0.76/0.86) rings of the forearm. Both bones of every such pair share one rotation, so linear
   blend skinning reproduces the lerp EXACTLY (proved by goldenDiff, not argued).

   THE INSEAM. Rig 6 omits the dark inseam panel on swim/tread/sleep/drive. The bind mesh cannot drop
   faces, so on those frames both inseam bones park at the pelvis centre, inside the pelvis lathe,
   where the z-buffer hides the collapsed box. goldenDiff reports those six faces as `parked`.

   WHAT A CLIP CANNOT CARRY. Rig 6's render() snaps the head centre to a pixel centre PER FACING
   (6.8, gridHead) before drawing; a camera-independent clip has no facing, so the exported head is
   the un-snapped pose() head (the golden rule is against pose(), as the brief says). The facet pass
   can re-apply the snap to the head bone in screen space, or accept <= 0.5 px of head jitter.
   renderSkinned() applies the snap so the side-by-side proof is pixel-exact.

   ROLL SEAMS. limb() picks its ring 'up' vector by whether the axis is within ~26 deg of vertical.
   Across that threshold the ring frame rolls (by 180 deg in the sagittal plane, less off it). Every
   keyframe is exact; a slerp between two such frames spins a 6-sided tube through the roll. The
   ring is 6-fold symmetric, so an extractor may add k·60 deg about local z to any tube bone whose
   quaternion is > 90 deg from the previous frame's — the bake is unchanged because the face SET is. */
(function (root) {
  const C6 = root.CharacterIso6;
  if(!C6){ console.warn('characterIsoRig7.js: load Art/characterIsoRig6.js (with head + eye rigs) first'); return; }
  /* nothing is rounded on the way out. Written to 1e-7 m the export still passes the 1e-4 rule, but
     z-buffer TIES then break differently from rig 6 — the hair shell's midline seam projects exactly
     through a pixel-centre column at E/W — and one ramp step flips on a quarter of the W frames. At
     full double precision renderSkinned() equals render() to the pixel at every facing tried. */
  const DEG = Math.PI/180, TOL = 1e-4;
  const ANIMS = C6.ANIMS, GARMENTS = C6.GARMENTS;
  const CARRY_OK = ['buckets','tray','helm','oars','pot'];

  /* ---------------- vectors · frames (column-major 3x3) · quaternions ---------------- */
  const I3 = [1,0,0, 0,1,0, 0,0,1];
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s];
  const v_norm=(a)=>{ const m=Math.hypot(a[0],a[1],a[2])||1; return [a[0]/m,a[1]/m,a[2]/m]; };
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  const v_dist=(a,b)=>Math.hypot(a[0]-b[0],a[1]-b[1],a[2]-b[2]);
  const mV =(R,v)=>[R[0]*v[0]+R[3]*v[1]+R[6]*v[2], R[1]*v[0]+R[4]*v[1]+R[7]*v[2], R[2]*v[0]+R[5]*v[1]+R[8]*v[2]];
  const mTV=(R,v)=>[R[0]*v[0]+R[1]*v[1]+R[2]*v[2], R[3]*v[0]+R[4]*v[1]+R[5]*v[2], R[6]*v[0]+R[7]*v[1]+R[8]*v[2]];
  const mM =(A,B)=>{ const o=[]; for(let c=0;c<3;c++) o.push(...mV (A,[B[c*3],B[c*3+1],B[c*3+2]])); return o; };
  const mTM=(A,B)=>{ const o=[]; for(let c=0;c<3;c++) o.push(...mTV(A,[B[c*3],B[c*3+1],B[c*3+2]])); return o; };
  const mMT=(A,B)=>{ const o=new Array(9); for(let j=0;j<3;j++) for(let i=0;i<3;i++){ let s=0; for(let k=0;k<3;k++) s+=A[k*3+i]*B[k*3+j]; o[j*3+i]=s; } return o; };
  const rotXm=(a)=>{ const c=Math.cos(a), s=Math.sin(a); return [1,0,0, 0,c,s, 0,-s,c]; };
  const rotZm=(a)=>{ const c=Math.cos(a), s=Math.sin(a); return [c,s,0, -s,c,0, 0,0,1]; };
  function quatOf(R){
    const m00=R[0],m10=R[1],m20=R[2], m01=R[3],m11=R[4],m21=R[5], m02=R[6],m12=R[7],m22=R[8];
    const tr=m00+m11+m22; let x,y,z,w;
    if(tr>0){ const s=Math.sqrt(tr+1)*2; w=0.25*s; x=(m21-m12)/s; y=(m02-m20)/s; z=(m10-m01)/s; }
    else if(m00>m11 && m00>m22){ const s=Math.sqrt(1+m00-m11-m22)*2; w=(m21-m12)/s; x=0.25*s; y=(m01+m10)/s; z=(m02+m20)/s; }
    else if(m11>m22){ const s=Math.sqrt(1+m11-m00-m22)*2; w=(m02-m20)/s; x=(m01+m10)/s; y=0.25*s; z=(m12+m21)/s; }
    else { const s=Math.sqrt(1+m22-m00-m11)*2; w=(m10-m01)/s; x=(m02+m20)/s; y=(m12+m21)/s; z=0.25*s; }
    return [x,y,z,w];
  }
  function matOf(q){ const n=Math.hypot(q[0],q[1],q[2],q[3])||1, x=q[0]/n, y=q[1]/n, z=q[2]/n, w=q[3]/n;
    return [1-2*(y*y+z*z), 2*(x*y+z*w), 2*(x*z-y*w),  2*(x*y-z*w), 1-2*(x*x+z*z), 2*(y*z+x*w),  2*(x*z+y*w), 2*(y*z-x*w), 1-2*(x*x+y*y)]; }
  const rnd=(a)=>a.slice(), rndR=(a)=>a.slice();

  /* rig 6's affine helpers, verbatim, so torsoXf / pelvXf are rebuilt from P the same way facesOf does */
  const ID=(p)=>p, TX=(dx,dy,dz)=>(p)=>[p[0]+dx,p[1]+dy,p[2]+dz];
  const rotZ=(a)=>{ const c=Math.cos(a), s=Math.sin(a); return (p)=>[p[0]*c-p[1]*s, p[0]*s+p[1]*c, p[2]]; };
  const pitchX=(a,z0)=>{ const c=Math.cos(a), s=Math.sin(a); return (p)=>[p[0], p[1]*c+(p[2]-z0)*s, z0-p[1]*s+(p[2]-z0)*c]; };
  const chain=(...fs)=>(p)=>{ for(let i=fs.length-1;i>=0;i--) p=fs[i](p); return p; };
  /* the rigid frame of an affine map: columns are where the basis vectors land */
  const frameOf=(xf,c)=>{ const o=xf([0,0,0]), R=[]; for(const e of [[1,0,0],[0,1,0],[0,0,1]]) R.push(...v_sub(xf(e),o)); return { p:xf(c), R }; };
  /* the ring frame limb() uses: local z along the tube, local x/y the ring plane */
  const limbFrame=(A,B)=>{ const ax=v_norm(v_sub(B,A)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const rr=v_norm(v_cross(ax,up)), uu=v_cross(ax,rr); return [...rr,...uu,...ax]; };
  function profR(prof, dz, which){
    if(dz<=prof[0][0]) return prof[0][which];
    for(let i=0;i+1<prof.length;i++){ const a=prof[i], b2=prof[i+1];
      if(dz<=b2[0]){ const t=(dz-a[0])/((b2[0]-a[0])||1); return a[which]+(b2[which]-a[which])*t; } }
    return prof[prof.length-1][which];
  }

  /* ---------------- rig 6's primitives, emitting a skin per vertex ----------------
     bone[k] is the skin of vertex k: [[boneIndex, weight], ...] — one pair (weight 1) everywhere but
     the rings in BLENDED, which carry two pairs summing to 1 */
  const skinOf=(s)=>Array.isArray(s) ? s.map(p=>[p[0],p[1]]) : [[s,1]];
  function face(v, mat, b, db, part, sk){ return { v, mat, b:b||0, db:db||0, part, bone: sk.map(skinOf) }; }
  function tlathe(F, c, prof, mat, b, db, xf, seg, part, ringSk){
    xf=xf||ID; seg=seg||8;
    const cs=[], sn=[]; for(let k=0;k<seg;k++){ const a=(k+0.5)/seg*Math.PI*2; cs.push(Math.cos(a)); sn.push(Math.sin(a)); }
    const rings=prof.map(([dz,rx,ry])=>{ const o=[]; for(let k=0;k<seg;k++) o.push(xf([c[0]+cs[k]*rx, c[1]+sn[k]*ry, c[2]+dz])); return o; });
    const sk=prof.map((_,i)=>ringSk(i));
    for(let i=0;i+1<rings.length;i++){ const r0=rings[i], r1=rings[i+1];
      for(let k=0;k<seg;k++){ const k2=(k+1)%seg; F.push(face([r0[k],r0[k2],r1[k2],r1[k]],mat,b,db,part,[sk[i],sk[i],sk[i+1],sk[i+1]])); } }
    const n=rings.length-1;
    F.push(face(rings[n].slice(),mat,b,db,part,rings[n].map(()=>sk[n])));
    F.push(face(rings[0].slice().reverse(),mat,b,db,part,rings[0].map(()=>sk[0])));
  }
  function tarc(F, c, prof, mat, b, db, xf, a0, a1, n, part, ringSk){
    xf=xf||ID; n=n||5;
    const rings=prof.map(([dz,rx,ry])=>{ const o=[]; for(let k=0;k<=n;k++){ const a=a0+(a1-a0)*k/n; o.push(xf([c[0]+Math.cos(a)*rx, c[1]+Math.sin(a)*ry, c[2]+dz])); } return o; });
    const sk=prof.map((_,i)=>ringSk(i));
    for(let i=0;i+1<rings.length;i++){ const r0=rings[i], r1=rings[i+1];
      for(let k=0;k<n;k++) F.push(face([r0[k],r0[k+1],r1[k+1],r1[k]],mat,b,db,part,[sk[i],sk[i],sk[i+1],sk[i+1]])); }
  }
  function tlimb(F, A, B2, r0, r1, mat, b, seg, dbv, part, bA, bB){
    seg=seg||6; dbv=(dbv==null? -0.05 : dbv);
    const ax=v_norm(v_sub(B2,A)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const rr=v_norm(v_cross(ax,up)), uu=v_cross(ax,rr);
    const ring=(P,rad)=>{ const o=[]; for(let k=0;k<seg;k++){ const a=(k+0.5)/seg*Math.PI*2;
      o.push(v_add(P, v_add(v_mul(rr,Math.cos(a)*rad), v_mul(uu,Math.sin(a)*rad)))); } return o; };
    const a0=ring(A,r0), a1=ring(B2,r1);
    for(let k=0;k<seg;k++){ const k2=(k+1)%seg; F.push(face([a0[k],a0[k2],a1[k2],a1[k]],mat,b,dbv,part,[bA,bA,bB,bB])); }
    F.push(face(a1.slice(),mat,b,dbv,part,a1.map(()=>bB)));
    F.push(face(a0.slice().reverse(),mat,b,dbv,part,a0.map(()=>bA)));
  }
  const tball=(F,c,r,mat,b,db,part,bone)=>tlathe(F, c, [[-r,r*0.42,r*0.42],[-r*0.62,r*0.80,r*0.78],[0,r,r*0.96],[r*0.62,r*0.80,r*0.78],[r,r*0.42,r*0.42]], mat, b, db, null, 6, part, ()=>bone);
  const BOX_SZ=[[1,1,1,1],[-1,-1,-1,-1],[1,1,-1,-1],[1,1,-1,-1],[1,1,-1,-1],[1,1,-1,-1]];
  function tbox(F, c, h, mat, b, db, xf, part, bTop, bBot){
    xf=xf||ID; if(bBot==null) bBot=bTop;
    const P=(sx,sy,sz)=>xf([c[0]+sx*h[0], c[1]+sy*h[1], c[2]+sz*h[2]]);
    const Q=[[P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)], [P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)],
             [P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)], [P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)],
             [P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)], [P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)]];
    Q.forEach((v,i)=>F.push(face(v,mat,b,db,part,BOX_SZ[i].map(s=>s>0?bTop:bBot))));
  }

  /* ============================ SOLVE ============================
     One pass over rig 6's facesOf(): the bones' WORLD frames first, then the faces in rig 6's exact
     order, each vertex tagged. Called at the rest pose (idle u=0) for the bind, and per frame. */
  function solve(P, b, ctx){
    ctx = ctx||{};
    const PR=P.PR, wS=PR.wS, hS=P.hS, TS=P.TS, hc=P.headC;
    const G = GARMENTS[b.garment] || GARMENTS.overalls;
    const TP = C6.torsoProf(PR);
    const at=(dz,grow)=>[dz*TS, profR(TP,dz,1)+(grow||0), profR(TP,dz,2)+(grow||0)];
    const zOf=(dz)=>P.torsoC + dz*TS;
    const TC=[0,0,P.torsoC], FRONT=Math.PI/2, BACK=-Math.PI/2;
    const LEGX=0.004*wS, ARMX=0.016*wS*PR.shoulderK, SHDROP=0.098*hS;
    const ox=(p,dx)=>[p[0]+dx, p[1], p[2]];
    const listX = P.listR || ID;
    const torsoXf = chain(TX(P.swayX*0.5, P.yOff||0, 0), listX, P.leanP, rotZ(P.yawS*0.6));
    const pelvXf = (P.water||P.sleepP) ? chain(TX(0, P.yOff||0, 0), pitchX(P.lean||0, P.hipZ), rotZ(P.yawH)) : rotZ(P.yawH);

    /* ---- bones (world frames), parents before children ---- */
    const BN=[], IX={};
    const bone=(id,parent,p,R)=>{ IX[id]=BN.length; BN.push({ id, parent: parent==null ? -1 : IX[parent], p:p.slice(), R:(R||I3).slice() }); return IX[id]; };
    bone('root', null, [0,0,0], I3);
    const PF = frameOf(pelvXf, [P.swayX*0.5, 0, P.hipZ+0.020*hS]); bone('pelvis','root', PF.p, PF.R);
    const TF = frameOf(torsoXf, TC); bone('torso','pelvis', TF.p, TF.R);
    const neckT=[hc[0], hc[1]-0.010, hc[2]-0.100*PR.headK];
    bone('neck','torso', P.neckB, limbFrame(P.neckB, neckT)); bone('neck_tip','neck', neckT, BN[IX.neck].R);
    bone('head','neck', hc, I3);
    let inseam=null;
    if(!G.skirtHem){
      const iz0=P.ankleZ+P.rise+0.11*hS, iz1=P.hipZ-0.010*hS;
      const drawn = !(P.water||P.sleepP||P.drive) && (iz1 > iz0 + 0.01);
      inseam={ iz0, iz1, drawn };
      /* parked: both bones take the PELVIS frame, so the collapsed box lies in the pelvis lathe's own ring
         plane and stays inside it when the pelvis pitches over on swim/sleep (axis-aligned, it poked 2 px
         out of the lying boy's hips at E/W) */
      bone('inseam_top','pelvis', drawn ? [P.swayX*0.5, 0.004, iz1] : PF.p, drawn ? I3 : PF.R);
      bone('inseam_bot','pelvis', drawn ? [P.swayX*0.5, 0.004, iz0] : PF.p, drawn ? I3 : PF.R);
    }
    let aBot=null, sBotZ=null;
    if(G.apron){ const aStand=P.ankleZ+P.rise+0.40*hS;
      aBot=(P.water||P.sleepP) ? P.torsoC-0.310*P.TS : P.drive ? P.hipZ+0.020 : P.reach ? Math.min(aStand, P.hipZ-0.020*hS) : aStand;
      bone('apron_hem','torso', torsoXf([0,0,aBot]), TF.R); }
    if(G.skirtHem){ const sStand=P.ankleZ+P.rise+0.30*hS;
      sBotZ=(P.water||P.sleepP) ? P.torsoC-0.360*P.TS : P.drive ? P.hipZ-0.030 : P.reach ? Math.min(sStand, P.hipZ-0.040*hS) : sStand;
      bone('skirt_hem','torso', torsoXf([0,0,sBotZ]), TF.R); }
    const LEG={};
    for(const side of ['L','R']){
      const g=P.legs[side], sgn = side==='L' ? -1 : 1, dx=sgn*LEGX;
      const hip=ox(g.hip,dx), knee=ox(g.knee,dx), a=ox(g.ankle,dx);
      const shA = G.skirtHem ? [knee[0],knee[1],knee[2]-0.01] : knee;
      bone('hip_'+side,'pelvis', hip, limbFrame(hip,knee)); bone('hip_'+side+'_tip','hip_'+side, knee, BN[IX['hip_'+side]].R);
      bone('knee_'+side,'hip_'+side, shA, limbFrame(shA,a)); bone('knee_'+side+'_tip','knee_'+side, a, BN[IX['knee_'+side]].R);
      let top=null, bootB=null;
      if(G.bootZ>0.02){
        if(P.board || P.water || P.sleepP || P.reach){
          const vx=knee[0]-a[0], vy=knee[1]-a[1], vz=knee[2]-a[2];
          const vl=Math.hypot(vx,vy,vz)||1, tt=Math.min(1,(G.bootZ*hS)/vl);
          top=[a[0]+vx*tt, a[1]+vy*tt, a[2]+vz*tt];
        } else {
          const bz=P.ankleZ+G.bootZ*hS, t=(bz-a[2])/Math.max(0.001,(knee[2]-a[2]));
          top=[a[0]+(knee[0]-a[0])*t, a[1]+(knee[1]-a[1])*t, bz];
        }
        bootB=[a[0],a[1],a[2]+0.006];
        bone('ankle_'+side,'knee_'+side, top, limbFrame(top,bootB)); bone('ankle_'+side+'_tip','ankle_'+side, bootB, BN[IX['ankle_'+side]].R);
        bone('ankle_'+side+'_cuff','ankle_'+side, top, I3);
      }
      bone('foot_'+side,'knee_'+side, a, I3);
      LEG[side]={ hip, knee, a, shA, top, bootB };
    }
    const full = G.sleeve==='longShirtFull' || G.sleeve==='longOver';
    const ARM={};
    for(const side of ['L','R']){
      const A=P.arms[side], sgn = side==='L' ? -1 : 1, dx=sgn*ARMX;
      const sh=[A.sh[0]+dx, A.sh[1], A.sh[2]-SHDROP], el=[A.elbow[0]+dx, A.elbow[1], A.elbow[2]-SHDROP*0.40], wr=ox(A.wrist,dx);
      bone('shoulder_'+side,'torso', sh, limbFrame(sh,el)); bone('shoulder_'+side+'_tip','shoulder_'+side, el, BN[IX['shoulder_'+side]].R);
      bone('elbow_'+side,'shoulder_'+side, el, limbFrame(el,wr)); bone('elbow_'+side+'_tip','elbow_'+side, wr, BN[IX['elbow_'+side]].R);
      /* the cuff ball's centre; the wrist bone lives here on every garment so the skeleton is fixed per garment */
      const cp=v_add(el, v_mul(v_sub(wr,el), full?0.88:0.78));
      bone('wrist_'+side,'elbow_'+side, cp, I3);
      const d=v_norm(v_sub(wr,el)), hb=v_add(wr, v_mul(d,0.024*hS));
      bone('hand_'+side,'elbow_'+side, hb, I3);
      ARM[side]={ sh, el, wr, cp, hb };
    }
    /* tool roots: rig 6's tool() contract as a frame — grip = wrist + (0.005, 0.010, 0.012), local +y
       along the tool (RodIso's own rod-local frame), pitch about x then yaw about z. dig re-aims the
       shaft wrist-to-wrist exactly as tool() does. Then RodIso's bend as a 2-chord chain. */
    const wR=P.arms.R.wrist, wL=P.arms.L.wrist;
    let pitch=0, yaw=0, bend=0, held=false;
    if(P.tool){ held=true; pitch=P.tool.pitch; yaw=P.tool.yaw; bend=P.tool.bend||0;
      if(P.anim==='dig'){ const D=v_sub(wR,wL), L=Math.hypot(D[0],D[1],D[2])||1;
        pitch=Math.asin(Math.max(-1,Math.min(1,D[2]/L))); yaw=Math.atan2(D[0],D[1]); } }
    const aimM=(p,y)=>mM(rotZm(-y), rotXm(p));
    const Rt=aimM(pitch,yaw);
    const gripR=[wR[0]+0.005, wR[1]+0.010, wR[2]+0.012], gripL=[wL[0]-0.005, wL[1]+0.010, wL[2]+0.012];
    bone('tool_R','hand_R', gripR, Rt); bone('tool_L','hand_L', gripL, Rt);
    const RT=root.RodIso && root.RodIso.TIERS, tier=RT && RT[ctx.tier||'coast'] ? RT[ctx.tier||'coast'] : null;
    const len = tier ? tier.len : 1.25;
    const D=mV(Rt,[0,1,0]);
    const aim=(A,B)=>{ const d=v_norm(v_sub(B,A)); return aimM(Math.asin(Math.max(-1,Math.min(1,d[2]))), Math.atan2(d[0],d[1])); };
    for(const [side,grip] of [['R',gripR],['L',gripL]]){
      const pt=(s)=>{ const q=Math.max(0, s/len-0.40)/0.60; return [grip[0]+D[0]*s, grip[1]+D[1]*s, grip[2]+D[2]*s - bend*q*q*len*0.24]; };
      const p1=pt(0.40*len), p2=pt(0.70*len), p3=pt(len);
      bone('tool_'+side+'_1','tool_'+side, p1, aim(p1,p2)); bone('tool_'+side+'_2','tool_'+side+'_1', p2, aim(p2,p3));
    }
    /* carry pins: rig 6's carry() swings as rotations about local x (the sagittal pendulum) */
    const cr=ctx.carry||null, an=P.anim, u=P.u;
    const amp=(an==='run'?15:an==='walk'?9:1.6)*DEG, lag=0.9;
    const swR=amp*Math.sin(2*Math.PI*u-lag), swL=amp*Math.sin(2*Math.PI*(u+0.5)-lag);
    const swM=(an==='idle'?0.8*Math.sin(2*Math.PI*u):2.0*Math.cos(4*Math.PI*u))*DEG*(cr==='pot'?0.6:1);
    const hands=(cr==='buckets'||cr==='oars'||cr==='helm'), midC=(cr==='tray'||cr==='pot');
    bone('carry_L','hand_L', wL, hands?rotXm(swL):I3); bone('carry_R','hand_R', wR, hands?rotXm(swR):I3);
    bone('carry_mid','torso', v_mul(v_add(wL,wR),0.5), midC?rotXm(swM):I3);

    /* ---- faces, in rig 6's facesOf order ---- */
    const F=[], T=IX.torso, tb=()=>T;
    const rTh=0.056*wS*PR.limbR, rKn=0.049*wS*PR.limbR, rAn=0.042*wS*PR.limbR;
    for(const side of ['L','R']){
      const L=LEG[side], hip=L.hip, knee=L.knee, a=L.a;
      const th=IX['hip_'+side], thT=IX['hip_'+side+'_tip'], sn=IX['knee_'+side], snT=IX['knee_'+side+'_tip'];
      if(!G.skirtHem){
        tlimb(F, hip,  knee, rTh, rKn, 'over', -0.04, null, null, 'thigh_'+side, th, thT);
        tlimb(F, knee, a,    rKn*0.94, rAn, 'over', -0.14, null, null, 'shin_'+side, sn, snT);
      } else {
        tlimb(F, L.shA, a, rKn*0.72, rAn*0.80, 'skin', -0.10, null, null, 'shin_'+side, sn, snT);
      }
      if(G.bootZ>0.02){
        const bt=IX['ankle_'+side], btT=IX['ankle_'+side+'_tip'], cf=IX['ankle_'+side+'_cuff'];
        tlimb(F, L.top, L.bootB, rAn*1.20, rAn*1.14, 'boot', -0.10, null, null, 'boot_'+side, bt, btT);
        tlathe(F, [L.top[0],L.top[1],L.top[2]], [[-0.020,rAn*1.24,rAn*1.24],[0.018,rAn*1.28,rAn*1.28]], 'bootL', 0.10, 0.014, null, 6, 'boot_cuff_'+side, ()=>cf);
      }
      const ft=IX['foot_'+side];
      tbox(F, [a[0], a[1]-0.002, a[2]+0.002], [0.047*wS, 0.048*wS, 0.028], 'boot', -0.06, -0.02, null, 'foot_'+side, ft);
      tbox(F, [a[0], a[1]+0.056*hS, a[2]-0.016], [0.044*wS, 0.046*wS, 0.022], 'boot',  0.06, -0.04, null, 'foot_'+side, ft);
      tbox(F, [a[0], a[1]+0.026*hS, a[2]-0.032], [0.049*wS, 0.090*wS, 0.010], 'sole', -0.40, -0.05, null, 'foot_'+side, ft);
    }
    if(inseam){ const z0 = inseam.drawn ? inseam.iz0 : inseam.iz1, z1 = inseam.iz1;
      const cz = inseam.drawn ? [P.swayX*0.5, 0.004, (z0+z1)/2] : PF.p;
      tbox(F, cz, [0.044*wS, 0.050*wS, (z1-z0)/2], 'overD', -0.62, -0.09, null, 'inseam', IX.inseam_top, IX.inseam_bot); }
    tlathe(F, [P.swayX*0.5, 0, P.hipZ+0.020*hS],
      [[-0.052*hS, 0.112*wS*PR.hipK, 0.080*wS],[-0.008*hS, 0.130*wS*PR.hipK, 0.090*wS],[0.048*hS, 0.128*wS*PR.hipK, 0.088*wS]],
      'over', -0.12, 0, pelvXf, 8, 'pelvis', ()=>IX.pelvis);
    const zBot=-0.155, zTop=0.182, wrapTop=G.wrapTop;
    if(wrapTop!=null){
      tlathe(F, TC, [at(zBot),at(-0.120),at(wrapTop)], 'over', -0.02, 0, torsoXf, 8, 'torso', tb);
      tlathe(F, TC, [at(wrapTop),at((wrapTop+zTop)/2),at(0.140),at(0.170),at(zTop)], 'shirt', 0.14, 0, torsoXf, 8, 'torso', tb);
      if(G.waistBand) tlathe(F, TC, [at(wrapTop-0.030,0.005),at(wrapTop+0.012,0.005)], 'overD', -0.22, 0.030, torsoXf, 8, 'torso', tb);
    } else {
      tlathe(F, TC, [at(zBot),at(-0.085),at(0.010),at(0.078),at(0.140),at(0.170),at(zTop)], 'shirt', 0.10, 0, torsoXf, 8, 'torso', tb);
    }
    if(G.bibTop!=null){
      const h=G.bibArc, bt=G.bibTop, bb=(wrapTop!=null?wrapTop:-0.085);
      const hemLo=Math.max(bb, bt-0.042/TS);
      const rows=(a2,b2,g)=>{ const o=[]; const n=3; for(let i=0;i<=n;i++){ const d=a2+(b2-a2)*i/n; o.push(at(d,g)); } return o; };
      tarc(F, TC, rows(bb,hemLo,0.007), 'over',  -0.02, 0.020, torsoXf, FRONT-h, FRONT+h, 5, 'torso', tb);
      tarc(F, TC, rows(hemLo,bt,0.009), 'overD', -0.34, 0.030, torsoXf, FRONT-h, FRONT+h, 5, 'torso', tb);
      if(G.backTop!=null){
        tarc(F, TC, rows(bb,G.backTop,0.007), 'over', -0.06, 0.020, torsoXf, BACK-h*0.92, BACK+h*0.92, 5, 'torso', tb);
        tarc(F, TC, rows(G.backTop-0.038,G.backTop,0.009), 'overD', -0.16, 0.030, torsoXf, BACK-h*0.92, BACK+h*0.92, 5, 'torso', tb);
      }
      if(G.straps){
        const sxx = Math.sin(h)*(profR(TP,0.078,1)+0.007);
        const sX  = Math.max(0.042*wS, sxx-0.004*wS);
        for(const sgn of [-1,1]){
          const yF=profR(TP,0.130,2)+0.004;
          tbox(F, [sgn*sX, yF*0.78, (zOf(bt)+zOf(0.184))/2], [0.0155*wS, 0.016, (zOf(0.184)-zOf(bt))/2], 'over', -0.40, 0.034, torsoXf, 'torso', T);
          tbox(F, [sgn*sX*0.66, -(profR(TP,0.120,2)+0.002), (zOf(-0.010)+zOf(0.176))/2], [0.0150*wS, 0.014, (zOf(0.176)-zOf(-0.010))/2], 'over', -0.30, 0.026, torsoXf, 'torso', T);
          tbox(F, [sgn*sX, 0.006, zOf(0.176)], [0.0165*wS, profR(TP,0.164,2)+0.006, 0.016*hS], 'overD', 0.16, 0.030, torsoXf, 'torso', T);
          if(G.buckles) tbox(F, [sgn*sX, yF*0.92, zOf(bt)-0.020*hS], [0.0150*wS, 0.015, 0.019*hS], 'brass', 0.60, 0.070, torsoXf, 'torso', T);
        }
      }
    }
    if(G.vest){
      tlathe(F, TC, [at(-0.100,0.008),at(-0.020,0.008),at(0.060,0.008),at(0.112,0.008),at(0.140,0.008)], 'over', -0.02, 0.018, torsoXf, 8, 'torso', tb);
      tbox(F, [0, profR(TP,0.040,2)+0.014, (zOf(-0.096)+zOf(0.126))/2], [0.0150*wS, 0.012, (zOf(0.126)-zOf(-0.096))/2], 'shirtL', 0.30, 0.030, torsoXf, 'torso', T);
    }
    if(G.apron){
      const aTop=zOf(0.086), rx=0.150*wS, ry=0.100*wS, n=5, H=IX.apron_hem;
      const rows=[]; for(let i=0;i<=n;i++) rows.push([aBot+(aTop-aBot)*i/n - P.torsoC, rx*(1+0.06*(1-i/n)), ry]);
      const ws=(i)=> i===0 ? H : i===n ? T : [[H,1-i/n],[T,i/n]];
      tarc(F, TC, rows, 'apron', 0.06, 0.046, torsoXf, FRONT-0.78, FRONT+0.78, 6, 'apron', ws);
      tarc(F, TC, [[aBot-P.torsoC, rx*1.06, ry],[aBot-P.torsoC+0.040, rx*1.06, ry]], 'apronD', -0.20, 0.052, torsoXf, FRONT-0.78, FRONT+0.78, 6, 'apron_hem', ()=>H);
      tarc(F, TC, [at(-0.096,0.026),at(-0.056,0.026)], 'apronD', -0.16, 0.052, torsoXf, FRONT-1.5, FRONT+1.5, 8, 'torso', tb);
      for(const sgn of [-1,1])
        tbox(F, [sgn*0.052*wS, profR(TP,0.130,2)+0.008, (zOf(0.086)+zOf(0.176))/2], [0.0140*wS, 0.014, (zOf(0.176)-zOf(0.086))/2], 'apronD', -0.10, 0.040, torsoXf, 'torso', T);
    }
    if(G.skirtHem){
      const sTop=-0.100, n=5, H=IX.skirt_hem, rows=[];
      for(let i=0;i<=n;i++){ const t=i/n, z=sBotZ+(zOf(sTop)-sBotZ)*t; rows.push([z-P.torsoC, (0.238-0.086*t)*wS, (0.150-0.056*t)*wS]); }
      const ws=(i)=> i===0 ? H : i===n ? T : [[H,1-i/n],[T,i/n]];
      tlathe(F, TC, rows, 'over', -0.06, 0, torsoXf, 10, 'skirt', ws);
      tlathe(F, TC, [[sBotZ-P.torsoC, 0.242*wS, 0.152*wS],[sBotZ-P.torsoC+0.042, 0.240*wS, 0.151*wS]], 'overD', -0.18, 0.030, torsoXf, 10, 'skirt_hem', ()=>H);
    }
    if(G.shawl) tlathe(F, TC, [at(0.030,0.012),at(0.090,0.014),at(0.140,0.014),at(0.168,0.012)], 'overD', 0.08, 0.026, torsoXf, 8, 'torso', tb);
    if(G.belt){
      tlathe(F, TC, [at(-0.126,0.007),at(-0.086,0.007)], 'belt', -0.02, 0.028, torsoXf, 8, 'torso', tb);
      tbox(F, [0, profR(TP,-0.106,2)+0.016, zOf(-0.106)], [0.020*wS, 0.012, 0.019*hS], 'brass', 0.55, 0.046, torsoXf, 'torso', T);
    }
    for(const sgn of [-1,1])
      tlathe(F, [sgn*0.116*wS*PR.shoulderK, 0, P.torsoC+0.076*TS],
        [[-0.040*hS,0.032*wS,0.036*wS],[-0.012*hS,0.041*wS,0.045*wS],[0.016*hS,0.038*wS,0.041*wS],[0.036*hS,0.022*wS,0.026*wS]],
        (G.sleeve==='longOver'||G.vest)?'over':'shirt', -0.20, 0.014, torsoXf, 8, 'torso', tb);
    const cMat = G.collar==='stand' ? ((G.sleeve==='longOver')?'overD':'shirtD') : 'collar';
    if(G.collar==='roll') tlathe(F, TC, [at(0.156,0.020),at(0.182,0.024),at(0.206,0.020)], 'shirtD', 0.20, 0.022, torsoXf, 8, 'torso', tb);
    else if(G.collar==='stand') tlathe(F, TC, [at(0.164,0.014),at(0.196,0.014)], cMat, 0.18, 0.020, torsoXf, 8, 'torso', tb);
    else if(G.collar==='open'){
      tlathe(F, TC, [at(0.160,0.012),at(0.184,0.012)], 'shirtD', 0.16, 0.020, torsoXf, 8, 'torso', tb);
      tbox(F, [0, profR(TP,0.150,2)+0.010, zOf(0.150)], [0.014*wS, 0.012, 0.020*hS], 'shirtD', -0.55, 0.032, torsoXf, 'torso', T);
    } else tlathe(F, TC, [at(0.166,0.010),at(0.186,0.010)], 'collar', 0.16, 0.018, torsoXf, 8, 'torso', tb);
    const rSh=0.054*wS*PR.limbR, rHem=0.047*wS*PR.limbR, rEl=0.043*wS*PR.limbR, rFo=0.040*wS*PR.limbR, rWr=0.032*wS*PR.limbR;
    const sleeveMat = G.sleeve==='longOver' ? 'over' : 'sleeve';
    for(const side of ['L','R']){
      const A=ARM[side], sh=A.sh, el=A.el, wr=A.wr;
      const up=IX['shoulder_'+side], upT=IX['shoulder_'+side+'_tip'], fo=IX['elbow_'+side], foT=IX['elbow_'+side+'_tip'];
      if(G.sleeve==='short'){
        const hem=v_add(sh, v_mul(v_sub(el,sh), 0.62)), wHem=[[up,0.38],[upT,0.62]];
        tlimb(F, sh,  hem, rSh, rHem, sleeveMat, -0.26, null, null, 'upper_'+side, up, wHem);
        tlimb(F, hem, el,  rEl, rFo,  'skin',    -0.08, null, null, 'upper_'+side, wHem, upT);
        tlimb(F, el,  wr,  rFo, rWr,  'skin',    -0.12, null, null, 'fore_'+side, fo, foT);
      } else {
        const f1 = full?0.90:0.80, f2 = full?0.86:0.76;
        tlimb(F, sh, el, rSh, rEl+0.003, sleeveMat, -0.26, null, null, 'upper_'+side, up, upT);
        tlimb(F, el, v_add(el, v_mul(v_sub(wr,el), f1)), rEl+0.003, rWr+0.004, sleeveMat, -0.12, null, null, 'fore_'+side, fo, [[fo,1-f1],[foT,f1]]);
        tlimb(F, v_add(el, v_mul(v_sub(wr,el), f2)), wr, rWr+0.002, rWr, 'skin', -0.12, null, null, 'fore_'+side, [[fo,1-f2],[foT,f2]], foT);
        if(G.cuff||G.cuffOver) tball(F, A.cp, rWr+0.010, G.cuffOver?'overD':'shirtD', 0.12, 0.014, 'cuff_'+side, IX['wrist_'+side]);
      }
      tball(F, A.hb, 0.036*wS*PR.limbR, 'skin', -0.02, -0.03, 'hand_'+side, IX['hand_'+side]);
    }
    tlimb(F, P.neckB, neckT, 0.049*wS*PR.neckK, 0.043*wS*PR.neckK, 'skin', -0.24, null, null, 'neck', IX.neck, IX.neck_tip);
    const HI=root.HeadIso;
    if(HI){ const hb=Object.assign({}, b, { jaw:PR.jawK, neck:PR.neckK });
      for(const f of HI.facesOf(hc, hb, P.look || {gaze:[0,0],lid:0,brow:0,mouth:'neutral'}, wS, PR.headK))
        F.push(Object.assign({}, f, { part:'head', bone:f.v.map(()=>[[IX.head,1]]) })); }
    return { bones:BN, ix:IX, faces:F, inseamDrawn: inseam ? inseam.drawn : true,
             tool:{ held, pitch:+(pitch/DEG).toFixed(3), yaw:+(yaw/DEG).toFixed(3), bend:+bend.toFixed(4), advisory: !!(P.tool&&P.tool.advisory) } };
  }

  /* ============================ SKELETON MATHS ============================ */
  function localsOf(bones){ return bones.map((B)=>{ if(B.parent<0) return { p:B.p.slice(), R:B.R.slice() };
    const Pp=bones[B.parent]; return { p: mTV(Pp.R, v_sub(B.p, Pp.p)), R: mTM(Pp.R, B.R) }; }); }
  function worldsOf(locals, bones){ const W=[]; locals.forEach((L,i)=>{ const par=bones[i].parent;
    if(par<0){ W.push({ p:L.p.slice(), R:L.R.slice() }); return; }
    const Wp=W[par]; W.push({ p: v_add(Wp.p, mV(Wp.R, L.p)), R: mM(Wp.R, L.R) }); }); return W; }
  /* intrinsic Z-Y-X Euler (deg) of a column-major rotation: yaw about z, then pitch about y, then roll about x */
  function eulerOf(R){ const m00=R[0], m10=R[1], m20=R[2], m01=R[3], m11=R[4], m21=R[5], m22=R[8];
    const sy=-m20, cy=Math.sqrt(Math.max(0,1-sy*sy)); let x,y,z;
    if(cy>1e-9){ y=Math.asin(Math.max(-1,Math.min(1,sy))); x=Math.atan2(m21,m22); z=Math.atan2(m10,m00); }
    else { y=sy>0?Math.PI/2:-Math.PI/2; x=0; z=Math.atan2(-m01,m11); }
    return [x/DEG, y/DEG, z/DEG].map(a=>+a.toFixed(6)); }
  /* the export form of one frame, keyed by bone id: local pos + unit quaternion (sign-continuous with
     the previous frame) + the same rotation as an Euler triple */
  function exportLocals(bones, prev){ const out={}; localsOf(bones).forEach((l,i)=>{ let q=quatOf(l.R); const id=bones[i].id, pq=prev && prev[id] && prev[id].rot;
    if(pq && (pq[0]*q[0]+pq[1]*q[1]+pq[2]*q[2]+pq[3]*q[3])<0) q=q.map(x=>-x);
    out[id]={ pos:rnd(l.p), rot:rndR(q), rotEuler:eulerOf(l.R) }; }); return out; }
  const worldsFromExport=(xf, bones)=>worldsOf(bones.map(b=>({ p:xf[b.id].pos, R:matOf(xf[b.id].rot) })), bones);
  /* per-bone skin matrix: world_pose · inverse(world_bind) */
  function skinMats(bind, W){ return W.map((w,i)=>{ const S=mMT(w.R, bind[i].R); return { S, t:v_sub(w.p, mV(S, bind[i].p)) }; }); }
  function skinFaces(faces, M){ return faces.map(f=>({ v:f.v.map((p,k)=>{ const w=f.bone[k];
      if(w.length===1){ const m=M[w[0][0]]; return v_add(mV(m.S,p), m.t); }
      const o=[0,0,0]; for(const [bi,wt] of w){ const m=M[bi], q=v_add(mV(m.S,p), m.t); o[0]+=q[0]*wt; o[1]+=q[1]*wt; o[2]+=q[2]*wt; } return o; }),
    mat:f.mat, b:f.b, db:f.db, part:f.part })); }

  /* ============================ THE API ============================ */
  const buildOf=(build)=>C6.resolveBuild({ build: (typeof build==='string') ? {preset:build} : (build||{}) });
  function solveAt(anim, u, b, opts){
    opts=opts||{};
    const power = opts.power==='long' ? 'long' : 'short';
    const carry = CARRY_OK.indexOf(opts.carry)>=0 ? opts.carry : null;
    const o = Object.assign({}, opts);                   // world-metre opts (railZ, workZ, saddle…) ride through untouched
    const P = C6.pose(anim, u, b, power, carry, o);
    const S = solve(P, b, { carry, tier:opts.tier }); S.P=P; S.b=b; S.carry=carry; S.power=power; return S;
  }
  const _bind={};
  /* THE BIND POSE IS idle u=0, exactly pose('idle', 0, build) — the pose every walk settles into
     and the rest end of the mount contract. Cached per resolved build. */
  function bindOf(b){ const k=JSON.stringify(b); if(!_bind[k]){ const S=solveAt('idle',0,b,{});
    _bind[k]={ b, bones:S.bones, faces:S.faces, locals:exportLocals(S.bones,null) }; } return _bind[k]; }
  const faceTrack=(P)=>{ const L=P.look||{}; return { lid:L.lid||0, gaze:L.gaze||[0,0], brow:L.brow||0, mouth:L.mouth||'neutral', eyesClosed:!!P.eyesClosed }; };

  /* the brief's shape: { id, parent, rest:{pos,rot} } — parent an index (-1 at the root), rest LOCAL to the
     parent, rotEuler alongside rot. Parents come before children. One skeleton per build: the count and
     order are fixed per garment, the lengths live in rest. */
  function skeleton(build){ const B=bindOf(buildOf(build));
    return B.bones.map((bn)=>({ id:bn.id, parent:bn.parent, rest:B.locals[bn.id] })); }
  /* the same bones in the figure frame (bindposes = inverse of these) */
  function skeletonWorld(build){ const B=bindOf(buildOf(build));
    return B.bones.map(bn=>({ id:bn.id, pos:rnd(bn.p), rot:rndR(quatOf(bn.R)), rotEuler:eulerOf(bn.R) })); }
  function bindMesh(build){ return bindOf(buildOf(build)).faces; }
  function clip(anim, build, frames, opts){
    opts=opts||{}; const A=ANIMS[anim]; if(!A) return null;
    const b=buildOf(build), B=bindOf(b);
    const n = frames||A.frames, den = A.settle ? Math.max(1,n-1) : n;
    const tracks=[]; let prev=null; const parked=[];
    for(let k=0;k<n;k++){
      const u=k/den, S=solveAt(anim,u,b,opts);
      if(S.bones.length!==B.bones.length) throw new Error('characterIsoRig7: bone count changed on '+anim+' u='+u);
      const bones=exportLocals(S.bones, prev); prev=bones;
      if(!S.inseamDrawn && parked.indexOf('inseam')<0) parked.push('inseam');
      tracks.push({ frame:k, u:+u.toFixed(6), ms:k*A.ms, bones, face:faceTrack(S.P), tool:S.tool });
    }
    const carry = CARRY_OK.indexOf(opts.carry)>=0 ? opts.carry : null;
    return { anim, name: anim + (carry?'+'+carry:'') + (opts.power==='long'?'·long':''), build: b.preset||'custom',
             frames:n, ms:A.ms, duration:n*A.ms, loop:!A.oneShot, settle:!!A.settle, mount:C6.ANIM_MOUNT[anim]||'free',
             carry, power: opts.power==='long'?'long':'short', rock: opts.rock ? Object.assign({}, opts.rock) : null,
             bones:B.bones.map(x=>x.id), parked, tracks };
  }
  function clips(build, opts){ return Object.keys(ANIMS).map(a=>clip(a, build, null, opts)); }
  const FRAME_DOC = { axes:'right-handed metres: +x right (curb), +y forward (nose), +z up; origin = cell pivot on the ground',
    rot:'unit quaternion [x,y,z,w]; local to the parent bone', rotEuler:'the same rotation, degrees, intrinsic Z-Y-X (yaw z, pitch y, roll x)', pos:'metres, local to the parent bone',
    unity:'pos (x, z, y); quaternion (-x, -z, -y, w)', bind:"pose('idle', 0, build)", u:'k/frames, or k/(frames-1) on settle clips (reach, mount*)',
    face:'per-frame HeadIso look for the raster stamp on the head-anchored quad', tool:'degrees; tool_L/R local +y runs along the tool, tool_*_1/2 carry RodIso\'s bend as two chords',
    bone:'bindMesh: bone[k] = [[boneIndex, weight], ...] for vertex k; one pair at weight 1 except the BLENDED rings', rock:'deck rock is a live hull transform; a clip made with opts.rock carries only the counter-lean rig 6 poses from it' };
  function exportBuild(build, opts){ const b=buildOf(build);
    return { rig:'characterIsoRig7', revision:API.revision, rig6:C6.revision, build:b, frame:FRAME_DOC, blended:BLENDED,
             skeleton:skeleton(b), skeletonWorld:skeletonWorld(b), bindMesh:bindMesh(b), clips:clips(b, opts) }; }

  /* ---------------- the golden rule ---------------- */
  /* goldenDiff(anim, u, build) -> the max vertex distance in metres, as the brief asks; Infinity when a
     face has no match. goldenDiffDetail returns the full record. */
  function goldenDiff(anim, u, build, opts){ const d=goldenDiffDetail(anim, u, build, opts); return d.err ? Infinity : d.max; }
  function goldenDiffDetail(anim, u, build, opts){
    const b=buildOf(build), B=bindOf(b), S=solveAt(anim,u,b,opts);
    if(S.bones.length!==B.bones.length) return { anim, u, max:Infinity, err:'bone count changed between rest and pose', ok:false };
    /* through the EXPORT path: locals -> quaternions -> composed worlds, exactly what a consumer does */
    const W=worldsFromExport(exportLocals(S.bones,null), B.bones);
    const posed=skinFaces(B.faces, skinMats(B.bones, W));
    const ref=C6.facesOf(S.P, b);
    let j=0, max=0, at=null, parked=0, err=null;
    for(let i=0;i<B.faces.length;i++){ const fb=B.faces[i];
      if(fb.part==='inseam' && !S.inseamDrawn){ parked++; continue; }
      const fr=ref[j++];
      if(!fr || fr.v.length!==fb.v.length || fr.mat!==fb.mat || (fr.b||0)!==fb.b || (fr.db||0)!==fb.db){ err='bind face '+i+' ('+fb.part+') has no match in rig 6 at face '+(j-1); break; }
      const sv=posed[i].v;
      for(let k=0;k<sv.length;k++){ const d=v_dist(sv[k], fr.v[k]); if(d>max){ max=d; at={ face:i, vert:k, part:fb.part, mat:fb.mat }; } }
    }
    if(!err && j!==ref.length) err='rig 6 emitted '+(ref.length-j)+' face(s) the bind mesh does not carry';
    return { anim, u, max, at, faces:B.faces.length, compared:j, parked, err, ok: !err && max<=TOL };
  }
  /* the flipbook set: every ANIMS entry at its own frame count, the long-rod casts, every carry on
     the clips it may ride — the rows a sheet baker bakes */
  function goldenRows(){
    const rows=Object.keys(ANIMS).map(anim=>({ label:anim, anim, opts:{} }));
    for(const a of ['cast','castBack','castRelease']) rows.push({ label:a+'·long', anim:a, opts:{power:'long'} });
    for(const c of C6.CARRY_ORDER) for(const a of C6.CARRIES[c].anims) rows.push({ label:a+'+'+c, anim:a, opts:{carry:c} });
    return rows;
  }
  function goldenRow(row, build){
    const A=ANIMS[row.anim], n=A.frames, den=A.settle ? Math.max(1,n-1) : n;
    let max=0, parked=0, err=null, at=null;
    for(let k=0;k<n;k++){ const d=goldenDiffDetail(row.anim, k/den, build, row.opts);
      if(d.err && !err) err=d.err+' (frame '+k+')'; if(d.max>max){ max=d.max; at=Object.assign({frame:k}, d.at||{}); } parked+=d.parked||0; }
    return { label:row.label, anim:row.anim, frames:n, max, at, parked, err, ok: !err && max<=TOL };
  }
  function goldenReport(build){
    const b=buildOf(build), rows=goldenRows().map(r=>goldenRow(r,b));
    const worst=rows.reduce((m,r)=>r.max>m.max?r:m, {max:-1});
    return { build:b.preset||'custom', bones:bindOf(b).bones.length, faces:bindOf(b).faces.length, frames:rows.reduce((s,r)=>s+r.frames,0),
             worst:worst.max, worstAt:worst.label, tol:TOL, ok:rows.every(r=>r.ok), rows };
  }
  function sizes(build){ const b=buildOf(build), B=bindOf(b);
    const tris=B.faces.reduce((s,f)=>s+f.v.length-2,0), verts=B.faces.reduce((s,f)=>s+f.v.length,0);
    const cl=clips(b), frames=cl.reduce((s,c)=>s+c.frames,0);
    return { bones:B.bones.length, faces:B.faces.length, tris, verts, frames, clipCount:cl.length,
             bindBytes:JSON.stringify(B.faces).length, skeletonBytes:JSON.stringify(skeleton(b)).length, clipBytes:JSON.stringify(cl).length }; }
  const BLENDED = [
    { part:'apron',  where:'rows 1-4 of 5',  bones:'apron_hem + torso', why:'rig 6 lerps the apron rings from a floor-anchored hem to the torso' },
    { part:'skirt',  where:'rows 1-4 of 5',  bones:'skirt_hem + torso', why:'same lerp, hem to waist' },
    { part:'upper_*',where:'hem ring at 0.62 (short sleeve)', bones:'shoulder + shoulder_tip', why:'the sleeve hem sits at a fraction of a tube whose drawn length changes' },
    { part:'fore_*', where:'sleeve-end ring 0.80/0.90 and skin-start ring 0.76/0.86 (long sleeves)', bones:'elbow + elbow_tip', why:'same, along the forearm' },
  ];

  /* ============================ THE PROOF RENDERER ============================
     Rig 6's rasterizer (fleet recipe, tinted keyline, face stamp), verbatim, fed the skinned bind
     mesh instead of facesOf(). renderSkinned(dir,opts) must equal CharacterIso6.render(dir,opts)
     to the pixel; diffPixels() counts where it does not. */
  const GAIN=C6.GAIN, BIAS=C6.BIAS, LN=C6.LN, BAYER=C6.BAYER, EDGE=0.13, KEY=C6.KEY;
  function normal(a,b,c){ const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx; const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m]; }
  const shadeOf=(n,se,ce)=>n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2];
  function camBasis(opts){ const dir=opts.dir||0, th=-dir*Math.PI/4, e=(opts.elev!=null?opts.elev:C6.defaultElev)*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e), cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch), heave:(opts.heave||0) }; }
  function projVert(x,y,z,B,S,cx,cy){ const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr; const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:cx+xr*S, sy:cy-(yr*B.se+zr*B.ce)*S - (B.heave||0), d:(yr*B.ce-zr*B.se) }; }
  const _hex=(c)=>[parseInt(c.slice(1,3),16),parseInt(c.slice(3,5),16),parseInt(c.slice(5,7),16)];
  const _mix=(a,b2,t)=>{ const A=_hex(a),B2=_hex(b2); return '#'+[0,1,2].map(i=>Math.round(A[i]+(B2[i]-A[i])*t).toString(16).padStart(2,'0')).join(''); };
  const _keyCache={}; const keyTint=(c)=>_keyCache[c]||(_keyCache[c]=_mix(KEY, c, 0.22));
  function paint(faces, opts, MATS, RINDEX, post){
    const W=C6.W, H=C6.H, cx=C6.pivot.x, cy=C6.pivot.y, S=C6.PX, B=camBasis(opts);
    const zbuf=new Float32Array(W*H).fill(Infinity), col=new Array(W*H).fill(null), dep=new Float32Array(W*H);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B,S,cx,cy));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n,B.se,B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]],B.se,B.ce)*0.9;
      const M=MATS[f.mat]||MATS.shirt;
      const fidx=sh*GAIN*(M.gain==null?1:M.gain) + (M.bias==null?BIAS:M.bias) + (f.b||0);
      const fillTri=(a,b,c)=>{
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx))), maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy))), maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy); if(Math.abs(area)<1e-6) return;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area, w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area, w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0), i=y*W+x;
          if(deff<zbuf[i]){ zbuf[i]=deff; dep[i]=d;
            const base=Math.floor(fidx), fr=fidx-base;
            const idx=base+(M.dith ? ((fr>BAYER[x&3][y&3])?1:0) : (fr>0.55?1:0))+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))]; }
        } };
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1]);
    }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!col[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){ const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue; const j=ny*W+nx; if(!col[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]]; if(e && e.i>0) col[far]=e.r[Math.max(0,e.i-1)]; } } }
    if(post) post({col, dep, zbuf, W, H}, B);
    const out=col.slice();
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue; let src=null;
      for(const [dx,dy] of [[0,-1],[1,0],[-1,0],[0,1]]){ const nx=x+dx, ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&col[ny*W+nx]){ src=col[ny*W+nx]; break; } }
      if(src) out[i]=keyTint(src); }
    const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const c=out[i]; if(!c){ rgba[i*4+3]=0; continue; }
      rgba[i*4]=parseInt(c.slice(1,3),16); rgba[i*4+1]=parseInt(c.slice(3,5),16); rgba[i*4+2]=parseInt(c.slice(5,7),16); rgba[i*4+3]=255; }
    return rgba;
  }
  /* rig 6's resolveOpts + gridHead (6.8), so the proof draws exactly what render() draws */
  function resolveOpts(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    const b=C6.resolveBuild(opts), anim=opts.anim||'idle', A=ANIMS[anim]||ANIMS.idle;
    const den=A.settle ? Math.max(1, A.frames-1) : A.frames;
    const u=opts.u!=null ? opts.u : (((opts.frame||0)%A.frames+A.frames)%A.frames)/den;
    const power=opts.power==='long' ? 'long' : 'short';
    const carry=CARRY_OK.indexOf(opts.carry)>=0 ? opts.carry : null;
    return { o:Object.assign({},opts,{dir}), b, anim, u, power, carry };
  }
  function gridHead(P, o){ const S=C6.PX, B=camBasis(o), h=P.headC, v=projVert(h[0],h[1],h[2],B,S,C6.pivot.x,C6.pivot.y);
    const tx=Math.round(v.sx-0.5)+0.5, ty=Math.round(v.sy-0.5)+0.5, dxr=(tx-v.sx)/S, dz=-(ty-v.sy)/(B.ce*S);
    P.headC=[h[0]+dxr*B.ct, h[1]-dxr*B.stt, h[2]+dz]; return P; }
  function renderSkinned(dir, opts){
    const {o,b,anim,u,power,carry}=resolveOpts(dir,opts);
    const P=gridHead(C6.pose(anim,u,b,power,carry,o), o);
    const S=solve(P, b, { carry, tier:o.tier }), B=bindOf(b);
    const faces=skinFaces(B.faces, skinMats(B.bones, worldsFromExport(exportLocals(S.bones,null), B.bones)));
    const {MATS,RINDEX}=C6.makeMats(b), HI=root.HeadIso;
    const hb=Object.assign({}, b, { jaw:P.PR.jawK, neck:P.PR.neckK });
    const post=HI ? (px,B2)=>HI.stamp(px, P.headC, hb, P.look, B2, MATS, C6.pivot.x, C6.pivot.y, P.PR.headK) : null;
    return paint(faces, o, MATS, RINDEX, post);
  }
  function diffPixels(dir, opts){
    const a=C6.render(dir,opts), c=renderSkinned(dir,opts), mask=new Uint8ClampedArray(a.length); let count=0;
    for(let i=0;i<a.length;i+=4){ if(a[i]!==c[i]||a[i+1]!==c[i+1]||a[i+2]!==c[i+2]||a[i+3]!==c[i+3]){ count++; mask[i]=255; mask[i+1]=64; mask[i+2]=200; mask[i+3]=255; } }
    return { count, mask, skinned:c, reference:a, W:C6.W, H:C6.H };
  }
  /* world-space bone frames for one pose — what the harness draws over the sprite */
  function poseBones(anim, u, build, opts){ const b=buildOf(build), S=solveAt(anim,u,b,opts);
    return S.bones.map(bn=>({ id:bn.id, parent:bn.parent, pos:bn.p, rot:quatOf(bn.R) })); }

  const API = Object.assign(Object.create(C6), {
    skeleton, skeletonWorld, bindMesh, clip, clips, exportBuild, goldenDiff, goldenDiffDetail, goldenRows, goldenRow, goldenReport, sizes, eulerOf,
    renderSkinned, diffPixels, poseBones, BLENDED, FRAME_DOC, TOL, solve:solveAt, bindOf,
    quatOf, matOf, pass:7, revision:'7.1', base:C6.revision });
  root.CharacterIso7 = API;
})(typeof globalThis!=='undefined'?globalThis:window);
