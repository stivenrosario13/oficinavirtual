import {apiFetch} from './api-fetch';

export const passwordChangeEvent = 'account-password-change';

/** Revoke the UI only on an authoritative session response, never on a network error. */
export function startSessionValidation(onRevoked: () => void) {
  let active = true, checking = false, changingPassword = false, generation = 0;
  let lastInteraction=Date.now(),presencePending=false;
  const controller = new AbortController();
  const check = async () => {
    if (checking || changingPassword || document.hidden || !navigator.onLine) return;
    checking = true;
    const current = generation;
    try {
      const response = await apiFetch('/api/admin/session', {cache:'no-store',signal:controller.signal});
      const revoked = response.status === 401 || response.ok && (await response.json()).authenticated === false;
      if (active && current === generation && revoked) onRevoked();
    } catch { /* Keep the current session during network/server outages. */ }
    finally { checking = false; }
  };
  const passwordChange = (event: Event) => { changingPassword = Boolean((event as CustomEvent<boolean>).detail); generation++; };
  const refresh = () => void check();
  const markInteraction=()=>{lastInteraction=Date.now()};
  const pulse=async()=>{
    if(!active||presencePending||!navigator.onLine)return;
    presencePending=true;
    const isActive=!document.hidden&&Date.now()-lastInteraction<60000;
    try{await apiFetch(`/api/admin/presence?active=${isActive}`,{method:'POST',cache:'no-store',keepalive:true,signal:controller.signal})}catch{/* El siguiente pulso restablece la presencia. */}finally{presencePending=false}
  };
  const visibilityRefresh=()=>{refresh();void pulse()};
  const timer = window.setInterval(refresh,15000);
  const presenceTimer=window.setInterval(()=>void pulse(),20000);
  for(const event of ['pointerdown','keydown','touchstart','scroll'] as const)window.addEventListener(event,markInteraction,{passive:true});
  window.addEventListener('focus',refresh);
  document.addEventListener('visibilitychange',visibilityRefresh);
  window.addEventListener(passwordChangeEvent,passwordChange);
  void pulse();
  return () => {
    active=false;controller.abort();window.clearInterval(timer);window.clearInterval(presenceTimer);
    for(const event of ['pointerdown','keydown','touchstart','scroll'] as const)window.removeEventListener(event,markInteraction);
    window.removeEventListener('focus',refresh);document.removeEventListener('visibilitychange',visibilityRefresh);
    window.removeEventListener(passwordChangeEvent,passwordChange);
  };
}
