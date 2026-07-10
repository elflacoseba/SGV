# Propuesta: Extraer `SGV.Contracts` (issue #100)

## Intent

`SGV.Web` arrastra `SGV.Aplicacion` e `SGV.Infraestructura` transitivamente vía `SGV.Api`. La frontera real son los **38 archivos** de Web que importan `SGV.Aplicacion.*`, no `SGV.Api/Contracts/` (un único archivo: `AuthApiRoutes`). La API serializa tipos de `SGV.Aplicacion.*Consultas.Dtos` y recibe `SGV.Aplicacion.*Comandos` sin re-empaquetar; ya son wire contract. Esta propuesta los mueve a `SGV.Contracts`, renombra el namespace y deja el grafo `Dominio ← Aplicacion ← Contracts ← {Api, Web}` **sin cambiar payloads ni comportamiento observable**.

## Scope

**In scope**
- Crear `src/SGV.Contracts/SGV.Contracts.csproj` (`net10.0`, sin deps de negocio).
- Mover tipos wire-shared: `LoginRequest`/`Response`/`AuthApiRoutes`, DTOs y requests de `Organizacion` y `Habilidades`, `RolesSgv`, `UsuarioDto`, `UsuarioCommandResult`, `UsuarioError`/`UsuarioErrorType`, enums de segmento.
- Sumar el proyecto a `SGV.slnx`; referencias `Aplicacion → Contracts`, `Api → Contracts`, `Web → Contracts`.
- Eliminar `src/SGV.Api/Contracts/` y la referencia `Web → Api`.
- Actualizar `using` en 38 archivos de Web, 11 controllers, `ApiWebApplicationFactory.cs` y tests.
- Alinear `AGENTS.md` y `docs/decisiones-implementacion.md` (línea 83).

**Out of scope**: `SGV.Aplicacion.Compatibilidad` (no consumido por Web); reescritura de DTOs, mappers o cambios de payload JSON; nuevas reglas de validación, autorización o persistencia.

## Capabilities

> Refactor puro: cambian namespaces, no la forma de payloads, endpoints ni reglas.

**New Capabilities**: `None`

**Modified Capabilities**: `None` — los nombres de tipo (`CargoDto`, `PuestoCommandResult`, `LoginRequest`, etc.) no cambian, solo el namespace. Los specs `web-apiclient-transport-contract`, `puesto-management`, `cargo-management`, `habilidad-management`, `sgv-web-authentication` siguen vigentes.

## Approach

Opción 3 de la exploración: **PRs encadenados por capa**, cada uno deja build + tests verdes.

- **PR1 — Crear `SGV.Contracts`**: proyecto + mover `AuthApiRoutes` a `SGV.Contracts.Auth`; eliminar `src/SGV.Api/Contracts/`. Desacopla Web de Api por auth.
- **PR2 — Organizacion**: DTOs, requests, results, errors. Quita la mitad de los 38 imports.
- **PR3 — Habilidades**: DTOs/requests de skills y niveles. Cierra la capa catálogo.
- **PR4 — Seguridad**: `RolesSgv`, `LoginRequest`/`Response`, tipos de `Usuario`. Cierra la arista `Aplicacion → Contracts`.

Blast radius controlado (~10-15 archivos por PR), merges revisables en 30-90 min, regresiones aisladas por capa. Esto anticipa el reviewer burden de mover 38 archivos: ningún PR mezcla capas.

## Affected Areas

- `SGV.slnx`: sumar el nuevo `.csproj`.
- `src/SGV.Contracts/**`: nuevo (namespaces `Auth`, `Organizacion`, `Habilidades`, `Seguridad`).
- `src/SGV.Api/Contracts/`: eliminado tras PR1.
- `src/SGV.Web/**.cs` (38): `using` actualizado.
- `src/SGV.Api/Controllers/**` (11): `using` actualizado.
- `src/SGV.Aplicacion/**`: `using` internos + nueva referencia.
- `tests/SGV.Tests/Api/**` (18): constructores primarios de DTOs.
- `tests/SGV.Tests/Web/**`, `Seguridad/JwtRealAuthTests.cs`: `using` actualizado.
- `AGENTS.md`, `docs/decisiones-implementacion.md`: nota del nuevo proyecto.

## Risks

- **Reviewer burden (4 PRs encadenados)**: PR1 pequeño precede a los grandes; cadena declarada en cada descripción.
- **Nueva arista `Aplicacion → Contracts` para `RolesSgv`**: declarar explícito en `design.md`; el grafo respeta Clean Architecture.
- **Merge conflicts en los 38 archivos**: PRs por capa aíslan el conflicto.
- **`ApiWebApplicationFactory` cambia de namespace**: concentrado en un PR; revertir = revertir un PR.
- **`decisiones-implementacion.md` desalineado**: ajuste mecánico al final del PR4.

## Rollback Plan

- **Por PR**: cada merge deja build + tests verdes; `git revert` del último PR restaura `Web → Api` y los namespaces anteriores sin pérdida funcional.
- **Global**: `git revert` desde el último PR hacia el primero devuelve al estado pre-cambio; los DTOs siguen donde estaban.
- **Recreación**: el orden en `SGV.slnx` queda documentado en `design.md` para reintentar desde el commit previo a PR1.

## Dependencies

Sin paquetes NuGet nuevos. `SGV.Contracts` es classlib pura sin dependencias de negocio.

## Success Criteria

- [ ] `dotnet build SGV.slnx` y `dotnet test SGV.slnx` verdes tras cada PR y al cerrar la cadena.
- [ ] `grep -r "using SGV.Aplicacion" src/SGV.Web` devuelve 0.
- [ ] `grep -r "using SGV.Api.Contracts" src/` devuelve 0.
- [ ] `SGV.Web.csproj` no referencia `SGV.Api`.
- [ ] Grafo final `Dominio ← Aplicacion ← Contracts ← {Api, Web}` (verificable con `dotnet list reference`).
- [ ] `AGENTS.md` y `docs/decisiones-implementacion.md` mencionan `SGV.Contracts`.
- [ ] Payloads JSON idénticos antes y después (suite API vigente).
