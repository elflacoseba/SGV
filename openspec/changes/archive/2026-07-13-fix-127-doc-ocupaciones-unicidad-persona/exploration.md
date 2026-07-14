# Exploración: Alineación doc/modelo de unicidad de Ocupaciones (issue #127)

**Issue GitHub**: #127 — "Documentación de Ocupaciones inconsistente con el modelo: unicidad por persona no implementada"
**Change**: `2026-07-13-fix-127-doc-ocupaciones-unicidad-persona`
**Modo**: exploratorio — solo investigación, sin código, sin migración, sin tests
**Artifact store**: híbrido — Engram topic key `sdd/2026-07-13-fix-127-doc-ocupaciones-unicidad-persona/explore` + filesystem en `openspec/changes/2026-07-13-fix-127-doc-ocupaciones-unicidad-persona/exploration.md`
**Strict TDD**: ACTIVO. Ver `openspec/config.yaml:11`. Tests RED antes de implementación cuando llegue a `sdd-spec`/`sdd-tasks`.

---

## Contexto y disparador

`docs/decisiones-implementacion.md:19-21` afirma:

> "La versión inicial aplica una única ocupación vigente por puesto y una única ocupación vigente por persona mediante columnas generadas con índices únicos. Si el negocio requiere cargos concurrentes, se deberá agregar tipo de ocupación o porcentaje de dedicación."

El issue #127 reporta que **el modelo sólo garantiza** unicidad por `ActivePuestoIdUnique` (columna simple sobre `PuestoId`) y por `ActivePersonaPuestoUnique` (columna compuesta sobre `PersonaId`+`PuestoId`). No existe la columna simple `ActivePersonaIdUnique` que la doc menciona para "una única ocupación vigente por persona".

Aceptación pedida:
1. Documentación y modelo coinciden.
2. Tests cubren el invariante documentado.

Recomendación del issue: Opción A (corregir doc al estado actual del modelo) o Opción B (agregar columna generada + índice único + migración forward-compatible).

## Estado actual: documentación

`docs/decisiones-implementacion.md:19-21` (verbatim):

```
## Ocupaciones Activas

La versión inicial aplica una única ocupación vigente por puesto y una única ocupación
vigente por persona mediante columnas generadas con índices únicos. Si el negocio
requiere cargos concurrentes, se deberá agregar tipo de ocupación o porcentaje de
dedicación.
```

Esta sección **contradice** a su vez la spec vigente `openspec/specs/sgv-database/spec.md:298-300`, cuyo requisito "Historial de Ocupaciones" dice textualmente:

> "Una Persona DEBE poder mantener múltiples ocupaciones activas simultáneamente cuando correspondan a Puestos distintos. El sistema DEBE conservar una sola ocupación activa por Puesto y DEBE impedir más de una ocupación activa para la misma combinación Persona + Puesto."

Es decir: la **spec canónica autoriza múltiples ocupaciones activas simultáneas por persona cuando los puestos son distintos**, y la doc de implementación la contradice.

No se localizaron otras menciones a "única ocupación vigente por persona" en `docs/` ni en `AGENTS.md`.

## Estado actual: modelo

### Entidad (`src/SGV.Infraestructura/Persistencia/Entidades/OcupacionEntity.cs`)

```csharp
public sealed class OcupacionEntity : AuditableEntityBase
{
    public Guid PersonaId { get; set; }                   // char(36)
    public Guid PuestoId  { get; set; }                   // char(36)
    public DateOnly FechaInicio { get; set; }             // date
    public DateOnly? FechaFin   { get; set; }             // date, null = activa
    public TipoAsignacion TipoAsignacion { get; set; }   // int (enum)
    public string? Observaciones { get; set; }            // varchar(1000)
}
```

Tipo `TipoAsignacion`: enum (`Permanente=0, Interina=1, Temporal=2`), migrado de string a int en `20260624153353_ConvertirTipoAsignacionAEnumYActualizarUnicidad`.

### Configuración (`src/SGV.Infraestructura/Persistencia/Configuraciones/OcupacionConfiguracion.cs:42-53`)

```csharp
builder.Property<string?>("ActivePuestoIdUnique")
    .HasMaxLength(36)
    .UseCollation("ascii_general_ci")
    .HasComputedColumnSql("CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END")
    .IsRequired(false);
builder.HasIndex("ActivePuestoIdUnique").IsUnique();

builder.Property<string?>("ActivePersonaPuestoUnique")
    .HasComputedColumnSql("CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN CONCAT(`PersonaId`, ':', `PuestoId`) ELSE NULL END")
    .IsRequired(false)
    .HasMaxLength(100);
builder.HasIndex("ActivePersonaPuestoUnique").IsUnique();
```

**Confirmado**: existen exactamente dos columnas generadas con índice único:
- `ActivePuestoIdUnique` (per-puesto) — UNIQUE
- `ActivePersonaPuestoUnique` (per-persona+puesto compuesto) — UNIQUE

**NO existe `ActivePersonaIdUnique`** (per-persona simple).

`SgvDbContextModelSnapshot.cs:978-1038` confirma el mismo modelo actual (snapshot vigente, regenerado por la última migración aplicada).

### Capa de dominio (`src/SGV.Dominio/Ocupaciones/Ocupacion.cs`)

La clase `Ocupacion` (record class) **no impone reglas de unicidad**. Sus invariantes son solo temporales: `FechaFin >= FechaInicio` y `EsVigente => FechaFin is null && !IsDeleted`. No hay un método `EsUnicaPorPersona` o equivalente. El dominio es agnóstico a la regla "una persona, una ocupación".

### Capa de aplicación (`src/SGV.Aplicacion/Ocupaciones`)

`IOcupacionRepository` (contrato, líneas 54-64) declara solo dos métodos de verificación de existencia activa:
- `ExistsActiveByPuestoAsync(Guid puestoId, Guid? excludingId = null, …)`
- `ExistsActiveByPersonaYPuestoAsync(Guid personaId, Guid puestoId, Guid? excludingId = null, …)`

**No existe** `ExistsActiveByPersonaAsync`. La impl en `src/SGV.Infraestructura/Persistencia/Repositorios/OcupacionRepository.cs:139-171` lo confirma.

`OcupacionServicioComandos` (`CrearAsync:144-154`, `ActualizarAsync:218-228`, `ReactivarAsync:374-384`) invoca **solo** esas dos verificaciones — primero `ExistsActiveByPersonaYPuestoAsync` (más específica) y luego `ExistsActiveByPuestoAsync`. La regla "única ocupación vigente por persona" **no se valida en código de aplicación**.

### Capa web/API

`src/SGV.Api/Controllers/OcupacionesController.cs` es CRUD estándar sin reglas adicionales de unicidad por persona; delega al servicio de aplicación.

`src/SGV.Web` no contiene UI ni integración para Ocupaciones (búsqueda en `src/SGV.Web/Integration` y `src/SGV.Web/Pages` no devuelve resultados para "Ocupacion"). No hay formularios ni validaciones cliente que tengan que cambiar.

## Migraciones existentes (con file names)

Cadena completa en `src/SGV.Infraestructura/Persistencia/Migraciones/`:

| Migración | Archivo | Efecto sobre Ocupaciones |
|---|---|---|
| InicialSgvo (2026-06-14) | `20260614183103_InicialSgvo.cs:605-647` | Crea tabla `Ocupaciones` con `ActivePersonaIdUnique` (computed `int`) + `ActivePuestoIdUnique` (computed `int`) + sus `UNIQUE INDEX` (líneas 617-618, 1103-1112). **Bug de tipo presente desde el origen** (int no admite Guid). |
| AgregarDatosSemillaBase | `20260614183109_AgregarDatosSemillaBase.cs` | Carga semilla. Sin cambios estructurales. |
| CambiarTipoUnidadATablaTipoUnidadOrganizativa | `20260616190624_*` | No toca `Ocupaciones`. |
| CambiarNivelStringANivelId | `20260618180508_*` | No toca `Ocupaciones`. |
| VincularIdentityUsuariosAPersonas | `20260621202540_*` | No toca `Ocupaciones`. |
| ConvertirTipoAsignacionAEnumYActualizarUnicidad | `20260624153353_ConvertirTipoAsignacionAEnumYActualizarUnicidad.cs` | **Dropea** `IX_Ocupaciones_ActivePersonaIdUnique` (línea 14-16) y la columna `ActivePersonaIdUnique` (línea 18-20) — corrige el bug de tipo de la columna simple sobre `PersonaId`. En su lugar **crea** `ActivePersonaPuestoUnique` compuesto (líneas 47-62) con `varchar(100)` y `UNIQUE INDEX`. `ActivePuestoIdUnique` permanece con tipo `int` (bug sin resolver). |
| FixActivePuestoIdUniqueType | `20260711181615_FixActivePuestoIdUniqueType.cs` | Issue #59. `Down()` `NotSupportedException`. Cambia `ActivePuestoIdUnique` de `int` a `varchar(36)` con `ascii_general_ci`, drop+create index en una transacción. Forward-only. Archivo archivado en `openspec/changes/2026-07-11-fix-active-puesto-id-unique-type/`. |

**Lectura del historial**: el modelo alguna vez **sí** intentó garantizar "una persona, una ocupación activa" (columna `ActivePersonaIdUnique` en la migración inicial) pero esa columna se dropeó el `2026-06-24` porque su tipo `int` era incompatible con el `Guid PersonaId` (mismo bug que #59 luego corrigió para `ActivePuestoIdUnique`). El arreglo elegido fue **componer** `PersonaId:PuestoId`, lo cual materializa el invariante "una persona, **un puesto**" pero libera "una persona, **cualquier puesto**". La doc no fue actualizada al cambiar el invariante.

## Cobertura de tests actual

### Tests estructurales del modelo

`tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs`:

- `Modelo_ConfiguraColumnaGeneradaUnicaParaOcupacionVigentePorPuesto` (44-71): valida `ActivePuestoIdUnique` (nombre, computed SQL con `FechaFin`/`IsDeleted`/`PuestoId`, índice único). Tras el fix #59 también asserta tipo `string`/`char(36)`.
- `Modelo_Ocupacion_ReemplazaUnicidadPersonaPorPersonaPuesto` (151-174): **asserta explícitamente** `Assert.Null(entidad!.FindProperty("ActivePersonaIdUnique"))` y `Assert.NotNull(entidad!.FindProperty("ActivePersonaPuestoUnique"))`. La suite **fijó el contrato actual** en este test: la columna simple per-persona **no debe existir**.
- `Modelo_Ocupacion_ConservaUnicidadActivaPorPuesto` (133-149): refuerzo del test anterior.

### Tests de comportamiento de aplicación

`tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs`:

| Test | Línea | Cubre |
|---|---|---|
| `CrearAsync_PuestoUnicoConflictivo_Retorna409` | 125 | `ExistsActiveByPuestoAsync` en Crear |
| `CrearAsync_PersonaYPuestoUnicoConflictivo_Retorna409` | 144 | `ExistsActiveByPersonaYPuestoAsync` en Crear |
| `ActualizarAsync_PuestoOcupado_Retorna409` | 328 | igual en Actualizar |
| `ActualizarAsync_PersonaYPuestoOcupados_Retorna409` | 353 | igual en Actualizar |
| `ReactivarAsync_PuestoConflictivo_Retorna409` | 548 | igual en Reactivar |

**No existe** ningún test `PersonaUnica_*` o `CrearAsync_PersonaOcupada_*` ni para `Actualizar` ni para `Reactivar`.

### Tests de repositorio (persistencia)

`tests/SGV.Tests/Persistencia/OcupacionRepositoryTests.cs`: cobertura solo de `ExistsActiveByPuestoAsync_*` (4 tests, líneas 275-358) y `ExistsActiveByPersonaYPuestoAsync_*` (3 tests, líneas 361-426). Ningún test de "persona tiene 2 ocupaciones activas en puestos distintos → qué pasa".

### Tests de dominio

`tests/SGV.Tests/Dominio/Ocupaciones/OcupacionTests.cs` (335 líneas): cubren constructor, finalizar, eliminar, reactivar, actualizar, inmutabilidad de `TipoAsignacion`. No tocan reglas de unicidad porque el dominio no las tiene.

### Conclusión de cobertura

El suite actual **codifica el modelo vigente** (compuesto `Persona+Puesto`, simple `Puesto`). Si se adopta Opción B ("agregar `ActivePersonaIdUnique` simple"), será necesario:
- Reemplazar `Modelo_Ocupacion_ReemplazaUnicidadPersonaPorPersonaPuesto` por un test que asserta **ambas** columnas (compuesto y simple) presentes, con sus computed SQL e índices únicos correctos.
- Agregar tests `[MySqlFact]` en `OcupacionRepositoryTests` que inserten dos ocupaciones activas de la **misma persona** en puestos distintos y asserten el `DbUpdateException` con violación de `IX_Ocupaciones_ActivePersonaIdUnique`.
- Agregar tests de servicio en `OcupacionServicioComandosTests` para `PersonaOcupada` (mismo flujo que `PuestoOcupado` pero invocando un `ExistsActiveByPersonaAsync` nuevo).

Si se adopta Opción A (corregir doc), no se requieren nuevos tests porque el comportamiento real ya está cubierto por los tests existentes.

## Reglas de dominio / aplicación relacionadas

| Capa | Regla | Estado |
|---|---|---|
| Dominio | Validación temporal `FechaFin >= FechaInicio` | Cubierta (`OcupacionTests.CrearConFechaFinAnteriorAFechaInicio_LanzaInvalidOperationException`). |
| Dominio | Vigencia (`EsVigente = FechaFin is null && !IsDeleted`) | Cubierta. |
| Dominio | RequerirEditable antes de Actualizar/Finalizar/Eliminar | Cubierta. |
| Aplicación | Unicidad por puesto activo | Cubierta (`ExistsActiveByPuestoAsync`). |
| Aplicación | Unicidad por Persona+Puesto compuesto | Cubierta (`ExistsActiveByPersonaYPuestoAsync`). |
| Aplicación | Unicidad por persona activa (simple) | **NO cubierta, NO implementada**. |
| Persistencia | Índice DB sobre `ActivePuestoIdUnique` | Cubierta (snapshot + migraciones). |
| Persistencia | Índice DB sobre `ActivePersonaPuestoUnique` | Cubierta. |
| Persistencia | Índice DB sobre `ActivePersonaIdUnique` simple | **NO existe** (dropeado en `20260624153353`). |
| Spec `sgv-database` | "Una Persona DEBE poder mantener múltiples ocupaciones activas simultáneamente cuando correspondan a Puestos distintos" | Cubierta por la **ausencia** de unicidad per-persona simple. |

## Análisis por opción

### Opción A — actualizar docs al estado actual del modelo

**Cambio**: editar `docs/decisiones-implementacion.md:19-21` para reflejar la realidad: **no** se garantiza "una única ocupación vigente por persona"; se garantiza "una sola ocupación activa por Puesto" y "una sola ocupación activa por la combinación Persona + Puesto". Eliminar la oración sobre "tipo de ocupación o porcentaje de dedicación" o reubicarla como nota de extensibilidad futura.

Reemplazo sugerido:

```
## Ocupaciones Activas

La versión inicial aplica una única ocupación vigente por Puesto y una única
ocupación vigente por la combinación Persona + Puesto, mediante columnas generadas
con índices únicos (`ActivePuestoIdUnique`, `ActivePersonaPuestoUnique`).
Una Persona puede mantener múltiples ocupaciones activas simultáneas siempre que
pertenezcan a Puestos distintos. Si el negocio requiere limitar a una sola
ocupación vigente por persona (independientemente del Puesto), se deberá agregar
la columna `ActivePersonaIdUnique` simple con su índice único y la verificación
de unicidad correspondiente en la capa de aplicación.
```

| Pros | Contras |
|---|---|
| Blast radius mínimo (1 archivo markdown, 0 archivos de código). | No resuelve el problema raíz si la intención de negocio es limitar a una por persona. |
| Alinear doc con código/spec vigente reduce confusión para nuevos contributors. | Cierra la puerta a "una persona, una ocupación" sin propuesta explícita. |
| Cero riesgo operacional (sin migración, sin tests nuevos). | El test `Modelo_Ocupacion_ReemplazaUnicidadPersonaPorPersonaPuesto` ya documenta el comportamiento vigente — la doc pasa a ser redundante con la spec y el test. |
| Cero riesgo de pérdida de datos (solo cambia narrativa). | — |
| La spec `sgv-database/spec.md:298-300` ya cubre la regla canónica. | — |

**Esfuerzo**: Bajo (1 archivo, ~10 líneas modificadas).
**Tests afectados**: 0 (suite ya alineada).

### Opción B — agregar `ActivePersonaIdUnique` + migración + checks de aplicación

**Cambio**: reintroducir la columna simple per-persona y el método de verificación correspondiente, asumiendo que la intención de negocio sí es "una sola ocupación activa por persona".

**Cambios concretos** (orden sugerido):

1. **`OcupacionConfiguracion.cs`**: agregar `ActivePersonaIdUnique` (computed `varchar(36)` con `ascii_general_ci`, expresión `CASE WHEN FechaFin IS NULL AND IsDeleted = 0 THEN PersonaId ELSE NULL END`) e `HasIndex(...).IsUnique()`. Mismo patrón que `ActivePuestoIdUnique` (líneas 42-47).
2. **`IOcupacionRepository`**: agregar `Task<bool> ExistsActiveByPersonaAsync(Guid personaId, Guid? excludingId = null, CancellationToken …)`.
3. **`OcupacionRepository`**: implementar el método (`AnyAsync(o => o.PersonaId == … && !o.IsDeleted && o.FechaFin == null && o.Id != excludingId)`).
4. **`OcupacionServicioComandos`**: invocar la nueva verificación en `CrearAsync`, `ActualizarAsync`, `ReactivarAsync` (entre la validación de `PersonaInactiva` y `ExistsActiveByPersonaYPuestoAsync`, en el mismo orden que la verificación por puesto). Mensaje de error sugerido: `"La persona ya tiene una ocupación activa."` con `OcupacionErrorType.Conflict`, código `"PersonaOcupada"`.
5. **Migración nueva**: `migrationBuilder.AddColumn<string>` + `migrationBuilder.CreateIndex(unique: true)`. La columna computada no requiere `AlterColumn` previo porque la tabla ya existe. Patrón: análogo al de `20260624153353_ConvertirTipoAsignacionAEnumYActualizarUnicidad.cs:99-110` (que re-creó `ActivePersonaIdUnique` como int, ahora con tipo correcto).
6. **Pre-apply SQL probe** (en `proposal.md`, sección "Pre-flight checks"):
   ```sql
   SELECT PersonaId, COUNT(*) AS concurrent
   FROM Ocupaciones
   WHERE IsDeleted = 0 AND FechaFin IS NULL
   GROUP BY PersonaId
   HAVING COUNT(*) > 1;
   ```
   Cualquier fila devuelta **bloquea** la creación del índice único (la columna computada devolvería el mismo `PersonaId` para ambas filas activas). Política sugerida: el operador debe finalizar/eliminar las duplicadas antes de aplicar la migración, o aceptar la pérdida de la segunda ocupación en una migración correctiva explícita (forward-only).
7. **Tests RED primero** (strict_tdd):
   - `ModeloPersistenciaTests.Modelo_ActivePersonaIdUnique_EsChar36ConIndiceUnico` — asserta existencia de la sombra, tipo `string`, colación `ascii_general_ci`, computed SQL correcto, índice único.
   - `OcupacionRepositoryTests.ExistsActiveByPersonaAsync_*` — `[Theory]` con activa (true), finalizada (false), eliminada (false), excludingId ignora self, distinto personaId (false).
   - `OcupacionRepositoryTests.AddAsync_DosOcupacionesActivasMismaPersonaDistintoPuesto_DisparaUniqueViolation` — `[MySqlFact]` inserta dos `OcupacionEntity` con misma `PersonaId` y `PuestoId` distinto, espera `DbUpdateException` cuyo mensaje contiene `IX_Ocupaciones_ActivePersonaIdUnique`.
   - `OcupacionServicioComandosTests.CrearAsync_PersonaOcupada_Retorna409` — análogo a `CrearAsync_PuestoUnicoConflictivo_Retorna409` pero con misma persona/puesto distinto.
   - Actualizar `Modelo_Ocupacion_ReemplazaUnicidadPersonaPorPersonaPuesto` → renombrar a `Modelo_Ocupacion_TieneUnicidadPorPersonaYPuestoYPorPersonaYPorPuesto` (o similar) que asserta **tres** columnas/índices.
8. **Regenerar** `SgvDbContextModelSnapshot.cs` y `docs/migracion-inicial-sgv.sql`.

| Pros | Contras |
|---|---|
| Doc y modelo vuelven a coincidir con la intención literal del issue. | Blast radius amplio: 1 migración nueva + 4 archivos de código + 4+ archivos de tests + script SQL regenerado. |
| Restaura la simetría con `ActivePuestoIdUnique` (mismo patrón). | Riesgo operacional: si producción tiene personas con múltiples ocupaciones activas en puestos distintos, el índice único **bloquea** la migración con `Duplicate entry`. |
| El test canario contra MySQL real protege contra drift futuro. | La spec canónica `sgv-database/spec.md:298-300` **autoriza** explícitamente el modelo actual — Opción B contradice la spec, así que requiere un delta spec (`specs/.../spec.md` que reemplace "Una Persona DEBE poder mantener múltiples ocupaciones activas simultáneamente…" por el invariante nuevo). |
| Forward-only compatible con `NotSupportedException` en `Down()` (mismo patrón que fix #59). | Si la intención de negocio nunca fue limitar a una por persona, Opción B introduce una restricción nueva que rompe flujos reales (p.ej. persona con cargo titular + cargo interino en otra unidad). |
| Tests RED primero garantizan contrato. | Más trabajo de mantenimiento: nueva verificación de unicidad en cada operación de escritura. |

**Esfuerzo**: Medio-Alto (8 archivos tocados + migración + regeneración + coordinación con spec).

**Riesgo dominante**: el comportamiento vigente **es legal** y usado por el spec. Adoptar B requiere confirmar con el usuario que efectivamente quiere romper ese contrato, lo que también obliga a delta de spec `sgv-database`.

## Recomendación

**Opción A — corregir la documentación.**

Razones técnicas:

1. **El spec canónico (`openspec/specs/sgv-database/spec.md:298-300`) ya autoriza el modelo actual**: "Una Persona DEBE poder mantener múltiples ocupaciones activas simultáneamente cuando correspondan a Puestos distintos". Opción B contradice este requisito y exige un delta de spec — trabajo extra fuera del scope del issue.
2. **El código, las pruebas y el snapshot coinciden entre sí**: `Modelo_Ocupacion_ReemplazaUnicidadPersonaPorPersonaPuesto` (test), `OcupacionConfiguracion` (config), `IOcupacionRepository` (contrato), `OcupacionRepository` (impl), `OcupacionServicioComandos` (servicio) son internamente consistentes. La única inconsistencia es el **texto** de la doc.
3. **Blast radius mínimo**: un único archivo markdown. Cero riesgo operacional, cero tests nuevos, cero migración.
4. **El historial de migraciones confirma el cambio de intención**: la columna `ActivePersonaIdUnique` original se dropeó en `20260624153353` (24 de junio) y se sustituyó por la compuesta. Es razonable asumir que el cambio fue deliberado y la doc quedó desactualizada por descuido, no por diseño.
5. **Aceptación del issue #127.1 ("Documentación y modelo coinciden") se cumple trivialmente**.
6. **Aceptación #127.2 ("Tests cubren el invariante documentado")**: el invariante documentado post-corrección ("una sola activa por Puesto" + "una sola activa por Persona+Puesto") ya está cubierto por `OcupacionRepositoryTests.ExistsActiveByPuestoAsync_*` + `ExistsActiveByPersonaYPuestoAsync_*` y por los tests de `OcupacionServicioComandosTests` correspondientes.
7. **Si en el futuro el negocio requiere unicidad per-persona simple**, el propio párrafo reescrito deja una puerta abierta: "Si el negocio requiere… se deberá agregar la columna `ActivePersonaIdUnique`…". Eso convierte la nota en una **extensión futura explícita**, no en una promesa rota.

Si el usuario tiene confirmación de negocio de que efectivamente se quiere limitar a una ocupación activa por persona, **recomiendo reabrir la conversación** antes de elegir Opción B y, en ese caso, escribir el delta de spec primero.

## Próximos pasos SDD

Si el orchestrator confirma Opción A (recomendada):

1. **sdd-propose**: `proposal.md` mínimo (1 sección "Scope", 1 sección "Approach", sección "Affected Areas" con `docs/decisiones-implementacion.md`).
2. **sdd-spec**: opcional — un delta `specs/decisiones-implementacion-mantenimiento/spec.md` que registre el invariante vigente y la nota de extensibilidad futura. No obligatorio porque `sgv-database/spec.md:298-325` ya lo cubre a nivel funcional.
3. **sdd-design**: innecesario (cambio de prosa, sin arquitectura).
4. **sdd-tasks**: innecesario (1 archivo, ~10 líneas).
5. **sdd-apply**: editar `docs/decisiones-implementacion.md:19-21`.
6. **sdd-verify**: confirmar que el suite sigue verde (no hay regresión funcional porque no se toca código) y que el texto nuevo coincide con el modelo.
7. **sdd-archive**: cerrar el change con `archive-report.md` referenciando el issue #127.

Si el usuario fuerza Opción B, el flujo es:

1. **sdd-propose**: propuesta completa con sección "Out of Scope" que mencione que `sgv-database/spec.md:298` también cambia.
2. **sdd-spec**: delta a `openspec/specs/sgv-database/spec.md` línea 300 — reemplazar el texto actual por el invariante "una sola activa por Persona" (más el resto de la regla por Puesto).
3. **sdd-design**: nota técnica sobre el orden de las llamadas de verificación en `OcupacionServicioComandos` (Persona → PersonaYPuesto → Puesto) y la necesidad de `ExistsActiveByPersonaAsync` con `excludingId`.
4. **sdd-tasks**: 4-5 tareas (config EF + migración, repositorio + contrato, servicio + tests, integración `[MySqlFact]` + regenerar script).
5. **sdd-apply**: implementar con strict TDD (tests RED primero).
6. **sdd-verify**: confirmar 0 fallos en `OcupacionRepositoryTests` + `OcupacionServicioComandosTests` contra MySQL real en CI.
7. **sdd-archive**: cerrar el change.

## Preguntas abiertas para el usuario

1. **¿La intención original de negocio era "una sola ocupación activa por persona" o "una sola por Persona+Puesto"?** El spec canónico dice lo segundo; la doc dice lo primero. Si el spec refleja la decisión vigente, Opción A es la respuesta correcta. Si no, Opción B con su delta de spec.
2. **¿Hay producción real con personas que tengan dos o más ocupaciones activas en puestos distintos?** Si sí, Opción B requiere una migración correctiva explícita antes del `AddColumn`+`CreateIndex` (finalizar o eliminar las duplicadas). Confirmar antes de elegir Opción B.
3. **¿La doc de `decisiones-implementacion.md:21` ("Si el negocio requiere cargos concurrentes…") se quiere mantener como nota de extensibilidad o se quiere eliminar?** Mi recomendación es dejarla como nota explícita de "qué se necesitaría agregar si más adelante se permite" — coherente con el modelo vigente.
4. **¿La doc forma parte de la suite que valida CI o es libre (sin check automático)?** No detecté ningún test que verifique texto de `docs/decisiones-implementacion.md`. Si se quiere protección automática contra reaparición de la inconsistencia, habría que considerar un test que parsee el markdown y verifique que las dos invariantes declaradas coinciden con los shadow properties activos del modelo (`ActivePuestoIdUnique`, `ActivePersonaPuestoUnique`). Out of scope del issue pero vale la pena anotarlo como follow-up.
