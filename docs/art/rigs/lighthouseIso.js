/* Hidden Harbours — parametric ISO lighthouse (from uploads ref: Art/_ref/lighthouse_ref.png).
   Pixel-iso projection matching CottageIso: vertical walls, horizontal circles as 2:1 ellipses,
   upper-LEFT key light, 1px #262930 keyline, no AA. 32 px = 1 m.
   Real-world read: ~10.3 m squat harbour light — white tapered octagonal tower, stone pad,
   gallery + railing, glazed lantern, red cap roof + finial, lean-to shed at lower-left (per ref).
   Canvas 244x384, tower axis at x=122 → pivot = bottom-centre (122,384); ground contact y≈382.
   Palette: 100% sampled from CottageIso + GreywickHouseRed (KTC clamp).
   Exposes globalThis.LighthouseIso = { W, H, render() -> Uint8ClampedArray } */
(function (root) {
  const PX = 32, W = 244, H = 384, cx = 122, pivotY = 382;
  const rPad = 76, baseY = pivotY - Math.round(rPad / 2);   // ground-ellipse centre line
  const cyAt = (h) => baseY - h * PX;

  const HEX = {
    out:'#262930',
    whiHi:'#e6dcc8', whiMid:'#d9cdb6', whiSh:'#bdae93', whiDp:'#a89878',
    redHi:'#bf6450', redMid:'#a8503c', redSh:'#863f2f', redDp:'#6e3326',
    stoHi:'#4a4d52', stoMid:'#37404a', stoSh:'#33363c', stoDp:'#283036',
    glass:'#bfe3e6', glassDk:'#283036', brass:'#d8b35a', wood:'#8a5a3b',
  };
  const MAT = {
    WHITE:{hi:'whiHi',mid:'whiMid',sh:'whiSh',dp:'whiDp'},
    RED:  {hi:'redHi',mid:'redMid',sh:'redSh',dp:'redDp'},
    STONE:{hi:'stoHi',mid:'stoMid',sh:'stoSh',dp:'stoDp'},
  };
  const SPECIAL = { out:'out', glass:'glass', glassDk:'glassDk', brass:'brass', wood:'wood' };
  const DARKER = { hi:'mid', mid:'sh', sh:'dp', dp:'dp' };

  const key = new Array(W*H).fill('');
  const mat = new Array(W*H).fill(null);
  const idx = (x,y)=>y*W+x, inb=(x,y)=>x>=0&&x<W&&y>=0&&y<H;
  function put(x,y,m,k){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return; key[idx(x,y)]=k; mat[idx(x,y)]=m; }
  function darken(x,y,steps){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return; const i=idx(x,y);
    let k=key[i]; if(!k||SPECIAL[k])return; for(let s=0;s<steps;s++)k=DARKER[k]; key[i]=k; }

  function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
  const rnd = mulberry32(1937);

  // octagon facet shading, light upper-left: hi | mid | sh | dp, 1px darker seams
  const SEAMS=[-0.58,0.02,0.60];
  function facetKey(t,r){
    let k = t<SEAMS[0]?'hi' : t<SEAMS[1]?'mid' : t<SEAMS[2]?'sh' : 'dp';
    for(const s of SEAMS){ if(Math.abs(t-s)*r<0.8) return DARKER[k]; }
    return k;
  }
  const arc = (t)=>Math.sqrt(Math.max(0,1-t*t));

  // cylindrical band h0..h1 (m), radius r0 bottom → r1 top. Fills the FRONT wall only:
  // per column, from the top-ellipse front arc down to the bottom-ellipse front arc.
  function band(h0,h1,r0,r1,matName,kfn){
    const yTopC=cyAt(h1), yBotC=cyAt(h0);
    const R=Math.max(r0,r1);
    for(let xi=Math.round(cx-R); xi<=Math.round(cx+R); xi++){
      const t1=(xi-cx)/r1, t0=(xi-cx)/r0;
      let ys = Math.abs(t1)<=1 ? yTopC + (r1/2)*arc(t1) : null;
      const ye = Math.abs(t0)<=1 ? yBotC + (r0/2)*arc(t0) : null;
      if(ye===null) continue;
      if(ys===null){ // outside top radius (taper): wall silhouette starts where r(f)=|x-cx|
        const f=(Math.abs(xi-cx)-r0)/((r1-r0)||1); ys = yBotC-(yBotC-yTopC)*Math.min(1,Math.max(0,f));
      }
      for(let y=Math.round(ys); y<=Math.round(ye); y++){
        const f=Math.min(1,Math.max(0,(yBotC-y)/((yBotC-yTopC)||1)));
        const r=r0+(r1-r0)*f, t=Math.max(-1,Math.min(1,(xi-cx)/r));
        put(xi,y,matName, kfn?kfn(t,r,y):facetKey(t,r));
      }
    }
  }
  // top face (full ellipse) at height h
  function ellipseTop(h,r,matName,kfn){
    const cy=cyAt(h), ry=r/2;
    for(let dy=-Math.ceil(ry); dy<=Math.ceil(ry); dy++){
      const hw=r*arc(dy/ry);
      for(let x=Math.round(cx-hw); x<=Math.round(cx+hw); x++)
        put(x,cy+dy,matName, kfn?kfn((x-cx)/r,dy):'hi');
    }
  }
  // octagonal cone roof: eave radius rE at hEave, apex at hApex
  function cone(hEave,hApex,rE,matName){
    const yA=cyAt(hApex), yE=cyAt(hEave);
    for(let xi=Math.round(cx-rE); xi<=Math.round(cx+rE); xi++){
      const s0=Math.abs(xi-cx)/rE;
      const ys = yA + s0*(yE-yA);                 // straight silhouette from apex
      const ye = yE + (rE/2)*arc((xi-cx)/rE);     // eave front arc
      for(let y=Math.round(ys); y<=Math.round(ye); y++){
        const s=Math.max(0.05,(y-yA)/((yE-yA)||1));
        const t=Math.max(-1,Math.min(1,(xi-cx)/(rE*Math.min(1,s))));
        put(xi,y,matName,facetKey(t,rE*s));
      }
    }
    // ridge seams apex → eave
    for(const t0 of SEAMS){
      const ex=cx+rE*t0, ey=yE+(rE/2)*arc(t0);
      line(cx,yA,ex,ey,(x,y)=>darken(x,y,1));
    }
  }
  function line(x0,y0,x1,y1,fn){
    x0=Math.round(x0);y0=Math.round(y0);x1=Math.round(x1);y1=Math.round(y1);
    let dx=Math.abs(x1-x0),dy=Math.abs(y1-y0),sx=x0<x1?1:-1,sy=y0<y1?1:-1,err=dx-dy;
    for(;;){ fn(x0,y0); if(x0===x1&&y0===y1)break; const e2=2*err;
      if(e2>-dy){err-=dy;x0+=sx;} if(e2<dx){err+=dx;y0+=sy;} }
  }
  function quad(pts,matName,k){
    const ys=pts.map(p=>p[1]);
    for(let y=Math.round(Math.min(...ys)); y<=Math.round(Math.max(...ys)); y++){
      const xs=[];
      for(let i=0;i<pts.length;i++){
        const [ax,ay]=pts[i],[bx,by]=pts[(i+1)%pts.length];
        if((ay<=y&&by>y)||(by<=y&&ay>y)) xs.push(ax+(y-ay)*(bx-ax)/(by-ay));
      }
      xs.sort((a,b)=>a-b);
      for(let j=0;j+1<xs.length;j+=2)
        for(let x=Math.round(xs[j]); x<=Math.round(xs[j+1]); x++) put(x,y,matName,k);
    }
  }

  // ---------------- structure heights (m) / radii (px) ----------------
  const T = {
    padTop:0.32, towerTop:6.78, rTower0:60, rTower1:46,
    slabTop:7.08, rSlab:68,
    sillTop:7.24, rSill:38,
    glazeTop:7.90, rGlaze:35,
    redTop:8.72, rRed:37,
    apex:10.10, rEave:42,
    railTop:8.02, rRail:56,
  };
  const rTowerAt=(h)=>T.rTower0+(T.rTower1-T.rTower0)*((h-T.padTop)/(T.towerTop-T.padTop));
  const wallY=(h,t)=>cyAt(h)+(rTowerAt(h)/2)*arc(t);   // screen y of a point on the tower wall

  function render(){
    key.fill(''); mat.fill(null);

    // 1 — stone pad
    band(0,T.padTop,rPad,rPad,'STONE',(t,r)=>{ const k=facetKey(t,r); return k==='hi'?'mid':k; });
    ellipseTop(T.padTop,rPad,'STONE',(t,dy)=> t>0.5&&dy<0 ? 'mid' : 'hi');

    // 2 — tower (white), under-gallery shadow on top rows, speckle, base course in stone
    const shadowBelow = cyAt(T.towerTop);
    band(T.padTop,T.towerTop,T.rTower0,T.rTower1,'WHITE',(t,r,y)=>{
      let k=facetKey(t,r);
      return k;
    });
    // plaster speckle (deterministic)
    for(let n=0;n<130;n++){
      const y=Math.round(cyAt(T.towerTop)+8+rnd()*(cyAt(T.padTop)-cyAt(T.towerTop)-8));
      const h=(baseY-y)/PX, r=rTowerAt(Math.max(T.padTop,Math.min(T.towerTop,h)));
      const x=Math.round(cx+(rnd()*2-1)*(r-3));
      if(inb(x,y)&&mat[idx(x,y)]==='WHITE') darken(x,y,1);
    }
    // string course under the gallery
    for(let xi=Math.round(cx-T.rTower1); xi<=Math.round(cx+T.rTower1); xi++){
      const t=(xi-cx)/rTowerAt(6.5); if(Math.abs(t)>1)continue;
      darken(xi, wallY(6.5,t), 1);
    }
    // stone base band
    band(T.padTop,0.78,T.rTower0,rTowerAt(0.78),'STONE',(t,r)=>{ const k=facetKey(t,r); return k==='hi'?'mid':k; });

    // 3 — door (front facet) + windows
    drawDoor(0.30, 0.42, 2.30, 12);
    drawWindow(-0.06, 4.30); drawWindow(0.34, 2.45); drawWindow(-0.06, 5.85);

    // 4 — back railing (behind lantern)
    for(const a of [115,145,180,215,245]) railPost(a);
    railRail([100,260], T.railTop);

    // 5 — gallery slab (rim in half-shadow, lit top)
    band(T.towerTop,T.slabTop,T.rSlab,T.rSlab,'WHITE',(t,r)=>DARKER[facetKey(t,r)]);
    ellipseTop(T.slabTop,T.rSlab,'WHITE',(t,dy)=> t>0.45&&dy<0?'mid':'hi');
    // under-slab shadow cast on tower
    for(let xi=Math.round(cx-T.rSlab); xi<=Math.round(cx+T.rSlab); xi++){
      const t=(xi-cx)/T.rSlab, ye=Math.round(cyAt(T.towerTop)+(T.rSlab/2)*arc(t));
      for(let y=ye+1;y<=ye+5;y++) if(inb(xi,y)&&mat[idx(xi,y)]==='WHITE') darken(xi,y,y<=ye+3?2:1);
    }

    // 6 — lantern: sill ring, glazing, red fascia band
    band(T.slabTop,T.sillTop,T.rSill,T.rSill,'WHITE');
    ellipseTop(T.sillTop,T.rSill,'WHITE',(t,dy)=> t>0.45&&dy<0?'mid':'hi');
    glazing();
    band(T.glazeTop,T.redTop,T.rRed,T.rRed,'RED');
    // 7 — red cap roof + under-eave shadow + finial
    cone(T.redTop,T.apex,T.rEave,'RED');
    for(let xi=Math.round(cx-T.rEave); xi<=Math.round(cx+T.rEave); xi++){
      const t=(xi-cx)/T.rEave, ye=Math.round(cyAt(T.redTop)+(T.rEave/2)*arc(t));
      for(let y=ye+1;y<=ye+2;y++) if(inb(xi,y)&&mat[idx(xi,y)]==='RED') darken(xi,y,1);
    }
    put(cx-1,cyAt(T.apex)-1,'STONE','out'); put(cx-1,cyAt(T.apex)-2,'STONE','out');
    put(cx,cyAt(T.apex)-1,'STONE','out');   put(cx,cyAt(T.apex)-2,'STONE','out');
    ball(cx-0.5, cyAt(T.apex)-6, 4.4);

    // 8 — front railing
    for(const a of [-75,-45,-15,15,45,75]) railPost(a);
    railRail([-85,85], T.railTop);
    railRail([-85,85], T.railTop-0.42);

    // 9 — lean-to shed, lower-left (per ref)
    shed();

    // 10 — keyline
    outline();
    return toRGBA();
  }

  function drawDoor(t,h0,h1,w){
    const x0=Math.round(cx+t*rTowerAt((h0+h1)/2)-w/2);
    const yTop=Math.round(wallY(h1,t)), yBot=Math.round(wallY(h0,t));
    for(let y=yTop;y<=yBot;y++)for(let x=x0;x<x0+w;x++) put(x,y,'STONE','out');
    for(let y=yTop+2;y<=yBot-1;y++)for(let x=x0+2;x<x0+w-2;x++) put(x,y,'STONE','mid');
    for(let y=yTop+2;y<=yBot-1;y++) put(x0+Math.floor(w/2),y,'STONE','sh'); // plank split
    put(x0+w-4,Math.round((yTop+yBot)/2),'STONE','brass');
    for(let x=x0-1;x<=x0+w;x++) put(x,yTop-1,'WHITE','hi');                 // lintel
  }
  function drawWindow(t,hc){
    const r=rTowerAt(hc), x0=Math.round(cx+t*r-4), yc=Math.round(wallY(hc,t));
    for(let y=yc-5;y<=yc+5;y++)for(let x=x0;x<x0+8;x++) put(x,y,'STONE','out');
    for(let y=yc-4;y<=yc+4;y++)for(let x=x0+1;x<x0+7;x++) put(x,y,'WHITE','glass');
    for(let x=x0+1;x<x0+7;x++) put(x,yc-4,'WHITE','glassDk');
    for(let y=yc-4;y<=yc+4;y++) put(x0+4,y,'WHITE','out');                  // mullion
    for(let x=x0-1;x<x0+9;x++) put(x,yc+6,'WHITE','hi');                    // sill
  }
  function railPost(deg){
    const th=deg*Math.PI/180, x=cx+T.rRail*Math.sin(th);
    const yb=cyAt(T.slabTop)+(T.rRail*Math.cos(th))/2, yt=cyAt(T.railTop)+(T.rRail*Math.cos(th))/2;
    for(let y=Math.round(yt);y<=Math.round(yb);y++) put(x,y,'STONE','sh');
  }
  function railRail(range,h){
    for(let d=range[0];d<=range[1];d+=0.5){
      const th=d*Math.PI/180;
      put(cx+T.rRail*Math.sin(th), cyAt(h)+(T.rRail*Math.cos(th))/2, 'STONE','sh');
    }
  }
  function glazing(){
    band(T.sillTop,T.glazeTop,T.rGlaze,T.rGlaze,'WHITE',(t)=>{
      // white corner posts at facet seams + frame edges; dark glass between; glint near left
      for(const s of [-0.80,-0.28,0.28,0.80]) if(Math.abs(t-s)<0.09) return 'mid';
      if(Math.abs(t)>0.93) return 'mid';
      return t<-0.25?'glassDk':'glassDk';
    });
    // glass glints
    const yT=cyAt(T.glazeTop), yB=cyAt(T.sillTop);
    for(let y=Math.round(yT+3);y<=Math.round(yB-2);y++){
      const x=Math.round(cx-T.rGlaze*0.55 + (y-yT)*0.3);
      if(mat[idx(x,y)]==='WHITE'&&key[idx(x,y)]==='glassDk') put(x,y,'WHITE','glass');
    }
    // top rail of glazing = white cap row
    for(let xi=Math.round(cx-T.rGlaze); xi<=Math.round(cx+T.rGlaze); xi++){
      const t=(xi-cx)/T.rGlaze;
      put(xi, cyAt(T.glazeTop)+(T.rGlaze/2)*arc(t), 'WHITE','mid');
    }
  }
  function ball(x,y,r){
    for(let dy=-r;dy<=r;dy++)for(let dx=-r;dx<=r;dx++)
      if(dx*dx+dy*dy<=r*r) put(x+dx,y+dy,'RED', dx<-r*0.2&&dy<-r*0.2?'hi':(dx>r*0.3||dy>r*0.3?'sh':'mid'));
  }
  function shed(){
    const C=[cx-54,372], L=[cx-106,346], Ltop=[cx-106,302], Ctop=[cx-54,320];
    const R=[cx-12,351], Rtop=[cx-12,293];
    const P1=Ctop, P2=Ltop, P3=[cx-64,275], P4=[cx-12,293];
    quad([C,L,Ltop,Ctop],'WHITE','mid');                       // lit left face
    quad([C,R,Rtop,Ctop],'WHITE','sh');                        // shaded right face
    // faces meet: corner edge
    line(C[0],C[1],Ctop[0],Ctop[1],(x,y)=>put(x,y,'WHITE','dp'));
    quad([P1,P2,P3,P4],'WHITE','sh');                          // plank roof
    for(let i=1;i<4;i++){                                      // plank seams along slope
      const f=i/4;
      line(P1[0]+(P4[0]-P1[0])*f, P1[1]+(P4[1]-P1[1])*f,
           P2[0]+(P3[0]-P2[0])*f, P2[1]+(P3[1]-P2[1])*f, (x,y)=>darken(x,y,1));
    }
    // roof front-edge highlight + eave shadow on walls
    line(P1[0],P1[1],P2[0],P2[1],(x,y)=>put(x,y,'WHITE','hi'));
    line(P1[0],P1[1]+1,P2[0],P2[1]+1,(x,y)=>darken(x,y,1));
    line(P1[0],P1[1],P4[0],P4[1],(x,y)=>put(x,y,'WHITE','mid'));
    // door on right face + window on left face
    for(let y=338;y<=360;y++)for(let x=cx-44;x<=cx-35;x++) put(x,y,'STONE','out');
    for(let y=340;y<=359;y++)for(let x=cx-43;x<=cx-36;x++) put(x,y,'STONE','mid');
    for(let y=340;y<=359;y++) put(cx-40,y,'STONE','sh');
    put(cx-37,350,'STONE','brass');
    for(let y=326;y<=334;y++)for(let x=cx-88;x<=cx-80;x++) put(x,y,'STONE','out');
    for(let y=327;y<=333;y++)for(let x=cx-87;x<=cx-81;x++) put(x,y,'WHITE','glass');
    for(let x=cx-89;x<=cx-79;x++) put(x,335,'WHITE','hi');
    // explicit keylines where shed meets the tower
    line(P4[0],P4[1],P1[0],P1[1],(x,y)=>put(x,y,'STONE','out'));
    line(P4[0],P4[1],P3[0],P3[1],(x,y)=>put(x,y,'STONE','out'));
    line(R[0],R[1],Rtop[0],Rtop[1],(x,y)=>put(x,y,'STONE','out'));
  }
  function outline(){
    const add=[];
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){
      if(key[idx(x,y)]) continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]])
        if(inb(x+dx,y+dy)&&key[idx(x+dx,y+dy)]&&key[idx(x+dx,y+dy)]!=='out'){ add.push([x,y]); break; }
    }
    for(const [x,y] of add){ key[idx(x,y)]='out'; mat[idx(x,y)]='STONE'; }
  }
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function toRGBA(){
    const out=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){
      const k=key[i]; if(!k){ out[i*4+3]=0; continue; }
      const hx = SPECIAL[k] ? HEX[k] : HEX[MAT[mat[i]][k]];
      const [r,g,b]=hex2rgb(hx||HEX.out);
      out[i*4]=r; out[i*4+1]=g; out[i*4+2]=b; out[i*4+3]=255;
    }
    return out;
  }

  root.LighthouseIso = { W, H, PX, pivot:{x:cx,y:H}, render };
})(typeof globalThis!=='undefined'?globalThis:window);
