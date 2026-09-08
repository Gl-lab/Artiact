const {test}=require('node:test');
const assert=require('node:assert/strict');
const {readFileSync}=require('node:fs');
const vm=require('node:vm');

test('Rejected preparation shows numeric inventory evidence without a saved run',async()=>{
    const p=panel({Accepted:false,Reason:'NoFeasibleCandidate',Decision:{Candidates:[{Id:'skill:mining:ore',Skill:'mining',Target:10,Rejection:'EstimatedInventoryInsufficient',Feasibility:{FreeUnits:57,RequiredUnits:80,DeficitUnits:23,BankConfigured:false}}]}});
    await p.settle();await p.element('start').click();await p.settle();
    assert.match(p.element('inspect-diagnostics').children.map(x=>x.textContent).join(' '),/57.*80.*23/);
    assert.equal(p.calls.some(c=>c.url.endsWith('/start')),false);
});

test('Saved mixed refusals retain stale label, unknown codes and expandable alternatives',async()=>{
    const p=panel(undefined,{Executor:'Blocked',Freshness:'Stale',Run:{Status:'Blocked',Candidates:[
        {Id:'one',Rejection:'EstimatedPathExceedsBudget',Feasibility:{FreeUnits:57,RequiredUnits:2,DeficitUnits:0,BankConfigured:false,RequiredActions:3,RemainingActions:2,RequiredSeconds:140,RemainingSeconds:120}},
        {Id:'two',Rejection:'NoSupportedResource'},{Id:'three',Rejection:'FutureCode<script>'},{Id:'four',Rejection:'UnsupportedAccess'}]}});
    await p.settle();const rows=p.element('run-diagnostics').children;
    assert.match(rows[0].textContent,/действий: 3, осталось: 2/);
    assert.match(rows[1].textContent,/Нет поддерживаемого ресурса/);
    assert.match(rows[2].textContent,/FutureCode<script>/);
    assert.match(rows[3].children[0].textContent,/альтернативы \(1\)/);
    assert.match(p.element('freshness').textContent,/Устаревшие/);
});

test('Historical result without diagnostic evidence explicitly shows missing details',async()=>{
    const p=panel(undefined,{Executor:'Blocked',Run:{Status:'Blocked'}});await p.settle();
    assert.match(p.element('run-diagnostics').children[0].textContent,/отсутствуют/);
});

function panel(inspect={Accepted:true,Reason:'InspectReady',Receipt:'fresh'},snapshot={Executor:'Idle',Storage:'NoRun',Freshness:'Unavailable',Enabled:false},start={Accepted:true,Reason:'StartAccepted'}){
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
                url.endsWith('/start')?start:snapshot;
            return {ok:true,json:async()=>value};
        }});
    vm.runInContext(readFileSync('Artiact/Operator/panel.js','utf8'),context);
    return {element,calls,refresh:()=>vm.runInContext('poll()',context),settle:()=>new Promise(resolve=>setImmediate(resolve))};
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

for(const status of ['Blocked','Cancelled','UnknownOutcome','Nonterminal']){
    test(`Archive explains why ${status} cannot be archived`,async()=>{
        const p=panel(undefined,{Executor:'AwaitingRecovery',Run:{RunId:'saved',IdentityDigest:'digest',Status:status,InterventionRequired:status!=='Nonterminal'}});
        await p.settle();
        assert.equal(p.element('archive').disabled,true);
        assert.ok(p.element('archive-help').textContent.length>30);
        assert.match(p.element('archive-help').textContent,/нельзя|недоступен|не завершён/);
    });
}
test('Identity mismatch does not offer unavailable archival',async()=>{
    const p=panel(undefined,{Executor:'Blocked',Run:{RunId:'saved',IdentityDigest:'digest',Status:'Blocked',InterventionRequired:true}},
        {Accepted:false,Reason:'ExistingRunIdentityMismatch'});
    await p.settle();await p.element('start').click();await p.settle();
    assert.doesNotMatch(p.element('control-result').textContent,/или сохраните/);
    assert.match(p.element('control-result').textContent,/saved/);
});
test('Completed run keeps archival available with an explanation',async()=>{
    const p=panel(undefined,{Executor:'Completed',Run:{RunId:'saved',IdentityDigest:'digest',Status:'Completed',CanArchive:true}});
    await p.settle();assert.equal(p.element('archive').disabled,false);
    assert.match(p.element('archive-help').textContent,/архив/);
});

test('Verified no-goal outcome can be archived but cannot be resumed',async()=>{
    const p=panel(undefined,{Executor:'Blocked',Run:{RunId:'saved',IdentityDigest:'digest',Status:'Blocked',Reason:'NoFeasibleCandidate',CanArchive:true,InterventionRequired:true}});
    await p.settle();assert.equal(p.element('archive').disabled,false);assert.equal(p.element('recover').disabled,true);
    await p.element('start').click();await p.settle();
    assert.equal(p.calls.some(c=>c.url.endsWith('/start')||c.url.endsWith('/inspect')),false);
    assert.match(p.element('control-result').textContent,/Сохраните итог в архив/);
});

test('Accepted message changes when the run stops without an available goal',async()=>{
    const snapshot={Executor:'Idle'};
    const p=panel(undefined,snapshot);await p.settle();await p.element('start').click();await p.settle();
    snapshot.Executor='Blocked';snapshot.Run={RunId:'saved',IdentityDigest:'digest',Status:'Blocked',Reason:'NoFeasibleCandidate',CanArchive:true};
    await p.refresh();
    assert.doesNotMatch(p.element('control-result').textContent,/Запуск принят/);
    assert.match(p.element('control-result').textContent,/архив/);
    assert.equal(p.element('archive').disabled,false);
});
