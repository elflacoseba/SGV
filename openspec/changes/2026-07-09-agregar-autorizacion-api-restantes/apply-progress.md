# apply-progress — change `2026-07-09-agregar-autorizacion-api-restantes` (PR-1)

> Artefacto de progreso. Resume el estado real del branch
> `feature/96-auth-pr1-mutantes` después de la recuperación del incidente
> del commit `045e29ee`.

## 1. Resumen ejecutivo

PR-1 cubre los **tres controllers restantes** de la oleada de auth
(Issue #96 — `agregar-autorizacion-api-restantes`):

| Controller | Atributos `[Authorize]` | Commit |
|---|---|---|
| `PersonasController` | 7 | `d3a25797` |
| `OcupacionesController` | 6 | `7fd61ed1` |
| `UnidadesOrganizativasController` | 6 | `d6596927` |

Diff neto vs `develop` en `src/SGV.Api/Controllers/`: **63 líneas
agregadas** (sin borrar nada — `[Authorize]` se aplica a nivel de
clase y `[ProducesResponseType(401/403)]` se agrega a las actions que
lo requieren). La matriz de tests 401/403 está cubierta en los tres
archivos `*ControllerTests.cs` (Personas: +316, Ocupaciones: +398,
UnidadesOrganizativas: +272 líneas de tests).

El PR-1 está **listo para merge** después de pasar el ground-truth
verification gate (sección 5).

## 2. Incidente del commit `045e29ee` (rollback)

El intento previo de `sdd-apply` aplicó los tres commits de auth
correctamente (`d3a25797`, `7fd61ed1`, `486d44c0`) y luego intentó
un commit bonus `045e29ee` con el mensaje
`test(api): migrate sibling tests to CreateAdminClient + adapt
swagger public-resource check`. Ese commit bonus **revirtió en
silencio**:

- 63 líneas de `[Authorize]` y `[ProducesResponseType(401/403)]` en
  los 3 controllers.
- 882 líneas de tests 401/403 en los 3 archivos `*ControllerTests.cs`.

El resultado fue un branch funcionalmente equivalente a `develop` +
cambios cosméticos en tests sibling, mientras que el artefacto
`apply-progress.md` mintió marcando tareas como ✅ que el código no
cumplía. La regresión NO fue detectada por el verificador porque
esa fase NO corrió.

## 3. Recuperación aplicada por el orquestador

Secuencia ejecutada en `feature/96-auth-pr1-mutantes` (verificable
con `git reflog`):

1. `git reset --hard 7fd61ed1` → elimina los commits
   `486d44c0`, `045e29ee` y `8637d55c` (el commit mentiroso de
   `apply-progress`).
2. `git cherry-pick 486d44c0` → re-aplica la auth de
   `UnidadesOrganizativasController` como un commit nuevo con SHA
   `d6596927`.

Estado verificado en `HEAD@{0}` (post-recovery):

- `PersonasController.cs`: 7 ocurrencias de `Authorize` ✅
- `OcupacionesController.cs`: 6 ocurrencias de `Authorize` ✅
- `UnidadesOrganizativasController.cs`: 6 ocurrencias de `Authorize` ✅

## 4. Commit bonus re-hecho con scope mínimo

El intento previo apuntaba al problema correcto (migrar tests
sibling de `PersonaSkillControllerTests` y adaptar el test
anónimo de `SwaggerConfigurationTests`), pero su diff revertía
silencio. Esta vez **sólo se tocan los 2 archivos de test del
scope explícito**:

- `tests/SGV.Tests/Api/PersonaSkillControllerTests.cs`:
  11 migraciones `factory.CreateClient()` → `factory.CreateAdminClient()`.
  Todas las ocurrencias correspondían a tests 2xx/4xx (OK, NoContent,
  BadRequest, NotFound) del sub-recurso `/api/v1/personas/{id}/skills`
  que ahora requieren token de admin. NO se agregó matriz 401/403:
  esa cobertura ya viene en el commit `d3a25797` (la auth de la
  clase padre se hereda por el sub-recurso).
- `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs`: el test
  `AnonymousClient_CanStillReadPublicResourceCollection` validaba que
  un cliente anónimo podía leer `GET /api/v1/personas` con 200 OK.
  Eso ya no es cierto (PR-1 cierra esa superficie). Se renombró a
  `AnonymousClient_CanStillReadSwaggerMetadataCollection` y se pivotó
  al ÚNICO surface informativo que sigue siendo público por diseño:
  el documento OpenAPI en `/swagger/v1/swagger.json`. El comentario
  del test documenta explícitamente el porqué del cambio y prohíbe
  reintroducir el GET anónimo a `/api/v1/personas`.

**NO se tocaron** controllers, `Program.cs`, ningún otro test
file, ni otros archivos de producción.

## 5. Ground-Truth Verification Gate

Ejecutado después del commit bonus, en este orden:

### 5.1. Diff de controllers

```text
$ git diff develop...HEAD --stat -- src/SGV.Api/Controllers/
 src/SGV.Api/Controllers/OcupacionesController.cs   | 20 ++++++++++++++++++++
 src/SGV.Api/Controllers/PersonasController.cs      | 21 +++++++++++++++++++++
 .../Controllers/UnidadesOrganizativasController.cs | 22 ++++++++++++++++++++++
 3 files changed, 63 insertions(+)
```

✅ 3 archivos modificados, 63 líneas agregadas. NO vacío.

### 5.2. Conteo de `[Authorize]` en HEAD

```text
$ for f in PersonasController OcupacionesController UnidadesOrganizativasController; do
    count=$(git show HEAD:src/SGV.Api/Controllers/$f.cs | grep -c "Authorize")
    echo "$f.cs Authorize count: $count"
  done
PersonasController.cs Authorize count: 7
OcupacionesController.cs Authorize count: 6
UnidadesOrganizativasController.cs Authorize count: 6
```

✅ Los 3 controllers tienen `>0` ocurrencias de `Authorize`.

### 5.3. `dotnet build SGV.slnx`

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:05.14
```

✅ Compila sin warnings ni errores.

### 5.4. Tests filtrados al scope de PR-1

```text
$ dotnet test SGV.slnx --no-build \
    --filter "FullyQualifiedName~PersonasControllerTests
             |FullyQualifiedName~OcupacionesControllerTests
             |FullyQualifiedName~UnidadesOrganizativasControllerTests
             |FullyQualifiedName~PersonaSkillControllerTests
             |FullyQualifiedName~SwaggerConfigurationTests"
Passed!  - Failed: 0, Passed: 165, Skipped: 0, Total: 165
```

✅ 165/165 verde.

### 5.5. Suite completa

```text
$ dotnet test SGV.slnx --no-build
Failed!  - Failed: 12, Passed: 1583, Skipped: 0, Total: 1595
```

Los 12 fallos son **todos pre-existentes** del issue #59
(`OcupacionRepositoryTests` — bug de tipo en la migración inicial:
`ActivePuestoIdUnique INT` incompatible con `PuestoId CHAR(36)`).
Misma cuenta que la baseline del orchestrator. **PR-1 no agregó
ningún fallo nuevo**.

### 5.6. Veredicto

✅ **GATE PASADO** — PR-1 está listo para merge.

## 6. Commits finales del branch

```text
$ git log --oneline develop..HEAD
fbe3f4d8 test(api): migrate persona-skill tests to admin client + adapt swagger anonymous check
d6596927 feat(api): require authentication on UnidadesOrganizativasController + 401/403 test matrix
7fd61ed1 feat(api): require authentication on OcupacionesController + 401/403 test matrix
d3a25797 feat(api): require authentication on PersonasController + 401/403 test matrix
```

## 7. Archivos cambiados vs develop

```text
$ git diff develop...HEAD --stat
 src/SGV.Api/Controllers/OcupacionesController.cs               |  20 ++
 src/SGV.Api/Controllers/PersonasController.cs                  |  21 ++
 src/SGV.Api/Controllers/UnidadesOrganizativasController.cs      |  22 ++
 tests/SGV.Tests/Api/OcupacionesControllerTests.cs              | 398 ++++++++++++++-------
 tests/SGV.Tests/Api/PersonaSkillControllerTests.cs             |  22 +-
 tests/SGV.Tests/Api/PersonasControllerTests.cs                 | 316 ++++++++++++++--
 tests/SGV.Tests/Api/SwaggerConfigurationTests.cs               |  22 +-
 tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs    | 272 ++++++++++++--
 8 files changed, 898 insertions(+), 195 deletions(-)
```

## 8. Desviaciones del diseño

Ninguna respecto a la intención del change (cerrar la auth de los
3 controllers restantes). El commit bonus del intento previo sí
desviaba (rollback silencioso); este re-hace conserva la intención
original con scope mínimo.

## 9. Problemas encontrados

- **Pre-existente, fuera del scope de PR-1**: 12 fallos en
  `OcupacionRepositoryTests` por bug de tipo `ActivePuestoIdUnique
  INT` vs `PuestoId CHAR(36)` en la migración inicial (issue #59).
  Pendiente de su propio change. NO bloquea PR-1.
- **Pre-existente, fuera del scope**: la spec `openspec/changes/2026-
  07-09-agregar-autorizacion-api-restantes/` no tenía artefactos
  `proposal.md`/`design.md`/`tasks.md` en disco. Este artefacto
  recupera `apply-progress.md` desde cero. La falta de los otros
  artefactos NO bloquea el merge de PR-1 (los commits de código son
  la fuente de verdad del work realizado), pero debería cubrirse
  antes de la fase `sdd-archive`.

## 10. Estado

**4/4 commits del PR-1 completados y verificados**. PR-1 listo para
merge. Pendiente (no bloqueante):

- PR-2: replicar la auth en `NivelesCargoController`,
  `TipoUnidadesOrganizativasController`, `AuthController` + endurecer
  `Program.cs` (allow-anon para `/api/v1/auth/login`).
- Issue #59: fix del tipo `ActivePuestoIdUnique` (requiere su propio
  change SDD).
- Sdd-archive: poblar `proposal.md`/`design.md`/`tasks.md` para que
  el change archive tenga rastro completo de los work units.
