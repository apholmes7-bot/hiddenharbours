// Keep vertical scrolling and browser pinch zoom available over the character canvas.
(function(root){
  function bindRotation(canvas,{getAngle,onRotate,onTap}){
    let pointer=null;
    function clear(){
      const old=pointer;pointer=null;
      if(old&&canvas.hasPointerCapture?.(old.id))canvas.releasePointerCapture(old.id);
      return old;
    }
    canvas.addEventListener('pointerdown',e=>{
      if(e.isPrimary===false){clear();return;}
      if(e.button!==0||pointer)return;
      pointer={id:e.pointerId,x:e.clientX,y:e.clientY,angle:getAngle(),mode:'pending'};
    });
    canvas.addEventListener('pointermove',e=>{
      if(!pointer||e.pointerId!==pointer.id)return;
      const dx=e.clientX-pointer.x,dy=e.clientY-pointer.y;
      if(pointer.mode==='pending'){
        if(Math.hypot(dx,dy)<8)return;
        pointer.mode=Math.abs(dx)>Math.abs(dy)*1.2?'rotate':'scroll';
        if(pointer.mode==='rotate')canvas.setPointerCapture?.(e.pointerId);
      }
      if(pointer.mode==='rotate')onRotate(((pointer.angle+dx*1.2)%360+360)%360);
    });
    canvas.addEventListener('pointerup',e=>{
      if(!pointer||e.pointerId!==pointer.id)return;
      const p=clear();
      if(p.mode==='pending'&&Math.hypot(e.clientX-p.x,e.clientY-p.y)<8)onTap?.();
    });
    canvas.addEventListener('pointercancel',e=>{if(pointer?.id===e.pointerId)clear();});
    canvas.addEventListener('pointerleave',e=>{if(pointer?.id===e.pointerId&&pointer.mode!=='rotate')clear();});
    canvas.addEventListener('lostpointercapture',e=>{if(pointer?.id===e.pointerId)pointer=null;});
  }
  root.CharacterViewerGestures={bindRotation};
  if(typeof module!=='undefined')module.exports=root.CharacterViewerGestures;
})(globalThis);
