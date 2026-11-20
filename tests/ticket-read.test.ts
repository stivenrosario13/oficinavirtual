import {afterEach, describe, expect, it, vi} from 'vitest';
import {loadTicketResources, readTicketResource} from '../src/lib/ticket-read';
import {ticketAssignedToSession} from '../src/lib/supervisor-support';

afterEach(() => vi.useRealTimers());
describe('supervisor ticket loading', () => {
  it('matches legacy email assignments without confusing names or empty identities', () => {
    const session = {id: 'supervisor-1', email: 'ana@example.test'};
    expect(ticketAssignedToSession({assignedTechnicianId: ' ANA@example.test '}, session)).toBe(true);
    expect(ticketAssignedToSession({assignedTechnicianId: 'supervisor-1'}, session)).toBe(true);
    expect(ticketAssignedToSession({assignedTechnicianId: 'ana@another.test'}, session)).toBe(false);
    expect(ticketAssignedToSession({assignedTechnicianId: ''}, {})).toBe(false);
    expect(ticketAssignedToSession({isAssignedToCurrentUser: true, assignedTechnicianId: 'old-id'}, session)).toBe(true);
    expect(ticketAssignedToSession({isAssignedToCurrentUser: false, assignedTechnicianId: session.id}, session)).toBe(false);
  });
  it('publishes successful resources even if another fails or remains pending', async () => {
    const events: string[] = []; const failed = vi.fn(); let finish!: () => void;
    const pending = new Promise<void>(resolve => {finish = resolve});
    const all = loadTicketResources([
      async () => { await pending; events.push('slow'); },
      async () => { events.push('catalog'); },
      async () => { throw new Error('directory offline'); },
    ], failed);
    await Promise.resolve(); await Promise.resolve();
    expect(events).toEqual(['catalog']); expect(failed).toHaveBeenCalledTimes(1);
    finish(); await all; expect(events).toEqual(['catalog', 'slow']);
  });
  it('ends a stalled request and permits the next refresh to succeed', async () => {
    vi.useFakeTimers();
    const stalled = vi.fn((_path, options) => new Promise((_resolve, reject) => {
      options.signal.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')));
    })) as unknown as typeof fetch;
    const request = readTicketResource('/api/tickets', stalled, 50);
    const result = expect(request).rejects.toThrow('Actualizar ahora');
    await vi.advanceTimersByTimeAsync(50); await result;
    const next = vi.fn(async () => new Response(JSON.stringify({tickets: [{id: 'assigned'}]}))) as unknown as typeof fetch;
    expect(await readTicketResource('/api/tickets', next)).toEqual({tickets: [{id: 'assigned'}]});
    expect(vi.getTimerCount()).toBe(0);
  });
  it('preserves session errors and rejects invalid responses instead of showing an empty inbox', async () => {
    const unauthorized = vi.fn(async () => new Response(JSON.stringify({error: 'Sesión vencida'}), {status: 401})) as unknown as typeof fetch;
    await expect(readTicketResource('/api/tickets', unauthorized)).rejects.toMatchObject({status: 401});
    const html = vi.fn(async () => new Response('<html>error</html>')) as unknown as typeof fetch;
    await expect(readTicketResource('/api/tickets', html)).rejects.toThrow();
  });
});
it('preserves authentication status for non-JSON errors and rejects corrupt success bodies',async()=>{
 await expect(readTicketResource('/api/test',vi.fn(async()=>new Response('Unauthorized',{status:401})))).rejects.toMatchObject({status:401});
 await expect(readTicketResource('/api/test',vi.fn(async()=>new Response('<html>Not JSON</html>',{status:200})))).rejects.toThrow('respuesta no válida');
});
