# Proposal: Issue #253 — Auditoría drill-down pierde userName

## Intent

Corregir el desajuste de nombre entre el query string y el binding en `DetailsModel` de Auditorías: el listado pasa `userName` en la URL pero el detalle bindea `userId`, haciendo que el filtro de usuario se pierda al hacer drill-down y al volver al listado.

## Scope

### In Scope
- Renombrar la propiedad `UserId` → `UserName` en `DetailsModel`
- Cambiar el binding `[FromQuery(Name = "userId")]` → `[FromQuery(Name = "userName")]`
- Actualizar `BuildBackUrl()` para usar `userName` en lugar de `userId`
- Agregar test de regresión que valide el round-trip del filtro `userName`

### Out of Scope
- Cambios en API o contratos de persistencia
- Modificaciones en `Index.cshtml.cs` (ya pasa `userName` correctamente)
- Refactorización semántica de `BuildBackUrl()` (enfoque mínimo)

## Capabilities

### New Capabilities
- `auditoria-drilldown-username-filter`: Test de regresión que verifica que el filtro `userName` se preserva en el drill-down y en el retorno al listado.

### Modified Capabilities
- Ninguna. Este change no modifica requerimientos de specs existentes — es un bug fix de binding.

## Approach

**Cambio mínimo quirúrgico**: renombrar el binding y propiedad en `DetailsModel.OnGet` para que coincida con el query string `userName` que ya genera `Index.cshtml`. Sin cambios en API ni persistencia.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs` | Modified | Binding `[FromQuery]` y propiedad `UserId` renombrados a `UserName` |
| `tests/SGV.Tests/Web/Auditoria/AuditoriasDetailsTests.cs` | Modified | Agregar test de round-trip `userName` |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Regresión accidental en tests existentes que referencien `UserId` | Low | Verificar que ningún test depende de la propiedad `UserId` con valor GUID técnico |
| Navegación directa sin filtro | Low | El filtro es opcional; la página debe cargar sin errores |

## Rollback Plan

Revertir los cambios en `Details.cshtml.cs`:
1. `UserName` → `UserId` en línea 107
2. `[FromQuery(Name = "userName")]` → `[FromQuery(Name = "userId")]` en línea 151
3. `userName = UserName` → `userId = UserId` en línea 128
4. Eliminar test de round-trip agregado

## Dependencies

Ninguna. El change es autocontenido en la capa Web.

## Success Criteria

- [ ] `DetailsModel.OnGet` bindea `userName` correctamente desde el query string
- [ ] `BuildBackUrl()` preserva el filtro `userName` al retornar al listado
- [ ] Test de round-trip pasa: dado un listado filtrado por `userName`, drill-down y retorno mantienen el filtro
- [ ] Navegación directa a `/auditorias/details?id=xxx` sin query string carga sin errores
- [ ] `dotnet build SGV.slnx` compila sin errores
- [ ] `dotnet test SGV.slnx` pasa sin regressions
