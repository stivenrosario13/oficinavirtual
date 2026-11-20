import { afterEach, describe, expect, it, vi } from 'vitest';
import { optimizeCameraPhoto } from '../src/lib/camera-photo';
import { readJsonWithTimeout } from '../src/lib/read-json-timeout';

afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); vi.restoreAllMocks(); });
function setup({ context = true, encode = true } = {}) {
  const drawImage = vi.fn();
  const canvas = { width: 0, height: 0, getContext: () => context ? { drawImage } : null,
    toBlob: vi.fn((callback: BlobCallback) => { if (encode) callback(new Blob(['small'], { type: 'image/jpeg' })); }) };
  const bitmap = { width: 1280, height: 2560, close: vi.fn() };
  vi.stubGlobal('document', { createElement: () => canvas });
  vi.stubGlobal('createImageBitmap', vi.fn(async () => bitmap));
  return { canvas, bitmap, drawImage };
}
const photo = () => new File(['photo'], 'camara.jpg', { type: 'image/jpeg' });

describe('camera preparation across browser capabilities', () => {
  it('preserves portrait proportions, bounds output and releases native memory', async () => {
    const { canvas, bitmap, drawImage } = setup();
    const result = await optimizeCameraPhoto(photo());
    expect(result.file.type).toBe('image/jpeg');
    expect(result.file.size).toBe(5);
    expect(drawImage).toHaveBeenCalledWith(bitmap, 0, 0, 800, 1600);
    expect(bitmap.close).toHaveBeenCalledTimes(1);
    expect(canvas.width).toBe(0); expect(canvas.height).toBe(0);
  });
  it('supports browsers without ImageBitmap and releases the source URL', async () => {
    const { drawImage } = setup();
    vi.stubGlobal('createImageBitmap', undefined);
    const revoke = vi.spyOn(URL, 'revokeObjectURL');
    class FallbackImage {
      naturalWidth = 4000; naturalHeight = 3000;
      onload: (() => void) | null = null; onerror: (() => void) | null = null;
      set src(value: string) { if (value) queueMicrotask(() => this.onload?.()); }
    }
    vi.stubGlobal('Image', FallbackImage);
    await optimizeCameraPhoto(photo());
    expect(drawImage).toHaveBeenCalledWith(expect.any(FallbackImage), 0, 0, 1600, 1200);
    expect(revoke).toHaveBeenCalledTimes(1);
  });
  it('releases the bitmap when canvas allocation fails instead of previewing the large original', async () => {
    const { bitmap, canvas } = setup({ context: false });
    await expect(optimizeCameraPhoto(photo())).rejects.toThrow('memoria');
    expect(bitmap.close).toHaveBeenCalledTimes(1);
    expect(canvas.width).toBe(0);
  });
  it('ends a stalled encoder and allows a later photo to succeed', async () => {
    vi.useFakeTimers();
    const { canvas } = setup({ encode: false });
    const result = expect(optimizeCameraPhoto(photo())).rejects.toThrow('tardó demasiado');
    await vi.advanceTimersByTimeAsync(15000); await result;
    expect(canvas.width).toBe(0); expect(vi.getTimerCount()).toBe(0);
    setup(); await expect(optimizeCameraPhoto(photo())).resolves.toMatchObject({ optimized: true });
  });
  it('closes a native decoder result that arrives after its timeout', async () => {
    vi.useFakeTimers();
    const { bitmap } = setup();
    let complete!: (value: typeof bitmap) => void;
    vi.stubGlobal('createImageBitmap', () => new Promise(resolve => { complete = resolve; }));
    const result = expect(optimizeCameraPhoto(photo())).rejects.toThrow('tardó demasiado');
    await vi.advanceTimersByTimeAsync(15000); await result;
    complete(bitmap); await vi.advanceTimersByTimeAsync(0);
    expect(bitmap.close).toHaveBeenCalledTimes(1);
  });
  it('ends a stalled fallback decoder and revokes its object URL', async () => {
    vi.useFakeTimers(); setup(); vi.stubGlobal('createImageBitmap', undefined);
    vi.stubGlobal('Image', class { onload = null; onerror = null; src = ''; });
    const revoke = vi.spyOn(URL, 'revokeObjectURL');
    const result = expect(optimizeCameraPhoto(photo())).rejects.toThrow('tardó demasiado');
    await vi.advanceTimersByTimeAsync(15000); await result;
    expect(revoke).toHaveBeenCalledTimes(1);
  });
});

describe('bounded address lookup', () => {
  it('keeps the timeout active when headers arrive but the JSON body stalls', async () => {
    vi.useFakeTimers();
    vi.stubGlobal('fetch', vi.fn(async (_url, init) => ({ ok: true, json: () => new Promise((_resolve, reject) => {
      init.signal.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')));
    }) })));
    const result = expect(readJsonWithTimeout('/address', 100)).rejects.toThrow('Aborted');
    await vi.advanceTimersByTimeAsync(100); await result;
    expect(vi.getTimerCount()).toBe(0);
  });
  it('clears the timer after a successful response', async () => {
    vi.useFakeTimers(); vi.stubGlobal('fetch', vi.fn(async () => new Response('{"direccion":"Calle 1"}')));
    expect(await readJsonWithTimeout('/address')).toEqual({ direccion: 'Calle 1' });
    expect(vi.getTimerCount()).toBe(0);
  });
});
