# Delta para `web-apiclient-transport-contract`

Este delta agrega dos requirements a la spec vigente
`web-apiclient-transport-contract` para reflejar la regla de adopción del
mapper común `CommandResultMapper` introducido por el change
`2026-07-13-taxonomia-errores-commandresult`. No se modifican los
requirements existentes; este delta solo agrega la obligación explícita
de usar el helper compartido.

## ADDED Requirements

### Requirement: Clientes HTTP administrativos usan `CommandResultMapper`

Los clientes HTTP tipados administrativos de `SGV.Web`
(`HabilidadApiClient`, `CargoApiClient` para Cargo y CargoSkill,
`PuestosApiClient`, `UnidadOrganizativaApiClient`) MUST delegar la
clasificación de respuestas HTTP a `CommandResultMapper.Map` en lugar de
mantener matrices `status→categoría` privadas.

#### Scenario: Cliente administrativo usa el mapper común

- GIVEN cualquier cliente HTTP administrativo
- WHEN procesa una respuesta HTTP no exitosa
- THEN la categoría resultante MUST provenir de
  `CommandResultMapper.Map`
- AND el cliente MUST NO contener una matriz `switch` privada que duplique
  la del mapper.

#### Scenario: `AuthApiClient` queda exceptuado

- GIVEN `AuthApiClient.LoginAsync` y un backend que responde 401
- WHEN se procesa la respuesta
- THEN MUST retornar `null` sin pasar por `CommandResultMapper`
- (cumple el requirement vigente "Propagar fallos nativos de transporte"
  más la excepción explícita documentada en la propuesta del change).

### Requirement: `*DeleteResult` exponen `ErrorCategoria`

Los resultados de baja (`HabilidadDeleteResult`, `CargoDeleteResult`,
`PuestoDeleteResult`, `UnidadOrganizativaDeleteResult`,
`CargoSkillDeleteResult`) MUST exponer `Categoria: ErrorCategoria`
además de preservar `StatusCode` como metadata. `Succeeded` MUST ser
`true` solo cuando el código HTTP sea 204.

#### Scenario: Delete 409 produce `Categoria=Conflict`

- GIVEN un `HabilidadApiClient.DeleteAsync` con backend respondiendo 409
- WHEN se obtiene el `HabilidadDeleteResult`
- THEN MUST tener `Succeeded == false`, `Categoria ==
  ErrorCategoria.Conflict` y `StatusCode == 409`.

#### Scenario: Delete 204 produce `Succeeded=true` sin `Categoria`

- GIVEN un `HabilidadApiClient.DeleteAsync` con backend respondiendo 204
- WHEN se obtiene el `HabilidadDeleteResult`
- THEN MUST tener `Succeeded == true`, `Categoria` igual al valor por
  defecto documentado y `StatusCode == 204`.
