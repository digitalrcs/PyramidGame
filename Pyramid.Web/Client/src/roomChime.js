import{useEffect,useRef,useState}from'react';
import{roomObservation,roomEvents,roomSound}from'./roomChimeEvents';
let audio;
export function unlockRoomAudio(){
 try{const Constructor=window.AudioContext||window.webkitAudioContext;if(!Constructor)return;audio??=new Constructor();if(audio.state==='suspended')audio.resume().catch(()=>{});}catch{/* Sound is optional. */}
}
const motifs={activity:[659.25,830.61,987.77],arrival:[329.63,440],reward:[1046.5,1318.51],puzzle:[523.25,659.25,783.99,1046.5],victory:[523.25,659.25,783.99,1046.5,1318.51]};
function chime(kind){
 if(!audio||audio.state!=='running')return;
 const start=audio.currentTime;
 (motifs[kind]||motifs.activity).forEach((frequency,i)=>{
   const tone=audio.createOscillator(),volume=audio.createGain(),time=start+i*.10;
   tone.type='sine';tone.frequency.value=frequency;volume.gain.setValueAtTime(0,time);volume.gain.linearRampToValueAtTime(.045,time+.012);volume.gain.exponentialRampToValueAtTime(.001,time+.65);
   tone.connect(volume);volume.connect(audio.destination);tone.start(time);tone.stop(time+.7);tone.onended=()=>{tone.disconnect();volume.disconnect();};
 });
}
export function useRoomChime(state,party,userId,active,defaultEnabled=true){
 const[preference,setPreference]=useState(()=>{try{return localStorage.getItem('pyramid.roomChimes');}catch{return null;}});
 const enabled=preference===null?defaultEnabled:preference!=='off';
 const previous=useRef(null),[notice,setNotice]=useState('');
 useEffect(()=>{const unlock=()=>unlockRoomAudio();document.addEventListener('pointerdown',unlock,{once:true});document.addEventListener('keydown',unlock,{once:true});return()=>{document.removeEventListener('pointerdown',unlock);document.removeEventListener('keydown',unlock);};},[]);
 useEffect(()=>{
   const next=roomObservation(state,party,userId,active),old=previous.current;
   const events=roomEvents(old,next);
   // Keep the last played world while the library loads a selected save, so
   // activation can distinguish resuming from switching adventures.
   previous.current=next?.active?next:old?{...old,active:false}:null;
   if(!next?.active||old?.world!==next?.world||old?.room!==next?.room)setNotice('');
   if(events.length){setNotice(events.map(e=>e.text).join(' '));if(enabled)chime(roomSound(events));}
 },[state,party,userId,active,enabled]);
 function toggle(){unlockRoomAudio();const next=enabled?'off':'on';try{localStorage.setItem('pyramid.roomChimes',next);}catch{}setPreference(next);}
 return{enabled,toggle,notice};
}
