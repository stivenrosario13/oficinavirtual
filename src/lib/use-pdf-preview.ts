import {useEffect, useRef, useState, type RefObject} from 'react';
import {apiFetch} from './api-fetch';

type RenderTask = {promise: Promise<unknown>; cancel: () => void};
type PdfPage = {getViewport: (options: {scale: number}) => {width: number; height: number}; render: (options: object) => RenderTask; cleanup: () => void};
type PdfDocument = {numPages: number; getPage: (page: number) => Promise<PdfPage>};

/** One parser/worker per open document; changing pages only renders that page. */
export function usePdfPreview(source: Uint8Array | string | null, page: number, canvas: RefObject<HTMLCanvasElement | null>, scale: number) {
  const [document, setDocument] = useState<PdfDocument | null>(null);
  const [count, setCount] = useState(1), [loading, setLoading] = useState(false), [error, setError] = useState('');
  const [renderedWidth, setRenderedWidth] = useState(0), [attempt, setAttempt] = useState(0);
  const activeSource = useRef(source);
  activeSource.current = source;
  useEffect(() => {
    let active = true;
    let destroy: (() => void) | undefined;
    const controller = new AbortController();
    setDocument(null); setCount(1); setError(''); setLoading(!!source);
    if (!source) return;
    const timer = setTimeout(() => {
      active = false; controller.abort(); destroy?.();
      setLoading(false); setError('El documento tardó demasiado en abrir. Puedes reintentar.');
    }, 20000);
    void (async () => {
      let data: Uint8Array;
      if (typeof source === 'string') {
        const response = await apiFetch(source, {credentials: 'same-origin', cache: 'no-store', signal: controller.signal});
        if (!response.ok) throw new Error('No fue posible cargar el archivo. Revisa tu sesión o actualiza la biblioteca.');
        data = new Uint8Array(await response.arrayBuffer());
      } else data = source.slice(); // PDF.js transfers ownership to its worker.
      if (!active) return;
      const path = '/vendor/pdfjs/pdf.mjs';
      const pdfjs = await import(/* @vite-ignore */ path);
      if (!active) return;
      pdfjs.GlobalWorkerOptions.workerSrc = '/vendor/pdfjs/pdf.worker.mjs';
      const task = pdfjs.getDocument({data, isEvalSupported: false});
      destroy = () => { void task.destroy().catch(() => {}); };
      const doc: PdfDocument = await task.promise;
      if (!active) return;
      clearTimeout(timer); setCount(doc.numPages); setDocument(doc);
    })().catch(reason => {
      if (active) { clearTimeout(timer); destroy?.(); setLoading(false); setError((reason as Error).message); }
    });
    return () => { active = false; clearTimeout(timer); controller.abort(); destroy?.(); };
  }, [source, attempt]);

  useEffect(() => {
    if (!document) return;
    const renderSource = source;
    let active = true, render: RenderTask | undefined, pdfPage: PdfPage | undefined;
    const buffer = window.document.createElement('canvas');
    const release = () => { buffer.width = 0; buffer.height = 0; pdfPage?.cleanup(); };
    setLoading(true); setError('');
    const timer = setTimeout(() => {
      active = false; render?.cancel(); release();
      setLoading(false); setError('La página tardó demasiado en dibujarse. Puedes reintentar.');
    }, 20000);
    void (async () => {
      pdfPage = await document.getPage(Math.max(1, Math.min(page, document.numPages)));
      if (!active || activeSource.current !== renderSource) { pdfPage.cleanup(); return; }
      const base = pdfPage.getViewport({scale});
      // Bound canvas memory even for oversized/scanned PDF page dimensions.
      const viewport = pdfPage.getViewport({scale: scale * Math.min(1, 2000 / Math.max(base.width, base.height))});
      buffer.width = viewport.width; buffer.height = viewport.height;
      render = pdfPage.render({canvas: buffer, canvasContext: buffer.getContext('2d'), viewport});
      await render.promise;
      if (!active || activeSource.current !== renderSource || !canvas.current) return;
      canvas.current.width = buffer.width; canvas.current.height = buffer.height;
      canvas.current.getContext('2d')?.drawImage(buffer, 0, 0);
      setRenderedWidth(viewport.width); setLoading(false);
    })().catch(reason => {
      if (active) { setLoading(false); setError((reason as Error).message); }
    }).finally(() => { clearTimeout(timer); release(); });
    return () => { active = false; clearTimeout(timer); render?.cancel(); release(); };
  }, [document, page, source, canvas, scale]);
  useEffect(() => {
    const element = canvas.current;
    return () => { if (element) { element.width = 0; element.height = 0; } };
  }, [canvas]);
  return {count, loading, error, renderedWidth, retry: () => setAttempt(value => value + 1)};
}
