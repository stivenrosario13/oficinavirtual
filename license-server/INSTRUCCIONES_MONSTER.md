# Servidor central de licencias en MonsterASP

Este proyecto debe instalarse como un sitio ASP.NET Core independiente del sistema cliente, idealmente en `licencias.tudominio.com`.

1. Cree una base de datos SQL Server independiente y ejecute `001_license_server.sql`.
2. Suba y extraiga el ZIP del servidor dentro de la raíz del nuevo sitio.
3. Configure en el panel las variables `ConnectionStrings__DefaultConnection`, `LICENSE_ADMIN_KEY` y `LICENSE_PRIVATE_KEY_BASE64`.
4. Reinicie el sitio y abra `/api/health`.
5. Abra la raíz del dominio para acceder al panel maestro.
6. Cree la licencia con el dominio exacto del sistema cliente.
7. Copie al sitio cliente la clave generada y la clave pública correspondiente.
8. Solo después de validar `/api/license/status`, cambie `LICENSE_ENFORCEMENT_ENABLED=true` en el cliente.

La clave privada nunca debe configurarse en el sistema cliente. La clave administrativa debe tener al menos 32 caracteres aleatorios.
