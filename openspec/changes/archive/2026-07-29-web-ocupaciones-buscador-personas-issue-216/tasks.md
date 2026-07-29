# Tareas: Buscador de Personas en Ocupación (#216)

## Resumen

6 tareas en 3 grupos/commits RED→GREEN. Estimación: ~382 líneas; PR único con control de tamaño.

Decision needed before apply: Yes
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Medium

## Work-unit-group 1: Configuración del filtro compartido

**Propósito**: Hacer configurable el filtro sin alterar Usuarios. **Riesgo compartido alto**: modifica `usuario-persona-buscador.js`.

### T-001
- **Tipo**: `test-first` · **Título**: Proteger configuración del filtro
- **Archivos a tocar**: `tests/SGV.Tests/Web/Usuario/PersonaBuscadorModalTests.cs`
- **Descripción**: Agregar contratos para default `true` y atributo `false`. Deben fallar antes del cambio JS.
- **Criterio de done**: casos ausente/inválido preservan Usuarios; `false` queda declarado para reutilización.
- **Dependencias**: ninguna · **LOC +/- estimado**: 18
- **Tests asociados**: `Modal_DefaultsSoloSinUsuarioToTrue`; `Modal_FalseConfig_IsSupported`.

### T-002
- **Tipo**: `production` · **Título**: Conditionalizar soloSinUsuario
- **Archivos a tocar**: `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js`
- **Descripción**: Parsear el atributo case-insensitive. Enviar `false` en Ocupaciones y mantener `true` por defecto.
- **Criterio de done**: T-001 verde; sin hardcode incondicional.
- **Dependencias**: T-001 · **LOC +/- estimado**: 4
- **Tests asociados**: T-001; regresión `PersonaBuscadorModalTests`.

## Work-unit-group 2: Estado enriquecido y precarga

**Propósito**: Eliminar el catálogo completo y resolver solo la persona seleccionada.

### T-003
- **Tipo**: `test-first` · **Título**: Especificar estado enriquecido
- **Archivos a tocar**: `tests/SGV.Tests/Web/Ocupaciones/OcupacionCreatePageTests.cs`, `tests/SGV.Tests/Web/Ocupaciones/OcupacionEditPageTests.cs`
- **Descripción**: Cubrir contrato, formato, llamadas y caída suave. Los tests deben quedar rojos inicialmente.
- **Criterio de done**: Create vacío no consulta; query válida precarga; inválida limpia; Edit consulta una vez.
- **Dependencias**: T-002 · **LOC +/- estimado**: 40
- **Tests asociados**: `Get_Create_WithPersonaId_PreselectsPersona`; `Get_Create_WithUnknownPersonaId_KeepsEmpty`; `Get_Edit_WhenVigente_PrepopulatesPersonaCard`.

### T-004
- **Tipo**: `production` · **Título**: Enriquecer persona seleccionada
- **Archivos a tocar**: `src/SGV.Web/Pages/Organizacion/Ocupaciones/IOcupacionForm.cs`, `OcupacionFormPageModel.cs`, `Create.cshtml.cs`, `Edit.cshtml.cs`
- **Descripción**: Sustituir `PersonaOptions` por `PersonaDisplay`/`PersonaVinculada`. Implementar `GetByIdAsync` con fallback no fatal.
- **Criterio de done**: T-003 verde; `GetAllAsync` deja de ejecutarse; documento/legajo se formatea correctamente.
- **Dependencias**: T-003 · **LOC +/- estimado**: 88
- **Tests asociados**: T-003; `dotnet test SGV.slnx --filter "Ocupacion"`.

## Work-unit-group 3: Card, modal y wiring

**Propósito**: Reemplazar el select y conectar script/modal. **Riesgo cruzado medio**: consume `_PersonaBuscadorModal.cshtml` compartido sin modificarlo.

### T-005
- **Tipo**: `test-first` · **Título**: Contratar UI del buscador
- **Archivos a tocar**: tests anteriores; `tests/SGV.Tests/Web/Ocupaciones/OcupacionBuscadorModalTests.cs`
- **Descripción**: Cubrir Create/Edit, hidden, card, script, estados y atributo `false`. Retirar expectativas del select.
- **Criterio de done**: tests rojos; ausencia del select; selección/exclusión y controles `Cambiar`/`Quitar` verificables.
- **Dependencias**: T-004 · **LOC +/- estimado**: 120
- **Tests asociados**: `Get_Create_RendersPersonaFinder`; `Get_Edit_RendersLinkedPersona`; `Modal_DeclaresSoloSinUsuarioFalse`.

### T-006
- **Tipo**: `production` · **Título**: Integrar card y modal
- **Archivos a tocar**: `src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml`, `Create.cshtml`, `Edit.cshtml`
- **Descripción**: Renderizar hidden/card y partial por path absoluto. Incluir el script en ambas páginas.
- **Criterio de done**: T-005 verde; `PuestoId` intacto; modal recibe ids/display y `data-solo-sin-usuario="false"`.
- **Dependencias**: T-005 · **LOC +/- estimado**: 112
- **Tests asociados**: T-005; suite Web/Ocupaciones.

## Plan de PR

**Single PR**, commits WUG-1→2→3 con RED+GREEN. Ejecutar `dotnet build SGV.slnx`, tests focalizados, `dotnet test SGV.slnx` y `bun run build` desde `src/SGV.Web`. Si supera 400 líneas, detener apply y proponer dos PR encadenados: filtro+PageModel → UI.

## Riesgos operativos

- Regresión de Usuarios por default del JS compartido.
- Persona inexistente puede dejar `PersonaId` inconsistente.
- ViewData del partial puede romper binding/exclusión.
- Margen de 18 líneas; controlar `git diff --stat`.
