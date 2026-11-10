const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
// Execute the production script with deterministic DOM, WAAPI and Web Audio fakes.
let now = 0, starts = 0;
const events = {}, timers = new Map();
const frames=[];
let timerId = 0;
class Element {
  constructor() {
    this.children = []; this.dataset = {}; this.style = {setProperty(){}};
    this.offsetWidth = 100; this.offsetHeight = 40;
    this.events = {}; this.nodes = {};
    this.classList = {add(){},remove(){},contains(){return false;},toggle(){}};
  }
  append(...els){this.children.push(...els);}
  insertBefore(el){this.append(el);}
  remove(){this.removed = true;}
  setAttribute(){}
  querySelector(key){return this.nodes[key] ||= new Element();}
  querySelectorAll(){return [];}
  insertAdjacentHTML(){}
  addEventListener(key, fn){this.events[key] = fn;}
  getBoundingClientRect(){return {left:0,top:0,width:context.innerWidth,height:context.innerHeight};}
  animate(frames, options){return {frames,options,currentTime:0,cancel(){this.cancelled=true;}};}
}
class AudioContext {
  constructor(){this.state='running';this.currentTime=0;}
  resume(){return Promise.resolve();}
  addEventListener(){}
  decodeAudioData(){return Promise.resolve({});}
  createGain(){return {gain:{value:0},connect(){}};}
  createBufferSource(){return {playbackRate:{value:1},connect(){},start(){starts++;},stop(){this.stopped=true;}};}
}
const nodes = {}, html = new Element();
const context = {
  console, performance:{now:()=>now}, innerWidth:1200,innerHeight:800,scrollY:0,
  navigator:{hardwareConcurrency:1,deviceMemory:1},
  document:{documentElement:html,body:new Element(),hidden:false,
    querySelector:key=>key==='#entry-gate'?null:(nodes[key] ||= new Element()),
    querySelectorAll:()=>[],createElement:()=>new Element(),
    addEventListener:(key,fn)=>(events[key] ||= []).push(fn)},
  AudioContext,fetch:async()=>({ok:true,arrayBuffer:async()=>new ArrayBuffer(0)}),
  matchMedia:()=>({matches:false}),scrollTo(){},
  setTimeout:(fn,ms)=>{timers.set(++timerId,{fn,ms});return timerId;},
  clearTimeout:id=>timers.delete(id),requestAnimationFrame:fn=>frames.push(fn),
  addEventListener:(key,fn)=>(events[key] ||= []).push(fn),removeEventListener(){},
};
context.window=context;
const source=fs.readFileSync(path.join(__dirname,'../motion.js'),'utf8');
const exposed=source.replace(/\}\)\(\);\s*$/, 'globalThis.storyTest={start,finish,loadSound,get run(){return run;}};})();');
vm.runInNewContext(exposed,context);
const resize=()=>events.resize.forEach(fn=>fn());
const flush=()=>{while(frames.length)frames.shift()();};
(async()=>{
  await context.storyTest.start();
  await new Promise(resolve=>setImmediate(resolve));
  const first=context.storyTest.run, sources=first.srcs, audioStarts=starts;
  assert.ok(audioStarts>0,'audio must be scheduled before resize');
  for(const [width,height,time] of [[900,800,1200],[900,500,2400],[390,700,3600],[1200,800,5000]]){
    now=time;context.innerWidth=width;context.innerHeight=height;resize();flush();
    const active=context.storyTest.run;
    assert.ok(active,'resizing must not cut the story to its final frame');
    assert.equal(active.t0,first.t0,'original playback clock must survive');
    assert.equal(active.srcs,sources,'keep the same scheduled audio sources');
    assert.equal(starts,audioStarts,'resizing must not replay audio');
    assert.ok(active.anims.every(a=>a.currentTime===time),'all rebuilt animations seek to the same elapsed time');
    assert.equal(timers.get(active.timer).ms,6720-time,'finish at the original deadline');
  }
  const active=context.storyTest.run;
  resize();flush();assert.equal(context.storyTest.run,active,'unchanged size does not rebuild');
  context.innerWidth=1000;for(let i=0;i<30;i++)resize();
  assert.equal(frames.length,1,'thirty resize events must merge into one frame');
  assert.equal(context.storyTest.run,active,'do not rebuild before the frame');flush();
  const rebuilt=context.storyTest.run;
  context.storyTest.finish('skip');
  assert.equal(context.storyTest.run,null);
  assert.ok(sources.every(s=>s.stopped),'skip stops every scheduled sound');
  assert.ok(rebuilt.anims.every(a=>a.cancelled),'skip cancels the visual animations');
  let requests=0;context.fetch=async()=>{requests++;return {ok:true,arrayBuffer:async()=>new ArrayBuffer(0)};};
  const firstLoad=context.storyTest.loadSound('probe','probe.mp3'),secondLoad=context.storyTest.loadSound('probe','probe.mp3');
  assert.equal(firstLoad,secondLoad,'concurrent sound loads share one promise');
  await Promise.all([firstLoad,secondLoad]);assert.equal(requests,1);
  await context.storyTest.loadSound('probe','probe.mp3');assert.equal(requests,1,'decoded sounds remain cached');
  console.log('PASS: resize continuation, thirty-event batching, audio reuse/load deduplication, deadline, skip cleanup');
})().catch(e=>{console.error(e);process.exitCode=1;});
