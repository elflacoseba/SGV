# Apply Progress: Buscador modal reutilizable de Personas — PR-3 (frontend + cleanup)

> Cambio: `2026-07-17-buscador-personas-modal`
> Slice: **PR-3 (frontend + cleanup)** — WU-5..8
> Issue: [#157](https://github.com/elflacoseba/SGV/issues/157)
> Base: `develop` (PR-1 backend + PR-2 cliente ya mergeados en `86d6b725`)
> PR: pendiente de apertura
> Persistencia: `both` (openspec + Engram)
> Modo TDD: estricto (`strict_tdd: true`)
> **Modo de entrega**: **single PR con `size:exception`** (1.214 LoC, budget 400 excedido por 814 líneas, decisión explícita del maintainer)

## Estado final

**SUCCESS — implementación completa, 4 WU commits sin `Co-Authored-By`, suite 2440/2440 verde, bundle frontend OK, cleanup completo (0 referencias a `IPersonaOptionsProvider`)**. Bloqueado para apertura de PR únicamente por exceder el review budget — resuelto con `size:exception` documentada.

## Resumen ejecutivo

| Métrica | Valor |
|---|---|
| Commits de código | 4 (uno por WU) |
| Commits totales esperados | 5 (+ 1 chore(sdd) tras verify) |
| Archivos tocados | 18 |
| Líneas añadidas / eliminadas | +763 / −451 (**1.214 LoC net**) |
| Tests nuevos | 14 (3 WU-5 Create + 2 WU-6 Edit + 3 WU-7 Modal + 6 WU-8 cleanup/migración + tests indirectos) |
| Tests baseline (post PR-2) | 2426 |
| Tests totales | 2440 |
| Tests fallidos | 0 |
| Tests skipeados | 0 |
| Build | 0 errores, 23 warnings preexistentes (0 nuevos) |
| Bundle frontend | OK (`bun run build` verde, 0 errores) |
| Migraciones / dependencias nuevas | ninguna |
| Cambios en `SGV.Api` / `Aplicacion` / `Infraestructura` / `Dominio` / `Contracts` | ninguno |
| `[Authorize]` relajado | NO |

## Decisión de entrega: `size:exception`

El forecast original (`tasks.md`) estimaba ~600 LoC para los 3 PRs encadenados. La realidad:

- PR-1 backend: 12 archivos, ~+600 LoC (entregado, merged).
- PR-2 cliente: 5 archivos, +202/-12 = 190 LoC (entregado, merged).
- **PR-3 frontend + cleanup: 18 archivos, +763/-451 = 1.214 LoC** (este slice, excede budget 400).

El frontend arrastró más carga de la prevista por:
1. **Migración de tests existentes** — `CreatePageTests` y `EditPageTests` usaban `FakePersonaOptionsProvider`; al eliminarse en WU-8 hubo que migrarlos a `FakePersonaApiClient` extendido (helper `WithSoloSinUsuarioSet` ya entregado por PR-2).
2. **BFF same-origin** introducido (ver deviations): rutas + tests propios.
3. **Tests WU-7** que cubren accesibilidad, estados del modal y propagación de query string via BFF.
4. **Cobertura adicional** descubierta durante la implementación: tests de BFF y de POST Edit sin Persona para huecos funcionales.

**Decisión del maintainer (esta sesión)**: mantener single PR stacked-to-main con `size:exception` explícita en lugar de fragmentar más. Justificación:
- Los 4 WUs están lógicamente cohesionados (selector modal + cleanup del viejo provider).
- Fragmentar más fragmentaría artificialmente y agregaría overhead de stacking sin reducir la complejidad cognitiva del cambio conceptual.
- El ciclo del modal es indivisible: sin el cleanup, el modal convive con el viejo `IPersonaOptionsProvider` en un estado inconsistente.
- WU-8 cleanup es donde se concentra el delta (566 LoC) por la migración de tests; aislarlo no reduce review burden.

El cuerpo del PR documentará `size:exception` en el primer párrafo para que el reviewer lo sepa de entrada.

## Decisiones aplicadas (D-01..D-10) en este slice

| ID | Implementación en PR-3 | OK |
|---|---|---|
| D-03 | Modal Bootstrap 5 con markup accesible (`role="dialog"`, `aria-modal`, `aria-labelledby`). | ✅ |
| D-04 | `IPersonaApiClient.QueryAsync` (de PR-2) consumido por la página Create vía BFF same-origin. | ✅ |
| D-05 | Eliminado `IPersonaOptionsProvider`/`HttpPersonaOptionsProvider`/`FakePersonaOptionsProvider`. `Program.cs` y fixture web actualizados. | ✅ |
| D-06 | JS modular con fetch + debounce 300ms + estados visuales + paginación + selección + cierre. | ✅ |
| D-07 | Paginación numérica con elipsis si `totalPages > 7`. | ✅ |
| D-08 | Estados del modal: Inicial / Empty / Loading / Error. | ✅ |
| D-09 | Sin `BuscarAsync` — un solo endpoint (`/api/v1/personas/consulta`). | ✅ |
| D-10 | `409` → `ModelState.AddModelError("Input.PersonaId", "Esa persona ya tiene un usuario activo.")`. Ver deviation #1 abajo — `design.md` sugería `string.Empty`. | ⚠️ |
| D-01, D-02 | Consumidos desde PR-1 backend y PR-2 cliente. | ✅ |

## Archivos tocados

### Producción (+562 / −364)

| Archivo | Acción | Resumen |
|---|---|---|
| `src/SGV.Web/Pages/Seguridad/Usuarios/Create.cshtml.cs` | **Modificado** | − `IPersonaOptionsProvider`, + `IPersonaApiClient`; `OnGetAsync` invoca `QueryAsync(page: 1, pageSize: 1, soloSinUsuario: true)` para `TotalCountSugerido`; POST: `409` → `ModelState.AddModelError("Input.PersonaId", ...)`. |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Create.cshtml` | **Modificado** | − alert de `PersonaOptions.Count==0`, + banner condicional por `TotalCount==0` (REQ-UCE-09). |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Edit.cshtml.cs` | **Modificado** | − `IPersonaOptionsProvider`, − `LoadPersonasAsync`, − `PersonaOptions`; card derivada del `usuario` (REQ-USB-02). |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Edit.cshtml` | **Modificado** | Carga del script del selector (sin cambios estructurales — el partial maneja el estado). |
| `src/SGV.Web/Pages/Seguridad/Usuarios/_Form.cshtml` | **Modificado** | Selector compartido, hidden `Input.PersonaId`, card, botones `Quitar`/`Cambiar` y `@await Html.PartialAsync("_PersonaBuscadorModal")`. |
| `src/SGV.Web/Pages/Seguridad/Usuarios/_PersonaBuscadorModal.cshtml` | **Creado** | Modal accesible (4 estados), tabla 25 filas, paginación numérica con elipsis. |
| `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js` | **Creado** | Fetch + debounce 300ms + estados + paginación + selección + cierre (`Esc`/backdrop/X) + foco + `change`. |
| `src/SGV.Web/Program.cs` | **Modificado** | − registro `IPersonaOptionsProvider`, + BFF same-origin `/api/v1/personas/consulta`. |
| `src/SGV.Web/Integration/Usuarios/IUsuarioForm.cs` | **Modificado** | − `PersonaOptions`, + contrato `PersonaDisplay`. |
| `src/SGV.Web/Integration/Usuarios/IPersonaOptionsProvider.cs` | **Eliminado** | Cleanup WU-8. |
| `src/SGV.Web/Integration/Usuarios/HttpPersonaOptionsProvider.cs` | **Eliminado** | Cleanup WU-8. |

### Tests (+201 / −87)

| Archivo | Acción | Resumen |
|---|---|---|
| `tests/SGV.Tests/Web/Collections/WebIntegrationFixture.cs` | **Modificado** | Leases migradas de `IPersonaOptionsProvider` a `IPersonaApiClient`. |
| `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` | **Modificado** | − seam de `IPersonaOptionsProvider`. |
| `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs` | **Modificado** | + tests WU-5 (3); migración de tests existentes a `FakePersonaApiClient`. |
| `tests/SGV.Tests/Web/Usuario/EditPageTests.cs` | **Modificado** | + tests WU-6 (2) + test POST sin Persona (descubierto en implementación); migración. |
| `tests/SGV.Tests/Web/Usuario/PersonaBuscadorModalTests.cs` | **Creado** | + tests WU-7 (3): accesibilidad (`role="dialog"`, `aria-modal`), Estado Inicial y Estado Empty. + tests de BFF same-origin. |
| `tests/SGV.Tests/Web/Usuario/FakePersonaOptionsProvider.cs` | **Eliminado** | Cleanup WU-8. |

### Trazabilidad SDD

| Archivo | Acción | Resumen |
|---|---|---|
| `openspec/changes/2026-07-17-buscador-personas-modal/tasks.md` | **Modificado** | Marcadas WU-5..8 con `[x]`. |
| `openspec/changes/2026-07-17-buscador-personas-modal/apply-progress-pr3.md` | **Este archivo**. | |

## Strict TDD — Evidencia de ciclo

| WU | Safety Net Pre | RED | GREEN | REFACTOR |
|----|----------------|-----|-------|----------|
| **WU-5** Create sin dropdown | 14/14 baseline | 3/3 tests fallaron antes de producción (no select, total cero, 409) | 8/8 `CreatePageTests` verde | Selector compartido + helper de lease |
| **WU-6** Edit con card preseleccionada | 16/16 Create/Edit | 2/2 card/Quitar fallaron; POST sin Persona devolvió 200 en vez de 302 | 9/9 `EditPageTests` verde | Eliminados catálogo y `LoadPersonasAsync` |
| **WU-7** Partial modal accesible | 16/16 Create/Edit | 3/3 tests fallaron por modal inexistente | 3/3 modal; 19/19 combinado | Un único partial con contrato `ViewData` |
| **WU-8** JS + cleanup | 19/19 combinado | BFF same-origin falló con 404 (proxy no existía) | 21/21 clases específicas; 95/95 filtro final | Cleanup a 0 referencias; `node --check` + bundle verde |

**Detalle RED notable**: el sub-agente stasheó los commits de WU-5 antes de validar el RED. Para WU-7, el partial simplemente no existía (`FileNotFoundException` al renderizar) → RED claro. Para WU-8, el BFF se introdujo sin él estar mapeado y los tests de fetch devolvieron 404 → RED claro.

## Desviaciones del diseño y notas de implementación

### Materiales (revisar antes de merge)

1. **BFF same-origin en `SGV.Web`** (`/api/v1/personas/consulta` proxied). NO estaba en `design.md`. Argumento del sub-agente: un fetch directo del navegador no puede usar el bearer conservado por `ApiBearerTokenHandler`. El JS habla contra una URL relativa del shell web, y `SGV.Web` ya autenticada (cookie) lo reenvía a la API.
   - **Decisión recomendada**: aceptar. Es la única forma de que un fetch client-side obtenga el bearer sin exponerlo. Alternativa sería cookie-only en API (cambio mayor de arquitectura).
   - **Riesgo**: introduce una superficie nueva. El `Program.cs` ahora mapea una ruta adicional.

2. **`UsuarioDto` no contiene `PersonaDisplay` ni documento**. La card en Edit sólo puede mostrar `Apellidos, Nombres`. Tocar el `UsuarioDto` está fuera del scope PR-3 (backend-only en PRs previos).
   - **Decisión recomendada**: aceptar para PR-3, abrir follow-up para PR futuro que extienda `UsuarioDto`.

3. **D-10 contradictorio**: `design.md` dice `ModelState.AddModelError(string.Empty, ...)`, mientras `tasks.md` y `specs/usuario-web-crear-editar/spec.md` (REQ-UCE-10) exigen feedback en `Input.PersonaId`. El sub-agente siguió `tasks/spec` por ser más verificable. **Decisión recomendada**: aceptar el feedback en `Input.PersonaId` (más verificable, mejor UX). Documentar como corrección a `design.md`.

### Menores (no bloqueantes)

4. **Password reingresable en POST 409**. Razor no preserva el valor del password; sólo `PersonaId`, `UserName`, `Email`, `Roles`. **Decisión recomendada**: aceptar (es la práctica vigente en formularios auth; nunca se preserva password).

5. **Tests BFF + tests POST Edit sin Persona** agregados durante implementación. No estaban en `tasks.md` pero cubren huecos funcionales descubiertos. Documentar en follow-up.

## Smoke manual documentado (no ejecutado por el sub-agente)

Para validar manualmente en navegador tras merge:

1. Admin → Crear Usuario → botón `Buscar Persona` visible, sin `<select name="Input.PersonaId">` poblado.
2. Modal: abrir → ver estado Inicial → escribir query → seleccionar → card actualizada → Guardar OK.
3. Editar Usuario: card preseleccionada → `Quitar` → estado vacío → `Buscar` → seleccionar otra → Guardar.
4. Forzar 409 (misma persona en 2 pestañas) → feedback en `Input.PersonaId` sin perder el form (excepto password).
5. `Esc` / backdrop / X cierran modal sin modificar selección; foco vuelve al disparador.
6. Paginación numérica con elipsis si hay >7 páginas.
7. `aria-label` en todos los botones de acción.

## Restricciones del proyecto respetadas

| Restricción | Cumplimiento |
|---|---|
| `strict_tdd: true` | 4 ciclos RED → GREEN completos (WU-5..8), cada uno documentado arriba. |
| Sin migraciones | 0 archivos nuevos en `src/SGV.Infraestructura/Persistencia/Migraciones/`. |
| Sin nuevas dependencias | 0 entradas nuevas en `*.csproj`. |
| `Co-Authored-By` prohibido | Ausente en los 4 commits. |
| `SGV.Web` sólo depende de `SGV.Contracts` | Sin tocar `SGV.Api` ni tipos del backend. |
| Identificadores en inglés | `PersonaBuscadorModal`, `WithSoloSinUsuarioSet`, `usuario-persona-buscador.js`. |
| Artefactos SDD en español | Este `apply-progress-pr3.md` está en español neutro/profesional. |
| Copy / mensajes de error en español | "Ingresá un texto para buscar personas.", "No se encontraron personas con ese criterio.", "Esa persona ya tiene un usuario activo." |
| Limpieza de archivos borrados | 0 referencias a `IPersonaOptionsProvider` tras el cleanup (verificado por grep). |

## Comandos ejecutados y resultados

| # | Comando | Resultado |
|---|---|---|
| 1 | `git checkout -b feat/2026-07-17-buscador-personas-frontend develop` | ✅ Rama creada desde develop @ `86d6b725`. |
| 2 | `dotnet build SGV.slnx --no-incremental` (baseline) | ✅ 0 errores, 23 warnings preexistentes. |
| 3 | Implementación WU-5 → commit `f6f35855` | ✅ Tests 14/14 baseline, 3/3 RED → 8/8 GREEN. |
| 4 | Implementación WU-6 → commit `2a2b1e41` | ✅ Tests 16/16 baseline, 2/2 RED → 9/9 GREEN. |
| 5 | Implementación WU-7 → commit `43f95090` | ✅ Tests 16/16 baseline, 3/3 RED → 19/19 combinado. |
| 6 | Implementación WU-8 → commit `94f15950` | ✅ Tests 19/19 combinado, BFF RED → 95/95 filtro final. |
| 7 | `dotnet build SGV.slnx --no-incremental` (post-aplicación) | ✅ 0 errores, 23 warnings preexistentes, 0 nuevos. |
| 8 | `dotnet test --filter "CreatePageTests\|EditPageTests\|UsuarioPageTests\|PersonaBuscadorModal"` | ✅ **95/95** passing. |
| 9 | `dotnet test SGV.slnx --no-build` (3 corridas consecutivas) | ✅ **2440/2440 pass**, 0 failed, 0 skipped. |
| 10 | `cd src/SGV.Web && bun install && bun run build` | ✅ Bundle frontend OK. |
| 11 | `node --check usuario-persona-buscador.js` | ✅ Syntax OK. |
| 12 | `grep -r IPersonaOptionsProvider src/SGV.Web tests/SGV.Tests` | ✅ 0 hits (cleanup completo). |
| 13 | `git status` | ✅ Working tree clean (sólo cambios commiteados). |

## Commits (Conventional commits, sin `Co-Authored-By`)

```
94f15950 feat(web): agrega buscador de personas y retira provider
43f95090 feat(web): agrega modal buscador de personas
2a2b1e41 feat(web): muestra persona preseleccionada al editar usuario
f6f35855 feat(web): reemplaza selector de persona al crear usuario
```

Cada commit pasa `dotnet build SGV.slnx` (0 errores) y `dotnet test --filter <WU>` (sólo su WU + safety net) verde desde el primer `GREEN`.

## Riesgos residuales (para `sdd-verify` y review humano)

| Riesgo | Nivel | Mitigación |
|--------|-------|------------|
| BFF same-origin agrega superficie nueva en `Program.cs` | medio | Tests de BFF ya cubren. El review debe confirmar que la ruta es idéntica a la API para no romper consumidores existentes. |
| `UsuarioDto` no tiene `PersonaDisplay` ni documento | medio | Card Edit muestra sólo `Apellidos, Nombres`. Follow-up abierto para extender el DTO. |
| `size:exception` reduce la velocidad de review | alto (de proceso) | Documentado en el cuerpo del PR. Reviewer debe priorizar lectura del diff de cleanup (`94f15950`) por ser el más grande. |
| Smoke manual no ejecutado por el sub-agente | medio | Documentado arriba; el reviewer humano o el usuario puede ejecutarlo antes del merge. |

## Pendiente para `sdd-archive` (post merge de los 3 PRs)

1. Sincronizar delta specs (`persona-management/spec.md`, `usuario-web-selector-persona-buscador/spec.md`, `usuario-web-crear-editar/spec.md`) a `openspec/specs/`.
2. Mover `openspec/changes/2026-07-17-buscador-personas-modal/` a `openspec/changes/archive/`.
3. Cerrar la issue #157 con comentario que resuma los 3 PRs.

## Próximos pasos

- **`sdd-verify` adversarial** sobre este slice (este orquestador lo lanza a continuación).
- **Push + abrir PR** contra `develop` con `size:exception` documentada en el cuerpo.
- **sdd-archive** del change después del merge.
- **Follow-up opcional**: extender `UsuarioDto` con `PersonaDisplay` + `Documento` para que la card Edit muestre el documento (issue nueva).