/* Hidden Harbours — CLAM HOD. The wire roller basket a clam digger drags along the flat and
   rinses the catch in — the one container the clam dig needs that the kit did not have.
   Art/isoSolid.js turntable (45deg CW, elev 40, 32 px = 1 m, dither, no AA), RINGLESS (ADR 0031;
   {keyline:true} is the A/B). 0.42 x 0.30 x 0.20 m: galvanised wire (bucketRig's STEEL ramp) on a
   bent-wire frame, an ash bail with a roller grip (bucketRig's WOOD). Wires bake at 1 px (0.012 m
   half-thickness) so the basket reads as mesh the clams show through.
   HOLLOW like the tote / tray / trap: render(dir,{layer:'back'}) is the far half of the wire,
   render(dir,{layer:'front'}) the near half — blit the CatchKit2 heap between them, clipped to
   opening(dir) and lowered by (1 - fill) x depthPx(). Cell 40x40, pivot (20,30) = ground centre;
   cpivot(dir) = the roller grip (pins to a CharacterIso carry anchor when carried).
   Exposes globalThis.ClamHod = { W,H,pivot,cpivot,dims,FILLS,render,opening,depthPx }. */
(function (root) {
  const K = root.IsoSolid;
  const W = 40, H = 40, cx = 20, cy = 30, S = 32;
  const STEEL=['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2'], WOOD=['#33271b','#473627','#5e4630','#6b4f35','#8a6a48'];
  const LX=0.21, LY=0.15, HZ=0.20, R=0.012, Z0=0.02, TAPER=0.03, GZ=HZ+0.17;
  const FILLS=['empty','few','half','full','brim'];
  function facesOf(){
    const F=[], push=(a)=>{ for (const f of a) F.push(f); };
    const bx=LX-TAPER, by=LY-TAPER;
    const T=(sx,sy)=>[sx*LX, sy*LY, HZ], B=(sx,sy)=>[sx*bx, sy*by, Z0];
    const bar=(a,b,r,m,bi)=>push(K.bar(a,b,r,m,bi,0,null));
    bar(T(-1,-1),T(1,-1),R,'STEEL',0.35); bar(T(1,-1),T(1,1),R,'STEEL',0.35); bar(T(1,1),T(-1,1),R,'STEEL',0.35); bar(T(-1,1),T(-1,-1),R,'STEEL',0.35);
    bar(B(-1,-1),B(1,-1),R,'STEEL',-0.4); bar(B(1,-1),B(1,1),R,'STEEL',-0.4); bar(B(1,1),B(-1,1),R,'STEEL',-0.4); bar(B(-1,1),B(-1,-1),R,'STEEL',-0.4);
    for (let i=0;i<3;i++){ const t=-1+i;     for (const sy of [-1,1]) bar(B(t,sy),T(t,sy),R*0.8,i===1?'WIRE':'STEEL',i===1?0:0.1); }
    for (const sx of [-1,1]) bar(B(sx,0),T(sx,0),R*0.8,'WIRE',0);
    const mz=Z0+(HZ-Z0)*0.5, mx=bx+(LX-bx)*0.5, my=by+(LY-by)*0.5;
    for (const [a,b] of [[[-mx,-my],[mx,-my]],[[mx,-my],[mx,my]],[[mx,my],[-mx,my]],[[-mx,my],[-mx,-my]]]) bar([a[0],a[1],mz],[b[0],b[1],mz],R*0.8,'WIRE',0);
    for (const x of [-0.10,0.10]) bar([x,-by,Z0],[x,by,Z0],R*0.8,'WIRE',-0.6);
    for (const sx of [-1,1]){ bar([sx*LX,0,HZ],[sx*LX*0.85,0,HZ+0.10],R,'STEEL',0.2); bar([sx*LX*0.85,0,HZ+0.10],[sx*0.075,0,GZ],R,'STEEL',0.2); }
    push(K.box([0,0,GZ],[0.075,0.014,0.014],'WOOD',0.1,0.01));
    return F;
  }
  let _F=null;
  function render(dir, opts){
    opts=opts||{}; if (!_F) _F=facesOf();
    const layer=opts.layer||'all', B=K.camBasis({dir, elev:opts.elev});
    const depthOf=(f)=>{ let x=0,y=0,z=0; for (const p of f.v){ x+=p[0]; y+=p[1]; z+=p[2]; } x/=f.v.length; y/=f.v.length; z/=f.v.length;
      return (x*B.stt+y*B.ct)*B.ce - z*B.se; };
    const emit = layer==='all' ? null : (f)=>{ const d=depthOf(f); return layer==='front' ? d<-0.02 : d>=-0.02; };
    return K.paint(_F, { W,H,cx,cy, dir, elev:opts.elev, keyline:opts.keyline===true,
      MATS:{ STEEL:{ramp:STEEL,off:0}, WIRE:{ramp:STEEL,off:-1}, WOOD:{ramp:WOOD,off:0} }, emit });
  }
  function opening(dir, opts){
    const B=K.camBasis({dir, elev:(opts||{}).elev});
    return [[-LX*0.92,-LY*0.88],[LX*0.92,-LY*0.88],[LX*0.92,LY*0.88],[-LX*0.92,LY*0.88]].map(([x,y])=>{
      const v=K.proj(x,y,HZ,B,cx,cy); return { dx:Math.round(v.sx-cx), dy:Math.round(v.sy-cy) }; });
  }
  const depthPx=(elev)=>Math.round((HZ-Z0)*Math.cos((elev!=null?elev:K.DEFAULT_ELEV)*K.DEG)*S);
  const cpivot=(dir,elev)=>{ const v=K.proj(0,0,GZ,K.camBasis({dir,elev}),cx,cy); return { x:Math.round(v.sx), y:Math.round(v.sy) }; };
  root.ClamHod = { W, H, pivot:{x:cx,y:cy}, cpivot, dims:{ l:LX*2, w:LY*2, h:HZ }, FILLS, defaultElev:K.DEFAULT_ELEV, render, opening, depthPx };
})(typeof globalThis!=='undefined'?globalThis:window);
