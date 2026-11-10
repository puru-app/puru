const runtimeModules=require('node:path').join(require('node:os').homedir(),'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules');
let chromium;try{({chromium}=require('playwright'));}catch{({chromium}=require(require('node:path').join(runtimeModules,'playwright')));}
const fs=require('node:fs'),http=require('node:http'),path=require('node:path'),assert=require('node:assert/strict');
const {execFileSync}=require('node:child_process');
const root=path.resolve(__dirname,'..');
const baseline=execFileSync('git',['show','a7d1c4e:motion.js'],{cwd:root,encoding:'utf8'});
const mime={'.html':'text/html','.js':'text/javascript','.css':'text/css','.mp3':'audio/mpeg','.svg':'image/svg+xml','.webp':'image/webp','.png':'image/png','.woff2':'font/woff2'};
const server=http.createServer((req,res)=>{
 const url=new URL(req.url,'http://localhost');
 if(url.pathname==='/baseline-motion.js'){res.setHeader('Content-Type','text/javascript');return res.end(baseline);}
 const target=path.resolve(root,'.'+decodeURIComponent(url.pathname==='/'?'/index.html':url.pathname));
 if(!target.startsWith(root+path.sep)){res.writeHead(403);return res.end();}
 try{let data=fs.readFileSync(target);if(url.searchParams.has('baseline'))data=String(data).replace(/motion.js\?[^"<]+/,'baseline-motion.js');res.setHeader('Content-Type',mime[path.extname(target)]||'application/octet-stream');res.end(data);}catch{res.writeHead(404);res.end();}
});
(async()=>{
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 const base='http://127.0.0.1:'+server.address().port;
 const browser=await chromium.launch({channel:'chrome',headless:true});
 try{
  async function open(query=''){
   const context=await browser.newContext({viewport:{width:1200,height:800}});
   await context.addInitScript(()=>{
    window.probes={audioStarts:0,fetches:{},errors:[]};
    const original=window.fetch;window.fetch=(u,...args)=>{probes.fetches[String(u)]=(probes.fetches[String(u)]||0)+1;return original(u,...args);};
    const start=AudioBufferSourceNode.prototype.start;
    AudioBufferSourceNode.prototype.start=function(...args){probes.audioStarts++;return start.apply(this,args);};
   });
   const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
   await page.goto(base+'/'+query);const cdp=await context.newCDPSession(page);await cdp.send('Performance.enable');
   const metric=async()=>Object.fromEntries((await cdp.send('Performance.getMetrics')).metrics.map(m=>[m.name,m.value]));
   const before=await metric();await page.locator('#enter-puru').click();await page.waitForSelector('.story-layer');
   await page.waitForTimeout(100);const after=await metric();
   return {context,page,errors,layouts:after.LayoutCount-before.LayoutCount};
  }
  const old=await open('?baseline');console.log('Baseline opening layouts:',old.layouts);await old.context.close();
  const current=await open();const {page}=current;console.log('Optimized opening layouts:',current.layouts);
  const starts=await page.evaluate(()=>probes.audioStarts);assert.ok(starts>0);
  for(const [width,height] of [[900,800],[900,500],[390,700],[1200,800]]){
   await page.setViewportSize({width,height});await page.waitForTimeout(120);
   assert.equal(await page.locator('html').evaluate(e=>e.classList.contains('story-run')),true,'resize must keep the story alive');
   assert.equal(await page.locator('.story-layer .sc').count(),19,'all nineteen objects survive');
   assert.equal(await page.evaluate(()=>probes.audioStarts),starts,'audio must not replay');
   const times=await page.evaluate(()=>document.querySelector('#mascot').getAnimations().map(a=>a.currentTime));
   assert.ok(times.every(t=>t>100&&t<5000),'animation has continued, not restarted or finished');
  }
  await page.waitForFunction(()=>!document.documentElement.classList.contains('story-run'));
  const fetches=await page.evaluate(()=>probes.fetches);
  for(const [url,count] of Object.entries(fetches))if(url.includes('.mp3'))assert.equal(count,1,'each sound loads once');
  await page.locator('.story-replay').click();await page.waitForSelector('.story-layer');
  await page.locator('.story-skip').click();await page.waitForSelector('.story-layer',{state:'detached'});
  assert.deepEqual(current.errors,[]);
  await page.screenshot({path:path.join(root,'../puru-performance-verified.png')});
  console.log('PASS: Chromium width/height/breakpoint resize continues; no repeated audio; natural finish; replay/skip; no JS errors.');
  await current.context.close();
  const reduced=await browser.newContext({reducedMotion:'reduce'}),rp=await reduced.newPage();
  await rp.goto(base);await rp.locator('#enter-puru').click();assert.equal(await rp.locator('.story-layer').count(),0);
  await reduced.close();console.log('PASS: reduced-motion preference preserved.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>server.close());
