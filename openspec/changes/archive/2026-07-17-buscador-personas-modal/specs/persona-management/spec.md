# Delta for `persona-management`

Este delta extiende el endpoint `GET /api/v1/personas/consulta` para aceptar el flag `soloSinUsuario` sin alterar el comportamiento vigente para los consumidores existentes (`/personas`, Index/Details, typeahead). NO crea ni elimina requisitos previos fuera del bloque copiado abajo.

## ADDED Requirements

### Requirement: REQ-PM-01 Ortogonalidad entre `soloSinUsuario` y `Segmento`

El parámetro `soloSinUsuario=true` MUST ser ortogonal al `Segmento` sólo en el sentido de que filtra personas activas sin usuario. Combinado con `Segmento=Eliminadas`, el endpoint MUST responder `200` con `items=[]` y `totalCount=0` sin invocar joins. Combinado con `Segmento=Activas` (default), el endpoint MUST aplicar el filtro anti-join. El parámetro ausente, `false` o `null` MUST restaurar el comportamiento previo (sin filtro adicional).

#### Scenario: soloSinUsuario=true con Activas aplica filtro anti-join

- **DADO** personas activas con y sin usuario activo asociado
- **CUANDO** se solicita `/consulta?status=activas&soloSinUsuario=true&p=1&pageSize=25`
- **ENTONCES** el `items` MUST contener únicamente las personas activas cuyo `AspNetUsers.PersonaId IS NULL`
- **Y** `totalCount` MUST reflejar el conteo post-filtro.

#### Scenario: soloSinUsuario=true con Eliminadas responde vacío

- **DADO** personas eliminadas en el segmento `Eliminadas`
- **CUANDO** se solicita `/consulta?status=eliminadas&soloSinUsuario=true&p=1&pageSize=25`
- **ENTONCES** MUST responder `200` con `items=[]` y `totalCount=0`
- **Y** MUST NOT aplicar el anti-join (cortocircuito por segmento).

#### Scenario: soloSinUsuario ausente preserva back-compat

- **DADO** personas activas y con usuario activo
- **CUANDO** se solicita `/consulta` sin el parámetro `soloSinUsuario`
- **ENTONCES** MUST devolver todas las activas sin aplicar el filtro anti-join
- **Y** MUST preservar el contrato previo de `PagedResult<PersonaDto>`.

#### Scenario: soloSinUsuario combinado con search sort y paginación

- **DADO** personas activas con y sin usuario
- **CUANDO** se solicita `/consulta?search=garcia&sort=apellidos_asc&p=2&pageSize=25&soloSinUsuario=true`
- **ENTONCES** el filtro `soloSinUsuario`, el `search`, el `sort` y la paginación 1-based MUST componerse en ese orden antes del `Skip/Take`
- **Y** `totalCount` MUST ser el conteo post-filtro `search` + `soloSinUsuario`.

## MODIFIED Requirements

### Requisito: Listado segmentado y paginado de Personas (endpoint `/consulta`)

`GET /api/v1/personas/consulta?p=&pageSize=&search=&sort=&status=activas|eliminadas&soloSinUsuario=true|false` MUST estar disponible para cualquier autenticado. Búsqueda aplica a `Legajo|Nombres|Apellidos|Email|NumeroDocumento`. `status` omitido o desconocido MUST caer a `activas`. `soloSinUsuario` ausente, `false` o `null` MUST preservar el comportamiento vigente (sin filtro adicional). Cuando `soloSinUsuario=true`, el endpoint MUST retornar sólo personas activas sin usuario activo asociado (anti-join sobre `AspNetUsers.PersonaId`); la combinación con `Segmento=Eliminadas` MUST devolver `items=[]` y `totalCount=0`. Respuesta MUST ser `PagedResult<PersonaDto>`.
(Previously: el endpoint no aceptaba `soloSinUsuario`; el comportamiento de filtrado por segmento era exclusivo y no se discriminaba contra `AspNetUsers.PersonaId`.)

#### Escenario: Listar personas con paginación, búsqueda y orden server-side

- **DADO** personas activas persistidas
- **CUANDO** se solicita `/consulta?search=garcia&sort=apellidos_asc&p=1`
- **ENTONCES** responde `200` con `PagedResult<PersonaDto>` paginado, excluye inactivas y aplica filtro + orden antes de `Skip/Take`.

#### Escenario: Filtrar soloSinUsuario=true devuelve solo activas sin usuario

- **DADO** personas activas con y sin usuario activo asociado
- **CUANDO** se solicita `/consulta?soloSinUsuario=true&p=1&pageSize=25`
- **ENTONCES** el `items` MUST contener únicamente las personas activas sin usuario
- **Y** `totalCount` MUST coincidir con el total filtrado.

#### Escenario: soloSinUsuario es ortogonal al search y paginación vigentes

- **DADO** 30 personas activas con y sin usuario, conteniendo `garcía` en algunos nombres
- **CUANDO** se solicita `/consulta?search=garcia&soloSinUsuario=true&p=1&pageSize=5`
- **ENTONCES** MUST responder con `PagedResult<PersonaDto>` cuya `page=1`, `pageSize=5`, sin paginar personas con usuario
- **Y** el orden server-side por defecto MUST mantenerse intacto.
