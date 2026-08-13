# Tasks: fix-mysql84-compat

## Review Workload Forecast

Decision needed before apply: No
Chained PRs recommended: pending
Chain strategy: pending
400-line budget risk: Medium

Estimación design: ~270–420 líneas. 5 archivos.

## Sección 1 — Validación empírica (bloqueante)

- [ ] **T-01 [OPEN] Validar rechazo de UNIQUE INDEX sobre VIRTUAL en MySQL 8.4**
  Acción: en `192.168.0.216` correr `dotnet ef database update` desde cero contra `sgv_validate_84`.
  Done: error de virtual column indexada en `IX_Personas_ActiveDocumentoUnique` (InicialSgvo L1145). Bloquea T-05.

- [ ] **T-02 [OPEN] Validar `FixActivePuestoIdUniqueType` (jul-11) en MySQL 8.4**
  Acción: misma corrida; ver si su `DropIndex` falla antes de InicialSgvo.
  Done: log con error o PASS. Bloquea T-05.

- [ ] **T-03 [OPEN] Validar `SgvDbContextModelSnapshot.cs` sin diff**
  Acción: `git diff -- SgvDbContextModelSnapshot.cs` tras `dotnet ef migrations add`.
  Done: 0 líneas. Bloquea T-05.

- [ ] **T-04 [OPEN] Validar `mb.Sql()` con `;` internos rompe `--idempotent`**
  Acción: aplicar script regenerado con prototipo `BEGIN...END` interno.
  Done: error en `MigrationsScript` confirma patrón D.3. Bloquea T-05.

## Sección 2 — Implementación

- [ ] **T-05 Crear migración compensatoria**
  Path: `src/SGV.Infraestructura/Persistencia/Migraciones/20260728120000_FixMySql84GeneratedUniqueIndex.cs` (~140–180 líneas, patrón D.3).
  Acción: helper `ConvertUniqueVirtualToStored` con `SET @var + PREPARE/EXECUTE/DEALLOCATE` en `mb.Sql()` separados; 10 invocaciones (design §Mapeo); `Down()` lanza `NotSupportedException`.
  Done: build 0 errores; `migrations list`=18. Bloquea T-06–T-09.

- [ ] **T-06 Verificar snapshot inalterado**
  Acción: `git diff --stat SgvDbContextModelSnapshot.cs` post-T-05.
  Done: 0 líneas. Bloquea T-09.

- [ ] **T-07 Regenerar `docs/migracion-inicial-sgv.sql`**
  Comando: `dotnet ef migrations script --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj --idempotent --output docs/migracion-inicial-sgv.sql`.
  Done: 18 bloques; `ActiveEmailUnique` STORED. Bloquea T-08, T-10.

- [ ] **T-08 Actualizar `ScriptStandaloneSmokeMySqlFactTests.cs`**
  Acción: L44 `ExpectedMigrationCount = 17` → `18` + comentario.
  Done: build 0 errores. Bloquea T-10.

- [ ] **T-09 Actualizar `docs/decisiones-implementacion.md`**
  Acción: en §"Dualidad de paths" agregar sub-sección "Compatibilidad con MySQL 8.4 LTS" (motivo, tabla VIRTUAL/STORED×8.0/8.4, ref D.3).
  Done: grep muestra sección. Bloquea T-13.

## Sección 3 — Verificación end-to-end

- [ ] **T-10 Build + suite sin MySQL**
  Comando: `dotnet build SGV.slnx --nologo --verbosity minimal`; `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName!~MySqlFact"`.
  Done: 0 errores; no-MySql verdes. Bloquea T-11.

- [ ] **T-11 Drop+CREATE+apply contra MySQL 8.4 fresh**
  Comando: `mysql -e "DROP DATABASE IF EXISTS sgv_test; CREATE DATABASE sgv_test"`; `dotnet ef database update --project src/SGV.Infraestructura --startup-project src/SGV.Infraestructura`.
  Done: 18 filas en `__EFMigrationsHistory`; 10 índices `Non_Unique=0`; `ActiveEmailUnique` EXTRA='STORED GENERATED'. Bloquea T-12.

- [ ] **T-12 Suite `[MySqlFact]` contra server remoto**
  Comando: `ConnectionStrings__SgvDatabase='...' dotnet test tests/SGV.Tests/SGV.Tests.csproj --no-build --filter "FullyQualifiedName~MySqlFact"`.
  Done: `ScriptStandaloneSmokeMySqlFactTests` + migraciones verdes. Bloquea T-13.

- [ ] **T-13 Conteo changed lines y split**
  Comando: `git diff --stat <base> -- ':!docs/migracion-inicial-sgv.sql'`.
  Done: ≤400 → single PR; >400 → pausar y reportar (evaluar `size:exception` o split). Bloquea T-14.

## Sección 4 — Commit

- [ ] **T-14 Conventional commit (sin IA)**
  Comando: `git add -A; git commit -m "fix(migrations): add compensatoria for MySQL 8.4 GENERATED UNIQUE INDEX"`.
  Done: SHA en `apply-progress.md`; sin Co-Authored-By. Bloquea T-15.

## Sección 5 — Cierre (post-commit)

- [ ] **T-15 Verificación final**
  Comando: `git log --oneline -1 <base>..HEAD; git status --short`.
  Done: 1 commit ahead, tree clean.

- [ ] **T-16 Generar `apply-progress.md` y `verify-report.md`**
  Path: `openspec/changes/fix-mysql84-compat/{apply-progress,verify-report}.md`.
  Done: SHA + conteo + resultado T-11/T-12.

- [ ] **T-17 Persistir en Engram + sesión summary**
  Comando: `mem_save(topic_key="sdd/fix-mysql84-compat/tasks", type="architecture", capture_prompt=false)`; `mem_session_summary`.
  Done: observación persistida; summary guardado.