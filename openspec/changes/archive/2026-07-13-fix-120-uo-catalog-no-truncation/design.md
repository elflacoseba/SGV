# Diseño: Eliminar catálogos sin consumidor en Edit de Puestos (#120)

## Enfoque técnico

`SGV.Web` eliminará de `Puestos/Edit` las cargas de unidades organizativas y cargos porque `_Form.cshtml` no renderiza esos campos cuando `IsEdit == true`. `LoadCatalogsAsync` conservará únicamente la carga de puestos para `PuestoSuperiorOptions`, requerida por el select visible. No cambian API, contratos wire, persistencia ni Create.

## Decisiones de arquitectura

| Decisión | Alternativas y trade-off | Elección y fundamento |
|---|---|---|
| Eliminar la carga UO/Cargo | Reemplazar `200` por `GetAllActivasAsync` evitaría truncamiento, pero mantendría dos round-trips sin consumidor. | Eliminar `unidadesTask`, `cargosTask` y sus ramas. Los campos son inmutables y el partial oculta ambos selects en Edit. |
| Reducir dependencias del PageModel | Mantener clientes facilita algunos tests, pero deja dependencias muertas. | Quitar `IUnidadOrganizativaApiClient` e `ICargoApiClient` del constructor: el código actual confirma que solo se usan en `LoadCatalogsAsync`. Las registraciones DI globales no cambian. |
| Conservar propiedades vacías | Quitar las propiedades rompería `IPuestoForm`, compartido con `_Form.cshtml`. | Mantener `UnidadOrganizativaOptions` y `CargoOptions` con inicializador `[]`; Edit no las asignará. |
| Preservar carga tolerante a fallos de superiores | Un `await` directo simplificaría más, pero cambiaría el patrón de consolidación de errores. | Mantener `LaunchSafeAsync`, `Task.WhenAll(puestosTask)`, chequeo de `TaskStatus` y mensaje recuperable. |
| Proteger la regresión en PageModel | Una prueba HTML sola no detecta round-trips ocultos. | Verificar contadores de fakes para UO/Cargo y carga positiva de puestos superiores. |

## Flujo de datos

```text
GET /organizacion/puestos/editar/{id}
    ├── IPuestosApiClient.GetByIdAsync ──> Input editable
    └── LoadCatalogsAsync
          └── IPuestosApiClient.GetAllAsync ──> PuestoSuperiorOptions

UnidadOrganizativaOptions = []   CargoOptions = []
(no llamadas HTTP; no hay selects downstream en Edit)
```

## Cambios de archivos

| Archivo | Acción | Descripción |
|---|---|---|
| `src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml.cs` | Modificar | Quitar ambos clientes del constructor, tareas UO/Cargo, entradas de `WhenAll` y bloques de estado; conservar `puestosTask`; actualizar el XML-doc para describir un único catálogo. |
| `tests/SGV.Tests/Web/Puesto/PuestoEditPageTests.cs` | Modificar | Agregar tres regresiones: cero llamadas UO, cero llamadas Cargo y una llamada con opciones no vacías para puestos superiores. |
| `docs/decisiones-implementacion.md` | Modificar | Documentar “catálogo completo” (`GetAllActivasAsync`, solo formularios Create) versus “listado paginado” (`QueryAsync`, Index/reportes), y prohibir catálogos en Edit mientras los campos sean inmutables. |

Estimación: `Edit.cshtml.cs` ~-15 líneas, documentación ~+25 y tests ~+80; total ~90 líneas modificadas, riesgo bajo frente al presupuesto de revisión de 400.

## Interfaces y contratos

No se crean ni modifican contratos públicos. `EditModel` reduce su constructor primario a `IPuestosApiClient` e `ILogger<EditModel>`. `IPuestoForm` sigue exponiendo las tres colecciones; en Edit, UO/Cargo cumplen el contrato con listas vacías. `PuestoSuperiorOptions` continúa mapeando cada `PuestoDto` mediante `PuestoFormHelpers.MapToSuperiorViewModel`.

## Estrategia de pruebas (TDD estricto)

| Fase | Prueba / acción | Evidencia |
|---|---|---|
| RED | Agregar `Edit_GET_NoInvocaCatalogoUnidadesOrganizativas`, `Edit_GET_NoInvocaCatalogoCargos` y `Edit_GET_CargaPuestosSuperiores` usando fakes explícitos pasados a `CreatePuestoLeaseAsync`. | El código actual registra llamadas UO/Cargo; el caso positivo controla que no se elimine la carga necesaria. |
| GREEN | Refactorizar constructor y `LoadCatalogsAsync`. | `QueryCalls`, `GetAllActivasCalls` y `FakeCargoApiClient.GetAllCalls` quedan en cero; `FakePuestosApiClient.GetAllCalls` vale uno y las opciones no están vacías. |
| REFACTOR | Simplificar XML-doc/comentarios obsoletos sin alterar comportamiento. | Build y suite focalizada siguen verdes. |

Primero se validará el baseline de `PuestoEditPageTests`. Si persiste la redirección preexistente a sign-in, los casos se aislarán invocando `EditModel.OnGetAsync` directamente con principal Administrador, `FakePuestosApiClient` y logger; la ausencia de dependencias UO/Cargo quedará además garantizada por la firma del constructor. Al tocar tests, apply deberá ejecutar la suite focalizada y luego las tres corridas `dotnet test SGV.slnx --no-build` exigidas por el repositorio.

## Migración y despliegue

No requiere migración, feature flag ni rollout por fases. El rollback es revertir los tres archivos.

## Riesgos

- **Medio — baseline de autenticación web:** validar antes del RED; aislar PageModel si continúa fallando.
- **Bajo — futura reintroducción de selects:** los tests de ausencia de llamadas y la documentación obligan a elegir el contrato de catálogo correcto.
- **Bajo — reducción del constructor:** compilación y activación Razor detectarán cualquier consumidor incompatible.

## Preguntas abiertas

Ninguna.
