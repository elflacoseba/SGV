# Tasks — Deuda operativa #126 (fix operational tech debt)

## PR 1 (CU-0) — Health / readiness infraestructura (completado)

- [x] **0-RED**: Escribir tests de health checks (API + Web) que fallen
- [x] **0a-GREEN**: Implementar `/health/live` y `/health/ready` en SGV.Api
- [x] **0b-GREEN**: Implementar `SgvApiUpstreamHealthCheck` en SGV.Web
- [x] **0c-GREEN**: Implementar validación fail-loud de `ConnectionStrings:SgvDatabase`
- [x] **0x-CORRECCIÓN**: Reemplazar `IDbContextFactory` check por `MySqlConnection` probe, restaurar `AddDbContext`, fix `CorsAllowedOriginsValidationTests`

## PR 2 (CU-1 + CU-2) — Timeout login + UX frontera (actual)

- [x] **1-RED**: Escribir tests de timeout para `AuthApiClient` y `UnidadOrganizativaApiClient`
- [x] **1-GREEN**: Agregar `Timeout = 10s` en registros de typed clients + factory override
- [x] **2-RED**: Escribir tests de UX para excepciones de transporte en SignIn
- [x] **2-GREEN**: Implementar try/catch en `SignInModel.OnPostAsync` para errores de transporte

## PR 3 (CU-3 + CU-4 + CU-5) — Docs + verify (pendiente)

- [x] **3-DOC**: Delta de specs (operational-readiness + docs)
- [x] **4-DOC**: Subsección "Contrato runtime MySQL" en `docs/decisiones-implementacion.md`
- [x] **5-VERIFY**: Ejecutar suite completa y archivar change
