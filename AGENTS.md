# Guía del Repositorio

## Resumen rápido

SGV es una solución .NET 10 con Clean Architecture, ASP.NET Core API, Razor Pages para `SGV.Web`, EF Core 9 y MySQL 8 mediante Pomelo. `SGV.Web` hoy funciona como shell autenticada que consume `SGV.Api` mediante clientes tipados `HttpClient`, con cookie auth en web y JWT bearer en API. El grafo de proyectos es `Dominio` ← `Aplicacion` ← `Infraestructura` (con `Contracts` como leaf transversal usado por `Api` y `Web`); `Api` es composition root. `SGV.Web` **NO** referencia `SGV.Api` como proyecto — sus contratos wire viven en `SGV.Contracts`; solo linkea por `<Compile Include>` un único helper compartido (`HealthCheckResponseWriter.cs`). El repositorio combina desarrollo tradicional con OpenSpec/SDD y testing orientado al comportamiento. **El proceso debe ser proporcional a la complejidad y riesgo de cada tarea.**

---

## Principio fundamental de trabajo

La metodología del proyecto debe proteger la calidad sin introducir burocracia innecesaria.

**No todas las tareas requieren el mismo nivel de análisis, documentación, testing ni validación.**

Antes de comenzar una tarea, clasificá su complejidad y aplicá únicamente el proceso necesario.

La prioridad es:

1. Mantener la arquitectura y las decisiones técnicas vigentes.
2. Preservar el comportamiento existente.
3. Implementar exactamente lo solicitado.
4. Validar suficientemente el cambio.
5. Evitar trabajo, contexto, tests, documentación y consumo de tokens innecesarios.

**No utilizar SDD, TDD exhaustivo ni validaciones completas únicamente porque el repositorio utiliza OpenSpec y testing.**

La complejidad de la tarea determina el proceso, no la tecnología utilizada por el repositorio.

---

## Clasificación de tareas

Antes de implementar, clasificá internamente la tarea como **TRIVIAL, PEQUEÑA, MEDIANA o GRANDE**.

No es necesario informar la clasificación al usuario salvo que sea relevante para explicar el proceso utilizado.

### TRIVIAL

Una tarea es TRIVIAL cuando:

- El cambio está localizado.
- No modifica arquitectura.
- No modifica contratos públicos.
- No modifica persistencia.
- No modifica reglas de negocio importantes.
- Es fácilmente reversible.
- Tiene bajo riesgo de regresión.

Ejemplos:

- Agregar un botón.
- Cambiar el texto de un botón.
- Ordenar una lista.
- Cambiar el orden visual de elementos.
- Modificar una clase CSS.
- Cambiar estilos o espaciado.
- Corregir un typo.
- Cambiar una etiqueta.
- Modificar una condición simple y localizada.
- Agregar una propiedad visual.
- Cambiar un valor de configuración puramente visual.
- Corregir una pequeña presentación en Razor.
- Cambiar el orden de columnas.
- Modificar un mensaje mostrado al usuario.

#### Proceso TRIVIAL

- No crear artefactos OpenSpec.
- No crear `proposal.md`.
- No crear `design.md`.
- No crear `tasks.md`.
- No crear `spec.md`.
- No crear documentación SDD.
- No crear tests nuevos salvo que sean imprescindibles para proteger una regresión.
- No ejecutar la suite completa si no es necesario.
- No analizar todo el repositorio.
- No leer documentación extensa que no sea relevante para el cambio.
- Inspeccionar únicamente los archivos y dependencias necesarios.
- Implementar directamente.
- Ejecutar la validación mínima razonable.

Para una tarea trivial, el objetivo es:

**entender → modificar → verificar**

No:

**explorar → diseñar → especificar → planificar → implementar → testear exhaustivamente → verificar → archivar**

### PEQUEÑA

Una tarea es PEQUEÑA cuando:

- Afecta varias piezas relacionadas.
- Puede requerir más de un archivo.
- Puede afectar una capa o una pequeña interacción entre capas.
- No introduce arquitectura nueva.
- No introduce un módulo significativo.
- No cambia decisiones técnicas importantes.

Ejemplos:

- Agregar un filtro sencillo a un listado.
- Agregar una búsqueda.
- Agregar paginación a una pantalla existente.
- Agregar una acción sencilla a una Razor Page.
- Modificar un cliente HTTP existente y su consumo.
- Agregar una pequeña funcionalidad a un caso de uso existente.
- Corregir un bug que requiere cambios coordinados en pocas clases.

#### Proceso PEQUEÑA

- Realizar un análisis breve del código relevante.
- No crear SDD completo.
- No crear artefactos OpenSpec salvo que exista una razón concreta.
- Identificar los archivos y capas afectadas.
- Implementar el cambio.
- Crear únicamente los tests que aporten valor.
- Ejecutar las validaciones relevantes.
- Evitar leer o modificar partes no relacionadas del repositorio.

Objetivo:

**analizar brevemente → implementar → validar**

### MEDIANA

Una tarea es MEDIANA cuando:

- Afecta varias capas.
- Modifica contratos.
- Modifica una integración existente.
- Modifica persistencia pero sin constituir un cambio arquitectónico importante.
- Requiere coordinación entre API, Application, Web, Contracts o Infrastructure.
- Tiene riesgo moderado de regresión.
- Requiere varias decisiones de implementación.

Ejemplos:

- Agregar una funcionalidad de exportación.
- Crear un nuevo caso de uso que atraviesa varias capas.
- Agregar una nueva consulta compleja.
- Modificar una funcionalidad existente que afecta API y Web.
- Agregar una funcionalidad que requiere cambios coordinados en `Contracts`, `Api`, `Application` y `Web`.

#### Proceso MEDIANA

- Analizar el impacto antes de implementar.
- Revisar los archivos y documentación directamente relacionados.
- Crear un plan breve antes de comenzar cuando ayude a reducir errores.
- Revisar `docs/decisiones-implementacion.md` si se afectan decisiones técnicas, persistencia, seguridad, auditoría o arquitectura.
- Revisar OpenSpec si existe un cambio relacionado.
- OpenSpec/SDD **puede** utilizarse, pero no es obligatorio si no aporta valor.
- Implementar.
- Crear tests significativos.
- Ejecutar las validaciones correspondientes a las capas modificadas.
- Evitar documentación SDD puramente burocrática.

Objetivo:

**analizar → planificar brevemente → implementar → probar → validar**

### GRANDE

Una tarea es GRANDE cuando:

- Introduce un nuevo módulo significativo.
- Modifica arquitectura.
- Modifica decisiones técnicas importantes.
- Introduce una funcionalidad de negocio importante.
- Modifica significativamente persistencia.
- Modifica seguridad o autenticación.
- Modifica contratos públicos de manera importante.
- Afecta múltiples módulos existentes.
- Tiene alto riesgo de regresión.
- Requiere múltiples decisiones de diseño.
- Requiere coordinación de varios desarrolladores o agentes.
- Es probable que la implementación se divida en múltiples tareas.

Ejemplos:

- Implementar un módulo completo.
- Implementar un sistema de permisos.
- Crear un nuevo subsistema de auditoría.
- Modificar significativamente el modelo de usuarios.
- Introducir una nueva estrategia de persistencia.
- Cambiar la arquitectura de comunicación Web → API.
- Implementar una funcionalidad transversal que afecta múltiples módulos.

#### Proceso GRANDE

Para tareas GRANDES utilizar OpenSpec/SDD.

El proceso esperado es:

1. Exploration cuando sea necesaria.
2. Proposal.
3. Design.
4. Tasks.
5. Implementación.
6. Tests.
7. Verification.
8. Archive.

Los artefactos deben mantenerse alineados con el estado real del repositorio.

---

## Regla de proporcionalidad

El nivel de proceso debe ser proporcional a:

- Complejidad.
- Riesgo.
- Cantidad de capas afectadas.
- Cantidad de archivos afectados.
- Impacto sobre contratos.
- Impacto sobre persistencia.
- Impacto sobre seguridad.
- Impacto sobre reglas de negocio.
- Reversibilidad del cambio.

**No medir la complejidad únicamente por la cantidad de líneas modificadas.**

Un cambio de 5 líneas que modifica una regla de seguridad puede ser GRANDE en términos de riesgo.

Un cambio de 100 líneas localizado en una vista puede continuar siendo PEQUEÑO.

Ante la duda entre dos niveles, elegir el nivel inferior cuando el cambio sea localizado, reversible y de bajo riesgo.

Si durante la implementación se descubre que la tarea es más compleja de lo previsto, aumentar el nivel de proceso.

---

## Principio de mínimo contexto

Para tareas TRIVIALES y PEQUEÑAS:

- Inspeccioná únicamente los archivos necesarios.
- No recorras todo el repositorio.
- No leas todos los artefactos OpenSpec.
- No leas documentación extensa si no es relevante.
- No inspecciones módulos no relacionados.
- No ejecutes comandos costosos innecesariamente.
- No generes resúmenes del repositorio que no aporten a la tarea.

Para tareas MEDIANAS y GRANDES, ampliar el contexto según el impacto real.

**El contexto también tiene un costo.**

El objetivo es obtener suficiente información para realizar correctamente el cambio, no maximizar la cantidad de información leída.

---

## Regla de cambio mínimo

Cuando una tarea sea TRIVIAL o PEQUEÑA:

- Modificá únicamente lo necesario.
- No refactorices código no relacionado.
- No reorganices archivos sin necesidad.
- No cambies nombres por motivos estéticos.
- No actualices dependencias sin necesidad.
- No "mejores" código que no forma parte de la tarea.
- No introduzcas abstracciones nuevas si una modificación directa es suficiente.
- No conviertas una corrección pequeña en una refactorización general.

Si detectás una mejora no relacionada, podés mencionarla al usuario, pero no implementarla automáticamente.

---

## Regla de no sobre-ingeniería

No crear una solución más compleja que el problema.

Para tareas pequeñas:

- Preferir modificar una implementación existente antes que introducir nuevas abstracciones.
- Preferir reutilizar servicios existentes.
- Preferir reutilizar contratos existentes.
- Preferir reutilizar componentes existentes.
- No crear patrones adicionales sin necesidad.
- No crear interfaces únicamente para satisfacer una preferencia arquitectónica abstracta.
- No crear servicios, clases o archivos nuevos si el cambio puede realizarse correctamente dentro de la estructura existente.

La arquitectura debe proteger el sistema, no convertir cada modificación en una ceremonia.

---

## Ruta rápida para trabajar

### Para tareas TRIVIALES

1. Identificá los archivos relevantes.
2. Entendé el comportamiento actual.
3. Realizá el cambio.
4. Ejecutá la validación mínima necesaria.

### Para tareas PEQUEÑAS

1. Identificá las capas afectadas.
2. Revisá las implementaciones existentes relevantes.
3. Implementá.
4. Ejecutá tests relevantes si aportan valor.
5. Ejecutá build o validación específica.

### Para tareas MEDIANAS

1. Analizá el impacto.
2. Revisá las decisiones técnicas relacionadas.
3. Revisá OpenSpec si existe un cambio relacionado.
4. Realizá un plan breve.
5. Implementá.
6. Ejecutá tests relevantes.
7. Ejecutá las validaciones correspondientes.

### Para tareas GRANDES

Utilizá el proceso OpenSpec/SDD completo.

---

## Validación proporcional

No todas las tareas requieren ejecutar toda la suite.

### TRIVIAL

Ejecutar únicamente la validación directamente relacionada.

Ejemplos:

- Cambio CSS → validar build frontend si corresponde.
- Cambio de Razor sin lógica → validación de compilación/build si corresponde.
- Cambio textual → no generar tests.
- Ordenamiento simple → test solo si existe lógica de negocio relevante.

### PEQUEÑA

Ejecutar:

- Build de la parte afectada.
- Tests directamente relacionados cuando aporten valor.

### MEDIANA

Ejecutar:

- Build.
- Tests de las capas afectadas.
- `bun run build` si se modifica frontend.
- Tests de integración si se modifica integración o persistencia.

### GRANDE

Ejecutar la validación correspondiente al alcance completo del cambio.

No ejecutar comandos costosos únicamente por costumbre.

---

## Construcción, Prueba y Desarrollo

- `dotnet restore`: restaura dependencias.
- `dotnet build SGV.slnx`: compila toda la solución.
- `dotnet test SGV.slnx`: ejecuta toda la suite.
- `dotnet test SGV.slnx --collect:"XPlat Code Coverage"`: ejecuta pruebas con cobertura.
- `bun install` (en `src/SGV.Web`): instala dependencias del pipeline frontend.
- `bun run dev` (en `src/SGV.Web`): levanta el pipeline de assets de Inspinia/Gulp para desarrollo.
- `bun run build` (en `src/SGV.Web`): genera el bundle frontend para validación local.
- `dotnet ef migrations add <Nombre> --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj --output-dir Persistencia/Migraciones`: crea una migración.
- `dotnet ef migrations script --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj --idempotent --output docs/migracion-inicial-sgv.sql`: genera script SQL idempotente.

---

## Configuración inicial

Antes del primer `dotnet run` de `SGV.Api`, configurá los secretos locales con `dotnet user-secrets`:

- Clave JWT (obligatoria): `dotnet user-secrets set "Jwt:SigningKey" "<random ≥32 bytes>" --project src/SGV.Api`.
- Connection string de MySQL (obligatoria): `dotnet user-secrets set "ConnectionStrings:SgvDatabase" "<server=...;database=sgv;user=...;password=...;>" --project src/SGV.Api`.
- Si no querés tocar `secrets.json`, podés setear `ConnectionStrings__SgvDatabase` como variable de entorno.
- El placeholder dev en `src/SGV.Api/appsettings.Development.json` (commiteado como ayuda de primer arranque) sirve solo para desarrollo local; **NO es apto para producción**. Reemplazá su `Jwt:SigningKey` con `dotnet user-secrets` antes de cualquier uso real.
- Las claves con `:` en user-secrets deben tipearse SIN espacios.
- **Sí hay GitHub CI** (`.github/workflows/ci.yml`). Se ejecuta en PRs y pushes a `develop`/`main`, levanta un servicio `mysql:8.0`, compila y testea con MySQL real (`[MySqlFact]`), requiere el secret `JWT_SIGNING_KEY` y verifica que `bun.lock` y `wwwroot` estén commiteados al día (`git diff --exit-code`). Localmente los tests `[MySqlFact]` se skipean solos cuando no hay conexión; corré toda la suite con `dotnet test SGV.slnx` cuando la tarea requiera validación completa.

---

## Estructura del Proyecto y Organización

- `SGV.slnx`: solución principal del repositorio.
- `global.json`: fija SDK `10.0.300`.
- `src/SGV.Dominio/`: entidades, value objects y reglas de negocio.
- `src/SGV.Aplicacion/`: casos de uso, contratos (interfaces) de servicios, validaciones y servicios. Solo depende de `SGV.Dominio` y `SGV.Contracts`.
- `src/SGV.Contracts/`: **wire-types compartidos** entre `SGV.Api` y `SGV.Web` (records/DTOs de request/response/result + constantes). Es una **leaf** del grafo.
- `src/SGV.Infraestructura/`: EF Core, Identity, repositorios, interceptor de auditoría y migraciones.
- `src/SGV.Api/`: controladores HTTP, autenticación y composición de la aplicación.
- `src/SGV.Web/`: frontend Razor Pages y shell web basado en Inspinia Starterkit. Depende **únicamente** de `SGV.Contracts`.
- `src/SGV.Web/Integration/`: clientes tipados hacia `SGV.Api`, bridge de JWT (`ApiBearerTokenHandler`) y contratos de integración web.
- `src/SGV.Web/Pages/Organizacion/`: módulos web vigentes de unidades organizativas, cargos y habilidades.
- `src/SGV.Web/Pages/Error/`: páginas de error HTTP de la shell web.
- `tests/SGV.Tests/`: pruebas unitarias, de persistencia, integración API, compatibilidad y smoke tests web.
- `docs/decisiones-implementacion.md`: decisiones técnicas vigentes.
- `docs/migracion-inicial-sgv.sql`: script SQL idempotente generado contra MySQL 8 (21 migraciones EF Core aplicadas; usable sobre DB vacía o para sincronizar entornos existentes).
- `docs/migracion-inicial-sgv-mariadb.sql`: variante lineal del script anterior para MariaDB (collation `utf8mb4_unicode_ci` y columnas generadas `STORED`; aplicar una sola vez sobre DB vacía).
- `docs/script-listar-ciclos-jerarquia-unidades-organizativas.sql`: utilitario de diagnóstico para detectar ciclos en la jerarquía de unidades organizativas (relacionado con el trigger anti-ciclos de la migración `20260816203122`).
- `openspec/config.yaml`: configuración SDD/OpenSpec del repo.
- `openspec/changes/<cambio>/`: artefactos de cambio.
- `InspinaTemplate/`: template de referencia importado para la shell web.
- `.github/workflows/ci.yml`: pipeline de CI activo (PRs/pushes a `develop` y `main`).

---

## Stack Técnico y Restricciones

- .NET 10 (`net10.0`) con SDK `10.0.300`.
- C# 14, nullable enabled e implicit usings enabled.
- Clean Architecture: `Dominio -> Aplicacion -> Infraestructura`, con `Api` como composition root.
- ASP.NET Core API + Swagger (`Swashbuckle.AspNetCore`).
- ASP.NET Core Razor Pages en `SGV.Web`.
- `SGV.Api` autentica con JWT bearer; `SGV.Web` autentica con cookies y reenvía el token a la API vía `ApiBearerTokenHandler`.
- EF Core 9.x.
- `Pomelo.EntityFrameworkCore.MySql 9.0.0` como proveedor único soportado.
- MySQL 8 requerido para escenarios reales de persistencia e integración.
- ASP.NET Core Identity con clave string.
- FluentValidation en capa de aplicación.
- Bun + Gulp para assets del frontend.
- Google Charts OrgChart para la vista de organigrama.
- xUnit 2.9.2 + `Microsoft.NET.Test.Sdk` + `coverlet.collector`.

---

## Convenciones de Código y Diseño

- Usá indentación de cuatro espacios.
- PascalCase para tipos y miembros públicos; camelCase para variables locales y parámetros.
- Métodos asíncronos terminan en `Async`.
- Respetá separaciones de capa: dominio no depende de infraestructura; aplicación no conoce detalles HTTP.
- `SGV.Web` actúa como capa web/composition layer; no mover lógica de dominio o persistencia al frontend.
- La integración runtime con backend debe pasar por clientes tipados en `src/SGV.Web/Integration/`.
- Los wire-types consumidos por Web viven en `SGV.Contracts`.
- Los cambios OpenSpec se nombran en kebab-case.
- Conservá nombres técnicos, código, comentarios e identificadores en inglés salvo que el contexto existente del archivo exija otra cosa.
- Los documentos generados por SDD deben escribirse en español.

---

## Filosofía de Testing

El objetivo de los tests es proteger el comportamiento funcional de la aplicación, no maximizar el porcentaje de cobertura ni la cantidad de código de pruebas.

Cada test debe aportar valor real.

Priorizar:

- Reglas de negocio del Dominio.
- Casos de uso de Application.
- Validaciones.
- Cálculos.
- Transformaciones con lógica.
- Permisos y autorización.
- Casos límite.
- Correcciones de errores.
- Flujos de negocio con múltiples operaciones.

Evitar tests para:

- Getters y setters.
- Constructores triviales.
- DTOs.
- Records sin lógica.
- Entidades sin comportamiento.
- Configuración de DI.
- Configuración de ASP.NET Core.
- Código generado.
- Mapeos simples.
- Controladores que solo delegan.
- Repositorios que solo invocan EF Core.
- Cambios puramente visuales sin lógica relevante.

Preferir pocos tests significativos.

No perseguir el 100% de cobertura.

No generar automáticamente cinco o más tests para proteger un método trivial.

Si un único test cubre correctamente una lógica sencilla, no crear tests adicionales innecesarios.

---

## Tests de Integración con MySQL

- Si tenés MySQL local con `root` sin password en puerto 3306, los tests `[MySqlFact]` corren automáticamente contra `sgv_test`.
- `MySqlFactAttribute` aplica `Database.Migrate()` una vez por sesión de test.
- `TestSgvDbContextFactory` resuelve la connection string mediante `ConnectionStrings__SgvDatabase`, archivos de configuración o defaults de desarrollo.
- Si no hay MySQL disponible, los tests `[MySqlFact]` se skipean limpiamente.
- Si tu MySQL local usa otro puerto, usuario o password, seteá `ConnectionStrings__SgvDatabase`.
- El factory de producción (`SgvDbContextFactory`) no utiliza defaults inseguros y exige configuración explícita.

---

## Decisiones Técnicas que NO conviene romper

- MySQL es el proveedor activo; no introducir supuestos de SQL Server.
- La unicidad sobre registros activos usa columnas generadas para convivir con soft delete.
- Identity mantiene `IdentityUser` con clave string.
- La auditoría centraliza eventos en una tabla `Auditorias` mediante interceptor de EF Core.
- `SGV.Api` valida autenticación solo con bearer token.
- `SGV.Web` depende del bridge por cookie + `ApiBearerTokenHandler`.
- Los listados segmentados de cargos, habilidades y unidades organizativas usan `status=activas|eliminadas`.
- El organigrama utiliza Google OrgChart.
- Las operaciones write de cargos, habilidades y usuarios están protegidas por rol `Administrador`.
- `SGV.Web` es una shell Razor Pages apoyada en Inspinia.
- La cookie de autenticación web y la política CORS de la API se endurecieron por ambiente en la issue #101.
- Revisá `docs/decisiones-implementacion.md` antes de modificar persistencia, auditoría, ocupaciones o seguridad.
- **Mapa de bloques GUID para catálogos inmutables**: `70000000-…` reservado para `NivelCargo`, `71000000-…` reservado para `TipoDocumento`, `72000000-…` reservado para `CategoriaHabilidad`. Cualquier catálogo inmutable nuevo debe pedir un bloque contiguo y actualizar la documentación correspondiente.

---

## OpenSpec / SDD

OpenSpec/SDD es obligatorio para tareas clasificadas como **GRANDES** y opcional para tareas MEDIANAS cuando aporte valor.

No aplicar OpenSpec automáticamente a tareas TRIVIALES o PEQUEÑAS.

### Cuándo utilizar OpenSpec

Utilizar OpenSpec cuando:

- Se introduce una funcionalidad importante.
- Se crea un módulo significativo.
- Se modifica arquitectura.
- Se modifica una decisión técnica importante.
- Se modifica significativamente persistencia.
- Se modifica seguridad.
- Se modifican contratos públicos de forma importante.
- Se requiere coordinación entre múltiples partes del sistema.
- Se requiere una especificación que deba mantenerse como referencia.

### Cuándo NO utilizar OpenSpec

No utilizar OpenSpec para:

- Cambios visuales.
- Cambios de textos.
- Cambios pequeños de UI.
- Cambios localizados.
- Correcciones triviales.
- Ordenamientos simples.
- Modificaciones pequeñas en una Razor Page.
- Cambios que puedan resolverse directamente sin decisiones arquitectónicas.

### Reglas OpenSpec

- Antes de trabajar sobre una tarea MEDIANA o GRANDE, revisá los artefactos OpenSpec directamente relacionados.
- Para tareas GRANDE, seguir el proceso SDD completo.
- Antes de crear un nuevo cambio OpenSpec, verificar si existe un cambio relacionado.
- No sobrescribas artefactos del cambio sin preservar trabajo previo del usuario.
- Si el pedido es solo de planificación, no implementes código ni migraciones.
- Si el CLI de OpenSpec no está disponible, creá/manualizá los archivos esperados e informá que no se pudo validar con la herramienta.
- Todo artefacto SDD nuevo o actualizado debe quedar en español.

### Artefactos SDD

Según corresponda:

- `exploration.md`
- `proposal.md`
- `design.md`
- `tasks.md`
- `apply-progress.md`
- `verify-report.md`
- `archive-report.md`
- `specs/**/spec.md`

No crear un artefacto únicamente para cumplir formalmente con una lista.

Cada artefacto debe aportar información útil al proceso.

---

## Confirmaciones y Solicitudes de Cambio

- Usá conventional commits breves, por ejemplo `feat: add ocupaciones query service` o `fix: separate db update exception handling`.
- Nunca agregues `Co-Authored-By` ni atribución a IA.

---

## Instrucciones para Agentes

- Toda respuesta conversacional al usuario debe salir en español.
- Verificá claims técnicos contra código o documentos antes de afirmarlos.
- Preservá cambios del usuario en curso.
- Determiná primero la complejidad de la tarea.
- Aplicá el proceso proporcional a esa complejidad.
- Para tareas TRIVIALES y PEQUEÑAS, evitá SDD y análisis de repositorio innecesarios.
- Para tareas MEDIANAS, utilizá planificación proporcional y revisá documentación relevante.
- Para tareas GRANDES, utilizá OpenSpec/SDD.
- Antes de tocar estructura, dependencias, persistencia o reglas de negocio en tareas MEDIANAS o GRANDES, revisá los artefactos OpenSpec relevantes y `docs/decisiones-implementacion.md`.
- Si tocás algo que afecte build o test, corré la validación mínima relevante.
- Si generás documentos en cualquier fase SDD, redactalos en español y alineados con el estado real del repo.
- No generes tests de bajo valor.
- No generes documentación de bajo valor.
- No realices refactorizaciones no solicitadas.
- No aumentes el alcance de una tarea sin una razón técnica necesaria.
- Si durante la implementación descubrís que el alcance o riesgo es mayor que el inicialmente estimado, elevá el nivel de proceso.
- Skills del proyecto disponibles en `.agents/skills/`:
  - `database-designer`
  - `dotnet-best-practices`
  - `dotnet-csharp`
  - `dotnet-xunit`
  - `mysql`
  - `pr-review-dotnet`
  - `razor-pages-patterns`
  - `enriquecer-issue`
  - `caveman` (modo de comunicación ultra-comprimido; se activa con "caveman mode" o `/caveman`)

---

## Regla final

**No confundir rigurosidad con cantidad de pasos.**

Una tarea trivial debe resolverse trivialmente.

Una tarea pequeña debe resolverse con un proceso pequeño.

Una tarea mediana debe recibir planificación y validación proporcional.

Una tarea grande debe recibir el proceso completo de ingeniería.

El objetivo de OpenCode no es producir la mayor cantidad posible de análisis, documentos o tests.

El objetivo es producir **el cambio correcto, con la calidad necesaria y con el menor costo razonable de tiempo y tokens**.
