# Tasks: Catálogo TipoDocumento y FK en Persona (issue #147)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1,150 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Catálogo Core) → PR 2 (API + Validación) → PR 3 (Web UI) |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: **Yes**
Chained PRs recommended: **Yes**
Chain strategy: **pending**
400-line budget risk: **High**

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | TipoDocumento domain + entity + constants + repository + consulta + migration + DatosSemilla + Persona FK | PR 1 | `dotnet test --filter "FullyQualifiedName~TipoDocumento"` | `dotnet run --project src/SGV.Api` + GET autenticado a `/api/v1/tipos-documento` | Revertir PR 1 deja TiposDocumento y columna legacy Personas.TipoDocumento intacta |
| 2 | API controller + validators + PersonaServicio + controller/validator tests | PR 2 | `dotnet test --filter "FullyQualifiedName~TipoDocumentoValidator\|TipoDocumentosController"` | `dotnet run --project src/SGV.Api` + crear persona con TipoDocumentoId via POST | Revertir PR 2 no afecta catálogo ni datos; solo API y validación |
| 3 | Web client + InputModel + Create/Edit + _Form.cshtml + FakePersonaApiClient + web tests | PR 3 | `dotnet test --filter "FullyQualifiedName~PersonaSelectTipoDocumento\|FakePersonaApiClient"` | `dotnet run --project src/SGV.Web` + abrir `/personas/crear` y verificar `<select>` | Revertir PR 3 no afecta API ni datos; solo shell web |

---

## Phase 1: Foundation — Catálogo Core + Persona FK + Migración

- [ ] **T1** | Dominio | Crear `TipoDocumento` record en `SGV.Dominio/Personas/TipoDocumento.cs`. Pre: ninguna. Test rojo: `Constructor_CodigoVacio_LanzaArgumentException`. Done: record sellado con validación de `Codigo`, `Nombre`, `PatronValidacion`, `LongitudMinima`, `LongitudMaxima`. Archivos: CREAR `src/SGV.Dominio/Personas/TipoDocumento.cs`
- [ ] **T2** | Tests | Tests unitarios `TipoDocumento` (REQ-TD-002, REQ-TD-006). Pre: T1. Test: `PatronRegex_MatchExitoso_DNI`, `PatronRegex_Fail_PasaporteFormatoInvalido`. Done: cubren constructor validation + regex match/fail. Archivos: CREAR `tests/SGV.Tests/Dominio/Personas/TipoDocumentoTests.cs`
- [ ] **T3** | Contracts | Crear `TipoDocumentoDto` record. Pre: T1. Test rojo: `Dto_SerializaCampos_Correctamente` falla sin record. Done: `TipoDocumentoDto(Guid Id, string Codigo, string Nombre, string? PatronValidacion, int? LongitudMinima, int? LongitudMaxima)`. Archivos: CREAR `src/SGV.Contracts/Personas/Consultas/Dtos/TipoDocumentoDto.cs`
- [ ] **T4** | Persistencia | Crear entity + config + constants + repository de `TipoDocumento`. Pre: T1. Test rojo: `EntityConfig_TienePK_Id` falla sin PK. Done: EF mapea `TiposDocumento` con PK, Codigo UNIQUE, `ascii_general_ci`, sin IsActive/IsDeleted. Archivos: CREAR `TipoDocumentoEntity.cs`, `TipoDocumentoConfiguracion.cs`, `TipoDocumentoConstantes.cs`, `TipoDocumentoRepository.cs` en Infraestructura; MODIFICAR `SgvDbContext.cs` (DbSet)
- [ ] **T5** | Tests | Tests de constantes + repository. Pre: T4. Test: `Constantes_Tiene4Valores_Unicos`, `Semilla_Guids_Rango71000000`. Done: REQ-TD-004, REQ-TD-005. Archivos: CREAR `tests/SGV.Tests/Persistencia/TipoDocumentoConstantesTests.cs`, `tests/SGV.Tests/Persistencia/TipoDocumentoRepositoryTests.cs`
- [ ] **T6** | Persistencia | Crear `ITipoDocumentoCatalogoConsulta` + implementación + DI. Pre: T4. Test rojo: `Servicio_ListarAsync_Devuelve4` falla sin impl. Done: interfaz en Aplicacion, impl registrada en DI, consulta catálogo vía repository. Archivos: CREAR `src/SGV.Aplicacion/Personas/Consultas/ITipoDocumentoCatalogoConsulta.cs`, implementación; MODIFICAR `DependencyInjection.cs`
- [ ] **T7** | Persistencia | Agregar DatosSemilla HasData para TipoDocumento. Pre: T4. Test rojo: `DatosSemilla_TipoDocumento_SeedIdsMatchConstantes` falla sin HasData. Done: 4 filas vía `TipoDocumentoConstantes.Semilla`. Archivos: MODIFICAR `DatosSemilla.cs`
- [ ] **T8** | Dominio | Agregar `TipoDocumentoId: Guid?` a `Persona`. Pre: T1. Test rojo: `Persona_Reconstruye_ConTipoDocumentoId` falla sin parámetro. Done: propiedad nullable, `Reconstitute` recibe `tipoDocumentoId`. Archivos: MODIFICAR `src/SGV.Dominio/Personas/Persona.cs`
- [ ] **T9** | Persistencia | Modificar `PersonaEntity` + `PersonaConfiguracion` + mapper para FK. Pre: T4, T8. Test rojo: `PersonaConfig_TipoDocumentoIdFK_OnDeleteRestrict` falla sin config. Done: string→Guid?, FK nullable, navigation, `OnDelete(Restrict)`, columna generada `CONCAT(TipoDocumentoId,':',NumeroDocumento)`. Archivos: MODIFICAR `PersonaEntity.cs`, `PersonaConfiguracion.cs`, `PersistenceToDomainMapper.cs`
- [ ] **T10** | Migración | Migración EF: crear `TiposDocumento`, alterar `Personas`, backfill, DROP legacy. Pre: T4, T9. Test rojo: `Migracion_BackfillLimpio_MapeaCodigosA_Guids` falla sin migration. Done: DDL design §76-86: CreateTable → InsertData → pre-flight logging → AddColumn → backfill → drop/recreate índice → FK → DropColumn legacy → Down() = `NotSupportedException`. Archivos: CREAR migración via `dotnet ef migrations add`, MODIFICAR `SgvDbContextModelSnapshot.cs` (regenerado)
- [ ] **T11** | Tests | Tests [MySqlFact] de migración + FK + unicidad + auditoría + backfill sucio. Pre: T10. Test: `FK_OnDeleteRestrict_RechazaEliminarCatalogado`, `IndiceUnico_RechazaDuplicadoActivo`, `Migracion_BackfillConSucio_TipoDocumentoIdNull`, `Auditoria_CambioTipoDocumentoIdRegistrado`, `ActiveDocumentoUnique_ComputedSql_Concat`. Done: cubren sgv-database y sgv-persistence-architecture spec. Archivos: CREAR `tests/SGV.Tests/Persistencia/TipoDocumentoMigracionBackfillTests.cs`; MODIFICAR `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs`

## Phase 2: API + Validación

- [ ] **T12** | Api | Crear `TipoDocumentosController` read-only. Pre: T6, T3. Test rojo: `GetAll_SinAuth_401` falla sin `[Authorize]`. Done: GET list + GET byId, auth required, sin rutas write (405 implícito). Archivos: CREAR `src/SGV.Api/Controllers/TipoDocumentosController.cs`
- [ ] **T13** | Tests | Tests de integración API del controlador. Pre: T12. Test: `GetAll_ConAuth_Devuelve4Tipos`, `GetById_NoExiste_404`. Done: REQ-TD-007, sgv-readonly-api spec. Archivos: CREAR `tests/SGV.Tests/Api/TipoDocumentosControllerTests.cs`
- [ ] **T14** | Aplicación | Actualizar `CrearPersonaRequestValidator` + `ActualizarPersonaRequestValidator` con validación tipo documento. Pre: T6. Test rojo: `Crear_FkInexistente_FK_INEXISTENTE` falla sin check FK. Done: FK_INEXISTENTE, PATRON_NO_CUMPLIDO, LONGITUD_FUERA_DE_RANGO con Regex timeout 50ms. Archivos: MODIFICAR `CrearPersonaRequestValidator.cs`, `ActualizarPersonaRequestValidator.cs`
- [ ] **T15** | Tests | Tests unitarios de validators con mock `ITipoDocumentoCatalogoConsulta`. Pre: T14. Test: `PatronNoCumplido_PATRON_NO_CUMPLIDO`, `LongitudFueraDeRango_LONGITUD_FUERA_DE_RANGO`, `AceptarValido_NoError`. Done: cubren persona-management spec. Archivos: CREAR `tests/SGV.Tests/Aplicacion/Personas/TipoDocumentoValidatorTests.cs`
- [ ] **T16** | Aplicación | Actualizar `PersonaServicioComandos` (armar Persona con TipoDocumentoId) + `PersonaServicioConsulta` (JOIN TipoDocumento). Pre: T8, T9. Test rojo: `PersonaDto_TipoDocumento_Denormalizado` falla sin JOIN. Done: `PersonaDto.TipoDocumento` expone `TipoDocumentoDto?` denormalizado. Archivos: MODIFICAR `PersonaServicioComandos.cs`, `PersonaServicioConsulta.cs`, `PersonaDto.cs`, `PersonaRequests.cs`
- [ ] **T17** | Tests | Tests de integración API persona con TipoDocumento. Pre: T16. Test: `CrearPersona_ConTipoDocumentoValido_201`, `CrearPersona_TipoDocumentoInexistente_400`. Done: flujo completo persona con FK validation. Archivos: MODIFICAR tests existentes de persona API

## Phase 3: Web UI

- [ ] **T18** | Web | Agregar `GetTiposDocumentoAsync` a `IPersonaApiClient` + `PersonaApiClient` + `FakePersonaApiClient`. Pre: T3, T12. Test rojo: `Fake_GetTiposDocumentoCalls_Count1` falla sin tracking. Done: fake registra invocaciones, test sin HTTP real. Archivos: MODIFICAR `IPersonaApiClient.cs`, `PersonaApiClient.cs`, `FakePersonaApiClient.cs`
- [ ] **T19** | Web | Agregar `TipoDocumentoId` y `TiposDocumento` a `PersonaInputModel` + `IPersonaForm`. Pre: T18. Test rojo: test de binding `TipoDocumentoId_GuidNullable`. Done: modelo con lista de opciones y valor seleccionado. Archivos: MODIFICAR `PersonaInputModel.cs`, `IPersonaForm.cs`
- [ ] **T20** | Web | `Create.cshtml.cs` carga catálogo en GET. Pre: T19. Test rojo: `Create_Get_CargaTiposDocumento` falla sin invocación. Done: GET invoca `GetTiposDocumentoAsync` 1 vez. Archivos: MODIFICAR `Create.cshtml.cs`
- [ ] **T21** | Web | `_Form.cshtml` renderiza `<select name="TipoDocumentoId">` con 4 opciones. Pre: T19. Test rojo: test de renderizado `<select>` con 4 `<option>`. Done: select con placeholder + DNI/LE/LC/Pasaporte. Archivos: MODIFICAR `_Form.cshtml`
- [ ] **T22** | Web | `Edit.cshtml.cs` carga catálogo y pre-selecciona tipo actual. Pre: T19, T20. Test rojo: `Edit_Get_PreSeleccionaTipoActual`. Done: GET invoca 1 vez, `<option selected>` correcto. Archivos: MODIFICAR `Edit.cshtml.cs`
- [ ] **T23** | Tests | Tests web smoke: Create/Edit render, POST inválido preserva formulario. Pre: T20-T22. Test: `Create_Get_RenderizaSelectCon4Options`, `Post_PatronInvalido_RenderizaMensajeEspanol`. Done: cubren escenarios de persona-management spec web. Archivos: CREAR `tests/SGV.Tests/Web/Persona/PersonaSelectTipoDocumentoTests.cs`

## Phase 4: Documentación

- [ ] **T24** | Docs | Actualizar docs + regenerar migracion-inicial-sgv.sql. Pre: T10. Test: N/A (verificación manual). Done: `docs/decisiones-implementacion.md` (bloque GUID), `AGENTS.md` (mapa rangos), `docs/migracion-inicial-sgv.sql` regenerado. Archivos: MODIFICAR `docs/decisiones-implementacion.md`, `AGENTS.md`; regenerar `docs/migracion-inicial-sgv.sql`

---

## Review Workload Forecast

```json
{
  "estimated_changed_lines": 1150,
  "review_budget_lines": 400,
  "review_budget_risk": "high",
  "estimated_hours": 48,
  "tasks_count": 24,
  "chained_prs_recommended": true,
  "decision_needed_before_apply": true,
  "rationale": "El cambio abarca ~50 archivos entre crear (18 nuevos: dominio, entity, config, constants, repository, consulta, DTO, controller, migration, 7 suites de test) y modificar (~25: Persona, entity, config, mapper, DatosSemilla, DI, validators, servicios, DTOs, clientes web, pages, docs). La migración DDL incluye drop/recreate de índice único con columna generada, backfill con política opt-in relajada, y FK OnDelete(Restrict). Estimación basada en el precedente NivelCargo (~500 líneas) pero este cambio es ~2.3x mayor porque toca 3 capas adicionales (Web UI, persona management, validación) y la migración es más compleja (columna generada + backfill dirty).",
  "split_strategy_suggestion": "3 PRs encadenados: PR1 (Foundation: ~450 líneas) = dominio + entity + constants + repository + consulta + DTO + DatosSemilla + Persona FK + migration + tests [MySqlFact]. PR2 (API + Validation: ~350 líneas) = controller API + validators + PersonaServicioComandos/Consulta updates + controller/validator tests. PR3 (Web UI: ~350 líneas) = client tipado + InputModel + Create/Edit + _Form.cshtml + web tests. La migración DDL y tests [MySqlFact] deben ir en PR1 porque validan el schema contra MySQL real; separarlos requeriría un snapshot de DB falso que no existe. La estrategia de chain queda pendiente de decisión del usuario (stacked-to-main vs feature-branch-chain)."
}
```
