# R-03-08 — Catálogos inmutables y bloques GUID

Referencia de los catálogos sembrados y los bloques GUID reservados. La convención documentada en `docs/decisiones-implementacion.md` reserva bloques contiguos para cada catálogo inmutable; cualquier catálogo nuevo debe pedir un bloque contiguo y actualizar este mapa.

## Mapa de bloques GUID

| Bloque (Guid) | Catálogo | Estado | Origen |
| --- | --- | --- | --- |
| `10000000-0000-0000-0000-000000000000` … `0000000F` | `NivelHabilidad` | Sembrado (4 filas) | `DatosSemilla.NivelBasicoId…NivelExpertoId` |
| `20000000-0000-0000-0000-000000000000` … `0000000F` | `EstadoVacante` | Sembrado (4 filas) | `EstadoVacanteConstantes` |
| `30000000-0000-0000-0000-000000000000` … `0000000F` | `EstadoPostulacion` | Sembrado (6 filas) | `DatosSemilla.Postulacion*Id` |
| `40000000-0000-0000-0000-000000000000` … `0000000F` | `Cargo` (seed demo) | Sembrado (6 filas) | `DatosSemilla.Cargo*Id` |
| `50000000-0000-0000-0000-000000000000` … `0000000F` | `Habilidad` (seed demo) | Sembrado (7 filas) | `DatosSemilla.Habilidad*Id` |
| `60000000-0000-0000-0000-000000000000` … `00000014` | `TipoUnidadOrganizativa` | Sembrado (20 filas) | `TipoUnidadOrganizativaConstantes` |
| `70000000-0000-0000-0000-000000000000` … `0000000F` | `NivelCargo` | Sembrado (4 filas) | `NivelCargoConstantes` |
| `71000000-0000-0000-0000-000000000000` … `0000000F` | `TipoDocumento` | Sembrado (4 filas) | `TipoDocumentoConstantes` |
| `72000000-0000-0000-0000-000000000000` … `0000000F` | `CategoriaHabilidad` | Sembrado (4 filas) | `CategoriaHabilidadConstantes` |

> Las filas de demo (bloques `40000000-…` y `50000000-…`) coexisten con los datos de usuario; un script de cleanup vive en los tests de Infraestructura.

## Catálogo `NivelHabilidad` (bloque `10000000-…`)

Tabla `NivelesHabilidad`. `NivelHabilidadEntity : EntityBase`. Sembrado vía `DatosSemilla.Configurar`.

| Id | Codigo | Nombre | ValorNumerico | Orden |
| --- | --- | --- | --- | --- |
| `10000000-0000-0000-0000-000000000001` | `Basico` | Básico | 1 | 1 |
| `10000000-0000-0000-0000-000000000002` | `Intermedio` | Intermedio | 2 | 2 |
| `10000000-0000-0000-0000-000000000003` | `Avanzado` | Avanzado | 3 | 3 |
| `10000000-0000-0000-0000-000000000004` | `Experto` | Experto | 4 | 4 |

Uso: nivel de competencia de una `PersonaHabilidad` o nivel requerido de una `CargoHabilidad`.

## Catálogo `EstadoVacante` (bloque `20000000-…`)

Tabla `EstadosVacante`. `EstadoVacanteEntity : EntityBase` (con flags adicionales `EsCubierta`, `EsCancelada`).

| Id | Codigo | Nombre | Orden | EsTerminal | Flags extra |
| --- | --- | --- | --- | --- | --- |
| `20000000-0000-0000-0000-000000000001` | `Abierta` | Abierta | 1 | false | — |
| `20000000-0000-0000-0000-000000000002` | `EnSeleccion` | En Selección | 2 | false | — |
| `20000000-0000-0000-0000-000000000003` | `Cubierta` | Cubierta | 3 | true | `EsCubierta=true` |
| `20000000-0000-0000-0000-000000000004` | `Cancelada` | Cancelada | 4 | true | `EsCancelada=true` |

Código string equivalente en `SGV.Contracts.Vacantes.Catalogos.EstadoVacanteCodigos`. Sólo lectura vía `GET /api/v1/estados-vacante`.

## Catálogo `EstadoPostulacion` (bloque `30000000-…`)

Tabla `EstadosPostulacion`. `EstadoPostulacionEntity : EntityBase`.

| Id | Codigo | Nombre | Orden | EsTerminal | EsTerminalPositivo |
| --- | --- | --- | --- | --- | --- |
| `30000000-0000-0000-0000-000000000001` | `Postulado` | Postulado | 1 | false | false |
| `30000000-0000-0000-0000-000000000002` | `Preseleccionado` | Preseleccionado | 2 | false | false |
| `30000000-0000-0000-0000-000000000003` | `Entrevistado` | Entrevistado | 3 | false | false |
| `30000000-0000-0000-0000-000000000004` | `Aprobado` | Aprobado | 4 | false | false |
| `30000000-0000-0000-0000-000000000005` | `Rechazado` | Rechazado | 5 | true | false |
| `30000000-0000-0000-0000-000000000006` | `Contratado` | Contratado | 6 | true | true |

Sin endpoint HTTP específico; consumido vía navegación de la vacante/postulación.

## Catálogo `NivelCargo` (bloque `70000000-…`)

Tabla `NivelesCargo`. `NivelCargoEntity : EntityBase`. Sembrado vía `NivelCargoConstantes.Semilla`.

| Id | Codigo | Nombre | ValorNumerico | Orden |
| --- | --- | --- | --- | --- |
| `70000000-0000-0000-0000-000000000001` | `Directivo` | Directivo | 1 | 1 |
| `70000000-0000-0000-0000-000000000002` | `ConduccionMedia` | Conducción Media | 2 | 2 |
| `70000000-0000-0000-0000-000000000003` | `Operativo` | Operativo | 3 | 3 |
| `70000000-0000-0000-0000-000000000004` | `Academico` | Académico | 4 | 4 |

Sólo lectura vía `GET /api/v1/niveles-cargo`. FK desde `Cargos.NivelId`.

## Catálogo `TipoDocumento` (bloque `71000000-…`)

Tabla `TiposDocumento`. `TipoDocumentoEntity : EntityBase`. Sembrado vía `TipoDocumentoConstantes.Semilla`.

| Id | Codigo | Nombre | PatronValidacion | Longitud |
| --- | --- | --- | --- | --- |
| `71000000-0000-0000-0000-000000000001` | `DNI` | Documento Nacional de Identidad | `^\d{7,8}$` | 7–8 |
| `71000000-0000-0000-0000-000000000002` | `LE` | Libreta de Enrolamiento | `^\d{6,8}$` | 6–8 |
| `71000000-0000-0000-0000-000000000003` | `LC` | Libreta Cívica | `^\d{6,8}$` | 6–8 |
| `71000000-0000-0000-0000-000000000004` | `Pasaporte` | Pasaporte | `^[A-Za-z]{3}\d{6}$` | 9 |

`GET /api/v1/tipos-documento` es `AllowAnonymous` para soportar el dropdown del setup inicial.

## Catálogo `CategoriaHabilidad` (bloque `72000000-…`)

Tabla `CategoriasHabilidad`. `CategoriaHabilidadEntity : EntityBase`. Sembrado vía `CategoriaHabilidadConstantes.Semilla`.

| Id | Codigo | Nombre |
| --- | --- | --- |
| `72000000-0000-0000-0000-000000000000` | `Conduccion` | Conducción |
| `72000000-0000-0000-0000-000000000001` | `Tecnica` | Técnica |
| `72000000-0000-0000-0000-000000000002` | `Dominio` | Dominio |
| `72000000-0000-0000-0000-000000000003` | `Academica` | Académica |

Sólo lectura vía `GET /api/v1/categorias-habilidad`. FK desde `Habilidades.CategoriaId`.

## Catálogo `TipoUnidadOrganizativa` (bloque `60000000-…`)

Tabla `TiposUnidadOrganizativa`. `TipoUnidadOrganizativaEntity : EntityBase`. Sembrado vía `TipoUnidadOrganizativaConstantes`.

| Id | Codigo | Nombre |
| --- | --- | --- |
| `60000000-0000-0000-0000-000000000001` | `Institucion` | Institución |
| `60000000-0000-0000-0000-000000000002` | `Facultad` | Facultad |
| `60000000-0000-0000-0000-000000000003` | `Secretaria` | Secretaría |
| `60000000-0000-0000-0000-000000000004` | `Direccion` | Dirección |
| `60000000-0000-0000-0000-000000000005` | `Departamento` | Departamento |
| `60000000-0000-0000-0000-000000000006` | `Division` | División |
| `60000000-0000-0000-0000-000000000007` | `Area` | Área |
| `60000000-0000-0000-0000-000000000008` | `Sede` | Sede |
| `60000000-0000-0000-0000-000000000009` | `Region` | Región |
| `60000000-0000-0000-0000-00000000000a` | `Gerencia` | Gerencia |
| `60000000-0000-0000-0000-00000000000b` | `Vicepresidencia` | Vicepresidencia |
| `60000000-0000-0000-0000-00000000000c` | `Subgerencia` | Subgerencia |
| `60000000-0000-0000-0000-00000000000d` | `Coordinacion` | Coordinación |
| `60000000-0000-0000-0000-00000000000e` | `Seccion` | Sección |
| `60000000-0000-0000-0000-00000000000f` | `Oficina` | Oficina |
| `60000000-0000-0000-0000-000000000010` | `Equipo` | Equipo |
| `60000000-0000-0000-0000-000000000011` | `Celula` | Célula |
| `60000000-0000-0000-0000-000000000012` | `Planta` | Planta |
| `60000000-0000-0000-0000-000000000013` | `Sucursal` | Sucursal |
| `60000000-0000-0000-0000-000000000014` | `Escuela` | Escuela |

Sólo lectura vía `GET /api/v1/tipos-unidad-organizativa`. FK desde `UnidadesOrganizativas.TipoUnidadOrganizativaId`.

## Seeds demo (bloques `40000000-…` y `50000000-…`)

Cargos y habilidades demo, sembrados para que el sistema tenga contenido al primer arranque. Los nombres viven en `DatosSemilla.Cargo*Id` / `Habilidad*Id`; los detalles (`Codigo`, `Nombre`) están en `DatosSemilla.Configurar`.

| Bloque | Tabla | Filas seed |
| --- | --- | --- |
| `40000000-…` | `Cargos` | `CargoDecano`, `CargoSecretario`, `CargoDirector`, `CargoJefeDepartamento`, `CargoAdministrativo`, `CargoProfesor` |
| `50000000-…` | `Habilidades` | `HabilidadLiderazgo`, `HabilidadGestionPersonal`, `HabilidadSqlServer`, `HabilidadEfCore`, `HabilidadDotNet`, `HabilidadAdministracionPublica`, `HabilidadDocenciaUniversitaria` |

Las filas demo pueden convivir con filas nuevas; los tests limpian este subset antes de los casos aislados.

## Cómo extender el mapa

Para agregar un catálogo inmutable nuevo:

1. Asignar el siguiente bloque GUID contiguo disponible (reservar al menos `00000000-…0000000F`).
2. Crear `*Constantes` en `src/SGV.Infraestructura/Persistencia/Catalogos/` con un record `*Seed(Id, Codigo, Nombre, ...)` y un `IReadOnlyList<...> Semilla`.
3. Crear la `IEntityTypeConfiguration<T>` en `src/SGV.Infraestructura/Persistencia/Configuraciones/`.
4. Crear una nueva migración `Add<Entidad>Catalog` que materialice `Semilla` en `InsertData`.
5. Agregar `builder.Entity<...>().HasData(...)` en `DatosSemilla.Configurar` (paridad con la migración).
6. Actualizar `docs/decisiones-implementacion.md` y este reference doc.

## Cómo agregar una fila nueva a un catálogo existente

1. Asignar el siguiente `Id` contiguo dentro del bloque reservado.
2. Actualizar el `*Seed` correspondiente en la constante.
3. Agregar la fila en `InsertData` de la nueva migración.
4. Sincronizar `DatosSemilla.Configurar` con `HasData`.

> ⚠️ A verificar: la fila 4 de `EstadoPostulacion` ("Aprobado") está marcada como `EsTerminal=false` en el seed (`DatosSemilla.cs:66`); verificar contra `openspec/specs/postulacion-management/spec.md` si la regla de negocio exige marcarla como terminal positivo cuando deriva a "Contratado".

## Referencias

- Tutorial: [Levantar el sistema local](../tutorials/01-levantar-sistema-local.md)
- How-to: [Crear catálogo inmutable con bloque GUID](../how-to/09-crear-catalogo-inmutable-bloque-guid.md)
- How-to: [Agregar migración EF Core](../how-to/05-agregar-migracion-ef-core.md)
- R-03-02 — Esquema de base de datos (FKs desde cada catálogo)
- R-03-11 — Tabla de migraciones EF Core (qué migración sembró cada catálogo)
