# Archive Report: `asociar-habilidades-cargos-personas`

**Date**: 2026-06-21
**Status**: Archived (intentional — orchestrator override: no archive folder move, keep artifacts alongside active changes)

---

## Change Summary

**What was implemented**: Asociación de habilidades a Cargos y Personas mediante endpoints de subrecurso REST. Se agregaron endpoints `GET/PUT/DELETE /api/v1/cargos/{cargoId}/skills` y `GET/PUT/DELETE /api/v1/personas/{personaId}/skills` para gestionar las habilidades requeridas/poseídas en cada entidad, con su nivel de competencia asociado.

**Why**: El producto necesita modelar qué habilidades requiere un Cargo (perfil del puesto) y qué habilidades posee una Persona (perfil del candidato), cada una con su nivel (NivelHabilidad). Esto habilita la comparación de compatibilidad entre candidatos y vacantes.

**Scope**:
- Capa de aplicación: DTOs, servicios, requests, command results, y validaciones para `CargoSkill` y `PersonaSkill`
- Capa de infraestructura: Repositorios EF Core con Pomelo/MySQL, mapeos domain↔persistence, registro DI
- Capa de API: Endpoints de subrecurso en `CargosController` y `PersonasController`, documentación Swagger
- Pruebas: 14 tests de aplicación, 18 tests de persistencia, 23 tests de API, más tests de documentación Swagger y aislamiento de payload

---

## Delivery Summary

3 stacked PRs targeting `develop` (stacked-to-develop strategy):

| PR | Branch | Estado | Archivos | Líneas | URL |
|----|--------|--------|----------|--------|-----|
| #34 | `feat/skill-assignment-contracts` | ✅ Merged | 26 | +1804 | https://github.com/elflacoseba/SGV/pull/34 |
| #35 | `feat/skill-assignment-persistence` | ✅ Merged | 10 | +896 | https://github.com/elflacoseba/SGV/pull/35 |
| #36 | `feat/skill-assignment-api` | 🔄 Open (last PR) | 6 | +950 | https://github.com/elflacoseba/SGV/pull/36 |

**Líneas totales del cambio**: ~3,650

---

## Artifact Completeness Checklist

| Artifact | Estado | Notas |
|----------|--------|-------|
| proposal.md | ✅ (Engram) | Creado durante SDD proposal phase |
| spec (delta specs) | ✅ (Engram) | Delta specs para cargo-skill-assignment, persona-skill-assignment, cargo-management, persona-management, sgv-readonly-api, sgv-database, sgv-persistence-architecture |
| design.md | ✅ (Engram) | Diseño con endpoints anidados, servicios, repositorios |
| tasks.md | ✅ (Engram) | Plan de 3 slices con chained PRs a develop |
| apply-progress.md | ✅ (Engram) | 3 fases de apply completadas |
| verify-report.md | ✅ (Engram) | Re-verificación PASS (770/770 tests, 18/18 spec compliance) |
| archive-report.md | ✅ (filesystem) | Este archivo |

> **Nota**: Este cambio se gestionó con artifacts en Engram (no en filesystem) durante las fases SDD. El archive-report.md se persiste en filesystem como parte del registro de auditoría.

---

## Spec Conformance Summary

**18/18 spec scenarios compliant** (verified in re-verify phase):

| Spec Domain | Escenarios | Compliant |
|-------------|-----------|-----------|
| cargo-skill-assignment | Asignar skill a cargo, reemplazar nivel, eliminar asignación, listar skills de cargo | ✅ 4/4 |
| persona-skill-assignment | Asignar skill a persona, reemplazar nivel, eliminar asignación, listar skills de persona | ✅ 4/4 |
| cargo-management | Crear cargo, codigo duplicado, nivel inexistente, actualizar, desactivar, reactivar — **payload isolation**: GetById no incluye campos de skill assignment | ✅ 3/3 |
| persona-management | Crear persona, legajo duplicado, datos de contacto — **payload isolation**: persona DTO no incluye habilidades asignadas | ✅ 2/2 |
| sgv-readonly-api | Recursos soportados, documentación Swagger — **subresource documentation**: Swagger documenta los 4 nuevos paths de subrecurso | ✅ 3/3 |
| sgv-persistence-architecture | Unique constraints en CargoHabilidad y PersonaHabilidad, cascade delete **NO** configurado, FK a NivelHabilidad | ✅ 2/2 |

---

## Test Evidence

**770/770 tests passing** (dotnet test):

| Test Suite | Cantidad | Resultado |
|-----------|----------|-----------|
| Aplicación: CargoSkillServicioTests | 7 | ✅ |
| Aplicación: PersonaSkillServicioTests | 7 | ✅ |
| Persistencia: CargoSkillRepositoryTests | 7 | ✅ |
| Persistencia: PersonaSkillRepositoryTests | 7 | ✅ |
| Persistencia: NivelHabilidadRepositoryTests | 4 | ✅ |
| API: CargoSkillControllerTests | 11 | ✅ |
| API: PersonaSkillControllerTests | 11 | ✅ |
| API: CargosControllerTests (payload isolation) | 1 | ✅ |
| API: SwaggerConfigurationTests (subresource docs, catalog isolation) | 3 | ✅ |
| Tests preexistentes (no modificados) | ~712 | ✅ |

**Distribución**: 14 aplicación + 18 persistencia + 23 API nuevos + ~715 preexistentes = 770 total

---

## Lessons Learned / Issues Discovered

### 1. Swagger Subresource Documentation No Cubierta Inicialmente
**Issue**: La primera verificación encontró que los nuevos paths de subrecurso (`/api/v1/cargos/{cargoId}/skills`, etc.) no tenían tests que verificaran su documentación en Swagger.
**Fix**: Se agregaron 3 tests en `SwaggerConfigurationTests.cs`: `SkillSubresources_AreDocumented`, `SkillsCatalog_DocumentsOnlyCatalogOperations`, `SwaggerDocument_NoCargoHabilidadOrPersonaHabilidadPaths`.
**Lección**: Los tests de documentación Swagger no deben olvidarse cuando se agregan nuevos endpoints. Incluir un test de "subresource documentation" en el checklist de apply.

### 2. Aislamiento de Payload Parent Resource
**Issue**: La especificación requería que `GET /api/v1/cargos/{id}` NO incluyera campos de skill assignment en su respuesta. Esto no estaba testeado.
**Fix**: Se agregó `GetById_ParentPayloadDoesNotIncludeSkillAssignmentFields` en `CargosControllerTests.cs`. El DTO ya no contenía esos campos (era correcto), pero faltaba el test.
**Lección**: Para cambios que agregan subrecursos, siempre verificar que el parent resource no filtre datos del subrecurso en su respuesta.

### 3. Nombre de Test Engañoso
**Issue**: El test `GetSkills_NonExistentCargo_ReturnsNotFound` sugería que un cargo inexistente devuelve 404, pero el comportamiento correcto por spec es devolver 200 con array vacío (el servicio de aplicación retorna lista vacía para parents inexistentes).
**Fix**: Renombrado a `GetSkills_NonExistentCargo_ReturnsOkWithEmptyArray`.
**Lección**: Los nombres de test deben reflejar exactamente el comportamiento esperado, no una suposición temprana.

### 4. Tasks.md Inconsistente con Estrategia de Entrega
**Issue**: `tasks.md` especificaba `stacked-to-main` como estrategia de chain, pero la decisión de producto usaba `develop` como base.
**Fix**: Se actualizó `tasks.md` línea 12 a `stacked-to-develop`.
**Lección**: Validar que la estrategia de entrega documentada coincida con la configuración real de los PRs antes del archive.

### 5. Budget de Líneas Excedido (Chained PRs)
**Issue**: Cada slice individual excede el budget recomendado de 800 líneas (1804, 896 y 950 líneas respectivamente). La primera verificación marcó esto como WARNING.
**Decisión**: Se documentó en `tasks.md` que los límites de los slices son coherentes (capa de aplicación, persistencia, API) y cada slice incluye sus propios tests. Se aceptó la desviación del budget.
**Lección**: Para cambios grandes con arquitectura Clean Architecture, es aceptable que los slices de aplicación sean más grandes porque incluyen DTOs, servicios, interfaces, y tests. Documentar la decisión explícitamente.

---

## Engram Observation IDs

Para trazabilidad, los artifacts en Engram:

| Artifact | Observation ID |
|----------|---------------|
| verify-report (initial) | #189 |
| apply-progress (verify fixes) | #186 |
| archive blockers discovery | #190 |
| re-verify session summary | #191 |
| PR creation record | #192 |

---

## Nota de Archive

Por instrucción del orquestador, este change **NO se movió** a `openspec/changes/archive/`. El archive-report.md se creó junto a los demás artifacts del cambio. El SDD cycle está completo.

**SDD Cycle**: Proposal → Spec → Design → Tasks → Apply (3 slices) → Verify (2 ciclos) → Archive ✅
