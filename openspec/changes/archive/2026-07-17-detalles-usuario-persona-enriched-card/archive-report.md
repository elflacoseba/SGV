# Archive Report — 2026-07-17-detalles-usuario-persona-enriched-card

## Resumen

Card enriquecida de Persona replicada en el detalle readonly de Usuarios
(mismo árbol DOM que Edit, sin botones Quitar/Cambiar ni modal buscador,
con fallback non-blocking "Apellidos, Nombres" ante 404/transporte del API
de Persona).

## Trazabilidad

- PR: https://github.com/elflacoseba/SGV/pull/169
- Branch: `feat/detalles-usuario-persona-enriched-card`
- Estado del PR al archivar: ABIERTO contra `develop` (merge depende del usuario)
- Spec modificado: `usuario-web-listado-detalle-baja` — `REQ-ULD-04` MODIFIED
- Verdict del verify: PASS (6/6 escenarios, 2464/2464 tests verdes)
- Production LoC: ~181 (Details.cshtml + Details.cshtml.cs)
- Tests LoC: ~210 (DetailsPageTests + FakePersonaApiClient)

## Delta consolidado

El requisito `REQ-ULD-04` del spec canónico `openspec/specs/usuario-web-listado-detalle-baja/spec.md`
quedó consolidado con:
- Texto del requirement actualizado a "Detalle readonly con persona enriquecida y retorno seguro".
- 6 scenarios: 2 originales preservados con wording actualizado + 4 nuevos del delta.

## Estado post-archive

- Artefactos SDD movidos a `openspec/changes/archive/2026-07-17-detalles-usuario-persona-enriched-card/`.
- Spec canónico `openspec/specs/usuario-web-listado-detalle-baja/spec.md` actualizado.
- Rama `feat/detalles-usuario-persona-enriched-card` permanece local y en origin; PR #169 sigue ABIERTO.
- Engram: topic `sdd/2026-07-17-detalles-usuario-persona-enriched-card/archive-report` creado como memoria cross-session.

## Próximos pasos sugeridos

1. Review humano del PR #169.
2. Merge del PR a `develop` (acción humana; el orquestador NO debe mergear automáticamente).
3. Después del merge: opcional cleanup local (`git fetch --prune`).
