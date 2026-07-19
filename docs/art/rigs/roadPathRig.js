/* Hidden Harbours — parametric ROAD / PATH / SIDEWALK tile kit.
   Companion to shorelineKitRig.js / wharfKitRig.js. Same KTC pixel conventions:
   no AA, upper-left key light, quantised muted North-Atlantic ramps, hash-value noise.
   FLAT 32x32 near-plan ground tiles (camera from the south) — roads sit IN the ground
   plane exactly like Grass.png / Dirt.png, so they auto-register with the iso houses,
   the wharf deck and the shoreline flats.

   Autotiling: full 47-tile BLOB (edge + corner). A tile fills to the border on every
   CONNECTED side and pulls in to an organic grass/earth verge on every disconnected
   side; two disconnected adjacent sides round the outer corner (caps, bends, blobs);
   two connected sides whose diagonal is empty carve a concave grass fillet (the T / +
   armpits). Structured surfaces (brick / cobble / concrete joints / lane markings) are
   phased on GLOBAL tile coords (opts.gx,opts.gy) so they run seamlessly across the map.

   API (globalThis.RoadKit):
     RoadKit.TILE = 32
     RoadKit.SURFACES = ['dirt','gravel','concrete','asphalt','cobble','sand','brick']
     RoadKit.WEAR     = ['new','worn','cracked']
     RoadKit.GROUNDS  = ['grass','dirt','sand']
     RoadKit.render(surface, opts) -> { data:Uint8ClampedArray, w:32, h:32 }
         opts = {
           con:{n,e,s,w},            // edges connected to same-family road
           diag:{ne,nw,se,sw},       // diagonal neighbour present (for concave fillets)
           wear:'new'|'worn'|'cracked',
           ground:'grass'|'dirt'|'sand',
           markings:[ 'edge','centerDash','centerDouble','laneDash','crosswalk','stop','curb' ],
           axis:'v'|'h'|'x',         // travel axis (auto from con if omitted)
           gx,gy,                    // global tile coords for seamless phase
           seed
         }
     RoadKit.BLOB47                    -> [{mask,con,diag,label}]  canonical 47-tile set
     RoadKit.canon(mask)               -> canonical 8-bit neighbour mask
     RoadKit.fromMask(mask)            -> {con,diag,axis}
     RoadKit.PAL                       -> palette object
*/
(function(root){
  const TILE=32;

  const PAL={
    // muted, desaturated North-Atlantic ground surfaces
    dirt   :['#3a3328','#4a4234','#5b5142','#6d6250','#837661','#998b73'],   // greige packed earth (matches Dirt.png)
    gravel :['#3a3d3b','#4a4e4b','#5b605c','#6e736e','#848983','#9ba09a'],   // cool loose stone
    concrete:['#54595a','#69706f','#7f8584','#949a98','#a9afac','#bec3c0'],  // pale neutral sidewalk
    asphalt:['#23262a','#2d3135','#393e42','#464c50','#555b5f','#666d71'],   // charcoal road
    cobble :['#41474f','#4e5760','#5f6a74','#727e89','#8792a0','#9fabb8'],   // muted slate-blue setts
    sand   :['#7c5f39','#96784d','#b39668','#cbb184','#ddc9a0','#ece0c1'],   // warm path sand
    brick  :['#573024','#6f3f2f','#83503b','#956149','#a67459','#b6875f'], // clay brick
    mortar :'#7c7466',
    // ground the shoulders blend into
    grass  :['#2b3a1d','#3c4d27','#4d6131','#61773e','#788e51','#8ea065'],
    gsoil  :'#453a2b',
    // markings — muted so they read painted, not neon
    yellow :['#9c7d2c','#bd9a3a','#d4b04e'],
    white  :['#8f968f','#aab0a8','#c8ccc2'],
    weed   :['#3c4d27','#4d6131','#61773e'],
    KEY:'#14100a', shadow:'#0d0f11'
  };

  function hash(x,y,s){ let h=(x*374761393+y*668265263+((s|0))*1274126177)|0;
    h=Math.imul(h^(h>>>13),1274126177); return ((h^(h>>>16))>>>0)/4294967296; }
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function mix(a,b,t){ const A=Array.isArray(a)?a:hex2rgb(a),B=Array.isArray(b)?b:hex2rgb(b);
    return [A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t]; }
  function ramp(r,i){ i=Math.max(0,Math.min(r.length-1,Math.round(i))); return r[i]; }
  function newBuf(){ return new Uint8ClampedArray(TILE*TILE*4); }
  function wearNum(w){ return w==='cracked'?1 : w==='worn'?0.5 : 0; }

  // ---------------------------------------------------------------- surface texel
  // returns a colour for GLOBAL pixel (X,Y). structured patterns phase on X,Y so the
  // surface is seamless across neighbouring tiles.
  function surfaceColor(surface, X, Y, wf, seed){
    const R=PAL[surface];
    switch(surface){
      case 'dirt': {
        let idx=2+Math.round(hash(X,Y,seed+1)*2); let c=ramp(R,idx);
        if(hash(X>>1,Y>>1,seed+2)<0.18) c=ramp(R,idx+1);           // dry clod highlight
        if(hash(X,Y,seed+3)<0.10) c=ramp(R,idx-1);                 // damp fleck
        if(hash(X,Y,seed+4)<0.04) c=ramp(R,1);                     // pebble shadow
        if(hash(X,Y,seed+5)<0.02) c=ramp(R,5);
        return c;
      }
      case 'gravel': {
        // small packed stones ~3px, each with a lit top and dark base
        const cs=3, cx=Math.floor(X/cs), cy=Math.floor(Y/cs);
        const jx=Math.round(hash(cx,cy,seed+6)*2-1), jy=Math.round(hash(cx,cy,seed+7)*2-1);
        const sx=Math.floor((X+jx)/cs), sy=Math.floor((Y+jy)/cs);
        let idx=2+Math.round(hash(sx,sy,seed+8)*3); idx=Math.max(1,Math.min(5,idx));
        let c=ramp(R,idx);
        const ly=((Y+jy)%cs+cs)%cs;
        if(ly===0) c=ramp(R,idx+1);                                // lit top of stone
        if(ly===cs-1) c=ramp(R,idx-2);                             // shaded gap
        if(hash(X,Y,seed+9)<0.05) c=ramp(R,5);
        return c;
      }
      case 'concrete': {
        let idx=3+Math.round(hash(X>>1,Y>>1,seed+10)*1.4); let c=ramp(R,idx);
        if(hash(X,Y,seed+11)<0.06) c=ramp(R,idx-1);                // fine aggregate
        if(hash(X,Y,seed+12)<0.03) c=ramp(R,idx+1);
        // scored expansion joints on the 32px grid (per slab)
        const jl=((X%32)+32)%32, jt=((Y%32)+32)%32;
        if(jl===0||jt===0) c=ramp(R,1);
        if(jl===1||jt===1) c=ramp(R,5);                            // lit lip beside the joint
        return c;
      }
      case 'asphalt': {
        let idx=2+Math.round(hash(X,Y,seed+13)*2); let c=ramp(R,idx);
        if(hash(X,Y,seed+14)<0.14) c=ramp(R,idx+1);                // lighter aggregate grit
        if(hash(X,Y,seed+15)<0.05) c=ramp(R,5);                    // pale chip
        if(hash(X,Y,seed+16)<0.05) c=ramp(R,1);
        return c;
      }
      case 'cobble': {
        // domed setts in offset courses ~6x5px
        const rh=5, course=Math.floor(Y/rh);
        const off=(course&1)?3:0;
        const cw=6, col=Math.floor((X+off)/cw);
        const lx=((X+off)%cw+cw)%cw, ly=((Y)%rh+rh)%rh;
        let idx=2+Math.round(hash(col,course,seed+17)*3); idx=Math.max(1,Math.min(5,idx));
        let c=ramp(R,idx);
        // dome shading: lit upper-left, dark lower-right, dark mortar frame
        const dome=(lx/cw-0.4)+(ly/rh-0.4);
        if(dome<-0.5) c=ramp(R,idx+2);
        else if(dome<0.1) c=ramp(R,idx+1);
        else if(dome>0.7) c=ramp(R,idx-1);
        if(lx===0||ly===0||lx===cw-1||ly===rh-1) c=ramp(R,0);      // mortar gap
        if(hash(X,Y,seed+18)<0.05) c=ramp(R,idx);
        return c;
      }
      case 'sand': {
        let idx=3+Math.round(hash(X,Y,seed+19)*1.6); let c=ramp(R,idx);
        if(hash(X,Y,seed+20)<0.06) c=ramp(R,5);
        if(hash(X,Y,seed+21)<0.04) c=ramp(R,1);
        // faint wind ripple bands
        const rip=Math.sin(X*0.4+Math.cos(Y*0.3+seed)*1.2);
        if(rip>0.8) c=ramp(R,idx+1); else if(rip<-0.9) c=ramp(R,idx-1);
        return c;
      }
      case 'brick': {
        // running bond: bricks ~7x4 with mortar, alternate courses offset
        const bh=4, course=Math.floor(Y/bh);
        const off=(course&1)?4:0, bw=7;
        const lx=((X+off)%bw+bw)%bw, ly=((Y)%bh+bh)%bh;
        if(lx===0||ly===0) return mix(PAL.mortar, PAL.KEY, ly===0?0.12:0.0); // mortar lines
        const bcol=Math.floor((X+off)/bw);
        let idx=2+Math.round(hash(bcol,course,seed+22)*3); idx=Math.max(1,Math.min(5,idx));
        let c=ramp(R,idx);
        if(ly===1) c=ramp(R,idx+1);                                // lit top of brick
        if(ly===bh-1) c=ramp(R,idx-1);                             // shaded base
        if(hash(X,Y,seed+23)<0.06) c=ramp(R,idx-1);
        return c;
      }
    }
    return hex2rgb(R?R[2]:'#888');
  }

  // ---------------------------------------------------------------- ground texel
  function groundColor(ground, X, Y, seed){
    if(ground==='grass'){
      let gi=2+Math.round(hash(X,Y,seed+30)*2); let c=ramp(PAL.grass,gi);
      if(hash(X,Y,seed+31)<0.10) c=ramp(PAL.grass,5);
      if(hash(X,Y,seed+32)<0.06) c=ramp(PAL.grass,1);
      if(hash(X,Y,seed+33)<0.02) c=PAL.gsoil;
      return c;
    }
    if(ground==='dirt') return surfaceColor('dirt', X, Y, 0, seed+40);
    return surfaceColor('sand', X, Y, 0, seed+50);
  }

  // ---------------------------------------------------------------- blob coverage
  function coverage(con, diag, seed){
    const M=4.5, rOut=8, rIn=7.5;
    const wob=(p,s)=> (Math.sin(p*0.5+s)+Math.sin(p*0.21+s*1.7))*0.85 + (hash(p,0,s)-0.5)*1.5;
    return function(x,y){
      if(!con.n){ if(y < M+wob(x,seed+11)) return false; }
      if(!con.s){ if((31-y) < M+wob(x,seed+37)) return false; }
      if(!con.w){ if(x < M+wob(y,seed+53)) return false; }
      if(!con.e){ if((31-x) < M+wob(y,seed+71)) return false; }
      // outer convex round: two adjacent disconnected sides
      if(!con.n&&!con.w){ const cx=M+rOut,cy=M+rOut; if(x<cx&&y<cy&&((x-cx)*(x-cx)+(y-cy)*(y-cy))>rOut*rOut) return false; }
      if(!con.n&&!con.e){ const cx=31-M-rOut,cy=M+rOut; if(x>cx&&y<cy&&((x-cx)*(x-cx)+(y-cy)*(y-cy))>rOut*rOut) return false; }
      if(!con.s&&!con.w){ const cx=M+rOut,cy=31-M-rOut; if(x<cx&&y>cy&&((x-cx)*(x-cx)+(y-cy)*(y-cy))>rOut*rOut) return false; }
      if(!con.s&&!con.e){ const cx=31-M-rOut,cy=31-M-rOut; if(x>cx&&y>cy&&((x-cx)*(x-cx)+(y-cy)*(y-cy))>rOut*rOut) return false; }
      // inner concave fillet: both edges connected, diagonal empty
      const f=(rIn+ (hash(x,y,seed+90)-0.5)*1.6);
      if(con.n&&con.w&&!diag.nw){ if(x*x+y*y < f*f) return false; }
      if(con.n&&con.e&&!diag.ne){ const dx=31-x; if(dx*dx+y*y < f*f) return false; }
      if(con.s&&con.w&&!diag.sw){ const dy=31-y; if(x*x+dy*dy < f*f) return false; }
      if(con.s&&con.e&&!diag.se){ const dx=31-x,dy=31-y; if(dx*dx+dy*dy < f*f) return false; }
      return true;
    };
  }

  // ---------------------------------------------------------------- wear overlay
  // mutates a colour in place at (X,Y); returns possibly-darkened colour + crack flags
  function applyWear(c, surface, X, Y, wf, seed){
    if(wf<=0) return c;
    const R=PAL[surface];
    // grime / faded blotches
    if(hash(X>>1,Y>>1,seed+60) < wf*0.20) c=mix(c, PAL.KEY, 0.13);
    // hairline cracks = contour lines of a smooth noise field (meandering, natural — never striped)
    const f1=Math.sin(X*0.17+seed)+Math.sin(Y*0.13+seed*1.7)+Math.sin((X+Y)*0.06+seed*2.3);
    const thin=0.045+wf*0.05;
    if(Math.abs(f1-Math.round(f1))<thin && hash(X,Y,seed+61)<0.5+wf*0.32){
      c=mix(c, PAL.KEY, 0.45);
      if(surface!=='asphalt' && wf>0.7 && hash(X,Y,seed+63)<0.14) c=ramp(PAL.weed,1+Math.round(hash(X,Y,seed)*2)); // weeds in cracks
    } else if(wf>0.7){                                    // cross-cracks only when badly cracked
      const f2=Math.sin(X*0.11+seed*3.1)+Math.sin(Y*0.19+seed*0.9)+Math.sin((X-Y)*0.05+seed);
      if(Math.abs(f2-Math.round(f2))<0.05 && hash(X,Y,seed+62)<0.6) c=mix(c, PAL.KEY, 0.4);
    }
    // potholes (cracked only): dark irregular pits
    if(wf>0.8){
      const pc=6, px=Math.floor(X/pc), py=Math.floor(Y/pc);
      if(hash(px,py,seed+64)<0.045){
        const cxp=px*pc+pc/2, cyp=py*pc+pc/2, d=Math.hypot(X-cxp,Y-cyp), r=1.4+hash(px,py,seed+65)*2.1;
        if(d<r) c=mix(PAL[surface][0], PAL.KEY, 0.5);
        else if(d<r+1) c=ramp(R,1);
      }
    }
    return c;
  }

  // ---------------------------------------------------------------- markings
  function drawMarkings(put, isRoad, opts, seed){
    const list=opts.markings||[]; if(!list.length) return;
    const wf=wearNum(opts.wear), gx=(opts.gx|0), gy=(opts.gy|0);
    const con=opts.con, axis=opts.axis || (con.n||con.s ? (con.e||con.w?'x':'v') : 'h');
    const paint=(x,y,col,fade)=>{ if(x<0||x>=32||y<0||y>=32) return; if(!isRoad(x,y)) return;
      const a=1-wf*0.55; if(hash(x,y,seed+80) > a) return;                 // fade dropout
      put(x,y, mix(col, col, 0), Math.round(255*(fade==null?1:fade))); };
    const dashOn=(t)=>{ return (((t%12)+12)%12) < 6; };                    // 6 on / 6 off, global-phased
    const Y0=gy*32, X0=gx*32;
    const yel=PAL.yellow, wht=PAL.white;

    // curb: raised concrete lip on disconnected sides
    if(list.includes('curb')){
      for(let i=0;i<32;i++){
        if(!con.n){ let y=0; while(y<32 && !isRoad(i,y)) y++; if(y<32){ paint(i,y,wht[2]); paint(i,y+1,PAL.concrete? PAL.concrete[1]:wht[0]); } }
        if(!con.s){ let y=31; while(y>=0 && !isRoad(i,y)) y--; if(y>=0){ paint(i,y,PAL.concrete[1]); paint(i,y-1,wht[2]); } }
        if(!con.w){ let x=0; while(x<32 && !isRoad(x,i)) x++; if(x<32){ paint(x,i,wht[2]); paint(x+1,i,PAL.concrete[1]); } }
        if(!con.e){ let x=31; while(x>=0 && !isRoad(x,i)) x--; if(x>=0){ paint(x,i,PAL.concrete[1]); paint(x-1,i,wht[2]); } }
      }
    }
    // edge lines (solid white just inside each long verge)
    if(list.includes('edge')){
      if(axis==='v'||axis==='x'){ for(let y=0;y<32;y++){ paint(5,y,wht[2]); paint(26,y,wht[2]); } }
      if(axis==='h'||axis==='x'){ for(let x=0;x<32;x++){ paint(x,5,wht[2]); paint(x,26,wht[2]); } }
    }
    // centre lines
    const centre=(col,dbl,dashed)=>{
      if(axis==='x') return;                                              // clear centre through junctions
      if(axis==='v'){ for(let y=0;y<32;y++){ if(dashed && !dashOn(Y0+y)) continue;
        if(dbl){ paint(14,y,col); paint(17,y,col); } else paint(16,y,col); } }
      else { for(let x=0;x<32;x++){ if(dashed && !dashOn(X0+x)) continue;
        if(dbl){ paint(x,14,col); paint(x,17,col); } else paint(x,16,col); } }
    };
    if(list.includes('centerDouble')) centre(yel[1],true,false);
    if(list.includes('centerDash'))   centre(yel[2],false,true);
    if(list.includes('laneDash'))     centre(wht[2],false,true);
    // crosswalk — zebra bars across the travel axis, filling the road band (skip junction boxes)
    if(list.includes('crosswalk') && axis!=='x'){
      if(axis==='h'){ for(let x=0;x<32;x++){ if((((X0+x)%4)+4)%4 < 2) for(let y=0;y<32;y++) paint(x,y,wht[2],0.95); } }
      else { for(let y=0;y<32;y++){ if((((Y0+y)%4)+4)%4 < 2) for(let x=0;x<32;x++) paint(x,y,wht[2],0.95); } }
    }
    // stop bar — thick white line across near the S approach
    if(list.includes('stop')){
      if(axis==='v'||axis==='x'){ for(let x=0;x<32;x++) for(let y=27;y<30;y++) paint(x,y,wht[2]); }
      else { for(let y=0;y<32;y++) for(let x=27;x<30;x++) paint(x,y,wht[2]); }
    }
  }

  // ---------------------------------------------------------------- render
  function render(surface, opts){
    opts=opts||{};
    const con=Object.assign({n:false,e:false,s:false,w:false}, opts.con||{});
    const diag=Object.assign({ne:false,nw:false,se:false,sw:false}, opts.diag||{});
    const wear=opts.wear||'new', wf=wearNum(wear), ground=opts.ground||'grass';
    const seed=(opts.seed|0), gx=(opts.gx|0), gy=(opts.gy|0);
    const buf=newBuf();
    const put=(x,y,c,a)=>{ if(x<0||x>=TILE||y<0||y>=TILE)return; const rgb=Array.isArray(c)?c:hex2rgb(c);
      const i=(y*TILE+x)*4; if(a!=null && a<255){ const t=a/255; buf[i]=buf[i]*(1-t)+rgb[0]*t; buf[i+1]=buf[i+1]*(1-t)+rgb[1]*t; buf[i+2]=buf[i+2]*(1-t)+rgb[2]*t; buf[i+3]=255; }
        else { buf[i]=rgb[0];buf[i+1]=rgb[1];buf[i+2]=rgb[2];buf[i+3]=255; } };

    const isRoad=coverage(con,diag,seed);
    const road=new Uint8Array(TILE*TILE);
    for(let y=0;y<TILE;y++)for(let x=0;x<TILE;x++) road[y*TILE+x]=isRoad(x,y)?1:0;
    const isR=(x,y)=> (x>=0&&x<TILE&&y>=0&&y<TILE)?road[y*TILE+x]: (
      // off-tile: connected borders read as road so the verge ring doesn't wrap onto seams
      (x<0&&con.w)||(x>=TILE&&con.e)||(y<0&&con.n)||(y>=TILE&&con.s) );

    for(let y=0;y<TILE;y++)for(let x=0;x<TILE;x++){
      const X=gx*32+x, Y=gy*32+y;
      if(road[y*TILE+x]){
        let c=surfaceColor(surface, X, Y, wf, seed);
        c=applyWear(c, surface, X, Y, wf, seed);
        // inner shoulder shade: darken the 1px rim against the verge
        let rim=false;
        for(let d=0;d<4&&!rim;d++){ const nx=x+[1,-1,0,0][d], ny=y+[0,0,1,-1][d]; if(!isR(nx,ny)) rim=true; }
        if(rim) c=mix(c, PAL.KEY, 0.28);
        put(x,y,c);
      } else {
        let c=groundColor(ground, X, Y, seed);
        // verge scuff + fringe hugging the road
        let near=99;
        for(let dy=-2;dy<=2&&near>1;dy++)for(let dx=-2;dx<=2;dx++){ if(isR(x+dx,y+dy)){ const dd=Math.abs(dx)+Math.abs(dy); if(dd<near)near=dd; } }
        if(near<=2){
          if(ground==='grass'){ c = near<=1 ? mix(PAL.gsoil,PAL.KEY,0.15) : mix(c,PAL.gsoil,0.5); }
          else c = mix(c, PAL.KEY, near<=1?0.22:0.12);
        }
        put(x,y,c);
      }
    }

    // grass/weed tufts poking from the verge onto the road edge (organic fringe like ShoreFlats)
    if(ground==='grass'){
      for(let y=0;y<TILE;y++)for(let x=0;x<TILE;x++){
        if(road[y*TILE+x]) continue;
        let onEdge=false;
        for(let d=0;d<4&&!onEdge;d++){ const nx=x+[1,-1,0,0][d], ny=y+[0,0,1,-1][d]; if(isR(nx,ny)) onEdge=true; }
        if(onEdge && hash(gx*32+x,gy*32+y,seed+70)<0.5){
          const g=ramp(PAL.grass,3+Math.round(hash(x,y,seed+71)*2));
          put(x,y,g); if(hash(x,y,seed+72)<0.4) put(x,y-1,ramp(PAL.grass,4));
        }
      }
    }

    drawMarkings(put, (x,y)=>road[y*TILE+x]===1, Object.assign({},opts,{con,wear}), seed);
    return { data:buf, w:TILE, h:TILE };
  }

  // ---------------------------------------------------------------- plain ground tile
  function renderGround(ground, opts){
    opts=opts||{}; const seed=(opts.seed|0), gx=(opts.gx|0), gy=(opts.gy|0);
    const buf=newBuf();
    for(let y=0;y<TILE;y++)for(let x=0;x<TILE;x++){
      const c=groundColor(ground, gx*32+x, gy*32+y, seed);
      const rgb=Array.isArray(c)?c:hex2rgb(c); const i=(y*TILE+x)*4;
      buf[i]=rgb[0];buf[i+1]=rgb[1];buf[i+2]=rgb[2];buf[i+3]=255;
    }
    return { data:buf, w:TILE, h:TILE };
  }

  // ---------------------------------------------------------------- blob-47 set
  const N=1,E=2,S=4,W=8,NE=16,SE=32,SW=64,NW=128;
  function canon(m){
    let r=m & 15;                                       // keep the 4 edges
    if((m&N)&&(m&E)&&(m&NE)) r|=NE;
    if((m&S)&&(m&E)&&(m&SE)) r|=SE;
    if((m&S)&&(m&W)&&(m&SW)) r|=SW;
    if((m&N)&&(m&W)&&(m&NW)) r|=NW;
    return r;
  }
  function fromMask(m){
    const con={ n:!!(m&N), e:!!(m&E), s:!!(m&S), w:!!(m&W) };
    const diag={ ne:!!(m&NE), se:!!(m&SE), sw:!!(m&SW), nw:!!(m&NW) };
    const axis = (con.n||con.s) ? ((con.e||con.w)?'x':'v') : ((con.e||con.w)?'h':'i');
    return {con,diag,axis};
  }
  const BLOB47=(function(){
    const seen={}, out=[];
    for(let m=0;m<256;m++){ const c=canon(m); if(seen[c])continue; seen[c]=1;
      const f=fromMask(c); out.push(Object.assign({mask:c,label:maskLabel(f)}, f)); }
    return out;                                          // 47 entries
  })();
  function maskLabel(f){
    const n=['n','e','s','w'].filter(k=>f.con[k]).length;
    if(n===0) return 'isolated';
    if(n===1) return 'cap';
    if(n===2) return (f.con.n&&f.con.s)||(f.con.e&&f.con.w) ? 'straight' : 'bend';
    if(n===3) return 'tee';
    return 'cross';
  }

  root.RoadKit={ TILE, PAL,
    SURFACES:['dirt','gravel','concrete','asphalt','cobble','sand','brick'],
    WEAR:['new','worn','cracked'], GROUNDS:['grass','dirt','sand'],
    render, renderGround, coverage, surfaceColor, groundColor, canon, fromMask, BLOB47, hash };
})(typeof globalThis!=='undefined'?globalThis:window);
