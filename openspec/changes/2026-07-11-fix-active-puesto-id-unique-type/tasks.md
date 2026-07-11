# Tasks: Fix `ActivePuestoIdUnique` tipo a `char(36)` (issue #59)

## Review Workload Forecast

| Campo | Valor |
|-------|-------|
| Total changed lines (est.) | ~85 |
| Files touched | 7 (3 mod, 2 new, 2 regenerated) |
| Test coverage impact | 12 fail→pass + 1 nuevo + 1 endurecido |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending (no aplica; forecast < 400) |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

## Phase 1: Modelo, migración, script y guardrail

- [x] **T-001**: Editar `OcupacionConfiguracion.cs:35-38` — `Property<int?>` → `Property<string?>` + `.HasMaxLength(36).HasColumnType("char(36)").UseCollation("ascii_general_ci")`. Sin test-first (config EF).
  - **LoC**: ~4 mod. **Done**: `dotnet build SGV.slnx` sin warnings nuevos.

- [x] **T-002**: `dotnet ef migrations add FixActivePuestoIdUniqueType --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj`.
  - **LoC**: ~30 added (auto). **Done**: nueva migración + snapshot regenerado en `SgvDbContextModelSnapshot.cs:984-987` con `string?`/`char(36)`.

- [x] **T-003**: Insertar al inicio de `Up()` la purga defensiva: `migrationBuilder.Sql("UPDATE \`Ocupaciones\` SET \`ActivePuestoIdUnique\` = NULL WHERE \`FechaFin\` IS NULL AND \`IsDeleted\` = 0")`. Resto (`DropIndex` → `AlterColumn` → `CreateIndex`) intacto.
  - **LoC**: ~3 added. **Done**: `Up` arranca con la purga antes del `DropIndex`.

- [x] **T-004**: Reemplazar `Down()` por `throw new NotSupportedException("Migración forward-only. Para revertir, escribir una migración correctiva explícita.")` como **primera línea**, antes de cualquier `migrationBuilder.*`.
  - **LoC**: ~5 added. **Done**: `Down` lanza la excepción antes de cualquier otra instrucción.

- [x] **T-005**: Regenerar script: `dotnet ef migrations script --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj --idempotent --output docs/migracion-inicial-sgv.sql`.
  - **LoC**: ~10 mod. **Done**: `grep -n 'ActivePuestoIdUnique' docs/migracion-inicial-sgv.sql` muestra `varchar(36)` con `ascii_general_ci`.

- [x] **T-006**: Endurecer `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs:44-61` — agregar `Assert.Equal(typeof(string), generatedProperty.ClrType)` y `Assert.Contains("char(36)", generatedProperty.GetColumnType(), StringComparison.OrdinalIgnoreCase)`. **Test-first**: falla hoy; pasa tras T-001+T-002.
  - **LoC**: ~3 added. **Done**: filter `Modelo_ConfiguraColumnaGeneradaUnicaParaOcupacionVigentePorPuesto` falla pre-fix, pasa post-fix.

## Phase 2: Canario y cierre documental

- [x] **T-007**: Crear `tests/SGV.Tests/Persistencia/OcupacionGeneratedColumnRegressionTests.cs` con `[MySqlFact] AddAsync_FilaActiva_ActivePuestoIdUniquePersisteComoGuidString`. Inserta `OcupacionEntity` activa (`FechaFin=null`, `IsDeleted=false`, `PuestoId = Guid.NewGuid()`), `SaveChangesAsync()`, lee vía `Database.SqlQueryRaw<string>("SELECT ActivePuestoIdUnique FROM Ocupaciones WHERE Id = {0}", entity.Id)`, asserta `result.Single() == puestoId.ToString()`. **Test-first**: falla hoy; pasa tras T-001+T-002.
  - **LoC**: ~35 added. **Done**: archivo compila; test skipea local; verde en CI.

- [x] **T-008**: Reemplazar `AGENTS.md:181-186` (bloque "Bug conocido (issue #59)… Pendiente de SDD change") por: `Cerrado por change archivado 2026-07-11-fix-active-puesto-id-unique-type (migración FixActivePuestoIdUniqueType).`
  - **LoC**: ~-5 mod. **Done**: `grep -i 'Pendiente de SDD change' AGENTS.md` vacío.

## Phase 3: Verificación y entrega

- [x] **T-009**: `dotnet build SGV.slnx` + `dotnet test SGV.slnx --filter "FullyQualifiedName~ModeloPersistenciaTests" --no-build`. `[MySqlFact]` skipea limpio.
  - **Done**: build 0 errores; filter 100% verde.

- [x] **T-010**: Commit + push → CI contra MySQL 8. Espera 12 fail→pass en `OcupacionRepositoryTests` + canario (T-007) + estructural (T-006) verdes.
  - **Done**: PR con CI verde; 0 rojos en `OcupacionRepositoryTests`.

## Commit split recomendado (mismo PR, dos commits)

- **C1 — T-001 → T-006**: Fix + migración + script + endurecimiento. **Verde local sin MySQL** (T-006 valida sin conexión).
- **C2 — T-007 → T-008**: Canario (solo MySQL real) + cierre documental. **Validación final en CI**.

## Notas de implementación

- **EF tool no se pudo ejecutar**: `dotnet ef migrations add` con `HasColumnType("char(36)")` + `HasComputedColumnSql` + `string` CLR type dispara una NRE conocida de EF Core 9 / Pomelo 9 (`ElementMappingConvention.ProcessModelFinalizing`). La migración y el Designer file se escribieron a mano siguiendo el patrón del precedente `20260624153353_ConvertirTipoAsignacionAEnumYActualizarUnicidad`.
- **Tipo final = `varchar(36)` (no `char(36)` como decía el spec)**: MySqlConnector 2.4.0 auto-detecta columnas `char(36)` como `Guid` independientemente del CLR type declarado en EF. Como `ActivePuestoIdUnique` debe ser `string` (no `Guid`), `varchar(36)` evita el conflicto `InvalidCastException` en `PropagateResults`. Funcionalmente equivalente en espacio (36 chars ASCII); pierde 1 byte de length-prefix por fila.
- **T-006 endurecido con asserts flexibles**: `Assert.Equal(typeof(string), ...)` + `Assert.Equal(36, GetMaxLength())` + assert de `(36)` en `GetColumnType()`. Acepta tanto `varchar(36)` como `char(36)`.
