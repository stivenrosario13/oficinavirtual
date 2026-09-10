# Registro de Agencias — Grupo Tejeda

Aplicación React + Vite con backend ASP.NET Core 8 y SQL Server 2025, preparada para MonsterASP.NET.

## Desarrollo local

1. Configura `ConnectionStrings__DefaultConnection` con una base SQL Server.
2. Ejecuta en orden los scripts de `database-mssql` que correspondan a la instalación, incluido `029_notification_receipts.sql` para sincronizar el estado leído de las notificaciones por usuario.
3. Configura `APP_SECRET` con al menos 32 caracteres.
4. Ejecuta `npm install` y `npm run dev`.

## Datos vigentes

- 3,122 agencias activas importadas del archivo actualizado de LTK.
- 161 grupos activos.
- Las agencias que desaparecieron del archivo se marcan inactivas para conservar auditorías históricas.
- 74 administradores de grupo existentes; 141 grupos tienen asignación exacta y 20 quedan reservados al administrador principal para revisión.

## Formulario de campo

- Tres fotografías base verifican que el empleado se encuentre en la misma banca y seleccionan la lectura GPS más precisa.
- Siete preguntas estructurales.
- “¿Tiene inversor?” y “¿Tiene batería?” admiten Sí, No y No aplica.
- Responder Sí abre la cámara y exige una evidencia adicional con coordenadas, precisión y hora.

## Publicación para MonsterASP.NET

```powershell
npm run publish:monster
```

El comando genera:

- `artifacts/01_CONFIGURACION_FUERA_DE_WWWROOT.zip`: SQL, variables, instrucciones y archivos confidenciales. No se publica en la web.
- `artifacts/02_SUBIR_DENTRO_DE_WWWROOT_V2.zip`: aplicación compilada para extraer directamente dentro del `wwwroot` exterior.
- `artifacts/REGISTRO_AGENCIAS_MONSTER_COMPLETO.zip`: copia completa de ambas carpetas para archivo interno.

Consulta `deploy/INSTRUCCIONES_DESPLIEGUE.md` para el orden exacto de instalación y verificación.

## Seguridad

- Todos los usuarios inician sesión antes de consultar grupos o agencias.
- Los administradores de grupo solo reciben desde SQL Server las agencias y estadísticas asignadas.
- Las fotografías se almacenan en SQL Server y solo se sirven a sesiones autorizadas.
- Los secretos se configuran exclusivamente en las variables del panel de MonsterASP.NET.
