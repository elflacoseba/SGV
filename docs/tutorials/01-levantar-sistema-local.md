# T-01-01 — Levantar SGV en local y ver la pantalla de SignIn

**Qué vas a lograr:** clonar el repositorio, compilar la solución, arrancar
`SGV.Api` y `SGV.Web` en paralelo, y terminar viendo el formulario de inicio de
sesión en el navegador. Si la base está vacía, el propio front te redirige a la
pantalla de configuración inicial del primer administrador.

---

## Prerrequisitos

1. **.NET SDK 10.0.300 o superior.** El repositorio fija la versión en
   `global.json` con `rollForward: latestMajor`. Verificá con
   `dotnet --version`.
2. **Bun 1.3+** para el pipeline de assets del frontend. Instalación:
   <https://bun.sh>.
3. **MySQL 8.0** accesible en `localhost:3306` (usuario `root` sin password
   alcanza para desarrollo local). MariaDB también funciona.
   Alternativa rápida con Docker:
   `docker run --name sgv-mysql -e MYSQL_ALLOW_EMPTY_PASSWORD=yes -p 3306:3306 -d mysql:8.0`.
4. **Git** para clonar.

---

## Paso 1 — Clonar y restaurar

```bash
git clone <url-del-repo> SGV
cd SGV
dotnet restore SGV.slnx
```

**Verificación:** la restauración termina sin errores. `global.json` fuerza el
SDK `10.0.300`; cualquier SDK anterior aborta el comando.

---

## Paso 2 — Compilar la solución

```bash
dotnet build SGV.slnx
```

**Verificación:** `Build succeeded. 0 Error(s)`. Los siete proyectos
(`Dominio`, `Aplicacion`, `Infraestructura`, `Contracts`, `Api`, `Web`,
`Tests`) compilan en orden de dependencias.

---

## Paso 3 — Configurar secretos locales

Cada proyecto tiene su propio `UserSecretsId` (ver `src/SGV.Api/SGV.Api.csproj`
y `src/SGV.Web/SGV.Web.csproj`). Configurá ambos:

```bash
# API
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/SGV.Api
dotnet user-secrets set "ConnectionStrings:SgvDatabase" "server=localhost;database=sgv;user=root;password=;" --project src/SGV.Api

# Web (la misma SigningKey que la API; es el secreto compartido del issuer)
dotnet user-secrets set "Jwt:SigningKey" "<mismo-valor-que-arriba>" --project src/SGV.Web
```

> ⚠️ A verificar: la clave `Jwt:SigningKey` debe ser exactamente la misma en
> `SGV.Api` y `SGV.Web`. Si difieren, la API rechaza los bearer tokens que
> emite la Web. `Program.cs` de la Web exige ≥ 32 bytes UTF-8
> (`Validate(o => ... GetByteCount(o.SigningKey) >= 32, ...)`).

Alternativa con variables de entorno (mismo efecto):

```bash
export ConnectionStrings__SgvDatabase="server=localhost;database=sgv;user=root;password=;"
export Jwt__SigningKey="<mismo-valor-que-arriba>"
```

---

## Paso 4 — Crear la base y aplicar migraciones

El proyecto `SGV.Infraestructura` es el dueño de las migraciones EF Core.
Aplicá todas contra tu MySQL local con la herramienta `dotnet ef`:

```bash
dotnet ef database update \
  --project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj
```

**Verificación:** el comando termina con `Applying migration ...20260819223914_AddRefreshTokens`
(o el último disponible en `src/SGV.Infraestructura/Persistencia/Migraciones/`).
La base `sgv` queda creada y todas las tablas (`Personas`, `Cargos`, etc.)
junto con los catálogos seed.

Si preferís un script SQL único en lugar de la herramienta:

```bash
dotnet ef migrations script --idempotent \
  --project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --output docs/migracion-inicial-sgv.sql
mysql -uroot < docs/migracion-inicial-sgv.sql
```

---

## Paso 5 — Levantar la API

En una terminal:

```bash
dotnet run --project src/SGV.Api
```

**Verificación:** la consola imprime algo como
`Now listening on: https://localhost:7160` y `http://localhost:5160`
(puertos definidos en `src/SGV.Api/Properties/launchSettings.json`).
Swagger queda expuesto en <https://localhost:7160/swagger> cuando
`ASPNETCORE_ENVIRONMENT=Development`. El endpoint
`GET https://localhost:7160/health/ready` debe devolver 200 sólo si MySQL
responde; `GET /health/live` responde 200 siempre.

---

## Paso 6 — Levantar la Web

En otra terminal, desde la raíz del repo:

```bash
dotnet run --project src/SGV.Web
```

**Verificación:** la consola imprime
`Now listening on: http://localhost:5266` (perfil `http`) o
`https://localhost:7298` (perfil `https`), según el perfil seleccionado por
`src/SGV.Web/Properties/launchSettings.json`. Navegá a
<http://localhost:5266>.

---

## Paso 7 — Crear el primer Administrador

Como la tabla `AspNetUsers` está vacía, `SGV.Web` detecta `RequiresSetup=true`
en el endpoint `/api/v1/setup` y te redirige automáticamente a
`/auth/setup` (ver `src/SGV.Web/Pages/Auth/SignIn.cshtml.cs`).

1. Completá el formulario con tus datos reales (nombre, apellido, email, nombre
   de usuario, contraseña) y un tipo de documento opcional.
2. Hacé clic en **Crear Administrador**.

**Verificación:** la pantalla hace PRG a `/auth/sign-in` con un banner verde
que dice "Configuración inicial completada". La API emite un JWT para tu
usuario (rol `Administrador`).

> ⚠️ A verificar: si la Web queda en blanco o devuelve 502, es probable que la
> Web no esté apuntando a la URL correcta de la API. Revisá
> `src/SGV.Web/appsettings.Development.json`: `SgvApi:BaseUrl` debe coincidir
> con el puerto de la API (en `launchSettings.json` la API expone
> `https://localhost:7160`).

---

## Paso 8 — Iniciar sesión

1. Navegá a <http://localhost:5266/auth/sign-in> (o `/`).
2. Ingresá el usuario y la contraseña del paso anterior.
3. Hacé clic en **Iniciar sesión**.

**Verificación:** el navegador te redirige a `/`. El frontend Razor ya tiene
tu cookie de autenticación y reenvía el JWT a la API en cada request saliente
vía `ApiBearerTokenHandler`.

> ✅ Si ves el listado de unidades organizativas en
> `/organizacion/unidades-organizativas`, todo el stack está conectado.

---

## Próximos pasos

- **T-01-02** — Hacer tu primera mutación end-to-end: crear una unidad
  organizativa y verificar la fila de auditoría.
- [R-03-05](../reference/05-configuracion-opciones-secretos.md) —
  Referencia completa de opciones, secretos y variables de entorno equivalentes.
- [E-04-02](../explanation/02-bridge-cookie-jwt.md) — Cómo encajan cookie
  auth, JWT y `ApiBearerTokenHandler` en el shell web.
- [H-02-07](../how-to/07-levantar-mysql-docker-para-tests.md) —
  Cómo apuntar la Web y la API a un MySQL externo mediante
  `ConnectionStrings__SgvDatabase`.
