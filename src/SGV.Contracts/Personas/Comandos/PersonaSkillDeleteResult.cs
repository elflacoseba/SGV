using System.Net;
using SGV.Contracts.Comun;

namespace SGV.Contracts.Personas.Comandos;

/// <summary>
/// Typed result of a <c>DELETE /api/v1/personas/{personaId}/skills/{skillId}</c>
/// consumed by the web shell. Shape espejo de <c>CargoSkillDeleteResult</c>
/// para mantener consistente el seam de integración: <see cref="Categoria"/>
/// es la fuente de verdad observable para que la Razor Page de PR3b pueda
/// ramificar por la taxonomía común sin comparar
/// <see cref="System.Net.HttpStatusCode"/> contra constantes HTTP.
/// </summary>
/// <remarks>
/// El lado HTTP devuelve <c>204 No Content</c> en éxito y <c>404 Not Found</c>
/// cuando la asociación no existe. Cualquier otro status se traduce al
/// resultado no exitoso preservando <see cref="StatusCode"/> como
/// metadata y propagando título/detalle cuando vienen desde
/// <c>ProblemDetails</c>.
/// </remarks>
public sealed record PersonaSkillDeleteResult(
    bool Succeeded,
    HttpStatusCode? StatusCode,
    string? Code,
    string? Message,
    ErrorCategoria Categoria = ErrorCategoria.NotFound);
