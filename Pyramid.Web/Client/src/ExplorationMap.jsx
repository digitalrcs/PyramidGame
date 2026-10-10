import React,{useEffect,useRef} from 'react';
import './exploration.css';

export default function ExplorationMap({snapshot,onClose}){
 const dialog=useRef();
 useEffect(()=>{dialog.current.showModal();},[]);
 const nodes=snapshot.nodes,positions=new Map(),occupied=new Set();
 const offsets={north:[0,-1],south:[0,1],east:[1,0],west:[-1,0],up:[1,-1],down:[-1,1]};
 function place(id,x,y){while(occupied.has(`${x},${y}`))x++;positions.set(id,{x,y});occupied.add(`${x},${y}`);}
 if(nodes.length)place(nodes[0].id,0,0);
 for(let pass=0;pass<nodes.length;pass++)for(const edge of snapshot.edges){
   const from=positions.get(edge.from);if(from&&!positions.has(edge.to)){const [dx,dy]=offsets[edge.direction]||[1,0];place(edge.to,from.x+dx,from.y+dy);}
 }
 for(const node of nodes)if(!positions.has(node.id))place(node.id,0,positions.size);
 const values=[...positions.values()],minX=Math.min(0,...values.map(p=>p.x)),minY=Math.min(0,...values.map(p=>p.y));
 const width=(Math.max(0,...values.map(p=>p.x))-minX+1)*230+40,height=(Math.max(0,...values.map(p=>p.y))-minY+1)*110+40;
 const point=id=>{const p=positions.get(id);return{x:(p.x-minX)*230+135,y:(p.y-minY)*110+75};};
 const connections=snapshot.edges.filter((edge,i,all)=>all.findIndex(e=>(e.from===edge.from&&e.to===edge.to)||(e.from===edge.to&&e.to===edge.from))===i);
 return <dialog ref={dialog} onCancel={onClose} className="journal exploration-map" aria-labelledby="map-title"><div className="rail-heading"><h2 id="map-title">Explorer’s map</h2><button onClick={onClose} autoFocus aria-label="Close map">CLOSE</button></div><p>2 points charged. Only rooms you have visited in this realm appear. This is a schematic; route directions are listed below. Reopening costs another 2 points.</p><div className="map-scroll"><svg viewBox={`0 0 ${width} ${height}`} width={width} height={height} role="img" aria-label="Gold map of your visited rooms; your current room has a double border"><g className="map-paths">{connections.map((e,i)=>{const a=point(e.from),b=point(e.to);return <line key={i} x1={a.x} y1={a.y} x2={b.x} y2={b.y}/>;})}</g>{nodes.map(n=>{const p=point(n.id),current=n.id===snapshot.currentRoom;return <g key={n.id} className={current?'map-room current':'map-room'}><rect x={p.x-102} y={p.y-32} width="204" height="64" rx="2"/>{current&&<rect x={p.x-98} y={p.y-28} width="196" height="56" rx="2"/>}<text x={p.x} y={p.y-3} textAnchor="middle">{n.name.length>25?n.name.slice(0,24)+'…':n.name}</text><text x={p.x} y={p.y+17} textAnchor="middle" className="map-marker">{current?'YOU ARE HERE':'VISITED'}</text></g>;})}</svg></div><details><summary>Known routes (text version)</summary><ul>{nodes.map(n=><li key={n.id}><strong>{n.name}{n.id===snapshot.currentRoom?' — you are here':''}</strong><ul>{snapshot.edges.filter(e=>e.from===n.id).map((e,i)=><li key={i}>{e.direction} → {nodes.find(node=>node.id===e.to)?.name}</li>)}</ul></li>)}</ul></details></dialog>;
}
