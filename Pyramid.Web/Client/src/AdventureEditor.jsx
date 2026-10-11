import { useEffect, useState } from 'react';
import './adventure-editor.css';
import RoomStates, { safeRoomStates, validateRoomStates } from './RoomStates.jsx';

const KEY = 'pyramid-adventure-designer-v1';
const directions = ['north', 'south', 'east', 'west', 'up', 'down'];
const reserved = new Set('help ? look l inventory i journal note save retreat go n s e w u d north south east west up down get take drop read examine x hint share offers map clear'.split(' '));
const identifier = value => typeof value === 'string' && /^[a-z][a-z0-9-]{0,47}$/.test(value);
const itemName = value => typeof value === 'string' && /^[a-z][a-z0-9 '-]{0,63}$/.test(value);
const prose = (value, limit = 2000) => typeof value === 'string' && value.length > 0 && value.length <= limit && !/[\x00-\x09\x0b-\x1f\x7f-\x9f]/.test(value);
const object = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const list = text => text.split(',').map(value => value.trim()).filter(Boolean);
const blankRoom = name => ({ name, description: 'Describe what the explorer sees, and leave a clue about what to try.', lit: true, exits: {}, requires: {}, items: [], features: {} });
const starter = () => ({ schemaVersion: 1, id: 'my-adventure', name: 'The Silent Sanctuary', startLevel: 1, levels: [{ number: 1, name: 'The Stone Gate', objective: 'Wake the sanctuary by ringing its ancient bell.', start: 'entrance', rooms: { entrance: { ...blankRoom('Sanctuary entrance'), description: 'An ancient bell hangs beside a sealed stone gate. An inscription invites you to RING BELL.', features: { inscription: 'The bell asks for no offering. RING BELL to wake the sanctuary.' }, image: 'room-art/room1.png' } }, puzzles: [{ room: 'entrance', command: 'ring bell', needs: [], requires: [], flag: 'bell-rung', response: 'The bell sounds. Golden light pours through the opening gate.', hints: ['EXAMINE INSCRIPTION.', 'The bell needs no tool.', 'RING BELL.'] }], winFlags: ['bell-rung'] }], portals: [] });

// This mirrors the installed content schema. Imports are data only; no markup or scripts are evaluated.
export function validateAdventure(pack) {
  const errors = [];
  const check = (ok, message) => { if (!ok) errors.push(message); };
  const fields = (value, allowed, label) => {
    if (!object(value)) { errors.push(`${label} must be an object.`); return false; }
    for (const key of Object.keys(value)) check(allowed.includes(key), `${label}: unsupported field ${key}.`);
    return true;
  };
  if (!fields(pack, ['schemaVersion', 'id', 'name', 'startLevel', 'levels', 'portals', 'readableItems'], 'Adventure')) return errors;
  check(pack.readableItems === undefined || (object(pack.readableItems) && Object.keys(pack.readableItems).length <= 100 && Object.entries(pack.readableItems).every(([name, text]) => itemName(name) && prose(text))), 'Readable items: use up to 100 valid item names with text of 1–2000 characters.');
  check(pack.schemaVersion === 1, 'Use schemaVersion 1. This designer imports complete adventures, not add-on files.');
  check(identifier(pack.id) && pack.id !== 'pyramid', 'Adventure ID: 1–48 lowercase letters, digits or hyphens; begin with a letter. “pyramid” is reserved.');
  check(prose(pack.name, 100), 'Adventure name must contain 1–100 characters.');
  if (!Array.isArray(pack.levels) || pack.levels.length < 1 || pack.levels.length > 100) return [...errors, 'Include 1–100 realms.'];
  check(new Set(pack.levels.map(l => l?.number)).size === pack.levels.length, 'Realm numbers must be unique.');
  check(pack.levels.some(l => l?.number === pack.startLevel), 'Select an existing starting realm.');
  const allItems = new Set(['lamp', 'bottle']);
  for (const level of pack.levels) if (object(level?.rooms)) for (const room of Object.values(level.rooms)) if (Array.isArray(room?.items)) room.items.forEach(item => allItems.add(item));
  if(object(pack.readableItems)) for(const name of Object.keys(pack.readableItems)) check(allItems.has(name), `Readable item ${name} must exist in the adventure.`);
  for (const level of pack.levels) {
    if (!fields(level, ['number', 'name', 'objective', 'start', 'rooms', 'puzzles', 'winFlags'], 'Realm')) continue;
    const label = `Realm ${level.number}`;
    check(Number.isInteger(level.number) && level.number > 0 && level.number <= 2147483647, `${label}: use a positive whole number.`);
    check(prose(level.name, 100) && prose(level.objective), `${label}: add a name (up to 100 characters) and objective (up to 2000).`);
    if (!object(level.rooms) || Object.keys(level.rooms).length < 1 || Object.keys(level.rooms).length > 100) { errors.push(`${label}: include 1–100 rooms.`); continue; }
    check(typeof level.start === 'string' && Object.hasOwn(level.rooms, level.start) && level.rooms[level.start]?.lit !== false, `${label}: the starting room must exist and be lit.`);
    if (!Array.isArray(level.puzzles) || !Array.isArray(level.winFlags)) { errors.push(`${label}: puzzles and winFlags must be arrays.`); continue; }
    const flags = new Set(level.puzzles.map(p => p?.flag));
    check(level.puzzles.length <= 100 && flags.size === level.puzzles.length, `${label}: use at most 100 puzzles with distinct flags.`);
    check(level.winFlags.every(flag => flags.has(flag)), `${label}: completion flags must refer to this realm’s puzzles.`);
    for (const [id, room] of Object.entries(level.rooms)) {
      const at = `${label}, room ${id}`;
      if (!fields(room, ['name', 'description', 'lit', 'exits', 'requires', 'items', 'features', 'image', 'descriptionStates', 'exitKinds'], at)) continue;
      errors.push(...validateRoomStates(room.descriptionStates, flags, at));
      check(identifier(id), `${at}: invalid room ID.`);
      check(prose(room.name, 100) && prose(room.description), `${at}: add a name (up to 100 characters) and description (up to 2000).`);
      check(room.lit === undefined || typeof room.lit === 'boolean', `${at}: lit must be true or false.`);
      check(object(room.exits), `${at}: exits must be an object.`);
      if (object(room.exits)) for (const [direction, target] of Object.entries(room.exits)) check(directions.includes(direction) && typeof target === 'string' && Object.hasOwn(level.rooms, target), `${at}: ${direction} must lead to a room in this realm.`);
      check(room.exitKinds === undefined || object(room.exitKinds), `${at}: exit types must be an object.`);
      if (object(room.exitKinds)) for (const [direction, kind] of Object.entries(room.exitKinds)) check(Object.hasOwn(room.exits || {}, direction) && ['passage','stairs','ladder','hole','bridge'].includes(kind), `${at}: exit type needs an existing exit and a supported type.`);
      check(room.requires === undefined || object(room.requires), `${at}: exit requirements must be an object.`);
      if (object(room.requires)) for (const [direction, flag] of Object.entries(room.requires)) check(Object.hasOwn(room.exits || {}, direction) && flags.has(flag), `${at}: ${direction} lock needs an exit and a known puzzle flag.`);
      check(Array.isArray(room.items) && room.items.length <= 40 && room.items.every(itemName), `${at}: up to 40 item names, each up to 64 lowercase letters, digits, spaces, apostrophes or hyphens.`);
      check(object(room.features) && Object.keys(room.features).length <= 40 && Object.entries(room.features).every(([name, text]) => itemName(name) && prose(text)), `${at}: features need valid item-style names and clue text.`);
      const customImage = typeof room.image === 'string' && room.image.startsWith(`mod-art/${pack.id}/`) && /^[a-z][a-z0-9-]{0,63}\.png$/.test(room.image.slice(`mod-art/${pack.id}/`.length));
      check(room.image == null || /^room-art\/room([1-9]|[1-5][0-9]|60)\.png$/.test(room.image) || customImage, `${at}: image must be room-art/room1.png through room60.png, or mod-art/${pack.id}/name.png.`);
    }
    const commands = new Set();
    for (const puzzle of level.puzzles) {
      if (!fields(puzzle, ['room', 'command', 'needs', 'requires', 'flag', 'response', 'hints'], `${label} puzzle`)) continue;
      const at = `${label}, puzzle ${puzzle.flag || '(unnamed)'}`;
      check(typeof puzzle.room === 'string' && Object.hasOwn(level.rooms, puzzle.room), `${at}: select an existing room.`);
      check(identifier(puzzle.flag), `${at}: use a valid completion flag ID.`);
      check(itemName(puzzle.command) && puzzle.command === puzzle.command.trim().replace(/ +/g, ' ') && !reserved.has(puzzle.command.split(' ')[0]) && !['light lamp', 'off lamp'].includes(puzzle.command), `${at}: command must be lowercase, up to 64 characters, and must not replace a built-in command such as GET, LOOK or GO.`);
      const key = `${puzzle.room}:${puzzle.command}`;
      check(!commands.has(key), `${at}: command already exists in this room.`); commands.add(key);
      check(Array.isArray(puzzle.needs) && puzzle.needs.every(name => itemName(name) && allItems.has(name)), `${at}: required items must exist in a room or be the starting lamp/bottle.`);
      check(Array.isArray(puzzle.requires) && puzzle.requires.every(flag => flags.has(flag) && flag !== puzzle.flag), `${at}: prerequisites must name other puzzle flags in this realm.`);
      check(prose(puzzle.response), `${at}: add a success response (up to 2000 characters).`);
      check(Array.isArray(puzzle.hints) && puzzle.hints.length > 0 && puzzle.hints.length <= 5 && puzzle.hints.every(h => prose(h)), `${at}: include 1–5 nonempty hints, up to 2000 characters each.`);
    }
  }
  if (!Array.isArray(pack.portals) || pack.portals.length > 200) errors.push('Portals must be an array of up to 200 links.');
  else {
    const used = new Set();
    for (const portal of pack.portals) {
      if (!fields(portal, ['fromLevel', 'fromRoom', 'direction', 'toLevel', 'toRoom', 'requires', 'description'], 'Portal')) continue;
      const from = pack.levels.find(l => l?.number === portal.fromLevel), to = pack.levels.find(l => l?.number === portal.toLevel);
      const key = `${portal.fromLevel}:${portal.fromRoom}:${portal.direction}`;
      check(!used.has(key), `Portal ${key}: another portal uses this exit.`); used.add(key);
      check(from && to && object(from.rooms) && object(to.rooms) && typeof portal.fromRoom === 'string' && typeof portal.toRoom === 'string' && Object.hasOwn(from.rooms, portal.fromRoom) && Object.hasOwn(to.rooms, portal.toRoom) && directions.includes(portal.direction), `Portal ${key}: select valid realms, rooms and direction.`);
      check(!from?.rooms?.[portal.fromRoom]?.exits?.[portal.direction], `Portal ${key}: conflicts with a room exit.`);
      check(portal.requires == null || (Array.isArray(from?.puzzles) && from.puzzles.some(p => p?.flag === portal.requires)), `Portal ${key}: lock must use a source realm puzzle flag.`);
      check(prose(portal.description), `Portal ${key}: add a description.`);
    }
  }
  if (new TextEncoder().encode(JSON.stringify(pack, null, 2) + '\n').length > 1024 * 1024) errors.push('Adventure must be smaller than 1 MB.');
  return errors;
}

const editableDraft = pack => {
  const strings = value => Array.isArray(value) && value.every(v => typeof v === 'string');
  const texts = value => object(value) && Object.values(value).every(v => typeof v === 'string');
  if(pack?.readableItems!==undefined&&!texts(pack.readableItems))return false;
  if (Array.isArray(pack?.levels) && pack.levels.some(l => object(l?.rooms) && Object.values(l.rooms).some(r => !safeRoomStates(r?.descriptionStates)))) return false;
  if (!object(pack) || typeof pack.id !== 'string' || typeof pack.name !== 'string' || !Array.isArray(pack.levels) || !pack.levels.length || pack.levels.length > 100 || !Array.isArray(pack.portals)) return false;
  if (!pack.levels.every(l => object(l) && Number.isInteger(l.number) && typeof l.name === 'string' && typeof l.objective === 'string' && typeof l.start === 'string' && object(l.rooms) && Object.hasOwn(l.rooms, l.start) && Object.keys(l.rooms).length <= 100 && Array.isArray(l.puzzles) && strings(l.winFlags))) return false;
  if (!pack.levels.every(l => Object.values(l.rooms).every(r => object(r) && typeof r.name === 'string' && typeof r.description === 'string' && texts(r.exits) && (r.requires === undefined || texts(r.requires)) && (r.exitKinds === undefined || texts(r.exitKinds)) && strings(r.items) && texts(r.features)) && l.puzzles.every(p => object(p) && ['room', 'command', 'flag', 'response'].every(key => typeof p[key] === 'string') && strings(p.needs) && strings(p.requires) && strings(p.hints)))) return false;
  return pack.portals.every(p => object(p) && typeof p.description === 'string' && typeof p.direction === 'string' && typeof p.fromRoom === 'string' && typeof p.toRoom === 'string' && pack.levels.some(l => l.number === p.fromLevel && Object.hasOwn(l.rooms, p.fromRoom)) && pack.levels.some(l => l.number === p.toLevel && Object.hasOwn(l.rooms, p.toRoom)));
};

function loadDraft(key) {
  try {
    const saved = JSON.parse(localStorage.getItem(key));
    // Restore unfinished form drafts only when their shape is safe for the guided editor.
    if (editableDraft(saved)) return saved;
  } catch { /* A private-mode or corrupt draft should not prevent opening the editor. */ }
  return starter();
}

const Field = ({ label, children, hint }) => <label className="designer-field"><span>{label}</span>{children}{hint && <small>{hint}</small>}</label>;

export default function AdventureEditor({ onClose, userId = 'local' }) {
  const draftKey = `${KEY}:${userId}`;
  const [pack, setPack] = useState(() => loadDraft(draftKey));
  const [realmIndex, setRealmIndex] = useState(0);
  const [roomId, setRoomId] = useState(() => pack.levels[0].start);
  const [tab, setTab] = useState('rooms');
  const [message, setMessage] = useState('');
  const [storageError, setStorageError] = useState('');
  const [raw, setRaw] = useState('');
  const [roomName, setRoomName] = useState('');
  const [featureName, setFeatureName] = useState('');
  const errors = validateAdventure(pack);
  const level = pack.levels[realmIndex] || pack.levels[0];
  const room = level.rooms[roomId] || level.rooms[level.start];
  const selectedRoom = Object.hasOwn(level.rooms, roomId) ? roomId : level.start;
  useEffect(() => {
    try { localStorage.setItem(draftKey, JSON.stringify(pack)); setStorageError(''); }
    catch { setStorageError('Browser storage is unavailable or full. Export JSON to keep your work.'); }
  }, [pack, draftKey]);
  useEffect(() => {
    if (!storageError) return;
    const warn = event => { event.preventDefault(); event.returnValue = ''; };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [storageError]);
  function close() {
    if (!storageError || window.confirm('Browser storage is unavailable. Unsaved changes will be lost. Leave the designer?')) onClose();
  }
  function edit(action) { setPack(previous => { const next = structuredClone(previous); action(next); return next; }); setMessage(''); }
  const editLevel = action => edit(next => action(next.levels[realmIndex]));
  const editRoom = action => editLevel(next => action(next.rooms[selectedRoom]));
  function replace(next) { setPack(next); setRealmIndex(0); setRoomId(next.levels[0].start); setTab('rooms'); setMessage('Adventure loaded. Your browser draft has been replaced.'); }
  function parseImport(text) {
    try {
      if (new TextEncoder().encode(text).length > 1024 * 1024) throw new Error('File exceeds 1 MB.');
      const next = JSON.parse(text);
      const issues = validateAdventure(next);
      if (issues.length) { setMessage(issues.slice(0, 8).join('\n')); return; }
      if (window.confirm('Replace your current designer draft? Export it first if you want to keep both adventures.')) replace(next);
    } catch (error) { setMessage(`Could not import: ${error.message}`); }
  }
  async function importFile(event) {
    const file = event.target.files?.[0]; event.target.value = '';
    if (!file) return;
    if (file.size > 1024 * 1024) { setMessage('File exceeds 1 MB.'); return; }
    try { parseImport(await file.text()); } catch { setMessage('Could not read this file.'); }
  }
  function download() {
    if (errors.length) { setMessage('Resolve the validation issues below before exporting.'); return; }
    const url = URL.createObjectURL(new Blob([JSON.stringify(pack, null, 2) + '\n'], { type: 'application/json' }));
    const link = document.createElement('a'); link.href = url; link.download = `${pack.id}.json`; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    setMessage('JSON exported. An administrator must review and install this pack before it appears in the adventure library.');
  }
  function addRealm() {
    if (pack.levels.length >= 100) return;
    const number = Math.max(...pack.levels.map(l => l.number)) + 1;
    edit(next => next.levels.push({ number, name: `Realm ${number}`, objective: 'Explore the realm and discover its secret.', start: 'entrance', rooms: { entrance: blankRoom('Realm entrance') }, puzzles: [], winFlags: [] }));
    setRealmIndex(pack.levels.length); setRoomId('entrance');
  }
  return <section className="adventure-designer" aria-label="Adventure designer">
    <header className="designer-heading"><div><p className="designer-eyebrow">THE SCRIBE’S WORKSHOP</p><h1>Adventure designer</h1><p>Shape a realm. Leave clues. Give its mysteries a solution.</p></div><button type="button" onClick={close}>BACK TO LIBRARY</button></header>
    <p className="designer-notice">Your draft, including unfinished edits, is saved only in this browser on this device. Export a JSON file to keep a backup or share it. This editor does not publish games or change live expeditions.</p>
    <div className="designer-actions"><button type="button" onClick={download}>EXPORT ADVENTURE</button><label className="designer-file">IMPORT JSON<input aria-label="Import adventure JSON" type="file" accept=".json,application/json" onChange={importFile} /></label><button type="button" onClick={() => { if (window.confirm('Replace your current draft with the starter adventure? Export first to keep your work.')) replace(starter()); }}>NEW FROM STARTER</button></div>
    {(message || storageError) && <div className="designer-message" role="status">{message}{message && storageError && '\n'}{storageError}</div>}
    <div className="designer-grid"><aside className="designer-sidebar">
      <Field label="Adventure title"><input value={pack.name} maxLength={100} onChange={e => edit(next => { next.name = e.target.value; })} /></Field>
      <Field label="Adventure ID" hint="Stable lowercase ID; use hyphens instead of spaces."><input value={pack.id} maxLength={48} onChange={e => edit(next => { next.id = e.target.value; })} /></Field>
      <Field label="Starting realm"><select value={pack.startLevel} onChange={e => edit(next => { next.startLevel = Number(e.target.value); })}>{pack.levels.map(l => <option key={l.number} value={l.number}>{l.number}. {l.name}</option>)}</select></Field>
      <h2>Realms</h2><div className="designer-scroll designer-realm-list">{pack.levels.map((l, i) => <button type="button" key={l.number} className={i === realmIndex ? 'selected' : ''} aria-pressed={i === realmIndex} onClick={() => { setRealmIndex(i); setRoomId(l.start); }}>{String(l.number).padStart(2, '0')} · {l.name}</button>)}</div><button type="button" disabled={pack.levels.length >= 100} onClick={addRealm}>+ ADD REALM</button>
      <p className="designer-muted">Connect realms with portals. Exits connect rooms within one realm. Connections are one-way unless you add a return route.</p>
    </aside><div className="designer-main">
      <div className="designer-two"><Field label="Realm name"><input value={level.name} maxLength={100} onChange={e => editLevel(l => { l.name = e.target.value; })} /></Field><Field label="Realm starting room"><select value={level.start} onChange={e => editLevel(l => { l.start = e.target.value; })}>{Object.entries(level.rooms).map(([id, r]) => <option key={id} value={id}>{r.name} ({id})</option>)}</select></Field></div>
      <Field label="Realm objective"><textarea value={level.objective} maxLength={2000} rows={2} onChange={e => editLevel(l => { l.objective = e.target.value; })} /></Field>
      <div className="designer-tabs" aria-label="Designer sections">{['rooms', 'puzzles', 'readables', 'portals', 'json'].map(name => <button type="button" key={name} aria-pressed={tab === name} onClick={() => { setTab(name); if (name === 'json') setRaw(JSON.stringify(pack, null, 2)); }}>{name.toUpperCase()}</button>)}</div>
      {tab==='readables'&&<section aria-label="Readable items"><h2>Scrolls, letters and other readable items</h2><p className="designer-muted">Add an item to a room first, then write its contents here. READ copies the text into the player's journal once. The item must be carried or visible in the current room. Leave blank for items that cannot be read. This text follows the item when it is picked up or dropped.</p>{[...new Set([...pack.levels.flatMap(l=>Object.values(l.rooms).flatMap(r=>r.items)),...Object.keys(pack.readableItems||{})])].filter(Boolean).map(name=><Field key={name} label={`READ ${name.toUpperCase()}`}><textarea maxLength={2000} rows={3} value={pack.readableItems?.[name]||''} onChange={e=>edit(p=>{p.readableItems||={};if(e.target.value)p.readableItems[name]=e.target.value;else delete p.readableItems[name];})}/></Field>)}</section>}
      {tab === 'rooms' && <div>
        <Field label="Edit room"><select value={selectedRoom} onChange={e => { setRoomId(e.target.value); setFeatureName(''); }}>{Object.entries(level.rooms).map(([id, r]) => <option key={id} value={id}>{r.name} ({id})</option>)}</select></Field>
        <div className="designer-inline"><Field label="New room ID"><input placeholder="hidden-vault" value={roomName} maxLength={48} onChange={e => setRoomName(e.target.value)} /></Field><button type="button" disabled={Object.keys(level.rooms).length >= 100} onClick={() => { if (!identifier(roomName) || Object.hasOwn(level.rooms, roomName)) { setMessage('Use a unique lowercase room ID, beginning with a letter.'); return; } editLevel(l => { l.rooms[roomName] = blankRoom(roomName.replaceAll('-', ' ')); }); setRoomId(roomName); setRoomName(''); }}>+ ADD ROOM</button></div>
        <Field label="Room name"><input value={room.name} maxLength={100} onChange={e => editRoom(r => { r.name = e.target.value; })} /></Field>
        <Field label="What does the explorer see?" hint="Describe permanent scenery here. Loose items are listed automatically from the current game state. Use changing descriptions below for puzzles and movable objects."><textarea value={room.description} maxLength={2000} rows={4} onChange={e => editRoom(r => { r.description = e.target.value; })} /></Field>
        <RoomStates key={`${level.number}:${selectedRoom}`} room={room} level={level} editRoom={editRoom} />
        <div className="designer-two"><Field label="Room lighting"><select value={room.lit === false ? 'dark' : 'lit'} onChange={e => editRoom(r => { r.lit = e.target.value === 'lit'; })}><option value="lit">Lit — visible without lamp</option><option value="dark">Dark — lamp required</option></select></Field><Field label="Bundled room illustration"><select value={/^room-art\//.test(room.image || '') ? room.image : ''} onChange={e => editRoom(r => { if (e.target.value) r.image = e.target.value; else delete r.image; })}><option value="">None / custom path in JSON</option>{Array.from({ length: 60 }, (_, i) => <option key={i} value={`room-art/room${i + 1}.png`}>Room illustration {i + 1}</option>)}</select></Field></div>
        {/^room-art\/room([1-9]|[1-5][0-9]|60)\.png$/.test(room.image || '') && <img className="designer-room-preview" src={`${import.meta.env.BASE_URL}${room.image}`} alt="Selected gold room illustration" />}
        <Field label="Loose items" hint="Comma separated, lowercase. Players can GET these items."><input value={room.items.join(', ')} onChange={e => editRoom(r => { r.items = e.target.value.split(',').map(s => s.trimStart()); })} onBlur={() => editRoom(r => { r.items = r.items.map(s => s.trim()).filter(Boolean); })} /></Field>
        <h3>Exits and locks</h3><div className="designer-exits">{directions.map(direction => <div className="designer-two" key={direction}><Field label={`${direction} destination`}><select value={room.exits[direction] || ''} onChange={e => editRoom(r => { if (e.target.value) r.exits[direction] = e.target.value; else { delete r.exits[direction]; if (r.requires) delete r.requires[direction]; if (r.exitKinds) delete r.exitKinds[direction]; } })}><option value="">No exit</option>{Object.entries(level.rooms).map(([id, r]) => <option key={id} value={id}>{r.name}</option>)}</select></Field><Field label={`${direction} unlock flag`}><select disabled={!room.exits[direction]} value={room.requires?.[direction] || ''} onChange={e => editRoom(r => { r.requires ||= {}; if (e.target.value) r.requires[direction] = e.target.value; else delete r.requires[direction]; })}><option value="">Always open</option>{level.puzzles.map((p, i) => <option key={i} value={p.flag}>{p.flag}</option>)}</select></Field><Field label={`${direction} passage type`} hint="Use up/down destinations for floors above or below. A one-way hole needs only a down exit."><select disabled={!room.exits[direction]} value={room.exitKinds?.[direction] || 'passage'} onChange={e => editRoom(r => { r.exitKinds ||= {}; r.exitKinds[direction] = e.target.value; })}>{['passage','stairs','ladder','hole','bridge'].map(kind => <option key={kind} value={kind}>{kind}</option>)}</select></Field></div>)}</div>
        <h3>Things to examine</h3><p className="designer-muted">Feature names become EXAMINE commands. Put fair clues here.</p>{Object.entries(room.features).map(([name, text]) => <div key={name} className="designer-feature"><Field label={`EXAMINE ${name.toUpperCase()}`}><textarea value={text} maxLength={2000} onChange={e => editRoom(r => { r.features[name] = e.target.value; })} /></Field><button type="button" aria-label={`Remove feature ${name}`} onClick={() => editRoom(r => { delete r.features[name]; })}>REMOVE</button></div>)}
        <div className="designer-inline"><Field label="New feature name"><input value={featureName} placeholder="inscription" onChange={e => setFeatureName(e.target.value)} /></Field><button type="button" onClick={() => { if (!itemName(featureName) || Object.hasOwn(room.features, featureName) || Object.keys(room.features).length >= 40) { setMessage('Choose a unique lowercase feature name; maximum 40 features per room.'); return; } editRoom(r => { r.features[featureName] = 'Write the clue the explorer discovers.'; }); setFeatureName(''); }}>+ ADD CLUE</button></div>
      </div>}
      {tab === 'puzzles' && <div><p className="designer-muted">A puzzle matches an exact command in its room. Solving it sets a flag; flags can unlock exits, portals or other puzzles. Items are required in the satchel but are not consumed.</p>
        {level.puzzles.map((p, i) => <PuzzleForm key={i} puzzle={p} index={i} level={level} update={action => editLevel(l => action(l.puzzles[i]))} remove={() => { if (window.confirm('Remove this puzzle? Any exits or puzzles using its flag will need updating.')) editLevel(l => { l.puzzles.splice(i, 1); l.winFlags = l.winFlags.filter(f => f !== p.flag); }); }} />)}
        <button type="button" disabled={level.puzzles.length >= 100} onClick={() => editLevel(l => { let n = l.puzzles.length + 1; while (l.puzzles.some(p => p.flag === `mystery-${n}`)) n++; l.puzzles.push({ room: selectedRoom, command: `solve mystery ${n}`, needs: [], requires: [], flag: `mystery-${n}`, response: 'The mechanism settles into place.', hints: ['Examine the room carefully.'] }); })}>+ ADD PUZZLE</button>
        <h3>Realm completion</h3><p className="designer-muted">All checked flags must be solved. Portals control travel between realms; completion flags do not create exits.</p>{level.puzzles.map((p, i) => <label className="designer-check" key={i}><input type="checkbox" checked={level.winFlags.includes(p.flag)} onChange={e => editLevel(l => { l.winFlags = e.target.checked ? [...new Set([...l.winFlags, p.flag])] : l.winFlags.filter(f => f !== p.flag); })} />{p.flag}</label>)}
      </div>}
      {tab === 'portals' && <div><p className="designer-muted">Portals connect realms. Add a second portal for a return trip. A portal cannot share a direction with a normal room exit.</p>{pack.portals.map((p, i) => <PortalForm key={i} portal={p} index={i} pack={pack} update={action => edit(next => action(next.portals[i]))} remove={() => edit(next => { next.portals.splice(i, 1); })} />)}<button type="button" disabled={pack.portals.length >= 200} onClick={() => edit(next => next.portals.push({ fromLevel: level.number, fromRoom: selectedRoom, direction: 'east', toLevel: next.levels[0].number, toRoom: next.levels[0].start, description: 'A portal shimmers here.' }))}>+ ADD PORTAL</button></div>}
      {tab === 'json' && <div><p className="designer-muted">Advanced view of the complete adventure. Apply validates and replaces the draft. Custom art paths require an administrator to install the matching PNG files. Add-on packs with an “extends” field are maintained outside this editor.</p><Field label="Adventure JSON"><textarea className="designer-json" spellCheck={false} value={raw} onChange={e => setRaw(e.target.value)} /></Field><button type="button" onClick={() => parseImport(raw)}>VALIDATE & APPLY JSON</button></div>}
    </div></div>
    <section className="designer-validation" aria-label="Adventure validation"><h2>{errors.length ? `${errors.length} issue${errors.length === 1 ? '' : 's'} to resolve` : 'Ready to export'}</h2>{errors.length ? <ul className="designer-scroll">{errors.map((error, i) => <li key={i}>{error}</li>)}</ul> : <p>Schema and references pass validation. Playtest all routes and puzzle solutions before publishing; this check cannot prove that the adventure is solvable.</p>}</section>
  </section>;
}

function PuzzleForm({ puzzle: p, index, level, update, remove }) {
  return <fieldset className="designer-card"><legend>Puzzle {index + 1}</legend><div className="designer-two"><Field label={`Puzzle ${index + 1} room`}><select value={p.room} onChange={e => update(x => { x.room = e.target.value; })}>{Object.entries(level.rooms).map(([id, r]) => <option key={id} value={id}>{r.name}</option>)}</select></Field><Field label="Exact command"><input value={p.command} maxLength={64} placeholder="turn bronze wheel" onChange={e => update(x => { x.command = e.target.value; })} /></Field></div><Field label="Completion flag" hint="Changing a used flag requires updating its locks and prerequisites."><input value={p.flag} maxLength={48} onChange={e => update(x => { x.flag = e.target.value; })} /></Field><div className="designer-two"><Field label="Required items (comma separated)"><input value={p.needs.join(', ')} onChange={e => update(x => { x.needs = e.target.value.split(',').map(s => s.trimStart()); })} onBlur={() => update(x => { x.needs = list(x.needs.join(',')); })} /></Field><Field label="Prerequisite flags (comma separated)"><input value={p.requires.join(', ')} onChange={e => update(x => { x.requires = e.target.value.split(',').map(s => s.trimStart()); })} onBlur={() => update(x => { x.requires = list(x.requires.join(',')); })} /></Field></div><Field label="Success response"><textarea value={p.response} maxLength={2000} onChange={e => update(x => { x.response = e.target.value; })} /></Field>{p.hints.map((hint, i) => <Field key={i} label={`Hint ${i + 1}`}><input value={hint} maxLength={2000} onChange={e => update(x => { x.hints[i] = e.target.value; })} /></Field>)}<div className="designer-actions"><button type="button" disabled={p.hints.length >= 5} onClick={() => update(x => x.hints.push('Write a more direct clue.'))}>+ ADD HINT</button>{p.hints.length > 1 && <button type="button" onClick={() => update(x => { x.hints.pop(); })}>REMOVE LAST HINT</button>}<button type="button" onClick={remove}>REMOVE PUZZLE</button></div></fieldset>;
}

function PortalForm({ portal: p, index, pack, update, remove }) {
  const from = pack.levels.find(l => l.number === p.fromLevel), to = pack.levels.find(l => l.number === p.toLevel);
  return <fieldset className="designer-card"><legend>Portal {index + 1}</legend><div className="designer-two"><Field label="From realm"><select value={p.fromLevel} onChange={e => update(x => { x.fromLevel = Number(e.target.value); x.fromRoom = pack.levels.find(l => l.number === x.fromLevel).start; delete x.requires; })}>{pack.levels.map(l => <option key={l.number} value={l.number}>{l.name}</option>)}</select></Field><Field label="From room"><select value={p.fromRoom} onChange={e => update(x => { x.fromRoom = e.target.value; })}>{Object.entries(from.rooms).map(([id, r]) => <option key={id} value={id}>{r.name}</option>)}</select></Field><Field label="To realm"><select value={p.toLevel} onChange={e => update(x => { x.toLevel = Number(e.target.value); x.toRoom = pack.levels.find(l => l.number === x.toLevel).start; })}>{pack.levels.map(l => <option key={l.number} value={l.number}>{l.name}</option>)}</select></Field><Field label="To room"><select value={p.toRoom} onChange={e => update(x => { x.toRoom = e.target.value; })}>{Object.entries(to.rooms).map(([id, r]) => <option key={id} value={id}>{r.name}</option>)}</select></Field><Field label="Portal direction"><select value={p.direction} onChange={e => update(x => { x.direction = e.target.value; })}>{directions.map(d => <option key={d} value={d}>{d}</option>)}</select></Field><Field label="Portal unlock flag"><select value={p.requires || ''} onChange={e => update(x => { if (e.target.value) x.requires = e.target.value; else delete x.requires; })}><option value="">Always open</option>{from.puzzles.map((puzzle, i) => <option key={i} value={puzzle.flag}>{puzzle.flag}</option>)}</select></Field></div><Field label="Portal description"><textarea value={p.description} maxLength={2000} onChange={e => update(x => { x.description = e.target.value; })} /></Field><button type="button" onClick={remove}>REMOVE PORTAL</button></fieldset>;
}





