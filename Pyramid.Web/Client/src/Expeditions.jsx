import React,{useState,useEffect,useRef} from 'react';
import {request} from './api';
import './expeditions-select.css';

function InvitePicker({prefix,explorers,disabled,optional=false}) {
  const [choice,setChoice]=useState('');
  return <div className="invite-picker">
    <label>{optional?'Invite an explorer (optional)':'Invite an explorer'}
      <select name={`${prefix}-choice`} value={choice} onChange={e=>setChoice(e.target.value)} disabled={disabled} required={!optional}>
        <option value="">{optional?'Start exploring on my own':'Choose an explorer'}</option>
        {explorers.map(p=><option key={p.id} value={p.name}>{p.name} — online</option>)}
        {choice&&choice!==':manual'&&!explorers.some(p=>p.name===choice)&&<option value={choice}>{choice} — offline</option>}
        <option value=":manual">Enter an explorer name…</option>
      </select>
    </label>
    {choice===':manual'&&<label>Explorer name (online or offline)<input name={`${prefix}-name`} autoComplete="off" required minLength={3} maxLength={24} pattern="[a-zA-Z0-9_-]+" disabled={disabled} placeholder="Exact explorer name"/></label>}
    {!explorers.length&&<p className="muted invite-note">No other explorers are online. You can still invite an existing account by name.</p>}
  </div>;
}
function invitationTarget(form,prefix) {
  const choice=form.get(`${prefix}-choice`);
  return choice===':manual'?String(form.get(`${prefix}-name`)||'').trim():String(choice||'');
}

export default function Expeditions({party,user,onClose,onChange,initialId=''}) {
  const ref=useRef(),working=useRef(false),refreshVersion=useRef(0);
  const [lobby,setLobby]=useState(null),[explorers,setExplorers]=useState([]),[selectedId,setSelectedId]=useState(initialId);
  const [error,setError]=useState(''),[notice,setNotice]=useState(''),[busy,setBusy]=useState(false);
  const [chosenPack,setChosenPack]=useState('pyramid');
  async function refresh() {
    const version=++refreshVersion.current;
    const [worlds,people]=await Promise.all([request('expeditions'),request('explorers')]);
    if(version!==refreshVersion.current)return;
    setLobby(worlds);setExplorers(people.filter(p=>p.online&&p.id!==user.id));
    setSelectedId(old=>worlds.games.some(g=>g.id===old)?old:worlds.games.some(g=>g.id===worlds.active)?worlds.active:worlds.games[0]?.id||'');
  }
  useEffect(()=>{
    ref.current.showModal();let active=true;
    const update=()=>refresh().catch(e=>{if(active)setError(e.message);});
    update();const timer=setInterval(()=>{if(!working.current)update();},10000);
    return()=>{active=false;refreshVersion.current++;clearInterval(timer);};
  },[]);
  async function action(id,verb,target='') {
    if(working.current)return;
    working.current=true;refreshVersion.current++;setBusy(true);setError('');setNotice('');
    try {await request('expeditions/action',{id,action:verb,target});await onChange();await refresh();if(verb==='invite')setNotice(`Invitation sent to ${target}. You can keep exploring while they decide.`);}
    catch(e){setError(e.message);}finally{working.current=false;setBusy(false);}
  }
  async function create(event) {
    event.preventDefault();if(working.current)return;
    const form=new FormData(event.currentTarget),target=invitationTarget(form,'setup');
    working.current=true;refreshVersion.current++;setBusy(true);setError('');setNotice('');
    let created=false;
    try {
      const result=await request('expeditions/create',{name:form.get('name'),pack:form.get('pack'),open:form.get('open')==='on'});
      created=true;setSelectedId(result.id);
      // Creation activates the host's save immediately; an invitation never gates entry.
      await onChange();setNotice('Your expedition is ready. Close this panel to begin exploring.');
      if(target){
        try{await request('expeditions/action',{id:result.id,action:'invite',target});setNotice(`Your expedition is ready. ${target} has been invited; you can explore while they decide.`);}
        catch(e){setError(`Expedition created and ready to play, but the invitation was not sent: ${e.message} You can retry from the current expedition below.`);}
      }
      await refresh();
    } catch(e){setError(created?`Your expedition was created, but this panel could not refresh: ${e.message} Close and reopen Expeditions to resume it.`:e.message);}
    finally{working.current=false;setBusy(false);}
  }
  const selected=lobby?.games.find(g=>g.id===selectedId);
  return <dialog ref={ref} onCancel={onClose} className="journal expeditions" aria-labelledby="expeditions-title" aria-busy={busy}>
    <div className="rail-heading"><h2 id="expeditions-title">Adventures & expeditions</h2><button onClick={onClose} autoFocus aria-label="Close expeditions">CLOSE</button></div>
    {initialId&&lobby&&!lobby.games.some(g=>g.id===initialId)&&<p className="expedition-notice" role="status">This invitation is unavailable for this account. It may have expired or been withdrawn. Sign in with the invited explorer account.</p>}<p>Keep your original solo journey, or host a new world for up to eight explorers. Open expeditions still require the host's approval.</p>
    {error&&<p className="error" role="alert">{error}</p>}{notice&&<p className="expedition-notice" role="status">{notice}</p>}
    <button disabled={busy||!lobby?.active} onClick={()=>action('','solo')}>RETURN TO ORIGINAL SOLO SAVE</button>
    <section><h3>Choose an expedition</h3>
      {!lobby?<p role="status">Searching for expeditions…</p>:lobby.games.length===0?<p className="muted">No shared worlds yet. Create one below, or ask an explorer to invite you.</p>:<>
        <label>Saved worlds, invitations & open expeditions<select value={selectedId} onChange={e=>setSelectedId(e.target.value)} disabled={busy}>{lobby.games.map(g=><option value={g.id} key={g.id}>{g.name} — {g.member?'saved world':g.invited?'invitation':g.requested?'awaiting approval':'open expedition'}</option>)}</select></label>
        {selected&&<div className="world-row"><strong>{selected.name}</strong><p>{selected.owner} · {selected.count}/8 explorers · {selected.pack}</p><div className="expedition-actions">
          {selected.member?<button disabled={busy||lobby.active===selected.id} onClick={()=>action(selected.id,'resume')}>{lobby.active===selected.id?'EXPLORING':'RESUME EXPEDITION'}</button>:selected.invited?<><button disabled={busy||selected.count>=8} onClick={()=>action(selected.id,'accept')}>ACCEPT & JOIN</button><button disabled={busy} onClick={()=>action(selected.id,'decline')}>DECLINE INVITATION</button></>:<button disabled={busy||selected.requested||selected.count>=8} onClick={()=>action(selected.id,'request')}>{selected.requested?'AWAITING HOST':selected.count>=8?'EXPEDITION FULL':'REQUEST TO JOIN'}</button>}
        </div></div>}
      </>}
    </section>
    {party&&<section><h3>{party.name}</h3><p>Current expedition · {party.members.length}/8 explorers</p>{party.owner===user.id&&<>
      <button disabled={busy} onClick={()=>action(party.id,party.open?'close':'open')}>{party.open?'CLOSE JOIN REQUESTS':'OPEN JOIN REQUESTS'}</button>
      <form onSubmit={e=>{e.preventDefault();action(party.id,'invite',invitationTarget(new FormData(e.currentTarget),'existing'));}}><InvitePicker prefix="existing" explorers={explorers} disabled={busy}/><button disabled={busy}>SEND INVITATION</button></form>
      {Object.entries(party.requests||{}).map(([id,name])=><div className="join-request" key={id}><p>{name} requests to join.</p><div className="expedition-actions"><button disabled={busy} onClick={()=>action(party.id,'approve',id)}>APPROVE</button><button disabled={busy} onClick={()=>action(party.id,'deny',id)}>DENY</button></div></div>)}
      {Object.entries(party.invitations||{}).map(([id,name])=><p key={id}>Invitation pending: {name} <button disabled={busy} onClick={()=>action(party.id,'revoke',id)}>REVOKE</button></p>)}
    </>}</section>}
    <form onSubmit={create}><h3>Choose your next adventure</h3><p>Each new expedition has its own saved progress. Play alone, or invite companions whenever you are ready.</p><fieldset className="adventure-choices"><legend>Adventure</legend>{lobby?.packs.map(p=><label className={`adventure-choice ${chosenPack===p.id?'chosen':''}`} key={p.id}><input type="radio" name="pack" value={p.id} checked={chosenPack===p.id} onChange={()=>setChosenPack(p.id)} disabled={busy}/><span><strong>{p.name}</strong><small>{p.levels} realms · {p.rooms} rooms</small><span>{p.summary}</span></span></label>)}</fieldset><label>Expedition name<input name="name" required maxLength={60} defaultValue="My expedition" disabled={busy}/></label><InvitePicker prefix="setup" explorers={explorers} disabled={busy} optional/><label className="check-label"><input type="checkbox" name="open" disabled={busy}/> Accept join requests</label><p className="muted invite-note">You can begin immediately. Companions can join later, and your original solo save stays available. Resume a saved world above to continue instead of starting again.</p><button disabled={busy||!lobby||!lobby.packs.some(p=>p.id===chosenPack)} className="primary">START ADVENTURE</button></form>
  </dialog>;
}
