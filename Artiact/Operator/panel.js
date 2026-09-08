const el=id=>document.getElementById(id);
async function loadOrders(){try{const response=await fetch('/operator/orders',{cache:'no-store'});if(!response.ok)throw Error();const book=await response.json();if(!changed("orders-list",book.Orders))return;const root=el('orders-list');root.replaceChildren();for(const order of book.Orders){const row=document.createElement('p');row.textContent=`${order.Code} × ${order.Quantity} · приоритет ${order.Priority} · ${{Active:'активен',Completed:'выполнен',Cancelled:'отменён'}[order.Status]??order.Status} `;if(order.Status==='Active'){const cancel=document.createElement('button');cancel.type='button';cancel.textContent='Отменить';cancel.onclick=()=>saveOrder({...order,Status:'Cancelled',Revision:order.Revision+1},order.Revision);row.append(cancel)}root.append(row)}}catch{text('order-result','Заказы недоступны. Повторите проверку.')}}
async function saveOrder(order,revision){try{const response=await fetch('/operator/orders',{method:'POST',headers:{'Content-Type':'application/json','X-Artiact-Control':controlToken},body:JSON.stringify({Order:order,ExpectedRevision:revision})});text('order-result',response.ok?'Заказ сохранён. Следующая проверка использует свежие потребности.':'Изменение отклонено. Проверьте свежую версию заказа.');await loadOrders()}catch{text('order-result','Ответ не получен. Обновите список перед повтором.')}}
el('order-form').addEventListener('submit',async event=>{event.preventDefault();if(!el('order-form').reportValidity())return;await saveOrder({Id:crypto.randomUUID(),Code:el('order-code').value,Quantity:Number(el('order-quantity').value),Priority:Number(el('order-priority').value),Revision:1,Status:'Active'},0)});
function diagnosticText(c){
    const names={mining:'Добыча',woodcutting:'Рубка',fishing:'Рыбалка',alchemy:'Алхимия'};
    const explanations={EstimatedNoProgressInsufficient:'Недостаточный предел без прогресса',UnsupportedNeedEstimate:'Полная цепочка не подтверждена поддерживаемой оценкой',NeedChainCycleOrBound:'Цепочка содержит цикл или превышает предел оценки',EstimatedInventoryInsufficient:'Недостаточно места для оценённого сбора',EstimatedPathExceedsBudget:'Оценённый путь превышает бюджет',NoSupportedResource:'Нет поддерживаемого ресурса',UnsupportedAccess:'Маршрут недоступен',UnsupportedResourceAccess:'Маршрут к ресурсу недоступен',TrainingCannotReachMilestone:'Ресурс не позволяет достичь рубежа',InventoryFull:'Инвентарь заполнен'};
    let value=`${names[c.Skill]??c.Skill??c.Id}${c.Target!=null?' → '+c.Target:''}: ${explanations[c.Rejection]??(c.Rejection?'Причина требует проверки':'Доступный маршрут')} [${c.Rejection??'Selected'}]`;
    const f=c.Feasibility;
    if(c.Need){const n=c.Need;value+=` · ${n.Cause}. Дефицит: ${n.Deficit}. Весь результат: ${n.Quantity} ${n.Code}, оценка ${n.FullActions} действий / ${n.FullSeconds} сек. Текущий срез: ${n.SliceActions} действий / ${n.SliceSeconds} сек; результат: ${n.SliceResult==='ParentSatisfied'?'потребность удовлетворена по оценке':n.SliceResult}. Предел без прогресса: ${n.ConfiguredNoProgress}; требуется: ${n.RequiredNoProgress}. Выбор: приоритет ${n.Priority}, затем полная стоимость пути. Цепочка (оценка): ${(n.Plan??[]).join(" → ")}.`;if(c.Rejection==='EstimatedNoProgressInsufficient')value+=' Настроенного предела без прогресса недостаточно; действия не начаты.';}
    const bankReasons={NoDepositableStock:'Нет разрешённого избытка для разгрузки',BankFull:'В банке недостаточно свободных слотов',NoSupportedBank:'Нет доступного поддерживаемого банка',UnsupportedBankAccess:'Маршрут к банку недоступен',InsufficientWorkingCapacity:'Недостаточная рабочая вместимость',BankEstimateBoundExceeded:'Банковский путь превышает предел оценки'};
    if(bankReasons[c.Rejection])value+=` · ${bankReasons[c.Rejection]}`;
    if(c.OriginalTarget!=null)value+=` · промежуточный рубеж вместо ${c.OriginalTarget}; причина: ${c.FallbackReason}. Дальний рубеж ещё не достигнут.`;
    if(f){value+=` · свободно ${f.FreeUnits}, оценочно нужно ${f.RequiredUnits}, дефицит ${f.DeficitUnits}. Банковская политика ${f.BankConfigured?'задана; выполнимость проверяется отдельно':'не задана'}.`;
        if(f.BankConfigured)value+=` Оценено разгрузок: ${f.Unloads??'неизвестно'}.`;
        if(c.Rejection==='EstimatedPathExceedsBudget')value+=` Нужно действий: ${f.RequiredActions}, осталось: ${f.RemainingActions}; оценка времени: ${f.RequiredSeconds} сек, осталось: ${f.RemainingSeconds} сек.`;
        if(c.Rejection==='EstimatedInventoryInsufficient')value+=' Освободите место или проверьте разрешённую банковскую политику, затем повторите Inspect. Другие ограничения могут сохраниться.';
    }else if(!c.Need)value+=' Числовые детали отсутствуют.';
    return value;
}
function diagnostics(id,candidates){
    candidates=candidates??[];
    if(!changed(id,candidates))return;
    const root=el(id);root.replaceChildren();
    if(!candidates?.length){const p=document.createElement('p');p.textContent='Детали кандидатов отсутствуют.';root.append(p);return}
    const ordered=[...candidates].sort((a,b)=>Number(!a.Feasibility)-Number(!b.Feasibility));
    let more;
    ordered.forEach((c,i)=>{if(i===3){more=document.createElement('details');const summary=document.createElement('summary');summary.textContent=`Остальные альтернативы (${ordered.length-3})`;more.append(summary);root.append(more)}const p=document.createElement('p');p.textContent=diagnosticText(c);(i<3?root:more).append(p)});
}
let seriesToken=null,seriesSignature=null;
const seriesLabels={Waiting:'Ожидает следующего запуска',Running:'Серия исполняется',Completed:'Серия завершена',Stopped:'Серия остановлена',InterventionRequired:'Серия требует вмешательства',Disabled:'Расписание выключено',RunCompleted:'Запуск завершён',RunFailed:'Отказ запуска',SeriesCompleted:'Серия завершена',SeriesStopped:'Серия остановлена'};
function seriesReason(reason){return ({SeriesBudgetExhausted:'Исчерпан бюджет серии',SeriesWindowExhausted:'Окончилось окно запуска',InterruptedReservation:'Предыдущий запуск прерван; требуется проверка',StopRequested:'Запрошена остановка',ScheduleStorageOrConfigurationInvalid:'Состояние серии или настройки недоступны либо изменены',RunUnavailableOrInvalid:'Не удалось безопасно выполнить запуск'}[reason]??reasons[reason]??reason)}
async function pollSeries(){try{const response=await fetch('/operator/schedule',{cache:'no-store'});if(!response.ok)throw Error();const v=await response.json();el('schedule').hidden=!v.Enabled;if(v.Enabled){seriesToken=v.Token;const s=v.State;const signature=JSON.stringify(s);if(signature!==seriesSignature){seriesSignature=signature;text('series-state',`${v.SeriesId} · ${seriesLabels[s.Status]??s.Status}`);text('series-budget',`Зарезервировано: ${s.ReservedRuns}/${v.Limits.MaxRuns} запусков · ${s.ReservedActions}/${v.Limits.MaxTotalActions} действий · ${s.ReservedDecisions}/${v.Limits.MaxTotalDecisions} решений · ${s.ReservedSeconds}/${v.Limits.MaxTotalSeconds} сек`);text('series-next',s.Status==='Waiting'?`Следующий запуск: ${new Date(s.NextDue).toLocaleString()} · окончание серии: ${new Date(v.Limits.ExpiresUtc).toLocaleString()}`:`Причина: ${seriesReason(s.Reason)}`);el('series-stop').disabled=['Completed','Stopped','InterventionRequired'].includes(s.Status);el('series-events').replaceChildren();for(const n of s.Notifications){const li=document.createElement('li');li.dataset.id=n.Id;li.textContent=`${new Date(n.At).toLocaleString()} · ${seriesLabels[n.Kind]??n.Kind} · ${n.RunId??v.SeriesId} · ${seriesReason(n.Reason)}`;el('series-events').append(li)}}}}catch{if(!el('schedule').hidden)text('series-result','Не удалось обновить состояние серии.')}finally{setTimeout(pollSeries,3000)}}
el('series-stop').onclick=async()=>{try{const response=await fetch('/operator/schedule/stop',{method:'POST',headers:{'X-Artiact-Control':seriesToken}});text('series-result',response.ok?'Остановка серии запрошена. Дождитесь сохранённого результата.':'Остановка отклонена.')}catch{text('series-result','Результат запроса остановки неизвестен.')}};
setTimeout(pollSeries,0);
const text=(id,value)=>{const next=String(value??'—');if(el(id).textContent!==next)el(id).textContent=next};
const renderedLists=new Map();
function changed(id,value){const signature=JSON.stringify(value);if(renderedLists.get(id)===signature)return false;renderedLists.set(id,signature);return true}
const labels={Running:'Исполняется',Idle:'Нет активного запуска',AwaitingRecovery:'Ожидает восстановления',Completed:'Цель достигнута',Stopped:'Штатно остановлен',Blocked:'Требуется вмешательство',UnknownOutcome:'Результат неизвестен',Cancelled:'Остановлен оператором'};
const facts={name:'Имя',hp:'HP',max_hp:'Максимум HP',level:'Уровень',xp:'XP',max_xp:'XP до уровня',map_id:'Карта',x:'X',y:'Y',inventory_max_items:'Вместимость',mining_level:'Добыча · уровень',mining_xp:'Добыча · XP',woodcutting_level:'Рубка · уровень',woodcutting_xp:'Рубка · XP',fishing_level:'Рыбалка · уровень',fishing_xp:'Рыбалка · XP',alchemy_level:'Алхимия · уровень',alchemy_xp:'Алхимия · XP'};
function list(id,entries){if(!changed(id,entries))return;el(id).replaceChildren();for(const [key,value] of entries){const dt=document.createElement('dt'),dd=document.createElement('dd');dt.textContent=facts[key]??key;dd.textContent=value;el(id).append(dt,dd)}if(!entries.length){const dt=document.createElement('dt');dt.textContent='Данные отсутствуют';el(id).append(dt)}}
const reasons={NoActiveSupportedNeeds:'Нет актуальных поддерживаемых потребностей',AutonomousBudgetExhausted:'Достигнут предел запуска',BudgetExhausted:'Бюджет исчерпан',NoUsefulSupportedGoals:'Поддерживаемые цели исчерпаны',TargetsReached:'Заданные цели достигнуты',CheckpointUnavailableOrInvalid:'Не удалось прочитать или сохранить состояние',ExecutionFailed:'Технический отказ исполнения',Cancelled:'Остановка подтверждена',UnresolvedOutcome:'Результат команды не удалось установить',DispatchOutcomeUnknown:'Ответ на команду не подтверждён',NoFeasibleCandidate:'Нет доступной поддерживаемой цели',ObservationFailed:'Не удалось обновить наблюдение',StaleObservation:'Наблюдение устарело',ResourceBudgetExhausted:'Лимит расхода запасов исчерпан'};
function render(v){const r=v.Run;diagnostics('run-diagnostics',r?.Candidates);currentRun=r;executor=v.Executor;connected=true;updateControls();if(startAccepted&&finishedRun()){text('control-result',savedRunHelp());startAccepted=false}text('connection','Хост доступен');text('executor',labels[v.Executor]??v.Executor);text('reason',(reasons[r?.Reason]??r?.Reason)??(v.Storage==='Unavailable'?'Хранилище недоступно':v.Storage==='NotConfigured'?'Каталог запусков не настроен':(r?'Работа в пределах заданных лимитов':'Запуск ещё не начат')));
text('run-id',r?.RunId);text('freshness',`${{Fresh:'Свежие данные',Stale:'Устаревшие данные',Unavailable:'Свежесть неизвестна'}[v.Freshness]}${v.ObservedAt?' · '+new Date(v.ObservedAt).toLocaleString():''}`);
text('actions',r?`${r.UsedActions} / ${r.MaxActions??'неизвестно'}`:'—');text('time',r?.RemainingSeconds==null?'Неизвестно':Math.floor(r.RemainingSeconds)+' сек');text('cooldown',r?r.VerifiedCooldownSeconds+' сек':'—');text('timing',r?.TimingComplete?'Ожидания подтверждены':'Не все ожидания подтверждены');text('goal',r?.Goal??'Нет активной цели');text('origin',r?.Origin==='Autonomous'?'Автоматически обнаруженная цель':r?'Настроенная цель':'—');text('explanation',r?.Need?r.Need.Cause:r?.Explanation?`Полезность открытия: ${r.Explanation.UnlockUtility}; прогресса: ${r.Explanation.ProgressUtility}`:'Объяснение текущей цели отсутствует');text('last-action',r?.LastConfirmedAction??'Нет подтверждённых действий');text('pending',r?.PendingCommand?'Незавершённая команда: '+r.PendingCommand:'');
list('character',Object.entries(r?.Character??{}));list('resources',Object.entries(r?.ChargedResources??{}));if(changed('milestones',r?.Milestones)){el('milestones').replaceChildren();for(const m of r?.Milestones??[]){const li=document.createElement('li');li.textContent=`${m.Skill} → ${m.Target} · ${m.Outcome}`;el('milestones').append(li)}if(!el('milestones').children.length){const li=document.createElement('li');li.textContent='История отсутствует';el('milestones').append(li)}}if(changed('history',v.History)){el('history').replaceChildren();for(const h of v.History??[]){const row=document.createElement('div');row.textContent=`${h.RunId??'Без RunId'} · ${labels[h.Status]??h.Status} · ${h.UsedActions} действий · ${h.VerifiedCooldownSeconds} сек cooldown`;el('history').append(row)}if(!el('history').children.length)text('history','Архив пуст');}
const notice=v.Failure?'Технический отказ: '+v.Failure.Operation+' / '+v.Failure.ExceptionType+' ('+v.Failure.ErrorCode+'). Требуется проверка сохранённого состояния.':v.Storage==='Unavailable'?'Сохранённое состояние недоступно. Показанные ранее данные сняты.':r?.CanArchive&&r?.Status==='Blocked'?'Запуск остановлен: нет доступной цели. Можно сохранить итог в архив и начать новую сессию.':r?.InterventionRequired?'Продолжение требует проверки состояния и результата команды.':v.StopRequested?'Остановка запрошена. Подтверждение появится после завершения текущего действия.':null;el('notice').hidden=!notice;text('notice',notice)}
let controlToken=null,currentRun=null,executor='Idle',connected=false,busy=false,pollTimer=null,polling=false,startAccepted=false;
async function poll(){
    if(polling)return;
    clearTimeout(pollTimer);polling=true;
    try{const response=await fetch('/operator/snapshot',{cache:'no-store'});if(!response.ok)throw Error();render(await response.json());if(!el('orders-section').hidden)await loadOrders()}
    catch{connected=false;updateControls();text('connection','Связь потеряна');text('freshness','Данные не обновляются');el('notice').hidden=false;text('notice','Хост недоступен. Последние показанные факты могут быть устаревшими.')}
    finally{polling=false;pollTimer=setTimeout(poll,2000)}
}
const messages={InspectReady:'План готов. При запуске он будет автоматически проверен заново.',StartAccepted:'Запуск принят. Закрытие страницы не остановит работу.',AlreadyAccepted:'Этот запуск уже принят.',StopRequested:'Остановка запрошена. Текущее действие должно завершиться; дождитесь подтверждения.',ExecutorIdle:'Исполнитель уже остановлен.',Archived:'Итог сохранён в архиве. Можно запустить новую сессию.',InspectExpired:'Данные изменились или устарели. Нажмите «Запустить» для новой проверки.',InspectRequired:'Не удалось подготовить запуск. Нажмите «Запустить» для новой проверки.',ExecutorBusy:'Персонаж уже занят. Дождитесь завершения или остановите запуск.',StartRefused:'Запуск отклонён. Проверьте разрешения в настройках и сохранённое состояние.',ExistingRunIdentityMismatch:'Есть сохранённый запуск с другими параметрами. Выберите «Продолжить сохранённый запуск» или сохраните завершённый итог в архив.',ArchiveRefused:'Итог нельзя архивировать: он не подтверждён или требует вмешательства.',ProfileUnavailableOrInvalid:'Проверьте лимиты и настройки профиля. Лимит без прогресса не должен превышать число решений.',InspectProfileChanged:'Настройки изменились. Нажмите «Запустить» для новой проверки.',InspectFailed:'Не удалось построить план. Проверьте настройки и состояние персонажа.'};
function archiveHelp(){
    if(!connected)return 'Архив недоступен: нет связи с сервером.';
    if(!currentRun?.IdentityDigest)return 'Нет сохранённого запуска для архива.';
    if(executor==='Running')return 'Архив недоступен, пока запуск выполняется.';
    if(currentRun.CanArchive)return 'Сохранить результат и причину остановки в архив. После этого можно запустить новую сессию.';
    if(currentRun.PendingCommand||currentRun.Status==='UnknownOutcome')return 'Архивировать нельзя: результат игровой команды не подтверждён. Сначала требуется сверка сохранённого состояния.';
    if(currentRun.Status==='Nonterminal')return 'Запуск ещё не завершён. Загрузите его параметры кнопкой «Продолжить сохранённый запуск», затем нажмите «Запустить».';
    return `Архивировать нельзя: ${labels[currentRun.Status]??currentRun.Status}. ${reasons[currentRun.Reason]??currentRun.Reason??'Итог не подтверждён'}. Требуется проверка сохранённого состояния.`;
}
function finishedRun(){return currentRun&&currentRun.Status!=='Nonterminal'&&currentRun.Status!=='UnknownOutcome'}
function savedRunHelp(){return `Сохранённый запуск ${currentRun?.RunId??''}: ${labels[currentRun?.Status]??currentRun?.Status??'состояние неизвестно'}. `+(currentRun?.CanArchive?'Сохраните итог в архив, затем нажмите «Запустить» для новой сессии.':archiveHelp())}
function updateControls(){
    const running=executor==='Running';
    el('start').disabled=busy||!controlToken||!connected||running;
    el('inspect').disabled=busy||!controlToken||!connected||running;
    el('stop').disabled=!controlToken||!connected||!running;
    el('recover').disabled=busy||running||!currentRun?.RunId||!!finishedRun();
    el('archive').disabled=busy||running||!connected||!controlToken||!currentRun?.CanArchive;
    text('archive-help',archiveHelp());
    text('recover-help',finishedRun()?'У этого запуска уже есть итог. Продолжение не выполняет новых действий. '+archiveHelp():'Загрузить прежние параметры. Затем нажмите «Запустить». Потраченные лимиты не обнуляются.');
    el('new-run').disabled=busy||running;
    for(const id of ['input-run','input-actions','input-seconds','input-decisions','input-no-progress'])el(id).disabled=busy||running;
}
function request(){return{RunId:el('input-run').value,MaxActions:Number(el('input-actions').value),MaxSeconds:Number(el('input-seconds').value),MaxDecisions:Number(el('input-decisions').value),MaxNoProgress:Number(el('input-no-progress').value)}}
function fill(p){el('input-run').value=p.RunId??'';el('input-actions').value=p.MaxActions;el('input-seconds').value=p.MaxSeconds;el('input-decisions').value=p.MaxDecisions;el('input-no-progress').value=p.MaxNoProgress;text('control-result','Укажите лимиты и нажмите «Запустить». Проверка выполнится автоматически.');updateControls()}
async function command(path,body){
    const response=await fetch('/operator/'+path,{method:'POST',headers:{'Content-Type':'application/json','X-Artiact-Control':controlToken},body:JSON.stringify(body??{})});
    if(response.status===403){text('control-result','Доступ отклонён. Обновите страницу.');return {Accepted:false}}
    const result=await response.json();text('control-result',['ExistingRunIdentityMismatch','RunFinished'].includes(result.Reason)?savedRunHelp():messages[result.Reason]??reasons[result.Reason]??('Операция отклонена. Код: '+result.Reason));return result;
}
async function guarded(action){
    if(busy)return;
    busy=true;updateControls();el('prepare').setAttribute('aria-busy','true');
    try{await action()}catch{ text('control-result','Запрос не подтверждён. Проверьте состояние запуска перед повторной попыткой.');await poll()}
    finally{busy=false;el('prepare').setAttribute('aria-busy','false');text('start','Запустить');updateControls()}
}
function validRequest(){if(!el('prepare').reportValidity())return false;if(request().MaxNoProgress>request().MaxDecisions){text('control-result','Лимит решений без прогресса не должен превышать общее число решений.');return false}return true}
async function prepare(start){
    if(start&&finishedRun()){text('control-result',savedRunHelp());return}
    if(!validRequest())return;
    await guarded(async()=>{
        text('start',start?'Проверяем и запускаем…':'Запустить');
        text('control-result',start?'Проверяем актуальное состояние персонажа перед запуском…':'Строим план. Игровые действия не выполняются…');
        const result=await command('inspect',request());
        diagnostics('inspect-diagnostics',result.Decision?.Candidates);
        text('inspect-freshness',`Результат Inspect получен ${new Date().toLocaleString()}. Это снимок проверки; он может устареть. Следующий запуск проверит состояние заново.`);
        if(!result.Accepted||!result.Receipt)return;
        if(!start){if(result.Decision)text('control-result',messages.InspectReady+' Цель: '+(result.Decision.Candidate??'—')+'; действие: '+(result.Decision.Command??'—'));return}
        text('control-result','Проверка пройдена. Запускаем…');
        const started=await command('start',{Receipt:result.Receipt});
        if(started.Accepted)startAccepted=true;
        await poll();
        if(started.Accepted&&!finishedRun()){executor='Running';updateControls()}
    });
}
el('prepare').addEventListener('submit',event=>{event.preventDefault();prepare(true)});
el('start').addEventListener('click',()=>prepare(true));
el('inspect').addEventListener('click',()=>prepare(false));
el('stop').addEventListener('click',async()=>{
    if(el('stop').disabled)return;el('stop').disabled=true;text('control-result','Отправляем запрос остановки…');
    try{await command('stop');await poll()}catch{text('control-result','Результат остановки неизвестен. Проверьте связь и состояние запуска.')}finally{updateControls()}
});
el('archive').addEventListener('click',()=>guarded(async()=>{if(!currentRun?.IdentityDigest)return;text('control-result','Сохраняем итог в архив…');const result=await command('archive',{IdentityDigest:currentRun.IdentityDigest});if(result.Accepted)el('input-run').value='run-'+crypto.randomUUID();await poll()}));
el('recover').addEventListener('click',()=>{if(!currentRun?.RunId)return;fill(currentRun);text('control-result','Параметры сохранённого запуска загружены. Нажмите «Запустить»: использованный бюджет сохранится.')});
el('new-run').addEventListener('click',()=>{el('input-run').value='run-'+crypto.randomUUID();text('control-result','Новый идентификатор создан. Если есть завершённый запуск, сначала сохраните его итог в архив.')});
poll();
async function loadControls(){try{const response=await fetch('/operator/controls',{cache:'no-store'});if(!response.ok)return;const value=await response.json();if(!value.Enabled)return;controlToken=value.Token;el('controls').hidden=false;const p=value.Profile;fill({...p.Limits,RunId:p.Limits.RunId||'run-'+crypto.randomUUID()});for(const [id,key] of [['input-actions','MaxActions'],['input-seconds','MaxSeconds'],['input-decisions','MaxDecisions'],['input-no-progress','MaxNoProgress']])el(id).max=p.Limits[key];text('permissions',`Персонаж: ${p.Character}. Игровые действия: ${p.AllowActions?'разрешены настройками':'выключены'}. Профиль: ${p.Needs?'цели от потребностей':p.AutonomousGoals?'автоматические цели':'заданные цели'}.`);text('profile',JSON.stringify(p,null,2));if(p.Needs){el('orders-section').hidden=false;loadOrders()}}catch{text('control-result','Не удалось загрузить управление.')}}loadControls();
