import { useEffect, useState } from "react";
import { Camera, CheckCircle2, KeyRound, LoaderCircle, Save, ShieldAlert, UserRound, X } from "lucide-react";
import type { PortalSession } from "@/lib/session";
import { sessionRoleLabel } from "@/lib/session";

export default function ProfileSettings({ session, forced = false, onClose, onSession }: { session: PortalSession; forced?: boolean; onClose: () => void; onSession: (session: PortalSession) => void }) {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [avatarPreview, setAvatarPreview] = useState(session.avatarUrl || "");
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => setAvatarPreview(session.avatarUrl || ""), [session.avatarUrl]);

  async function uploadAvatar(file: File | null) {
    if (!file) return;
    setSaving(true); setError(""); setNotice("");
    try {
      const data = new FormData(); data.append("avatar", file);
      const response = await fetch("/api/account/avatar", { method: "POST", body: data });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(body.error || "No se pudo guardar la fotografía.");
      setAvatarPreview(body.avatarUrl); onSession({ ...session, avatarUrl: body.avatarUrl }); setNotice("Fotografía de perfil actualizada.");
    } catch (uploadError) { setError((uploadError as Error).message); }
    finally { setSaving(false); }
  }

  async function changePassword(event: React.FormEvent) {
    event.preventDefault(); setError(""); setNotice("");
    if (newPassword !== confirmPassword) { setError("La confirmación no coincide con la nueva contraseña."); return; }
    setSaving(true);
    try {
      const response = await fetch("/api/account/password", { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ currentPassword, newPassword }) });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(body.error || "No se pudo cambiar la contraseña.");
      onSession(body as PortalSession); setCurrentPassword(""); setNewPassword(""); setConfirmPassword(""); setNotice("Contraseña actualizada correctamente.");
    } catch (passwordError) { setError((passwordError as Error).message); }
    finally { setSaving(false); }
  }

  return <div className={`profile-settings-backdrop ${forced ? "forced" : ""}`} role="presentation">
    <section className="profile-settings" role="dialog" aria-modal="true" aria-label="Configuración de mi perfil">
      <header><div><span>CONFIGURACIÓN PERSONAL</span><h2>Configuración</h2><p>Personaliza la apariencia, tu identidad y la seguridad de tu cuenta.</p></div>{!forced && <button onClick={onClose} aria-label="Cerrar"><X/></button>}</header>
      {forced && <div className="profile-forced"><ShieldAlert/><div><strong>Cambio de contraseña obligatorio</strong><span>Antes de ver tus grupos, crea una contraseña personal que solo tú conozcas.</span></div></div>}
      <div className="profile-overview"><label className="profile-avatar-editor" htmlFor="profile-avatar">{avatarPreview ? <img src={avatarPreview} alt={session.displayName}/> : <UserRound/>}<span><Camera/> Cambiar foto</span><input id="profile-avatar" name="profileAvatar" type="file" accept="image/jpeg,image/png,image/webp" onChange={event => void uploadAvatar(event.target.files?.[0] || null)}/></label><div><small>{sessionRoleLabel(session)}</small><strong>{session.displayName}</strong><span>@{session.email.replace(/@grupotejeda\.local$/i, "")}</span><em>{session.scope === "all" ? "Cobertura completa" : session.scope === "support-department" ? "Bandeja departamental asignada" : "Solo grupos asignados"}</em></div></div>
      {notice && <div className="profile-notice"><CheckCircle2/> {notice}</div>}{error && <div className="alert">{error}</div>}
      <form className="profile-password-form" onSubmit={changePassword}><div className="profile-section-title"><KeyRound/><span><strong>Cambiar contraseña</strong><small>Mínimo 8 caracteres.</small></span></div><label htmlFor="current-password">Contraseña actual<input id="current-password" name="currentPassword" type="password" autoComplete="current-password" required value={currentPassword} onChange={event=>setCurrentPassword(event.target.value)}/></label><label htmlFor="new-password">Nueva contraseña<input id="new-password" name="newPassword" type="password" autoComplete="new-password" minLength={8} required value={newPassword} onChange={event=>setNewPassword(event.target.value)}/></label><label htmlFor="confirm-password">Confirmar nueva contraseña<input id="confirm-password" name="confirmPassword" type="password" autoComplete="new-password" minLength={8} required value={confirmPassword} onChange={event=>setConfirmPassword(event.target.value)}/></label><button className="primary" disabled={saving}>{saving?<LoaderCircle className="spin"/>:<Save/>} Guardar nueva contraseña</button></form>
    </section>
  </div>;
}
