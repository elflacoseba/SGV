## Exploración: Agregar proyecto API con controladores tradicionales

### Estado actual
La solución `SGV.slnx` contiene cuatro proyectos: `SGV.Dominio`, `SGV.Aplicacion`, `SGV.Infraestructura` y `SGV.Tests`, todos apuntando a `net10.0` con nullable e implicit usings habilitados. No existe todavía un proyecto ASP.NET Core API ni `Program.cs`; la exposición HTTP es nueva.

La arquitectura actual separa dominio, aplicación e infraestructura. `SGV.Dominio` contiene entidades ricas con constructores/métodos de dominio y colecciones privadas. `SGV.Aplicacion` contiene servicios puros como `ServicioCompatibilidadHabilidades` y la abstracción `IUsuarioActual`. `SGV.Infraestructura` contiene `SgvDbContext`, configuraciones EF Core, migraciones, seed data, Identity EF Core y Pomelo/MySQL 8. No hay repositorios ni unit of work existentes; hoy el acceso a datos está centrado en `SgvDbContext`.

Las pruebas usan xUnit 2.9.2 en `tests/SGV.Tests`. Hay pruebas unitarias de aplicación y pruebas de modelo EF/Pomelo. `strict_tdd: true` está configurado en `openspec/config.yaml`, por lo que el cambio API debería empezar con pruebas de contratos/controladores/servicios antes de implementar.

### Áreas afectadas
- `SGV.slnx` — deberá incluir el nuevo proyecto API.
- `src/SGV.Api/SGV.Api.csproj` — nuevo proyecto ASP.NET Core Web API con controladores y Swagger/OpenAPI.
- `src/SGV.Api/Program.cs` — composición de la aplicación: controllers, Swagger, ProblemDetails, CORS si aplica, DbContext, repositories, unit of work, application services.
- `src/SGV.Api/Controllers/` — controladores tradicionales; no Minimal API.
- `src/SGV.Api/Models/` o `Contracts/` — request/response DTOs para evitar exponer entidades EF/domain directamente.
- `src/SGV.Aplicacion/` — contratos de servicios de caso de uso y, si se decide mantener Clean Architecture estricta, abstracciones de repositorios/unit of work.
- `src/SGV.Infraestructura/Persistencia/` — implementaciones EF Core de repositories/unit of work y registro DI.
- `src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs` — requiere `IUsuarioActual`; sin autenticación habrá que proveer una implementación API anónima/sistema para auditoría.
- `tests/SGV.Tests/` — nuevas pruebas unitarias/integración para servicios, repositorios y controladores/API behavior.
- `openspec/specs/` — futura delta spec para comportamiento observable de API, Swagger y ausencia temporal de auth.

### Enfoques
1. **Clean Architecture strict: controllers -> application services -> repository/unit of work abstractions -> infrastructure EF implementations** — El API referencia Aplicación e Infraestructura solo para composición; los controladores delegan en servicios de aplicación; las abstracciones viven en Aplicación o Dominio según semántica del agregado; Infraestructura implementa EF Core.
   - Pros: Mantiene límites claros; facilita pruebas de servicios sin EF; evita controladores gordos; permite cambiar persistencia sin tocar casos de uso.
   - Cons: Más archivos iniciales; requiere diseñar DTOs, servicios y repositorios por caso de uso/aggregate; riesgo de sobre-abstracción si se crea un repositorio genérico.
   - Esfuerzo: Medio

2. **API-first thin services with direct DbContext in services** — Controladores tradicionales delegan en servicios ubicados en Aplicación o API, pero esos servicios usan `SgvDbContext` directamente mediante Infraestructura.
   - Pros: Menos boilerplate; EF Core ya actúa como repository/unit of work; implementación inicial más rápida.
   - Cons: No cumple claramente el pedido explícito de repository/unit of work; acopla servicios a EF; debilita Clean Architecture y complica pruebas unitarias puras.
   - Esfuerzo: Bajo

3. **Generic repository + generic unit of work over DbContext** — Crear `IRepository<T>` genérico, `Repository<T>` y `IUnitOfWork` para todos los CRUDs.
   - Pros: Patrón conocido; reduce duplicación superficial en CRUD simple.
   - Cons: Abstracción pobre para agregados; suele filtrar queries o esconder capacidades de EF; no expresa cargas específicas (`Include`, invariantes, soft delete); entra en tensión con el diseño de dominio.
   - Esfuerzo: Medio

### Recomendación
Avanzar con el Enfoque 1. Crear un proyecto `SGV.Api` con controladores tradicionales, Swagger y configuración explícita de MVC, manteniendo controladores delgados y servicios de aplicación como fachada de casos de uso. Definir repositorios específicos por agregado/caso de lectura en lugar de un repositorio genérico; usar `IUnitOfWork` como abstracción de `SaveChangesAsync` cuando un caso de uso coordine varios repositorios. Registrar todo como scoped junto con `SgvDbContext` porque EF Core y unit of work deben vivir por request.

Para esta primera API, mantener autenticación/autorización fuera de alcance: no agregar Identity endpoints, JWT, policies ni `[Authorize]`. Aun así, `IUsuarioActual` necesita una implementación segura de transición que devuelva `null`/usuario sistema y correlation id del request para no romper auditoría.

### Riesgos
- El proyecto ya referencia Identity EF Core y seed roles, pero el usuario pidió sin auth; la propuesta debe separar persistencia Identity existente de exposición/autorización API.
- `AuditoriaSaveChangesInterceptor` depende de `IUsuarioActual`; si se registra DbContext con el interceptor y no se registra una implementación anónima, la API fallará en DI.
- Repositorios genéricos pueden degradar el modelo Clean Architecture; conviene especificar repositorios por agregado o caso de uso.
- Swagger sin auth expone todos los endpoints disponibles; limitar alcance funcional inicial y no exponer endpoints administrativos sensibles por accidente.
- MySQL/Pomelo requiere mantener EF Core packages en 9.x aunque la API apunte a .NET 10.
- `strict_tdd: true` exige planear pruebas antes de implementación; probablemente se necesite paquete de test host ASP.NET Core si se agregan pruebas de integración HTTP.

### Listo para propuesta
Sí — la propuesta debería acotar el cambio a crear `SGV.Api` con controladores MVC tradicionales, Swagger, DTOs, servicios de aplicación, repositories específicos, unit of work EF Core y configuración DI. Debe declarar como no objetivos: Minimal API, autenticación/autorización, Identity endpoints y cambios de esquema no indispensables.
