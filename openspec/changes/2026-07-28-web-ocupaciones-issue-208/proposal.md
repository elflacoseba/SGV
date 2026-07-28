# Proposal: feat(web): módulo de Ocupaciones — wire-types, cliente, Razor Pages y navegación cruzada

> Issue: [#208](https://github.com/elflacoseba/SGV/issues/208) — feat(web): implementar módulo de Ocupaciones
> Cambio: `2026-07-28-web-ocupaciones-issue-208` (stacked-to-main sobre `develop`, 4 slices anticipadas)
> Modo artefactos: **Both** (OpenSpec + Engram) · Review budget: **400 líneas** por slice

## Contexto

La API `OcupacionesController` ya expone el ciclo CRUD completo: `GET` paginado (con `?includeHistory=true|false`), `GET/{id}`, `POST`, `PUT`, `PATCH/finalizar`, `PATCH/reactivar`, `DELETE`. Las reglas de autorización están en backend (lectura autenticada; escritura sólo `Administrador`). Sin embargo, **el frontend no existe** y el contrato está distribuido entre `SGV.Aplicacion` y `SGV.Api`, lo que rompe el patrón vigente donde `SGV.Web` sólo conoce `SGV.Contracts`.

Estado verificado (memoria `issue-208-explore-state`):
- `OcupacionDto` y `OcupacionCommandResult` viven en `SGV.Aplicacion/Ocupaciones/...` — **deuda**: `SGV.Web` no debe depender de esa capa.
- `OcupacionCommandResult` no expone `ErrorCategoria` (deuda #125, doc línea 435).
- El listado usa `?includeHistory=true|false`, **inconsistente** con `?status=activas|eliminadas` ya vigente en Puesto/Persona.
- No hay filtros contextuales por `PersonaId` ni `PuestoId` en `GET /api/v1/ocupaciones` — un call contextual hoy baja todo el dataset.
- No existen `SGV.Contracts/Ocupaciones/`, `SGV.Web/Integration/Ocupaciones/` ni `SGV.Web/Pages/Organizacion/Ocupaciones/`.

**Objetivo funcional**: cerrar el flujo operativo principal (asignar una Persona a un Puesto, consultar, finalizar, reactivar) desde `SGV.Web` con paridad operativa con Puesto, Cargos y Personas.

## Decisiones de Diseño (locked por el orchestrator)

### 1. Unificar contrato de listado: `?status=activas|eliminadas`

- **Motivación**: consistencia con `CargosController.GetConsulta` y `PersonasController.GetConsulta`. El toggle "Eliminadas" en la grilla ya usa el mismo lenguaje en los otros tres módulos.
- **Consecuencias**: `OcupacionesController.Get` cambia el parámetro `includeHistory` por `status` (default `activas`). El JSON shape no se rompe. Tests API existentes que asertan `includeHistory` se actualizan en este mismo PR.
- **Riesgos**: consumidores externos (no hay en el repo) podrían romperse. Aceptable porque `SGV.Web` es el único cliente conocido.

### 2. Filtros contextuales en el listado general: `?personaId=&puestoId=`

- **Motivación**: navegar desde Persona o Puesto no debe descargar el universo. La navegación cruzada filtra en origen.
- **Consecuencias**: extender `OcupacionListQuery` con dos parámetros opcionales; el controller los propaga al repositorio vía `IOcupacionRepository`. NO se crean sub-recursos anidados tipo `/api/v1/personas/{id}/ocupaciones` — un único endpoint con filtros es más simple y evita proliferar rutas.
- **Riesgos**: índices de BD. Verificar con `SHOW INDEX` que la combinación `PersonaId + IsDeleted` y `PuestoId + IsDeleted` se sirve de los índices únicos vigentes (`ActivePersonaPuestoUnique`, `ActivePuestoIdUnique`); si no, agregar índice compuesto.

### 3. `OcupacionCommandResult` migra a `ErrorCategoria` en este PR

- **Motivación**: la deuda #125 dejó explícito que `OcupacionCommandResult` no se migraba en su momento "porque no impactaba flujos administrativos". El módulo web de Ocupaciones **es** flujo administrativo: necesita que la Web mapee 400/401/403/404/409/transport vía el `CommandResultMapper` común.
- **Consecuencias**: agregar `Categoria: ErrorCategoria` al record; las variantes locales (`NotFound`, `Conflict`, `Validation`) se reemplazan por las del enum común. `MapCategoriaToLegacyType` endémico de los otros clientes (warning CS8524) acepta la nueva variante. La spec transversal `web-apiclient-transport-contract` ya exige esta uniformidad.
- **Riesgos**: cambio source-breaking interno (sólo `SGV.Aplicacion` lo consume; `SGV.Web` aún no existe). Bajo impacto.

### 4. Wire-types en `SGV.Contracts/Ocupaciones/`

- **Motivación**: `SGV.Contracts` debe seguir siendo **leaf** y el lugar único de tipos wire entre Web y API. Precedente: `2026-07-10-extraer-contratos-sgv`.
- **Consecuencias**: nueva carpeta `src/SGV.Contracts/Ocupaciones/` con subcarpetas `Consultas/Dtos/` (DTOs de lectura, `OcupacionListQuery`, `OcupacionSegmentoListado`, `PagedResult<OcupacionDto>`) y `Comandos/` (`CrearOcupacionRequest`, `ActualizarOcupacionRequest`, `FinalizarOcupacionRequest`, `OcupacionError`, `OcupacionCommandResult` con `Categoria`, constantes de ruta). El record `OcupacionDto` se mueve desde `SGV.Aplicacion/Ocupaciones/Consultas/Dtos/` y los call sites internos actualizan `using`.
- **Riesgos**: JSON shape idéntico (mismo nombre y orden de propiedades). Los tests API existentes siguen pasando sin cambios funcionales.

### 5. Cliente Web `IOcupacionApiClient` + `OcupacionApiClient`

- **Motivación**: paridad con `IPuestosApiClient` y `IPersonaApiClient`. Toda la comunicación Web → API pasa por un cliente tipado registrado en DI con `ApiBearerTokenHandler` (10s timeout) y delega en `CommandResultMapper.Map` para clasificar respuestas no exitosas.
- **Consecuencias**: nueva carpeta `src/SGV.Web/Integration/Ocupaciones/` con `IOcupacionApiClient`, `OcupacionApiClient` y un view-model de paginación. Registro en `Program.cs` con `AddHttpClient<IOcupacionApiClient, OcupacionApiClient>`. Fake equivalente (`FakeOcupacionApiClient`) para tests Web.
- **Riesgos**: el fake debe implementar la misma `IOcupacionApiClient` que el real, con `Contadores` por método para que `SgvWebApplicationFactory` lo inyecte en tests.

### 6. Razor Pages en `SGV.Web/Pages/Organizacion/Ocupaciones/`

- **Motivación**: paridad con `Pages/Organizacion/Puestos/` (recién ampliado en #209). El módulo cuelga de `/organizacion/ocupaciones` porque Ocupaciones es un subdominio de Organización (a diferencia de Personas, que es sibling directo en `/personas`).
- **Consecuencias**: 4 pages — `Index`, `Create`, `Edit`, `Details`. `Details` incluye finalizar/eliminar/reactivar según el patrón vigente en Puesto Details (acciones inline con confirmación SweetAlert2). PRG con `TempData` para feedback.
- **Riesgos**: `Edit` y `Details` no aplican a ocupaciones finalizadas/eliminadas; el page model gatea contra `Estado == Activa` antes de invocar la mutación, y el backend responde 409 si se cuela.

### 7. Navegación cruzada: `PersonaOcupaciones` y `PuestoOcupaciones`

- **Motivación**: paridad con `PersonaHabilidades.cshtml` y `PuestoHabilidades.cshtml`. Una Persona debe mostrar las ocupaciones que la tienen asignada; un Puesto debe mostrar la o las personas que lo ocupan.
- **Consecuencias**: dos Razor Pages nuevas en `Pages/Personas/PersonaOcupaciones.{cshtml,cshtml.cs}` y `Pages/Organizacion/Puestos/PuestoOcupaciones.{cshtml,cshtml.cs}`. Filtran por `PersonaId` o `PuestoId` en query al listado. Botón "Ocupaciones" en `Details` de Persona y de Puesto, gateado por `EsAdministrador` y `Estado == Activa`.
- **Riesgos**: la página cruzada hereda el contexto de origen vía `ReturnUrl` o query string, igual que `PersonaHabilidades` ya hace con `Habilidades` y `Ocupaciones`.

### 8. Entrada de menú en `_Sidenav.cshtml`

- **Motivación**: descubribilidad operativa. Precedente: entrada colapsable "Puestos" en el sidenav.
- **Consecuencias**: nuevo ítem colapsable `Ocupaciones` dentro de `Organización`, con sub-items `Listado` y `Nuevo`, gateado con `EsAdministrador` para los sub-items de escritura. El ítem padre se muestra para usuarios autenticados (lectura permitida a todos los autenticados, como el resto de listados administrativos).
- **Riesgos**: bajo. El gate de Admin aplica sólo a la creación; el listado es visible para todos los autenticados como en Puesto/Persona.

### 9. Plan de 4 slices stacked-to-main sobre `develop`

Ver §"Plan de Slices" más abajo. Cada slice pasa `dotnet build SGV.slnx` y `dotnet test SGV.slnx`. El budget de 400 líneas por PR se respeta; el slice más caro (3a) se acerca al límite porque suma 4 pages + tests.

## Alcance

### In Scope

- **Wire-types nuevos** en `src/SGV.Contracts/Ocupaciones/` (`Consultas/Dtos/`, `Comandos/`, constantes de ruta).
- **Migración** de `OcupacionDto` y `OcupacionCommandResult` desde `SGV.Aplicacion` a `SGV.Contracts` con `Categoria: ErrorCategoria` agregado.
- **Backend API**: extensión de `OcupacionesController.Get` para soportar `?status=activas|eliminadas` y `?personaId=&puestoId=` opcionales; cambio de `includeHistory` a `status`. Actualización de `OcupacionesControllerTests` y `OcupacionServicioConsulta` para reflejar el nuevo contrato.
- **Repositorio**: `IOcupacionRepository.QueryAsync(OcupacionListQuery)` server-side con segmentación, filtros y paginación (espejo `PuestoRepository.QueryAsync`).
- **Cliente Web** `IOcupacionApiClient` + `OcupacionApiClient` registrados en DI; fake para tests.
- **Razor Pages** en `src/SGV.Web/Pages/Organizacion/Ocupaciones/{Index,Create,Edit,Details}.{cshtml,cshtml.cs}` con PRG, `_Form.cshtml` compartido y feedback por `TempData`.
- **Navegación cruzada**: `PersonaOcupaciones`, `PuestoOcupaciones` + botón "Ocupaciones" en `Details` de Persona y Puesto.
- **Entrada colapsable** en `_Sidenav.cshtml` con sub-items gateados.
- **Tests**: dominios (reglas de unicidad, transiciones), aplicación (servicio, validador), persistencia `[MySqlFact]` (filtros, paginación, unicidad), API (auth 401/403, 400 con `ValidationProblemDetails`, 404, 409, `status=activas|eliminadas`, filtros), Web (render, PRG, errores recuperables, fakes).

### Out of Scope

- **No** se introducen nuevos bloques GUID ni catálogos inmutables. `Ocupacion` no requiere un catálogo nuevo.
- **No** se migran `PersonaCommandResult` ni `PersonaSkillCommandResult` a `ErrorCategoria` (siguen en `SGV.Aplicacion`; ya hay issue de follow-up post #125).
- **No** se modifican reglas de autorización server-side (lectura autenticada, escritura `Administrador` ya vigente).
- **No** se relajan las restricciones de unicidad `ActivePuestoIdUnique` y `ActivePersonaPuestoUnique`.
- **No** se introduce borrado físico desde la UI. Sólo baja lógica + reactivación.
- **No** se modifican los assets del template Inspinia ni `gulpfile.js`. Si una pantalla requiere CSS, se usa el patrón ya disponible.
- **No** se agregan nuevas dependencias NuGet.
- **No** se crea un sub-recurso anidado tipo `/api/v1/personas/{id}/ocupaciones` (decisión §2).

## Plan de Slices

| Slice | Archivos principales | Dependencias | LOC est. | Decisión PR |
|-------|---------------------|--------------|----------|-------------|
| **1** Contracts + API extendida | `SGV.Contracts/Ocupaciones/**` (~8 archivos), `OcupacionesController.cs`, `OcupacionListQuery`, `OcupacionServicioConsulta.cs`, `OcupacionRepository.cs` (QueryAsync), `OcupacionesControllerTests.cs` actualizado | Tests API existentes | ~250 | stacked-to-main sobre `develop` |
| **2** Cliente Web + Listado | `SGV.Web/Integration/Ocupaciones/{IOcupacionApiClient,OcupacionApiClient,OcupacionListItemViewModel}.cs`, `SGV.Web/Pages/Organizacion/Ocupaciones/Index.{cshtml,cshtml.cs}`, `Program.cs` (registro DI), `_Sidenav.cshtml`, `FakeOcupacionApiClient.cs`, `IndexPageTests.cs` | Slice 1 mergeado | ~280 | stacked-to-main sobre `develop` |
| **3a** Formularios CRUD | `SGV.Web/Pages/Organizacion/Ocupaciones/{Create,Edit,Details}.{cshtml,cshtml.cs}` + `_Form.cshtml` compartido, tests asociados | Slice 2 mergeado | ~390 | stacked-to-main sobre `develop` |
| **3b** Navegación cruzada | `SGV.Web/Pages/Personas/PersonaOcupaciones.{cshtml,cshtml.cs}`, `SGV.Web/Pages/Organizacion/Puestos/PuestoOcupaciones.{cshtml,cshtml.cs}`, botones en `Personas/Details.cshtml` y `Puestos/Details.cshtml`, tests asociados | Slice 2 mergeado (puede vivir en paralelo con 3a porque toca pages distintas) | ~200 | stacked-to-main sobre `develop` |

## Estrategia de Delivery

Stacked-to-main sobre `develop`, idéntico al patrón de `2026-07-27-completar-puestos-issue-209`. Cada slice genera un PR independiente hacia `develop`; el cierre de la issue #208 ocurre cuando el último PR mergea. **Review budget de 400 líneas por PR** — el slice 3a se acerca al límite; si lo supera, se subdivide en 3a-Form (Create+Edit) y 3a-Details, pero NO se reduce el alcance.

`delivery: ask-on-risk` significa que el orchestrator consulta al usuario antes de mergear si algún slice excede el budget o introduce riesgo nuevo (ej. una migración de esquema no anticipada).

## Riesgos y Supuestos

| # | Riesgo | Mitigación |
|---|--------|-----------|
| 1 | Cambiar `?includeHistory` por `?status=activas|eliminadas` es wire-breaking para cualquier consumidor externo | Único cliente conocido: `SGV.Web` (este PR). Documentar en `docs/decisiones-implementacion.md`. |
| 2 | Tests API existentes rompen con el cambio de nombre de parámetro | Actualizar aserciones en `OcupacionesControllerTests` dentro del mismo Slice 1; forma parte del alcance. |
| 3 | Filtros contextuales `?personaId=&puestoId=` requieren índices | Verificar con `SHOW INDEX FROM Ocupaciones` que `ActivePersonaPuestoUnique` cubre `PersonaId + IsDeleted`. Si no, agregar índice compuesto en la migración que acompañe al slice. **Supuesto**: los índices únicos vigentes ya cubren los filtros. |
| 4 | `OcupacionCommandResult` con `ErrorCategoria` introduce warning CS8524 en `MapCategoriaToLegacyType` si el fake del mapper no se actualiza | El mapper común (`SGV.Web/Integration/Common/CommandResultMapper.cs`) ya cubre las 7 variantes; el switch exhaustivo compila sin warning. Verificar en `verify-report.md`. |
| 5 | Slice 3a (4 pages + tests) puede acercarse o superar 400 líneas | Subdividir 3a-Form y 3a-Details si el conteo previo al PR excede 380 líneas. NO reducir alcance. |
| 6 | Tests `[MySqlFact]` se skipean sin MySQL → cobertura condicional | Documentar en `verify-report.md`; patrón vigente. 146+ tests skipped es el comportamiento esperado. |
| 7 | Web debe seguir usando exclusivamente `SGV.Contracts` | `dotnet list src/SGV.Web/SGV.Web.csproj reference` valida en CI. El grep `grep -r "SGV.Aplicacion" src/SGV.Web/` debe retornar cero hits sobre los nuevos archivos. |
| 8 | Botón "Ocupaciones" en Details de Persona/Puesto introduce gate de Admin | El gate es sólo para escribir; el listado cruzado es visible para todos los autenticados (paridad con `PersonaHabilidades`). |

## Pruebas (estrategia por capa)

| Capa | Alcance | Tests clave |
|------|---------|-------------|
| **Dominio** | Reglas de transición vigente/finalizada/eliminada/reactivada; unicidad vigente | `OcupacionTests` (ya existentes) — verificar que el cambio de contrato no las rompe. |
| **Aplicación** | `OcupacionServicioComandos` con guardas de Persona/Puesto activos; reactivaci\u00f3n con conflicto | `OcupacionServicioComandosTests`: `CrearAsync_*`, `EditarAsync_*`, `FinalizarAsync_*`, `ReactivarAsync_*`, `DesactivarAsync_*`. |
| **Persistencia** | `OcupacionRepository.QueryAsync` server-side con segmento + filtros + paginación | `[MySqlFact] OcupacionRepositoryQueryTests`: segmento, filtros por Persona/Puesto, sort, paginación con `TotalCount`. |
| **API** | Auth 401/403, validación 400, 404, 409 con códigos (`PersonaNoActiva`, `PuestoNoActivo`, `OcupacionDuplicada`, `OcupacionNoVigente`, `ConflictoReactivacion`), `status=activas|eliminadas`, filtros | `OcupacionesControllerTests`: actualizar aserciones de `includeHistory` → `status`; agregar `Get_ConFiltros_RetornaSoloCoincidencias`. |
| **Web** | Render de listado/historial, PRG, errores recuperables, navegación cruzada, gate Admin | `IndexPageTests`, `CreatePageTests`, `EditPageTests`, `DetailsPageTests`, `PersonaOcupacionesPageTests`, `PuestoOcupacionesPageTests`, `FakeOcupacionApiClientTests`, `IOcupacionApiClientContractTests`. |

**Cantidad esperada**: ~25-35 tests nuevos, alineado con el patrón de #209 (no inflar la suite con tests redundantes).

## Criterios de Aceptación (de issue #208, mapeados a slices)

| AC | Slice | Notas |
|----|-------|-------|
| Existen wire-types de Ocupaciones en `SGV.Contracts` sin referencias a capas internas | 1 | |
| Existe un cliente HTTP tipado registrado en `SGV.Web` | 2 | |
| El listado muestra ocupaciones activas con paginación server-side | 2 | |
| El listado permite consultar el historial sin filtrar todos los datos en memoria | 2 | toggle `activas|eliminadas` |
| Se puede crear una ocupación desde la Web | 3a | |
| Se puede editar una ocupación vigente desde la Web | 3a | |
| Se puede finalizar una ocupación desde la Web | 3a | `PATCH /finalizar` |
| Se puede eliminar lógicamente una ocupación desde la Web | 3a | `DELETE` |
| Se puede reactivar una ocupación histórica cuando el backend lo permite | 3a | `PATCH /reactivar` con 409 específico |
| Las acciones de escritura solo están disponibles para administradores | 2 (Listado), 3a (Forms) | `_Sidenav` y `Details` gatean `EsAdministrador` |
| Los endpoints mantienen autorización server-side independientemente de la UI | 1 (verificable con tests API 401/403) | |
| Los errores 400/401/403/404/409 y los fallos de transporte se muestran con feedback adecuado | 2, 3a | vía `CommandResultMapper` y feedback recuperable |
| Se puede acceder a las ocupaciones relacionadas desde una Persona | 3b | `PersonaOcupaciones` |
| Se puede acceder a las ocupaciones relacionadas desde un Puesto | 3b | `PuestoOcupaciones` |
| Se preserva el contexto de navegación al volver al listado de origen | 3b | `ReturnUrl` o query string |
| No se agregan referencias de `SGV.Web` a `SGV.Api`, `SGV.Aplicacion` ni `SGV.Infraestructura` | 1, 2, 3a, 3b | `dotnet list reference` + grep `SGV.Aplicacion` en `src/SGV.Web/` |
| Los tests cubren comportamiento observable y no detalles de implementación | Todos | |
| Los tests de persistencia se ejecutan contra MySQL cuando corresponde | 1 | `[MySqlFact]` |
| `dotnet build SGV.slnx` finaliza sin errores | Todos | |
| La suite .NET relevante pasa | Todos | `dotnet test SGV.slnx` |
| `bun run build` pasa si se modifican assets frontend | N/A | no se modifican assets (sólo Razor Pages) |

## Migración de Datos

**No requerida.** El cambio de `?includeHistory=true|false` por `?status=activas|eliminadas` es wire-breaking pero funcionalmente equivalente (mismas filas, mismos códigos). Los filtros contextuales son aditivos. No hay cambio de esquema, no hay `ALTER TABLE`, no hay `INSERT/DELETE` masivo. La migración de `OcupacionCommandResult` a `ErrorCategoria` es interna a `SGV.Contracts` y no toca la BD.

## Rollback Plan

- **Slice 1 (backend)**: revertir merge restaura el parámetro `includeHistory` y elimina los filtros contextuales y el `Categoria` del `CommandResult`. `OcupacionDto` y `OcupacionCommandResult` vuelven a vivir en `SGV.Aplicacion` (cambio atómico: archivos movidos, no duplicados). Tests API vuelven a sus aserciones originales.
- **Slice 2 (cliente+listado)**: revertir merge elimina `Pages/Organizacion/Ocupaciones/Index.*`, el cliente y la entrada del sidenav. Sin impacto si Slice 1 ya está mergeada porque el cliente sólo consume los endpoints vigentes.
- **Slice 3a (forms)**: revertir merge elimina `Create/Edit/Details/_Form`. Cero impacto en datos.
- **Slice 3b (navegación cruzada)**: revertir merge elimina `PersonaOcupaciones`/`PuestoOcupaciones` y los botones. Cero impacto.

Ningún slice toca migraciones de BD, por lo que el rollback es siempre limpio.

## Referencias

- Issue GitHub: [#208](https://github.com/elflacoseba/SGV/issues/208) (label `enhancement`, `status:needs-review`).
- Memoria Engram: `sdd-preflight-issue-208` (#1461) y `issue-208-explore-state` (#1462).
- Specs espejo: `web-apiclient-transport-contract`, `puesto-web-crear-editar`, `puesto-web-listado-detalle-baja`, `persona-skill-web-management`, `habilidad-web-listado-detalle-baja`.
- Change archivado de referencia: `openspec/changes/archive/2026-07-27-completar-puestos-issue-209/` (propuesta + spec + design + tasks con el mismo patrón).
- Deuda documentada: `docs/decisiones-implementacion.md` línea 435 (`OcupacionCommandResult` pendiente de migrar a `ErrorCategoria`, bloque levantado en este PR).
- Precedente arquitectónico: `openspec/changes/archive/2026-07-10-extraer-contratos-sgv/` (SGV.Contracts como leaf).
