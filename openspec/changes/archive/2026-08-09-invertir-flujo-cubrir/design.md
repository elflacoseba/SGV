# Design: invertir-flujo-cubrir

## A. Contexto y problema

El change `vacante-ocupacion-flow-alignment` (archivado 2026-08-07) implementó N2 como "transición a Cubierta vía `PATCH /vacantes/{id}/estado` crea la Ocupación derivada", exigiendo `PersonaId` en `CambiarEstadoVacanteRequest`. El frontend **no expone `PersonaId`** y el dropdown de Edit ya excluye Cubierta (issue #268). Resultado: el ciclo "Crear Vacante → Cubrir" no cierra desde la UI.

Este change **invierte el flujo Cubrir**: la creación de Ocupación con `VacanteId` optional en `OcupacionServicioComandos.CrearAsync` se vuelve el único camino para cubrir una Vacante; `PATCH .../estado` con destino Cubierta se rechaza con `400 Validation` y un mensaje que guía al botón "Cubrir Vacante" del Details. La atomicidad transaccional se conserva usando el patrón EF vigente (sección D-5 del archivo `VacanteRepository`).

Decisiones vigentes relevantes (de `docs/decisiones-implementacion.md` y specs actuales): FK opcional `Ocupaciones.VacanteId` ya existe; `OcupacionConfiguracion` ya declara `HasOne(Vacante).WithMany()` con `OnDelete(Restrict)`; `IX_Ocupaciones_VacanteId` ya existe; `EstadoVacante.EsCubierta` es el flag de dominio usado por `VacanteServicioComandos.CambiarEstadoAsync`.

## B. Decisiones arquitectónicas

| ID | Decisión | Alternativas rechazadas | Rationale |
|----|----------|-------------------------|-----------|
| **D-1** | Invertir la creación de Ocupación: el bloque de `VacanteServicioComandos.CambiarEstadoAsync` (líneas 288–344) que valida `PersonaId` y crea `Ocupacion` se elimina. La transición a `Cubierta` vía `PATCH` se rechaza con `400 Validation` y código `CubrirVacanteRequiereCrearOcupacion` (renombre de `PersonaIdRequeridoParaCubrir`, ver D-4), mensaje "Use el botón 'Cubrir Vacante' en el detalle de la Vacante para crear la Ocupación derivada.". La lógica Cubrir vive en `OcupacionServicioComandos.CrearAsync` cuando `request.VacanteId` no es null. | (a) Mantener dos paths coexisting (rompe AC4/AC5). (b) Agregar `PersonaId` al form Edit (rompe AC4). | Inversión completa del flujo. La validación `EsCubierta` del catálogo sigue siendo el flag de dominio; el service nueva rama con `request.VacanteId is null` mantiene N3 vigente sin cambios. `CambiarEstadoVacanteRequest.PersonaId` queda en el record como deprecated (D-3). |
| **D-2** | Atomicidad de `OcupacionServicioComandos.CrearAsync` con `VacanteId`: dentro del mismo `try { ... unitOfWork.SaveChangesAsync() ... }` vigente, agregar `vacanteRepository.GetByIdForUpdateAsync(vacanteId)` → `vacante.CambiarEstado(... `  → `vacanteRepository.RegistrarCambioEstadoAsync(vacante, historial)`. `Ocupacion` y el cambio de Vacante se persisten en el mismo `SaveChangesAsync`. El `DbUpdateException` catch ya existente cubre el rollback. | (a) TransactionScope explícito (no se usa en el repo). (b) Two SaveChanges (rompe atomicidad). | Patrón transaccional vigente del archivo `VacanteServicioComandos.CambiarEstadoAsync`. EF agrupa inserts y updates en una transacción única al `SaveChangesAsync`. Es el patrón documentado en `design.md §D-5` del change archivado. |
| **D-3** | `VacanteDetailDto` se extiende con `Guid? OcupacionDerivadaId` y `string? PersonaAsignadaNombre` (string plano, no DTO anidado). Hidratación: `VacanteServicioConsulta.ObtenerPorIdAsync` después de `MapToDetailDto`, consulta separada vía `IOcupacionRepository.ObtenerVigentePorVacanteAsync(vacanteId)` → retorna `(Guid Id, string PersonaNombre)?`. Si es null → ambos campos null (defensivo, estado inconsistente). `IVacanteServicioConsulta.GetByIdAsync` NO se renombra ni se sobrecarga — el método existente absorbe la hidratación con la consulta lateral. | (a) Renombrar/crear `GetByIdWithOcupacionAsync` (duplica el path del listado). (b) Extender `VacanteRepository.GetByIdForUpdateAsync` con Includes de Ocupaciones (no existe nav `Vacante.Ocupaciones`). (c) DTO `PersonaResumenDto` anidado (overkill — el Details solo muestra nombre). | Q1 cerrada: el método vigente no carga Ocupaciones. `VacanteEntity` no declara `Ocupaciones` como nav list — agregarlo rompería el agregado. Una consulta lateral en `OcupacionRepository` respeta Clean Architecture (cada repo es dueño de su DbSet) y se reutiliza en `CrearAsync` para la validación `VacanteYaCubierta`. Q2 cerrada: string plano. |
| **D-4** | Renombrar el código de error `PersonaIdRequeridoParaCubrir` → **`CubrirVacanteRequiereCrearOcupacion`** en `VacanteErrorCodigo.cs`. El código viejo `PersonaIdRequeridoParaCubrir` se conserva como `[Obsolete("…use CubrirVacanteRequiereCrearOcupacion…")]` apuntando al nuevo para no romper binding legacy en clientes cacheados; sólo se enumera `CubrirVacanteRequiereCrearOcupacion` en specs/tests nuevos. Verificado: no colisiona con ningún vigente en `VacanteErrorCodigo` ni `OcupacionErrorCodigo`. | Mantener `PersonaIdRequeridoParaCubrir` (enfatiza el campo deprecado, no el flujo). | Refleja la inversión (la acción requerida es "crear Ocupación", no "enviar PersonaId"). No rompe wire porque es un nuevo código de error devuelto en una rama (`PATCH` a Cubierta) que antes devolvía `400` con el código viejo; clientes no integrados todavía. |
| **D-5** | UI flow: (a) `Vacantes/Details.cshtml` renderiza botón "Cubrir Vacante" cuando `CanMutate && ViewModel.EstadoVacante != Cubierta && != Cancelada` (nuevo `bool EsCubrible` en `VacanteDetailViewModel`). `href = Url.Page("/Organizacion/Ocupaciones/Create", new { vacanteId = Id, returnUrl = ...Details... })`. El bloque "Persona asignada" + link "Ver ocupación" se renderiza cuando `EsCubierta && OcupacionDerivadaId.HasValue`, entre el card de detalle y el card de historial. (b) `Ocupaciones/Create` acepta `?vacanteId` en `OnGetAsync`: consulta `vacanteApiClient.ObtenerPorIdAsync(vacanteId)`, mapea a 4 estados (Abierta/En Selección → form con `PuestoId` bloqueado + hint, Cubierta → error "Esta Vacante ya está cubierta.", Cancelada → error "Esta Vacante está cancelada y no puede cubrirse.", null → error "La Vacante no existe."). (c) `PuestoOcupaciones.cshtml.cs` mantiene `NewOcupacionRouteValues` apuntando a `vacanteId` cuando `HayVacanteAbierta && !HayOcupacionActiva`, y agrega `NewOcupacionButtonLabel` (`"Cubrir Vacante"` vs `"Nueva ocupación"`); `_CrossList.cshtml` consume el label sin lógica. | (a) Crear endpoint web dedicado `/cubrir`. (b) Inline-fowardear desde Details al POST. | Una sola página (`Ocupaciones/Create`) con dos entry points (`?puestoId`, `?vacanteId`) reutiliza el form, validator, JS de buscador. El POST sigue siendo a `/api/v1/ocupaciones`; el back decide con `VacanteId` presente. |
| **D-6** | Convención de escenarios: dejar nota explícita en `sdd-archive` para **normalizar todos los escenarios a DADO-CUANDO-ENTONCES** (español) en la spec vigente al sincronizar deltas. En los deltas de este change, se respetó la convención "delta relativo" — los escenarios vigentes heredados (GIVEN-WHEN-THEN) no se reescriben en el delta; sólo los nuevos y modificados siguen DADO-CUANDO-ENTONCES. | Reescribir todos los escenarios vigentes en el delta (rompe delta relativo). | El delta debe ser mínimo; la normalización se hace una sola vez en archive. R1 cerrada. |

## C. Capas y archivos clave

| Capa | Archivo | Acción | Rol |
|------|---------|--------|-----|
| Contracts | `src/SGV.Contracts/Ocupaciones/Comandos/CrearOcupacionRequest.cs` | MODIFY | Agregar `Guid? VacanteId` al final del record (backward-compatible). |
| Contracts | `src/SGV.Contracts/Vacantes/Consultas/Dtos/VacanteDetailDto.cs` | MODIFY | Agregar `Guid? OcupacionDerivadaId, string? PersonaAsignadaNombre` al final. |
| Contracts | `src/SGV.Contracts/Vacantes/Comandos/CambiarEstadoVacanteRequest.cs` | MODIFY | XML doc mark `PersonaId` como `[Obsolete("…use Ocupaciones Create con VacanteId…")]` en record summary. |
| Contracts | `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs` | MODIFY | Renombrar a `CubrirVacanteRequiereCrearOcupacion`; marcar `PersonaIdRequeridoParaCubrir` como Obsolete backup. |
| Contracts | `src/SGV.Contracts/Ocupaciones/Comandos/OcupacionErrorCodigo.cs` | MODIFY | Agregar `VacanteNoEncontrada`, `VacanteNoAbierta`, `VacanteYaCubierta`, `PuestoIdNoCoincideConVacante`. |
| Aplicacion | `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs` | MODIFY | En `CrearAsync`: si `VacanteId.HasValue` → validar Vacante existe / no terminal / sin Ocupación vigente / coherencia `PuestoId`, crear Ocupación con `VacanteId` setado, invocar `vacante.CambiarEstado → RegistrarCambioEstadoAsync` dentro del mismo `try/SaveChangesAsync`. |
| Aplicacion | `src/SGV.Aplicacion/Ocupaciones/Consultas/IOcupacionRepository.cs` | MODIFY | Agregar `Task<(Guid Id, string PersonaNombre)?> ObtenerVigentePorVacanteAsync(Guid vacanteId, CancellationToken)` y `Task<bool> ExistsActiveByVacanteAsync(Guid vacanteId, CancellationToken)`. |
| Aplicacion | `src/SGV.Aplicacion/Ocupaciones/Comandos/Validaciones/CrearOcupacionRequestValidator.cs` | MODIFY | Si `VacanteId.HasValue` → `PuestoId` opcional; si ambos presentes → `Must` que coincidan (custom validator). |
| Aplicacion | `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` | MODIFY | Reemplazar bloque líneas 288–344: si `estadoNuevo.EsCubierta` → return `Failure` con `CubrirVacanteRequiereCrearOcupacion` y mensaje "Use el botón 'Cubrir Vacante'…". Eliminar `using Ocupaciones` / dependencia `IOcupacionRepository` de la firma si queda dead-code. |
| Aplicacion | `src/SGV.Aplicacion/Vacantes/Consultas/VacanteServicioConsulta.cs` | MODIFY | Inyectar `IOcupacionRepository`. En `ObtenerPorIdAsync`, después de `MapToDetailDto`, llamar `ObtenerVigentePorVacanteAsync` y construir nuevo `VacanteDetailDto` con los campos extraídos. |
| Infraestructura | `src/SGV.Infraestructura/Persistencia/Repositorios/OcupacionRepository.cs` | MODIFY | Implementar `ObtenerVigentePorVacanteAsync` (project `Id` + `Persona.Nombres + ' ' + Persona.Apellidos`) y `ExistsActiveByVacanteAsync` (`AnyAsync(o => o.VacanteId == vacanteId && !IsDeleted && FechaFin == null)`). |
| Web | `src/SGV.Web/Integration/Vacantes/VacanteDetailViewModel.cs` | MODIFY | Agregar campos `OcupacionDerivadaId`, `PersonaAsignadaNombre`, `bool EsCubrible` (basado en `EstadoVacanteNombre` / nuevo flag de DTO). Actualizar `FromDto`. |
| Web | `src/SGV.Web/Pages/Organizacion/Vacantes/Details.cshtml` | MODIFY | Bloque botón "Cubrir Vacante" + bloque "Persona asignada" (entre detalle e historial). |
| Web | `src/SGV.Web/Pages/Organizacion/Vacantes/Details.cshtml.cs` | MODIFY | Exponer flag `EsCubrible` (no hace falta nueva consulta — `ViewModel.EsCubrible`). |
| Web | `src/SGV.Web/Pages/Organizacion/Ocupaciones/Create.cshtml.cs` | MODIFY | `OnGetAsync` acepta `Guid? vacanteId`. Resuelve Vacante vía `IVacanteApiClient.ObtenerPorIdAsync`. Bloquea `PuestoId`, setea `Input.VacanteId`, agrega `VacanteHintLabel`. `OnPostAsync` incluye `VacanteId` en `CrearOcupacionRequest` y decide returnUrl (si `vacanteId` → `/organizacion/vacantes/detalles/{vacanteId}`). |
| Web | `src/SGV.Web/Pages/Organizacion/Ocupaciones/OcupacionFormPageModel.cs` | MODIFY | Agregar `VacanteId` a `OcupacionInputModel` (hidden). |
| Web | `src/SGV.Web/Pages/Organizacion/Ocupaciones/Create.cshtml` | MODIFY | Mensaje de inicio/redirect según `Model.VacanteHintLabel`. |
| Web | `src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml` | MODIFY | Hidden `Input.VacanteId` + hint informativo cuando viene de Vacante. Si `VacanteId` está setado → dropdown `PuestoId` con `disabled` + hidden que preserva el valor para model binding. |
| Web | `src/SGV.Web/Pages/Organizacion/Ocupaciones/IOcupacionForm.cs` | MODIFY | Agregar `VacanteId` y `VacanteHintLabel` al contract. |
| Web | `src/SGV.Web/Pages/Organizacion/Puestos/PuestoOcupaciones.cshtml.cs` | MODIFY | `NewOcupacionRouteValues` ahora pasa `vacanteId` en lugar de `puestoId` cuando `HayVacanteAbierta && !HayOcupacionActiva`. Agregar `NewOcupacionButtonLabel` al `IOcupacionesCrossList`. (Requiere resolver Vacante abierta por Puesto — vía `IVacanteApiClient` existente o nuevo endpoint `ObtenerAbiertaPorPuestoAsync`.) |
| Web | `src/SGV.Web/Pages/Organizacion/Ocupaciones/IOcupacionesCrossList.cs` | MODIFY | Agregar `string NewOcupacionButtonLabel { get; }` (default `"Nueva ocupación"` en PersonaOcupaciones). |
| Web | `src/SGV.Web/Pages/Organizacion/Ocupaciones/_CrossList.cshtml` | MODIFY | Renderiza `@Model.NewOcupacionButtonLabel` en el botón en lugar del literal. |
| Tests | `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs` | MODIFY | 5 tests nuevos `CrearAsync_ConVacanteId_*` (happy path, vacante no encontrada, vacante no abierta, ya cubierta, `PuestoId` no coincide, atomicidad). |
| Tests | `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` | MODIFY | Reemplazar `CambiarEstado_A_Cubierta_ConPersonaId_CreaOcupacionYRegistraHistorial` (assert 400 + codigo nuevo + mensaje). Agregar test con `PersonaId` populado (se ignora). |
| Tests | `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioConsultaTests.cs` | MODIFY | 2 tests de `ObtenerPorIdAsync` cubriendo `OcupacionDerivadaId`/`PersonaAsignadaNombre`. |
| Tests | `tests/SGV.Tests/Api/Ocupaciones/OcupacionServicioComandosTests.cs` o equivalente controller wiring | MODIFY | 3 tests `Create_ConVacanteId_*` (201, 404, 409). |
| Tests | `tests/SGV.Tests/Api/Vacantes/` (`VacantesControllerTests.cs` si existe, sino crear) | MODIFY/CREATE | 1 test `PatchEstado_A_Cubierta_Returns400_CubrirVacanteRequiereCrearOcupacion`. 2 tests `VacantesControllerDetailTests` con campos `OcupacionDerivadaId`/`PersonaAsignadaNombre`. |
| Tests | `tests/SGV.Tests/Web/Vacantes/VacantesDetailsAndSidenavTests.cs` | MODIFY | 4 tests: botón visible Abierta/En Selección, oculto Cubierta/Cancelada, bloque persona asignada, link ver ocupación. |
| Tests | `tests/SGV.Tests/Web/Ocupaciones/OcupacionCreatePageTests.cs` | MODIFY | 3 tests `?vacanteId`: Abierta renderea form bloqueado, Cubierta muestra error, inexistente muestra error. |
| Tests | `tests/SGV.Tests/Web/Puesto/PuestoOcupacionesPageTests.cs` | MODIFY | 2 tests de `NewOcupacionButtonLabel`. |
| OpenSpec | `openspec/changes/invertir-flujo-cubrir/specs/**` | EXISTS | Deltas ya creados — no se tocan en design. |

## D. Riesgos residuales

| Riesgo | Likelihood post-mitigación | Mitigation de design | Residual |
|--------|------------------------------|----------------------|----------|
| Tests existentes `CambiarEstado_A_Cubierta_*` y `CubrirYLuegoFinalizar_*` rompen al eliminar el bloque de creación | Alta → Baja | Reescribir asserts: el primero afirma rechazo 400; el segundo se reescribe usando `OcupacionServicioComandos.CrearAsync` con `VacanteId` como setup del scenario Cubierta. | Bajo — es cambio esperado. |
| `VacanteEntity` no tiene nav `Ocupaciones` — `MapToDetailDto` no puede eager-load | — | Consulta lateral en `OcupacionRepository.ObtenerVigentePorVacanteAsync` (`AsNoTracking`, project `Id` + nombre). `VacanteServicioConsulta` la consume. | Bajo — mismo patrón que `ExistsAbiertaByPuestoAsync`. |
| `OcupacionConfiguracion.ActivePuestoIdUnique` revierte el Cubrir si existe Ocupación activa previa del mismo Puesto (escenario de inconsistencia histórica) | Media | Validar con `ExistsActiveByVacanteAsync` ANTES de crear. Si `DbUpdateException` cae en catch de `CrearAsync`, mapea a `VacanteYaCubierta` o `PuestoOcupado` según `constraintDetector`. | Bajo — el catch vigente ya maneja el path. |
| PuestoOcupaciones envía `vacanteId` — pero al usuario le falta saber que es la Vacante **abierta** del Puesto. Si hay múltiples Vacantes no terminales para un Puesto (debería estar prohibido por `ActivePuestoIdUnique` de Vacantes — ver D-5 spec archivado) | Baja | `IVacanteApiClient.ObtenerAbiertaPorPuestoAsync(puestoId)` nuevo método (opcional) o usar resultado del `ExisteVacanteAbiertaParaPuestoAsync` + extraer `vacanteId` vía endpoint de listado filtrado por `PuestoId` segmento `abiertas`. | Medio — requiere definir cómo obtener el `vacanteId` concreto desde el Web. |
| Renombre `PersonaIdRequeridoParaCubrir` → `CubrirVacanteRequiereCrearOcupacion` rompe clientes cacheados del código viejo | Baja | `[Obsolete]` backup apuntando al nuevo; tests nuevos usan exclusivamente el nuevo. El código viejo nunca se devuelve en runtime post-change. | Bajo. |

## E. Cobertura de tests

| Archivo | Tests nuevos/modificados | Cubren |
|---------|---------------------------|-------|
| `OcupacionServicioComandosTests.cs` | +5 (happy path, Vacante no encontrada 404, no abierta 400, ya cubierta 409, `PuestoId` mismatch 400, atomicidad — rollback al forzar fallo) | REQ-OCC-FORM-010 escenarios 1–8. Atomicidad con `FakeThrowingUnitOfWork`. |
| `VacanteServicioComandosTests.cs` | reescribir `CambiarEstado_A_Cubierta_ConPersonaId_CreaOcupacionYRegistraHistorial` → `CambiarEstado_A_Cubierta_Devuelve400ConCodigoNuevoYMensaje`; +1 con `PersonaId` populado (se ignora) | Spec `vacante-management` escenarios de rechazo. |
| `VacanteServicioConsultaTests.cs` | +2 (Cubierta con Ocupación derivada; Abierta sin). | Detalle defensivo (null sin lanzar). |
| `OcupacionesControllerTests.cs` (o equivalente Api) | +3 (201, 404 `VacanteNoEncontrada`, 409 `VacanteYaCubierta`). | REQ-OCC-FORM-010 vías HTTP. |
| `VacantesControllerTests.cs` (Api) | +1 rechazo PATCH a Cubierta + 2 Detail con campos. | Spec `vacante-management` + `vacante-web` AC5, AC2, AC3. |
| `VacantesDetailsAndSidenavTests.cs` (Web) | +4 (botón Abierta, En Selección, oculto Cubierta/Cancelada, bloque persona asignada). | Spec `vacante-web` AC1, AC6, AC3. |
| `OcupacionCreatePageTests.cs` (Web) | +3 (?vacanteId Abierta renderea form bloqueado, Cubierta error, 404 error). | REQ-OCC-FORM-001 escenarios `?vacanteId`. |
| `PuestoOcupacionesPageTests.cs` (Web) | +2 (label "Cubrir Vacante" con Vacante abierta sin Ocupación; "Nueva ocupación" con Ocupación activa). | REQ-OCC-NAV-008. |

Total aprox.: 21 tests, alineados con la **filosofía de testing** del repo (cobertura sobre reglas de negocio y behaviour observable, no getters/DTOs). Se prefieren `[Theory]` con `InlineData` para los happy-path-paramétricos (`EsCerrada`, `EsCubrible`). Tests transaccionales usan `[MySqlFact]` si toca `ExistsAbiertaByPuestoAsync` + `ObtenerVigentePorVacanteAsync` conjunto; el resto `[Fact]` con `FakeUnitOfWork`. R3 cerrada: la hidratación del nombre se hace en la proyección SQL (`Personas.Nombres + ' ' + Personas.Apellidos`), no en dominio, evitando un método extra.

## F. Chained PR strategy

**Strategy: Stacked PRs to `develop`** (chained-pr skill, decision gate "PR >400, each slice can land independently"). Cada PR mergea a `develop` en orden; el siguiente se brancha de `develop` post-merge.

**Total estimado**: 330–450 líneas cambiadas. Review budget: 400 líneas por PR (skill umbral).

| # | Título | Branch | Base | Scope | Estimación |
|---|--------|--------|------|-------|------------|
| **S1** | `feat(ocupaciones): invertir flujo cubrir — backend + wire contracts` | `feature/invertir-flujo-cubrir-s1-backend` | `develop` | `OcupacionServicioComandos.CrearAsync` con `VacanteId`; `VacanteServicioComandos.CambiarEstadoAsync` rechaza Cubierta; `CrearOcupacionRequest.VacanteId`; `VacanteDetailDto` extendido; `VacanteErrorCodigo.CubrirVacanteRequiereCrearOcupacion` (+ `OcupacionErrorCodigo` nuevos); `OcupacionRepository.ObtenerVigentePorVacanteAsync` + `ExistsActiveByVacanteAsync`; `VacanteServicioConsulta` hidratación; `IOcupacionRepository.ObtenerVigentePorVacanteAsync`; tests unitarios + API. | ~170–200 líneas |
| **S2** | `feat(web): crear ocupación con ?vacanteId + label dinámico en PuestoOcupaciones` | `feature/invertir-flujo-cubrir-s2-create-frontend` | `develop` (post-S1) | `Ocupaciones/Create.cshtml.cs` OnGet con `?vacanteId`; `_Form.cshtml` hidden + dropdown bloqueado + hint; `OcupacionFormPageModel`/`IOcupacionForm`; `PuestoOcupaciones.cshtml.cs` `NewOcupacionRouteValues` con `vacanteId` + `NewOcupacionButtonLabel`; `_CrossList.cshtml` label dinámico; `IOcupacionesCrossList.NewOcupacionButtonLabel`; tests Web. | ~120–150 líneas |
| **S3** | `feat(web): botón cubrir vacante + bloque persona asignada en Details` | `feature/invertir-flujo-cubrir-s3-vacante-details` | `develop` (post-S2) | `Vacantes/Details.cshtml` botón "Cubrir Vacante" + bloque persona asignada; `VacanteDetailViewModel` campos `OcupacionDerivadaId`/`PersonaAsignadaNombre`/`EsCubrible`; `Details.cshtml.cs` setup; tests Web. | ~80–110 líneas |

**Justificación**: S1 contiene los contracts (`CrearOcupacionRequest.VacanteId`, `VacanteDetailDto.OcupacionDerivadaId`) que S2 y S3 consumen — mergea primero para que S2/S3 puedan branchear de `develop`. S2 y S3 son fw-independent de frontend y pueden revisarse en paralelo conceptual, pero S3 usa `OcupacionDerivadaId` para mostrar el bloque "Persona asignada" — queda natural después de S2. Cada PR ≤400. Cada PR comunica `start`, `end`, `prior dependencies`, `follow-up work`, `out-of-scope`.

**Cada PR debe incluir** (Chain Context):
- Dependencias: `Depends on: S1 (#XXX merged)`. `📍 Current: S2`.
- Verificación: `dotnet test SGV.slnx` (suite + `[MySqlFact]` skip limpio en CI local), `bun run build` si tocó assets (no toca), `dotnet build`.
- Rollback scope (ver G).

## G. Rollback plan

| PR | Revert | Limpieza |
|----|--------|---------|
| S1 | `git revert <merge-s1>` restaura bloques de `VacanteServicioComandos.CambiarEstadoAsync`, `OcupacionServicioComandos.CrearAsync` sin rama `VacanteId`, `CrearOcupacionRequest` sin `VacanteId`, `VacanteDetailDto` sin campos, `VacanteErrorCodigo` con nombre viejo, novos códigos de `OcupacionErrorCodigo` quitados, métodos de `OcupacionRepository` eliminados. No hay migración que revertir. | S2 y S3 drilled deben rebasearse. |
| S2 | `git revert <merge-s2>` restaura `Ocupaciones/Create.cshtml.cs` OnGet sin `?vacanteId`, `_Form.cshtml` sin bloque `VacanteId`, `PuestoOcupaciones.cshtml.cs` `NewOcupacionRouteValues` con `puestoId`, `_CrossList.cshtml` con literal "Nueva ocupación", `IOcupacionesCrossList` sin label. | S3 rebasea o se cancela si dependía de S2. |
| S3 | `git revert <merge-s3>` restaura `Vacantes/Details.cshtml` sin botón ni bloque persona asignada; `VacanteDetailViewModel` sin campos extra. | Sin dependientes. |

Cada revert es independiente. S3 → S2 → S1 en orden inverso es el orden seguro si se rompe algo en runtime; el revert unit de S1 dispara que S2/S3 dejen de tener sentido — preferible revertir en orden.

## H. Open Questions para sdd-tasks

- **Q-T1** (Tests de integración `OcupacionServicioComandos.CrearAsync` con `VacanteId`): ¿usan `[MySqlFact]` o suficiente con `[Fact]` + `FakeUnitOfWork`? **Sugerencia**: `[Fact]` para unitarios puros de validation path; `[MySqlFact]` para el escenario de atomicidad (rollback real de transacción) — pero evaluar costo-beneficio; el `FakeThrowingUnitOfWork` del repo ya cubre rollback declarativo. Decisión final en `sdd-tasks`.
- **Q-T2** (Resolución `vacanteId` desde `PuestoOcupaciones`): cuando `HayVacanteAbierta && !HayOcupacionActiva`, el botón debe mandar `?vacanteId={X}`. `IVacanteApiClient` sólo expone `ExisteVacanteAbiertaParaPuestoAsync(bool)`. ¿Agregar `ObtenerAbiertaPorPuestoAsync(Guid puestoId) -> VacanteDto?` o consultar el listado segmentado `abiertas` con `PuestoId` filter? Decisión de: scope del nuevo método de integración.
- **Q-T3** (`_CrossList.cshtml` label): ¿se agrega `NewOcupacionButtonLabel` a `IOcupacionesCrossList` con default `"Nueva ocupación"` en `PersonaOcupaciones`, o se agrega como propiedad nueva forzada? **Sugerencia**: propiedad con default implícito para mantener `PersonaOcupaciones` sin cambios.
- **Q-T4** (docs): ¿`docs/migracion-inicial-sgv.sql` necesita actualizarse? **Respuesta anticipada**: no, porque no hay migración nueva (la columna `VacanteId` ya existe desde el change archivado). Verificar en `sdd-verify`.
- **Q-T5** (`docs/decisiones-implementacion.md`): ¿qué nueva entrada documentar? **Sugerencia**: nueva entrada "Inversión del flujo Cubrir (2026-08-09)" que registre D-1, D-3 y D-4 — se actualiza en el apply phase, no en design.

## I. Definition of Done

- [ ] Las 3 PRs (S1, S2, S3) mergeadas a `develop` en orden.
- [ ] `dotnet test SGV.slnx` global en verde (suite sin importar presencia de MySQL — skipeo limpio de `[MySqlFact]`).
- [ ] `dotnet build SGV.slnx` sin warnings nuevos (nullable, nullable annotations en `CrearOcupacionRequest.VacanteId`).
- [ ] `bun run build` en `src/SGV.Web` exitoso si S2/S3 tocaron assets (no se anticipa, pero verificar).
- [ ] `docs/decisiones-implementacion.md` actualizado con la inversión del flujo Cubrir (entrada nueva).
- [ ] OpenSpec change archivado con specs vigentes sincronizadas — el archive phase ejecuta la normalización DADO-CUANDO-ENTONCES (D-6) y aplica el renombre del código de error en la spec vigente.
- [ ] AC1–AC10 del proposal verificados por tests automatizados.