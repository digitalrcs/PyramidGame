import assert from 'node:assert/strict';
import {layoutMap,floorName,routeLabel} from './mapLayout.js';
const nodes=['start','lower','cellar','upper'].map(id=>({id,name:id}));
const edges=[{from:'start',to:'lower',direction:'down',kind:'hole'},{from:'lower',to:'cellar',direction:'east'},{from:'start',to:'upper',direction:'up',kind:'ladder'}];
const map=layoutMap({nodes,edges});
assert.deepEqual(map.floors,[1,0,-1]);
assert.deepEqual(map.positions.get('lower'),{x:0,y:0,z:-1});
assert.deepEqual(map.positions.get('upper'),{x:0,y:0,z:1});
assert.deepEqual(map.positions.get('cellar'),{x:1,y:0,z:-1});
assert.equal(map.edges.length,3); // Do not invent a return path for a hole.
assert.equal(routeLabel(edges[0]),'DOWN · HOLE');
const filtered=layoutMap({nodes:[nodes[0]],edges});assert.equal(filtered.edges.length,0);assert.equal(filtered.positions.size,1);
const incoming=layoutMap({nodes:[nodes[1],nodes[0]],edges:[edges[0]]});assert.equal(incoming.positions.get('start').z,1);
const portal=layoutMap({nodes:[nodes[0],nodes[1]],edges:[{...edges[0],kind:'portal'}]});assert.equal(portal.positions.get('lower').z,0);assert.notEqual(portal.positions.get('lower').x,0);
const conflict=layoutMap({nodes:nodes.slice(0,2),edges:[edges[0],{from:'lower',to:'start',direction:'east'}]});assert.ok(conflict.conflicts>0);
assert.equal(floorName(-2),'2 floors below');assert.equal(floorName(0),'Reference floor');
console.log('Map layout checks passed: vertical floors, one-way holes, known rooms, incoming paths, portals, conflicting geometry.');
