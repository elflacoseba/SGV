# T-01-03 — Correr la suite de tests completa

**Qué vas a lograr:** ejecutar toda la suite de xUnit, entender el skip limpio
de los `[MySqlFact]` cuando no hay MySQL local, levantar MySQL con Docker para
que corran, y aislar tests por namespace o filtro de clase.

---

## Prerrequisitos

1. Haber completado **T-01-01** (al menos `dotnet restore`).
2. SDK .NET 10 (mismo de `global.json`).
3. **Opcional pero recomendado**: Docker para levantar un MySQL efímero y
   ejecutar los `[MySqlFact]`. Sin Docker, esos tests se skipean y la suite
   igual corre.

---

## Paso 1 — Correr la suite completa

Desde la raíz del repo:

```bash
dotnet test SGV.slnx
```

**Verificación:** el resumen al final muestra dos grandes bloques de tests:

- **Pasaron** (Passed): tests que no requieren MySQL (modelos, mappers,
  validators, servicios con fakes, `[Fact]` y `[Theory]` puros).
- **Skipped**: tests marcados con `[MySqlFact]` o `[MySqlTheory]`. Cada uno
  muestra un mensaje del estilo
  `MySQL server is not available for persistence tests. ...`.

Si la salida final dice `Failed: 0`, todo lo ejecutable pasó. Los `Skipped`
no cuentan como falla.

> ⚠️ A verificar: si `dotnet test` no encuentra los `[MySqlFact]`, asegurate
> de que `tests/SGV.Tests/SGV.Tests.csproj` referencie los proyectos
> correctos y que `xunit.runner.json` esté copiado en el output (csproj
> incluye `<Content Include="xunit.runner.json" CopyToOutputDirectory="PreserveNewest" />`).

---

## Paso 2 — Generar reporte de cobertura

```bash
dotnet test SGV.slnx --collect:"XPlat Code Coverage"
```

**Verificación:** cada proyecto produce un archivo `coverage.cobertura.xml`
bajo `tests/SGV.Tests/TestResults/<guid>/`. La métrica se acumula por
assembly, no por línea de código (formato XPlat de coverlet 6.0.2).

Para producir un reporte HTML legible:

```bash
# Una vez por máquina
dotnet tool install -g dotnet-reportgenerator-globaltool

reportgenerator \
  -reports:"tests/SGV.Tests/TestResults/**/coverage.cobertura.xml" \
  -targetdir:coverage-report \
  -reporttypes:Html
```

El reporte queda en `coverage-report/index.html`. **No** es un objetivo
puntuar alto: la filosofía del repo es "pocos tests significativos", no
maximizar porcentaje (ver `AGENTS.md` §"Filosofía de Testing").

---

## Paso 3 — Levantar MySQL para correr los `[MySqlFact]`

Los `[MySqlFact]` se skipean cuando `Database.CanConnect()` falla. Para
habilitarlos, levantá un MySQL con Docker:

```bash
docker run --name sgv-mysql-test \
  -e MYSQL_ALLOW_EMPTY_PASSWORD=yes \
  -e MYSQL_DATABASE=sgv_test \
  -p 3306:3306 \
  -d mysql:8.0
```

`TestSgvDbContextFactory.LocalDevConnectionString` apunta exactamente a esa
URL por default:

```
Server=localhost;Port=3306;Database=sgv_test;User=root;Password=;
```

Volvé a correr:

```bash
dotnet test SGV.slnx
```

**Verificación:** la columna `Skipped` ahora muestra sólo tests marcados
explícitamente con `[Fact(Skip="...")]` (por ejemplo specs en progreso). Los
`[MySqlFact]` corren contra `sgv_test` y `MySqlTestDatabaseBootstrap`
aplica todas las migraciones automáticamente en la primera invocación.

> ⚠️ A verificar: si tu MySQL local usa otro puerto, usuario o password,
> exportá la variable de entorno antes de los tests:
> `export ConnectionStrings__SgvDatabase="server=localhost;port=3307;database=sgv_test;user=app;password=xxx;"`.
> `TestSgvDbContextFactory.ResolveSettings()` lee esa variable antes que el
> default `LocalDevConnectionString`.

---

## Paso 4 — Aislar un test por namespace o filtro

xUnit acepta filtros por clase totalmente calificada con `-f` (filter) y
`-s` (subnamespace filter).

```bash
# Solo tests del módulo Personas
dotnet test SGV.slnx --filter "FullyQualifiedName~SGV.Tests.Personas"

# Solo el TestContainer de un módulo específico
dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaRepositoryTests"

# Un test puntual por nombre
dotnet test SGV.slnx --filter "DisplayName~Deberia_reactivar_cuando_esta_eliminado"
```

**Verificación:** la salida lista sólo los tests que matchean el filtro y
omite el resto. Útil para iterar sin esperar la suite completa.

> ⚠️ A verificar: la sintaxis de `--filter` cambió entre xUnit v2 y v3.
> Este repo usa xUnit 2.9.2, así que la forma correcta es
> `FullyQualifiedName~SGV.Tests.X` (contains) y los operadores `=`, `!=`,
> `~`, `!~`. Para booleanos múltiples: `DisplayName~foo|FullyQualifiedName~bar`.

---

## Paso 5 — Sólo los `[MySqlFact]` (cuando MySQL está arriba)

Si querés ejecutar **únicamente** la capa de persistencia y dejar los tests
puros para después:

```bash
dotnet test SGV.slnx --filter "FullyQualifiedName~MySqlFact|FullyQualifiedName~MySqlTheory|FullyQualifiedName~Tests.Persistencia"
```

**Verificación:** el resumen muestra sólo tests que efectivamente contactan
MySQL. Si MySQL está caído, todos van a `Skipped`. Si está arriba, todos
corren.

Para limpiar el contenedor MySQL cuando termines:

```bash
docker stop sgv-mysql-test && docker rm sgv-mysql-test
```

---

## Próximos pasos

- **T-01-04** — Hacer tu primer cambio siguiendo Clean Architecture (agregar
  una propiedad opcional a `Persona`).
- [T-01-04](04-primer-cambio-clean-architecture.md) — Hacer tu primer
  cambio siguiendo Clean Architecture (agregar una propiedad opcional a
  `Persona`).
- [H-02-07](../how-to/07-levantar-mysql-docker-para-tests.md) —
  Levantar MySQL con Docker para que los `[MySqlFact]` corran.
- [H-02-11](../how-to/11-ejecutar-tests-de-un-modulo.md) —
  Ejecutar solo los tests de un módulo.
