# Archive Report: Implementar FluentValidation para validaciones de UnidadesOrganizativas

## Metadata

| Campo | Valor |
|-------|-------|
| **Slug** | `implementar-fluentvalidation-validaciones-unidades-organizativas` |
| **Archived at** | `2026-06-17` |
| **Archive path** | `openspec/changes/archive/2026-06-17-implementar-fluentvalidation-validaciones-unidades-organizativas/` |
| **Delivery strategy** | `ask-always` |
| **Chain strategy** | `stacked-to-main` (PR 1 + PR 2 + Phase 6 remediation) |
| **Task count** | 20/20 completas |
| **Test count** | 202/202 passing (0 failed, 0 skipped) |
| **Veredicto final** | PASS |

## Resumen ejecutivo

Se implementó FluentValidation 12.1.1 en `SGV.Aplicacion` para centralizar las validaciones de entrada de `CrearUnidadOrganizativaRequest` y `ActualizarUnidadOrganizativaRequest`. Las validaciones se ejecutan con short-circuit manual via `IValidator<TRequest>.ValidateAsync` al inicio de `CrearAsync` y `ActualizarAsync`, antes de consultar repositorios (duplicados, tipo, padre, ciclos) o persistir. Los errores se exponen por campo en camelCase mediante `ValidationProblemDetails` con `errors[field]`.

### Artefactos archivados

| Artefacto | Ruta en archive | Estado |
|-----------|-----------------|--------|
| Exploration | `exploration.md` | ✅ |
| Proposal | `proposal.md` | ✅ |
| Spec delta | `specs/unidad-organizativa-crud/spec.md` | ✅ |
| Design | `design.md` | ✅ |
| Tasks | `tasks.md` | ✅ (20/20) |
| Verify report | `verify-report.md` | ✅ PASS |
| Archive report | `archive-report.md` | ✅ (este) |

### IDs de observaciones Engram

| Observación | Engram ID | Topic key |
|-------------|-----------|-----------|
| Apply progress | #98 | `sdd/implementar-fluentvalidation-validaciones-unidades-organizativas/apply-progress` |
| Verify session summary | #102 | `sdd/implementar-fluentvalidation-validaciones-unidades-organizativas/verify-report` |
| Archive report | (nuevo) | `sdd/implementar-fluentvalidation-validaciones-unidades-organizativas/archive-report` |

## Specs sincronizadas

| Domain | Action | Detalles |
|--------|--------|---------|
| `unidad-organizativa-crud` | Updated | 1 requirement modificada (`Validate Organizational Unit Writes` → reglas FluentValidation + 2 nuevos scenarios), 2 requirements agregadas (`Exponer errores de validación por campo`, `Mantener frontera de validación`). Source of truth: `openspec/specs/unidad-organizativa-crud/spec.md` |

## Resumen del cambio

### ¿Qué se cambió?

1. **Paquetes NuGet**: Se agregaron `FluentValidation 12.1.1` y `FluentValidation.DependencyInjectionExtensions 12.1.1` a `SGV.Aplicacion.csproj`.
2. **Registro DI**: Se creó `SGV.Aplicacion/DependencyInjection.cs` con `AddAplicacionServicios()` que registra validadores via `AddValidatorsFromAssemblyContaining`.
3. **Validadores**:
   - `CrearUnidadOrganizativaRequestValidator.cs` — reglas: `Codigo` requerido/máx 50, `Nombre` requerido/máx 200, `Descripcion` máx 1000, `TipoUnidadOrganizativaId != Guid.Empty`, `VigenteHasta >= VigenteDesde`.
   - `ActualizarUnidadOrganizativaRequestValidator.cs` — mismas reglas, con `TipoUnidadOrganizativaId` no vacío solo si se envía.
4. **Short-circuit en servicio**: `UnidadOrganizativaServicioComandos` inyecta `IValidator<TRequest>` y valida antes de cualquier consulta a repositorio.
5. **Errores por campo**: `UnidadOrganizativaCommandResult` extendido con `FieldErrors` (diccionario). Helper `BuildFieldErrors` + `ToCamelCase` transforma `PropertyName` a camelCase localmente.
6. **Controller**: Nuevo método `ToValidationProblemResult` construye `ValidationProblemDetails` desde `FieldErrors`, separado de `ToProblemResult` para errores de negocio.
7. **Tests**: 42 tests de validadores, 27 tests de servicio (incluyendo 7 nuevos con contadores de fake repo), 22 tests HTTP.

### ¿Por qué?

Centralizar validaciones de entrada en FluentValidation dentro de `SGV.Aplicacion` reduce validaciones dispersas, mejora errores por campo y mantiene el dominio como última barrera de invariantes.

### Riesgos abiertos

| Riesgo | Severidad | Mitigación |
|--------|-----------|------------|
| SUGGESTION: Test HTTP end-to-end con servicio real | Baja | Documentado y aceptado. Controller es pass-through literal; la transformación camelCase está probada en servicio con validadores reales. Reabrir si futuro refactor del controller introduce transformación de casing. |
| Divergencia entre dominio y FluentValidation | Media | Frontera documentada: shape (FluentValidation) → invariantes (dominio) → reglas con repositorio (servicio). Tests de dominio no modificados siguen pasando. |
| CamelCase global no configurado | Baja | Decisión deliberada: transformación local para no afectar otros validators del proyecto. Si se agregan más validadores, evaluar `PropertyNameResolver` global. |

## Verificación final

- `dotnet build`: 0 warnings, 0 errors
- `dotnet test`: 202/202 passing
- Cobertura de líneas en archivos nuevos/modificados: 100% (excepto rutas pre-existentes no tocadas)
- TDD Compliance: 5/5 checks ✅
- Spec compliance: 12/12 escenarios ✅
- CRITICAL: 0
- WARNING: 0
- SUGGESTION: 1 (deferral documentado con re-open trigger explícito)

## SDD Cycle Complete

El cambio fue completamente planeado (explore → proposal → spec → design → tasks), implementado (apply con 2 PRs + remediation), verificado (verify PASS) y archivado. Listo para el próximo cambio.
