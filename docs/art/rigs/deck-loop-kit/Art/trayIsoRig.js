/* Hidden Harbours — STACK-NEST FISH TRAY, iso. The shallow tray the catch is graded
   into before it goes in a tote — the reshape the catch-handling pass listed as next.
   Fleet turntable via Art/isoSolid.js (45deg CW, elev 40, 32 px = 1 m, dither, no AA),
   RINGLESS (ADR 0031, {keyline:true} kept as the A/B).

   The real trick of this tray is the one the name carries: tapered walls and a stepped
   rim, so empties NEST down into each other (0.075 m a tray) and full ones turned end
   for end STACK clear of the catch (0.25 m a tray). Both steps are data — nestStep()
   and stackStep() — and the layout planner counts deck space with them.

   PASS 2: the wall is DEEPER — 0.25 m, a proper lobster pan rather than a tray, with a
   moulded rib round the middle to break the taller face. The stack step follows it, so
   a column of full pans reads as a column and not as a smear.

   0.72 x 0.44 x 0.25 m. Cell 48x44, pivot (24,34) = ground under the centre. HOLLOW
   like the tote: slots(dir,fill) returns projected fill points, opening(dir) the rim
   quad to clip to, and the page blits CatchKit / TrapCatch items onto them.

   Exposes globalThis.FishTray2 = { W,H,pivot,COLOURS,CORDER,FILLS,render(dir,opts),
     slots(dir,fill), opening(dir), nestStep(elev), stackStep(elev) }. */
(function (root) {
  const K = root.IsoSolid;
  const W = 48, H = 44, cx = 24, cy = 34, S = 32;
  const KEY = '#0f1614';

  const GREY =['#22282b','#333c40','#4a565b','#66757b','#8b9ba1'];
  const BLUE =['#152234','#1f3450','#2d4c71','#3f6a99','#5b8cbd'];
  const BLACK=['#0b0e10','#141a1d','#212a2e','#313d42','#465458'];
  const RED  =['#4a100e','#7c1a15','#a8241b','#cf3626','#e2573c'];
  const YELL =['#5a4a12','#87701c','#b2952c','#d6b842','#ecd469'];
  const COLOURS = { grey:{label:'GREY',ramp:GREY}, blue:{label:'BLUE',ramp:BLUE},
    black:{label:'BLACK',ramp:BLACK}, red:{label:'RED',ramp:RED}, yellow:{label:'YELLOW',ramp:YELL} };
  const CORDER = ['grey','blue','black','red','yellow'];
  const FILLS = ['empty','few','half','full','brim'];

  const LX=0.36, LY=0.22, HZ=0.25, TAPER=0.055;    // half-extents at the rim; walls draw in
  const bx=LX-TAPER, by=LY-TAPER;                  // half-extents at the floor

  function quad(a,b,c,d,mat,bi,db,tag){ return { v:[a,b,c,d], mat, b:bi||0, db:db||0, tag:tag||null }; }
  function facesOf(){
    const F=[], push=(a)=>{ for(const f of a) F.push(f); };
    const T=(sx,sy)=>[sx*LX, sy*LY, HZ], B=(sx,sy)=>[sx*bx, sy*by, 0.018];
    // outer tapered walls
    F.push(quad(T(-1,1),T(1,1),B(1,1),B(-1,1),'WALL',0.05,0,'shell'));
    F.push(quad(T(1,-1),T(-1,-1),B(-1,-1),B(1,-1),'WALL',-0.05,0,'shell'));
    F.push(quad(T(1,1),T(1,-1),B(1,-1),B(1,1),'WALL',-0.2,0,'shell'));
    F.push(quad(T(-1,-1),T(-1,1),B(-1,1),B(-1,-1),'WALL',0.35,0,'shell'));
    // underside + pallet ribs
    F.push(quad(B(-1,1),B(1,1),B(1,-1),B(-1,-1),'WALL',-1.0,0,'base'));
    for (const y of [-by*0.62, by*0.62]) push(K.box([0,y,0.009],[bx*0.9,0.022,0.009],'WALL',-0.7,0,null,'base'));
    // inner void — floor and four inner walls, one ramp step darker
    push(K.box([0,0,0.032],[bx*0.93,by*0.93,0.007],'INNER',-1.1,0.012,null,'base'));
    F.push(quad([-LX*0.94,LY*0.9,HZ-0.012],[LX*0.94,LY*0.9,HZ-0.012],[bx*0.93,by*0.93,0.036],[-bx*0.93,by*0.93,0.036],'INNER',-0.8,0.01,'base'));
    F.push(quad([LX*0.94,-LY*0.9,HZ-0.012],[-LX*0.94,-LY*0.9,HZ-0.012],[-bx*0.93,-by*0.93,0.036],[bx*0.93,-by*0.93,0.036],'INNER',-0.5,0.01,'shell'));
    F.push(quad([LX*0.94,LY*0.9,HZ-0.012],[LX*0.94,-LY*0.9,HZ-0.012],[bx*0.93,-by*0.93,0.036],[bx*0.93,by*0.93,0.036],'INNER',-0.6,0.01,'shell'));
    F.push(quad([-LX*0.94,-LY*0.9,HZ-0.012],[-LX*0.94,LY*0.9,HZ-0.012],[-bx*0.93,by*0.93,0.036],[-bx*0.93,-by*0.93,0.036],'INNER',-0.4,0.01,'shell'));
    // moulded rib round the middle of the deeper wall (follows the taper exactly)
    (function(){ const t=0.52, rx=bx+(LX-bx)*t, ry=by+(LY-by)*t;
      push(K.box([0,0,HZ*t],[rx+0.007,ry+0.007,0.011],'WALL',0.55,0,null,'shell')); })();
    // stepped rim — the frame that lets a full tray stack instead of nest
    push(K.box([0, LY,HZ+0.008],[LX+0.008,0.016,0.011],'WALL',0.35,0,null,'shell'));
    push(K.box([0,-LY,HZ+0.008],[LX+0.008,0.016,0.011],'WALL',0.35,0,null,'shell'));
    push(K.box([ LX,0,HZ+0.008],[0.016,LY+0.008,0.011],'WALL',0.35,0,null,'shell'));
    push(K.box([-LX,0,HZ+0.008],[0.016,LY+0.008,0.011],'WALL',0.35,0,null,'shell'));
    // stacking lugs at the corners (what the tray above lands on)
    for (const sx of [-1,1]) for (const sy of [-1,1])
      push(K.box([sx*(LX-0.045), sy*(LY-0.03), HZ+0.024],[0.038,0.024,0.014],'WALL',0.5,0,null,'shell'));
    // hand slots in the ends
    for (const sx of [-1,1]) push(K.box([sx*LX, 0, HZ-0.05],[0.02,0.075,0.022],'INNER',-1.3,0.02,null,'shell'));
    // drain slots along the floor edge
    for (const x of [-0.20,-0.07,0.07,0.20]) push(K.box([x,-by,0.03],[0.028,0.02,0.008],'INNER',-1.3,0.02,null,'base'));
    return F;
  }
  let _F=null;
  function render(dir, opts){
    opts=opts||{};
    if(!_F) _F=facesOf();
    const ramp=(COLOURS[opts.colour]||COLOURS.grey).ramp;
    return K.paint(_F, { W,H,cx,cy, dir, elev:opts.elev, key:KEY,
      keyline: opts.keyline!=null?opts.keyline:false,
      MATS:{ WALL:{ramp,off:0}, INNER:{ramp,off:-1} } });
  }
  const FILLZ={ empty:0, few:1, half:2, full:2, brim:3 };
  function slots(dir, fill, opts){
    opts=opts||{};
    const B=K.camBasis({dir, elev:opts.elev});
    const rng=K.mulberry(29), pts=[];
    const layers=Math.max(1, FILLZ[fill]!=null?FILLZ[fill]:2);
    for (let L=0; L<layers; L++){
      const lz=0.10+L*0.078, lp=[];
      for (let gy=-1; gy<=1; gy+=2) for (let i=0;i<4;i++){
        const gx=-0.24+i*0.16;
        const v=K.proj(gx+(rng()-0.5)*0.06+(L%2?0.05:0), gy*0.105+(rng()-0.5)*0.05+(L%2?0.04:0), lz, B, cx, cy);
        lp.push({ dx:Math.round(v.sx-cx), dy:Math.round(v.sy-cy), sy:v.sy });
      }
      lp.sort((a,b)=>a.sy-b.sy); pts.push.apply(pts,lp);
    }
    return pts;
  }
  function opening(dir, opts){
    opts=opts||{};
    const B=K.camBasis({dir, elev:(opts||{}).elev});
    return [[-LX*0.97,-LY*0.94],[LX*0.97,-LY*0.94],[LX*0.97,LY*0.94],[-LX*0.97,LY*0.94]].map(([x,y])=>{
      const v=K.proj(x,y,HZ,B,cx,cy); return { dx:Math.round(v.sx-cx), dy:Math.round(v.sy-cy) };
    });
  }
  const px=(m,elev)=>Math.round(m*S*Math.cos(((elev!=null?elev:K.DEFAULT_ELEV))*K.DEG));
  root.FishTray2 = { W,H, pivot:{x:cx,y:cy}, KEY, COLOURS, CORDER, FILLS,
    dims:{ l:LX*2, w:LY*2, h:HZ }, nest_m:0.075, stack_m:0.25,
    nestStep:(e)=>px(0.075,e), stackStep:(e)=>px(0.25,e),
    defaultElev:K.DEFAULT_ELEV, render, slots, opening };
})(typeof globalThis!=='undefined'?globalThis:window);
