using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.Puestos;

/// <summary>
/// PageModel de la página cruzada <c>/organizacion/puestos/{id:guid}/ocupaciones</c>
/// del change #208 / Slice 3b (REQ-OCC-NAV-002, NAV-004..006). Espejo de
/// <see cref="SGV.Web.Pages.Personas.PersonaOcupacionesModel"/> con filtro
/// <see cref="OcupacionListQuery.PuestoId"/> en lugar de
/// <see cref="OcupacionListQuery.PersonaId"/>.
/// Gatea por <c>Puesto.IsActive</c> (proxy: la API devuelve <c>null</c>
/// para puestos inactivos en <see cref="IPuestosApiClient.GetByIdAsync"/>).
/// Acceso autenticado sin rol Administrador para lectura; el botón
/// "Nueva ocupación" se gated por rol.
/// </summary>
[Authorize]
public sealed class PuestoOcupacionesModel(
    IPuestosApiClient puestosApiClient,
    IOcupacionApiClient ocupacionApiClient,
    ILogger<PuestoOcupacionesModel> logger) : PageModel
{
    /// <summary>Tamaño de página fijo para la grilla cruzada.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Identificador del puesto dueño (route <c>{id:guid}</c>).</summary>
    public Guid PuestoId { get; private set; }

    /// <summary>Nombre del puesto dueño para el encabezado.</summary>
    public string PuestoNombre { get; private set; } = string.Empty;

    /// <summary>Código del puesto dueño (referencia operativa).</summary>
    public string PuestoCodigo { get; private set; } = string.Empty;

    /// <summary>Filas visibles en la página actual.</summary>
    public IReadOnlyList<OcupacionListItemViewModel> Items { get; private set; } = [];

    /// <summary>Total de ocupaciones vigentes asociadas al puesto.</summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// Indica que el puesto dueño no está disponible (inexistente o
    /// inactivo — la API ya devuelve <c>null</c> para puestos inactivos
    /// vía <see cref="IPuestosApiClient.GetByIdAsync"/>). La vista debe
    /// mostrar un estado recuperable sin invocar el listado.
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
    /// Handler GET de la página cruzada. Verifica primero que el puesto
    /// dueño exista y esté activo (proxy: la API devuelve <c>null</c>
    /// para puestos inactivos). Si falla, marca <see cref="IsNotFound"/>
    /// y NO invoca al cliente de ocupaciones. Si el puesto es válido,
    /// consulta las ocupaciones vigentes con <c>Segmento=Activas</c> y
    /// <c>PuestoId</c> fijo; el parámetro <c>status</c> del query string
    /// se ignora (REQ-OCC-NAV-004).
    /// </summary>
    public async Task OnGetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        PuestoId = id;

        try
        {
            var puesto = await puestosApiClient.GetByIdAsync(id, cancellationToken);
            if (puesto is null)
            {
                IsNotFound = true;
                logger.LogWarning(
                    "Puesto with Id {PuestoId} is not available for ocupaciones cross-page.",
                    id);
                return;
            }

            PuestoNombre = puesto.Nombre;
            PuestoCodigo = puesto.Codigo;
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex,
                "Failed to load puesto {PuestoId} for ocupaciones cross-page.",
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
                PuestoId: id);

            var result = await ocupacionApiClient.ListarAsync(query, cancellationToken);

            TotalCount = Math.Max(0, result.TotalCount);
            Items = result.Items.Select(OcupacionListItemViewModel.FromDto).ToArray();
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex,
                "Failed to load ocupaciones for puesto {PuestoId}.",
                id);
            ErrorMessage = "No se pudo cargar el listado de ocupaciones del puesto. Intentá nuevamente.";
        }
    }

    /// <summary>
    /// Construye los route values para el botón "Nueva ocupación" que
    /// REQ-OCC-NAV-006 requiere: precarga <c>puestoId</c> para que
    /// <c>Create</c> lo bindee en el selector dueño.
    /// </summary>
    public object BuildNewOcupacionRouteValues() => new
    {
        puestoId = PuestoId
    };

    /// <summary>
    /// URL absoluta al detalle del puesto dueño. El Details de Puestos
    /// ya preserva su propio contexto (<c>p/search/sort/returnStatus</c>);
    /// acá no se transporta contexto adicional porque la página cruzada
    /// no tiene filtros propios que propagar.
    /// </summary>
    public string BuildPuestoDetailsUrl()
        => $"/organizacion/puestos/detalles/{PuestoId:D}";
}