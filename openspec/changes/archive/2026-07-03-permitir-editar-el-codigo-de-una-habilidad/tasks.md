# Tasks: Permitir editar el código de una Habilidad

> **Change:** `permitir-editar-el-codigo-de-una-habilidad`
> **Phase:** `sdd-tasks`
> **Strict TDD:** `true` (RED → GREEN → REFACTOR por escenario)
> **Base PR:** `develop`
> **Delivery:** 1 slice, `stacked-to-main`, budget 400 líneas

## 1. Overview

Forma del delivery:

- 1 PR único, `stacked-to-main` contra `develop` (decidido por el usuario).
- Budget 400 líneas (forecast ~380; ver §5).
- `strict_tdd: true`: cada work unit introduce tests rojos antes de producción.
- Estrategia de orden: **Dominio → Aplicación → Persistencia → API → Web**.
- El cambio **NO** requiere migración de EF; la columna generada
  `ActiveCodigoUnique` se recalcula sola cuando cambia `Codigo`.
- **NO** se introduce un catálogo de niveles ni se agrega `NivelId` a la
  entidad (anti-drift respecto del precedente de `Cargo`).

---

## 2. Work Units (orden de ejecución)

Cada task referencia archivo(s) concreto(s), scenario(s) del delta spec
que cumple y la verificación observable. Numeración jerárquica por capa.

### 1. Dominio

- [x] **1.1 RED: tests de dominio para edición de `Codigo`**.
    - Capa: Dominio (tests).
    - Archivos: `tests/SGV.Tests/Dominio/HabilidadTests.cs`.
    - Eliminar o reemplazar `Codigo_EsInmutableTrasCreacion`,
      `Actualizar_ModificaCamposEditables` y `Actualizar_CodigoNoCambia`
      (ahora fijan la regla opuesta).
    - Agregar `Actualizar_CambiaCodigoSiNoDuplica`,
      `Actualizar_ConCodigoVacio_ThrowsArgumentException`,
      `Actualizar_ConCodigoMayorA50_ThrowsArgumentException`.
    - Scenario(s) cubiertos: `habilidad-management` → ADDED
      *Edición de Codigo con unicidad activa* / MODIFIED *Actualizar
      Habilidad* (`Actualización exitosa con cambio de Codigo`,
      `Codigo inválido en update`).
    - Verificación: `dotnet test --filter
      "FullyQualifiedName~HabilidadTests"` falla con el motivo esperado.
    - Dependencias: —.

- [x] **1.2 GREEN: `Habilidad.Actualizar` acepta `codigo`**.
    - Capa: Dominio.
    - Archivo: `src/SGV.Dominio/Habilidades/Habilidad.cs`.
    - Nueva firma:
      `public void Actualizar(string codigo, string nombre, string? categoria = null, string? descripcion = null)`
      que reutiliza `ValidacionesDominio.Requerido(codigo, ..., 50)`.
      `Codigo` mantiene `private set`; actualizar el XML doc de la
      propiedad y del método.
    - Verificación: 1.1 verde.
    - Dependencias: 1.1.

### 2. Aplicación

- [x] **2.1 RED: tests del validator para `Codigo` en update**.
    - Capa: Aplicación (tests).
    - Archivo: `tests/SGV.Tests/Aplicacion/Habilidades/ActualizarHabilidadRequestValidatorTests.cs`.
    - Tests: `Should_Have_Error_When_Codigo_Is_Empty`,
      `Should_Have_Error_When_Codigo_Is_Whitespace`,
      `Should_Have_Error_When_Codigo_Exceeds_Max_Length`,
      `Should_Not_Have_Error_For_Valid_Codigo`; ajustar el helper
      `RequestValido()` para incluir `Codigo` válido.
    - Scenario(s) cubiertos: `habilidad-management` → MODIFIED
      *Actualizar Habilidad* (`Codigo inválido en update`).
    - Verificación: validator tests rojos.
    - Dependencias: 1.2.

- [x] **2.2 GREEN: extender `ActualizarHabilidadRequest` y su
      validator**.
    - Capa: Aplicación.
    - Archivos:
      `src/SGV.Aplicacion/Habilidades/Comandos/HabilidadRequests.cs`,
      `src/SGV.Aplicacion/Habilidades/Comandos/Validaciones/ActualizarHabilidadRequestValidator.cs`.
    - Agregar `string Codigo` al record
      `ActualizarHabilidadRequest` (primer parámetro para no romper
      call sites que usan argumentos con nombre si los hubiera);
      `RuleFor(x => x.Codigo).NotEmpty().MaximumLength(50)` en el
      validator; eliminar el comentario que declaraba inmutable.
    - Verificación: 2.1 verde; `dotnet build` sigue compilando call
      sites existentes (`Edit.cshtml.cs`, `SkillsController.Update`,
      `HabilidadApiClient.UpdateAsync`, `HabilidadServicioComandos`).
    - Dependencias: 2.1.

- [x] **2.3 RED: tests de servicio para unicidad activa en update**.
    - Capa: Aplicación (tests).
    - Archivo: `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs`.
    - Tests:
      `ActualizarAsync_CodigoDuplicadoActivo_RetornaConflictoYSinGuardar`,
      `ActualizarAsync_MismoCodigo_NoSeTrataComoDuplicado`,
      `ActualizarAsync_CodigoDeEliminada_PermiteReutilizar`,
      `ActualizarAsync_CodigoValido_PersisteYCambiaCodigo`,
      `ActualizarAsync_CodigoInvalido_CortaAntesDeConsultarRepos`.
    - Scenario(s) cubiertos: `habilidad-management` → ADDED
      *Edición de Codigo con unicidad activa* (los 3 scenarios) y
      MODIFIED *Actualizar Habilidad*
      (`Actualización exitosa con cambio de Codigo`,
      `Actualización exitosa sin cambiar Codigo`).
    - Verificación: tests rojos por mensaje de conflicto
      `HabilidadErrorType.Conflict`/`CodigoDuplicado`.
    - Dependencias: 2.2.

- [x] **2.4 GREEN: `HabilidadServicioComandos.ActualizarAsync` con
      pre-check de unicidad + catch de índice único**.
    - Capa: Aplicación.
    - Archivo:
      `src/SGV.Aplicacion/Habilidades/Comandos/HabilidadServicioComandos.cs`.
    - Llamar
      `await repository.ExistsActiveCodeAsync(request.Codigo, excludingId: id, ct)`
      después de cargar la entidad; abortar con
      `HabilidadErrorType.Conflict / "CodigoDuplicado"` si retorna true.
    - Reutilizar el helper privado `EnsureCodigoNoDuplicado(string codigo,
      Guid id, CancellationToken)` (espejo del patrón actual de
      `CargoServicioComandos`) para no duplicar la lógica con
      `CrearAsync`.
    - Pasar `request.Codigo` a `habilidad.Actualizar(...)`.
    - Envolver `SaveChangesAsync` con `catch (DbUpdateException ex) when
      (IsActiveCodigoUniqueViolation(ex))` y mapear a
      `HabilidadErrorType.Conflict / "CodigoDuplicado"`; otras
      violaciones se propagan.
    - El helper `IsActiveCodigoUniqueViolation` analiza el `InnerException`
      buscando `IX_Habilidades_ActiveCodigoUnique` (o `ActiveCodigoUnique`)
      sin meter dependencias Pomelo en `SGV.Aplicacion`.
    - Verificación: 2.3 verde.
    - Dependencias: 2.3.

- [x] **2.5 Migrar tests existentes que protegen la regla opuesta**.
    - Capa: Aplicación (tests).
    - Archivos:
      `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs`,
      `tests/SGV.Tests/Aplicacion/Habilidades/ActualizarHabilidadRequestValidatorTests.cs`.
    - Eliminar o reemplazar `ActualizarAsync_DatosValidos_RetornaDtoActualizadoYGuarda`
      (hoy afirma que `Codigo` queda en `COM01`) y
      `ActualizarAsync_CodigoNoExpuesto_LoIgnora` (ya no aplica: el
      request sí expone `Codigo`).
    - Verificación: `grep -rn "CodigoNoCambia\|CodigoNoExpuesto"
      tests/SGV.Tests/Aplicacion/Habilidades` no devuelve hits y la suite
      sigue verde con los tests reemplazados.
    - Dependencias: 2.4.

### 3. Persistencia

- [x] **3.1 RED: tests de repositorio para `UpdateAsync` que propaga
      `Codigo`**.
    - Capa: Persistencia (tests).
    - Archivo: `tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs`.
    - Tests:
      `UpdateAsync_CambiaCodigoYPersisteConUnicidadActiva`,
      `UpdateAsync_MismoCodigo_NoViolaIndice`,
      `UpdateAsync_CodigoDuplicadoDeOtraActiva_ThrowsDbUpdateException`
      (este último sólo con `[MySqlFact]` real o con un mock que
      simule la violación del índice para el unit test).
    - Actualizar `UpdateAsync_ModificaCampos` para que aserte el cambio
      de `Codigo` cuando se provee uno distinto.
    - Scenario(s) cubiertos: `habilidad-management` →
      *Actualización exitosa con cambio de Codigo* y
      `habilidad-web-crear-editar` → ADDED *Reutilizar un Codigo
      liberado por baja lógica* (mitad backend de esta scenario).
    - Verificación: tests rojos por la aserción de `Codigo` actualizado
      y por la excepción esperada.
    - Dependencias: 2.5.

- [x] **3.2 GREEN: propagar `Codigo` en `UpdateEntity`**.
    - Capa: Persistencia.
    - Archivo:
      `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs`.
    - En `UpdateEntity(HabilidadEntity, Habilidad)` agregar la línea
      que copia `Codigo` desde la entidad de dominio a la entidad
      rastreada. **SIN** migración nueva: la columna generada
      `ActiveCodigoUnique` se recalcula automáticamente.
    - Verificación: 3.1 verde. Re-leer
      `src/SGV.Infraestructura/Persistencia/Configuraciones/HabilidadConfiguracion.cs`
      sin cambiar nada (la constraint ya cubre update).
    - Dependencias: 3.1.

### 4. API

- [x] **4.1 RED: tests de API para `PUT` con `codigo` (400/409)**.
    - Capa: API (tests).
    - Archivo: `tests/SGV.Tests/Api/SkillsControllerTests.cs`.
    - Tests:
      `Put_ValidRequest_WithCodigo_Returns200OkWithUpdatedDto`
      (ajustar el existente que hoy no envía `codigo`),
      `Put_EmptyCodigo_Returns400WithFieldErrors`,
      `Put_CodigoExceedsMaxLength_Returns400WithFieldErrors`,
      `Put_DuplicateActiveCodigo_Returns409WithProblemDetails`.
    - Scenario(s) cubiertos: `habilidad-management` → MODIFIED
      *Actualizar Habilidad* (los 3 scenarios) y
      `habilidad-web-crear-editar` → ADDED
      *Codigo duplicado de otra Habilidad activa*.
    - Verificación: tests rojos con los códigos esperados.
    - Dependencias: 2.5.

- [x] **4.2 GREEN: actualizar XML doc y contrato del `PUT`**.
    - Capa: API.
    - Archivo: `src/SGV.Api/Controllers/SkillsController.cs`.
    - El endpoint ya delega a `HabilidadServicioComandos`; bastan
      comentarios XML del método `Update` que reflejen que `PUT` edita
      `codigo` y que los códigos 200/400/404/409 se conservan.
      Sin cambio funcional nuevo: el `HabilidadCommandResult.Failure`
      ya cubre `FieldErrors` (400) y `Conflict` (409).
    - Ajustar `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` si el
      fake conserva la `Codigo = "PROG"` en el DTO devuelto (debe
      reflejar el `request.Codigo`).
    - Verificación: 4.1 verde.
    - Dependencias: 4.1.

### 5. Web

- [x] **5.1 RED: tests web de Edit con `Codigo` editable**.
    - Capa: Web (tests).
    - Archivo: `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs`.
    - Reemplazar el test
      `EditPage_MuestraCodigoComoReadonly_O_Disabled` por
      `EditPage_MuestraCodigoEditable`.
    - Agregar tests:
      `Post_Edit_WhenCodigoChanges_RedirectsWithUpdatedCodigo`,
      `Post_Edit_WhenCodigoUnchanged_UpdatesOtherFields`,
      `Post_Edit_WhenCodigoConflict_ShowsFieldErrorAndKeepsForm`,
      `Post_Edit_WhenCodigoReusedFromSoftDeleted_Succeeds`,
      `Post_Edit_WhenInvalidCodigo_ShowsValidationErrorAndKeepsForm`
      (paridad con el precedent Cargo).
    - Scenario(s) cubiertos: `habilidad-web-crear-editar` → ADDED
      *Edición web de Codigo de una Habilidad* (los 5 scenarios) y
      MODIFIED *Campos visibles y Codigo inmutable* /
      *Guardado con PRG y feedback accionable*.
    - Verificación: tests rojos por la nueva aserción (input sin
      `readonly`, redirect con nuevo `Codigo`, error de campo en
      conflicto).
    - Dependencias: 4.2.

- [x] **5.2 GREEN: remover `readonly` en `_Form.cshtml` y postear
      `Codigo` desde `Edit.cshtml.cs`**.
    - Capa: Web.
    - Archivos:
      `src/SGV.Web/Pages/Organizacion/Habilidades/_Form.cshtml`,
      `src/SGV.Web/Pages/Organizacion/Habilidades/Edit.cshtml.cs`.
    - En `_Form.cshtml`: eliminar la rama `readonly` específica de edit
      y dejar un único `<input asp-for="Input.Codigo" ...>`
      editable; quitar el comentario `REQ-HCW-01` que justificaba la
      inmutabilidad.
    - En `Edit.cshtml.cs`: construir
      `new ActualizarHabilidadRequest(Input.Codigo, Input.Nombre,
      Input.Categoria, Input.Descripcion)` en `OnPostAsync`; mantener
      PRG a `Details` (o a sí mismo) y el manejo existente de 409
      sobre `Input.Codigo` (`ModelState.AddModelError(nameof(Input.Codigo),
      ...)`).
    - Verificación: 5.1 verde.
    - Dependencias: 5.1.

- [x] **5.3 GREEN: transporte de `Codigo` en el cliente HTTP y su
      fake**.
    - Capa: Web + Web (tests).
    - Archivos:
      `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs`,
      `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs`,
      `tests/SGV.Tests/Web/Habilidad/FakeHabilidadApiClient.cs`
      (o donde exista el fake).
    - Cambiar el comentario y/o el shape de `UpdateAsync`; el cuerpo
      sigue serializando `ActualizarHabilidadRequest`, así que el
      cambio es trivial en el cliente real. En el fake, exponer
      `UpdateResult` configurable y capturar
      `(Guid Id, ActualizarHabilidadRequest Request,
      CancellationToken)` en `UpdateCalls` para que los tests de 5.1
      puedan inspeccionar lo que se envía.
    - Verificación: `dotnet build` verde; los tests de 5.1 pueden
      inspeccionar `UpdateCalls[0].Request.Codigo`.
    - Dependencias: 5.2 (no compila hasta que 5.2 cambie la firma del
      request).

- [x] **5.4 VERIFY: suite web completa + smoke frontend**.
    - Capa: Soporte.
    - Comandos: `dotnet build SGV.slnx`; `dotnet test SGV.slnx
      --no-build --configuration Release`; `bun install && bun run
      build` desde `src/SGV.Web` (smoke pipeline de assets).
    - Verificación: suite completa verde + `bun run build` sin
      warnings de bundler.
    - Dependencias: 5.3.

### 6. Specs (delta formales + archive posterior)

- [x] **6.1 Confirmar delta specs contra la implementación final**.
    - Capa: Specs.
    - Archivos:
      `openspec/changes/permitir-editar-el-codigo-de-una-habilidad/specs/habilidad-web-crear-editar/spec.md`,
      `openspec/changes/permitir-editar-el-codigo-de-una-habilidad/specs/habilidad-management/spec.md`.
    - Verificar que los scenarios de §2–§5 están reflejados en los
      delta specs; ajustar la prosa sólo si la implementación reveló
      un detalle que el spec no captura (no inventar nuevos
      scenarios fuera de los work units).
    - Verificación: `openspec validate
      permitir-editar-el-codigo-de-una-habilidad --strict --json`
      retorna éxito.
    - Dependencias: 5.4.

- [x] **6.2 NOTE**: la sincronización de los delta specs contra los
      baselines (`openspec/specs/.../spec.md`) y el `archive-report.md`
      se ejecutan en la fase **`sdd-archive`**, no en esta fase.
      Quedan fuera del scope de `sdd-apply`.

---

## 3. Work-Unit Commit Strategy

Alineada con §6 del `design.md`. Cada commit es revisable por sí mismo y
mantiene tests juntos al código que verifican (RED → GREEN → REFACTOR).

1. **feat(habilidades): permitir reasignar Codigo en
   `Habilidad.Actualizar`** — Dominio (1.1 + 1.2 + migración de tests
   viejos). Commits separados para RED y GREEN si el equipo lo
   prefiere; mínimo 1 commit por capa.
2. **feat(habilidades): aceptar Codigo en `ActualizarHabilidadRequest`
   y traducir índice único a 409** — Aplicación (2.1 → 2.5). Helper
   `EnsureCodigoNoDuplicado` y catch `IsActiveCodigoUniqueViolation`
   entran en este commit junto con sus tests.
3. **feat(habilidades): propagar Codigo en `UpdateEntity` del mapper**
   — Persistencia (3.1 + 3.2). **SIN** migración.
4. **feat(api): PUT /api/v1/skills/{id} acepta Codigo y conserva
   400/409** — API (4.1 + 4.2 + ajuste del fake).
5. **feat(web): editar Codigo en pantalla de edición de Habilidades** —
   Web (5.1 → 5.4, incluido `bun run build`).
6. **docs(spec): confirmar delta specs reflejar la implementación** —
   Specs (6.1).

Notas sobre el orden:

- El commit 4 depende de 2 (firma de request + helper de
  aplicación). El commit 5 depende de 4.
- Cada commit deja la suite de su capa verde (roja durante RED, verde
  al cerrar GREEN).
- **NO** fragmentar commits artificialmente si juntos no exceden el
  budget.

---

## 4. Verificación end-to-end

Comandos concretos a correr antes de mergear (todos desde la raíz del
repo):

- `dotnet restore SGV.slnx`.
- `dotnet build SGV.slnx --configuration Release` (debe pasar limpio;
  coincide con CI en `.github/workflows/ci.yml`).
- `dotnet test SGV.slnx --no-build --configuration Release`
  (sin filtros para cazar regresiones).
- `dotnet test SGV.slnx --no-build --configuration Release --filter
  "FullyQualifiedName~Habilidad"` como smoke focal.
- Si hay migraciones: **NO APLICA** (este change no las introduce).
- Frontend: `bun install && bun run build` desde
  `src/SGV.Web`.
- `openspec validate permitir-editar-el-codigo-de-una-habilidad
  --strict --json` (corre tras 6.1).

Si la suite `~Habilidad` y la suite `~Web` están verdes y
`bun run build` no emite warnings nuevos, el PR está listo para review.

---

## 5. Review Workload Forecast

```
## 5. Review Workload Forecast

- **Forma del delivery decidida**: 1 slice, stacked-to-main contra
  `develop`, budget 400 líneas (decidido por el usuario).
- **PR propuesto**: PR único.
- **Estimated changed lines**: ~380 (bajo→medio), breakdown:
    - Dominio (impl): ~15.
    - Aplicación (impl): ~95.
    - Persistencia (impl): ~5.
    - API (impl): ~10.
    - Web (impl): ~70.
    - Tests: ~175 (sustituir tests que fijaban la regla opuesta +
      cubrir los delta scenarios: 1.1, 2.1, 2.3, 3.1, 4.1, 5.1).
    - Specs/docs: ~10.
- **Código de tests vs specs/docs**: ~95% impl + tests; ~5% docs.
- **400-line budget risk**: Bajo. Justificación: el cambio NO crea
  páginas nuevas, NO crea catálogos (anti-drift con Cargo), NO
  introduce navegación adicional y NO requiere migración de EF. La
  superficie es estrictamente `Update` end-to-end sobre `Codigo`.
- **Chained PRs recommended**: No. Comparado con el precedente de
  `Cargo` (~1080 líneas, 2 PRs por Create + Edit + submenú +
  catálogo `niveles-cargo`), aquí no aplica el split: el delta
  funcional es chico y autocontenido.
- **Decision needed before apply**: No.
- **Work units breakdown** (estimado de líneas `additions +
  deletions`, diff impl + tests, sin docs):
    - 1.1 + 1.2 Dominio: ~25
    - 2.1 → 2.5 Aplicación: ~165
    - 3.1 + 3.2 Persistencia: ~25
    - 4.1 + 4.2 API: ~35
    - 5.1 → 5.3 Web + fake: ~95
    - 5.4 verify suite: 0 diff
    - 6.1 confirmar delta specs: ~5
    - **Total**: ~350 → cabe en 400.
```

Si durante `sdd-apply` el diff supera 400 líneas, recortar alcance
accidental (tests redundantes); **NO** ampliar features ni inventar un
chained split sin re-debate.

---

## 6. Rollback plan

Pasos para revertir (del `design.md` §8, sin reescribir código):

1. `ActualizarHabilidadRequest`: quitar el parámetro `Codigo` y la
   regla de validator asociada.
2. `Habilidad.Actualizar(...)`: restaurar firma vieja
   `(string nombre, string? categoria = null, string? descripcion =
   null)` y el XML doc que declara `Codigo` inmutable.
3. `DomainToPersistenceMapper.UpdateEntity`: dejar de copiar
   `Codigo`.
4. Web: restaurar la rama `readonly` en `_Form.cshtml` (con el
   comentario `REQ-HCW-01`); en `Edit.cshtml.cs`, dejar de postear
   `Input.Codigo`.
5. Tests: re-habilitar los tests sustituidos en 1.1 y 2.5.
6. **Sin rollback de schema**: no hay migración ni cambios de
   constraint revertibles.

---

## 7. Dependencias externas / blockers

- **MySQL 8 para `[MySqlFact]`**: opcional. Sin MySQL local, esos
  tests se skipean; el cambio no depende de ellos para mergear.
- **Anti-drift con `Cargo`**: `Habilidad` no introduce `NivelId`
  ni catálogo de niveles; `HabilidadAntiDriftTests` siguen
  vigentes y no se tocan.
- **Consumidores externos de `PUT /api/v1/skills/{id}`**: el design
  asume que `SGV.Web` es el único consumidor. No bloqueante para
  este PR; si apareciera un consumidor externo, documentar el
  breaking change fuera de este change.
- **Sin bloqueadores** que requieran decisión del usuario.
