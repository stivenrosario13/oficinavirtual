import {afterEach, describe, expect, it, vi} from 'vitest';

afterEach(() => {vi.unstubAllGlobals(); vi.resetModules();});
describe('on-demand assets', () => {
  it('decodes the equipment QR with the dynamically loaded fallback', async () => {
    const [{default: QRCode}, {default: jsQR}] = await Promise.all([import('qrcode'), import('jsqr')]);
    const value = 'REAL-MNT|SOL-ALM-20261024-001';
    const matrix = QRCode.create(value).modules;
    const scale = 6, quiet = 4, width = (matrix.size + quiet * 2) * scale;
    const pixels = new Uint8ClampedArray(width * width * 4).fill(255);
    for (let y = 0; y < width; y++) for (let x = 0; x < width; x++) {
      const row = Math.floor(y / scale) - quiet, col = Math.floor(x / scale) - quiet;
      if (row >= 0 && col >= 0 && row < matrix.size && col < matrix.size && matrix.get(row, col)) {
        const i = (y * width + x) * 4; pixels[i] = pixels[i + 1] = pixels[i + 2] = 0;
      }
    }
    expect(jsQR(pixels, width, width)?.data).toBe(value);
  });
  it('shares public-logo work and permits retry after a failed download', async () => {
    const fetcher = vi.fn().mockResolvedValueOnce(new Response('', {status: 503})).mockResolvedValue(new Response(new Blob(['logo'])));
    vi.stubGlobal('fetch', fetcher);
    vi.stubGlobal('FileReader', class {result = 'data:image/png;base64,bG9nbw=='; onload?: () => void; readAsDataURL(){queueMicrotask(() => this.onload?.());}});
    const {realLogoData} = await import('../src/components/vite/institutionalDocumentAssets');
    await expect(realLogoData()).rejects.toThrow('logo');
    const a = realLogoData(), b = realLogoData();
    expect(a).toBe(b);
    expect(await a).toContain('data:image/png');
    expect(await realLogoData()).toBe(await b);
    expect(fetcher).toHaveBeenCalledTimes(2);
  });
});
