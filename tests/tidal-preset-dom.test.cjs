// Execute the compiled production expression against a minimal DOM contract.
// Live DOM selector evidence is recorded separately; this checks branch effects.
const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const expression = fs.readFileSync(process.argv[2], 'utf8');
function fixture(options = {}) {
  const o = { active: true, id: '1564299', footerId: '1564299', state: 'paused', ...options };
  const calls = {row: 0, play: 0, pause: 0};
  const pause = {disabled: !!o.disabledPause, click() {calls.pause++; o.state='paused';}};
  const play = {disabled: !!o.disabledPlay, click() {calls.play++; o.state='playing';}};
  const row = {
    textContent: o.title ?? 'Hard Times League Unlimited Orchestra',
    getAttribute(name) {return name === 'data-test-is-playing' ? String(o.active) : name === 'data-track-id' ? o.id : null;},
    querySelector(selector) {
      if (selector === '[data-test="table-row-title"]') return {textContent: this.textContent};
      if (selector === '[data-test="play-button"]') return o.rowButton ? {click() {calls.row++;}} : null;
      throw new Error('Unexpected row selector '+selector);
    }
  };
  const footer = {
    querySelector(selector) {
      if (selector === 'a[href^="/track/"]') return o.footerId === null ? null : {getAttribute(name) {assert.equal(name,'href'); return '/track/'+o.footerId;}};
      if (selector === '[data-test="play"]') return o.state==='paused' ? play : null;
      if (selector === '[data-test="pause"]') return o.state==='playing' ? pause : null;
      throw new Error('Unexpected footer selector '+selector);
    }
  };
  const document = {
    querySelectorAll(selector) {assert.equal(selector,'[data-test="tracklist-row"]'); return o.noRows ? [] : [row];},
    querySelector(selector) {assert.equal(selector,'#footerPlayer'); return o.noFooter ? null : footer;}
  };
  return {calls,run:()=>vm.runInNewContext(expression,{document})};
}
const tests = [
  ['paused current resumes, repetition does not pause', () => {
    const f=fixture(); assert.equal(f.run(),'ok-current'); assert.deepEqual(f.calls,{row:0,play:1,pause:0});
    assert.equal(f.run(),'ok-current'); assert.deepEqual(f.calls,{row:0,play:1,pause:0});
  }],
  ['playing current unchanged',()=>{const f=fixture({state:'playing'});assert.equal(f.run(),'ok-current');assert.deepEqual(f.calls,{row:0,play:0,pause:0});}],
  ['normal unselected row remains playable',()=>{const f=fixture({active:false,rowButton:true,footerId:'other'});assert.equal(f.run(),'ok');assert.deepEqual(f.calls,{row:1,play:0,pause:0});}],
  ...[
    ['different footer',{footerId:'other'}],['inactive row',{active:false}],['missing row ID',{id:null}],
    ['missing footer link',{footerId:null}],['missing footer',{noFooter:true}],['missing controls',{state:'none'}],
    ['disabled resume',{disabledPlay:true}],['disabled pause',{state:'playing',disabledPause:true}]
  ].map(([name,opts])=>[name,()=>{const f=fixture(opts);assert.equal(f.run(),'brak-przycisku-w-wierszu');assert.deepEqual(f.calls,{row:0,play:0,pause:0});}]),
  ['no list',()=>{assert.equal(fixture({noRows:true}).run(),'brak-listy-utworow');}],
  ['wrong title',()=>{assert.equal(fixture({title:'Unrelated'}).run(),'brak-utworu');}]
];
const failures=[];
for(const [name,test] of tests){try{test();console.log('PASS '+name);}catch(e){failures.push({name,error:e.message});console.log('FAIL '+name+': '+e.message);}}
console.log(JSON.stringify({total:tests.length,passed:tests.length-failures.length,failures}));
process.exitCode=failures.length ? 1 : 0;
