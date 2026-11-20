import { afterEach, describe, expect, it, vi } from 'vitest';
import { createApiFetch } from '../src/lib/api-fetch';

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });
afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); });

describe('shared finite API transport', () => {
  it('shares concurrent reads but never caches a completed result', async () => {
    const native = vi.fn(async () => json({ value: 1 }));
    const api = createApiFetch(native);
    const [a, b] = await Promise.all([api('/api/tickets'), api('/api/tickets')]);
    expect(await a.json()).toEqual({ value: 1 }); expect(await b.json()).toEqual({ value: 1 });
    expect(native).toHaveBeenCalledTimes(1);
    await api('/api/tickets'); expect(native).toHaveBeenCalledTimes(2);
  });
  it('preserves response status and does not retry failed writes', async () => {
    const native = vi.fn(async () => json({ error: 'Sin permiso' }, 403));
    const api = createApiFetch(native);
    const response = await api('/api/tickets', { method: 'POST', body: '{}' });
    expect(response.status).toBe(403); expect(await response.json()).toEqual({ error: 'Sin permiso' });
    expect(native).toHaveBeenCalledTimes(1);
  });
  it('ends stalled headers even if a fetch implementation ignores cancellation', async () => {
    vi.useFakeTimers(); const native = vi.fn(() => new Promise<Response>(() => {}));
    const api = createApiFetch(native, { readTimeoutMs: 40 });
    const result = expect(api('/api/tickets')).rejects.toMatchObject({ code: 'REQUEST_TIMEOUT' });
    await vi.advanceTimersByTimeAsync(40); await result;
    expect(vi.getTimerCount()).toBe(0);
  });
  it('includes a stalled JSON response body in the deadline and releases its slot', async () => {
    vi.useFakeTimers();
    const native = vi.fn().mockResolvedValueOnce(new Response(new ReadableStream(), { headers: { 'content-type': 'application/json' } })).mockResolvedValue(json({ ok: true }));
    const api = createApiFetch(native, { readTimeoutMs: 40, maxReads: 1 });
    const result = expect(api('/api/stalled')).rejects.toThrow('consulta');
    await vi.advanceTimersByTimeAsync(40); await result;
    expect(await (await api('/api/retry')).json()).toEqual({ ok: true });
  });
  it('bounds concurrent distinct reads while allowing writes to bypass the read queue', async () => {
    const finish: Array<() => void> = [];
    const native = vi.fn((_url, init) => init.method === 'POST' ? Promise.resolve(json({ saved: true })) : new Promise<Response>(resolve => finish.push(() => resolve(json({ ok: true })))));
    const api = createApiFetch(native, { maxReads: 2 });
    const reads = ['/api/a', '/api/b', '/api/c'].map(url => api(url));
    await Promise.resolve(); await Promise.resolve(); expect(native).toHaveBeenCalledTimes(2);
    await api('/api/save', { method: 'POST' }); expect(native).toHaveBeenCalledTimes(3);
    finish[0](); await reads[0]; await Promise.resolve(); expect(native).toHaveBeenCalledTimes(4);
    finish[1](); finish[2](); await Promise.all(reads);
  });
  it('cancels a queued read without ever sending it', async () => {
    const native = vi.fn(() => new Promise<Response>(() => {}));
    const api = createApiFetch(native, { maxReads: 1 });
    const firstController = new AbortController(), secondController = new AbortController();
    const first = expect(api('/api/a', { signal: firstController.signal })).rejects.toMatchObject({ name: 'AbortError' });
    const second = expect(api('/api/b', { signal: secondController.signal })).rejects.toMatchObject({ name: 'AbortError' });
    await Promise.resolve(); await Promise.resolve();
    secondController.abort(); await second; firstController.abort(); await first;
    expect(native).toHaveBeenCalledTimes(1);
  });
  it('does not reuse reads started before a mutation or across different request headers', async () => {
    const native = vi.fn(async () => json({ ok: true }));
    const api = createApiFetch(native);
    await Promise.all([api('/api/a'), api('/api/a', { headers: { 'X-Scope': 'other' } }), api('/api/logout', { method: 'POST' }), api('/api/a')]);
    expect(native).toHaveBeenCalledTimes(4);
  });
  it('reports uncertain writes without resending them', async () => {
    vi.useFakeTimers(); const native = vi.fn(() => new Promise<Response>(() => {}));
    const api = createApiFetch(native, { writeTimeoutMs: 50 });
    const result = expect(api('/api/save', { method: 'POST' })).rejects.toThrow('comprobar si se guardó');
    await vi.advanceTimersByTimeAsync(50); await result; expect(native).toHaveBeenCalledTimes(1);
  });
  it('preserves 204 and streamed download bytes', async () => {
    const native = vi.fn().mockResolvedValueOnce(new Response(null, { status: 204 })).mockResolvedValueOnce(new Response(new Uint8Array([1,2,3]), { headers: { 'content-type': 'application/pdf' } }));
    const api = createApiFetch(native);
    expect((await api('/api/save', { method: 'DELETE' })).status).toBe(204);
    expect(new Uint8Array(await (await api('/api/document/file')).arrayBuffer())).toEqual(new Uint8Array([1,2,3]));
  });
  it('ends stalled non-JSON response bodies too', async () => {
    vi.useFakeTimers();
    const api = createApiFetch(vi.fn(async () => new Response(new ReadableStream(), { headers: { 'content-type': 'text/html' } })), { readTimeoutMs: 40 });
    const response = await api('/api/file');
    const result = expect(response.text()).rejects.toThrow('consulta');
    await vi.advanceTimersByTimeAsync(40); await result;
  });
  it('does not clone or retain unexpected binary responses in the read-sharing path', async () => {
    const native = vi.fn(async () => new Response(new Uint8Array([4,5,6]), {headers: {'content-type':'application/pdf'}}));
    const api = createApiFetch(native);
    const [a,b] = await Promise.all([api('/api/attachment'),api('/api/attachment')]);
    expect(new Uint8Array(await a.arrayBuffer())).toEqual(new Uint8Array([4,5,6]));
    expect(new Uint8Array(await b.arrayBuffer())).toEqual(new Uint8Array([4,5,6]));
    expect(native).toHaveBeenCalledTimes(2);
  });
  it('leaves external URLs and live streams untouched', async () => {
    const native = vi.fn(async () => new Response('external'));
    const api = createApiFetch(native);
    await api('https://example.test/api/external'); await api('/api/live/stream');
    expect(native.mock.calls[0][1]).toBeUndefined(); expect(native.mock.calls[1][1]).toBeUndefined();
  });
});
