import {useState} from 'react';

// Paths come from the server's fixed room mapping, never from player text.
export default function RoomArt({number,name,source,lit}) {
  const [failed,setFailed]=useState(null);
  const visible=source&&failed!==source;
  return <figure className="room-art" aria-label={`Room ${number}: ${name}`}>
    <div className="room-art-picture">
      {visible?<img key={source} src={source} width="384" height="128" alt={`Gold pixel relief of ${name}`} decoding="async" draggable="false" onError={()=>setFailed(source)}/>:<span className="room-art-empty">{source?'Relief unavailable':lit?'No relief recorded':'Hidden in darkness'}</span>}
    </div>
    <figcaption>ROOM {String(number).padStart(2,'0')} <span>· {name}</span></figcaption>
  </figure>;
}
