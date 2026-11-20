import React from 'react';
import {readFileSync} from 'node:fs';
import {renderToStaticMarkup} from 'react-dom/server';
import {expect,it} from 'vitest';
import {requestHubCounts} from '../src/lib/request-hub-counts';
import RequestHubCards from '../src/components/vite/RequestHubCards';

it('cuenta pendientes propios por destino sin mezclar departamentos',()=>{
 const row={department:'Administración',isOwner:true,status:'pending',recipientDepartment:'Administración'};
 const counts=requestHubCounts([row,{...row,status:'received'},{...row,status:'approved'},{...row,status:'rejected'},{...row,isOwner:false},{...row,department:'Tecnología'},{...row,recipientDepartment:'Almacén'},{...row,recipientDepartment:'Almacén',status:'correction_requested'}],[{department:'ADMINISTRATIVE',status:'SENT'},{department:'ADMINISTRATIVE',status:'REPAIR'},{department:'ADMINISTRATIVE',status:'DELIVERED'},{department:'TECHNOLOGY',status:'SENT'}],'ADMINISTRATIVE');
 expect(counts).toEqual({administrative:2,warehouse:2,workshop:2});
});
it('mantiene cuatro accesos y no inventa cero durante la carga',()=>{
 const html=renderToStaticMarkup(<RequestHubCards department="ADMINISTRATIVE" name="Administración" canWorkshop onSelect={()=>{}}/>);
 expect(html.match(/data-destination=/g)).toHaveLength(4);
 expect(html).toContain('Actualizando');expect(html).toContain('solicitudes pendientes');
 expect(html).not.toContain('>0</b>');
});
it('conserva Taller visible pero inactivo para Tesorería sin consultar ese módulo',()=>{
 const html=renderToStaticMarkup(<RequestHubCards department="TREASURY" name="Tesorería" canWorkshop={false} onSelect={()=>{}}/>);
 expect(html.match(/data-destination=/g)).toHaveLength(4);
 expect(html).toMatch(/data-destination="workshop" disabled="" aria-disabled="true"/);
 expect(html).toContain('Tu perfil no tiene acceso a Taller');
 const source=readFileSync('src/components/vite/RequestHubCards.tsx','utf8');
 expect(source).toContain("canWorkshop?read<WorkshopCase[]>('/api/workshop'):Promise.resolve([] as WorkshopCase[])");
});
it('oculta el botón de tickets en el modo personal de técnicos y conserva la gestión',()=>{
 const source=readFileSync('src/components/vite/TicketCenter.tsx','utf8');
 const button=source.indexOf('<Boxes /> Solicitar equipo a Almacén');
 expect(source.slice(button-1100,button)).toContain('!strictTechnologyTechnician &&');
 expect(source).toContain('session.supportTeam === "TECHNICIANS" ||');
});
