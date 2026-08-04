# auditoria-drilldown-username-filter Specification

## Purpose

Garantizar que el filtro `userName` aplicado en el listado de auditoría (`Pages/Auditorias/Index`) se preserve al hacer drill-down al detalle (`Pages/Auditorias/Details`) y al retornar al listado mediante el back-link. Cierra el bug #253 donde Index enviaba `userName` pero Details bindeaba `userId`, provocando la pérdida del filtro de usuario en la navegación de round-trip.

## Requirements

### Requirement: Details bindea `userName` desde el query string

`DetailsModel.OnGetAsync` MUST bindear el parámetro opcional `userName` desde el query string con el nombre que `IndexModel.BuildDetailsRouteValues` envía en el enlace de drill-down. La propiedad expuesta por la PageModel MUST llamarse `UserName`. El parámetro es opcional: su ausencia MUST NOT causar error de carga ni alterar el comportamiento del detalle.

#### Scenario: Drill-down desde Index con filtro userName activo

- GIVEN el listado de auditoría filtrado por `userName=jperez`
- WHEN el administrador sigue el enlace de detalle de una fila
- THEN `DetailsModel` recibe `userName=jperez` en el query string y lo bindea a la propiedad `UserName`

#### Scenario: Navegación directa a Details sin userName

- GIVEN un administrador que accede directamente a `/auditorias/details?id={guid}` sin `userName`
- WHEN `DetailsModel.OnGetAsync` se ejecuta
- THEN la página carga sin errores y `UserName` es `null`

### Requirement: Back-link preserva el filtro `userName`

`DetailsModel.BuildBackUrl()` MUST incluir `userName` con el valor de la propiedad `UserName` en la URL de retorno al listado. La clave del route value MUST ser `userName`, coincidiendo con el binding de `IndexModel.OnGetAsync`.

#### Scenario: Back-link incluye userName cuando el filtro estaba activo

- GIVEN `DetailsModel` con `UserName = "jperez"` recibido desde el drill-down
- WHEN se construye el back-link con `BuildBackUrl()`
- THEN la URL de retorno contiene `userName=jperez`

#### Scenario: Back-link sin userName cuando no había filtro

- GIVEN `DetailsModel` con `UserName = null` (navegación directa sin filtro)
- WHEN se construye el back-link con `BuildBackUrl()`
- THEN la URL de retorno NO transporta el filtro `userName` (o lo envía vacío, que `IndexModel` normaliza a `null`)

### Requirement: Test de regresión del round-trip `userName`

La suite de tests MUST incluir al menos un test que verifique el round-trip del filtro `userName` entre Index y Details: el enlace de drill-down transporta `userName`, Details lo bindea, y el back-link lo conserva para que el listado lo reciba de vuelta.

#### Scenario: Round-trip completo del filtro userName

- GIVEN `IndexModel` con `UserName = "jperez"` genera el enlace de detalle vía `BuildDetailsRouteValues(id)`
- WHEN `DetailsModel.OnGetAsync` recibe ese query string y construye el back-link con `BuildBackUrl()`
- THEN el back-link contiene `userName=jperez`

#### Scenario: Round-trip sin filtro no introduce userName espurio

- GIVEN `IndexModel` con `UserName = null` genera el enlace de detalle
- WHEN `DetailsModel.OnGetAsync` recibe el query string y construye el back-link
- THEN el back-link NO contiene un `userName` con valor espurio