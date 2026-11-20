# Bloom Cobros — arquitectura n8n

Paquete de workflows para campañas mediante la API oficial de WhatsApp. Ningún workflow automatiza WhatsApp Web.

## Archivos

1. `01-campana-whatsapp-cloud-api.json`: recibe clientes, valida, deduplica, envía uno por uno, reintenta y continúa ante fallos individuales.
2. `02-estados-whatsapp.json`: usa el trigger oficial de n8n para recibir estados de Meta (`sent`, `delivered`, `read`, `failed`) y mensajes entrantes.
3. `03-manejador-global-errores.json`: captura errores no controlados del workflow completo.
4. `ejemplo-campana.json`: cuerpo de ejemplo para probar el webhook.

## Variables de n8n

Crea estas variables en n8n antes de activar el workflow:

| Variable | Ejemplo | Uso |
|---|---|---|
| `WA_GRAPH_VERSION` | `v23.0` | Versión vigente de Graph API |
| `WA_PHONE_NUMBER_ID` | `123456789` | Phone Number ID de Meta |
| `WA_TEMPLATE_NAME` | `recordatorio_factura` | Plantilla aprobada |
| `WA_TEMPLATE_LANGUAGE` | `es` | Idioma aprobado |
| `WA_DELAY_SECONDS` | `3` | Pausa entre destinatarios |

Después de importar, abre **Enviar plantilla oficial** y selecciona una credencial **Header Auth**:

- Name: `Authorization`
- Value: `Bearer TU_TOKEN_PERMANENTE`

n8n cifra las credenciales; no escribas el token en Variables, nodos Code, expresiones ni archivos exportados. Si tu plan no permite Variables, reemplaza únicamente los valores no secretos (`Phone Number ID`, versión, plantilla e idioma) directamente en el nodo.

## Importación y activación

1. Importa los tres archivos JSON desde **Workflows → Import from file**.
2. Crea las variables no secretas anteriores y selecciona la credencial cifrada en el nodo de envío.
3. En el workflow principal, prueba primero con el webhook de prueba y un solo cliente.
4. Confirma en la ejecución que `outcome` sea `ACCEPTED` y exista `message_id`.
5. Configura el workflow `Bloom | Manejador global de errores` como Error Workflow del principal.
6. En el workflow de estados selecciona una credencial **WhatsApp OAuth2**; el trigger oficial administra el webhook con Meta.
7. Añade un nodo PostgreSQL, SQL Server o Data Table después de **Auditar resultado**, **Registrar omitidos** y **Salida para auditoría** para persistencia permanente.
8. Activa los workflows solo después de probar la plantilla aprobada.

## Validaciones incorporadas

- Campaña vacía.
- Identificador o contrato ausente.
- Teléfono inválido y normalización a formato internacional.
- Balance vacío, inválido, cero o negativo.
- Consentimiento de WhatsApp ausente.
- Lista `No contactar`.
- Estado legal bloqueado.
- Teléfonos duplicados dentro de la campaña.
- Texto limpio de caracteres de control.
- Reintentos de solicitudes fallidas: cuatro intentos con cinco segundos entre intentos.
- Respuesta HTTP no exitosa convertida en `FAILED_CONTINUED`; el siguiente cliente continúa.
- Procesamiento individual y pausa configurable para controlar el ritmo.

## Regla de identidad

El workflow usa teléfono normalizado como destino y contrato/ID para auditoría. El nombre personaliza la plantilla, pero nunca se utiliza para elegir el destinatario: dos personas pueden compartir el mismo nombre.

## Persistencia recomendada

Para producción conecta una base de datos y usa una restricción única sobre `(campaign_id, phone)` o una clave de idempotencia equivalente. Conserva `message_id`, estado, error, hora, contrato y teléfono. Esto evita duplicados al reintentar ejecuciones.
