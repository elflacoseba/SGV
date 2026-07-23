# Especificación del Catálogo de Categorías de Habilidad

## Propósito

Catálogo de solo lectura e inmutable que clasifica las `Habilidades` registradas en el sistema. Es la única fuente de verdad del conjunto de categorías (`Conducción`, `Técnica`, `Dominio`, `Académica`) y se consume desde los formularios web de Habilidad, Cargo y Persona para poblar un dropdown, y desde el servicio de aplicación de Habilidad para validar la FK referenciada. No es un recurso CRUD: solo lectura en runtime y solo evoluciona con una nueva migración.

## Requisitos

### Requirement: Inmutabilidad del catálogo (REQ-CHC-001)

El sistema MUST NOT exponer endpoints HTTP de escritura (`POST`, `PUT`, `PATCH`, `DELETE`) sobre `/api/v1/categorias-habilidad` ni sobre `/api/v1/categorias-habilidad/{id:guid}`. El catálogo MUST ser sembrado exclusivamente por una migración de EF Core con constantes `Guid` estáticas; cualquier modificación de contenido exige una nueva migración.

#### Scenario: No se exponen endpoints de escritura

- **DADO** que la API está corriendo
- **WHEN** un cliente intenta `POST`, `PUT`, `PATCH` o `DELETE` sobre `/api/v1/categorias-habilidad` o `/api/v1/categorias-habilidad/{id:guid}`
- **ENTONCES** la respuesta es `405 Method Not Allowed` o `404 Not Found`
- **Y** ninguna fila de `CategoriasHabilidad` se inserta, actualiza ni elimina.

### Requirement: Endpoints read-only autenticados (REQ-CHC-002)

`CategoriasHabilidadController` MUST exponer `GET /api/v1/categorias-habilidad` (catálogo completo ordenado por `Nombre` ascendente) y `GET /api/v1/categorias-habilidad/{id:guid}` (item por identificador). Ambos endpoints MUST requerir autenticación: responder `401 Unauthorized` para clientes anónimos y `200 OK` para usuarios autenticados, conservando el contrato `{id, codigo, nombre}` consumer-safe.

#### Scenario: Listado autenticado devuelve 4 categorías semilla

- **DADO** el catálogo sembrado con 4 filas (`Conducción`, `Técnica`, `Dominio`, `Académica`)
- **WHEN** un usuario autenticado solicita `GET /api/v1/categorias-habilidad`
- **ENTONCES** la API MUST responder `200 OK` con un array JSON de 4 elementos ordenados alfabéticamente por `Nombre`
- **Y** cada elemento MUST exponer `id`, `codigo` y `nombre`.

#### Scenario: Acceso anónimo rechazado

- **DADO** un cliente sin credenciales
- **WHEN** solicita `GET /api/v1/categorias-habilidad` o `GET /api/v1/categorias-habilidad/{id:guid}`
- **ENTONCES** la API MUST responder `401 Unauthorized`.

#### Scenario: GET por id de categoría existente

- **DADO** una categoría persistida con `Id = <guid>`
- **WHEN** un usuario autenticado la solicita
- **ENTONCES** la API MUST responder `200 OK` con `id`, `codigo` y `nombre`.

#### Scenario: GET por id inexistente responde 404

- **DADO** un identificador que no corresponde a una categoría persistida
- **WHEN** un usuario autenticado lo solicita
- **ENTONCES** la API MUST responder `404 Not Found`.

### Requirement: Forma de tabla y seed (REQ-CHC-003)

La persistencia MUST mapear `CategoriasHabilidad` con PK `Id` `char(36)` GUID, `Codigo` `varchar(50)` `UNIQUE NOT NULL`, `Nombre` `varchar(100)` `NOT NULL`. La tabla MUST NOT tener columnas `IsActive` ni `IsDeleted`. La siembra MUST provenir del bloque GUID `72000000-…` reservado para `CategoriaHabilidad` y MUST contener exactamente 4 filas: `Conduccion`, `Tecnica`, `Dominio`, `Academica`.

#### Scenario: Estructura de la tabla post-migración

- **DADO** que la migración se ejecutó
- **WHEN** se consulta `DESCRIBE CategoriasHabilidad`
- **ENTONCES** DEBEN existir `Id` (char(36) PK), `Codigo` (varchar(50) UNIQUE NOT NULL) y `Nombre` (varchar(100) NOT NULL)
- **Y** NO DEBEN existir columnas `IsActive` ni `IsDeleted`.

#### Scenario: Seed crea exactamente 4 filas canónicas

- **DADO** que la tabla está vacía
- **WHEN** la migración corre contra una base de datos limpia
- **ENTONCES** existen exactamente 4 filas
- **Y** los 4 códigos seed son `Conduccion`, `Tecnica`, `Dominio` y `Academica`.

### Requirement: Paridad migración ↔ DatosSemilla (REQ-CHC-004)

Las constantes `Guid` declaradas en `CategoriaHabilidadConstantes` MUST ser la única fuente de verdad, usadas por la migración (`InsertData`) y por `DatosSemilla.HasData`. Un test unitario MUST afirmar la paridad sin drift entre ambos orígenes (igualdad de `Id` y de cardinalidad).

#### Scenario: Sin drift entre migración y `HasData`

- **DADO** que la clase `CategoriaHabilidadConstantes` define 4 GUID en `72000000-…` (`…000`, `…001`, `…002`, `…003`)
- **WHEN** se ejecuta el test `DatosSemilla_CategoriaHabilidad_SeedIdsMatchConstantes`
- **ENTONCES** todo `Id` del `InsertData` de la migración está presente en `DatosSemilla.HasData`
- **Y** todo `Id` de `DatosSemilla.HasData` está presente en `InsertData`
- **Y** la cantidad de `Id` distintos en ambas fuentes es idéntica (4).

### Requirement: Reserva del bloque GUID 72000000-… (REQ-CAT-08)

El bloque GUID `72000000-0000-0000-0000-000000000000` … `72000000-0000-0000-0000-00000000000F` MUST estar reservado exclusivamente para `CategoriaHabilidad` y MUST estar registrado en `docs/decisiones-implementacion.md` § "Mapa de bloques GUID reservados por catálogo" con la asignación de las primeras 4 posiciones a `Conduccion`, `Tecnica`, `Dominio` y `Academica`.

#### Scenario: Bloque registrado en decisiones de implementación

- **DADO** la versión actual de `docs/decisiones-implementacion.md`
- **WHEN** se inspecciona la sección "Mapa de bloques GUID reservados por catálogo"
- **ENTONCES** DEBE existir una entrada `72000000-…` etiquetada como `CategoriaHabilidad` con sus 4 posiciones asignadas.
