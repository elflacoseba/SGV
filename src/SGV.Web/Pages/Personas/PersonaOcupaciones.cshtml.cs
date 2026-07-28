using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Personas;

namespace SGV.Web.Pages.Personas;

/// <summary>
/// PageModel de la página cruzada <c>/personas/{id:guid}/ocupaciones</c>
/// del change #208 / Slice 3b (REQ-OCC-NAV-001, NAV-004..006).
/// Espejo contextual de <c>PersonaHabilidadesModel</c>: fija
/// <see cref="OcupacionSegmentoListado.Activas"/> y el <c>PersonaId</c>
/// en la query, gatea por <c>Persona.IsActive</c> y expone el botón
/// "Nueva ocupación" sólo a Administrador. A diferencia de
/// <c>PersonaHabilidadesModel</c>, NO requiere rol administrador para
/// la lectura — cualquier usuario autenticado puede ver las
/// ocupaciones vigentes de la persona dueña.
/// </summary>
[Authorize]
public sealed class PersonaOcupacionesModel(
    IPersonaApiClient personaApiClient,
    IOcupacionApiClient ocupacionApiClient,
    ILogger<PersonaOcupacionesModel> logger) : PageModel
{
    /// <summary>Tamaño de página fijo para la grilla cruzada.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Identificador de la persona dueña (route <c>{id:guid}</c>).</summary>
    public Guid PersonaId { get; private set; }

    /// <summary>Nombre completo de la persona dueña para el encabezado.</summary>
    public string PersonaNombre { get; private set; } = string.Empty;

    /// <summary>Filas visibles en la página actual.</summary>
    public IReadOnlyList<OcupacionListItemViewModel> Items { get; private set; } = [];

    /// <summary>Total de ocupaciones vigentes asociadas a la persona.</summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// Indica que la persona dueña no está disponible (inexistente o
    /// inactiva). La vista debe mostrar un estado recuperable sin
    /// invocar el listado de ocupaciones.
    /// </summary>
    public bool IsNotFound { get; private set; }

    /// <summary>
    /// Mensaje de error visible cuando falla la carga del listado por
    /// transporte (red, timeout, JSON malformado).
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Indica si el usuario actual tiene el rol Administrador (gating
    /// del botón "Nueva ocupación", REQ-OCC-NAV-006).
    /// </summary>
    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    /// <summary>
    /// Handler GET de la página cruzada. Verifica primero que la
    /// persona dueña exista y esté activa; si falla, marca
    /// <see cref="IsNotFound"/> y NO invoca al cliente de ocupaciones.
    /// Si la persona es válida, consulta las ocupaciones vigentes con
    /// <c>Segmento=Activas</c> y <c>PersonaId</c> fijo; el parámetro
    /// <c>status</c> del query string se ignora (REQ-OCC-NAV-004 —
    /// "Status inyectado se ignora y conserva el segmento activo").
    /// </summary>
    public async Task OnGetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        PersonaId = id;

        try
        {
            var persona = await personaApiClient.GetByIdAsync(id, cancellationToken);
            if (persona is null || !persona.IsActive)
            {
                IsNotFound = true;
                logger.LogWarning(
                    "Persona with Id {PersonaId} is not available for ocupaciones cross-page.",
                    id);
                return;
            }

            PersonaNombre = $"{persona.Nombres} {persona.Apellidos}";
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex,
                "Failed to load persona {PersonaId} for ocupaciones cross-page.",
                id);
            IsNotFound = true;
            return;
        }

        try
        {
            var query = new OcupacionListQuery(
                Page: 1,
                PageSize: DefaultPageSize,
                Search: null,
                Sort: null,
                Segmento: OcupacionSegmentoListado.Activas,
                PersonaId: id);

            var result = await ocupacionApiClient.ListarAsync(query, cancellationToken);

            TotalCount = Math.Max(0, result.TotalCount);
            Items = result.Items.Select(OcupacionListItemViewModel.FromDto).ToArray();
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex,
                "Failed to load ocupaciones for persona {PersonaId}.",
                id);
            ErrorMessage = "No se pudo cargar el listado de ocupaciones de la persona. Intentá nuevamente.";
        }
    }

    /// <summary>
    /// Construye los route values para el botón "Nueva ocupación" que
    /// REQ-OCC-NAV-006 requiere: precarga <c>personaId</c> para que
    /// <c>Create</c> lo bindee en el selector dueño.
    /// </summary>
    public object BuildNewOcupacionRouteValues() => new
    {
        personaId = PersonaId
    };

    /// <summary>
    /// URL absoluta al detalle de la persona dueña. El Details de
    /// Personas ya preserva su propio contexto (<c>p/search/sort/status</c>);
    /// acá no se transporta contexto adicional porque la página cruzada
    /// no tiene filtros propios que propagar.
    /// </summary>
    public string BuildPersonaDetailsUrl()
        => $"/personas/detalle/{PersonaId:D}";
}