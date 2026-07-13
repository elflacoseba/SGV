# Exploración — Issue #120

## Contexto

La issue señala un `pageSize=200` fijo al cargar unidades organizativas (UO) desde `Puestos/Edit`. El literal sigue presente, pero el estado actual de `develop` cambia el diagnóstico original: `IUnidadOrganizativaApiClient` ya expone `GetAllActivasAsync`, `Puestos/Create` ya lo consume y el cliente recorre todas las páginas hasta alcanzar `TotalCount`.

Además, en `Puestos/Edit` el catálogo de UO no alimenta ningún dropdown visible: `UnidadOrganizativaId` es inmutable y `_Form.cshtml` solo renderiza ese select en Create. Por eso, la línea denunciada es hoy una carga truncada y redundante, no un bug funcional visible en Edit. El riesgo funcional >200 existía en Create, pero quedó mitigado parcialmente por el refactor #114; falta una prueba explícita con más de 200 elementos y documentación del contrato.

## Hallazgos de código

### Contratos y cliente web

- `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaListItemViewModel.cs:17-20` define `UnidadOrganizativaListQuery(Page, PageSize, Search, Sort, Status)` dentro de `SGV.Web`; no es un wire-type compartido.
- `src/SGV.Contracts/Organizacion/Consultas/Dtos/PagedResult.cs:6-10` define la respuesta paginada como `Items`, `TotalCount`, `Page` y `PageSize`.
- `src/SGV.Contracts/Organizacion/Consultas/Dtos/UnidadOrganizativaDto.cs:6-17` contiene el DTO compartido de UO.
- `src/SGV.Web/Integration/Organizacion/IUnidadOrganizativaApiClient.cs:11-19` expone tanto `QueryAsync(...)` como `GetAllActivasAsync(pageSize = 100, ...)`. Este último fue incorporado en `b7ff2bb9` y vuelve obsoleto el hallazgo histórico que afirmaba que solo existía `QueryAsync`.
- `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs:21-29` deserializa `/consulta` como `PagedResult<UnidadOrganizativaDto>`.
- `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs:32-60` implementa `GetAllActivasAsync` iterando páginas de 100 hasta que la cantidad acumulada alcanza `TotalCount`; no existe un tope total arbitrario. El parámetro `pageSize` sigue siendo un detalle público innecesario para los PageModels.

### Backend HTTP y persistencia

- `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs:38-46` ya ofrece `GET /api/v1/unidades-organizativas`, que retorna `IReadOnlyList<UnidadOrganizativaDto>` con todas las UO activas, sin paginación.
- `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs:176-196` ofrece la consulta paginada real en `GET /api/v1/unidades-organizativas/consulta`.
- La consulta de UO **no tiene máximo de `pageSize` ni normalización**: el controller pasa `page` y `pageSize` sin cambios a `UnidadOrganizativaQuery`. Esto difiere de `SkillsController`, que limita a 100.
- `src/SGV.Contracts/Organizacion/Consultas/Dtos/UnidadOrganizativaQuery.cs:18-25` tampoco valida rangos.
- `src/SGV.Aplicacion/Organizacion/Consultas/UnidadOrganizativaServicioConsulta.cs:9-13` respalda el endpoint no paginado mediante `repository.ListAllAsync`; `:21-40` preserva exactamente la metadata recibida en consultas paginadas.
- `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs:13-29` filtra activas/no eliminadas, ordena por código y materializa todas para `ListAllAsync`; `:144-196` aplica `Skip/Take` sin cap para `/consulta`.

### Consumidores actuales

- `src/SGV.Web/Pages/Organizacion/Puestos/Create.cshtml.cs:201-255` usa `GetAllActivasAsync`; es el único consumidor visible de un dropdown completo de UO.
- `src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml.cs:290-352` conserva el workaround `QueryAsync(... pageSize=200 ...)` y asigna solo `result.Items`.
- `src/SGV.Web/Pages/Organizacion/Puestos/_Form.cshtml:38-61` prueba que el select de UO se renderiza solo cuando `!Model.IsEdit`; en Edit la carga anterior no tiene consumidor visual. `PuestoSuperiorId` usa exclusivamente `PuestoSuperiorOptions` (`:63-72`).
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs:230,283` usa paginación legítima para el listado, no para un catálogo.
- Create/Edit de UO y Organigrama usan `GetTreeAsync`, que devuelve el árbol completo; Details usa `GetByIdAsync`. No se detectó otro catálogo UO limitado a 200.

### Pruebas y MySQL local

- `tests/SGV.Tests/Web/UnidadOrganizativa/UnidadOrganizativaApiClientTests.cs:73-107` ya protege la paginación completa, pero solo con 3 elementos en 2 páginas; no cubre explícitamente el umbral >200.
- `tests/SGV.Tests/Web/Puesto/FakeUnidadOrganizativaApiClient.cs:25-65` soporta ambos caminos y conserva un `QueryResult` histórico con `PageSize=200`, además de registrar `QueryCalls` y `GetAllActivasCalls`.
- `tests/SGV.Tests/Web/Puesto/PuestoCreatePageTests.cs:68-117` verifica que Create usa `GetAllActivasAsync` y no `QueryAsync`, pero con una sola UO.
- `tests/SGV.Tests/Web/Puesto/PuestoEditPageTests.cs` no verifica el contrato de carga completa ni la ausencia de la consulta redundante.
- MySQL local está disponible (`mysqld is alive`, versión 9.6.0), existe `sgv_test` y los 34 `UnidadOrganizativaRepositoryTests` pasaron. Es viable crear una prueba MySQL con >200 UO, aunque no es necesaria para demostrar el bug web: la regresión principal puede cubrirse de forma más rápida en el cliente tipado y el PageModel fake.
- Bloqueo de baseline: la corrida focalizada de Create/Edit/cliente produjo 21 fallos y 6 éxitos; los tests de PageModels redirigen a `/auth/sign-in` en lugar de obtener sesión autenticada. Los dos casos web representativos también fallan aislados. En cambio, los 4 tests de `UnidadOrganizativaApiClientTests` pasan. Este problema de autenticación del harness debe resolverse o declararse como preexistente antes de usar los tests web como gate RED/GREEN.

## Alternativas evaluadas

| Alternativa | Ventajas | Desventajas | Esfuerzo |
|---|---|---|---|
| **A) Nuevo `GET /api/v1/unidades-organizativas/all`** | Semántica explícita; el frontend no decide `pageSize`; permite paginación interna del backend. | Duplica el `GET` raíz que ya devuelve todas las activas; agrega ruta, documentación y tests sin eliminar un gap real. | Medio |
| **B) Aumentar `pageSize` (`int.MaxValue` o valor grande)** | Cambio mínimo en Edit. | Sigue siendo un límite mágico; `/consulta` no aplica cap ni valida rangos; puede provocar consumo excesivo, errores de proveedor u overflow en `(page - 1) * pageSize`. No garantiza completitud. | Bajo, pero frágil |
| **C) Búsqueda remota/autocomplete** | Escala mejor para catálogos grandes y evita renderizar cientos de `<option>`. | Requiere UX/JS, contrato de búsqueda, accesibilidad, debounce y manejo de selección existente; excede el alcance acotado de #120. | Alto |
| **D) Contrato de catálogo sin paginación expuesta al PageModel** | Elimina el tope arbitrario y centraliza la completitud; puede reutilizar el `GET` raíz ya existente y el cliente tipado. Es consistente con Clean Architecture y no requiere que Web referencie Api. | La respuesta completa crece con el catálogo; a escala de miles de UO deberá migrarse a C. | Bajo/Medio |

## Recomendación

Adoptar **D, reutilizando capacidades existentes y sin crear una ruta duplicada**:

1. Tratar `GetAllActivasAsync` como el único contrato web para dropdowns/catálogos completos y ocultar el detalle de `pageSize` a los PageModels. Preferentemente, simplificar su implementación para consumir el `GET /api/v1/unidades-organizativas` no paginado que ya existe; como variante conservadora, mantener la paginación completa interna actual.
2. Mantener `Puestos/Create` sobre ese contrato y agregar una regresión explícita con al menos 201 UO que pruebe que la última opción está disponible.
3. En `Puestos/Edit`, eliminar la consulta UO redundante en vez de reemplazar `200` por otra cifra: el select no se renderiza y la propiedad no interviene en `PuestoSuperiorId`. Si se prioriza una corrección mecánica de riesgo mínimo, reemplazarla por `GetAllActivasAsync`, pero eso conservaría I/O sin valor.
4. Documentar en `docs/decisiones-implementacion.md` que los listados usan `/consulta`, mientras los catálogos completos no aceptan topes desde PageModels y deben usar el contrato de catálogo.
5. No introducir un DTO nuevo salvo que se decida optimizar payload (`Id`, `Codigo`, `Nombre`); `UnidadOrganizativaDto` ya es compartido y suficiente para esta corrección.

La implementación prevista permanece dentro de `single-pr-default` y debería quedar holgadamente por debajo del presupuesto de revisión de 400 líneas.

## Riesgos y dependencias

- La premisa histórica de la issue está parcialmente desactualizada: Create ya no está truncado y Edit no renderiza el dropdown de UO. La propuesta debe corregir el alcance para no diseñar una API duplicada a partir de evidencia vieja.
- El endpoint paginado de UO no limita ni valida `page/pageSize`; no debe usarse con `int.MaxValue` como workaround.
- Un catálogo completo en HTML no escala indefinidamente. Si el volumen esperado supera cientos o pocos miles, C deberá convertirse en un cambio separado.
- El harness web de Puestos falla actualmente al autenticar clientes; bloquea una prueba RED/GREEN confiable sobre PageModels hasta corregir o aislar ese baseline.
- Una prueba MySQL con >200 filas es viable, pero aumenta costo y limpieza de datos. Debe reservarse para validar persistencia/end-to-end, no duplicar una prueba de contrato web.

## Próximos pasos

1. Ejecutar `sdd-propose` con alcance corregido: contrato de catálogo completo, eliminación de la carga redundante de Edit, regresión >200 y documentación; sin refactorizar PageModels grandes.
2. En `sdd-spec`, definir escenarios independientes para: catálogo con 201 activas, ausencia de tope en consumidores y separación entre listado paginado y catálogo.
3. En `sdd-design`, decidir explícitamente si `GetAllActivasAsync` usa el `GET` raíz o conserva paginación completa interna, y registrar por qué no se crea `/all`.
4. Antes de `sdd-apply`, resolver o aceptar formalmente el baseline fallido de autenticación en `PuestoCreatePageTests`/`PuestoEditPageTests`.
