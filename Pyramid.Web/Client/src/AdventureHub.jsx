import React,{useEffect,useState} from 'react';
import {request} from './api';
import './adventure-hub.css';

export default function AdventureHub({onPlay,onManage,onDesign,onBusyChange}){
 const [catalog,setCatalog]=useState(null),[error,setError]=useState(''),[busy,setBusy]=useState(false),[creating,setCreating]=useState(null),[name,setName]=useState('');
 useEffect(()=>{onBusyChange(busy);return()=>onBusyChange(false);},[busy,onBusyChange]);
 useEffect(()=>{let active=true;request('expeditions').then(data=>{if(active)setCatalog(data);}).catch(e=>{if(active)setError(e.message);});return()=>{active=false;};},[]);
 async function resume(id){if(busy)return;setBusy(true);setError('');try{await request('expeditions/action',{id,action:id?'resume':'solo',target:''});await onPlay();}catch(e){setError(e.message);}finally{setBusy(false);}}
 async function manage(){if(busy)return;setBusy(true);setError('');try{await onManage();}catch(e){setError(e.message);}finally{setBusy(false);}}
 async function create(e){e.preventDefault();if(busy)return;setBusy(true);setError('');let created=false;try{await request('expeditions/create',{name:name.trim(),pack:creating.id,open:false});created=true;setCreating(null);await onPlay();}catch(e){setError(created?'Your expedition was saved, but could not be opened. Resume it from Your journeys, or reload this page.':e.message);if(created)try{setCatalog(await request('expeditions'));}catch{}}finally{setBusy(false);}}
 const packName=id=>catalog?.packs.find(p=>p.id===id)?.name||id;
 return <main className="adventure-hub"><header className="hub-title"><h1>Choose your adventure</h1><p>Continue a saved journey or begin somewhere new.</p></header>
 {error&&<p role="alert" className="error">{error} {!catalog&&<button onClick={()=>request('expeditions').then(setCatalog).catch(e=>setError(e.message))}>RETRY</button>}</p>}
 <section aria-labelledby="saved-journeys"><h2 id="saved-journeys">Your journeys</h2><div className="hub-saves">
 <article className="hub-save"><div><h3>Original Pyramid journey</h3><p>Chamber of the Gods · Personal save</p></div><button disabled={busy||!catalog} onClick={()=>resume('')}>RESUME</button></article>
 {!catalog&&!error&&<p role="status">Finding your expeditions…</p>}
 {catalog?.games.filter(g=>g.member).map(g=><article className="hub-save" key={g.id}><div><h3>{g.name}</h3><p>{packName(g.pack)} · {g.count} {g.count===1?'explorer':'explorers'}</p></div><button disabled={busy} onClick={()=>resume(g.id)} aria-label={`Resume ${g.name}`}>RESUME</button></article>)}
 </div></section>
 <section aria-labelledby="new-adventure"><h2 id="new-adventure">Start a new adventure</h2><div className="hub-cards">{catalog?.packs.map(pack=><article className="hub-card" key={pack.id}>
 <img src={pack.id==='oasis-of-stars'?'adventure-art/oasis.png':pack.id==='pyramid'?'adventure-art/pyramid.png':'room-art/room10.png'} alt={pack.id==='oasis-of-stars'?'An oasis beneath a crescent moon':'An ancient stone gateway'} width="900" height="300"/>
 <div><h3>{pack.name}</h3><p className="hub-meta">{pack.levels} realms · {pack.rooms} rooms</p><p>{pack.summary}</p><button disabled={busy} onClick={()=>{setCreating(pack);setName(`My ${pack.name}`.slice(0,60));}}>NEW EXPEDITION</button></div></article>)}</div></section>
 {creating&&<section className="hub-setup" aria-labelledby="setup-title"><h2 id="setup-title">Begin {creating.name}</h2><form onSubmit={create}><label>Expedition name<input autoFocus value={name} onChange={e=>setName(e.target.value)} maxLength={60} required disabled={busy}/></label><p>Your existing journeys stay saved. Start alone and invite companions later.</p><div><button className="primary" disabled={busy||!name.trim()}>BEGIN ADVENTURE</button><button type="button" disabled={busy} onClick={()=>setCreating(null)}>CANCEL</button></div></form></section>}
 <nav className="hub-links" aria-label="Adventure tools"><button disabled={busy} onClick={manage}>INVITATIONS & COMPANIONS{catalog?.games.some(g=>g.invited)?' · Invitation waiting':''}</button><button disabled={busy} onClick={onDesign}>CREATE AN ADVENTURE</button><a href="GAMEPLAY-API-MODS.txt" target="_blank" rel="noreferrer">Player, API & Mod Guide</a></nav>
 </main>;
}
