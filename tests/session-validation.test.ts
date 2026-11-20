import {afterEach, beforeEach, expect, it, vi} from 'vitest';
import {passwordChangeEvent, startSessionValidation} from '../src/lib/session-validation';
let stop: (() => void) | undefined;
let windowEvents: EventTarget, documentEvents: EventTarget;
beforeEach(() => {
  vi.useFakeTimers(); windowEvents = new EventTarget(); documentEvents = new EventTarget();
  vi.stubGlobal('window', Object.assign(windowEvents, {setInterval,clearInterval}));
  vi.stubGlobal('document', Object.assign(documentEvents, {hidden:false}));
  vi.stubGlobal('navigator', {onLine:true});
});
afterEach(() => {stop?.();vi.useRealTimers();vi.unstubAllGlobals();});
it('checks periodically and closes revoked sessions even with a non-JSON 401', async () => {
  vi.stubGlobal('fetch', vi.fn(async()=>new Response('Unauthorized',{status:401})));
  const revoked=vi.fn(); stop=startSessionValidation(revoked);
  await vi.advanceTimersByTimeAsync(15000); expect(revoked).toHaveBeenCalledTimes(1);
});
it('does not log out on server/network errors or check hidden/offline tabs', async () => {
  const fetch=vi.fn(async()=>new Response('Unavailable',{status:503}));vi.stubGlobal('fetch',fetch);
  const revoked=vi.fn(); stop=startSessionValidation(revoked);
  await vi.advanceTimersByTimeAsync(16000); expect(revoked).not.toHaveBeenCalled();
  expect(fetch).toHaveBeenCalledTimes(2); // One bounded retry for the transient 503.
  Object.assign(document,{hidden:true}); await vi.advanceTimersByTimeAsync(15000);expect(fetch).toHaveBeenCalledTimes(2);
  Object.assign(document,{hidden:false});Object.assign(navigator,{onLine:false});await vi.advanceTimersByTimeAsync(15000);expect(fetch).toHaveBeenCalledTimes(2);
});
it('ignores an old validation response during a password change and checks again on focus', async () => {
  let finish!: (value: Response) => void;
  vi.stubGlobal('fetch',vi.fn(()=>new Promise<Response>(resolve=>{finish=resolve})));
  const revoked=vi.fn();stop=startSessionValidation(revoked);
  await vi.advanceTimersByTimeAsync(15000);
  windowEvents.dispatchEvent(Object.assign(new Event(passwordChangeEvent),{detail:true}));
  finish(new Response('{}',{status:401}));await vi.advanceTimersByTimeAsync(1);expect(revoked).not.toHaveBeenCalled();
  windowEvents.dispatchEvent(Object.assign(new Event(passwordChangeEvent),{detail:false}));
  vi.stubGlobal('fetch',vi.fn(async()=>Response.json({authenticated:false})));
  windowEvents.dispatchEvent(new Event('focus'));await vi.advanceTimersByTimeAsync(1);expect(revoked).toHaveBeenCalledTimes(1);
});
