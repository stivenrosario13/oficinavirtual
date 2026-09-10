import { Camera, Check, ClipboardCheck, MapPin, Navigation, RotateCcw, ShieldCheck } from "lucide-react";

type Geo = { latitude: number; longitude: number; accuracyMeters: number; capturedAt: string; source: string };
type Result = {
  profileId: string;
  uploadToken: string;
  geolocation?: Geo;
  address?: { direccion: string; sector: string; municipio: string; provincia: string };
  distanceMeters?: number | null;
  photosUploaded?: number;
  submittedAt?: string;
  locationSpreadMeters?: number;
  bestPhotoTitle?: string;
};

export default function EvidenceSuccess({ result, reset }: { result: Result; reset: () => void }) {
  const geo = result.geolocation;
  const address = result.address;
  const mapUrl = geo ? `https://www.openstreetmap.org/export/embed.html?bbox=${geo.longitude - .0018}%2C${geo.latitude - .0012}%2C${geo.longitude + .0018}%2C${geo.latitude + .0012}&layer=mapnik&marker=${geo.latitude}%2C${geo.longitude}` : "";
  return <section className="atlas-success">
    <header><span className="success-orbit"><Check /></span><div><small>Auditoría finalizada</small><h2>Levantamiento guardado al 100%</h2><p>Los datos, las siete respuestas y {result.photosUploaded || 3} fotografías georreferenciadas quedaron vinculados al mismo registro.</p></div><img src="/loto-real-logo-transparent.png" alt="Loto Real" /></header>
    <div className="success-atlas-grid">
      <div className="success-map">{geo ? <iframe title="Mapa final de la auditoría" src={mapUrl} loading="lazy" /> : <Navigation />}</div>
      <div className="success-receipt">
        <span className="receipt-status"><ShieldCheck /> EVIDENCIA CERTIFICADA</span>
        <article><MapPin /><div><small>Dirección del levantamiento</small><strong>{address?.direccion || "Dirección registrada"}</strong><span>{[address?.sector, address?.municipio, address?.provincia].filter(Boolean).join(" · ")}</span></div></article>
        <div className="receipt-values"><span><small>Latitud exacta</small><strong>{geo?.latitude.toFixed(6) || "—"}</strong></span><span><small>Longitud exacta</small><strong>{geo?.longitude.toFixed(6) || "—"}</strong></span><span><small>Mejor precisión</small><strong>{geo ? `±${Math.round(geo.accuracyMeters)} m` : "—"}</strong></span><span><small>Coincidencia de fotos</small><strong>{result.locationSpreadMeters == null ? "Verificada" : `${Math.round(result.locationSpreadMeters)} m`}</strong></span><span><small>Lectura seleccionada</small><strong>{result.bestPhotoTitle || "Mayor precisión"}</strong></span><span><small>Distancia a banca</small><strong>{result.distanceMeters == null ? "Sin referencia" : `${Math.round(result.distanceMeters)} m`}</strong></span></div>
        <div className="receipt-checks"><span><Camera /><strong>{result.photosUploaded || 3}</strong> fotos GPS</span><span><ShieldCheck /><strong>3/3</strong> fotos base coinciden</span><span><ClipboardCheck /><strong>7/7</strong> verificaciones</span></div>
      </div>
    </div>
    <button className="primary success-reset" onClick={reset}><RotateCcw /> Registrar otra banca</button>
  </section>;
}
