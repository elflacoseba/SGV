# Progreso de aplicación — Implementa módulo usuarios

## Estado del lote

- **PR actual**: PR1 — Backend.
- **Rama tracker**: `feat/2026-07-15-implementa-modulo-usuarios-tracker`.
- **Rama de trabajo**: `feat/2026-07-15-implementa-modulo-usuarios-pr1-backend`.
- **Estrategia**: `feature-branch-chain`; PR1 debe apuntar al tracker.
- **Modo de implementación**: Strict TDD.
- **Tareas PR1**: 15/15 completadas.
- **Tareas del change**: 15/34 completadas; PR2, PR3 y PR4 permanecen fuera de alcance.
- **Estado**: implementación funcional completa. Decisión humana adoptada sobre la desviación operativa de migración (ver "Decisión humana sobre desviaciones").

## Resumen de implementación

- Se agregó soft-delete a `SgvIdentityUser` y la migración `AddSoftDeleteToAspNetUsers` con `IsDeleted`, columna generada `ActiveUserNameUnique` e índice único.
- `UsuarioDto` conserva el orden existente y agrega `Nombres`/`Apellidos` nullable al final; se incorporaron `ActualizarUsuarioRequest`, `UsuarioListQuery`, `UsuarioSegmentoListado` y `UsuarioListadoDto`.
- `UsuarioIdentityGateway` ahora expone consulta paginada/segmentada, detalle, actualización atómica, baja y reactivación; la carga de roles usa una consulta agregada y no ejecuta `GetRolesAsync` dentro de un bucle.
- `UsuarioServicioComandos` implementa D-01 (`AutoBaja` → `Forbidden`), D-02 (`PersonaInactiva` → `Conflict`), D-03 (LWW) y D-04 (PUT único UserName+Email+Roles).
- Todas las mutaciones registran auditoría explícita con `IAuditoriaServicio`, incluyendo diffs de `UserName`, `Email` y roles.
- `UsuariosController` deja las lecturas a cualquier autenticado y exige `Administrador` en POST/PUT/DELETE/PATCH y en el catálogo de roles.
- Se agregó `UsuarioActualHttpContext` para que el guard de auto-baja y la auditoría reciban el `sub` real del JWT.
- Se corrigió `JwtRealWebApplicationFactory`: el DbContext del test ahora se reemplaza explícitamente y queda aislado en `sgv_test`, evitando tocar la base local `sgv`.

## Tareas completadas

- [x] **1.1** Tests RED para auto-baja y Persona inactiva con mapeos 403/409.
- [x] **1.2** Migración EF y modelo Identity con soft-delete, columna generada e índice único.
- [x] **1.3** Script SQL idempotente acotado a `AddSoftDeleteToAspNetUsers`.
- [x] **1.4** `UsuarioDto` con `Nombres`/`Apellidos` nullable al final.
- [x] **1.5** Contratos de consulta, segmento y wrapper paginado.
- [x] **1.6** `IUsuarioServicioConsulta.QueryAsync` y detalle por id.
- [x] **1.7** `UsuarioIdentityGateway.QueryAsync` sin N+1.
- [x] **1.8** Puertos de actualización, baja y reactivación.
- [x] **1.9** Handlers de aplicación con validaciones y auditoría.
- [x] **1.10** Tests unitarios de aplicación.
- [x] **1.11** Tests MySQL de gateway, migración limpia, consulta y reactivación.
- [x] **1.12** Endpoints API de consulta, detalle, PUT, DELETE y PATCH.
- [x] **1.13** Taxonomía HTTP para `AutoBaja`, `PersonaInactiva`, duplicados y persona asociada mediante `ErrorCategoria`/`ApiResults`.
- [x] **1.14** Tests API de paginación, normalización, autorización y códigos de error.
- [x] **1.15** Build, gate focalizado, migración sin cambios pendientes y suite completa.

## Evidencia de ciclos TDD

| Task | Archivo(s) de test | Capa | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 1.1 | `UsuarioServicioComandosTests.cs`, `UsuariosControllerTests.cs` | Unit/API | 27/27 previos | Falló por APIs inexistentes | `AutoBaja` 403 y `PersonaInactiva` 409 verdes | Caller propio/ajeno + Persona activa/inactiva | Guard clauses y helper de fallos |
| 1.2 | `SgvIdentityUserConfiguracionTests.cs`, `UsuarioIdentityGatewayTests.cs` | Modelo/MySQL | 3/3 config previos | Faltaban propiedades y DDL | Modelo y esquema verdes | Metadata + base descartable desde cero | ALTER dividido por capacidad real de MySQL |
| 1.3 | `SgvIdentityUserConfiguracionTests.cs` | Estructural | N/A (artefacto nuevo) | No existía operación SQL verificable | Script idempotente generado | DDL estática + aplicación real | Script acotado a una migración |
| 1.4 | `UsuarioContractsTests.cs` | Contrato | N/A (comportamiento nuevo) | Constructor sin nombres | 4/4 contratos verdes | Orden + nullability | Defaults nullable para compatibilidad fuente |
| 1.5 | `UsuarioContractsTests.cs` | Contrato | N/A (tipos nuevos) | Tipos inexistentes | Query/segmento/wrapper verdes | Segmento default + metadata paginada | Triangulación estructural suficiente |
| 1.6 | `ApiWebApplicationFactory.cs`, `UsuariosControllerTests.cs` | Contrato/API | 27/27 previos | Fakes no compilaban contra la nueva interfaz | Consulta y detalle verdes | Resultado existente/no existente | Interfaces estrechas por operación |
| 1.7 | `UsuarioIdentityGatewayTests.cs` | MySQL | N/A (método nuevo) | Gateway sin `QueryAsync` | Consulta devuelve DTO+roles | 3 usuarios/4 roles con exactamente 2 readers | GroupJoin y agrupación en memoria |
| 1.8 | `UsuarioServicioComandosTests.cs`, `UsuarioIdentityGatewayTests.cs` | Unit/MySQL | 6/6 comandos previos | Puertos y métodos inexistentes | Ciclo completo verde | Éxito, missing y conflicto | Reemplazo de roles por diferencias |
| 1.9 | `UsuarioServicioComandosTests.cs` | Unit | 6/6 comandos previos | Servicio no implementaba handlers | 18/18 verdes | Validaciones, auto-baja, LWW y Persona inactiva | Helpers de validación/auditoría |
| 1.10 | `UsuarioServicioComandosTests.cs` | Unit | 6/6 previos | Casos nuevos fallaron/compilaron en rojo | 18/18 verdes | Casos felices + bordes por comportamiento | Fakes con estado observable |
| 1.11 | `UsuarioIdentityGatewayTests.cs` | Integración MySQL | Bootstrap disponible | Migración limpia falló con STORED+INPLACE | 10/10 verdes | DB existente + DB descartable limpia | Gate explícito de query-count y cleanup |
| 1.12 | `UsuariosControllerTests.cs` | API integración | 4/4 API usuarios previos | Rutas inexistentes | 26/26 verdes | Auth, éxito y fallos por endpoint | Controller delgado y `ApiResults` central |
| 1.13 | `UsuariosControllerTests.cs`, `ErrorCategoriaMappersTests.cs` | API/contrato | Mapper legacy verde | 403/409 nuevos no existían | Matriz observable verde | Forbidden/Conflict/Validation/NotFound | Se reutilizó `MapCategoria`; mapper legacy no se alteró |
| 1.14 | `UsuariosControllerTests.cs` | API integración | 4/4 previos | Nuevos escenarios fallaron | 26/26 verdes | Normalización, aliases, auth y mutaciones | Theories para matriz de autorización |
| 1.15 | Suite completa | Solución | Build y 27 tests usuarios previos | Primer full run expuso aislamiento JWT | 2211/2211 en 3 corridas | Focalizado + MySQL + suite completa | Fixture JWT aislada en `sgv_test` |

## Resumen de pruebas

- **Casos focalizados de usuarios**: 77/77 verdes (baseline previo: 27).
- **Tests de comandos de aplicación**: 18/18 verdes.
- **Tests API de `UsuariosController`**: 26/26 verdes.
- **Tests MySQL del gateway/migración**: 10/10 verdes, incluido bootstrap de una base descartable limpia.
- **Suite completa final**: 2211/2211, 0 fallidos, 0 omitidos, tres corridas consecutivas (`61 s`, `69 s`, `59 s`).
- **Build final**: exitoso; warnings preexistentes conocidos (`CS8524`, `CS8602`, `xUnit1026`) reaparecen en build limpio, sin errores.
- **Modelo EF**: `dotnet ef migrations has-pending-model-changes` → sin cambios pendientes.

## Evidencia de work unit PR1

| Evidencia | Resultado |
|---|---|
| Comando focalizado | `dotnet test SGV.slnx --no-build --filter "Api.Usuarios|Persistencia.Usuarios|Aplicacion.Usuarios"` → 26/26; gate amplio `FullyQualifiedName~Usuario` → 77/77 |
| Runtime harness | `[MySqlFact]` sobre `sgv_test` + base descartable desde cero → 10/10; migraciones aplicadas y columna/índice verificados en `INFORMATION_SCHEMA` |
| Rollback boundary | Revertir los seis commits de PR1 elimina contratos, migración, gateway/handlers, endpoints, auditoría explícita y tests sin tocar PR2/PR3/PR4 |

## Commits de implementación

1. `9bd11420` — `feat(schema): add soft delete to identity users`
2. `8de4990b` — `feat(application): add atomic identity user lifecycle`
3. `0e6e499f` — `feat(api): expose complete identity user management`
4. `654b68e3` — `fix(test): isolate real jwt auth database`
5. `6e10634b` — `fix(schema): make stored user column migration executable`
6. `0a324059` — `test(application): preserve unsupported role guard`

## Desviaciones del diseño

1. **STORED + INPLACE no es ejecutable en MySQL 8**. El RED sobre una base limpia devolvió: `ALGORITHM=INPLACE is not supported for this operation. Try ALGORITHM=COPY.` La migración conserva el esquema final solicitado (`STORED`) y divide el rollout: `IsDeleted` e índice usan `INPLACE/LOCK=NONE`; la incorporación de la columna STORED declara `ALGORITHM=COPY`.

## Decisión humana sobre desviaciones

En sesión interactiva tras cerrar `sdd-apply` del PR1, el maintainer adoptó explícitamente la **opción A: aceptar `ALGORITHM=COPY`**. Implicaciones operativas:

- La columna `AspNetUsers.ActiveUserNameUnique` (GENERATED STORED) exige `ALGORITHM=COPY` cuando se aplica a la base productiva. Esto bloquea lecturas/escrituras sobre `AspNetUsers` durante la ventana de copia — proporcional al tamaño de la tabla al momento del deploy.
- El plan de rollout queda registrado en `docs/decisiones-implementacion.md` bajo "Módulo Usuarios — soft-delete de Identity con columna generada STORED".
- Las alternativas (cambiar a `VIRTUAL` o rediseñar el patrón) quedan descartadas para este change; podrían re-evaluarse en un change futuro si la tabla crece fuera de la ventana de mantenimiento razonable.

Las desviaciones 2 y 3 no requirieron decisión humana (son adaptaciones técnicas con resultado observable equivalente al diseño).
2. `QueryAsync` usa JOIN explícito con `Persona` en lugar de `Include(Persona)` porque `SgvIdentityUser` no expone navegación; el resultado observable y el límite sin N+1 se mantienen. La consulta paginada ejecuta un `COUNT` y un reader agregado de datos/roles (2 readers constantes), no una sola sentencia total.
3. `ErrorCategoriaMappers.ToTipoUsuario` conserva el comportamiento legacy que rechaza `Forbidden`; `ApiResults` consume `UsuarioError.Categoria` directamente y mapea `AutoBaja` a 403. Cambiar el enum obsoleto habría roto tests/compatibilidad fuera del alcance.

## Riesgos

- **Resuelto (decisión humana opción A)** — La columna generada `STORED` exige `ALGORITHM=COPY` en MySQL 8; ventana de mantenimiento aceptada por el maintainer. Documentada en `docs/decisiones-implementacion.md`.
- El diff total del PR1 es **4471 adiciones / 127 eliminaciones** antes de artefactos de progreso; incluye ~2178 líneas generadas de EF/script. Aun excluyéndolas, el contenido autoral supera el budget de 800 líneas. La estrategia encadenada ya fue aceptada, pero PR1 requiere revisión enfocada.
- Identity mantiene además su índice único estándar sobre `NormalizedUserName`; la columna nueva protege la regla pedida, pero reutilizar el mismo username mientras otro usuario eliminado conserva `NormalizedUserName` puede seguir chocando con Identity. No se alteró ese índice porque no figura en el DDL aprobado.

## Pendiente fuera de PR1

- PR2: tasks 2.1–2.7 (`SGV.Web/Integration/Usuarios`, DI y navegación).
- PR3: tasks 3.1–3.6 (Index, Details, baja/reactivación PRG).
- PR4: tasks 4.1–4.6 (Create, Edit y `_Form.cshtml`).

## Límite de PR

```text
develop
  └── feat/2026-07-15-implementa-modulo-usuarios-tracker
       └── 📍 feat/2026-07-15-implementa-modulo-usuarios-pr1-backend
            └── PR2 (pendiente)
                 └── PR3 (pendiente)
                      └── PR4 (pendiente)
```

PR1 comienza en el tracker sin código del módulo y termina con el backend completo, migración ejecutable, contratos, auditoría, endpoints y verificación. No incluye clientes Web ni Razor Pages.
