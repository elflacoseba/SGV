# SGV — Sistema de Gestión de Vacantes

> **v1.0 (release candidate)** — primera versión lista para desplegar a producción.

Solución .NET 10 con **Clean Architecture**, ASP.NET Core API + Razor Pages,
EF Core 9 y MySQL 8 / MariaDB para gestionar el ciclo de vida completo de
vacantes, unidades organizativas, cargos, habilidades y personas.

`SGV.Api` autentica con **JWT bearer**; `SGV.Web` es una shell Razor Pages
basada en Inspinia Starterkit que autentica con **cookies** y reenvía el
token a la API vía `ApiBearerTokenHandler`.

---

## Stack

| Capa | Tecnología |
|---|---|
| Runtime | .NET 10 (`net10.0`), SDK `10.0.300`, C# 14 |
| Web API | ASP.NET Core + Swashbuckle |
| Web Shell | ASP.NET Core Razor Pages + Inspinia |
| Persistencia | EF Core 9 + Pomelo MySQL 9.0.0 (MySQL 8 / MariaDB) |
| Identidad | ASP.NET Core Identity con clave string, vinculado 1:1 a `Personas` |
| Validación | FluentValidation |
| Auth tokens | JWT bearer (API) + Refresh tokens con **family tracking** |
| Frontend assets | Bun + Gulp + Google Charts OrgChart |
| Testing | xUnit 2.9.2 + `[MySqlFact]` (skip limpio sin MySQL local) |
| CI | GitHub Actions contra MySQL 8 (`mysql:8.0`) |

---

## Arquitectura

Clean Architecture estricta. Grafo de proyectos:

```
Dominio ──► Aplicacion ──► Infraestructura ──► Api (composition root)
                  ▲                              ▲
                  └────── Contracts ◄───────────┘   (wire types compartidos)
                                                    ▲
                                                    └── Web (NO referencia Api)
```

- `SGV.Dominio` — entidades, value objects y reglas de negocio.
- `SGV.Aplicacion` — casos de uso, puertos (interfaces), validaciones y servicios.
  Solo depende de `Dominio` y `Contracts`. **No conoce EF Core ni HTTP**.
- `SGV.Infraestructura` — EF Core, Identity, repositorios, interceptor de
  auditoría, migraciones, SMTP, Unit of Work.
- `SGV.Contracts` — **wire-types compartidos** entre Api y Web (records/DTOs
  de request/response/result + constantes). Es una **leaf** del grafo.
- `SGV.Api` — controladores HTTP, autenticación JWT, Swagger, composición.
- `SGV.Web` — frontend Razor Pages + clientes tipados `HttpClient` hacia
  `SGV.Api` (`src/SGV.Web/Integration/`). Solo depende de `Contracts` y un
  único helper compartido (`HealthCheckResponseWriter.cs`) por `<Compile Include>`.

Ver `docs/decisiones-implementacion.md` para el detalle de decisiones técnicas
vigentes y `AGENTS.md` para convenciones del repositorio.

---

## Estructura del repositorio

```
SGV/
├── SGV.slnx                              # solución
├── global.json                            # fija SDK 10.0.300
├── AGENTS.md                              # guía del repo (en español)
├── docs/
│   ├── decisiones-implementacion.md      # decisiones técnicas vigentes
│   ├── migracion-inicial-sgv.sql         # script idempotente MySQL 8
│   ├── migracion-inicial-sgv-mariadb.sql # script lineal MariaDB
│   └── script-listar-ciclos-jerarquia-unidades-organizativas.sql
├── openspec/
│   ├── config.yaml                        # configuración SDD/OpenSpec
│   ├── specs/                             # specs vigentes (delta specs)
│   └── changes/                           # cambios aplicados (archivados)
├── src/
│   ├── SGV.Dominio/
│   ├── SGV.Aplicacion/
│   ├── SGV.Infraestructura/Persistencia/Migraciones/  # 21 migraciones EF
│   ├── SGV.Contracts/
│   ├── SGV.Api/
│   └── SGV.Web/
└── tests/SGV.Tests/                       # unit + integration + smoke
```

---

## Setup local

### Prerrequisitos

- **.NET SDK 10.0.300** (fijado en `global.json`; usar `rollForward: latestMajor`).
- **Bun ≥ 1.3.14** (solo si vas a tocar assets frontend).
- **MySQL 8.x** (recomendado) o **MariaDB 10.11+** corriendo localmente.
  - Si tenés `root` sin password en `localhost:3306`, los tests `[MySqlFact]`
    corren automáticamente contra `sgv_test`.
  - Si no, los tests se skipean limpiamente.

### 1. Restaurar y compilar

```bash
dotnet restore
dotnet build SGV.slnx
```

### 2. Dependencias frontend (opcional, solo si modificás assets)

```bash
cd src/SGV.Web
bun install        # o `bun ci` para reproducibilidad
bun run build      # genera el bundle para validación local
```

> CI verifica que `bun.lock` y `wwwroot` estén commiteados al día:
> `git diff --exit-code -- bun.lock wwwroot`.

### 3. Secretos locales (obligatorio)

Antes del primer `dotnet run` de `SGV.Api`:

```bash
# Clave JWT (mínimo 32 bytes aleatorios)
dotnet user-secrets set "Jwt:SigningKey" "<random ≥32 bytes>" \
  --project src/SGV.Api

# Connection string MySQL/MariaDB
dotnet user-secrets set "ConnectionStrings:SgvDatabase" \
  "Server=localhost;Port=3306;Database=sgv;User=root;Password=<pwd>;" \
  --project src/SGV.Api
```

> Las claves con `:` en `user-secrets` se tipean **sin espacios**.
>
> Alternativa por variable de entorno: `ConnectionStrings__SgvDatabase`.

> El `appsettings.Development.json` de `SGV.Api` trae un placeholder
> (`DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD-...`) y un ServerVersion `8.4.11` como
> ayuda de primer arranque. **Nunca** lo uses en producción. Reemplazá
> `Jwt:SigningKey` con `user-secrets` antes de cualquier uso real.

### 4. Crear la base de datos local

```bash
mysql -uroot -p -e "CREATE DATABASE sgv CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
mysql -uroot -p sgv < docs/migracion-inicial-sgv.sql
```

> Si usás MariaDB, cambiá el script por `docs/migracion-inicial-sgv-mariadb.sql`
> (lineal, aplicar **una sola vez** sobre DB vacía).

### 5. Levantar la solución

```bash
# API en https://localhost:7160 (ver appsettings.Development.json)
dotnet run --project src/SGV.Api

# Web shell en https://localhost:7298 (en otra terminal)
dotnet run --project src/SGV.Web
```

Swagger UI queda disponible en `https://localhost:7160/swagger` (solo Development).

### 6. Bootstrap del primer Administrador (one-time)

Cuando la base está vacía (`AspNetUsers` sin filas), la API habilita un
endpoint anónimo para crear el primer Administrador:

```bash
# Verificar estado
curl -k https://localhost:7160/api/v1/setup/status

# Crear admin inicial
curl -k -X POST https://localhost:7160/api/v1/setup \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "admin",
    "email": "admin@example.com",
    "password": "<Pwd inicial!>",
    "nombres": "Ana",
    "apellidos": "Administradora",
    "tipoDocumento": "DNI",
    "numeroDocumento": "00000000"
  }'
```

El endpoint `POST /api/v1/setup` está rate-limited (5 req / 15 min). Una vez
que existe al menos un usuario, retorna **409 Conflict** y queda deshabilitado
de forma permanente. Ver `openspec/specs/setup-initial-admin/spec.md` (REQ-SETUP-001..003).

---

## Tests

```bash
# Suite completa (los [MySqlFact] se skipean si no hay MySQL local)
dotnet test SGV.slnx

# Solo un subset
dotnet test SGV.slnx --filter "FullyQualifiedName~Vacantes"
dotnet test SGV.slnx --filter "FullyQualifiedName~DatosSemilla"

# Con cobertura
dotnet test SGV.slnx --collect:"XPlat Code Coverage"
```

Convenciones del repo:

- Los tests `[MySqlFact]` aplican `Database.Migrate()` una vez por sesión.
- El factory de tests (`TestSgvDbContextFactory`) resuelve la connection string
  vía `ConnectionStrings__SgvDatabase`, archivos de configuración o defaults.
- Tests de seed (`DatosSemilla_*_SeedIdsMatchConstantes`) detectan drift entre
  los seeds hardcoded del script inicial y `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs`.

> Si tu MySQL local usa otro puerto/usuario/password, seteá
> `ConnectionStrings__SgvDatabase` antes de correr la suite.

---

## Deploy a Producción

### 1. Elegí el motor de base de datos

| Motor | Script | Notas |
|---|---|---|
| **MySQL 8.x** (recomendado) | `docs/migracion-inicial-sgv.sql` | Idempotente. Cubre las 21 migraciones EF Core. Aplica con `--idempotent`. |
| **MariaDB 10.11+** | `docs/migracion-inicial-sgv-mariadb.sql` | Lineal (no idempotente). Aplicar **una sola vez** sobre DB vacía. Usa collation `utf8mb4_unicode_ci` y columnas generadas `STORED` (MariaDB no soporta `VIRTUAL` indexadas con `UNIQUE`). |

Ambos scripts:

- Crean la tabla `__EFMigrationsHistory` y aplican las 21 migraciones.
- Insertan los **datos semilla** de los catálogos inmutables
  (`AspNetRoles`, `NivelesHabilidad`, `EstadosVacante`, `EstadosPostulacion`,
  `NivelesCargo`, `Cargos`, `Habilidades`, `CategoriasHabilidad`,
  `TiposUnidadOrganizativa`, `TiposDocumento`).
- Incluyen el **trigger anti-ciclos** sobre `UnidadesOrganizativas` (issue #277)
  y el **fix de encoding UTF-8** para `EstadosVacante.Nombre = 'En Selección'`
  (issue #273).
- Terminan con `COMMIT;`.

> **Validación recomendada antes de aplicar en producción:**
>
> ```bash
> # Smoke-test contra una DB MySQL 8 efímera
> docker run --rm -d --name mysql-test \
>   -e MYSQL_ROOT_PASSWORD=test -e MYSQL_DATABASE=sgv_test \
>   -p 3306:3306 mysql:8.0
> sleep 30  # esperar healthcheck
> mysql -h 127.0.0.1 -uroot -ptest sgv_test < docs/migracion-inicial-sgv.sql
> mysql -h 127.0.0.1 -uroot -ptest -e "SHOW TABLES;" sgv_test | wc -l   # ~30+
> mysql -h 127.0.0.1 -uroot -ptest -e "SELECT MigrationId FROM __EFMigrationsHistory;" sgv_test
> docker stop mysql-test
> ```

### 2. Secretos de producción (obligatorio)

> **Nunca** commitees credenciales reales. **Nunca** subas el `Jwt:SigningKey`
> de producción a `appsettings.json`.

Tres opciones, en orden de preferencia:

| Opción | Cómo |
|---|---|
| **Variables de entorno** (recomendado en containers) | `Jwt__SigningKey`, `ConnectionStrings__SgvDatabase`, `Smtp__*`, `AllowedOrigins__0`, etc. |
| **Azure Key Vault / AWS Secrets Manager / HashiCorp Vault** | Inyectar vía provider de configuración al arranque. |
| **`dotnet user-secrets`** | Solo desarrollo. No usar en prod. |

Variables mínimas:

| Variable | Descripción |
|---|---|
| `Jwt__SigningKey` | Firma de tokens. Mínimo 32 bytes aleatorios. **Rotar periódicamente.** |
| `ConnectionStrings__SgvDatabase` | `Server=...;Port=3306;Database=sgv;User=...;Password=...;Allow User Variables=True;Default Command Timeout=60;` |
| `Smtp__Mode` | `Logger` (dev) / `Smtp` (prod) |
| `Smtp__Host`, `Smtp__Port`, `Smtp__UserName`, `Smtp__Password` | Si `Mode=Smtp` |
| `Smtp__FromAddress`, `Smtp__FromName`, `Smtp__WebBaseUrl` | Plantillas de email |
| `AllowedOrigins__0`..`AllowedOrigins__N` | Orígenes CORS permitidos (issue #101, endurecido por ambiente) |

### 3. Bootstrap del admin inicial en producción

Repetir el paso 6 del setup local contra la URL de producción
(`https://api.example.com/api/v1/setup`). **Rate-limited** a 5 req / 15 min
para mitigar fuerza bruta. Tras el primer usuario el endpoint queda cerrado
(409). Cambiá la contraseña en el primer login.

### 4. Frontend

`SGV.Web` es una shell Razor Pages con assets precompilados. CI verifica que
`src/SGV.Web/bun.lock` y `src/SGV.Web/wwwroot` estén commiteados al día, así
que un deploy estándar de la solución cubre el frontend.

---

## Roles

Catálogo fijo, definido en `src/SGV.Contracts/Seguridad/RolesSgv.cs`:

| Rol | Alcance |
|---|---|
| **Administrador** | Acceso total. Único rol que puede crear/editar/eliminar cargos, habilidades, unidades organizativas y usuarios. Único rol con acceso al módulo de **Auditoría**. |
| **GestorVacantes** | CRUD de vacantes, postulantes, postulaciones, evaluaciones. Crear/editar personas y puestos. |
| **Consultor** | Solo lectura sobre unidades organizativas, puestos, personas, cargos, habilidades, vacantes (cerradas/abiertas) y auditoría (filtrada). |

Los write de cargos, habilidades y usuarios están protegidos por
`[Authorize(Roles = "Administrador")]`. Ver `openspec/specs/identity-user-role-management/spec.md`.

> **Migración histórica:** los roles legacy `RecursosHumanos`,
> `GestorOrganizacional`, `EvaluadorSeleccion`, `Lector` se eliminaron
> definitivamente en la migración `20260621202540_VincularIdentityUsuariosAPersonas`.
> El path release-ready es el catálogo actual de 3 roles.

---

## Decisiones técnicas que NO conviene romper

- **MySQL 8 / MariaDB** como proveedores soportados. No introducir supuestos
  de SQL Server.
- **Unicidad sobre registros activos** con columnas generadas (`CASE WHEN`).
- **Identity con clave string** y `PersonaId` como FK única obligatoria.
- **Auditoría centralizada** vía interceptor EF Core
  (`AuditoriaSaveChangesInterceptor`) en la tabla `Auditorias`.
- **`SGV.Api` valida auth solo con bearer token**; `SGV.Web` depende del
  bridge por cookie + `ApiBearerTokenHandler`.
- **Listados segmentados** de cargos, habilidades y unidades organizativas
  usan `status=activas|eliminadas`.
- **Organigrama** visualizado con Google OrgChart.
- **Refresh tokens** con family tracking (revocación atómica de la familia
  ante replay).
- **Triggers anti-ciclos** en `UnidadesOrganizativas` (defensa en
  profundidad a nivel DB; la capa de app traduce la violación a
  `409 CicloJerarquico`).
- **CORS endurecido por ambiente** (issue #101).

Ver `docs/decisiones-implementacion.md` para el detalle completo (D-1..D-N).

---

## Mapa de bloques GUID para catálogos inmutables

Convención vigente. **No** reasignar bloques existentes; pedir uno nuevo si
hace falta.

| Bloque | Catálogo |
|---|---|
| `10000000-…` | `NivelesHabilidad` |
| `20000000-…` | `EstadosVacante` |
| `30000000-…` | `EstadosPostulacion` |
| `40000000-…` | `Cargos` |
| `50000000-…` | `Habilidades` |
| `60000000-…` | `TiposUnidadOrganizativa` |
| `70000000-…` | `NivelesCargo` |
| `71000000-…` | `TiposDocumento` |
| `72000000-…` | `CategoriasHabilidad` |

---

## Catálogos pre-cargados (semilla)

Los scripts de migración insertan el siguiente set mínimo:

- **5 roles de Identity** → tras la migración D7, quedan **3 vigentes** (ver Roles).
- **4 niveles de habilidad**: Básico, Intermedio, Avanzado, Experto.
- **4 estados de vacante**: Abierta, En Selección, Cubierta, Cancelada.
- **6 estados de postulación**: Postulado, Preseleccionado, Entrevistado,
  Aprobado, Rechazado, Contratado.
- **4 niveles de cargo**: Directivo, Conducción Media, Operativo, Académico.
- **6 cargos iniciales**: Decano, Secretario, Director, Jefe de Departamento,
  Administrativo, Profesor.
- **7 habilidades iniciales**: Liderazgo, Gestión de Personal, SQL Server,
  EF Core, Programación .NET, Administración Pública, Docencia Universitaria.
- **4 categorías de habilidad**: Conducción, Técnica, Dominio, Académica.
- **20 tipos de unidad organizativa**: Institución, Facultad, Secretaría,
  Dirección, Departamento, División, Área, Sede, Región, Gerencia,
  Vicepresidencia, Subgerencia, Coordinación, Sección, Oficina, Equipo,
  Célula, Planta, Sucursal, Escuela.
- **4 tipos de documento**: DNI, LE, LC, Pasaporte.

---

## CI

`.github/workflows/ci.yml` corre en cada PR y push a `develop`/`main`:

1. `bun ci` + `bun audit --audit-level=high` + `bun run build` (frontend).
2. `git diff --exit-code -- bun.lock wwwroot` (verifica que el bundle esté commiteado).
3. `dotnet restore` + `dotnet build --configuration Release`.
4. `dotnet test --configuration Release` contra `mysql:8.0` real
   (`sgv_test`), usando el secret `JWT_SIGNING_KEY`.

---

## Troubleshooting

| Síntoma | Causa probable | Fix |
|---|---|---|
| `'ConnectionStrings:SgvDatabase' not found` al correr `dotnet ef` | Falta secreto/variable de entorno | `dotnet user-secrets set "ConnectionStrings:SgvDatabase" "..."` o `export ConnectionStrings__SgvDatabase=...` |
| `Unknown collation: 'utf8mb4_0900_ai_ci'` al correr el script MariaDB | Estás corriendo el script MySQL 8 contra MariaDB | Usar `docs/migracion-inicial-sgv-mariadb.sql` (transforma a `utf8mb4_unicode_ci`) |
| `Cannot index a virtual generated column` (MariaDB) | Mismo problema, falta STORED | Usar el script MariaDB específico |
| `Duplicate entry for key 'IX_AspNetUsers_PersonaId'` al crear el segundo admin | Reaplicación del script de setup | El endpoint `/setup` es one-time (409 si ya hay usuarios). Crear el segundo admin vía endpoint normal `POST /api/v1/usuarios`. |
| `CicloJerarquico` al asignar una `UnidadPadreId` | Trigger anti-ciclos detecta referencia cíclica en la jerarquía de unidades organizativas | La capa de app traduce a `409`. Resolver el ciclo a nivel de árbol. Ver `docs/script-listar-ciclos-jerarquia-unidades-organizativas.sql` para diagnóstico. |
| `[MySqlFact]` tests se skipean en CI local | No hay MySQL en `localhost:3306` con `root` sin password | Setear `ConnectionStrings__SgvDatabase` o levantar MySQL local |
| `Entity 'Persona' has a `PersonaId` that conflicts with the existing FK` al regenerar migraciones | Drift entre modelo y migraciones históricas | **Nunca** borrar migraciones aplicadas en prod. Crear una nueva migración aditiva. |
| Encoding de `EstadosVacante.Nombre = 'En SelecciÃ³n'` | Mojibake clásico UTF-8/Latin-1 en filas pre-existentes | La migración `20260813120000_FixEstadoVacanteEnSeleccionEncoding` lo corrige idempotentemente (incluida en ambos scripts de inicial). |

---

## Documentación adicional

- `AGENTS.md` — guía operativa del repo (en español).
- `docs/decisiones-implementacion.md` — bitácora vigente de decisiones técnicas.
- `openspec/specs/` — delta specs (requisitos + escenarios Given/When/Then).
- `openspec/changes/archive/` — historial de cambios aplicados (referencia).
- `InspinaTemplate/` — template de referencia importado para la shell web.
- `.github/workflows/ci.yml` — pipeline de CI activo.

---

## Licencia

Privado / propietario. Todos los derechos reservados.
