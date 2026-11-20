import {it,expect} from 'vitest';
import {renderToStaticMarkup} from 'react-dom/server';
import MaintenanceOverview from '../src/components/vite/MaintenanceOverview';
import {warehouseRequestDepartments,requestDepartmentChoices} from '../src/lib/warehouse-request-departments';
import {administrativeDepartmentName} from '../src/lib/administrative-requests';
it('shows all department cards while retaining inventory access',()=>{
 const html=renderToStaticMarkup(<MaintenanceOverview area="WAREHOUSE" movements={[]} loading={false} error="" onRefresh={()=>{}} onDepartment={()=>{}} onPanel={()=>{}} onDocument={()=>{}}/>);
 for(const name of warehouseRequestDepartments)expect(html).toContain('Abrir '+name);
 expect(html).toContain('Ver inventario');
});
it('maps every department code for filtering its requests',()=>{
 const codes=['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','ADMINISTRATIVE','WAREHOUSE','WORKSHOP','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','SUPERVISION','TECHNICAL_FAILURE','CALL_CENTER'];
 expect(new Set(codes.map(administrativeDepartmentName))).toEqual(new Set(warehouseRequestDepartments));
});
it('does not crash on historical requests missing department',()=>{
 expect(requestDepartmentChoices([{}, {department:null},{department:' Otra área '}])).toContain('Otra área');
});
