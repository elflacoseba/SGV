# T-01-02 — Hacer tu primera mutación end-to-end

**Qué vas a lograr:** crear una unidad organizativa desde la pantalla Create
del shell web y confirmar el ciclo completo: UI → API → EF Core → tabla
`UnidadesOrganizativas` → interceptor de auditoría → fila en `Auditorias` →
re-render del listado.

---

## Prerrequisitos

- Haber completado **T-01-01** (sistema levantado, primer admin creado, sesión
  iniciada).
- Sesión activa con un usuario con rol `Administrador` (las operaciones write
  de unidades organizativas están protegidas por
  `[Authorize(Roles = RolesSgv.Administrador)]`, ver
  `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs`).

---

## Paso 1 — Ir a la pantalla de creación

En el navegador, abrí <http://localhost:5266/organizacion/unidades-organizativas>
(Listado) y hacé clic en **Crear** arriba a la derecha. También podés ir
directamente a `/organizacion/unidades-organizativas/crear`.

**Verificación:** la página muestra el formulario con dos selects: **Tipo**
(cargado desde `/api/v1/tipos-unidad-organizativa`) y **Unidad padre**
(árbol jerárquico cargado desde `/api/v1/unidades-organizativas/arbol`). Si
los selects están vacíos, la API no responde y verás el banner "No se pudieron
cargar los catálogos".

---

## Paso 2 — Completar el formulario

Datos mínimos obligatorios:

| Campo | Valor sugerido |
|-------|----------------|
| Código | `GER-001` (≤ 50 chars, único entre activas) |
| Nombre | `Gerencia General` |
| Tipo | `Dirección` |
| Descripción | (opcional) |
| Unidad padre | (sin selección, queda como raíz) |
| Vigente desde / hasta | (opcional, informativo) |

Hacé clic en **Guardar**.

**Verificación:** la página redirige a `/organizacion/unidades-organizativas/details/{id}`
y muestra los datos recién creados. La URL lleva un fragmento TempData que en
el listado siguiente se rendereará como banner verde de éxito.

---

## Paso 3 — Verificar el código HTTP 201 Created

Abrid las **DevTools del navegador** → pestaña **Network** y filtrá por
`Fetch/XHR`. Repetí el alta (con otro código, p. ej. `GER-002`). Vas a ver
la request `POST https://localhost:7160/api/v1/unidades-organizativas` con
status `201 Created` y un body JSON con la nueva unidad y su `Id`.

**Verificación:** el response lleva `Location: /api/v1/unidades-organizativas/{id}`
(header estándar de `CreatedAtAction` en el controller). Si ves `400`,
revisá que el `Codigo` no esté duplicado entre unidades activas.

---

## Paso 4 — Confirmar en el listado

Volvé a <http://localhost:5266/organizacion/unidades-organizativas>.

**Verificación:** ves tu nueva unidad en la tabla con su **Código**, **Nombre**,
**Tipo** y la **vigencia** actual. El contador arriba a la derecha
muestra `N registro(s)` incrementado. La columna Vigencia muestra el rango
informativo; la badge de clase sigue el helper `VigenciaViewModel.Desde` (ver
`Index.cshtml.cs`).

---

## Paso 5 — Leer la fila de auditoría

Abrid <http://localhost:5266/auditorias>. El listado consume
`GET /api/v1/auditorias` y está protegido por
`[Authorize(Roles = RolesSgv.Administrador)]` (ver
`src/SGV.Api/Controllers/AuditoriasController.cs`).

**Verificación:** la fila de tu unidad aparece con:

| Columna | Valor esperado |
|---------|----------------|
| Entidad | `UnidadOrganizativa` |
| Operación | `Alta` o `Modificacion` (según la pantalla abierta al insertar) |
| Fecha | timestamp UTC del `SaveChangesAsync` |
| Usuario | tu nombre de usuario logueado |
| Correlación | GUID del request |

Las filas las escribe el interceptor
`src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs`
durante `SaveChangesAsync`. Cada `Added`/`Modified`/`Deleted` de un
`EntityBase` produce una entrada en `Auditorias` con los snapshots JSON de
valores anteriores y nuevos.

Hacé clic en **Ver detalle** de tu fila para confirmar que están los
`OldValuesJson` y `NewValuesJson` correctos.

---

## Paso 6 — (Opcional) Probar el camino de error

Repetí el alta con el mismo `Codigo` (`GER-001`) que ya usaste.

**Verificación:** la página renderiza un banner rojo con el mensaje
"Ya existe una unidad organizativa activa con el mismo código." El status
HTTP de la API fue `409 Conflict`, mapeado por `ErrorCategoryMapper` y
`UnidadOrganizativaFormHelpers.ApplyFieldErrorsToModelState`.

---

## Resumen del flujo

```
SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Create.cshtml.cs
   └─ POST /api/v1/unidades-organizativas      (IUnidadOrganizativaApiClient)
        └─ SGV.Api/Controllers/UnidadesOrganizativasController.Create
             └─ SGV.Aplicacion/UnidadOrganizativaServicioComandos.CrearAsync
                  ├─ FluentValidation (CrearUnidadOrganizativaRequestValidator)
                  ├─ repository.ExistsActiveCodeAsync  → conflict 409 si true
                  ├─ new UnidadOrganizativa(...)        (dominio, reglas)
                  ├─ repository.AddAsync + unitOfWork.SaveChangesAsync
                  │     └─ AuditoriaSaveChangesInterceptor → fila en Auditorias
                  └─ return UnidadOrganizativaDto
        └─ 201 Created + Location header
   └─ PRG → /organizacion/unidades-organizativas/details/{id}
```

---

## Próximos pasos

- **T-01-03** — Correr la suite de tests completa y entender el skip de
  `[MySqlFact]`.
- [R-03-03](../reference/03-wire-types-contracts.md) — Referencia del wire
  contract `CrearUnidadOrganizativaRequest` y todos los DTOs del módulo
  Organizacion.
- [R-03-10](../reference/10-taxonomia-errores.md) — Taxonomía de errores
  HTTP y códigos de dominio.
- [E-04-03](../explanation/03-auditoria-transversal-savechanges-interceptor.md) —
  Explanation del flujo de auditoría centralizada: interceptor, tabla,
  soft-delete e `IsDeleted` en columnas generadas únicas.
