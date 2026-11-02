/* Opening story: the octopus meets the real hero. DOM + Web Animations only, transform and opacity only.
   The page's own hero elements are the final frame, so the hand-off needs no swap and no video. */
(()=>{
const html=document.documentElement,hero=document.querySelector('.hero'),oct=document.querySelector('#mascot');
let run=null,ac=null,replay=null;
/* the proud face lives on the page: eyes, thick red lips and a glint sit on a wrapper that bobs with the sprite, so it stays registered with the artwork */
const art=oct.querySelector('picture')||oct.querySelector('img'),face=document.createElement('span');face.className='face';oct.insertBefore(face,art);face.append(art);
const prof=document.createElement('div');prof.className='proud';prof.setAttribute('aria-hidden','true');
prof.innerHTML='<i class="pe" style="left:42.8%;top:47.3%;width:5.4%;height:6.4%"></i><i class="pe" style="left:54.9%;top:44.2%;width:4.6%;height:6.4%"></i><i class="mo" style="left:48.8%;top:50.9%;width:7.6%;height:7.2%"></i><i class="gh" style="left:60.6%;top:44%"></i><i class="gl" style="left:60.6%;top:44%"></i>';face.append(prof);
const setU=()=>prof.style.setProperty('--u',(face.offsetWidth/100)+'px');setU();addEventListener('resize',setU);
const settle=()=>prof.classList.add('on');
oct.addEventListener('click',callOcto);setTimeout(()=>{loadOcto().catch(()=>{})},2500);
if(!html.classList.contains('story-intro'))settle();const bufs={},SFX={fly1:'assets/sfx-fly1.mp3',fly2:'assets/sfx-fly2.mp3',swish:'assets/sfx-swish.mp3',catch:'assets/sfx-catch.mp3',brand:'assets/brand.mp3'};
/* sound: Web Audio so 19 whooshes and catches land on the beat. Plays by default; where the browser blocks sound before a tap, it joins at the visitor's first tap or key. */
function playCues(){
 if(!run||!ac||ac.state!=='running')return;
 const el=performance.now()-run.t0;
 run.cues.forEach(c=>{if(c.done||!bufs[c.k])return;const dt=(c.t-el)/1000;c.done=true;if(dt<-.08)return;
  const s=ac.createBufferSource();s.buffer=bufs[c.k];s.playbackRate.value=c.r;const g=ac.createGain();g.gain.value=c.g*.85;s.connect(g);g.connect(ac.destination);s.start(ac.currentTime+Math.max(0,dt));run.srcs.push(s);});
}
/* after the story, clicking the octopus makes her call */
async function loadOcto(){if(bufs.octo)return;ac=ac||new(window.AudioContext||window.webkitAudioContext)();const r=await fetch('assets/sfx-octopus.mp3');bufs.octo=await ac.decodeAudioData(await r.arrayBuffer());}
async function callOcto(){
 if(run)return;
 try{await loadOcto();await ac.resume();const s=ac.createBufferSource();s.buffer=bufs.octo;s.playbackRate.value=.94+Math.random()*.12;const g=ac.createGain();g.gain.value=.9;s.connect(g);g.connect(ac.destination);s.start();}catch{}
}
async function sfxStart(){
 try{
  ac=ac||new(window.AudioContext||window.webkitAudioContext)();
  if(ac.state!=='running'){ac.resume().then(playCues).catch(()=>{});const go=()=>{removeEventListener('pointerdown',go);removeEventListener('keydown',go);removeEventListener('touchstart',go);ac&&ac.resume().then(playCues).catch(()=>{});};addEventListener('pointerdown',go,{once:true});addEventListener('keydown',go,{once:true});addEventListener('touchstart',go,{once:true});}
  await Promise.all(Object.entries(SFX).map(async([k,u])=>{if(bufs[k])return;const r=await fetch(u);bufs[k]=await ac.decodeAudioData(await r.arrayBuffer());}));
  playCues();
 }catch{}
}
async function replayWithSound(){
 if(run||!motion)return;
 replay.disabled=true;
 try{
  ac=ac||new(window.AudioContext||window.webkitAudioContext)();
  const reflect=()=>replay.dataset.audioState=ac.state;
  if(!replay.dataset.audioObserved){ac.addEventListener('statechange',reflect);replay.dataset.audioObserved='true';}
  const unlocked=ac.resume();
  reflect();
  await unlocked;
  reflect();
  if(ac.state!=='running'){replay.disabled=false;return;}
  await start();
 }catch{replay.disabled=false;}
}
const T=6800,$$=s=>hero.querySelector(s);
const TEXTS=['新訊息 12','15:00 會議','待辦 × 9','請盡快回覆','Inbox 99+','Re: Re: Fwd:','更新提醒','尚未讀取','審核中'];
const T3=(x,y,r=0,sx=1,sy=1)=>`translate(${x}px,${y}px) rotate(${r}deg) scale(${sx},${sy})`;
function finish(msg){
 if(!run)return;
 const run0=run;
 clearTimeout(run.timer);
 html.classList.remove('story-intro','story-run');
 run.anims.forEach(a=>{try{a.cancel()}catch{}});
 settle();(run.pks||[]).forEach(e=>e.remove());run.layer.remove();run=null;
 if(replay)replay.disabled=false;
 if(msg&&run0&&run0.srcs)run0.srcs.forEach(s=>{try{s.stop()}catch{}});
}
async function start(){
 if(run)return;
 if(!motion||!hero.animate){html.classList.remove('story-intro');settle();return;}
 run={prof,cues:[],srcs:[],t0:performance.now(),anims:[],layer:document.createElement('div'),timer:0,width:innerWidth};
 try{
  window.scrollTo({top:0,behavior:'auto'});
  html.classList.add('story-ran','story-intro','story-run');
  const img=oct.querySelector('img');
  if(img&&img.decode)await Promise.race([img.decode().catch(()=>{}),new Promise(r=>setTimeout(r,1500))]);
  if(!run)return;
  const layer=run.layer;layer.className='story-layer';hero.append(layer);
  const skip=document.createElement('button');skip.className='story-skip';skip.textContent='略過 ↓';skip.setAttribute('aria-label','略過開場動畫');skip.addEventListener('click',()=>finish('已略過開場。'));layer.append(skip);
  const L=layer.getBoundingClientRect(),LW=L.width,VH=Math.max(260,Math.min(L.height,innerHeight-L.top));/* pile only where the visitor can see it */
  const rel=el=>{const r=el.getBoundingClientRect();return{x:r.left-L.left,y:r.top-L.top,w:r.width,h:r.height,cx:r.left-L.left+r.width/2,cy:r.top-L.top+r.height/2};};
  const A=(el,fr,o)=>{const a=el.animate(fr,o);run.anims.push(a);return a;};
  /* Fibonacci barrage: 1,2,3,5,8 objects fly straight left to right at the octopus, faster and tighter each wave (last wave = bullets); she catches each with an arm, then looks proud */
  const WAVES=[[1,600,520,0],[2,1550,440,240],[3,2400,360,160],[5,3100,280,100],[8,3700,190,45]];
  const KINDS=['win','text','alert','text','win','text','alert','text','text','win','alert','text','text','alert','text','win','text','alert','alert'];
  const HANDS=[[.73,.22],[.15,.51],[.23,.72],[.83,.68]];
  const oc=rel(oct),ev=[[0,0,0,0,1,1],[300,0,-4,-2,1.03,1.03],[430,0,0,0,1,1]];
  const cue=(t,k,g,r=1)=>run.cues.push({t,k,g,r});
  let n=0,ti=0;
  WAVES.forEach(([cnt,start,fl,gap],wi)=>{for(let k=0;k<cnt;k++,n++){
   const kind=KINDS[n],c=document.createElement('div');c.className='sc sc-'+kind;c.setAttribute('aria-hidden','true');
   if(kind==='text')c.textContent=TEXTS[ti++%TEXTS.length];else if(kind==='alert')c.textContent='!';else c.innerHTML='<i></i><i></i><i></i><b></b><b></b>';
   const tr=document.createElement('u');tr.className='tr';c.append(tr);layer.append(c);
   const w=c.offsetWidth,h=c.offsetHeight,f=(n*.618)%1,bullet=wi===4;
   const hd=HANDS[(n*3)%4],tx=oc.x+hd[0]*oc.w,ty=oc.y+hd[1]*oc.h,ox=-w-60;
   const t0=start+k*gap,hit=t0+fl,ss=bullet?2.1:1.5,sy=bullet?.8:.88;
   /* straight horizontal flight, stretched along the motion with a fading streak: motion blur without filters */
   A(c,[{offset:0,transform:T3(ox,ty-h/2,0,ss,sy),easing:'cubic-bezier(.35,0,.9,.75)'},{offset:.9,transform:T3(tx-w/2-6,ty-h/2,0,ss,sy),easing:'ease-out'},{offset:1,transform:T3(tx-w/2,ty-h/2,0)}],{delay:t0,duration:fl,fill:'both'});
   A(tr,[{opacity:1},{opacity:1,offset:.88},{opacity:0}],{delay:t0,duration:fl,fill:'both'});
   const endT=T3(tx-w/2,ty-h/2,0);
   A(c,[{offset:0,transform:endT,opacity:1},{offset:.4,transform:endT,opacity:1},{offset:1,transform:T3(tx-w/2,ty-h/2,0,.3,.3),opacity:0}],{delay:hit,duration:bullet?120:180,fill:'forwards'});
   const m=3+wi*1.6,rt=Math.min((gap||120)-8,80);
   ev.push([hit,m,0,1+wi*.5,1+.03+.02*wi,1-.03-.02*wi],[hit+rt,0,0,0,1,1]);
   if(wi<4)cue(t0,wi===1?'fly2':'fly1',wi===1?1:.55,1+f*.1);else if(k%2===0)cue(t0,'swish',.45,1.1+f*.3);
   cue(hit,'catch',.85,.95+f*.15);
  }});
  ev.sort((a,b)=>a[0]-b[0]);
  /* proud look: chest out, "^ ^" eyes and sparkles, then the page's real hero appears */
  ev.push([4560,0,0,0,1.03,.97,'ease-out'],[4760,0,-4,-3,1.04,1.06,'cubic-bezier(.2,.8,.3,1.2)'],[5500,0,-4,-3,1.03,1.05],[5800,0,0,-1,1.01,1],[6150,0,0,0,1,1]);
  A(oct,ev.map(([t,x,y,r,sx,sy,e])=>({offset:t/T,transform:T3(x,y,r,sx,sy),easing:e||'ease-out'})).concat([{offset:1,transform:T3(0,0)}]),{duration:T,fill:'forwards'});
  const prof=run.prof;prof.insertAdjacentHTML('beforeend','<i class="pk" style="left:30%;top:20%"></i><i class="pk r" style="left:66%;top:25%"></i>');const pkEls=[...prof.querySelectorAll('.pk')];run.pks=pkEls;
  const P0=4760;
  A(prof,[{opacity:0,offset:0},{opacity:0,offset:P0/T},{opacity:1,offset:(P0+1)/T},{opacity:1,offset:1}],{duration:T,fill:'both'});
  pkEls.forEach(el=>A(el,[{opacity:1},{opacity:1,offset:.7},{opacity:0}],{delay:P0,duration:1300,fill:'both'}));
  prof.querySelectorAll('.pk,.gl').forEach((el,i)=>A(el,[{transform:'scale(0) rotate(0deg)'},{transform:'scale(1.4) rotate(45deg)'},{transform:'scale(1) rotate(90deg)'}],{delay:P0+80+i*140,duration:320,easing:'ease-out',fill:'both'}));
  A(prof.querySelector('.mo'),[{transform:'scale(.4)'},{transform:'scale(1.22)'},{transform:'scale(.96)'},{transform:'scale(1)'}],{delay:P0,duration:360,easing:'ease-out',fill:'both'});
  const rv=(el,at,dur=520)=>A(el,[{opacity:0,transform:'translateY(18px) scale(.95)'},{opacity:1,transform:'none'}],{delay:at,duration:dur,easing:'cubic-bezier(.16,1,.3,1)',fill:'both'});
  const RV=5550;
  rv($$('.hero-red'),RV,300);A($$('.hero-red'),[{transform:'scale(1)'},{transform:'scale(1.7)'},{transform:'scale(1)'}],{delay:RV+300,duration:380,fill:'forwards'});
  rv($$('h1'),RV+100,640);rv($$('.brand-zh'),RV+250);rv($$('.intro'),RV+350);
  cue(4760,'brand',.9);
  run.t0=performance.now();
  sfxStart();
  run.timer=setTimeout(()=>finish(),T-80);
 }catch{settle();html.classList.remove('story-intro','story-run');if(run){run.anims.forEach(a=>{try{a.cancel()}catch{}});run.layer.remove();run=null;}if(replay)replay.disabled=false;}
}
document.addEventListener('visibilitychange',()=>{if(document.hidden&&run)finish('播放已停止，可隨時重播。');});
addEventListener('resize',()=>{if(run&&innerWidth!==run.width)finish();});
const reduce=matchMedia('(prefers-reduced-motion: reduce)');
const toggle=document.createElement('button');toggle.className='motion-toggle';toggle.textContent='動態效果：'+(reduce.matches?'關閉':'開啟');toggle.setAttribute('aria-pressed',String(!reduce.matches));{const ft=document.querySelector('footer');ft.insertBefore(toggle,ft.querySelector('.legal'));}
let motion=!reduce.matches;document.body.classList.toggle('motion-paused',!motion);
const progress=document.createElement('div');progress.className='reading-progress';progress.setAttribute('aria-hidden','true');document.body.append(progress);
let pending=false;function update(){const max=document.documentElement.scrollHeight-innerHeight;progress.style.width=innerWidth+'px';progress.style.transform='scaleX('+(max>0?scrollY/max:0)+')';pending=false;}addEventListener('scroll',()=>{if(!pending){pending=true;requestAnimationFrame(update)}},{passive:true});addEventListener('resize',update);update();
/* Scroll reveals: opacity + transform only, one shared IntersectionObserver, no scroll handlers.
   Old or weak machines (<=2 cores, <=2 GB, no IntersectionObserver) get the lite page: content is simply there. */
const lowEnd=(navigator.hardwareConcurrency||4)<=2||(navigator.deviceMemory||4)<=2||!('IntersectionObserver' in window);
if(lowEnd)html.classList.add('lite');
if(motion&&!lowEnd){
const rvEls=[...document.querySelectorAll('.family-intro h2,.verb-grid,.family-note,.cut-copy>*,.cut-visual,.talk-visual,.talk-grid>div:last-child>*,.experience-grid>div:first-child>*,.idea-result,.philosophy-title,.about-grid>*,.closing>*')];
const seen=new Map();rvEls.forEach(el=>{const n=seen.get(el.parentNode)||0;seen.set(el.parentNode,n+1);el.classList.add('rv');el.style.setProperty('--d',Math.min(n,5)*80+'ms');});
let fired=false;
const io=new IntersectionObserver(entries=>{fired=true;for(const e of entries)if(e.isIntersecting){e.target.classList.add('in');if(e.target.classList.contains('verb-grid'))e.target.classList.add('reveal-arrive');io.unobserve(e.target);}},{threshold:.12,rootMargin:'0px 0px -6% 0px'});
html.classList.add('rv-on');rvEls.forEach(el=>io.observe(el));
setTimeout(()=>{if(!fired)rvEls.forEach(el=>el.classList.add('in'));},2500);
}
const tk=document.querySelector('.ticker');if(tk&&'IntersectionObserver' in window)new IntersectionObserver(es=>tk.classList.toggle('off',!es[0].isIntersecting)).observe(tk);
toggle.addEventListener('click',()=>{motion=!motion;document.body.classList.toggle('motion-paused',!motion);toggle.setAttribute('aria-pressed',String(motion));toggle.textContent='動態效果：'+(motion?'開啟':'關閉');if(!motion&&run)finish('已關閉動態效果。');});
replay=document.createElement('button');replay.type='button';replay.className='story-replay';replay.textContent='↻ 重播有聲開場';replay.setAttribute('aria-label','重播有聲開場');replay.dataset.audioState='idle';replay.hidden=!motion||lowEnd;replay.addEventListener('click',replayWithSound);document.querySelector('.hero-art').append(replay);
toggle.addEventListener('click',()=>{replay.hidden=!motion||lowEnd;});
if(html.classList.contains('story-intro'))start();
})();
