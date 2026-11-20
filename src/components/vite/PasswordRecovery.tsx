import { apiFetch as fetch } from "@/lib/api-fetch";
import {useState} from 'react';
import './passwordRecovery.css';

async function post(path:string,body:object){
  const response=await fetch(path,{method:'POST',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
  const data=await response.json().catch(()=>({}));
  if(!response.ok)throw new Error(response.status===429?'Demasiados intentos. Espera un minuto antes de volver a intentar.':data.error||'No se pudo completar la operación.');
  return data;
}

export function RecoveryCodeStart({initialUser,onBack}:{initialUser:string;onBack:()=>void}){
  const [user,setUser]=useState(initialUser);
  const [password,setPassword]=useState(''),[code,setCode]=useState(''),[busy,setBusy]=useState(false),[message,setMessage]=useState('');
  async function generate(event:React.FormEvent){
    event.preventDefault();if(busy)return;setBusy(true);setMessage('');setCode('');
    try{const data=await post('/api/account/recovery-code/generate',{userName:user.trim(),currentPassword:password});setCode(data.code);setPassword('');setMessage('Guarda este código en un lugar privado. No podremos volver a mostrarlo después de cerrar esta ventana.');}
    catch(error){setMessage((error as Error).message);}finally{setBusy(false);}
  }
  return <section className="password-recovery"><h3>Código de recuperación</h3><p>Permite recuperar tu contraseña sin correo ni teléfono. Es de un solo uso. Generar otro reemplaza el anterior.</p><form onSubmit={generate}><label>Usuario<input autoComplete="username" maxLength={160} required value={user} onChange={event=>setUser(event.target.value)} spellCheck={false}/></label><label>Confirma tu contraseña actual<input type="password" autoComplete="current-password" value={password} maxLength={128} required onChange={event=>setPassword(event.target.value)}/></label><button type="submit" disabled={busy||!password||!user.trim()}>{busy?'Generando…':'Generar o reemplazar código'}</button></form>{message&&<p role="status">{message}</p>}{code&&<div><label>Código privado<input readOnly value={code} onFocus={event=>event.target.select()} spellCheck={false} autoComplete="off"/></label><button type="button" onClick={()=>void navigator.clipboard.writeText(code).then(()=>setMessage('Código copiado. Guárdalo fuera de esta aplicación.')).catch(()=>setMessage('Selecciona el código y cópialo manualmente.'))}>Copiar código</button></div>}<p>Si pierdes también este código, necesitarás ayuda del administrador.</p><button type="button" disabled={busy} onClick={onBack}>Volver al inicio de sesión</button></section>;
}

export default function PasswordRecovery({initialUser,onBack,onRecovered}:{initialUser:string;onBack:()=>void;onRecovered:(user:string)=>void}){
  const [user,setUser]=useState(initialUser),[code,setCode]=useState(''),[password,setPassword]=useState(''),[confirm,setConfirm]=useState(''),[busy,setBusy]=useState(false),[error,setError]=useState('');
  async function submit(event:React.FormEvent){
    event.preventDefault();if(busy)return;setError('');
    if(password!==confirm){setError('Las contraseñas no coinciden.');return;}
    setBusy(true);
    try{await post('/api/account/password/recover',{userName:user.trim(),code:code.trim(),newPassword:password});setCode('');setPassword('');setConfirm('');onRecovered(user.trim());}
    catch(reason){setError((reason as Error).message);}finally{setBusy(false);}
  }
  return <section className="password-recovery"><h2>Recuperar contraseña</h2><p>Escribe tu usuario y el código privado que guardaste para recuperar tu cuenta.</p><form onSubmit={submit}><label>Usuario<input autoComplete="username" maxLength={160} required value={user} onChange={event=>setUser(event.target.value)} spellCheck={false}/></label><label>Código de recuperación<input autoComplete="off" maxLength={80} required value={code} onChange={event=>setCode(event.target.value)} spellCheck={false}/></label><label>Nueva contraseña<input type="password" autoComplete="new-password" minLength={8} maxLength={128} required value={password} onChange={event=>setPassword(event.target.value)}/></label><label>Confirmar nueva contraseña<input type="password" autoComplete="new-password" minLength={8} maxLength={128} required value={confirm} onChange={event=>setConfirm(event.target.value)}/></label>{error&&<p role="alert">{error}</p>}<button type="submit" disabled={busy}>{busy?'Restableciendo…':'Restablecer contraseña'}</button><button type="button" disabled={busy} onClick={onBack}>Volver al inicio de sesión</button></form><p>Si nunca generaste un código o lo perdiste, contacta al administrador. No necesitas correo ni teléfono.</p></section>;
}
