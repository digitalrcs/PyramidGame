import{useEffect,useRef,useState}from'react';
let audio;
export function unlockRoomAudio(){
 try{const Constructor=window.AudioContext||window.webkitAudioContext;if(!Constructor)return;audio??=new Constructor();if(audio.state==='suspended')audio.resume().catch(()=>{});}catch{/* Sound is optional. */}
}
function chime(){
 if(!audio||audio.state!=='running')return;
 const start=audio.currentTime;
 [659.25,830.61,987.77].forEach((frequency,i)=>{
   const tone=audio.createOscillator(),volume=audio.createGain(),time=start+i*.10;
   tone.type='sine';tone.frequency.value=frequency;volume.gain.setValueAtTime(0,time);volume.gain.linearRampToValueAtTime(.045,time+.012);volume.gain.exponentialRampToValueAtTime(.001,time+.65);
   tone.connect(volume);volume.connect(audio.destination);tone.start(time);tone.stop(time+.7);tone.onended=()=>{tone.disconnect();volume.disconnect();};
 });
}
export function useRoomChime(state,partyId){
 const[enabled,setEnabled]=useState(()=>{try{return localStorage.getItem('pyramid.roomChimes')!=='off';}catch{return true;}});
 const previous=useRef(null),[notice,setNotice]=useState('');
 useEffect(()=>{const unlock=()=>unlockRoomAudio();document.addEventListener('pointerdown',unlock,{once:true});document.addEventListener('keydown',unlock,{once:true});return()=>{document.removeEventListener('pointerdown',unlock);document.removeEventListener('keydown',unlock);};},[]);
 const key=state?`${partyId||'solo'}:${state.packId}:${state.level}:${state.roomId}`:null;
 useEffect(()=>{
   if(key!==previous.current){previous.current=key;setNotice('');if(key&&state.roomActivity){setNotice('Something here invites investigation.');if(enabled)chime();}}
 },[key,state?.roomActivity,enabled]);
 function toggle(){unlockRoomAudio();setEnabled(old=>{try{localStorage.setItem('pyramid.roomChimes',old?'off':'on');}catch{}return !old;});}
 return{enabled,toggle,notice};
}
