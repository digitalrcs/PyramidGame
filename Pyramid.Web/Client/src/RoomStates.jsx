import {useState} from 'react';
const keys=['requires','absentFlags','presentItems','absentItems'];
const object=v=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const strings=v=>Array.isArray(v)&&v.every(x=>typeof x==='string');
const prose=v=>typeof v==='string'&&v.length>0&&v.length<=2000&&!/[\x00-\x09\x0b-\x1f\x7f-\x9f]/.test(v);
const item=v=>typeof v==='string'&&/^[a-z][a-z0-9 '-]{0,63}$/.test(v);
export const safeRoomStates=states=>states===undefined||(Array.isArray(states)&&states.length<=40&&states.every(s=>object(s)&&typeof s.description==='string'&&keys.every(k=>s[k]===undefined||strings(s[k]))&&(s.features===undefined||(object(s.features)&&Object.values(s.features).every(v=>typeof v==='string')))));
export function validateRoomStates(states,flags,label){
 if(states===undefined)return [];
 if(!safeRoomStates(states))return [`${label}: changing descriptions must be up to 40 rules with text and condition lists.`];
 const errors=[];
 states.forEach((s,i)=>{
  const at=`${label}, description ${i+1}`;
  if(Object.keys(s).some(k=>![...keys,'description','features'].includes(k)))errors.push(`${at}: unsupported field.`);
  if(!prose(s.description))errors.push(`${at}: add replacement text (up to 2000 characters).`);
  for(const k of keys)if((s[k]||[]).length>(k.includes('Items')?40:100)||!(s[k]||[]).every(k.includes('Items')?item:x=>typeof x==='string'&&flags.has(x)))errors.push(`${at}: ${k} must contain valid ${k.includes('Items')?'item names':'puzzle flags from this realm'}.`);
  if((s.requires||[]).some(f=>(s.absentFlags||[]).includes(f))||(s.presentItems||[]).some(f=>(s.absentItems||[]).includes(f)))errors.push(`${at}: the same condition cannot be both present and absent.`);
  if(s.features!==undefined&&(!object(s.features)||Object.keys(s.features).length>40||!Object.entries(s.features).every(([k,v])=>item(k)&&prose(v))))errors.push(`${at}: EXAMINE overrides need valid names and nonempty text.`);
 });return errors;
}
export function previewRoom(room,flags,items){
 let description=room.description,features={...room.features};
 for(const s of room.descriptionStates||[])if((s.requires||[]).every(f=>flags.includes(f))&&(s.absentFlags||[]).every(f=>!flags.includes(f))&&(s.presentItems||[]).every(x=>items.includes(x))&&(s.absentItems||[]).every(x=>!items.includes(x))){description=s.description;Object.assign(features,s.features);}
 return {description,features};
}
const split=v=>v.split(',').map(x=>x.trim()).filter(Boolean);
export default function RoomStates({room,level,editRoom}){
 const [flags,setFlags]=useState([]),[items,setItems]=useState(room.items.join(', '));
 const rules=room.descriptionStates||[],preview=previewRoom(room,flags,split(items));
 const update=(i,fn)=>editRoom(r=>fn(r.descriptionStates[i]));
 return <section aria-label="Changing room descriptions"><h3>Changing room descriptions</h3>
 <p className="designer-muted">All conditions in a rule must match. Rules are checked from top to bottom; the last matching description wins. Item conditions check loose objects in this room, including dropped items. Puzzle conditions use completion flags. Opened paths and solved puzzles trigger change chimes automatically.</p>
 {rules.map((rule,i)=><fieldset className="designer-card" key={i}><legend>Room state {i+1}</legend>
 <div className="designer-two">{[['requires','Puzzles completed'],['absentFlags','Puzzles not completed']].map(([key,label])=><div key={key}><p>{label}</p>{level.puzzles.length?level.puzzles.map((p,n)=><label className="designer-check" key={n}><input type="checkbox" checked={(rule[key]||[]).includes(p.flag)} onChange={e=>update(i,s=>{s[key]=e.target.checked?[...new Set([...(s[key]||[]),p.flag])]:(s[key]||[]).filter(f=>f!==p.flag);})}/>{p.flag||'(unnamed puzzle)'}</label>):<p className="designer-muted">Add a puzzle to select its completion flag.</p>}</div>)}</div>
 {['presentItems','absentItems'].map(key=><label className="designer-field" key={key}><span>{key==='presentItems'?'Items in room':'Items absent from room'} (comma separated)</span><input value={(rule[key]||[]).join(', ')} onChange={e=>update(i,s=>{s[key]=e.target.value.split(',').map(x=>x.trimStart());})} onBlur={()=>update(i,s=>{s[key]=split((s[key]||[]).join(','));})}/></label>)}
 <label className="designer-field"><span>Room state {i+1} description</span><textarea rows={3} maxLength={2000} value={rule.description} onChange={e=>update(i,s=>{s.description=e.target.value;})}/></label>
 <p className="designer-muted">Optional EXAMINE text replaces the selected feature only while this rule matches. Leave blank to retain its normal clue.</p>
 {Object.keys({...room.features,...rule.features}).map(name=><label className="designer-field" key={name}><span>State {i+1}: EXAMINE {name}</span><textarea maxLength={2000} value={rule.features?.[name]||''} onChange={e=>update(i,s=>{s.features||={};if(e.target.value)s.features[name]=e.target.value;else delete s.features[name];})}/></label>)}
 <div className="designer-actions"><button type="button" disabled={i===0} onClick={()=>editRoom(r=>{[r.descriptionStates[i-1],r.descriptionStates[i]]=[r.descriptionStates[i],r.descriptionStates[i-1]];})}>MOVE UP</button><button type="button" disabled={i===rules.length-1} onClick={()=>editRoom(r=>{[r.descriptionStates[i+1],r.descriptionStates[i]]=[r.descriptionStates[i],r.descriptionStates[i+1]];})}>MOVE DOWN</button><button type="button" onClick={()=>editRoom(r=>{r.descriptionStates.splice(i,1);})}>REMOVE STATE {i+1}</button></div>
 </fieldset>)}
 <button type="button" disabled={rules.length>=40} onClick={()=>editRoom(r=>{r.descriptionStates||=[];r.descriptionStates.push({requires:[],absentFlags:[],presentItems:[],absentItems:[],description:'Describe this room after the change.',features:{}});})}>+ ADD ROOM STATE</button>
 <details className="designer-card"><summary>Preview room changes</summary><p className="designer-muted">This preview does not change your adventure or saved game. Select solved puzzles and enter items currently in the room to check your writing.</p>
 {level.puzzles.map((p,i)=><label className="designer-check" key={i}><input type="checkbox" checked={flags.includes(p.flag)} onChange={e=>setFlags(e.target.checked?[...flags,p.flag]:flags.filter(f=>f!==p.flag))}/>{p.flag}</label>)}
 <label className="designer-field"><span>Preview items in room</span><input value={items} onChange={e=>setItems(e.target.value)}/></label>
 <div className="designer-message" aria-live="polite"><strong>{room.name}</strong><p>{preview.description}</p><p>{split(items).length?`You see: ${split(items).join(', ')}.`:'There are no loose objects here.'}</p>{Object.entries(preview.features).map(([name,text])=><p key={name}>EXAMINE {name}: {text}</p>)}</div></details>
 </section>;
}
