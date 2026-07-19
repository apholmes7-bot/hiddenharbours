/* Hidden Harbours — scene compositor for test dioramas.
   Lays a near-plan 32px ground grid from ShoreKit / WharfKit / RoadKit, autotiling water
   edges into faces, then drops baked building/boat sprites by their pivot. NOT a shipped rig —
   a staging helper eval'd inside run_script (rigs must already be eval'd in scope).

   composeScene(cfg, sprites) -> { canvas, bbox }
     cfg = {
       Wt, Ht, seed,
       ground: 2D array [ty][tx] of 'water'|'grass'|'beach'|'quay'|'tallpier'|'lowpier'|'float'|'ledge',
       road:   2D array [ty][tx] of {surface,wear,profile}|null  (drawn instead of the ground cell),
       seaEdges: {n,e,s,w}  — map borders that are open sea (offmap neighbour reads as water),
       depthBase, depthK,
       placements: [ {key,tx,ty, alpha, flip} ]  — tx,ty fractional tile of the sprite pivot,
       bg
     }
     sprites = { key: {img, px, py, w, h} }   (pre-loaded)
*/
(function(root){
  const SK=root.ShoreKit, WK=root.WharfKit, RK=root.RoadKit;
  const T=32;

  function blit(ctx, r, dx, dy){ const t=createCanvas(r.w,r.h);
    t.getContext('2d').putImageData(new ImageData(r.data,r.w,r.h),0,0); ctx.drawImage(t,dx,dy); }

  const isWater=(m)=> m==='water'||m==null;

  function openSides(g, x, y, sea){
    const W=g[0].length, H=g.length, here=g[y][x];
    const nb=(nx,ny,edge)=>{ if(nx<0||ny<0||nx>=W||ny>=H) return sea[edge]?'water':here; return g[ny][nx]; };
    return {
      n:isWater(nb(x,y-1,'n')) && !isWater(here),
      s:isWater(nb(x,y+1,'s')) && !isWater(here),
      e:isWater(nb(x+1,y,'e')) && !isWater(here),
      w:isWater(nb(x-1,y,'w')) && !isWater(here),
    };
  }
  // outer 45° corner cut when two adjacent sides are open water (rounds a jetty tip)
  function cornerCut(open){
    if(open.s&&open.e) return 'se'; if(open.s&&open.w) return 'sw';
    if(open.n&&open.e) return 'ne'; if(open.n&&open.w) return 'nw'; return null;
  }

  function depthField(g, sea, base, k){
    const W=g[0].length, H=g.length, INF=1e9;
    const d=Array.from({length:H},()=>new Array(W).fill(INF));
    const q=[];
    for(let y=0;y<H;y++)for(let x=0;x<W;x++) if(!isWater(g[y][x])){ d[y][x]=0; q.push([x,y]); }
    // treat non-sea offmap as land seed (shore) so enclosed water stays shallow
    for(let i=0;i<q.length;i++){ const [x,y]=q[i];
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx<0||ny<0||nx>=W||ny>=H) continue;
        if(isWater(g[ny][nx]) && d[ny][nx]>d[y][x]+1){ d[ny][nx]=d[y][x]+1; q.push([nx,ny]); } } }
    return (x,y)=>{ let dist=d[y][x]; if(dist>=INF) dist=8;
      // deepen toward sea borders
      const W2=W,H2=H; let sb=99;
      if(sea.s) sb=Math.min(sb,H2-1-y); if(sea.n) sb=Math.min(sb,y);
      if(sea.e) sb=Math.min(sb,W2-1-x); if(sea.w) sb=Math.min(sb,x);
      const seaDepth = sb<99 ? (6-sb) : -99;
      return Math.max(0,Math.min(1, base + dist*k + Math.max(0,seaDepth)*0.06 )); };
  }

  function roadCon(road, x, y){
    const W=road[0].length, H=road.length, fam=(nx,ny)=> (nx>=0&&ny>=0&&nx<W&&ny<H&&road[ny][nx])?1:0;
    const con={ n:!!fam(x,y-1), e:!!fam(x+1,y), s:!!fam(x,y+1), w:!!fam(x-1,y) };
    const diag={ ne:!!fam(x+1,y-1), nw:!!fam(x-1,y-1), se:!!fam(x+1,y+1), sw:!!fam(x-1,y+1) };
    return {con,diag};
  }
  function roleOf(c){ const n=(c.n?1:0)+(c.e?1:0)+(c.s?1:0)+(c.w?1:0);
    if(n===0)return'isolated'; if(n===1)return'cap'; if(n===2)return((c.n&&c.s)||(c.e&&c.w))?'straight':'bend'; if(n===3)return'tee'; return'cross'; }
  function axisOf(c){ return (c.n||c.s)?((c.e||c.w)?'x':'v'):'h'; }

  function renderGroundCell(mat, open, cut, seed, gx, gy, depthFn){
    if(mat==='water') return SK.render('shallows',{depth:depthFn(gx,gy),seed,frame:0});
    if(mat==='grass'||mat==='beach'||mat==='ledge') return SK.render(mat,{open,cut,seed});
    if(mat==='quay'||mat==='tallpier'||mat==='lowpier'||mat==='float') return WK.render(mat,{open,cut});
    return SK.render('shallows',{depth:0.5,seed});
  }

  function composeScene(cfg, sprites){
    const {Wt,Ht,seed,ground,road,placements}=cfg;
    const sea=Object.assign({n:false,e:false,s:false,w:false}, cfg.seaEdges||{});
    const depthFn=depthField(ground, sea, cfg.depthBase!=null?cfg.depthBase:0.18, cfg.depthK!=null?cfg.depthK:0.1);
    const road2 = road || Array.from({length:Ht},()=>new Array(Wt).fill(null));

    const mTop=cfg.marginTop!=null?cfg.marginTop:260, mBot=64, mX=24;
    const CW=Wt*T+mX*2, CH=Ht*T+mTop+mBot;
    const cv=createCanvas(CW,CH); const ctx=cv.getContext('2d'); ctx.imageSmoothingEnabled=false;
    const OX=mX, OY=mTop;

    // ---- GROUND: bottom-to-top so hanging south faces overlay the row below ----
    for(let ty=Ht-1; ty>=0; ty--){
      for(let tx=0; tx<Wt; tx++){
        const rd=road2[ty][tx];
        if(rd){ const {con,diag}=roadCon(road2,tx,ty); const role=roleOf(con), ax=axisOf(con);
          let mk=[];
          if(rd.profile==='road2'){ if(role==='straight'){ mk = (ax==='x')?[]:['edge', rd.center==='double'?'centerDouble':'centerDash']; } }
          else if(rd.profile==='lane'){ if(role==='straight'&&ax!=='x') mk=['edge']; }
          else if(rd.profile==='sidewalk'){ mk=['curb']; }
          const r=RK.render(rd.surface,{con,diag,axis:ax,wear:rd.wear||'new',ground:'grass',markings:mk,gx:tx,gy:ty,seed:seed+3});
          blit(ctx, r, OX+tx*T, OY+ty*T);
          continue;
        }
        const mat=ground[ty][tx];
        const open=openSides(ground,tx,ty,sea);
        const cut=(mat!=='water')?cornerCut(open):null;
        const r=renderGroundCell(mat, open, cut, seed, tx, ty, depthFn);
        blit(ctx, r, OX+tx*T, OY+ty*T);
      }
    }

    // ---- SPRITES: sort north→south (ascending ground screen-y) ----
    const items=placements.map(p=>{ const s=sprites[p.key]; if(!s) return null;
      const gx=OX+p.tx*T, gy=OY+p.ty*T; return {p,s,gx,gy}; }).filter(Boolean);
    items.sort((a,b)=> a.gy-b.gy);
    for(const it of items){
      const {p,s,gx,gy}=it; ctx.save(); if(p.alpha!=null) ctx.globalAlpha=p.alpha;
      if(p.flip){ ctx.translate(gx,gy); ctx.scale(-1,1); ctx.drawImage(s.img, -s.px, -s.py); }
      else ctx.drawImage(s.img, Math.round(gx-s.px), Math.round(gy-s.py));
      ctx.restore();
    }

    // ---- autocrop to content, then lay on background ----
    const id=ctx.getImageData(0,0,CW,CH).data;
    let a=CW,b=CH,c=0,d=0,any=false;
    for(let y=0;y<CH;y++)for(let x=0;x<CW;x++){ if(id[(y*CW+x)*4+3]>4){any=true; if(x<a)a=x;if(x>c)c=x;if(y<b)b=y;if(y>d)d=y;} }
    const pad=10; a=Math.max(0,a-pad); b=Math.max(0,b-pad); c=Math.min(CW-1,c+pad); d=Math.min(CH-1,d+pad);
    const cw=c-a+1, chh=d-b+1;
    const out=createCanvas(cw,chh); const octx=out.getContext('2d'); octx.imageSmoothingEnabled=false;
    if(cfg.bg){ octx.fillStyle=cfg.bg; octx.fillRect(0,0,cw,chh); }
    octx.drawImage(cv, a,b,cw,chh, 0,0,cw,chh);
    return { canvas:out, ox:OX-a, oy:OY-b };
  }

  root.SceneKit={ composeScene, T };
})(typeof globalThis!=='undefined'?globalThis:window);
