import { useState } from "react";
import { ArrowRight, Building2, CheckCircle2, Eye, EyeOff, KeyRound, LoaderCircle, LockKeyhole, MapPinned, ShieldCheck, UserRound } from "lucide-react";
import type { PortalSession } from "@/lib/session";

export default function PortalLogin({ onSuccess }: { onSuccess: (session: PortalSession) => void }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  async function submit(event: React.FormEvent) {
    event.preventDefault(); setLoading(true); setError("");
    try {
      const response = await fetch("/api/admin/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: email.trim().toLowerCase(), password }),
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(body.error || "No reconocemos ese usuario o contraseña.");
      onSuccess(body as PortalSession);
    } catch (loginError) {
      setError((loginError as Error).message || "No fue posible iniciar sesión.");
    } finally { setLoading(false); }
  }

  return <main className="portal-login-shell">
    <section className="portal-login-brand">
      <div className="portal-login-logo"><img src="/loto-real-logo-transparent.png" alt="Loto Real" /><span>GRUPO TEJEDA</span></div>
      <div className="portal-login-copy"><span><ShieldCheck /> Plataforma corporativa protegida</span><h1>Control territorial<br/><em>con acceso inteligente.</em></h1><p>Cada administrador accede únicamente a los grupos, agencias, auditorías y estadísticas que tiene asignados.</p></div>
      <div className="portal-login-features"><article><Building2/><span><strong>Alcance por grupo</strong><small>Información segmentada por responsable</small></span></article><article><MapPinned/><span><strong>Evidencia georreferenciada</strong><small>Fotografías, coordenadas y formularios</small></span></article></div>
    </section>
    <section className="portal-login-card">
      <div className="portal-login-card-inner">
        <div className="portal-login-card-head"><div className="portal-login-icon"><LockKeyhole /></div><span><small>OFICINA VIRTUAL SEGURA</small><em>GRUPO TEJEDA</em></span></div>
        <h2>Bienvenido de nuevo</h2><p>Accede con tu usuario corporativo y contraseña personal.</p>
        <div className="login-access-badge"><ShieldCheck/><span><strong>Acceso por usuario</strong><small>Formato: nombre.apellido</small></span></div>
        <form onSubmit={submit}>
          <div className="portal-login-field"><label htmlFor="portal-username">Usuario corporativo</label><div><UserRound/><input id="portal-username" name="username" type="text" value={email} onChange={event => setEmail(event.target.value.replace(/\s/g, "").toLowerCase())} required autoComplete="username" spellCheck={false} placeholder="nombre.apellido" /></div></div>
          <div className="portal-login-field"><label htmlFor="portal-password">Contraseña</label><div className="login-password-field"><KeyRound/><input id="portal-password" name="password" type={showPassword ? "text" : "password"} value={password} onChange={event => setPassword(event.target.value)} required autoComplete="current-password" placeholder="Escribe tu contraseña" /><button type="button" onClick={() => setShowPassword(value => !value)} aria-label={showPassword ? "Ocultar contraseña" : "Mostrar contraseña"}>{showPassword ? <EyeOff/> : <Eye/>}</button></div></div>
          {error && <div className="alert portal-login-error"><ShieldCheck/><span><strong>No fue posible iniciar sesión</strong><small>{error}</small><em>Si es tu primer acceso, prueba la contraseña temporal 123456.</em></span></div>}
          <button className="primary" disabled={loading}>{loading ? <LoaderCircle className="spin"/> : <ShieldCheck/>}{loading ? "Verificando acceso..." : "Entrar al sistema"}<ArrowRight/></button>
        </form>
        <div className="login-trust-row"><span><CheckCircle2/> Conexión protegida</span><span><CheckCircle2/> Permisos por rol</span></div>
        <span className="portal-login-security"><i/> Sesión privada · permisos validados por el servidor</span>
      </div>
    </section>
  </main>;
}
