# Delta para confiabilidad de la suite de pruebas

## Propósito

Este delta define los contratos técnicos verificables que la suite `tests/SGV.Tests` debe satisfacer para ser determinista, repetible y libre de contención entre hosts de integración. No describe comportamiento de producto; describe invariantes operacionales de la infraestructura de pruebas.

## ADDED Requirements

### Requirement: Aislamiento de validación de sesión web

La validación de la sesión web y de JWT NO debe depender de estado mutable compartido a nivel de proceso. Las opciones de JWT (Issuer, Audience, SigningKey) consumidas por cada host de prueba deben afectar únicamente a la instancia que las configuró. El tiempo de vida del servicio en el contenedor de dependencias queda como detalle de implementación y no se prescribe desde este spec.

#### Scenario: Hosts con opciones JWT distintas validan solo su propia configuración

- GIVEN dos hosts Web con `Jwt:SigningKey` distintos configurados en cada uno
- WHEN cada host valida un token firmado con su propia clave
- THEN el primer host acepta su token y rechaza el del segundo
- AND el segundo host acepta su token y rechaza el del primero

#### Scenario: Validaciones repetidas permanecen independientes

- GIVEN dos invocaciones consecutivas del mecanismo de validación dentro del mismo proceso de tests
- WHEN se ejecutan en orden sobre opciones JWT distintas
- THEN la segunda no observa parámetros residuales de la primera
- AND no queda caché compartida que condicione invocaciones futuras

### Requirement: Límite explícito de concurrencia para tests con hosts

La suite MUST versionar y copiar al directorio de salida un archivo de configuración del runner de pruebas. Esa configuración MUST mantener la paralelización entre colecciones con un máximo de cuatro workers simultáneos. Las suites que comparten un host Web o API MUST ejecutarse en serie dentro de su colección compartida, mientras que suites independientes MAY continuar ejecutándose en paralelo entre sí.

#### Scenario: Configuración del runner disponible en tiempo de ejecución

- GIVEN el archivo de configuración versionado del runner de pruebas
- WHEN se ejecuta `dotnet test SGV.slnx --no-build`
- THEN la configuración está presente en el directorio de salida del ensamblado de tests
- AND sus valores son los declarados por el repositorio

#### Scenario: Dos clases de integración web se serializan en su colección compartida

- GIVEN dos clases de tests marcadas con la misma colección de host Web compartido
- WHEN el runner ejecuta ambas clases en la misma corrida
- THEN sus ejecuciones no se solapan en el tiempo
- AND ninguna inicia hasta que la anterior haya finalizado

#### Scenario: Suites independientes conservan paralelismo entre sí

- GIVEN una suite sin host y una suite de integración Web en colecciones distintas
- WHEN el runner planifica la ejecución
- THEN ambas suites son elegibles para correr en paralelo
- AND el tope global de workers simultáneos no se excede

### Requirement: Ciclo de vida determinista de factories de integración

Toda factory que arranque un host de prueba Web o API MUST disponerse de forma determinista al finalizar la colección o fixture que la posee. La configuración de overrides MUST NO dejar factories derivadas ni hosts huérfanos sin disponer.

#### Scenario: Tests con overrides distintos no conservan configuración previa

- GIVEN dos tests en la misma colección compartida que invocan el mecanismo de overrides con valores distintos
- WHEN el segundo test aplica su override
- THEN el host activo refleja exclusivamente el segundo override
- AND no quedan restos del primer override en servicios del host

#### Scenario: Disposal de fixture libera los recursos del host

- GIVEN una fixture de colección que posee la factory base
- WHEN la fixture se dispone al finalizar la colección
- THEN el host subyacente se detiene y libera sus recursos
- AND ningún factory derivada queda retenida fuera de la cadena de dispose

#### Scenario: Overrides no crean factories huérfanas

- GIVEN el mecanismo de overrides expuesto por la factory de integración
- WHEN se aplican overrides encadenados dentro de la misma colección
- THEN solo la factory base pertenece al ciclo de vida de la colección
- AND los overrides no introducen instancias adicionales sin disposición

### Requirement: Gate de estabilidad de la suite

Sobre un ambiente comparable con dependencias disponibles (MySQL 8, .NET 10 SDK), `dotnet test SGV.slnx --no-build` MUST completar en menos de quince minutos en tres ejecuciones consecutivas con totales de pass/fail idénticos. La salida MUST NO contener el código `MSB4166` ni el mensaje `Timed out waiting for the entry point to build the IHost`. Cualquier varianza o falla bloquea la declaración de aptitud y obliga a diagnosticar antes de archivar el cambio.

#### Scenario: Tres corridas consecutivas satisfacen el gate

- GIVEN un ambiente comparable con dependencias disponibles
- WHEN se ejecutan tres corridas consecutivas del comando de aceptación
- THEN cada corrida completa en menos de quince minutos
- AND los totales de pass/fail son idénticos entre las tres
- AND la salida no contiene `MSB4166` ni el mensaje de timeout del host

#### Scenario: Variación o timeout bloquea la declaración de aptitud

- GIVEN una corrida cuya duración o totales difieren de las corridas anteriores
- WHEN se compara contra el gate de estabilidad
- THEN el cambio NO se declara listo para archivar
- AND se inicia diagnóstico antes de cualquier intento de merge o release