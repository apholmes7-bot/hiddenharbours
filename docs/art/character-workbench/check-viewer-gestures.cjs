// Pointer event regression checks; this is not a browser or physical iPhone test.
const assert=require('node:assert/strict'),{bindRotation}=require('./viewer-gestures.js');
function fixture(){
  const handlers={},captures=new Set(),turns=[];let taps=0;
  const canvas={addEventListener:(name,fn)=>handlers[name]=fn,setPointerCapture:id=>captures.add(id),hasPointerCapture:id=>captures.has(id),releasePointerCapture:id=>captures.delete(id)};
  bindRotation(canvas,{getAngle:()=>25,onRotate:angle=>turns.push(angle),onTap:()=>taps++});
  return {turns,captures,get taps(){return taps;},event(name,extra={}){handlers[name]({pointerId:1,button:0,isPrimary:true,clientX:100,clientY:100,...extra});}};
}
let t=fixture();t.event('pointerdown');t.event('pointerup');assert.equal(t.taps,1);assert.equal(t.turns.length,0);
t=fixture();t.event('pointerdown');t.event('pointermove',{clientX:104,clientY:140});t.event('pointermove',{clientX:160,clientY:180});t.event('pointerup',{clientX:160,clientY:180});assert.equal(t.turns.length,0);assert.equal(t.taps,0);assert.equal(t.captures.size,0);
t=fixture();t.event('pointerdown');t.event('pointermove',{clientX:140,clientY:103});assert.deepEqual(t.turns,[73]);assert(t.captures.has(1));t.event('pointerup',{clientX:140});assert.equal(t.taps,0);assert.equal(t.captures.size,0);
t=fixture();t.event('pointerdown');t.event('pointermove',{clientX:60});t.event('pointercancel');t.event('pointerup');assert.equal(t.taps,0);assert.equal(t.captures.size,0);t.event('pointerdown');t.event('pointerup');assert.equal(t.taps,1);
t=fixture();t.event('pointerdown');t.event('pointerdown',{pointerId:2,isPrimary:false});t.event('pointermove',{clientX:180});t.event('pointerup');assert.equal(t.taps,0);assert.equal(t.turns.length,0);
t=fixture();t.event('pointerdown');t.event('pointermove',{pointerId:2,clientX:180});t.event('pointerup',{pointerId:2});assert.equal(t.turns.length,0);t.event('pointerup');assert.equal(t.taps,1);
t=fixture();t.event('pointerdown');t.event('pointerleave');t.event('pointerdown');t.event('pointerup');assert.equal(t.taps,1);
t=fixture();t.event('pointerdown',{button:2});t.event('pointermove',{clientX:140});t.event('pointerup');assert.equal(t.turns.length,0);assert.equal(t.taps,0);
t=fixture();t.event('pointerdown');t.event('pointerup',{clientY:170});assert.equal(t.taps,0);
console.log('9 gesture checks passed: tap, scroll, rotate, cancel, pinch, pointer ownership, leave, mouse button, missed move.');
