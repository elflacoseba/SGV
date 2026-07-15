using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Web.Integration.Personas;

namespace SGV.Web.Integration.Usuarios;

/// <summary>
/// Implementación HTTP del catálogo de
/// <see cref="PersonaDto"/> activas expuesto por
/// <see cref="IPersonaOptionsProvider"/>. Reenvía la consulta a
/// <see cref="IPersonaApiClient.GetAllActivasAsync"/>, que ya existe y
/// propaga el bearer token del cookie-auth ticket. Se registra como
/// <c>Transient</c> en <c>Program.cs</c> para permitir su reemplazo
/// por un fake en la suite web.
/// </summary>
/// <remarks>
/// <para>
/// Razones para una clase wrapper en lugar de consumir
/// <see cref="IPersonaApiClient"/> directo desde la PageModel:
/// <list type="bullet">
/// <item>
/// desacopla la page de la implementación HTTP, lo que permite
/// triangular el dropdown con un fake en la suite de tests;
/// </item>
/// <item>
/// mantiene una superficie estrecha: el único método que la page
/// necesita es <c>GetActivasAsync</c>;
/// </item>
/// <item>
/// aísla la transición si el backend de Personas cambia el shape
/// (e.g. introduce paginación o búsqueda) — el cambio queda
/// contenido en esta clase, no en cada PageModel que lo usa.
/// </item>
/// </list>
/// </para>
/// <para>
/// No agregamos caché: el dropdown de Create se renderiza al abrir
/// la Razor Page (operación infrecuente). Si en el futuro hace falta
/// cachear, el reemplazo del HttpClient es transparente porque el
/// <see cref="IPersonaApiClient"/> vive en el composition root.
/// </para>
/// </remarks>
public sealed class HttpPersonaOptionsProvider(IPersonaApiClient personaApiClient) : IPersonaOptionsProvider
{
    /// <inheritdoc />
    public Task<IReadOnlyList<PersonaDto>> GetActivasAsync(CancellationToken cancellationToken = default)
        => personaApiClient.GetAllAsync(cancellationToken);
}
