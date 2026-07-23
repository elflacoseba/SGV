# Exploración: Implementa Persona-Habilidades

## Estado Actual

### Backend — COMPLETO (7 capas, funcional en producción)

**Dominio (`SGV.Dominio`):**
- `PersonaHabilidad` — entidad sealed record con `PersonaId`, `HabilidadId`, `NivelHabilidadId`, `VerificadoAt`, `Fuente`.
- `Persona.AgregarHabilidad()` — método de dominio que valida unicidad de skill por persona.
- `Persona._habilidades` — colección interna `List<PersonaHabilidad>` expuesta como `IReadOnlyCollection`.

**Aplicación (`SGV.Aplicacion`):**
- `IPersonaSkillServicio` / `PersonaSkillServicio` — servicio completo con `ListAsync`, `UpsertAsync`, `DeleteAsync`. Valida existencia de Persona, Habilidad y NivelHabilidad antes de persistir.
- `IPersonaSkillRepository` — contrato con `ListByPersonaIdAsync`, `GetByPersonaAndSkillAsync`, `ListDetailedByPersonaIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`.
- DTOs existentes en `SGV.Aplicacion.Personas.Consultas.Dtos`: `PersonaSkillDetailDto` (Skill + Nivel anidados), `PersonaSkillDto` (SkillId, NivelId).
- `AsignarPersonaSkillRequest` (NivelId) en `SGV.Aplicacion.Personas.Comandos`.
- `PersonaSkillCommandResult` / `PersonaSkillError` / `PersonaSkillErrorType` en `SGV.Aplicacion.Personas.Comandos`.

**Infraestructura (`SGV.Infraestructura`):**
- `PersonaHabilidadEntity` — entidad EF con `PersonaId`, `HabilidadId`, `NivelHabilidadId`, `VerificadoAt`, `Fuente`.
- `PersonaHabilidadConfiguracion` — mapeo a tabla `PersonaHabilidades`.
- `PersonaSkillRepository` — implementación EF completa con queries detalladas (incluye proyección anidada Skill+Nivel).
- `DomainToPersistenceMapper` / `PersistenceToDomainMapper` — mapping bidireccional.
- Migraciones: tabla `PersonaHabilidades` existe desde la migración inicial (`InicialSgvo`), con FK a `Habilidades`, `NivelesHabilidad`, `Personas` e índice único compuesto `IX_PersonaHabilidades_PersonaId_HabilidadId`.

**API (`SGV.Api`):**
- `GET /api/v1/personas/{personaId:guid}/skills` → lista skills con detalle anidado.
- `PUT /api/v1/personas/{personaId:guid}/skills/{skillId:guid}` → asigna/actualiza nivel.
- `DELETE /api/v1/personas/{personaId:guid}/skills/{skillId:guid}` → elimina asociación.
- Write operations protegidas con `[Authorize(Roles = RolesSgv.Administrador)]`.

### Frontend (Web) — AUSENTE (gap completo)

**Integration layer (`SGV.Web/Integration/Personas`):**
- `IPersonaApiClient` — NO expone métodos de skills (sin `GetSkillsAsync`, `UpsertSkillAsync`, `DeleteSkillAsync`).
- `PersonaApiClient` — sin implementación de métodos de skills.
- `FakePersonaApiClient` (tests) — sin infraestructura de skills fakes.

**Razor Pages (`SGV.Web/Pages/Personas`):**
- `Index.cshtml` — listado CRUD de personas.
- `Create.cshtml` / `Edit.cshtml` / `Details.cshtml` — CRUD de datos básicos.
- **NO existe** `Habilidades.cshtml` / `Habilidades.cshtml.cs` (sub-página de habilidades de persona).
- `Details.cshtml` muestra datos de persona pero **no tiene sección de habilidades ni enlace a gestión de skills**.

**Contracts (`SGV.Contracts`):**
- `SGV.Contracts.Habilidades.Consultas.Dtos` — tiene `HabilidadDto`, `NivelHabilidadDto` (usables por Web).
- `SGV.Contracts.Personas` — TODO lo de skills **no existe**. Los DTOs viven en `SGV.Aplicacion` que Web no referencia.
- `SGV.Contracts.Organizacion` — tiene el patrón de referencia (`CargoSkillDetailDto`, `CargoSkillDto`, `AsignarCargoSkillRequest`, `CargoSkillCommandResult`, `CargoSkillDeleteResult`, `CargoSkillError`, `CargoSkillErrorType`).

### Tests

**Backend tests — cubiertos:**
- `PersonaSkillRepositoryTests.cs` — CRUD persistencia (agregar, duplicado, actualizar, eliminar, listar).
- `PersonaSkillServicioTests.cs` — servicio aplicación (éxito, upsert reemplaza, validaciones).
- `PersonaSkillControllerTests.cs` — tests de controlador con fake (GET, PUT, DELETE, errores).
- `PersonasControllerTests.cs` — tests de auth (sin credenciales, non-admin, admin).

**Frontend tests — inexistentes:**
- No hay tests web de Persona-Habilidades (no existe página que testear).
- `FakePersonaApiClient` necesita métodos de skill nuevos.

### Patrón de referencia: CargoHabilidades

El módulo `Cargos/Habilidades` implementa la paridad completa. Usa:
- `Pages/Organizacion/Cargos/Habilidades.cshtml` — grilla editable (listar, actualizar inline, quitar, asignar nuevo).
- `Pages/Organizacion/Cargos/Habilidades.cshtml.cs` — PageModel con handlers delegados.
- `CargoHabilidadesPostHandlers.cs` — handlers POST extraídos para testabilidad.
- `CargoSkillFormHelpers.cs` — helpers de formulario (input models, validación).
- `ICargoApiClient.GetSkillsAsync`, `UpsertSkillAsync`, `DeleteSkillAsync`.
- Contratos en `SGV.Contracts.Organizacion`: `CargoSkillDetailDto`, `CargoSkillDto`, `AsignarCargoSkillRequest`, `CargoSkillCommandResult`, `CargoSkillDeleteResult`.
- Pruebas: load, validation, mutation, PRG, delete error, contract, API client, anti-drift.

### Diferencias clave PersonaSkill vs CargoSkill

| Aspecto | CargoSkill | PersonaSkill |
|---------|-----------|--------------|
| Nivel | `NivelRequeridoId` | `NivelHabilidadId` |
| Ponderación | Sí (`decimal`) | No |
| EsObligatoria | Sí (`bool`) | No |
| VerificadoAt | No | Sí (`DateTime?`) |
| Fuente | No | Sí (`string?`, max 100) |
| ErrorTypes | NotFound, Validation, Conflict, Unauthorized, Forbidden, Transport | NotFound, Validation |
| DeleteResult | `CargoSkillDeleteResult` separado | No (usa `PersonaSkillCommandResult`) |
| DTOs en Contracts | Sí | No (están en Application) |

## Áreas Afectadas

- `src/SGV.Contracts/Personas/Consultas/Dtos/` — necesita `PersonaSkillDetailDto` y `PersonaSkillDto` (espejo desde Application).
- `src/SGV.Contracts/Personas/Comandos/` — necesita `AsignarPersonaSkillRequest`, `PersonaSkillCommandResult`, `PersonaSkillError`, `PersonaSkillErrorType`.
- `src/SGV.Web/Integration/Personas/IPersonaApiClient.cs` — necesita `GetSkillsAsync`, `UpsertSkillAsync`, `DeleteSkillAsync`.
- `src/SGV.Web/Integration/Personas/PersonaApiClient.cs` — implementación HTTP de los 3 métodos de skill.
- `src/SGV.Web/Pages/Personas/` — necesita `Habilidades.cshtml` + `Habilidades.cshtml.cs` + `PersonaHabilidadesPostHandlers.cs` + `PersonaSkillFormHelpers.cs`.
- `src/SGV.Web/Pages/Personas/Details.cshtml` — necesita enlace a la página de habilidades.
- `tests/SGV.Tests/Web/Persona/FakePersonaApiClient.cs` — necesita propiedades/métodos fake para skills.
- `tests/SGV.Tests/Web/Persona/` — necesita tests de página (load, validation, mutation, PRG, etc.).

## Enfoques

1. **Paridad completa con CargoHabilidades (recomendado)** — Seguir el mismo patrón: Razor Page con grilla editable, handlers POST extraídos, contratos en SGV.Contracts, tests completos.
   - Pros: Consistente con el codebase, patrón ya validado, reutiliza helpers existentes (`TransportFailureClassifier`, `PageFeedback`, `AuthSessionRedirector`).
   - Cons: PersonaSkill no tiene Ponderación/EsObligatoria — la UI será más simple. VerificadoAt y Fuente agregan campos que CargoHabilidades no tiene.
   - Esfuerzo: Medio (estimado 300-400 líneas distribuidas en Contracts, Integration, Pages, Tests).

2. **Mínimo viable** — Solo añadir la página de habilidades con lo básico (listar + asignar/quitar), sin mover DTOs a Contracts (exponer aplicación-layer DTOs directamente).
   - Pros: Menos código de Contracts, más rápido.
   - Cons: Rompe la separación de capas (Web depende de Aplicación), inconsistente con el resto del shell web. No recomendado.

3. **Inline en Details** — Mostrar/editar habilidades inline en la página de detalle de persona sin sub-página separada.
   - Pros: Sin navegación extra.
   - Cons: Complejidad del PageModel, inconsistente con el patrón CargoHabilidades, difícil de testear. No recomendado.

## Recomendación

**Enfoque 1 — Paridad completa con CargoHabilidades.** Los DTOs de PersonaSkill deben migrarse a `SGV.Contracts` (siguiendo el patrón Organización/CargoSkill) para que la Web pueda consumirlos sin acoplar `SGV.Web` a `SGV.Aplicacion`. La UI será una sub-página `/personas/{id:guid}/habilidades` con grilla similar a Cargos/Habilidades pero adaptada a los campos específicos de PersonaSkill (NivelHabilidadId en lugar de NivelRequeridoId, más VerificadoAt y Fuente). No incluir Ponderación ni EsObligatoria porque el modelo de dominio no los soporta.

## Riesgos

- **CRITICAL**: Los DTOs de PersonaSkill (`PersonaSkillDetailDto`, `PersonaSkillDto`, `AsignarPersonaSkillRequest`, `PersonaSkillCommandResult`) están en `SGV.Aplicacion` y deben migrarse a `SGV.Contracts` para que la Web los consuma. Si no se migran, la Web no puede deserializar las respuestas de la API sin acoplamiento directo a la capa de aplicación.
- **HIGH**: El API expone `PersonaSkillDetailDto` y `PersonaSkillDto` desde `SGV.Aplicacion.Personas.Consultas.Dtos`. Migrar estos tipos a `SGV.Contracts` requiere modificar el controlador de API y los mappers de infraestructura para que apunten a los nuevos tipos en Contracts.
- **MEDIUM**: PersonaSkill usa `NivelHabilidadId` (no `NivelRequeridoId` como CargoSkill). La UI debe nombrar el campo correctamente para evitar el drift que ocurrió en CargoHabilidades (ver mem #644).
- **LOW**: PersonaSkill tiene campos opcionales (`VerificadoAt`, `Fuente`) que CargoHabilidades no tiene. La UI debe decidir si mostrarlos/editarlos desde el vamos o en una iteración posterior.
- **LOW**: El enum `PersonaSkillErrorType` en Application solo tiene `NotFound` y `Validation`. La versión en Contracts debería considerar si necesita `Conflict`, `Unauthorized`, `Forbidden`, `Transport` como el de CargoSkill para que el cliente web pueda mapear correctamente los códigos HTTP.

## Preparado para Propuesta

**Sí.** El gap está claramente identificado: es exclusivamente frontend web. La propuesta debe definir el alcance exacto de la primera entrega (¿incluir VerificadoAt/Fuente desde el vamos o en iteración posterior?) y decidir si el enum de errores de PersonaSkill debe expandirse como el de CargoSkill.
