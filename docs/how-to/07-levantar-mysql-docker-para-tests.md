# H-02-07 — Levantar MySQL local con Docker para que los `[MySqlFact]` corran

Sin MySQL local accesible, los tests marcados con `[MySqlFact]` o `[MySqlTheory]` se skipean y la cobertura de persistencia queda afuera. Este how-to levanta un MySQL 8.0 con Docker, le apunta la connection string y vuelve a correr la suite completa.

---

## Prerrequisitos

- Docker Desktop (o Docker Engine en Linux) instalado y corriendo.
- Puerto 3306 libre en `localhost` (o cambiá el mapeo si tenés otro MySQL corriendo).
- SDK .NET 10 con `dotnet test` disponible.

---

## Paso 1 — Crear y arrancar el contenedor

```bash
docker run --name sgv-mysql-test -d \
  -e MYSQL_ROOT_PASSWORD=sgv_test_pwd \
  -e MYSQL_DATABASE=sgv_test \
  -p 3306:3306 \
  --health-cmd="mysqladmin ping -h localhost" \
  --health-interval=10s \
  --health-timeout=5s \
  --health-retries=10 \
  mysql:8.0
```

El bloque `--health-*` espeja el `services.mysql.options` del job `build-and-test` de `.github/workflows/ci.yml` para que el comportamiento local sea idéntico al de CI.

**Verificación:** `docker ps` muestra el contenedor con `STATUS: Up` y `(healthy)`. `docker logs sgv-mysql-test` emite `ready for connections` cuando MySQL acepta autenticaciones.

---

## Paso 2 — Setear la connection string para la suite

`MySqlTestDatabaseBootstrap.GetAvailability()` cachea la disponibilidad por sesión de test, así que basta con que la variable esté presente cuando arranca el proceso de test:

```bash
export ConnectionStrings__SgvDatabase="Server=127.0.0.1;Port=3306;Database=sgv_test;User=root;Password=sgv_test_pwd;Allow User Variables=True;Default Command Timeout=60"
```

> ⚠️ A verificar: `TestSgvDbContextFactory.ResolveSettings()` resuelve la variable antes que el default. Si exportás una connection string distinta a `Server=localhost;Port=3306;Database=sgv_test;User=root;Password=;`, todos los `[MySqlFact]` apuntan a esa otra.

---

## Paso 3 — Confirmar que la disponibilidad se detecta

`MySqlTestDatabaseBootstrap` corre `Database.CanConnect()` una sola vez por sesión (caché con `Lazy<>`). El primer test `[MySqlFact]` dispara el probe; los siguientes lo ven cacheado.

```bash
dotnet test SGV.slnx --filter "FullyQualifiedName~MySqlFact&FullyQualifiedName~Tests.Persistencia"
```

**Verificación:** el primer test `[MySqlFact]` aplica `Database.Migrate()` (definido en `MySqlFactAttribute`) y crea el schema completo en `sgv_test`. Los subsiguientes tests `[MySqlFact]` corren, no se skipean. La columna `Skipped` del resumen queda reservada a tests marcados con `[Fact(Skip="...")]`.

---

## Paso 4 — Correr la suite completa

```bash
dotnet test SGV.slnx
```

**Verificación:** `Failed: 0` y `Passed: N` (sin contar `Skipped`). El reporte de cobertura con `--collect:"XPlat Code Coverage"` ahora incluye los assemblies ejecutados por los `[MySqlFact]`.

---

## Paso 5 — Apagar el contenedor cuando termines

```bash
docker stop sgv-mysql-test && docker rm sgv-mysql-test
```

**Verificación:** `docker ps -a` no muestra `sgv-mysql-test`. La próxima corrida vuelve a skipear `[MySqlFact]` porque la disponibilidad cacheada se reinicia con el proceso.

---

## Troubleshooting

- **`docker: Error response from daemon: port is already allocated`**: otro MySQL ocupa el 3306 local. O paralo (`brew services stop mysql`) o cambiá el mapeo (`-p 3307:3306`) y exportá `ConnectionStrings__SgvDatabase` con `Port=3307`.
- **Los `[MySqlFact]` siguen apareciendo como `Skipped`**: la variable de entorno no se exportó en la misma shell que ejecuta `dotnet test`. Verificá con `env | grep ConnectionStrings`.
- **`Authentication to host '127.0.0.1' for user 'root' using method 'caching_sha2_password' failed`**: el cliente MySQL es más viejo que 8.0. Actualizá la imagen (`docker pull mysql:8.0`) o cambiá el método de auth con `ALTER USER 'root'@'%' IDENTIFIED WITH mysql_native_password BY 'sgv_test_pwd';`.
- **`MySQL server is not available for persistence tests. ...`**: el contenedor no terminó de arrancar. Esperá el estado `(healthy)` o extendé `--health-start-period=30s`.

---

## Referencias

- `.github/workflows/ci.yml` — `services.mysql` que define el shape del contenedor en CI.
- `tests/SGV.Tests/Persistencia/MySqlFactAttribute.cs` — skip con caché de disponibilidad.
- `tests/SGV.Tests/Persistencia/MySqlTestDatabaseBootstrap.cs` — probe único por sesión.
- `../tutorials/03-correr-suite-tests.md` — flujo completo de la suite con y sin MySQL.
