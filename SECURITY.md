# Seguridad operativa de Real Agencias

La aplicación aplica autenticación por cookie, autorización por rol y permisos,
aislamiento por grupos, limitación de solicitudes, protección contra solicitudes
entre sitios, cabeceras defensivas, validación de firmas de archivos y consultas
SQL parametrizadas.

## Requisitos de producción

1. Publicar exclusivamente mediante HTTPS y redirigir HTTP a HTTPS desde IIS o
   el proveedor de alojamiento. HSTS se activa automáticamente en solicitudes
   HTTPS.
2. Configurar `APP_SECRET` con al menos 32 bytes aleatorios. No reutilizarlo en
   otro sistema ni guardarlo dentro de `wwwroot`.
3. Configurar `ADMIN_EMAIL` y `ADMIN_PASSWORD` únicamente como acceso de
   recuperación. La contraseña debe ser aleatoria, única y almacenarse en el
   gestor de secretos del proveedor.
4. Usar una cuenta SQL exclusiva con los permisos mínimos necesarios. Mantener
   `Encrypt=True`; en producción debe usarse un certificado válido y
   `TrustServerCertificate=False` cuando el proveedor lo permita.
5. Para el servidor de licencias, usar una `LICENSE_ADMIN_KEY` aleatoria de al
   menos 32 caracteres y proteger la clave privada ECDSA fuera del sitio web.
6. Mantener `LICENSE_ENFORCEMENT_ENABLED=true` después de validar la conexión
   con el servidor central.
7. Restringir el panel del servidor de licencias por firewall o allowlist de IP
   cuando el alojamiento lo permita.
8. Conservar copias de seguridad cifradas de SQL Server y probar su restauración
   periódicamente.

## Mantenimiento

- Ejecutar antes de cada publicación: `npm audit --omit=dev`,
  `dotnet list api/RegistroAgencias.Api.csproj package --vulnerable --include-transitive`
  y el mismo comando para `license-server/LicenseServer.csproj`.
- Revisar intentos de acceso, respuestas 401/403/429, cambios de usuarios y el
  registro de auditoría. Una subida repentina puede indicar abuso.
- Revocar de inmediato credenciales o claves expuestas y rotar también las
  sesiones relacionadas.
- Aplicar actualizaciones del sistema operativo, IIS, .NET y SQL Server dentro
  de una ventana de mantenimiento regular.
- No registrar contraseñas, cookies, claves, fotografías ni cadenas de conexión.

## Respuesta ante incidentes

1. Aislar la instancia afectada sin destruir registros.
2. Rotar `APP_SECRET`, credenciales administrativas, usuario SQL y claves de
   licenciamiento según el alcance del incidente.
3. Invalidar sesiones, revisar el registro de auditoría y conservar una copia
   forense de los logs.
4. Restaurar desde una copia verificada y corregir la causa antes de reabrir el
   acceso.

Las defensas de aplicación reducen el riesgo, pero deben complementarse con
HTTPS, firewall, copias de seguridad, monitoreo y actualización continua.
