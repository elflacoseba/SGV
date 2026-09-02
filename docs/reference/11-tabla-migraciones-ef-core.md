# R-03-11 — Tabla de migraciones EF Core

Cronología de las migraciones EF Core aplicadas por `SgvDbContext`. Cada migración tiene un timestamp `YYYYMMDDHHMMSS` y un nombre `PascalCase` que resume el cambio. El script idempotente `docs/migracion-inicial-sgv.sql` reproduce el esquema final sobre una base vacía; la variante MariaDB vive en `docs/migracion-inicial-sgv-mariadb.sql`.

## Listado cronológico

| Timestamp | Nombre | Propósito | Impacto |
| --- | --- | --- | --- |
| `20260614183103` | `InicialSgvo` | Crea el esquema base del SGV con todas las tablas auditables (`AuditableEntityBase`), Identity (`AspNetUsers` con clave string, FK a `Personas`), `UnidadesOrganizativas` (con `UnidadPadreId` self-FK), `Cargos`, `Habilidades`, `NivelesHabilidad` (bloque `10000000-…`), `CargoHabilidades`, `Personas`, `PersonaHabilidades`, `Puestos`, `Ocupaciones` (con `TipoAsignacion` como string), `EstadosVacante` (bloque `20000000-…`), `Vacantes`, `HistorialEstadosVacante`, `Postulantes`, `EstadosPostulacion` (bloque `30000000-…`), `Postulaciones`, `HistorialEstadosPostulacion`, `EvaluacionesPostulacion`, `Auditorias`. Crea también `TiposUnidadOrganizativa` (bloque `60000000-…`, 20 filas seed) y `NivelesCargo` (bloque `70000000-…`, 4 filas seed). | Crea ~25 tablas. FKs con `OnDelete(Restrict)` salvo las que requieren `Cascade`. Triggers anti-ciclos NO incluidos todavía. |
| `20260614183109` | `AgregarDatosSemillaBase` | Siembra `Cargo` (bloque `40000000-…`, 6 filas), `Habilidad` (bloque `50000000-…`, 7 filas) y los roles Identity `Administrador`/`GestorVacantes`/`Consultor`. | Datos demo. Convive con los datos de usuario. |
| `20260616190624` | `CambiarTipoUnidadATablaTipoUnidadOrganizativa` | Convierte `UnidadesOrganizativas.TipoUnidad` (string) en FK a la tabla `TiposUnidadOrganizativa`. | Renombra `TipoUnidad` → `TipoUnidadOrganizativaId` (Guid, FK Restrict), agrega índice sobre la nueva FK. |
| `20260618180508` | `CambiarNivelStringANivelId` | Convierte `Cargos.Nivel` (string) en FK a la tabla `NivelesCargo`. | Renombra `Nivel` → `NivelId` (Guid, FK Restrict), agrega `IX_Cargos_NivelId`. `UpdateData` re-mapea las filas seed usando las constantes `NivelCargoConstantes.*Id`. |
| `20260621202540` | `VincularIdentityUsuariosAPersonas` | Agrega `PersonaId` (Guid) en `AspNetUsers` con FK Restrict a `Personas` e índice UNIQUE para evitar que una persona quede con dos usuarios. | `FK_AspNetUsers_Personas_PersonaId`, `IX_AspNetUsers_PersonaId UNIQUE`. |
| `20260624153353` | `ConvertirTipoAsignacionAEnumYActualizarUnicidad` | Convierte `Ocupaciones.TipoAsignacion` (string) a `int` (enum). Cambia la unicidad activa: pasa de unique-filter sobre `PersonaId`+`PuestoId`+`FechaInicio` a unique-filter vía columna generada `ActivePersonaPuestoUnique` (`CONCAT(PersonaId, ':', PuestoId)` cuando `FechaFin IS NULL AND IsDeleted = 0`). | `DropIndex` + `AlterColumn` + `AddColumn ActivePersonaPuestoUnique STORED` + `CreateIndex`. |
| `20260711181615` | `FixActivePuestoIdUniqueType` | Corrige el tipo de la columna generada `ActivePuestoIdUnique` en `Ocupaciones`: era creada con tipo string implícito; el fix fuerza `varchar(36)` + collation `ascii_general_ci` para estabilizar comparaciones binarias. | Drop → alter → create siguiendo la regla MySQL InnoDB sobre columnas generadas indexadas. |
| `20260715145121` | `AddSoftDeleteToAspNetUsers` | Agrega `IsDeleted` y dos columnas generadas (`ActiveUserNameUnique`, `ActivePersonaIdUnique`) en `AspNetUsers`. | Soft delete transitorio sobre Identity users. |
| `20260716120000` | `DropSoftDeleteFromAspNetUsers` | Reversa el soft-delete de `AspNetUsers` introducido en `20260715145121`. Se vuelve a la separación nativa entre `LockoutEnd` y `IsDeleted`. La FK `PersonaId` UNIQUE sigue vigente. | Drop de columnas generadas e índice UNIQUE soft-delete. Conserva `IX_AspNetUsers_PersonaId UNIQUE`. |
| `20260719180541` | `AddPersonasNumeroDocumentoIndex` | Agrega `IX_Personas_NumeroDocumento` para sostener búsquedas substring case-insensitive. | Único índice no generado. |
| `20260720230343` | `TipoDocumentoCatalogoYPersonaFk` | Introduce el catálogo `TipoDocumento` (bloque `71000000-…`, 4 filas seed) y agrega `Persona.TipoDocumentoId` con FK Restrict. Crea el índice `ActiveDocumentoUnique` (columna generada con `CONCAT(TipoDocumentoId, ':', NumeroDocumento)`). | Nueva tabla `TiposDocumento`; FK `FK_Personas_TiposDocumento_TipoDocumentoId`. |
| `20260723203015` | `AddCategoriaHabilidadCatalog` | Introduce el catálogo `CategoriaHabilidad` (bloque `72000000-…`, 4 filas seed) y agrega `Habilidad.CategoriaId` con FK Restrict. | Nueva tabla `CategoriasHabilidad`; FK `FK_Habilidades_CategoriasHabilidad_CategoriaId`. |
| `20260729145632` | `MariaDbStoredColumnsAndCollation` | Ajustes de compatibilidad MariaDB: cambia columnas generadas a `STORED` explícito y collation `utf8mb4_unicode_ci` donde corresponda. Idempotente para MySQL 8 (que también acepta `STORED`). | Sin impacto funcional; ajusta la generación del script SQL. |
| `20260730000000` | `SemillaTipoUnidadOrganizativaAmpliada` | Resiembra `TiposUnidadOrganizativa` con el set ampliado (20 filas). | `UpdateData`/seed ampliado. |
| `20260731173842` | `AddActivePuestoIdUniqueToVacantes` | Agrega el índice `IX_Vacantes_ActivePuestoIdUnique UNIQUE` (columna generada `CASE WHEN FechaCierre IS NULL AND IsDeleted = 0 THEN PuestoId ELSE NULL END`) para garantizar una sola vacante abierta por puesto. | Drop → alter → create; defensa contra la ventana TOCTOU del pre-check `ExistsAbiertaByPuestoAsync`. |
| `20260801014133` | `IndiceAuditoriaCorrelationIdOccurredAt` | Agrega el índice compuesto `IX_Auditorias_CorrelationId_OccurredAt` para sostener el filtro por `CorrelationId` + orden `OccurredAt` sin filesort. | Remueve el índice simple redundante sobre `CorrelationId`. |
| `20260804235936` | `AddVacanteIdToOcupaciones` | Agrega `Ocupacion.VacanteId` (Guid nullable, FK a `Vacantes`, OnDelete Restrict) más `IX_Ocupaciones_VacanteId`. N2 del change `vacante-ocupacion-flow-alignment`. | Las ocupaciones derivadas de cubrir una vacante llevan `VacanteId` poblado. |
| `20260805000000` | `AddEstadoVacanteFlags` | Agrega los flags `EsCubierta` y `EsCancelada` a `EstadoVacanteEntity`. | UpdateData en las 4 filas seed. |
| `20260813120000` | `FixEstadoVacanteEnSeleccionEncoding` | Corrige encoding del nombre "En Selección" en la fila seed. | UpdateData del nombre. |
| `20260816203122` | `AddTriggerAntiCiclosUnidadesOrganizativas` | Crea `trg_UnidadesOrganizativas_BeforeInsert_Ciclo` y `trg_UnidadesOrganizativas_BeforeUpdate_Ciclo` que rechazan cualquier cambio que forme un ciclo transitivo en la jerarquía activa (CTE recursiva con `depth < 32`). Cualquier ciclo emite `SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CicloJerarquico'`, que la capa de aplicación traduce a `409` vía `MySqlConstraintViolationDetector` (error code 1644). | Sin nuevas tablas; dos triggers `BEFORE INSERT/UPDATE`. |
| `20260819223914` | `AddRefreshTokens` | Crea la tabla `RefreshTokens` con `UserId` (FK `AspNetUsers`, Cascade), `FamilyId` (Guid), `TokenHash` (varchar(64) SHA-256 hex), `CreatedAt`/`ExpiresAt`/`LastUsedAt` (`datetime(6)`) y `ReplacedById` (Guid?, puntero lógico sin FK self-referencing). Índices: `IX_RefreshTokens_TokenHash UNIQUE`, `IX_RefreshTokens_UserId`, `IX_RefreshTokens_FamilyId`, `IX_RefreshTokens_ReplacedById`. | Nueva tabla. La desviación respecto al FK self-referencing en `ReplacedById` está documentada en `RefreshTokenConfiguracion.cs` (PR1b). |

## Conteo y categorías

| Categoría | Cantidad |
| --- | --- |
| Creadoras de tabla | 1 (`InicialSgvo`) |
| Modificadoras de columnas | 4 (`CambiarTipoUnidadATablaTipoUnidadOrganizativa`, `CambiarNivelStringANivelId`, `ConvertirTipoAsignacionAEnumYActualizarUnicidad`, `FixActivePuestoIdUniqueType`) |
| Identity-related | 3 (`VincularIdentityUsuariosAPersonas`, `AddSoftDeleteToAspNetUsers`, `DropSoftDeleteFromAspNetUsers`) |
| Catálogos nuevos | 2 (`TipoDocumentoCatalogoYPersonaFk`, `AddCategoriaHabilidadCatalog`) |
| Índices / constraints | 4 (`AddPersonasNumeroDocumentoIndex`, `AddActivePuestoIdUniqueToVacantes`, `IndiceAuditoriaCorrelationIdOccurredAt`, `AddTriggerAntiCiclosUnidadesOrganizativas`) |
| Seeds / resiembras | 3 (`AgregarDatosSemillaBase`, `MariaDbStoredColumnsAndCollation`, `SemillaTipoUnidadOrganizativaAmpliada`, `AddEstadoVacanteFlags`, `FixEstadoVacanteEnSeleccionEncoding`) |
| Vacantes / ocupaciones | 2 (`AddVacanteIdToOcupaciones`, `AddRefreshTokens` cubre refresh) |
| Refresh tokens | 1 (`AddRefreshTokens`) |

> Las categorías no son mutuamente excluyentes; algunas migraciones combinan seed + ajuste de tipo (`MariaDbStoredColumnsAndCollation`).

## Forward-only / reversibilidad

| Migración | ¿Forward-only? | Notas |
| --- | --- | --- |
| `20260716120000_DropSoftDeleteFromAspNetUsers` | Sí | Reversa `20260715145121_AddSoftDeleteToAspNetUsers` y consolida `IX_AspNetUsers_PersonaId UNIQUE`. |
| `20260723203015_AddCategoriaHabilidadCatalog` | Sí | Crea FK + catálogo. |
| `20260816203122_AddTriggerAntiCiclosUnidadesOrganizativas` | Sí | Down elimina los triggers; sin embargo, mantener la app sin triggers abre la puerta a inserciones cíclicas. |
| `20260819223914_AddRefreshTokens` | Sí | Tabla nueva; `Down` la borra pero eso rompe la rotación vigente. |

## Cómo generar / aplicar migraciones nuevas

```bash
dotnet ef migrations add <Nombre> \
  --project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --output-dir Persistencia/Migraciones

dotnet ef migrations script \
  --project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --idempotent \
  --output docs/migracion-inicial-sgv.sql
```

## Cómo regenerar el script SQL inicial

El script `docs/migracion-inicial-sgv.sql` se regenera con el comando `migrations script --idempotent` y se commitea cada vez que se agrega una migración. La variante MariaDB se genera con un script separado (`docs/migracion-inicial-sgv-mariadb.sql`).

## Referencias

- How-to: [Agregar migración EF Core](../how-to/05-agregar-migracion-ef-core.md)
- How-to: [Levantar MySQL Docker para tests](../how-to/07-levantar-mysql-docker-para-tests.md)
- Tutorial: [Levantar el sistema local](../tutorials/01-levantar-sistema-local.md)
- R-03-02 — Esquema de base de datos (forma final del esquema tras todas las migraciones)
- R-03-08 — Catálogos inmutables y bloques GUID (qué catálogos sembraron las migraciones)
