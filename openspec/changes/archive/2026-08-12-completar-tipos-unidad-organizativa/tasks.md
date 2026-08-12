# Tasks: completar-tipos-unidad-organizativa

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~2.600 (Designer auto ~2.500, migración ~50, snapshot ~0–200, comment ~10, script +13) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | Single PR con `size:exception` (Designer indivisible de su `.cs`) |
| Delivery strategy | single-pr |
| Chain strategy | size-exception |
| Work-unit evidence | `dotnet build SGV.slnx` + `dotnet test --filter "~MigracionFailLoud"` + `dotnet ef migrations list` + `script --idempotent`; rollback = `git revert` del commit (huérfana intacta). |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: size-exception
400-line budget risk: High

## Fase 1: Infraestructura EF

- [ ] 1.1 `dotnet ef migrations add CompletarTiposUnidadOrganizativaSeed --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj --output-dir Persistencia/Migraciones`. Esperado: `.cs` (vacío) + `.Designer.cs` (20 HasData) + snapshot regenerado.
- [ ] 1.2 `dotnet build SGV.slnx` + `dotnet ef migrations list` ⇒ N+1 migraciones incluyendo `<ts>_CompletarTiposUnidadOrganizativaSeed`.

## Fase 2: Core — Hand-author Up/Down

- [ ] 2.1 Poblar `Up()` con un único `migrationBuilder.InsertData(table: "TiposUnidadOrganizativa", columns: new[] { "Id", "Codigo", "Nombre" }, values: new object[,])` de 13 filas verbatim desde `TipoUnidadOrganizativaConstantes` (Sede→Escuela, tildes en Región/Coordinación/Sección/Célula).
- [ ] 2.2 Sobrescribir `Down()` con `throw new NotSupportedException("…forward-only…append-only.")` (REQ-TUO-007). Eliminar cualquier `DeleteData` auto.

## Fase 3: Comment test engañoso

- [ ] 3.1 Editar doc-comment de clase en `tests/SGV.Tests/Persistencia/MigracionFailLoudTests.cs`: aclarar `EnsureCreated()` (20 vía snapshot `HasData`) vs `Migrate()` (7 hasta aplicar la nueva migración). Aserciones inalteradas.
- [ ] 3.2 Confirmar con `git diff tests/SGV.Tests/Persistencia/MigracionFailLoudTests.cs` que solo cambian líneas de comment/XML doc, no `Assert.Equal(20, …)`.

## Fase 4: Validación EF migrations

- [ ] 4.1 `dotnet ef migrations list` ⇒ incluye `<ts>_CompletarTiposUnidadOrganizativaSeed` tras `20260805000000_AddEstadoVacanteFlags`.
- [ ] 4.2 `dotnet ef migrations script --idempotent … --output /tmp/check.sql`. `grep -c "INSERT INTO \`TiposUnidadOrganizativa\`" /tmp/check.sql` ≥ 13.

## Fase 5: Regeneración script SQL idempotente

- [ ] 5.1 `dotnet ef migrations script --idempotent … --output docs/migracion-inicial-sgv.sql`. Override del Out-of-Scope del proposal (instrucción explícita del orchestrator).
- [ ] 5.2 Validar `grep -c "INSERT INTO \`TiposUnidadOrganizativa\`" docs/migracion-inicial-sgv.sql` ≥ 13 y que termine con `INSERT INTO \`__EFMigrationsHistory\``.

## Fase 6: Build + Tests (GREEN, strict_tdd)

- [ ] 6.1 `dotnet build SGV.slnx` ⇒ 0 errores, 0 warnings nuevos.
- [ ] 6.2 `dotnet test SGV.slnx --filter "FullyQualifiedName~MigracionFailLoud"` ⇒ pasa con MySQL 8 real (`[MySqlFact]`).
- [ ] 6.3 `dotnet test SGV.slnx` suite completa, sin regresiones. Único GREEN observable: `MigracionFailLoudTests` (no se crean tests nuevos — Out of Scope).

## Fase 7: PR Forecast y entrega

- [ ] 7.1 `git diff --stat main...HEAD` ≈ +2600/-0. Solicitar maintainer `size:exception` (budget High + delivery single-pr).
- [ ] 7.2 PR único contra `main`: resumen, REQ-TUO-001/002/007, huérfana `20260730000000` intacta, evidencia `migrations list` + `script --idempotent` + `dotnet test`.

## Criterios de aceptación

- `dotnet build SGV.slnx` ⇒ 0 errores.
- `dotnet test --filter "~MigracionFailLoud"` ⇒ 2 tests GREEN con MySQL real.
- `dotnet ef migrations list` ⇒ incluye `<ts>_CompletarTiposUnidadOrganizativaSeed`.
- `dotnet ef migrations script --idempotent` ⇒ ≥ 13 `INSERT INTO TiposUnidadOrganizativa`.
- `docs/migracion-inicial-sgv.sql` ⇒ regenerado con los 13 INSERTs y termina en `__EFMigrationsHistory`.
- `20260730000000_SemillaTipoUnidadOrganizativaAmpliada.cs` ⇒ intacta.
- `MigracionFailLoudTests.cs` ⇒ aserciones inalteradas, sólo doc-comment cambia.
