// Optional browser acceptance runner; requires Playwright and installed Edge.
const {chromium}=require('playwright'),fs=require('fs'),path=require('path'),assert=require('assert');
(async()=>{
 const browser=await chromium.launch({headless:true,channel:'msedge'}),page=await browser.newPage({viewport:{width:1100,height:1050}}),errors=[];
 page.on('pageerror',e=>errors.push(e.message));
 await page.goto('file:///'+path.join(__dirname,'Hidden-Harbours-Cast-Viewer.html').replaceAll('\\','/'));
 await page.waitForSelector('[data-ready=true]');
 const diagnostics=()=>page.locator('#hh-cast-viewer').evaluate(el=>el.castDiagnostics());
 const pixels=()=>page.locator('[data-cast=fisher]').evaluate(el=>el.toDataURL());
 const initial=await pixels();await page.screenshot({path:path.join(__dirname,'cast-desktop.png'),fullPage:true});
 await page.locator('[data-play]').click();await page.waitForTimeout(900);const moving=await pixels();const performance=await diagnostics();await page.locator('[data-play]').click();
 assert.notStrictEqual(initial,moving,'Playback must change the pose');
 const beforeRotation=await pixels();await page.locator('[data-angle]').fill('180');await page.locator('[data-angle]').dispatchEvent('input');const back=await pixels();assert.notStrictEqual(beforeRotation,back);
 const anims=await page.locator('[data-clip] option').evaluateAll(els=>els.map(e=>e.value));
 const clipping=[],empty=[];
 for(const anim of anims){
   await page.locator('[data-clip]').selectOption(anim);
   for(const value of ['0','500','1000']){
     await page.locator('[data-frame]').fill(value);await page.locator('[data-frame]').dispatchEvent('input');
     const scan=await page.locator('[data-gallery] canvas').evaluateAll(els=>els.map(c=>{const {width:w,height:h}=c,a=c.getContext('2d').getImageData(0,0,w,h).data;let count=0,edge=0;for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(a[(y*w+x)*4+3]){count++;if(x===0||y===0||x===w-1||y===h-1)edge++;}return {key:c.dataset.cast,count,edge};}));
     for(const s of scan){if(s.edge)clipping.push({anim,value,...s});if(!s.count)empty.push({anim,value,...s});}
   }
 }
 await page.locator('[data-clip]').selectOption('idle');await page.locator('[data-angle]').fill('35');await page.locator('[data-angle]').dispatchEvent('input');
 await page.locator('[data-inspect=ginny]').click();assert.strictEqual((await diagnostics()).state.selected,'ginny');
 const faceImage=()=>page.locator('[data-head]').evaluate(el=>el.toDataURL());
 await page.locator('[data-angle]').fill('0');await page.locator('[data-angle]').dispatchEvent('input');const faceNeutral=await faceImage();
 await page.locator('[data-expression]').selectOption('grin');const faceGrin=await faceImage();assert.notStrictEqual(faceNeutral,faceGrin);
 await page.locator('[data-talk]').check();await page.locator('[data-play]').click();const speech=[];for(let i=0;i<9;i++){await page.waitForTimeout(100);speech.push(await faceImage());}await page.locator('[data-play]').click();assert(new Set(speech).size>1);
 await page.locator('[data-talk]').uncheck();await page.locator('[data-expression]').selectOption('auto');
 await page.locator('[data-character]').selectOption('fisher');await page.locator('[data-clip]').selectOption('walk');await page.locator('[data-angle]').fill('35');await page.locator('[data-angle]').dispatchEvent('input');
 await page.screenshot({path:path.join(__dirname,'cast-inspector.png'),fullPage:true});
 await page.locator('[data-view]').selectOption('cast');await page.locator('[data-clip]').selectOption('swim');await page.screenshot({path:path.join(__dirname,'cast-swimming.png'),fullPage:true});
 await page.locator('[data-clip]').selectOption('walk');await page.locator('[data-spin]').click();await page.waitForTimeout(350);assert((await diagnostics()).state.angle!==35);await page.locator('[data-spin]').click();
 await page.locator('[data-clip]').selectOption('reach');await page.locator('[data-speed]').selectOption('2');await page.locator('[data-play]').click();await page.waitForTimeout(700);const oneShot=await diagnostics();assert.strictEqual(oneShot.state.playing,false);assert.strictEqual(oneShot.state.u,1);
 await page.locator('[data-clip]').selectOption('idle');await page.locator('[data-angle]').fill('0');await page.locator('[data-angle]').dispatchEvent('input');await page.locator('[data-density]').selectOption('32');
 await page.setViewportSize({width:360,height:800});await page.screenshot({path:path.join(__dirname,'cast-mobile.png'),fullPage:true});
 const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth);
 const report={errors,characters:10,animations:anims.length,renderChecks:anims.length*3*10,clipping,empty,playbackChanges:true,rotationChanges:true,expressionChanges:true,speechFrames:new Set(speech).size,oneShotStops:true,mobileOverflow:overflow,fullCastRenderMs:performance.lastRenderMs};
 fs.writeFileSync(path.join(__dirname,'browser-validation.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));await browser.close();assert(!overflow);assert(!errors.length);assert(!clipping.length);assert(!empty.length);
})().catch(e=>{console.error(e);process.exit(1)});
