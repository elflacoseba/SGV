# Propuesta: Alineación doc/modelo de unicidad de Ocupaciones (issue #127)

## Resumen

La sección "Ocupaciones Activas" de `docs/decisiones-implementacion.md` declara un invariante que el modelo EF Core no implementa: "una única ocupación vigente por persona". El estado real del modelo, codificado en `OcupacionConfiguracion` y blindado por `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs`, garantiza únicamente "una sola activa por Puesto" y "una sola activa por la combinación Persona + Puesto" — alineado a su vez con la spec canónica `sgv-database/spec.md:298-300`. Este change corrige la prosa desactualizada, elimina la nota sobre cargos concurrentes y agrega un test de coherencia que parsea el markdown y asserta contra los shadow properties activos. No toca código de modelo, ni migraciones, ni spec.

## Problema

Issue #127 reporta la divergencia entre documentación y modelo. Cita verbatim:

> "Documentación de Ocupaciones inconsistente con el modelo: unicidad por persona no implementada."

Texto actual de `docs/decisiones-implementacion.md:19-21` (verbatim):

```
## Ocupaciones Activas

La versión inicial aplica una única ocupación vigente por puesto y una única ocupación
vigente por persona mediante columnas generadas con índices únicos. Si el negocio
requiere cargos concurrentes, se deberá agregar tipo de ocupación o porcentaje de
dedicación.
```

Evidencia de divergencia:

- `OcupacionConfiguracion.cs:42-53` define dos columnas generadas (`ActivePuestoIdUnique`, `ActivePersonaPuestoUnique`) y **no** declara `ActivePersonaIdUnique`.
- `Modelo_Ocupacion_ReemplazaUnicidadPersonaPorPersonaPuesto` (`tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs:151-174`) asserta `Assert.Null(entidad!.FindProperty("ActivePersonaIdUnique"))` y `Assert.NotNull(... "ActivePersonaPuestoUnique")` — el test fija el contrato vigente.
- `sgv-database/spec.md:298-300` autoriza explícitamente múltiples activas por Persona en Puestos distintos, exigiendo unicidad solo por Puesto y por Persona+Puesto.
- Historial de migraciones: la columna simple per-persona se dropeó en `20260624153353_ConvertirTipoAsignacionAEnumYActualizarUnicidad` y se sustituyó por la compuesta `PersonaId:PuestoId`.

## Enfoque

**Opción A: corregir la prosa al estado actual del modelo, sin tocar código ni spec.**

Sustentos:

1. La spec canónica `sgv-database/spec.md:298-300` autoriza el modelo actual: "Una Persona DEBE poder mantener múltiples ocupaciones activas simultáneamente cuando correspondan a Puestos distintos. El sistema DEBE conservar una sola ocupación activa por Puesto y DEBE impedir más de una ocupación activa para la misma combinación Persona + Puesto."
2. El test `Modelo_Ocupacion_ReemplazaUnicidadPersonaPorPersonaPuesto` ya codifica el invariante vigente; el modelo, el snapshot, el repositorio y el servicio son internamente consistentes entre sí.
3. Blast radius mínimo: una sección markdown + un test de coherencia.
4. El historial de migraciones (`20260614183103_InicialSgvo` → `20260624153353_ConvertirTipoAsignacionAEnumYActualizarUnicidad` → `20260711181615_FixActivePuestoIdUniqueType`) muestra que la decisión de reemplazar la columna simple por la compuesta fue deliberada; la prosa quedó atrás por descuido.
5. La nota "Si el negocio requiere cargos concurrentes…" se elimina (A3): ya no aplica porque cargos concurrentes en Puestos distintos **están permitidos** hoy.

Opción B (reintroducir `ActivePersonaIdUnique` + nueva migración) queda descartada: contradice la spec canónica, requiere delta de spec, blast radius amplio y riesgo operacional de datos duplicados en producción potencial.

## Áreas afectadas

| Área | Impacto | Descripción |
|------|---------|-------------|
| `docs/decisiones-implementacion.md` (L19-21) | Modificado (prosa) | Reescribir la sección "Ocupaciones Activas" para declarar los DOS invariantes vigentes (`ActivePuestoIdUnique`, `ActivePersonaPuestoUnique`) y eliminar la nota sobre cargos concurrentes. |
| `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs` (nuevo) | Nuevo | Test que parsea el markdown de `decisiones-implementacion.md`, extrae las invariantes declaradas en la sección "Ocupaciones Activas" y asserta que coinciden con los shadow properties activos del modelo EF Core. |

## Fuera de alcance

- NO reintroducir `ActivePersonaIdUnique`, ni agregar columna nueva.
- NO generar delta a `openspec/specs/sgv-database/spec.md` (la spec vigente ya coincide con el modelo).
- NO tocar `OcupacionConfiguracion.cs`, `IOcupacionRepository`, `OcupacionRepository`, `OcupacionServicioComandos` ni ninguna migración.
- NO modificar `AGENTS.md` ni otras secciones de `decisiones-implementacion.md`.
- NO regenerar `SgvDbContextModelSnapshot.cs` ni `docs/migracion-inicial-sgv.sql` (el modelo no cambia).

## Acceptance criteria

1. `docs/decisiones-implementacion.md:19-21` describe los DOS invariantes vigentes: una sola Ocupación activa por Puesto y una sola Ocupación activa por la combinación Persona + Puesto, citando explícitamente las columnas `ActivePuestoIdUnique` y `ActivePersonaPuestoUnique`. Sin frase sobre cargos concurrentes.
2. El test nuevo en `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs` parsea el markdown de `decisiones-implementacion.md`, ubica la sección "Ocupaciones Activas", y asserta que las invariantes declaradas matchean los shadow properties `ActivePuestoIdUnique` y `ActivePersonaPuestoUnique` presentes en `OcupacionEntity`. Tambien debe assertar que NO se menciona `ActivePersonaIdUnique` ni "única por persona" sin matización.
3. `dotnet test SGV.slnx` pasa verde: persistencia + aplicación + API + compat + web, con todos los `[MySqlFact]` habilitados contra `sgv_test`.

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|--------|------------|
| El parser de markdown del test nuevo se vuelve frágil ante reformateos menores. | Parser case-insensitive y tolerante a whitespace/saltos de línea; aserta solo presencia de las subcadenas clave (`ActivePuestoIdUnique`, `ActivePersonaPuestoUnique`, "Puesto", "Persona + Puesto"), no estructura literal. |
| El test introduce dependencia nueva (Markdig, ReverseMarkdown, etc.) que infla el grafo de tests. | Usar regex sobre `File.ReadAllText` + `Assert.Contains` de xUnit. Sin paquetes adicionales. |
| La prosa reescrita queda ambigua o confusa para futuros contributors. | Adoptar la misma estructura que el requisito "Historial de Ocupaciones" de la spec canónica: "una sola activa por X" + "una sola activa por combinación Y + Z". |
| La cobertura del test RED-only se rompe si el test parsea desde un path distinto al cwd del runner. | Resolver la ruta del archivo desde `AppContext.BaseDirectory` o desde un path relativo a la solución; test marca `[Fact]` simple sin `[MySqlFact]`. |

## Trabajo relacionado

- Issue #127 (GitHub) — disparador del change.
- Change archivado `2026-07-11-fix-active-puesto-id-unique-type` (issue #59) — precedente del patrón soft-delete con columna generada + índice único en MySQL; patrón aplicado a `ActivePuestoIdUnique`.
- Spec canónica `openspec/specs/sgv-database/spec.md:298-325` — fuente de verdad del invariante vigente.
- `exploration.md` (este mismo change) — análisis comparativo de Opción A vs. B y recomendación.

## Próximos pasos

1. **sdd-spec**: opcional y recomendado. Delta `openspec/changes/2026-07-13-fix-127-doc-ocupaciones-unicidad-persona/specs/decisiones-implementacion-mantenimiento/spec.md` declarando que la sección "Ocupaciones Activas" de `decisiones-implementacion.md` debe mantenerse coherente con el modelo y protegida por el test de coherencia. No obligatorio porque la spec de comportamiento (`sgv-database`) ya cubre el invariante.
2. **sdd-design**: no necesario (cambio de prosa + un test).
3. **sdd-tasks**: opcional y de bajo alcance para guiar el apply (3-4 tareas: rewrite markdown, crear test, validar suite).
4. **sdd-apply**: editar `docs/decisiones-implementacion.md:19-21` y crear `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs` con parser regex.
5. **sdd-verify**: `dotnet test SGV.slnx` debe pasar verde; el nuevo test debe fallar (RED) si se revierte la prosa al texto con "única por persona" simple.
6. **sdd-archive**: cerrar el change con `archive-report.md` referenciando el issue #127 y el fix #59 como precedente.
