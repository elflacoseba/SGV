# Verify Report — Implementar asignar/quitar Habilidades de un Cargo

## Resumen ejecutivo
Se verificó el slice **PR3a** contra `tasks.md`, `design.md`, `apply-progress.md` y las cuatro specs del change, más el diff real `origin/develop..HEAD`. El alcance implementado coincide con T3.1-T3.3: contrato del cliente tipado, implementación HTTP del subrecurso y tests/fake asociados.

No encontré **findings CRITICAL** de contrato o regresión en el slice. Sí quedaron **2 WARNING**: strict TDD sólo quedó parcialmente demostrable a nivel de commits, y el helper del cliente colapsa `401/403/5xx` en un `Validation/Unexpected`, lo que limita el feedback específico que PR3b podrá mostrar.

## PR3a — verify interim
- **Rama**: `feat/cargo-habilidad-pr3a-cliente-web`
- **Base**: `develop` (merge commit `7d511d55`)
- **Diff**: +777 / −0 en 7 archivos
- **Tests nuevos**: 14
- **Strict TDD**: parcial — T3.2/T3.3 sí tienen par RED→GREEN verificable; T3.1 y el touch autorizado `CargoSkillDeleteResult` quedaron como commits `feat:` previos al commit `test:`
- **Resultado subset**: 49/49 PASS
- **Resultado full**: 1333/1345 PASS

### Specs cubiertas por este PR
| Spec | Req cubiertos | Evidencia |
|---|---|---|
| `cargo-skill-asignar-editar` | Req 1 y Req 3 | `CargoApiClient.UpsertSkillAsync` usa `PUT /api/v1/cargos/{cargoId}/skills/{skillId}` y `ToSkillCommandResultAsync` mapea `404` a `CargoSkillErrorType.NotFound` |
| `cargo-skill-ponderacion-obligatoria` | Req 4 | `CargoApiClient.ToSkillCommandResultAsync` parsea `ValidationProblemDetails.Errors` y propaga `FieldErrors` |
| `cargo-skill-query-contract` | Req 1 y Req 2 | `GetSkillsAsync` deserializa `IReadOnlyList<CargoSkillDetailDto>` preservando `skillId`, `nivelRequeridoId`, `ponderacion`, `esObligatoria`, `skill`, `nivel` |
| `cargo-skill-ui-tabla-editable` | Req 5 y precondiciones de cliente | `ICargoApiClient` define `GetSkillsAsync/UpsertSkillAsync/DeleteSkillAsync`; `FakeCargoApiClient` agrega `GetSkillsResult/Calls`, `SkillUpsertResult/Calls`, `SkillDeleteResult/Calls` |

### Hallazgos
#### CRITICAL
Sin findings en CRITICAL.

#### WARNING
1. **Strict TDD no queda completamente alineado por commit history**  
   - **Ubicación**: `openspec/changes/implementar-asignar-quitar-habilidades-de-un-cargo/apply-progress.md:26-33` + historial `git log origin/develop..HEAD --reverse --oneline`  
   - **Evidencia**: `9b4aac48 feat(aplicacion)...` → `941b705e feat(web)...` → `e7b2c675 test(web)...` → `c3bc2743 feat(web)...`  
   - **Por qué importa**: la verificación strict TDD sólo puede probar con claridad el par RED→GREEN de T3.2/T3.3; no cada commit `feat:` del slice quedó precedido por un commit `test:`.

2. **El helper del subrecurso colapsa `401/403/5xx` en `Validation/Unexpected`**  
   - **Ubicación**: `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs:323-330`  
   - **Evidencia**: `return CargoSkillCommandResult.Failure(new CargoSkillError(CargoSkillErrorType.Validation, "Unexpected", "Respuesta inesperada del servidor."));`  
   - **Por qué importa**: aunque no rompe PR3a, PR3b no podrá distinguir acceso denegado o error de servidor del backend; sólo recibirá un fallo genérico con semántica de validación.

#### SUGGESTION
Sin findings en SUGGESTION.

### Verificaciones ejecutadas
- [x] `git log` confirma 5 commits del slice (`feat/test/feat/docs` esperados en la rama actual)
- [x] 0 `Co-Authored-By` y 0 mensajes `WIP`/`tmp`/`asd`/`fix-me` en commits del slice
- [x] `dotnet build SGV.slnx` limpio
- [x] Subset tests PASS: `dotnet test SGV.slnx --filter "FullyQualifiedName~CargoApiClient|FullyQualifiedName~FakeCargoApiClient"` → 49/49
- [x] Equivalencia HTTP↔controller verificada manualmente para `GET /skills`, `PUT /skills/{skillId}` y `DELETE /skills/{skillId}`
- [x] `CargoSkillDeleteResult.cs` consistente con `CargoDeleteResult` en shape (`Succeeded`, `StatusCode`, `Code`, `Message`)
- [x] No se introdujeron dependencias NuGet nuevas
- [x] No se modificaron controllers, repositorios ni contratos HTTP de capas inferiores fuera del alcance PR3a; único toque extra: `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillDeleteResult.cs` autorizado
- [x] Strict TDD verificado parcialmente: RED/GREEN confirmado para T3.2/T3.3; T3.1 figura como estructural/no testeable aisladamente

### Limitaciones de esta verificación
Interina. Cubre sólo **PR3a**. No evalúa `Habilidades.cshtml`, PageModel, suite web, anti-drift cruzado ni `bun run build`, porque eso pertenece a **PR3b** y quedaría fuera de scope de esta verify.

## Pendiente para cierre del change
- [ ] PR3b — Razor Page, PageModel, suite web con `SgvWebApplicationFactory` o `HabilidadWebTestFixture`, anti-drift cross-module `HabilidadesPage_NoContaminaHabilidadCatalogoConNivelRequerido`
- [ ] `bun run build` en `src/SGV.Web`
- [ ] Decisión UX sobre si `Habilidades.cshtml` se enlaza desde `Index` o `Edit`
- [ ] Re-correr verify completo del change una vez mergeado PR3b
- [ ] `sdd-archive` para sincronizar delta specs y cerrar el change
