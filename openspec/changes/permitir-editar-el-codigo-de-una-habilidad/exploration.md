# Exploración: Permitir editar el código de una Habilidad

## Contexto y motivación

El cambio busca habilitar en `SGV.Web` la edición de `Codigo` para una `Habilidad` existente desde la pantalla `Edit`, manteniendo la misma regla de unicidad activa que ya existe al crear. Hoy el comportamiento es asimétrico: `Codigo` se puede cargar en `Create`, pero en `Edit` se muestra visible y bloqueado.

El precedente directo es el change archivado de Cargos (`/Users/elflacoseba/Source/SGV/openspec/changes/archive/2026-07-01-2026-06-30-cargos-crear-editar-codigo-editable/`), ya mergeado a la rama principal según `git log --oneline --all -- openspec/specs/cargo-web-crear-editar` (`6e4c51ec`). La hipótesis de paridad es válida, pero debe respetar el drift ya documentado: `Habilidad` NO tiene `NivelId` propio y no debe copiarse el formulario de `Cargo` de manera mecánica.

## Estado actual del código

### Dominio

- La entidad `Habilidad` expone `Codigo` como `string` con `private set` y lo valida con longitud máxima 50 mediante `ValidacionesDominio.Requerido(...)` (`/Users/elflacoseba/Source/SGV/src/SGV.Dominio/Habilidades/Habilidad.cs:18-20`, `:34-40`).
- El constructor usa `CambiarDatos(codigo, nombre, categoria, descripcion)`, y ese método sí puede reasignar `Codigo`, pero está documentado como reservado al constructor y al mapper (`/Users/elflacoseba/Source/SGV/src/SGV.Dominio/Habilidades/Habilidad.cs:30-40`).
- El método público de negocio para update es `Actualizar(string nombre, string? categoria, string? descripcion)` y explícitamente NO modifica `Codigo` (`/Users/elflacoseba/Source/SGV/src/SGV.Dominio/Habilidades/Habilidad.cs:42-51`).
- Los tests actuales fijan esa inmutabilidad: `Codigo_EsInmutableTrasCreacion`, `Actualizar_ModificaCamposEditables` y `Actualizar_CodigoNoCambia` (`/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Dominio/HabilidadTests.cs:114-160`).

### Aplicación

- `ActualizarHabilidadRequest` hoy NO incluye `Codigo`; sólo acepta `Nombre`, `Categoria` y `Descripcion` (`/Users/elflacoseba/Source/SGV/src/SGV.Aplicacion/Habilidades/Comandos/HabilidadRequests.cs:13-21`).
- El validator de update replica esa decisión y documenta que `Codigo` es inmutable tras la creación (`/Users/elflacoseba/Source/SGV/src/SGV.Aplicacion/Habilidades/Comandos/Validaciones/ActualizarHabilidadRequestValidator.cs:5-22`).
- `HabilidadServicioComandos.ActualizarAsync(...)` valida el request, obtiene la entidad y llama `habilidad.Actualizar(...)`; no hace pre-check de unicidad para un código nuevo porque el request no puede traerlo (`/Users/elflacoseba/Source/SGV/src/SGV.Aplicacion/Habilidades/Comandos/HabilidadServicioComandos.cs:77-111`).
- A diferencia de `CargoServicioComandos`, el servicio de Habilidad no tiene hoy un catch específico para traducir violaciones del índice único activo durante `update` (`/Users/elflacoseba/Source/SGV/src/SGV.Aplicacion/Habilidades/Comandos/HabilidadServicioComandos.cs:97-110` vs `/Users/elflacoseba/Source/SGV/src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs:123-137`, `:252-285`).
- Los tests de aplicación también fijan la inmutabilidad: `ActualizarAsync_DatosValidos_RetornaDtoActualizadoYGuarda` verifica que el DTO devuelto conserva `COM01`, y `ActualizarAsync_CodigoNoExpuesto_LoIgnora` documenta que el request no tiene código (`/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs:115-163`).

### API

- El endpoint es `PUT /api/v1/skills/{id}` en `SkillsController.Update(...)`, pero consume `ActualizarHabilidadRequest`, por lo que hoy el contrato HTTP tampoco acepta `Codigo` (`/Users/elflacoseba/Source/SGV/src/SGV.Api/Controllers/SkillsController.cs:140-172`).
- La documentación del endpoint ya está desalineada con la implementación: declara un `409` por código en uso (`/Users/elflacoseba/Source/SGV/src/SGV.Api/Controllers/SkillsController.cs:150`) aunque el request actual no permite cambiarlo.
- Los tests de API reflejan el contrato actual: el `PUT` válido envía sólo `nombre` y `categoria`, y el test de `400` sólo espera error en `nombre` (`/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Api/SkillsControllerTests.cs:280-347`).
- El fake de comandos API también conserva esa forma: ante update exitoso devuelve siempre `Codigo = "PROG"`, ignorando cualquier potencial cambio futuro (`/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Api/ApiWebApplicationFactory.cs:546-554`).

### Frontend (Razor Pages)

- El `PageModel` de edit documenta explícitamente que `Codigo` es readonly y que NO se envía al backend (`/Users/elflacoseba/Source/SGV/src/SGV.Web/Pages/Organizacion/Habilidades/Edit.cshtml.cs:10-15`).
- En `OnPostAsync`, el request de update se construye sin `Codigo` (`/Users/elflacoseba/Source/SGV/src/SGV.Web/Pages/Organizacion/Habilidades/Edit.cshtml.cs:103-107`).
- El partial compartido `_Form.cshtml` tiene una rama especial para edit que renderiza `<input ... readonly />` y un comentario `REQ-HCW-01` que justifica esa decisión por la “inmutabilidad del dominio” (`/Users/elflacoseba/Source/SGV/src/SGV.Web/Pages/Organizacion/Habilidades/_Form.cshtml:9-26`).
- En `Create`, el mismo partial deja `Codigo` editable (`/Users/elflacoseba/Source/SGV/src/SGV.Web/Pages/Organizacion/Habilidades/Create.cshtml:23-35`, `/Users/elflacoseba/Source/SGV/src/SGV.Web/Pages/Organizacion/Habilidades/Create.cshtml.cs:58-68`).
- `IHabilidadApiClient.UpdateAsync(...)` y su implementación consumen `ActualizarHabilidadRequest`, por lo que el cliente tipado tampoco transporta `Codigo` (`/Users/elflacoseba/Source/SGV/src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs:34-38`, `/Users/elflacoseba/Source/SGV/src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs:91-106`).
- `HabilidadInputModel` ya tiene `Codigo` con `[Required]` y `[StringLength(50)]`, así que el shape del form no necesita un modelo nuevo para habilitar la edición (`/Users/elflacoseba/Source/SGV/src/SGV.Web/Integration/Habilidades/HabilidadInputModel.cs:11-25`).

### Persistencia / Constraints

- La configuración EF define `Codigo` como `required`, `maxLength(50)` y agrega la computed column `ActiveCodigoUnique = CASE WHEN IsDeleted = 0 THEN Codigo ELSE NULL END` con índice único (`/Users/elflacoseba/Source/SGV/src/SGV.Infraestructura/Persistencia/Configuraciones/HabilidadConfiguracion.cs:15-25`).
- La migración inicial materializa ese patrón en MySQL: columna generada en `Habilidades.ActiveCodigoUnique` e índice único `IX_Habilidades_ActiveCodigoUnique` (`/Users/elflacoseba/Source/SGV/src/SGV.Infraestructura/Persistencia/Migraciones/20260614183103_InicialSgvo.cs:173-198`, `:1044-1048`).
- Esto significa que NO hace falta migración nueva para permitir updates de `Codigo`: MySQL recalcula la columna generada automáticamente cuando cambia `Codigo` o `IsDeleted`.
- Pero hoy el mapper de persistencia para `Habilidad` NO copia `Codigo` al actualizar: `UpdateEntity(HabilidadEntity, Habilidad)` sólo propaga `Nombre`, `Descripcion`, `Categoria`, flags y auditoría (`/Users/elflacoseba/Source/SGV/src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs:143-151`). Aunque el dominio cambiara `Codigo`, hoy se perdería en persistencia.
- `ExistsActiveCodeAsync(codigo, excludingId)` ya soporta el caso de update excluyendo el propio id (`/Users/elflacoseba/Source/SGV/src/SGV.Infraestructura/Persistencia/Repositorios/HabilidadRepository.cs:103-117`).

### Tests existentes

- Dominio: hay tests que habría que reemplazar, no sólo extender, porque hoy validan la regla opuesta (`/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Dominio/HabilidadTests.cs:114-160`).
- Aplicación: faltan casos equivalentes a Cargo para `Codigo` requerido, duplicado activo, exclusión del propio id y potencial race con índice único (`/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Aplicacion/Habilidades/ActualizarHabilidadRequestValidatorTests.cs:10-109`, `/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs:113-181`).
- API: falta cubrir `PUT` con `codigo` válido, `400` por `codigo` vacío y `409` por duplicado activo (`/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Api/SkillsControllerTests.cs:280-347`).
- Web: `HabilidadEditPageTests.EditPage_MuestraCodigoComoReadonly_O_Disabled` fija exactamente el comportamiento que este cambio debe romper (`/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs:120-146`).
- Persistencia: `UpdateAsync_ModificaCampos` verifica que `Codigo` quede igual (`/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs:295-319`). No existen aún tests MySQL específicos para update exitoso de código, duplicado activo ni reutilización tras soft delete.
- Anti-drift: los tests de `HabilidadAntiDriftTests` sólo blindan la ausencia de `Nivel`; no bloquean hacer editable `Codigo`, por lo que siguen siendo compatibles con este cambio (`/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Web/Habilidad/HabilidadAntiDriftTests.cs:15-20`, `:89-97`).

## Cambio análogo en Cargos (referencia)

- El change de Cargos explicitó la reversión de la inmutabilidad previa y modificó dominio, request, validator, servicio, API, Razor y tests (`/Users/elflacoseba/Source/SGV/openspec/changes/archive/2026-07-01-2026-06-30-cargos-crear-editar-codigo-editable/proposal.md:5-17`).
- La solución técnica fue: agregar `Codigo` a `ActualizarCargoRequest`, validar shape en FluentValidation, hacer pre-check de unicidad activa en `CargoServicioComandos`, mantener el índice único como árbitro final y mapear la violación específica de `IX_Cargos_ActiveCodigoUnique` a `409 Conflict` (`/Users/elflacoseba/Source/SGV/openspec/changes/archive/2026-07-01-2026-06-30-cargos-crear-editar-codigo-editable/design.md:20-29`, `:35-45`; `/Users/elflacoseba/Source/SGV/src/SGV.Aplicacion/Organizacion/Comandos/CargoRequests.cs:13-24`; `/Users/elflacoseba/Source/SGV/src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs:112-137`, `:238-285`).
- En web, el partial `_Form.cshtml` pasó a dejar `Codigo` editable también en edit, y el `EditModel` empezó a postear `Input.Codigo` dentro de `ActualizarCargoRequest` (`/Users/elflacoseba/Source/SGV/src/SGV.Web/Pages/Organizacion/Cargos/_Form.cshtml:10-15`; `/Users/elflacoseba/Source/SGV/src/SGV.Web/Pages/Organizacion/Cargos/Edit.cshtml.cs:133-145`).
- En apply-progress quedó documentado el punto clave de persistencia: la computed column se recalcula sola y no requiere migración nueva (`/Users/elflacoseba/Source/SGV/openspec/changes/archive/2026-07-01-2026-06-30-cargos-crear-editar-codigo-editable/apply-progress.md:49-56`).
- También quedó una lección útil para Habilidad: el pre-check mejora UX, pero no reemplaza al índice único; si se permite editar `Codigo`, conviene decidir si Habilidad adopta el mismo catch específico de `DbUpdateException` que Cargo.

## Gaps detectados

1. **El bloqueo de `Codigo` no es sólo visual**: está codificado en dominio, aplicación, API, mapper de persistencia, frontend y tests.
2. **El baseline spec actual contradice el cambio deseado**:
   - `habilidad-web-crear-editar` exige que en edit `Codigo` permanezca readonly o deshabilitado (`/Users/elflacoseba/Source/SGV/openspec/specs/habilidad-web-crear-editar/spec.md:32-50`).
   - `habilidad-management` exige que `Codigo` MUST NOT ser editable tras la creación y que el contrato de update MUST NOT incluirlo (`/Users/elflacoseba/Source/SGV/openspec/specs/habilidad-management/spec.md:64-80`).
3. **El mapper de persistencia impediría persistir el cambio aunque se habilitara arriba** (`/Users/elflacoseba/Source/SGV/src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs:143-151`).
4. **Falta cobertura de MySQL real** para el nuevo comportamiento de update de `Codigo` con índice único activo.
5. **El fake de API/controller y los tests web actuales asumen `Codigo` inmutable**, por lo que el cambio tocará varios seams de prueba, aunque el scope funcional sea chico.

## Riesgos y dudas abiertas

- **Riesgo contractual**: `PUT /api/v1/skills/{id}` cambiaría su body, igual que pasó con `Cargos`. Dentro del repo no aparecieron otros consumers runtime claros, pero el contrato HTTP sí cambia.
- **Riesgo de carrera**: si sólo se agrega pre-check y no se traduce la violación del índice único, un conflicto concurrente podría escapar como `500` en lugar de `409`.
- **Riesgo de drift por copia**: NO debe copiarse ningún manejo de `NivelId` o catálogo de niveles desde Cargos; Habilidad sigue sin campo de nivel propio.
- **Riesgo de alcance en tests**: aunque el cambio de producción es pequeño, hay varias pruebas que habrá que actualizar porque hoy protegen la regla opuesta.
- **Trabajo del usuario en curso**: no encontré carpeta previa para este change en `openspec/changes/permitir-editar-el-codigo-de-una-habilidad/`; no hubo nada que preservar.

## Recomendación para la propuesta

### Enfoque recomendado

Tomar **paridad controlada con Cargos**, pero sólo en lo que aplica a `Habilidad`:

1. **Dominio**: permitir que `Actualizar(...)` acepte `codigo` o introducir una variante equivalente, manteniendo `private set` y la validación de shape dentro de la entidad.
2. **Aplicación**: agregar `Codigo` a `ActualizarHabilidadRequest`, extender el validator y hacer pre-check con `ExistsActiveCodeAsync(request.Codigo, id, ...)`.
3. **Persistencia**: actualizar `DomainToPersistenceMapper.UpdateEntity(HabilidadEntity, Habilidad)` para propagar `Codigo`.
4. **API/Web**: enviar `Codigo` en update y remover el `readonly` del input en edit.
5. **Tests**: reemplazar explícitamente los tests que fijan inmutabilidad por tests que fijen editabilidad con unicidad activa.

### Alternativas

| Enfoque | Pros | Contras | Complejidad |
|---|---|---|---|
| Paridad completa con Cargo, incluyendo catch específico del índice | Comportamiento consistente entre módulos; UX y API más robustas frente a carreras | Más archivos/touch points y más tests | Media |
| Habilitar sólo el cambio funcional mínimo (sin catch específico de `DbUpdateException`) | Menor diff inicial | Peor manejo de conflictos concurrentes; menor paridad técnica | Baja-media |

### Slice / PR recomendado

- **Slice único probable**: este cambio parece bastante más chico que el de Cargos porque no crea páginas nuevas, no agrega catálogos, no toca navegación y no requiere migración. A priori debería entrar en **1 PR reviewable** si se mantiene enfocado en update + tests.
- **Alternativa 2 slices**: si se quiere aislar riesgo contractual, separar en **PR 1 backend+API+persistencia** y **PR 2 web+tests**. Sólo lo justificaría si durante la propuesta se confirma que el diff supera el budget o que hay consumidores sensibles del `PUT`.

### Ready for proposal

Sí. Hay evidencia suficiente para redactar propuesta. La propuesta debería declarar explícitamente que este cambio **revierte** dos reglas vigentes en spec (`Codigo` readonly en web y `Codigo` no editable en management) y que la implementación debe preservar el anti-drift de Habilidad respecto de `NivelId`.
