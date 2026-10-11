import React,{useEffect,useId,useMemo,useRef,useState} from 'react';
import {layoutMap,floorName,routeLabel} from './mapLayout';
import './exploration.css';

export default function ExplorationMap({snapshot,onClose}){
 const dialog=useRef(),arrowId=useId().replace(/:/g,''),[view,setView]=useState('rooms');
 const map=useMemo(()=>layoutMap(snapshot),[snapshot]);
 const currentFloor=map.positions.get(snapshot.currentRoom)?.z??0;
 const [selectedFloor,setSelectedFloor]=useState(currentFloor);
 useEffect(()=>{dialog.current.showModal();},[]);
 const floor=map.floors.includes(selectedFloor)?selectedFloor:currentFloor;
 const nodes=map.nodes.filter(n=>map.positions.get(n.id).z===floor);
 const names=new Map(map.nodes.map(n=>[n.id,n.name]));
 const ordered=[...nodes].sort((a,b)=>Number(b.id===snapshot.currentRoom)-Number(a.id===snapshot.currentRoom));
 const values=nodes.map(n=>map.positions.get(n.id)),minX=Math.min(0,...values.map(p=>p.x)),minY=Math.min(0,...values.map(p=>p.y));
 const width=(Math.max(0,...values.map(p=>p.x))-minX+1)*260+40,height=(Math.max(0,...values.map(p=>p.y))-minY+1)*130+40;
 const point=id=>{const p=map.positions.get(id);return{x:(p.x-minX)*260+150,y:(p.y-minY)*130+85};};
 const onFloor=id=>map.positions.get(id).z===floor;
 const sameFloor=map.edges.filter(e=>onFloor(e.from)&&onFloor(e.to));
 const connections=sameFloor.filter((e,i)=>!sameFloor.slice(0,i).some(p=>p.from===e.to&&p.to===e.from&&p.kind===e.kind));
 const crossFloor=map.edges.filter(e=>onFloor(e.from)&&!onFloor(e.to));
 const route=(e)=>`${routeLabel(e)} → ${names.get(e.to)}${onFloor(e.to)?'':` (${floorName(map.positions.get(e.to).z)})`}`;
 function ends(e){const a=point(e.from),b=point(e.to),dx=b.x-a.x,dy=b.y-a.y,scale=1/Math.max(Math.abs(dx)/108,Math.abs(dy)/37);return {x1:a.x+dx*scale,y1:a.y+dy*scale,x2:b.x-dx*scale,y2:b.y-dy*scale};}
 return <dialog ref={dialog} onCancel={onClose} className={`journal exploration-map map-view-${view}`} aria-labelledby="map-title">
  <div className="rail-heading"><h2 id="map-title">Explorer’s map</h2><button onClick={onClose} autoFocus aria-label="Close map">CLOSE</button></div>
  <p>2 points charged. Only visited rooms and known routes appear. Floors are relative to the first mapped room in each connected area. This is a schematic; route labels give exact directions. Switching floors or views is free.</p>
  {map.conflicts>0&&<p className="map-layout-note">Some passages bend between floors. Follow the labeled routes; their directions take precedence over the drawing.</p>}
  <div className="map-floor-controls"><label htmlFor="map-floor">Show floor</label><select id="map-floor" value={floor} onChange={e=>setSelectedFloor(Number(e.target.value))}>{map.floors.map(z=><option key={z} value={z}>{floorName(z)}{z===currentFloor?' — you are here':''}</option>)}</select>{floor!==currentFloor&&<button onClick={()=>setSelectedFloor(currentFloor)}>MY FLOOR</button>}</div>
  <nav className="map-mobile-switch" aria-label="Map display"><button aria-pressed={view==='rooms'} onClick={()=>setView('rooms')}>ROOMS &amp; ROUTES</button><button aria-pressed={view==='drawing'} onClick={()=>setView('drawing')}>DRAWING</button></nav>
  <div className="map-room-cards">{ordered.map(n=><section key={n.id} className={`map-room-card ${n.id===snapshot.currentRoom?'current':''}`}><h3>{n.name}</h3>{n.id===snapshot.currentRoom&&<span className="map-here">YOU ARE HERE</span>}<ul>{map.edges.filter(e=>e.from===n.id).map((e,i)=><li key={i}><span>{routeLabel(e)}</span><strong>{names.get(e.to)}{!onFloor(e.to)&&<button className="map-floor-link" onClick={()=>setSelectedFloor(map.positions.get(e.to).z)}>View {floorName(map.positions.get(e.to).z)}</button>}</strong></li>)}</ul>{!map.edges.some(e=>e.from===n.id)&&<p>No outgoing routes to other visited rooms recorded.</p>}</section>)}</div>
  <p className="map-pan-note">Swipe the drawing to explore it. Arrowheads show permitted travel. Dashed paths are portals.</p>
  <div className="map-scroll" tabIndex="0" aria-label="Scrollable map drawing"><svg viewBox={`0 0 ${width} ${height}`} width={width} height={height} role="img" aria-label={`Visited rooms on ${floorName(floor)}. Arrows show travel direction. Current room has a double border.`}>
   <defs><marker id={arrowId} viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M 0 0 L 10 5 L 0 10 z" fill="#dbb675"/></marker></defs>
   <g className="map-paths">{connections.filter(e=>e.from!==e.to).map((e,i)=>{const twoWay=sameFloor.some(p=>p.from===e.to&&p.to===e.from&&p.kind===e.kind);return <line key={i} {...ends(e)} className={e.kind==='portal'?'portal':''} markerEnd={`url(#${arrowId})`} markerStart={twoWay?`url(#${arrowId})`:undefined}><title>{route(e)}{twoWay?' (return route available)':' (one-way)'}</title></line>;})}</g>
   {nodes.map(n=>{const p=point(n.id),current=n.id===snapshot.currentRoom,vertical=map.edges.filter(e=>e.from===n.id&&(e.direction==='up'||e.direction==='down'||!onFloor(e.to)));return <g key={n.id} className={current?'map-room current':'map-room'}><title>{n.name}{vertical.length?' · '+vertical.map(route).join('; '):''}</title><rect x={p.x-102} y={p.y-32} width="204" height="64" rx="2"/>{current&&<rect x={p.x-98} y={p.y-28} width="196" height="56" rx="2"/>}<text x={p.x} y={p.y-3} textAnchor="middle">{n.name.length>25?n.name.slice(0,24)+'…':n.name}</text><text x={p.x} y={p.y+17} textAnchor="middle" className="map-marker">{current?'YOU ARE HERE':'VISITED'}</text>{vertical.length>0&&<text x={p.x} y={p.y+49} textAnchor="middle" className="map-level-marker">{[...new Set(vertical.map(e=>routeLabel(e)))].join(' / ')}</text>}</g>;})}
  </svg></div>
  {crossFloor.length>0&&<section className="map-floor-routes"><h3>Routes between floors</h3><ul>{crossFloor.map((e,i)=><li key={i}><span>{names.get(e.from)}: {route(e)}{!map.edges.some(p=>p.from===e.to&&p.to===e.from)?' · ONE-WAY':''}</span><button onClick={()=>setSelectedFloor(map.positions.get(e.to).z)}>VIEW FLOOR</button></li>)}</ul></section>}
  <details><summary>Known routes on this floor (text version)</summary><ul>{ordered.map(n=><li key={n.id}><strong>{n.name}{n.id===snapshot.currentRoom?' — you are here':''}</strong><ul>{map.edges.filter(e=>e.from===n.id).map((e,i)=><li key={i}>{route(e)}</li>)}</ul></li>)}</ul></details>
 </dialog>;
}
