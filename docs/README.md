# Documentación de SGV

Documentación del sistema SGV organizada con la metodología **Diátaxis**: cada
documento vive en uno de cuatro cuadrantes según el trabajo que habilita
(aprender, resolver un problema, consultar datos o entender el "por qué").

## ¿Qué es SGV?

SGV es una solución .NET 10 con Clean Architecture para gestión de estructura
organizacional, personas, habilidades, ocupaciones y vacantes. El backend
corre como API HTTP (`SGV.Api` con JWT bearer) y el frontend como shell Razor
Pages (`SGV.Web` con cookie auth + bridge JWT). Persistencia en MySQL 8 vía
EF Core 9 + Pomelo.

Más detalle de arquitectura y decisiones técnicas en
[`docs/decisiones-implementacion.md`](./decisiones-implementacion.md) y en
[`openspec/specs/`](../../openspec/specs/) (53 specs vigentes).

## Convenciones de la documentación

- **Idioma**: español rioplatense neutro formal. Identificadores y nombres de
  clases/métodos quedan en inglés.
- **Diagramas**: se usan bloques ASCII para grafos y matrices; son opcionales.
- **Marcadores**: cualquier afirmación que no se pudo verificar contra código
  lleva `> ⚠️ A verificar: <motivo>`. No inventamos.
- **Links**: las referencias cruzadas entre docs usan paths relativos desde
  `docs/`. Verificá que el destino exista antes de mergear.

## Por dónde empezar según tu rol

| Si sos… | Empezá por | Después |
|---|---|---|
| Desarrollador nuevo en el repo | [`T-01-01`](./tutorials/01-levantar-sistema-local.md) (levantar el sistema) → [`T-01-02`](./tutorials/02-primera-mutacion-unidad-organizativa.md) (primera mutación) → [`E-04-01`](./explanation/01-clean-architecture-dos-composition-roots.md) (arquitectura) | [`T-01-04`](./tutorials/04-primer-cambio-clean-architecture.md) (primer cambio) → [`R-03-02`](./reference/02-esquema-base-de-datos.md) (esquema BD) |
| Backend dev que va a tocar Dominio/Aplicación | [`E-04-01`](./explanation/01-clean-architecture-dos-composition-roots.md) + [`E-04-11`](./explanation/11-patron-reconstitute-internalsvisibleto.md) | [`R-03-02`](./reference/02-esquema-base-de-datos.md) + [`R-03-03`](./reference/03-wire-types-contracts.md) |
| Backend dev que va a tocar API/Controllers | [`R-03-01`](./reference/01-mapa-apis-http.md) + [`R-03-06`](./reference/06-pipeline-middleware-api.md) | [`H-02-05`](./how-to/05-agregar-migracion-ef-core.md) + [`E-04-09`](./explanation/09-rate-limiting-ip-vs-subject.md) |
| Frontend / Web dev | [`R-03-07`](./reference/07-pipeline-arranque-web.md) + [`R-03-03`](./reference/03-wire-types-contracts.md) | [`E-04-02`](./explanation/02-bridge-cookie-jwt.md) + [`R-03-10`](./reference/10-taxonomia-errores.md) |
| DBA / data engineer | [`R-03-02`](./reference/02-esquema-base-de-datos.md) + [`R-03-11`](./reference/11-tabla-migraciones-ef-core.md) | [`H-02-01`](./how-to/01-diagnosticar-ciclos-jerarquia.md) + [`E-04-04`](./explanation/04-columnas-generadas-unicidad-activa.md) + [`E-04-08`](./explanation/08-anti-ciclos-jerarquia.md) |
| SRE / DevOps | [`H-02-07`](./how-to/07-levantar-mysql-docker-para-tests.md) + [`R-03-05`](./reference/05-configuracion-opciones-secretos.md) | [`H-02-03`](./how-to/03-rotar-jwt-signing-key.md) + [`H-02-06`](./how-to/06-configurar-allowed-origins-produccion.md) + [`H-02-12`](./how-to/12-configurar-smtp-real.md) + [`R-03-09`](./reference/09-health-checks.md) |
| Administrador (operación) | [`H-02-10`](./how-to/10-forzar-setup-inicial.md) + [`H-02-02`](./how-to/02-operar-flujo-recuperacion-contrasena.md) | [`H-02-04`](./how-to/04-bloquear-desbloquear-usuario.md) + [`H-02-08`](./how-to/08-auditar-quien-modifico-entidad.md) |
| Auditor de seguridad | [`E-04-07`](./explanation/07-password-policy-single-source-truth.md) + [`E-04-06`](./explanation/06-refresh-tokens-single-use-replay.md) | [`R-03-04`](./reference/04-roles-matriz-autorizacion.md) + [`E-04-02`](./explanation/02-bridge-cookie-jwt.md) |
| QA funcional | [`T-01-02`](./tutorials/02-primera-mutacion-unidad-organizativa.md) | [`E-04-05`](./explanation/05-maquina-estados-vacantes.md) + [`R-03-01`](./reference/01-mapa-apis-http.md) |

## Mapa "si necesitás X, empezá por Y"

| Si necesitás… | Andá a |
|---|---|
| Levantar el sistema por primera vez | [`T-01-01`](./tutorials/01-levantar-sistema-local.md) |
| Diagnosticar ciclos en la jerarquía de unidades organizativas | [`H-02-01`](./how-to/01-diagnosticar-ciclos-jerarquia.md) |
| Cambiar la clave JWT sin tirar sesiones | [`H-02-03`](./how-to/03-rotar-jwt-signing-key.md) |
| Configurar SMTP real para password reset | [`H-02-12`](./how-to/12-configurar-smtp-real.md) |
| Crear una migración nueva | [`H-02-05`](./how-to/05-agregar-migracion-ef-core.md) |
| Ejecutar solo los tests de un módulo | [`H-02-11`](./how-to/11-ejecutar-tests-de-un-modulo.md) |
| Ver todos los endpoints HTTP de la API | [`R-03-01`](./reference/01-mapa-apis-http.md) |
| Ver todas las tablas, columnas, índices, triggers | [`R-03-02`](./reference/02-esquema-base-de-datos.md) |
| Ver todos los wire-types compartidos | [`R-03-03`](./reference/03-wire-types-contracts.md) |
| Ver la matriz de roles × endpoints | [`R-03-04`](./reference/04-roles-matriz-autorizacion.md) |
| Ver todas las opciones de configuración y sus secretos equivalentes | [`R-03-05`](./reference/05-configuracion-opciones-secretos.md) |
| Ver el orden del pipeline middleware de la API | [`R-03-06`](./reference/06-pipeline-middleware-api.md) |
| Ver cómo arranca la Web (HttpClients, BFF, middleware) | [`R-03-07`](./reference/07-pipeline-arranque-web.md) |
| Ver los catálogos inmutables y sus bloques GUID | [`R-03-08`](./reference/08-catalogos-inmutables-bloques-guid.md) |
| Ver qué hace cada health check | [`R-03-09`](./reference/09-health-checks.md) |
| Entender la taxonomía de errores HTTP | [`R-03-10`](./reference/10-taxonomia-errores.md) |
| Ver la cronología de migraciones EF Core | [`R-03-11`](./reference/11-tabla-migraciones-ef-core.md) |
| Entender por qué hay dos composition roots | [`E-04-01`](./explanation/01-clean-architecture-dos-composition-roots.md) |
| Entender el bridge cookie → JWT | [`E-04-02`](./explanation/02-bridge-cookie-jwt.md) |
| Entender cómo funciona la auditoría transversal | [`E-04-03`](./explanation/03-auditoria-transversal-savechanges-interceptor.md) |
| Entender por qué hay columnas generadas para unicidad activa | [`E-04-04`](./explanation/04-columnas-generadas-unicidad-activa.md) |
| Entender la máquina de estados de Vacantes | [`E-04-05`](./explanation/05-maquina-estados-vacantes.md) |
| Entender los refresh tokens single-use | [`E-04-06`](./explanation/06-refresh-tokens-single-use-replay.md) |
| Entender por qué `PasswordPolicy` es un único archivo | [`E-04-07`](./explanation/07-password-policy-single-source-truth.md) |
| Entender el trigger anti-ciclos y el diagnóstico de arranque | [`E-04-08`](./explanation/08-anti-ciclos-jerarquia.md) |
| Entender rate limiting por IP vs por subject | [`E-04-09`](./explanation/09-rate-limiting-ip-vs-subject.md) |
| Entender por qué se fuerza `es-AR` | [`E-04-10`](./explanation/10-cultura-forzada-es-ar.md) |
| Entender el patrón `Reconstitute` + `InternalsVisibleTo` | [`E-04-11`](./explanation/11-patron-reconstitute-internalsvisibleto.md) |
| Entender los bloques GUID reservados para catálogos | [`E-04-12`](./explanation/12-catalogos-inmutables-bloques-guid.md) |
| Entender cómo usa OpenSpec este repo | [`E-04-13`](./explanation/13-openspec-sdd-en-este-repo.md) |

## Tutorials (4)

Documentos **learning-oriented**. Guían a un newcomer a un primer resultado
exitoso con pasos copy-paste friendly. Asumen contexto cero.

| ID | Título | Cuándo leerlo |
|---|---|---|
| T-01-01 | [Levantar SGV en local y ver la pantalla de SignIn](./tutorials/01-levantar-sistema-local.md) | Primer día en el proyecto |
| T-01-02 | [Hacer tu primera mutación end-to-end](./tutorials/02-primera-mutacion-unidad-organizativa.md) | Después de T-01-01 |
| T-01-03 | [Correr la suite de tests completa](./tutorials/03-correr-suite-tests.md) | Antes de tocar código por primera vez |
| T-01-04 | [Hacer tu primer cambio siguiendo Clean Architecture](./tutorials/04-primer-cambio-clean-architecture.md) | Antes del primer PR |

## How-to guides (12)

Documentos **problem-oriented**. Pasos directos para resolver un problema
concreto. Asumen contexto básico del sistema.

| ID | Título | Problema que resuelve |
|---|---|---|
| H-02-01 | [Diagnosticar ciclos en la jerarquía](./how-to/01-diagnosticar-ciclos-jerarquia.md) | "Hay ciclos en UnidadesOrganizativas" |
| H-02-02 | [Operar el flujo de recuperación de contraseña](./how-to/02-operar-flujo-recuperacion-contrasena.md) | Forzar un reset end-to-end en dev/staging |
| H-02-03 | [Rotar el `Jwt:SigningKey` sin tumbar sesiones](./how-to/03-rotar-jwt-signing-key.md) | Secret filtrado o incidente de seguridad |
| H-02-04 | [Bloquear y desbloquear un usuario](./how-to/04-bloquear-desbloquear-usuario.md) | Mitigar ataque de fuerza bruta |
| H-02-05 | [Agregar una nueva migración EF Core](./how-to/05-agregar-migracion-ef-core.md) | Cambio de schema nuevo |
| H-02-06 | [Configurar `AllowedOrigins` para producción](./how-to/06-configurar-allowed-origins-produccion.md) | Salir del fallback permisivo de Development |
| H-02-07 | [Levantar MySQL local con Docker para tests `[MySqlFact]`](./how-to/07-levantar-mysql-docker-para-tests.md) | Dev sin MySQL local |
| H-02-08 | [Auditar quién modificó una entidad](./how-to/08-auditar-quien-modifico-entidad.md) | Dato cambiado sin explicación |
| H-02-09 | [Crear un nuevo catálogo inmutable con bloque GUID](./how-to/09-crear-catalogo-inmutable-bloque-guid.md) | Pedir bloque para catálogo nuevo |
| H-02-10 | [Forzar el setup inicial](./how-to/10-forzar-setup-inicial.md) | Sistema nuevo sin admin creado |
| H-02-11 | [Ejecutar solo los tests de un módulo](./how-to/11-ejecutar-tests-de-un-modulo.md) | Iteración rápida sobre un módulo |
| H-02-12 | [Configurar SMTP real (no `Logger`)](./how-to/12-configurar-smtp-real.md) | Salir del modo Logger en staging/prod |

## Reference (12)

Documentos **information-oriented**. Catálogos del machinery: APIs, esquemas,
contratos, opciones. Sin prosa narrativa, tablas dominantes.

| ID | Título | Qué cataloga |
|---|---|---|
| R-03-01 | [Mapa de APIs HTTP](./reference/01-mapa-apis-http.md) | Los 18 controllers, ~60 endpoints |
| R-03-02 | [Esquema de base de datos](./reference/02-esquema-base-de-datos.md) | Tablas, columnas, índices, triggers |
| R-03-03 | [Wire-types (SGV.Contracts)](./reference/03-wire-types-contracts.md) | Records, DTOs, enums, constantes |
| R-03-04 | [Roles y matriz de autorización](./reference/04-roles-matriz-autorizacion.md) | Rol × endpoint × acción |
| R-03-05 | [Configuración: opciones y secretos](./reference/05-configuracion-opciones-secretos.md) | `Jwt`, `RefreshToken`, `Smtp`, `SgvApi`, `AllowedOrigins`, etc. |
| R-03-06 | [Pipeline middleware de SGV.Api](./reference/06-pipeline-middleware-api.md) | Orden y propósito de cada middleware |
| R-03-07 | [Pipeline de arranque de SGV.Web](./reference/07-pipeline-arranque-web.md) | HttpClients tipados, BFF, middleware |
| R-03-08 | [Catálogos inmutables y bloques GUID](./reference/08-catalogos-inmutables-bloques-guid.md) | Niveles, tipos, estados, categorías |
| R-03-09 | [Health checks](./reference/09-health-checks.md) | `/health/live`, `/health/ready` |
| R-03-10 | [Taxonomía de errores](./reference/10-taxonomia-errores.md) | `ErrorCategoria` → HTTP status |
| R-03-11 | [Tabla de migraciones EF Core](./reference/11-tabla-migraciones-ef-core.md) | Cronología de las 22 migraciones |
| R-03-12 | [Especificaciones OpenSpec vigentes](./reference/12-especificaciones-openspec-vigentes.md) | Índice de los 53 specs |

## Explanation (13)

Documentos **understanding-oriented**. Discuten el "por qué" de las
decisiones de diseño. Sin pasos numerados; prosa reflexiva, admite diagramas.

| ID | Título | Concepto |
|---|---|---|
| E-04-01 | [Clean Architecture: dos composition roots](./explanation/01-clean-architecture-dos-composition-roots.md) | Por qué `Api` y `Web` no comparten proyecto |
| E-04-02 | [Bridge cookie → JWT](./explanation/02-bridge-cookie-jwt.md) | El `ApiBearerTokenHandler` |
| E-04-03 | [Auditoría transversal con `SaveChangesInterceptor`](./explanation/03-auditoria-transversal-savechanges-interceptor.md) | Cómo EF captura `OldValuesJson`/`NewValuesJson` |
| E-04-04 | [Columnas generadas para unicidad activa](./explanation/04-columnas-generadas-unicidad-activa.md) | Convivencia con soft-delete |
| E-04-05 | [Máquina de estados de Vacantes y `ActivePuestoIdUnique`](./explanation/05-maquina-estados-vacantes.md) | Transiciones y constraint |
| E-04-06 | [Refresh tokens single-use y detección de replay](./explanation/06-refresh-tokens-single-use-replay.md) | Ciclo de vida y revocación de familia |
| E-04-07 | [`PasswordPolicy` como single source of truth](./explanation/07-password-policy-single-source-truth.md) | Un archivo, cinco consumidores |
| E-04-08 | [Anti-ciclos en la jerarquía](./explanation/08-anti-ciclos-jerarquia.md) | Trigger MySQL + diagnóstico de arranque |
| E-04-09 | [Rate limiting por IP vs por subject](./explanation/09-rate-limiting-ip-vs-subject.md) | Tradeoffs de partition key |
| E-04-10 | [Cultura forzada `es-AR` y contrato HTTP invariante](./explanation/10-cultura-forzada-es-ar.md) | Por qué el wire JSON no cambia |
| E-04-11 | [Patrón `Reconstitute` + `InternalsVisibleTo`](./explanation/11-patron-reconstitute-internalsvisibleto.md) | Hidratación desde EF sin reflexión |
| E-04-12 | [Catálogos inmutables con bloques GUID](./explanation/12-catalogos-inmutables-bloques-guid.md) | Estabilidad entre reinicios |
| E-04-13 | [OpenSpec/SDD en este repositorio](./explanation/13-openspec-sdd-en-este-repo.md) | `strict_tdd` y el ciclo proposal → archive |

## Cómo contribuir

1. **Clasificá tu cambio** según Diátaxis: ¿es learning, problem-solving,
   information o understanding? Eso determina el cuadrante.
2. **Reutilizá el patrón del cuadrante**: título, prólogo, estructura interna.
   Mirá un par de docs del mismo cuadrante antes de empezar.
3. **Verificá contra código**: cada comando, ruta o flag debe respaldarse en
   código. Si no podés verificar algo, dejá `> ⚠️ A verificar: <motivo>`.
4. **Cross-links**: agregá links relativos a otros docs al final bajo
   `## Referencias`. No uses `(pendiente)` si el destino ya existe.
5. **No commitees caracteres CJK** ni otros scripts mezclados en español.
   Antes de commitear, ejecutá:

   ```bash
   perl -nle 'print if /\p{Han}/' docs/**/*.md
   ```

   (Debe retornar vacío.)
6. **Diátaxis strict**: los reference docs no llevan pasos numerados; los
   tutoriales no llevan tablas de referencia exhaustiva; los how-to no llevan
   prosa reflexiva larga. Si tu doc no encaja en el cuadrante que elegiste,
   probablemente elegiste mal.

## Estado de la documentación

- **41 documentos** generados: 4 tutorials + 12 how-to + 12 reference + 13 explanation.
- **6 101 líneas** totales en `docs/`.
- **Cobertura**: todas las áreas principales del código están documentadas
  (arquitectura, auth, auditoría, módulos de negocio, infraestructura,
  despliegue, configuración).
- **Marcadores `⚠️ A verificar`**: cada uno está justificado en el doc donde
  aparece; si confirmás que la información es correcta, podés editarlo a una
  afirmación directa.

## Glosario

- **Composition root**: el proyecto que monta la aplicación (en SGV, `Api` y
  `Web` son dos composition roots paralelos).
- **Wire-type**: un record/DTO que cruza la frontera HTTP (vive en
  `SGV.Contracts`).
- **`Reconstitute`**: factoría `internal static` en cada agregado de Dominio
  que permite a EF rehidratar el agregado sin usar reflexión.
- **Refresh family**: la cadena de refresh tokens que descienden del mismo
  login inicial; un replay revoca la familia completa.
- **Catálogo inmutable**: tabla sembrada con datos que no mutan en runtime
  (niveles, tipos, estados). Usa bloques GUID reservados para garantizar
  estabilidad entre reinicios.
- **`SaveChangesInterceptor`**: hook de EF Core que intercepta cada
  `SaveChanges` para escribir filas de auditoría.
