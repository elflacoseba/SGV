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

## PR3b — verify interim

- **Rama**: `feat/cargo-habilidad-pr3b-razor-page`
- **Base**: `develop` (merge commit `914a93d3`)
- **Diff**: +1316/−4 en 7 archivos
- **Tests nuevos**: 10 (9 página + 1 anti-drift)
- **Strict TDD**: parcial — el par RED→GREEN principal sí está probado en `9b20975f` → `522ea4d3`, pero `T3.6` entró como test guardia GREEN-first y `apply-progress.md` no dejó una tabla dedicada de TDD evidence para este slice
- **Resultado subset**: 10/10 PASS
- **Resultado subset consolidado**: 225/225 PASS
- **Resultado full**: 1363/1375 PASS (12 pre-existentes `OcupacionRepositoryTests` sin cambios)

### Specs cubiertas por este PR
| Spec | Req cubiertos | Evidencia |
|---|---|---|
| `cargo-skill-ui-tabla-editable` | Req 1, 2, 3 y 5 cubiertos; Req 4 parcial | `Habilidades.cshtml` + `Habilidades.cshtml.cs` + 9 tests web + 1 anti-drift; falta confirmación previa al `Quitar` |
| `cargo-skill-asignar-editar` | Req 1, 2, 3, 4, 5 | `OnPostAsignar/Actualizar/Quitar` + equivalencia `ICargoApiClient` ↔ `CargosController` verificada en runtime |
| `cargo-skill-ponderacion-obligatoria` | Req 1, 2, 3, 4 | `CargoHabilidadInputModels`, mapping de `FieldErrors`, defaults/shape consumidos por la página y el cliente |
| `cargo-skill-query-contract` | Req 1, 2, 3 | `OnGetAsync` consume `GET /api/v1/cargos/{cargoId}/skills` con `CargoSkillDetailDto` enriquecido y preserva el shape del cargo padre |

### Hallazgos
#### CRITICAL
1. **La acción `Quitar` no pide confirmación antes del POST**  
   - **Ubicación**: `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml:136-140`  
   - **Evidencia**: el botón sólo define `type="submit"` + `formaction="?handler=Quitar..."`; no hay `confirm(...)`, `data-*` para harness JS ni diálogo/modal asociado.  
   - **Por qué importa**: incumple `cargo-skill-ui-tabla-editable` Req 4 (“La interfaz MUST confirmar la baja antes de quitar una asociación”), así que el slice NO está listo para merge tal como está.

#### WARNING
1. **Los errores de validación de `Actualizar` no quedan anclados a la fila editada**  
   - **Ubicación**: `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs:357-379` + `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml:96-141`  
   - **Evidencia**: `ApplySkillFailureToModelState(...)` prefija siempre `AsignarInput.`; la grilla editable no tiene `asp-validation-for` ni summary por fila para `NivelRequeridoId`/`Ponderacion`/`EsObligatoria`.  
   - **Por qué importa**: si el backend devuelve `FieldErrors` al editar una fila, el mensaje aparece —como mucho— en el formulario de asignación, no junto a la fila que falló; el feedback existe pero queda confuso.

2. **`apply-progress.md` reporta 224/224 PASS, pero el conteo real del subset consolidado hoy es 225/225**  
   - **Ubicación**: `openspec/changes/implementar-asignar-quitar-habilidades-de-un-cargo/apply-progress.md:31`  
   - **Evidencia**: `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~CargoHabilidadesPage|FullyQualifiedName~CargoHabilidadesAntiDrift|FullyQualifiedName~CargoApiClient|FullyQualifiedName~FakeCargoApiClient|FullyQualifiedName~CargoSkill|FullyQualifiedName~HabilidadAntiDrift|FullyQualifiedName~CargoEditPage|FullyQualifiedName~CargoCreatePage|FullyQualifiedName~CargoIndexPage|FullyQualifiedName~HabilidadEditPage|FullyQualifiedName~HabilidadCreatePage|FullyQualifiedName~Web.Cargo|FullyQualifiedName~ICargoApiClient"` → `Passed: 225, Total: 225`.  
   - **Por qué importa**: la evidencia documental del apply slice quedó desfasada y puede confundir la trazabilidad de verify contra el conteo real.

#### SUGGESTION
Sin findings en SUGGESTION.

### Verificaciones ejecutadas
- [x] 4 commits del slice (`test/feat/test/docs`)
- [x] 0 `Co-Authored-By`
- [x] `dotnet build SGV.slnx` limpio
- [x] Subset PR3b: 10/10 PASS
- [x] Subset consolidado: 225/225 PASS
- [x] Full: 1363/1375 PASS (12 `OcupacionRepositoryTests` pre-existentes)
- [x] `bun run build` verde
- [x] Equivalencia PageModel↔Client↔Controller para los 4 handlers
- [x] `[Authorize]` + chequeo explícito `RolesSgv.Administrador` en cada handler
- [x] PRG + `TempData["StatusMessage"|"StatusKind"]`
- [x] ModelState mapping desde `FieldErrors`
- [x] Sin `Html.Raw` con datos del usuario
- [x] Strict TDD ordering principal: RED precede GREEN en `9b20975f` → `522ea4d3`
- [x] Anti-drift cross-module cubre las 4 aserciones (memoria #569)

### Limitaciones
Interina. Cubre sólo **PR3b**. El change tiene 3 PRs verificados (PR1/PR2 ya cerradas, PR3a y PR3b interim). El **verify final del change completo** debe correr después del merge de PR3b, antes de `sdd-archive`.

## Pendiente para cierre del change (actualizado)
- [x] PR1 — Aplicación ✅ mergeado (#82)
- [x] PR2 — Infraestructura + API ✅ mergeado (#83)
- [x] PR3a — Cliente web tipado ✅ mergeado (#84) + interim verify + cierre W1/W2
- [ ] PR3b — Razor Page + suite web + anti-drift (este PR, pendiente merge)
- [ ] Corregir la confirmación obligatoria de `Quitar` antes del merge de PR3b
- [ ] Re-correr **verify final del change completo** una vez mergeado PR3b
- [ ] `sdd-archive` para sincronizar delta specs y cerrar el change formalmente
