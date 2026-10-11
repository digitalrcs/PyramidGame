const offsets={north:[0,-1,0],south:[0,1,0],east:[1,0,0],west:[-1,0,0],up:[0,0,1],down:[0,0,-1]};
export const floorName=z=>z===0?'Reference floor':`${Math.abs(z)} floor${Math.abs(z)===1?'':'s'} ${z>0?'above':'below'}`;
export function layoutMap(snapshot){
 const nodes=snapshot.nodes||[],known=new Set(nodes.map(n=>n.id));
 const edges=(snapshot.edges||[]).filter(e=>known.has(e.from)&&known.has(e.to));
 const logical=new Map(),positions=new Map(),occupied=new Set();let component=0,conflicts=0;
 for(const start of nodes){
  if(logical.has(start.id))continue;
  logical.set(start.id,{x:component*3,y:0,z:0});component++;
  const queue=[start.id];
  for(let i=0;i<queue.length;i++){
   const id=queue[i],from=logical.get(id);
   for(const e of edges){
    if(e.kind==='portal'||!offsets[e.direction])continue;
    const forward=e.from===id,reverse=e.to===id;if(!forward&&!reverse)continue;
    const other=forward?e.to:e.from,[dx,dy,dz]=offsets[e.direction],sign=forward?1:-1;
    const proposed={x:from.x+dx*sign,y:from.y+dy*sign,z:from.z+dz*sign};
    if(!logical.has(other)){logical.set(other,proposed);queue.push(other);}
    else if(logical.get(other).z!==proposed.z)conflicts++;
   }
  }
 }
 // Separate colliding room boxes only within a floor; vertical passages keep their x/y when possible.
 for(const n of nodes){const p={...logical.get(n.id)};while(occupied.has(`${p.x},${p.y},${p.z}`))p.x++;occupied.add(`${p.x},${p.y},${p.z}`);positions.set(n.id,p);}
 return {nodes,edges,positions,floors:[...new Set([...positions.values()].map(p=>p.z))].sort((a,b)=>b-a),conflicts};
}
export function routeLabel(edge){const kind=edge.kind||'passage';return `${edge.direction.toUpperCase()}${kind==='passage'?'':` · ${kind.toUpperCase()}`}`;}
