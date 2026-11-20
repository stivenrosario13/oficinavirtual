import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const workflowPath = path.join(here, '..', '01-campana-whatsapp-cloud-api.json');
const workflow = JSON.parse(fs.readFileSync(workflowPath, 'utf8'));
const validationNode = workflow.nodes.find((node) => node.name === 'Validar y normalizar');
const validate = new Function('$input', validationNode.parameters.jsCode);

const customers = [
  { id: '1', contrato: 'A-1', nombre: ' Ana\u0000 ', telefono: '809-555-0101', balance: 'RD$1,560.00', consentimiento_whatsapp: 'si' },
  { id: '2', contrato: 'A-2', nombre: 'Duplicado', telefono: '8095550101', balance: 500, consentimiento_whatsapp: 'si' },
  { id: '3', contrato: 'A-3', nombre: 'Sin teléfono', telefono: '12', balance: 500, consentimiento_whatsapp: 'si' },
  { id: '4', contrato: 'A-4', nombre: 'Sin permiso', telefono: '8295550104', balance: 500, consentimiento_whatsapp: 'no' },
  { id: '5', contrato: 'A-5', nombre: 'Legal', telefono: '8495550105', balance: 500, consentimiento_whatsapp: 'si', estatus: 'Legal' },
  { id: '6', contrato: 'A-6', nombre: 'Correcto', telefono: '8095550106', balance: 700, consentimiento_whatsapp: 'si' }
];

const result = validate({ first: () => ({ json: { body: { campaign_id: 'test', customers } } }) });
const rows = result.map((item) => item.json);
const assert = (condition, message) => { if (!condition) throw new Error(message); };

assert(rows.length === 6, 'Debe conservar una decisión auditable por cada fila.');
assert(rows[0].reason === 'DUPLICATE_PHONE_IN_CAMPAIGN', 'Debe bloquear ambos teléfonos duplicados.');
assert(rows[1].reason === 'DUPLICATE_PHONE_IN_CAMPAIGN', 'Debe bloquear ambos teléfonos duplicados.');
assert(rows[2].reason === 'INVALID_PHONE', 'Debe detectar teléfono inválido.');
assert(rows[3].reason === 'NO_CONSENT', 'Debe exigir consentimiento.');
assert(rows[4].reason === 'LEGAL_BLOCKED', 'Debe bloquear estado legal.');
assert(rows[5].eligible === true && rows[5].phone === '18095550106', 'Debe normalizar y admitir el registro correcto.');
assert(rows[0].first_name === 'Ana', 'Debe limpiar caracteres de control del nombre.');

console.log('7 validaciones n8n completadas correctamente.');
