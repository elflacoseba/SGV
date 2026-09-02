# R-03-12 — Especificaciones OpenSpec vigentes

Índice de las 64 specs activas bajo `openspec/specs/`. Cada spec es un delta de capacidad escrita en español (salvo indicación en el archivo individual) y versionada en el repositorio. La convención de naming es kebab-case; el sufijo `-web` indica que la spec cubre UI Razor Pages.

## Specs transversales (infraestructura, shell, seguridad)

| Spec | Módulo | Resumen |
| --- | --- | --- |
| `sgv-database` | DB | Capacidad de base de datos SGV: invariantes de modelo, soft-delete, columnas generadas. |
| `sgv-persistence-architecture` | Persistencia | Arquitectura de persistencia EF Core + Pomelo MySQL. |
| `sgv-web-shell` | Web | Shell Razor Pages: layout, auth, navegación. |
| `sgv-web-authentication` | Web | Autenticación web: cookie auth, bridge bearer, revalidator. |
| `sgv-readonly-api` | API | API read-only: endpoints GET-only (catálogos, consultas). |
| `operational-readiness` | Operación | Release-readiness operacional: rate limits, health checks, diagnóstico de jerarquía. |
| `test-suite-reliability` | Tests | Confiabilidad de la suite xUnit + `[MySqlFact]`. |
| `web-apiclient-transport-contract` | Web↔API | Contrato de transporte de los `*ApiClient` tipados. |
| `api-cors-allowed-origins-validation` | API | Validación de `AllowedOrigins` y fail-loud fuera de Development. |
| `jwt-signing-key-validation` | API | Validación de `Jwt:SigningKey` ≥32 bytes UTF-8. |
| `commandresult-error-taxonomy` | API/Web | Taxonomía común `ErrorCategoria` y mapeos legacy. |

## Specs de catálogos inmutables

| Spec | Módulo | Resumen |
| --- | --- | --- |
| `nivel-cargo-catalog` | Organización | Catálogo `NivelCargo` (bloque `70000000-…`). |
| `tipo-documento-catalog` | Personas | Catálogo `TipoDocumento` (bloque `71000000-…`). |
| `tipo-unidad-organizativa-catalog` | Organización | Catálogo `TipoUnidadOrganizativa` (bloque `60000000-…`). |
| `categoria-habilidad-catalog` | Habilidades | Catálogo `CategoriaHabilidad` (bloque `72000000-…`). |

## Specs de gestión (CRUD backend + read-only API)

| Spec | Módulo | Resumen |
| --- | --- | --- |
| `cargo-management` | Organización | CRUD de cargos + reactivación. |
| `habilidad-management` | Habilidades | CRUD de habilidades + reactivación. |
| `persona-management` | Personas | CRUD de personas + reactivación. |
| `puesto-management` | Organización | CRUD de puestos + reactivación. |
| `unidad-organizativa-crud` | Organización | CRUD de unidades organizativas + cambio de padre + reactivación. |
| `vacante-management` | Vacantes | CRUD de vacantes + transición de estado + historial atómico. |
| `identity-user-role-management` | Seguridad | Gestión de usuarios Identity (alta, edición, asignación de roles, lockout, delete físico). |

## Specs de subrecursos y consultas

| Spec | Módulo | Resumen |
| --- | --- | --- |
| `cargo-skill-asignar-editar` | Organización | Asignar/editar/quitar habilidades de un cargo. |
| `cargo-skill-ponderacion-obligatoria` | Organización | Ponderación obligatoria y rangos válidos en `CargoHabilidades`. |
| `cargo-skill-query-contract` | Organización | Contrato de consulta de habilidades por cargo. |
| `cargo-skill-ui-tabla-editable` | Web | Página web editable de habilidades por cargo. |
| `persona-skill-query-contract` | Personas | Contrato de consulta de habilidades por persona. |
| `persona-skill-web-management` | Personas/UI | Gestión web de persona-habilidades. |
| `skill-cargo-query-contract` | Habilidades | Contrato de consulta inversa: cargos por habilidad. |
| `skill-persona-query-contract` | Habilidades | Contrato de consulta inversa: personas por habilidad. |
| `ocupacion-web-selector-persona-buscador` | Ocupaciones/UI | Selector modal de persona con buscador (Create/Edit Ocupación). |
| `persona-card-partial` | Personas/UI | Partial de tarjeta de persona reusable. |
| `persona-format-helper` | Personas/UI | Helper de formateo de persona (Legajo + Apellido, Nombre). |
| `web-detalle-consistencia-botones` | Web | Consistencia de botones en vistas Detalle. |
| `web-ocupaciones-contrato-api` | Ocupaciones | Contrato del cliente HTTP web para ocupaciones. |
| `web-ocupaciones-crear-editar` | Ocupaciones/UI | Create/Edit web de ocupaciones. |
| `web-ocupaciones-detalle` | Ocupaciones/UI | Detalle web de ocupaciones con preservación de estado. |
| `web-ocupaciones-listado` | Ocupaciones/UI | Listado web de ocupaciones. |
| `web-ocupaciones-navegacion-contextual` | Ocupaciones/UI | Navegación contextual entre páginas de ocupaciones. |

## Specs de auditoria

| Spec | Módulo | Resumen |
| --- | --- | --- |
| `auditoria-query` | Auditoria | Query base del listado de auditoría (filtros, orden, paginación). |
| `auditoria-sort` | Auditoria | Whitelist de sort `fecha/entidad/operacion/usuario/correlacion` con default `fecha_desc`. |
| `auditoria-detalle` | Auditoria | Detalle enriquecido con `OldValuesJson`/`NewValuesJson`. |
| `auditoria-page-size` | Auditoria | Clamp de `pageSize` a `[1, 100]`. |
| `auditoria-drilldown-username-filter` | Auditoria | Filtro adicional por `UserName` (UI). |

## Specs de autenticación y password

| Spec | Módulo | Resumen |
| --- | --- | --- |
| `password-reset-flow` | API | Flujo backend de password reset (token + email). |
| `password-change` | API | Cambio de contraseña autenticado (rotación de `SecurityStamp`). |
| `password-reset-web` | Web | UI de password reset. |
| `password-change-web` | Web | UI de cambio de contraseña. |

## Specs de gestión de usuarios

| Spec | Módulo | Resumen |
| --- | --- | --- |
| `usuario-delete-fisico` | Seguridad | Borrado físico de cuenta (admin). |
| `usuario-lockout-administrativo` | Seguridad | Lockout/unlockout administrativo. |
| `usuario-web-crear-editar` | Web | Alta/edición web de usuarios. |
| `usuario-web-listado-detalle-baja` | Web | Listado/detalle/baja/reactivación web de usuarios. |
| `usuario-web-confirmacion-bloqueo-desbloqueo` | Web | Confirmación modal obligatoria al bloquear/desbloquear. |
| `usuario-web-selector-persona-buscador` | Web | Selector modal de persona con buscador (Create/Edit Usuario). |

## Specs de UI Web (CRUD y detalle)

| Spec | Módulo | Resumen |
| --- | --- | --- |
| `cargo-web-crear-editar` | Web | Create/Edit web de cargos. |
| `cargo-web-listado-detalle-baja` | Web | Listado/Detalle/Baja web de cargos. |
| `habilidad-web-crear-editar` | Web | Create/Edit web de habilidades. |
| `habilidad-web-listado-detalle-baja` | Web | Listado/Detalle/Baja web de habilidades. |
| `puesto-web-crear-editar` | Web | Create/Edit web de puestos. |
| `puesto-web-listado-detalle-baja` | Web | Listado/Detalle/Baja web de puestos. |
| `unidad-organizativa-web-listado` | Web | Listado web de unidades organizativas. |
| `unidad-organizativa-web-detalle-edicion` | Web | Detalle/Edición web de unidades organizativas. |
| `vacante-web` | Web | UI web completa de gestión de vacantes. |

## Specs misceláneas

| Spec | Módulo | Resumen |
| --- | --- | --- |
| `setup-initial-admin` | Setup | Setup one-time del primer Administrador. |

## Conteo y categorías

| Categoría | Cantidad |
| --- | --- |
| Transversales | 11 |
| Catálogos inmutables | 4 |
| Gestión CRUD | 7 |
| Subrecursos y consultas | 16 |
| Auditoría | 5 |
| Auth/password | 4 |
| Gestión de usuarios | 6 |
| UI Web (CRUD/detalle) | 10 |
| Misceláneas | 1 |
| **Total** | **64** |

## Cómo explorar las specs

Cada spec vive en `openspec/specs/<nombre>/spec.md`. Los deltas que están en desarrollo activo viven en `openspec/changes/` (no incluidos en este índice). Para auditar capacidades vigentes:

```bash
ls openspec/specs/         # 64 capacidades
ls openspec/changes/       # capacidades en draft
ls openspec/changes/archive/  # capacidades archivadas
```

> ⚠️ A verificar: el conteo de "Total" se realizó contra `ls openspec/specs/` en este snapshot. Si se agregan specs nuevas, este índice debe regenerarse para mantener paridad.

## Referencias

- Tutorial: [Levantar el sistema local](../tutorials/01-levantar-sistema-local.md)
- How-to: [Bloquear y desbloquear usuario](../how-to/04-bloquear-desbloquear-usuario.md)
- How-to: [Operar flujo de recuperación de contraseña](../how-to/02-operar-flujo-recuperacion-contrasena.md)
- How-to: [Auditar quién modificó entidad](../how-to/08-auditar-quien-modifico-entidad.md)
- R-03-01 — Mapa de APIs HTTP (capacidades que afectan el wire contract)
