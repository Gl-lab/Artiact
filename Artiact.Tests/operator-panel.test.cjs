const {test}=require('node:test');
const assert=require('node:assert/strict');
const {readFileSync}=require('node:fs');
const vm=require('node:vm');

function panel(inspect={Accepted:true,Reason:'InspectReady',Receipt:'fresh'}){
    const elements=new Map(), calls=[];
    const element=id=>{
        if(!elements.has(id)) elements.set(id,{value:'5',hidden:true,disabled:false,children:[],dataset:{},
            textContent:'',addEventListener(type,handler){this[type]=handler},
            replaceChildren(){this.children=[]},append(...items){this.children.push(...items)},
            reportValidity(){return true},setAttribute(){}});
        return elements.get(id);
    };
    const context=vm.createContext({document:{getElementById:element,createElement:()=>element(Symbol())},
        crypto:{randomUUID:()=> 'new-id'},setTimeout:()=>0,clearTimeout(){},console,
        fetch:async(url,options)=>{
            calls.push({url,body:options?.body&&JSON.parse(options.body)});
            const value=url.endsWith('/controls')?{Enabled:true,Token:'token',Profile:{Limits:{RunId:'test',MaxActions:5,MaxSeconds:60,MaxDecisions:5,MaxNoProgress:3}}}:
                url.endsWith('/inspect')?await (typeof inspect==='function'?inspect():inspect):
                url.endsWith('/start')?{Accepted:true,Reason:'StartAccepted'}:
                {Executor:'Idle',Storage:'NoRun',Freshness:'Unavailable',Enabled:false};
            return {ok:true,json:async()=>value};
        }});
    vm.runInContext(readFileSync('Artiact/Operator/panel.js','utf8'),context);
    return {element,calls,settle:()=>new Promise(resolve=>setImmediate(resolve))};
}
test('Start obtains a fresh receipt and starts without a separate preview',async()=>{
    const p=panel();await p.settle();
    await p.element('start').click();await p.settle();
    assert.deepEqual(p.calls.filter(c=>/\/(inspect|start)$/.test(c.url)).map(c=>[c.url,c.body.Receipt]),
        [['/operator/inspect',undefined],['/operator/start','fresh']]);
});
test('Rejected preparation never starts and explains the refusal',async()=>{
    const p=panel({Accepted:false,Reason:'BudgetExhausted'});await p.settle();
    await p.element('start').click();await p.settle();
    assert.equal(p.calls.some(c=>c.url.endsWith('/start')),false);
    assert.match(p.element('control-result').textContent,/Бюджет/);
});
test('Repeated clicks during preparation produce one start',async()=>{
    let complete;const p=panel(()=>new Promise(resolve=>{complete=resolve}));await p.settle();
    p.element('start').click();p.element('start').click();await p.settle();
    assert.equal(p.calls.filter(c=>c.url.endsWith('/inspect')).length,1);
    complete({Accepted:true,Receipt:'fresh',Reason:'InspectReady'});await p.settle();
    assert.equal(p.calls.filter(c=>c.url.endsWith('/start')).length,1);
});
test('Preview stays read-only and a later start checks again',async()=>{
    const p=panel();await p.settle();
    await p.element('inspect').click();await p.settle();
    assert.equal(p.calls.some(c=>c.url.endsWith('/start')),false);
    await p.element('start').click();await p.settle();
    assert.equal(p.calls.filter(c=>c.url.endsWith('/inspect')).length,2);
});
test('Inconsistent limits are explained before requesting preparation',async()=>{
    const p=panel();await p.settle();p.element('input-no-progress').value='6';
    await p.element('start').click();await p.settle();
    assert.equal(p.calls.some(c=>c.url.endsWith('/inspect')),false);
    assert.match(p.element('control-result').textContent,/не должен превышать/);
});
test('Preparation transport failure releases controls without retrying or starting',async()=>{
    const p=panel(()=>{throw Error('offline')});await p.settle();
    await p.element('start').click();await p.settle();
    assert.equal(p.calls.filter(c=>c.url.endsWith('/inspect')).length,1);
    assert.equal(p.calls.some(c=>c.url.endsWith('/start')),false);
    assert.equal(p.element('start').disabled,false);
    assert.match(p.element('control-result').textContent,/не подтверждён/);
});
