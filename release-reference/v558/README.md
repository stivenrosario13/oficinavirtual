# Interfaz estable V558

Esta carpeta conserva la interfaz original aprobada, los formularios de prestaciones y cheques y la corrección de precarga de CSS con parámetros de versión. `wwwroot` es una referencia compilada; no contiene configuración del servidor ni datos de usuarios.

Para crear el ZIP de interfaz para Monster desde un clon del repositorio:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/package-monster-stable.ps1
```

El ZIP aparece en `outputs`. Actualiza solamente la interfaz; conserva el backend instalado, incluida la corrección de reactivación de usuarios. Detener el sitio, extraer sobre la carpeta publicada confirmando reemplazos e iniciar nuevamente.

Para verificar la interfaz con Playwright en un entorno que lo tenga instalado:

```powershell
$env:QA_REFERENCE='1'
$env:QA_ORIGINAL='1'
$env:QA_RESULT='outputs/v558-package-result.json'
$env:QA_PREFIX='v558'
node scripts/qa-v558-visual.mjs
```

El comando de empaquetado genera el descriptor usado por esta prueba. La prueba comprueba 100 dependencias de precarga, los tres formularios y la consola en escritorio y móvil con una API simulada.

La prueba usa `playwright` instalado localmente. En un entorno con Playwright instalado en otra ubicación se puede definir `PLAYWRIGHT_LOADER` con la ruta absoluta de un archivo desde el que Node pueda resolver ese paquete.

`npm run build` compila el árbol de desarrollo `src`; no reconstruye esta referencia histórica. Para mantener exactamente el diseño aprobado en Monster, usar el empaquetador estable anterior. Los cambios futuros en formularios deben reconciliarse con esta referencia y verificarse antes de publicarlos.
