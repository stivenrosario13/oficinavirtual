const MAX_SIDE = 1600;
const PREPARATION_TIMEOUT_MS = 15000;

// A timed-out native decoder may still complete. Dispose of that late result too.
function bounded<T>(work: Promise<T>, disposeLate?: (value: T) => void): Promise<T> {
  return new Promise((resolve, reject) => {
    let expired = false;
    const timer = setTimeout(() => {
      expired = true;
      reject(new Error("La cámara tardó demasiado en preparar la foto. Intenta de nuevo con una fotografía de menor resolución."));
    }, PREPARATION_TIMEOUT_MS);
    work.then(value => {
      clearTimeout(timer);
      if (expired) disposeLate?.(value);
      else resolve(value);
    }, error => { clearTimeout(timer); if (!expired) reject(error); });
  });
}

export async function optimizeCameraPhoto(original: File): Promise<{ file: File; optimized: boolean }> {
  if ((!original.type.startsWith("image/") && !/\.(jpe?g|png|webp|heic|heif)$/i.test(original.name)) ||
      original.size <= 0 || original.size > 25 * 1024 * 1024) {
    throw new Error("Selecciona una fotografía válida de hasta 25 MB.");
  }
  let bitmap: ImageBitmap | undefined;
  let image: HTMLImageElement | undefined;
  let sourceUrl: string | undefined;
  let canvas: HTMLCanvasElement | undefined;
  try {
    // Ask the decoder for a smaller bitmap. Omitting height preserves aspect ratio.
    // Older browsers and formats unsupported by ImageBitmap use the image element.
    if (typeof createImageBitmap === "function") {
      bitmap = await bounded(createImageBitmap(original, { resizeWidth: 1280, resizeQuality: "high" })
        .catch(() => undefined), value => value?.close());
    }
    if (!bitmap) {
      sourceUrl = URL.createObjectURL(original);
      image = new Image();
      const target = image;
      await bounded(new Promise<void>((resolve, reject) => {
        target.onload = () => resolve();
        target.onerror = () => reject(new Error("No se pudo leer la foto. Usa una imagen JPG, PNG o WEBP compatible con este dispositivo."));
        target.src = sourceUrl!;
      }));
    }
    const source = bitmap || image!;
    const width = bitmap ? bitmap.width : image!.naturalWidth;
    const height = bitmap ? bitmap.height : image!.naturalHeight;
    if (!width || !height) throw new Error("La fotografía no tiene dimensiones válidas.");
    const scale = Math.min(1, MAX_SIDE / Math.max(width, height));
    canvas = document.createElement("canvas");
    canvas.width = Math.max(1, Math.round(width * scale));
    canvas.height = Math.max(1, Math.round(height * scale));
    const context = canvas.getContext("2d");
    if (!context) throw new Error("No hay memoria disponible para preparar la foto. Cierra otras aplicaciones e inténtalo de nuevo.");
    context.drawImage(source, 0, 0, canvas.width, canvas.height);
    // The canvas now owns the reduced pixels; release the full source immediately.
    bitmap?.close(); bitmap = undefined;
    if (image) { image.onload = null; image.onerror = null; image.src = ""; }
    const target = canvas;
    const blob = await bounded(new Promise<Blob | null>(resolve => target.toBlob(resolve, "image/jpeg", .8)));
    if (!blob || blob.size > 4 * 1024 * 1024) throw new Error("No se pudo reducir la foto. Intenta con una fotografía de menor resolución.");
    const name = original.name.replace(/\.[^.]+$/, "") || "foto";
    return { file: new File([blob], `${name}.jpg`, { type: "image/jpeg", lastModified: original.lastModified }), optimized: true };
  } finally {
    bitmap?.close();
    if (image) { image.onload = null; image.onerror = null; image.src = ""; }
    if (canvas) { canvas.width = 0; canvas.height = 0; }
    if (sourceUrl) URL.revokeObjectURL(sourceUrl);
  }
}
