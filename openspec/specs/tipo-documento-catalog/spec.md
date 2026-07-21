# Capability: Tipo de Documento (Catálogo)

> **Status:** NEW — capability does not exist in `openspec/specs/` yet. This delta is the full first version of the capability.
> **Change:** `2026-07-20-147-tipos-documento-catalogo` (issue #147)

## Purpose

Documentar el catálogo read-only e inmutable de tipos de documento que clasifica el `NumeroDocumento` de cada `Persona`. El catálogo es la única fuente de verdad de los cuatro valores seedeados (`DNI`, `LE`, `LC`, `Pasaporte`) y es consumido por la ruta de escritura de Persona para validar el `TipoDocumentoId` foreign key y aplicar `PatronValidacion`/`LongitudMinima`/`LongitudMaxima` sobre `NumeroDocumento`. El catálogo **no** es un recurso CRUD: es de solo lectura en runtime y solo puede evolucionarse con una nueva migración.

## Requirements

### REQ-TD-001 — Inmutabilidad del catálogo.

El catálogo `TipoDocumento` DEBE ser inmutable en runtime. El sistema NO DEBE exponer ningún endpoint de escritura (`POST`, `PUT`, `PATCH`, `DELETE`) sobre la colección `/api/v1/tipos-documento` ni sobre ningún item debajo. El catálogo DEBE ser seedeado exclusivamente por una migración de EF Core que use constantes `Guid` estáticas; cualquier modificación del contenido del catálogo exige una nueva migración.

#### Escenario: Seed crea 4 tipos estáticos

- **DADO** que la tabla `TiposDocumento` está vacía
- **CUANDO** la migración corre contra una base de datos limpia
- **ENTONCES** existen exactamente 4 filas
- **Y** cada fila tiene el `Id`, `Codigo`, `Nombre`, `PatronValidacion`, `LongitudMinima` y `LongitudMaxima` declarados como constantes en la migración y en `DatosSemilla.cs`
- **Y** los 4 códigos son `DNI`, `LE`, `LC`, `Pasaporte`.

#### Escenario: No se exponen endpoints de escritura

- **DADO** que la API está corriendo
- **CUANDO** cualquier cliente intenta `POST`, `PUT`, `PATCH` o `DELETE` sobre `/api/v1/tipos-documento` o `/api/v1/tipos-documento/{id:guid}`
- **ENTONCES** la respuesta es `405 Method Not Allowed` o `404 Not Found`
- **Y** ninguna fila de `TiposDocumento` se inserta, actualiza ni elimina.

### REQ-TD-002 — Forma de la entidad de dominio `TipoDocumento`.

El sistema DEBE permitir construir una entidad de dominio `TipoDocumento` con `Codigo` (string no vacío), `Nombre` (string no vacío), `PatronValidacion` (string regex opcional), `LongitudMinima` (int ≥ 0, opcional) y `LongitudMaxima` (int ≥ `LongitudMinima`, opcional). La entidad DEBE ser EF-agnóstica y DEBE residir en `SGV.Dominio`.

#### Escenario: Creación válida de `TipoDocumento` en dominio

- **DADO** valores `Codigo="DNI"`, `Nombre="Documento Nacional de Identidad"`, `PatronValidacion="^\d{7,8}$"`, `LongitudMinima=7`, `LongitudMaxima=8`
- **CUANDO** se instancia la entidad de dominio `TipoDocumento`
- **ENTONCES** la entidad expone esos valores sin envolverlos en tipos de EF
- **Y** la instancia es usable fuera de la capa de persistencia.

### REQ-TD-003 — Mapeo de persistencia.

La persistencia DEBE mapear `TipoDocumentoEntity` con `Codigo varchar(50) NOT NULL UNIQUE`, `Nombre varchar(100) NOT NULL`, `PatronValidacion varchar(255) NULL`, `LongitudMinima int NULL` y `LongitudMaxima int NULL`. La tabla DEBE llamarse `TiposDocumento` con PK `Id char(36)`. El catálogo NO DEBE tener columnas `IsActive` ni `IsDeleted`.

#### Escenario: Estructura de la tabla

- **DADO** que la migración se ejecutó
- **CUANDO** se consulta `DESCRIBE TiposDocumento`
- **ENTONCES** DEBEN existir las columnas `Id` (char(36) PK), `Codigo` (varchar(50) UNIQUE NOT NULL), `Nombre` (varchar(100) NOT NULL), `PatronValidacion` (varchar(255) NULL), `LongitudMinima` (int NULL), `LongitudMaxima` (int NULL)
- **Y** NO DEBEN existir columnas `IsActive` ni `IsDeleted`.

### REQ-TD-004 — Semilla con 4 filas y códigos canónicos.

`DatosSemilla.HasData` DEBE cargar exactamente 4 filas con códigos `DNI`, `LE`, `LC`, `Pasaporte` referenciando los `Id` estáticos de `TipoDocumentoConstantes`. Un test `[MySqlFact]` DEBE verificar el conteo y los códigos luego de aplicar la migración.

#### Escenario: Semilla presente tras la migración

- **DADO** que la migración corrió sobre una base de datos limpia
- **CUANDO** se consulta `SELECT Codigo FROM TiposDocumento ORDER BY Codigo`
- **ENTONCES** el resultado DEBE ser `DNI`, `LC`, `LE`, `Pasaporte` (4 filas, en ese orden alfabético).

### REQ-TD-005 — Paridad seed ↔ constantes.

Los `Guid` declarados en `TipoDocumentoConstantes` (bloque reservado `71000000-0000-0000-0000-000000000000`, …001 DNI, …002 LE, …003 LC, …004 Pasaporte) DEBEN ser la única fuente de verdad, usados tanto por la migración (`InsertData`) como por `DatosSemilla.HasData`. Un test unitario DEBE afirmar la igualdad sin drift entre ambos orígenes.

#### Escenario: Sin drift entre migración y HasData

- **DADO** que la clase `TipoDocumentoConstantes` define 4 `Guid` en el bloque `71000000-…`
- **CUANDO** se ejecuta el test `DatosSemilla_TipoDocumento_SeedIdsMatchConstantes`
- **ENTONCES** todo `Id` del `InsertData` de la migración está presente en `DatosSemilla.HasData`
- **Y** todo `Id` de `DatosSemilla.HasData` está presente en `InsertData`
- **Y** la cantidad de `Id` distintos en ambas fuentes es idéntica (4).

### REQ-TD-006 — Patrones de validación por tipo.

Cada `TipoDocumento` DEBE declarar un `PatronValidacion` que matchea números válidos del tipo correspondiente y NO matchea números inválidos. Los patrones seedeados son: `DNI` → `^\d{7,8}$`; `LE` y `LC` → `^\d{6,8}$`; `Pasaporte` → `^[A-Za-z]{3}\d{6}$`.

#### Escenario: Patrón DNI acepta 7-8 dígitos y rechaza no dígitos

- **DADO** el `TipoDocumento` con `Codigo="DNI"` y `PatronValidacion="^\d{7,8}$"`
- **CUANDO** se valida `NumeroDocumento="12345678"` contra ese patrón
- **ENTONCES** el match es verdadero
- **Y** al validar `NumeroDocumento="12A45678"` el match es falso.

#### Escenario: Patrón Pasaporte acepta 3 letras + 6 dígitos y rechaza otros formatos

- **DADO** el `TipoDocumento` con `Codigo="Pasaporte"` y `PatronValidacion="^[A-Za-z]{3}\d{6}$"`
- **CUANDO** se valida `NumeroDocumento="ABC123456"` contra ese patrón
- **ENTONCES** el match es verdadero
- **Y** al validar `NumeroDocumento="AB1234567"` el match es falso.

### REQ-TD-007 — Catálogo read-only requiere autenticación.

`TipoDocumentosController` DEBE requerir autenticación para sus endpoints de lectura. `GET /api/v1/tipos-documento` y `GET /api/v1/tipos-documento/{id:guid}` DEBEN responder `2xx` únicamente para usuarios autenticados y DEBEN conservar el contrato de respuesta vigente (`id`, `codigo`, `nombre`, `patronValidacion`, `longitudMinima`, `longitudMaxima`). Los endpoints de escritura (`POST`, `PUT`, `PATCH`, `DELETE`) sobre `TiposDocumento` NO DEBEN estar expuestos; cualquier intento de escritura DEBE responder `405 Method Not Allowed` o no estar disponible como acción documentada, independientemente del estado de autenticación del cliente.

#### Escenario: Acceso anónimo rechazado

- **DADO** un cliente sin credenciales
- **CUANDO** solicita `GET /api/v1/tipos-documento` o `GET /api/v1/tipos-documento/{id:guid}`
- **ENTONCES** la API DEBE responder `401 Unauthorized`.

#### Escenario: Lectura autenticada exitosa

- **DADO** un usuario autenticado
- **CUANDO** solicita `GET /api/v1/tipos-documento`
- **ENTONCES** la API DEBE responder `200 OK` con un array JSON de 4 elementos
- **Y** cada elemento contiene `id`, `codigo`, `nombre`, `patronValidacion` (cuando aplique) y longitudes.