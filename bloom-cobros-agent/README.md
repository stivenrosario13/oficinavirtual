# Bloom Cobros Agent

Aplicación de escritorio Windows, independiente del sistema principal. Importa clientes desde `.xlsx` o `.csv`, clasifica sus cuentas, compara la importación con la anterior y prepara campañas con controles de consentimiento y exclusión.

## Alcance seguro de esta primera versión

- Guarda los datos únicamente en `%LocalAppData%\BloomCobrosAgent`.
- No se conecta a la base de datos del sistema principal.
- El modo temporal abre una conversación individual con el mensaje preparado; el operador confirma el envío.
- Solo prepara borradores para clientes con consentimiento explícito.
- Excluye “No contactar” y bloquea los registros “Legal”.
- Integra WhatsApp Cloud API y almacena el token cifrado con la protección del usuario de Windows.
- Incluye simulación, prueba de conexión, límites por campaña, pausa configurable, cancelación y bitácora.
- Permite registrar individualmente el consentimiento del cliente, con fecha, origen y entrada de auditoría.
- Incluye búsqueda instantánea por contrato, nombre, cédula/identificación y teléfono.
- Separa la preparación de campaña en dos pasos: **Filtrar** muestra la lista final y **Enviar filtrados** inicia el envío únicamente después de revisarla.
- Permite registrar permisos masivos sobre la lista visible con confirmación explícita y auditoría individual.
- Interfaz oscura futurista con panel operativo, estados destacados y tabla de alta legibilidad.

## Columnas reconocidas

Los nombres admiten variantes. Las principales son: `id`, `contrato`, `nombre`, `apellido`, `telefono`, `balance`, `mensualidad`, `fecha de pago`, `estatus`, `sector`, `localidad`, `consentimiento whatsapp` y `no contactar`.

El consentimiento acepta: `sí`, `yes`, `true`, `1` o `autorizado`.

## Ejecutar

```powershell
dotnet run --project .\BloomCobrosAgent.csproj
```

## Crear ejecutable para Windows

```powershell
.\publicar.ps1
```

El resultado queda en `dist\win-x64`.

## Instalar desde CMD

La distribución instalable incluye `INSTALAR.cmd`, `ABRIR.cmd` y `DESINSTALAR.cmd`.
El instalador no requiere permisos de administrador: copia la aplicación a
`%LOCALAPPDATA%\BloomCobrosAgent\App` y crea accesos directos en el Escritorio y menú Inicio.

## Conectar WhatsApp Cloud API

1. En Meta for Developers crea o selecciona una app con WhatsApp.
2. Obtén el `Phone Number ID` y un token permanente con los permisos correspondientes.
3. Crea y consigue aprobación para una plantilla cuyo cuerpo use dos parámetros, en este orden:
   nombre del cliente y balance formateado.
4. En la aplicación abre el botón de engranaje, introduce esos valores y usa **Probar conexión**.
5. Mantén **Modo simulación** activo para la primera prueba. Al confirmar el resultado, desactívalo.

La versión de Graph API es configurable porque Meta retira versiones periódicamente. El valor inicial es `v23.0`.

## Modo temporal Playwright sin API

Selecciona un cliente elegible y pulsa **Playwright asistido**. La aplicación abre Microsoft Edge con
un perfil persistente, carga WhatsApp Web y deja el destinatario y el texto preparados. La primera vez
deberás vincular la sesión con el código QR. El operador revisa y pulsa Enviar.

Playwright no pulsa automáticamente el botón de envío. Los envíos automáticos por lotes se realizan
exclusivamente mediante Cloud API, con plantilla aprobada, consentimiento y las salvaguardas de la app.

## Verificación

```powershell
dotnet run --project .\tests\BloomCobrosAgent.SmokeTests.csproj -c Release
```
