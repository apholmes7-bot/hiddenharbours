/* Hidden Harbours — SCENE COMPOSITOR (test-scene helper, not a rig).
   Lays a near-plan 32px tile floor (RoadKit / ShoreKit / WharfKit) and blits rig-baked
   building / boat / character sprites at correct pivot registration. Runs in the run_script
   sandbox (createCanvas) OR the browser. Attaches globalThis.SceneKit.

   All tiles are 32px near-plan squares, camera from the SOUTH; sprite rigs are 32px/m axonometric
   at dir facings with pivot = ground contact — so a sprite blits 1:1 with its pivot aligned to a
   tile's ground point. Painter order: sea bg -> water tiles -> land tiles north->south -> props by depth.

   SceneKit.scene({cols,rows,tile=32, legend, ground:[rowStrings], rigs:{road,shore,wharf}, seed, sky})
     -> { cv, ctx, tile, cols, rows, groundPt(tx,ty), place(props) }
   legend entry (per char):
     { water:true }
     | { shore:'grass'|'beach'|'tidalDry'|'tidalWet'|'ledge', fringe?:true }
     | { wharf:'quay'|'float'|'lowpier'|'tallpier' }
     | { road:'asphalt'|'concrete'|..., ground:'grass'|'dirt'|'sand', profile?, wear?, markings? }
   props: [{ img, meta, tx, ty, nx?, ny?, line?:{toX,toY} , flip? }]  (see place())
*/
(function(root){
  function hexA(){}
  function classify(sp){
    if(!sp) return 'empty';
    if(sp.water) return 'water';
    if(sp.road) return 'road';
    if(sp.wharf) return 'wharf';
    if(sp.shore) return 'shore';
    return 'empty';
  }
  function blit(ctx, r, dx, dy){          // r = {data,w,h} rig output
    const tmp=root.__mk(r.w,r.h); tmp.getContext('2d').putImageData(new ImageData(r.data,r.w,r.h),0,0);
    ctx.imageSmoothingEnabled=false; ctx.drawImage(tmp, dx, dy);
  }

  function scene(opts){
    const tile=opts.tile||32, cols=opts.cols, rows=opts.rows, seed=opts.seed||1;
    const legend=opts.legend||{}, grid=opts.ground||[];
    const R=opts.rigs||{};
    const W=cols*tile, H=rows*tile + 24;   // +face overhang room at the bottom
    const cv=root.__mk(W,H), ctx=cv.getContext('2d');
    ctx.imageSmoothingEnabled=false;

    // background sky/sea wash
    if(opts.sky!==null){
      const g=ctx.createLinearGradient(0,0,0,H);
      const sky=opts.sky||['#243a44','#33565c'];
      g.addColorStop(0,sky[0]); g.addColorStop(1,sky[1]);
      ctx.fillStyle=g; ctx.fillRect(0,0,W,H);
    }

    const at=(x,y)=> (x>=0&&x<cols&&y>=0&&y<rows) ? legend[grid[y] && grid[y][x]] : null;
    const kind=(x,y)=> classify(at(x,y));
    const isWater=(x,y)=>{ if(x<0||x>=cols||y<0||y>=rows) return false;  // map border = solid, not coast
      const k=kind(x,y); return k==='water'||k==='empty'; };
    const sameRoad=(x,y)=> kind(x,y)==='road';

    // ---- water depth via BFS distance to nearest non-water ----
    const dist=new Int16Array(cols*rows).fill(-1); const q=[];
    for(let y=0;y<rows;y++)for(let x=0;x<cols;x++){ const k=kind(x,y);
      if(k!=='water'&&k!=='empty'){ dist[y*cols+x]=0; q.push([x,y]); } }
    for(let h=0;h<q.length;h++){ const [x,y]=q[h]; const d=dist[y*cols+x];
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx<0||nx>=cols||ny<0||ny>=rows)continue; const i=ny*cols+nx;
        if(dist[i]<0){ dist[i]=d+1; q.push([nx,ny]); } } }
    const depthAt=(x,y)=>{ const d=dist[y*cols+x]; const dd=d<0?9:d; return Math.max(0.05, Math.min(1, dd/11)); };

    // ---- water pass ----
    if(R.shore) for(let y=0;y<rows;y++)for(let x=0;x<cols;x++){
      if(kind(x,y)!=='water')continue;
      blit(ctx, R.shore.render('shallows',{depth:depthAt(x,y), seed:seed+ x*7+y*13, frame:0}), x*tile, y*tile);
    }

    // ---- land pass, north -> south so south faces overlay the tile below ----
    for(let y=0;y<rows;y++)for(let x=0;x<cols;x++){
      const sp=at(x,y), k=kind(x,y); if(k==='water'||k==='empty')continue;
      const openN=isWater(x,y-1), openS=isWater(x,y+1), openW=isWater(x-1,y), openE=isWater(x+1,y);
      if(k==='road' && R.road){
        const con={n:sameRoad(x,y-1),e:sameRoad(x+1,y),s:sameRoad(x,y+1),w:sameRoad(x-1,y)};
        const diag={ne:sameRoad(x+1,y-1),nw:sameRoad(x-1,y-1),se:sameRoad(x+1,y+1),sw:sameRoad(x-1,y+1)};
        const axis=(con.n||con.s)?((con.e||con.w)?'x':'v'):'h';
        blit(ctx, R.road.render(sp.road,{con,diag,axis,ground:sp.ground||'grass',wear:sp.wear||'new',
          markings:sp.markings||[], gx:x, gy:y, seed}), x*tile, y*tile);
      } else if(k==='wharf' && R.wharf){
        let cut=null;
        if(openS&&openE)cut='se'; else if(openS&&openW)cut='sw'; else if(openN&&openE)cut='ne'; else if(openN&&openW)cut='nw';
        blit(ctx, R.wharf.render(sp.wharf,{open:{n:openN,e:openE,s:openS,w:openW}, cut}), x*tile, y*tile);
      } else if(k==='shore' && R.shore){
        // organic rounding from open flags; ragged fringe onto a lower/other neighbour to the south
        let fringe='';
        if(sp.fringe){ if(kind(x,y+1)==='shore'||kind(x,y+1)==='road') fringe+='s'; }
        blit(ctx, R.shore.render(sp.shore,{open:{n:openN,e:openE,s:openS,w:openW}, seed:seed+x*5+y*11, fringe}), x*tile, y*tile);
      }
    }

    function groundPt(tx,ty){ return { x: Math.round(tx*tile + tile/2), y: Math.round(ty*tile + tile/2) }; }

    // ---- props: pivot-aligned, depth-sorted (screen y asc = back to front) ----
    function place(props){
      const list=props.map(p=>{
        const g=groundPt(p.tx, p.ty);
        const sx=g.x + (p.nx||0), sy=g.y + (p.ny||0);
        return Object.assign({}, p, {sx, sy, depth: sy});
      }).sort((a,b)=> a.depth-b.depth);
      for(const p of list){
        const m=p.meta, img=p.img;
        const dx=Math.round(p.sx - m.px), dy=Math.round(p.sy - m.py);
        // optional fishing line + bobber from a stored tip
        if(p.line && m.tipX!=null){
          const tx=dx+m.tipX, ty=dy+m.tipY;
          ctx.strokeStyle='rgba(207,212,204,0.85)'; ctx.lineWidth=1; ctx.beginPath();
          ctx.moveTo(tx+0.5, ty+0.5); ctx.lineTo(p.line.toX+0.5, p.line.toY+0.5); ctx.stroke();
          const bx=p.line.toX, by=p.line.toY;
          ctx.fillStyle='#cf3626'; ctx.fillRect(bx-1,by-1,2,2);
          ctx.fillStyle='#eef0ea'; ctx.fillRect(bx-1,by-1,1,1);
          // little ripple ring
          ctx.strokeStyle='rgba(234,243,238,0.5)'; ctx.beginPath(); ctx.ellipse(bx,by+1,3,1.4,0,0,Math.PI*2); ctx.stroke();
        }
        ctx.imageSmoothingEnabled=false;
        if(p.flip){ ctx.save(); ctx.translate(dx+img.width,dy); ctx.scale(-1,1); ctx.drawImage(img,0,0); ctx.restore(); }
        else ctx.drawImage(img, dx, dy);
      }
    }

    return { cv, ctx, tile, cols, rows, W, H, groundPt, place, depthAt };
  }

  // canvas factory shim (run_script provides createCanvas; browser uses document)
  root.__mk = (typeof createCanvas!=='undefined')
    ? (w,h)=>createCanvas(w,h)
    : (w,h)=>{ const c=document.createElement('canvas'); c.width=w; c.height=h; return c; };

  root.SceneKit = { scene, blit };
})(typeof globalThis!=='undefined'?globalThis:window);
