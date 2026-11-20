# Arquitectura operativa

```text
Aplicación Windows / sistema exportador
                 │
                 │ POST /webhook/bloom-cobros/campaign
                 ▼
        ┌───────────────────────┐
        │ Entrada segura n8n    │
        └───────────┬───────────┘
                    ▼
        ┌───────────────────────┐
        │ Validar y normalizar  │
        │ identidad, teléfono,  │
        │ saldo y consentimiento│
        └───────┬─────────┬─────┘
                │ válido  │ omitido
                ▼         ▼
       ┌──────────────┐  Bitácora de exclusiones
       │ Deduplicación│
       └──────┬───────┘
              ▼
       Lote individual → Cloud API → Auditar → Esperar → siguiente
                                      │
                                      ▼
                            message_id / error

Meta Webhook → normalizar sent/delivered/read/failed → base de auditoría

Error Trigger → incidente global → alerta operativa
```

La campaña continúa ante errores de destinatario. Solo un error estructural del workflow llega al manejador global.
