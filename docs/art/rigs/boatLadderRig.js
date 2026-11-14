/* Hidden Harbours — the BOARDING LADDER, one shared fitting for every hull that is too tall to climb
   (owner, 2026-11-13: "Side dragger and stern trawler, cape islander and lobster boats should have
   stern or side ladders. They would definitely be too tall to climb" / "maybe all boats need a
   ladder"). The boarding RULE already asks each hull's deck data for a ladder point; this rig is the
   thing you SEE at that point. It is a fitting, not a hull: it carries no loft and no rock, and it
   is hung on a hull by data (docs/art/rigs/gameplay/boatLadderRig.ladders.json), never by editing
   that hull's rig — so a ladder never moves a hull's rig digest or its baked mesh.

   Same recipe as the fleet: 1 unit = 1 m, hull-local frame (+x starboard, +y bow, +z up, origin
   amidships at the keel bottom), flat-facet shading from the fixed upper-left key, ordered dither,
   NO AA. Faces are {v:[[x,y,z]..], mat, b, db} exactly as a hull's F, so the mesh baker reads them
   on the same terms.

   TWO KINDS
     'rigid'  a stainless stern or side ladder for the working boats: two rails, round rungs, the
              rails carried over the top of the side as grab hoops and landed on the covering board,
              stand-off feet at the head, above the waterline and every 2.4 m between. Two rungs sit
              under the water so a swimmer has a foot.
     'pilot'  a rope-and-rung pilot ladder for a ship's side: two manila side ropes, flat wooden
              steps every 0.33 m, the bottom four in black rubber, a long spreader at the fifth step
              and every ninth above it (so she cannot twist), the ropes led over the bulwark cap and
              made fast inside, and two steel stanchions at the rail to pull yourself over with.

   A SPEC places one ladder on one hull:
     { kind, at:[x,y], outward:[nx,ny], zTop, zWater, px, zBottom? }
       at       the boarding point, hull-local metres, ON the skin. This is the SAME pair the deck
                Def stores as BoardingPoint.DeckLocalMeters; the ladder is centred on it.
       outward  unit horizontal normal of the hull skin there ([0,-1] on a transom, [1,0] to starboard).
       zTop     top of the side at that point (the hull's own sheerZ).
       zWater   her resting waterline (hull-local z).
       px       the hull's working resolution (32 for the fleet, 16 for the tanker). Sets the
                thinnest member that still lands on a pixel centre: no radius is drawn under 0.8/px.
       zBottom  optional override of the foot; default zWater - KINDS[kind].submerge.

   Exposes globalThis.BoatLadder = { KINDS, faces(spec), anchors(spec), pivot(spec),
     renderOn(hull,dir,opts), MATS, GAIN, BIAS, LN, BAYER, F, STEEL, ROPE, WOOD, RUBBER, KEY }.
     faces(spec)    the ladder's faces in the hull's own frame (the builder the mesh bake calls).
     anchors(spec)  { point, outward, head, foot, grabTop, waterline } - what the sidecar publishes.
     pivot(spec)    the head of the ladder on the skin: [x, y, zTop].
     renderOn(hull, dir, {ladders:[spec..], elev, roll, pitch, heave})
                    PREVIEW ONLY: the hull's own render() with the ladders drawn over it, depth-tested
                    against the hull's own faces() through the hull's own cell and key light. Returns
                    RGBA like render(). It needs only what every converted hull already exports
                    (render, faces, loft.cell, loft.shade). */
(function (root) {
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40;

  // ---- palette ramps dark->light (fleet ramps, KTC-clamped; nothing new is invented here) ----
  const STEEL  = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2','#e6edee'];             // stainless (the lobster boat's arch + rails)
  const ROPE   = ['#4a3b22','#66522f','#84693d','#a1844f','#bd9f66','#d6bb85'];             // manila side ropes
  const WOOD   = ['#33271b','#473627','#5e4630','#6b4f35','#8a6a48','#9a7853','#a98352'];  // the dory ramp: ash steps + spreaders
  const RUBBER = ['#101317','#1d2127','#2b323a','#3d454e'];                                 // the four moulded bottom steps
  const KEY    = '#12161a';
  const MATS = { steel:{ramp:STEEL,off:0}, rope:{ramp:ROPE,off:0}, wood:{ramp:WOOD,off:0}, rubber:{ramp:RUBBER,off:-1} };
  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- the two kinds (metres; every number a ladder is built from lives here) ----
  const KINDS = {
    rigid: { width:0.40, standoff:0.10, rail:0.022, rung:0.016, pitch:0.28, grab:0.50, inboard:0.22,
             submerge:0.62, footEvery:2.4 },
    pilot: { width:0.52, standoff:0.07, rope:0.020, stepDepth:0.115, stepThick:0.030, pitch:0.33,
             rubberSteps:4, firstSpreader:4, spreaderEvery:9, spreaderLength:1.80, inboard:0.45,
             tieDrop:0.35, submerge:0.30, stanchion:1.00, stanchionOut:0.18 },
  };

  // ---- generic solids (the fleet's own tube; a box in the ladder's own frame) ----
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  function tube(P0,P1,rad,mat,b){
    const ax=v_norm(v_sub(P1,P0)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax);
    const ring=(P)=>[ v_add(v_add(P,v_mul(r,rad)),v_mul(u,rad)), v_add(v_add(P,v_mul(r,-rad)),v_mul(u,rad)),
                      v_add(v_add(P,v_mul(r,-rad)),v_mul(u,-rad)), v_add(v_add(P,v_mul(r,rad)),v_mul(u,-rad)) ];
    const r0=ring(P0), r1=ring(P1), out=[];
    for(let k=0;k<4;k++){ const k2=(k+1)%4; out.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat,b:b||0,db:-0.15}); }
    return out;
  }

  /* The ladder's own frame at a spec: a = along the hull (t), b = out from the skin (n), z = up.
     t = [ny,-nx] keeps (t, n, z) right-handed, so a face wound for an outward normal stays outward. */
  function frame(spec){
    const m=Math.hypot(spec.outward[0],spec.outward[1])||1, n=[spec.outward[0]/m, spec.outward[1]/m], t=[n[1],-n[0]];
    const x=spec.at[0], y=spec.at[1];
    return { n, t, P:(a,b,z)=>[x+t[0]*a+n[0]*b, y+t[1]*a+n[1]*b, z] };
  }
  function boxIn(P,c,h,mat,b){
    const Q=(sa,sb,sz)=>P(c[0]+sa*h[0], c[1]+sb*h[1], c[2]+sz*h[2]);
    const f=(v)=>({v,mat,b:b||0,db:-0.10});
    return [ f([Q(-1,-1,1),Q(1,-1,1),Q(1,1,1),Q(-1,1,1)]),   f([Q(-1,1,-1),Q(1,1,-1),Q(1,-1,-1),Q(-1,-1,-1)]),
             f([Q(-1,1,1),Q(1,1,1),Q(1,1,-1),Q(-1,1,-1)]),   f([Q(1,-1,1),Q(-1,-1,1),Q(-1,-1,-1),Q(1,-1,-1)]),
             f([Q(1,1,1),Q(1,-1,1),Q(1,-1,-1),Q(1,1,-1)]),   f([Q(-1,-1,1),Q(-1,1,1),Q(-1,1,-1),Q(-1,-1,-1)]) ];
  }

  function kindOf(spec){ const K=KINDS[spec.kind]; if(!K) throw new Error("BoatLadder: unknown kind '"+spec.kind+"'"); return K; }
  function footZ(spec){ return spec.zBottom!=null ? spec.zBottom : spec.zWater - kindOf(spec).submerge; }
  const thick=(spec,nominal)=>Math.max(nominal, 0.8/(spec.px||32));   // never thinner than a pixel centre can catch

  function facesRigid(spec){
    const K=KINDS.rigid, {P}=frame(spec), out=[], add=(fs)=>{ for(const f of fs) out.push(f); };
    const hw=K.width/2, so=K.standoff, rr=thick(spec,K.rail), rg=thick(spec,K.rung);
    const zB=footZ(spec), zT=spec.zTop, zG=zT+K.grab;
    const feet=[zT-0.12];
    for(let z=zT-0.12-K.footEvery; z>spec.zWater+0.30; z-=K.footEvery) feet.push(z);
    feet.push(spec.zWater+0.18);
    for(const s of [-1,1]){
      add(tube(P(s*hw,so,zB), P(s*hw,so,zG), rr,'steel',0.15));                    // the rail
      add(tube(P(s*hw,so,zG), P(s*hw,-K.inboard,zG), rr,'steel',0.30));            // over the top of the side
      add(tube(P(s*hw,-K.inboard,zG), P(s*hw,-K.inboard,zT+0.02), rr,'steel',0.15)); // landed on the covering board
      for(const z of feet) add(tube(P(s*hw,0,z), P(s*hw,so,z), rr,'steel',-0.20)); // stand-off feet
    }
    for(let z=zB+0.06; z<zT+0.02; z+=K.pitch) add(tube(P(-hw,so,z), P(hw,so,z), rg,'steel',0.05));
    return out;
  }

  function facesPilot(spec){
    const K=KINDS.pilot, {P}=frame(spec), out=[], add=(fs)=>{ for(const f of fs) out.push(f); };
    const hw=K.width/2, so=K.standoff, rp=thick(spec,K.rope), hz=thick(spec,K.stepThick/2);
    const zB=footZ(spec), zT=spec.zTop, zC=zT+0.04;
    for(const s of [-1,1]){
      add(tube(P(s*hw,so,zB-0.10), P(s*hw,so,zC), rp,'rope',0.10));                       // side rope
      add(tube(P(s*hw,so,zC), P(s*hw,-K.inboard,zC), rp,'rope',0.30));                    // over the bulwark cap
      add(tube(P(s*hw,-K.inboard,zC), P(s*hw,-K.inboard,zT-K.tieDrop), rp,'rope',0.00));  // made fast inside
      const a=s*(hw+K.stanchionOut);
      add(tube(P(a,-0.05,zT), P(a,-0.05,zT+K.stanchion), thick(spec,0.025),'steel',0.15)); // rail stanchion
    }
    let i=0;
    for(let z=zB; z<zT-0.15; z+=K.pitch, i++){
      const spreader = i>=K.firstSpreader && (i-K.firstSpreader)%K.spreaderEvery===0;
      const ha = spreader ? K.spreaderLength/2 : hw+0.04;
      add(boxIn(P, [0,so,z], [ha,K.stepDepth/2,hz], i<K.rubberSteps?'rubber':'wood', spreader?0.35:0.20));
    }
    return out;
  }

  function faces(spec){ return spec.kind==='pilot' ? facesPilot(spec) : (kindOf(spec), facesRigid(spec)); }

  const r3=(v)=>Math.round(v*1000)/1000;
  function anchors(spec){
    const K=kindOf(spec), {n,P}=frame(spec), zB=footZ(spec);
    const grab = spec.kind==='pilot' ? spec.zTop+K.stanchion : spec.zTop+K.grab;
    return { point:[r3(spec.at[0]),r3(spec.at[1])], outward:[r3(n[0]),r3(n[1])],
             head:P(0,K.standoff,spec.zTop).map(r3), foot:P(0,K.standoff,zB).map(r3),
             grabTop:r3(grab), waterline:r3(spec.zWater), rungsUnderWater:Math.max(0,Math.floor((spec.zWater-zB)/K.pitch)+ (spec.kind==='pilot'?1:0)) };
  }
  function pivot(spec){ return [spec.at[0], spec.at[1], spec.zTop]; }

  // ---- preview: the ladders over a hull's own render, through the hull's own cell (shared recipe) ----
  function camBasis(dir,opts,elev){
    const th=dir*Math.PI/4, e=(opts.elev!=null?opts.elev:(elev!=null?elev:DEFAULT_ELEV))*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch), heave:(opts.heave||0) };
  }
  function projVert(x,y,z,B,C){
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:C.cx+xr*C.S, sy:C.cy-(yr*B.se+zr*B.ce)*C.S - B.heave, d:(yr*B.ce-zr*B.se) };
  }
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n,se,ce,L){ return n[0]*L[0] + (n[1]*se+n[2]*ce)*L[1] + (-n[1]*ce+n[2]*se)*L[2]; }
  function raster(f,B,C,hit){
    const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B,C));
    for(let t=1;t+1<rv.length;t++){
      const a=rv[0], b=rv[t], c=rv[t+1];
      const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx))), maxX=Math.min(C.W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
      const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy))), maxY=Math.min(C.H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
      const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
      if(Math.abs(area)<1e-6) continue;
      for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
        const px=x+0.5, py=y+0.5;
        const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
        const w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
        const w2=1-w0-w1;
        if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
        hit(y*C.W+x, x, y, (w0*a.d+w1*b.d+w2*c.d)-(f.db||0), rv);
      }
    }
  }
  const hex=(c)=>[parseInt(c.slice(1,3),16),parseInt(c.slice(3,5),16),parseInt(c.slice(5,7),16)];
  function renderOn(hull, dir, opts){
    opts=opts||{};
    const C=hull.loft.cell, SH=hull.loft.shade, L=SH.LN||LN, bayer=SH.BAYER||BAYER;
    const pose={ elev:opts.elev, roll:opts.roll, pitch:opts.pitch, heave:opts.heave };
    const out=new Uint8ClampedArray(hull.render(dir, pose));
    const B=camBasis(dir, pose, hull.defaultElev);
    const zbuf=new Float32Array(C.W*C.H).fill(Infinity);
    for(const f of hull.faces()) raster(f,B,C,(i,x,y,d)=>{ if(d<zbuf[i]) zbuf[i]=d; });
    const mine=new Uint8Array(C.W*C.H);
    for(const spec of (opts.ladders||[])) for(const f of faces(spec)){
      const M=MATS[f.mat]||MATS.steel; let fidx=null;
      raster(f,B,C,(i,x,y,d,rv)=>{
        if(!(d<zbuf[i])) return;
        if(fidx===null) fidx=shadeOf(normal(rv[0],rv[1],rv[2]),B.se,B.ce,L)*(SH.GAIN||GAIN)+(SH.BIAS||BIAS)+(f.b||0);
        const base=Math.floor(fidx), idx=base+((fidx-base)>bayer[x&3][y&3]?1:0)+M.off;
        const c=hex(M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))]);
        zbuf[i]=d; mine[i]=1; out[i*4]=c[0]; out[i*4+1]=c[1]; out[i*4+2]=c[2]; out[i*4+3]=255;
      });
    }
    const k=hex(SH.KEY||KEY);                       // 1px keyline where a ladder meets empty cell
    for(let y=0;y<C.H;y++) for(let x=0;x<C.W;x++){
      const i=y*C.W+x; if(out[i*4+3]!==0) continue;
      if((x>0&&mine[i-1])||(x<C.W-1&&mine[i+1])||(y>0&&mine[i-C.W])||(y<C.H-1&&mine[i+C.W])){
        out[i*4]=k[0]; out[i*4+1]=k[1]; out[i*4+2]=k[2]; out[i*4+3]=255; }
    }
    return out;
  }

  /* F: the canonical unit ladder (a 1.2 m rigid stern ladder at the origin, facing aft) so the
     export has the same static symbols a hull rig has. Real ladders come from faces(spec). */
  const F = faces({ kind:'rigid', at:[0,0], outward:[0,-1], zTop:1.2, zWater:0.5, px:32 });

  root.BoatLadder = { KINDS, faces, anchors, pivot, renderOn, F, MATS, GAIN, BIAS, LN, BAYER,
    STEEL, ROPE, WOOD, RUBBER, KEY, defaultElev:DEFAULT_ELEV };
})(typeof globalThis!=='undefined'?globalThis:window);
