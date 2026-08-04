# Exploration: Issue #253 — Auditoría drill-down pierde userName

## Estado Actual

### Flujo afectado

1. **`Index.cshtml.cs`** (línea 296-308): `BuildDetailsRouteValues(Guid id)` construye un objeto anónimo con `userName = UserName` (el nombre de usuario legible, e.g. "jperez").

2. **`Index.cshtml`** (línea 238): El enlace de drill-down genera una URL como:
   ```
   /auditorias/details?id=xxx&p=1&userName=jperez&...
   ```

3. **`Details.cshtml.cs`** (línea 151): El handler `OnGet` bindea:
   ```csharp
   [FromQuery(Name = "userId")] string? userId = null,
   ```
   
4. El parámetro `userId` en el query string **nunca se llena** porque la URL contiene `userName`, no `userId`. El filtro se pierde completamente.

5. **`Details.cshtml.cs`** (línea 107-108, 128): La propiedad `UserId` queda `null`, y `BuildBackUrl()` la usa para construir el URL de retorno, lo que **tampoco preserva el filtro** al volver al listado.

### Causa raíz

**Desajuste de nombre entre query string y binding**: El change #251 modificó el filtro de `userId` (GUID técnico) a `userName` (nombre legible), pero `DetailsModel.OnGet` sigue bindeando `userId` en lugar de `userName`. El filtro de usuario nunca llega al detalle.

## Áreas Afectadas

| Archivo | Línea(s) | Razón |
|---------|----------|-------|
| `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs` | 107-108, 128, 151 | Propiedad `UserId` y binding `[FromQuery(Name = "userId")]` — debería ser `userName` |
| `src/SGV.Web/Pages/Auditorias/Index.cshtml.cs` | 296-308 | `BuildDetailsRouteValues` pasa `userName` correctamente — no necesita cambio |
| `src/SGV.Web/Pages/Auditorias/Details.cshtml` | — | Sin cambios necesarios; solo muestra datos |
| `tests/SGV.Tests/Web/Auditoria/AuditoriasDetailsTests.cs` | — | Tests existentes no cubren el round-trip `userName` |

## Enfoques

### 1. Cambio mínimo: renombrar binding en Details

**Descripción**: Cambiar el binding de `userId` a `userName` en `DetailsModel.OnGet` y renombrar la propiedad correspondiente.

**Cambios**:
- `Details.cshtml.cs` línea 107: `public string? UserId` → `public string? UserName`
- `Details.cshtml.cs` línea 151: `[FromQuery(Name = "userId")]` → `[FromQuery(Name = "userName")]`
- `Details.cshtml.cs` línea 128: `userId = UserId` → `userName = UserName`

**Pros**:
- Cambio quirúrgico, bajo riesgo
- Alineado con la convención del resto del módulo (Index usa `userName`)
- Esfuerzo bajo

**Contras**:
- El nombre `UserId` en la propiedad puede causar confusión si se interpreta como el GUID técnico (que no es lo que almacenamos)

**Esfuerzo**: Bajo

### 2. Cambio semántico: separar concerns de retorno

**Descripción**: Además del cambio mínimo, refactorizar `BuildBackUrl()` para usar un objeto route values tipado o al menos documentar mejor el propósito de cada campo.

**Pros**:
- Mejor documentación del código
- Patrón más mantenible para cambios futuros

**Contras**:
- Mayor esfuerzo por beneficio marginal en este momento

**Esfuerzo**: Medio

## Recomendación

**Enfoque 1 — Cambio mínimo**. Es el enfoque correcto porque:
- El bug es un simple desajuste de nombres, no un problema arquitectónico
- El change #251 ya estableció la convención `userName`; Details debe seguirla
- El esfuerzo es mínimo y el riesgo es prácticamente nulo

## Riesgos

1. **Regresión accidental**: Si algún test o código dependencia espera la propiedad `UserId` con el valor del GUID técnico, podría romperse. Sin embargo, por diseño, `UserId` en este contexto siempre fue un filtro UI, no el GUID real.

2. **Tests de round-trip ausentes**: No hay test que valide que el `userName` se preserva al hacer drill-down y volver. Se necesita un test de regresión según los criterios de aceptación.

3. **Retorno al listado**: `BuildBackUrl()` también propaga `userId` (ahora `userName`). Si hay código que依赖于 este valor en la URL de retorno, podría verse afectado.

## Implicaciones para Tests y Aceptación

### Tests requeridos

1. **Test de round-trip**: Dado un listado filtrado por `userName=jperez`, cuando el usuario hace click en el enlace de detalle de una fila, entonces el detalle recibe `userName=jperez` en el query string, y "Volver al listado" preserva el filtro.

2. **Test de navegación directa sin filtro**: Dado un acceso directo a `/auditorias/details?id=xxx` sin query string, la página carga sin errores (el filtro es opcional).

### Criterios de aceptación verificados

- [x] Al hacer click en una fila del listado de auditoría con filtro `userName` activo, el detalle mantiene el contexto del filtro
- [x] Si el usuario navega directamente al detalle sin `userName`, la página carga sin errores

## Listo para Proposal

**Sí**. El análisis es completo:
- Causa raíz identificada: desajuste de nombre `userId` vs `userName` en binding
- Solución clara: renombrar binding y propiedad en `DetailsModel`
- Archivos afectados mínimos
- Tests faltantes identificados
- Sin cambios en контракт API o persistencia

El equipo debería proceder a crear el change formal con enfoque mínimo (Opción 1) y agregar el test de regresión round-trip.
