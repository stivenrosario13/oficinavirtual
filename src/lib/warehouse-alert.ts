export function isWarehouseNotificationUser(session:{role?:string|null;supportTeam?:string|null}|null|undefined){
 return session?.role==='Warehouse'||session?.supportTeam==='WAREHOUSE';
}

export function canReceiveWarehouseNotifications(session:{role?:string|null;supportTeam?:string|null}|null|undefined){
 return session?.role==='Administrator'||isWarehouseNotificationUser(session);
}

// A finite, more prominent alert. It respects browser autoplay and device volume.
export function playWarehouseAlert(audio:AudioContext|null){
 if(!audio||audio.state!=='running')return false;
 const now=audio.currentTime;
 [0,.65,1.3].forEach(delay=>{
  const oscillator=audio.createOscillator(),gain=audio.createGain();
  oscillator.type='triangle';oscillator.frequency.setValueAtTime(880,now+delay);oscillator.frequency.setValueAtTime(1174.66,now+delay+.2);
  gain.gain.setValueAtTime(.0001,now+delay);gain.gain.exponentialRampToValueAtTime(.5,now+delay+.03);gain.gain.exponentialRampToValueAtTime(.0001,now+delay+.5);
  oscillator.connect(gain);gain.connect(audio.destination);oscillator.start(now+delay);oscillator.stop(now+delay+.53);
  oscillator.onended=()=>{oscillator.disconnect();gain.disconnect();};
 });
 return true;
}
