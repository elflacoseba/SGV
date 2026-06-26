# Exploración: módulo de Unidades Organizativas en el frontend

## Estado actual

- `SGV.Web` hoy es una shell Razor Pages mínima: solo tiene `Index`, `Auth/SignIn` y `Auth/Logout`, con layout vertical por defecto y navegación reducida a Home.
- La integración web con el API existe solo para autenticación (`IAuthApiClient`, `AuthApiClient`, `SgvApiOptions`). No hay un cliente genérico para otros módulos.
- El backend ya expone un contrato completo para Unidades Organizativas en `SGV.Api`:
  - `GET /api/v1/unidades-organizativas`
  - `GET /api/v1/unidades-organizativas/{id}`
  - `GET /api/v1/unidades-organizativas/consulta`
  - `GET /api/v1/unidades-organizativas/arbol`
  - `POST /api/v1/unidades-organizativas`
  - `PUT /api/v1/unidades-organizativas/{id}`
  - `PATCH /api/v1/unidades-organizativas/{id}/unidad-padre`
  - `PATCH /api/v1/unidades-organizativas/{id}/reactivar`
  - `DELETE /api/v1/unidades-organizativas/{id}`
- También existe el catálogo `TipoUnidadesOrganizativas` con `GET /api/v1/tipos-unidad-organizativa` y `GET /api/v1/tipos-unidad-organizativa/{id}`.
- OpenSpec ya contiene specs base relevantes (`sgv-web-shell`, `sgv-web-authentication`, `unidad-organizativa-crud`, `tipo-unidad-organizativa-catalog`), pero no encontré un change activo para este frontend.

## Áreas afectadas

- `src/SGV.Web/Pages/**` — nuevas páginas Razor del módulo, PageModels, validación de formularios y rutas.
- `src/SGV.Web/Integration/**` — cliente tipado para consumir Unidades Organizativas y catálogo de tipos desde `SGV.Api`.
- `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` — navegación para exponer el módulo.
- `src/SGV.Web/Program.cs` — registro de nuevos clientes/configuración si hace falta.
- `tests/SGV.Tests/Web/**` — smoke tests e integración web para navegación y flujos del módulo.
- `openspec/changes/implementa-el-modulo-de-unidadesorganizativas-en-el-frontend/exploration.md` — artefacto de esta fase.

## Enfoques

1. **Módulo Razor Pages integrado a la shell actual** — agregar páginas bajo `Pages/Organizacion/UnidadesOrganizativas`, con listado, alta/edición, detalle/árbol y acciones de baja/reactivación consumiendo `SGV.Api`.
   - Pros: encaja con la arquitectura actual de `SGV.Web`, reutiliza layouts y el patrón de autenticación existente, y mantiene la UI consistente.
   - Contras: requiere definir el cliente web para el módulo y su superficie de formularios/validaciones.
   - Esfuerzo: Medio.

2. **Exponer el módulo solo como navegación + páginas mínimas** — crear una entrada de menú y una pantalla inicial sin cubrir las operaciones completas del backend.
   - Pros: menor esfuerzo inicial.
   - Contras: deja sin usar la mayoría de los contratos ya disponibles y no cumple el sentido del módulo funcional.
   - Esfuerzo: Bajo.

## Recomendación

Ir con el enfoque 1. El backend ya ofrece contratos suficientes para un módulo web real; la mejor base es replicar el patrón de autenticación web: cliente tipado, opciones de base URL, PageModels delgados y navegación en la shell.

## Riesgos

- Definir demasiado alcance de entrada: el backend permite árbol, consulta paginada, cambio de padre y reactivación; conviene acotar qué pantallas entran en el primer corte.
- No existe aún un contrato web para este módulo; habrá que decidir si se centralizan rutas del API como con autenticación o se resuelve el endpoint directamente en el cliente.
- La shell actual casi no tiene navegación ni páginas de módulo, así que el cambio tocará estructura de frontend además de formularios.

## Listo para propuesta

Sí. Ya hay suficientes evidencias de arquitectura, rutas y huecos del frontend para redactar la propuesta sin inventar alcance.
