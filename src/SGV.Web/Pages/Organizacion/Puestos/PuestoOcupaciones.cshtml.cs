using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Integration.Vacantes;
using SGV.Web.Pages.Organizacion.Ocupaciones;

namespace SGV.Web.Pages.Organizacion.Puestos;

/// <summary>
/// PageModel de la página cruzada <c>/organizacion/puestos/{id:guid}/ocupaciones</c>
/// del change #208 / Slice 3b (REQ-OCC-NAV-002, NAV-004..006). Espejo de
/// <see cref="Personas.PersonaOcupacionesModel"/> con filtro
/// <see cref="OcupacionListQuery.PuestoId"/> en lugar de
/// <see cref="OcupacionListQuery.PersonaId"/>.
/// Gatea por <c>Puesto.IsActive</c> (proxy: la API devuelve <c>null</c>
/// para puestos inactivos en <see cref="IPuestosApiClient.GetByIdAsync"/>).
/// Acceso autenticado sin rol Administrador para lectura; el botón
/// "Nueva ocupación" se gated por rol.
/// </summary>
/// <remarks>
/// Refactor (PR #215 review): implementa <see cref="IOcupacionesCrossList"/>
/// para compartir el partial <c>_CrossList.cshtml</c> con
/// <c>PersonaOcupaciones</c>. La vista cruzada no expone paginación
/// (volumen esperado ≤ 20 por entidad dueña; ver
/// <see cref="IOcupacionesCrossList"/>).
/// T-7.2 (change <c>vacante-ocupacion-flow-alignment</c>):
/// agrega banderines <c>HayVacanteAbierta</c>, <c>HayOcupacionActiva</c>
/// y botón "Abrir Vacante" (REQ-OCC-NAV-007) cuando el Puesto no tiene
/// Vacante abierta y el usuario es Administrador.
/// </remarks>
[Authorize]
public sealed class PuestoOcupacionesModel(
    IPuestosApiClient puestosApiClient,
    IOcupacionApiClient ocupacionApiClient,
    IVacanteApiClient vacanteApiClient,
    ILogger<PuestoOcupacionesModel> logger) : PageModel, IOcupacionesCrossList
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
    /// T-7.2 (change <c>vacante-ocupacion-flow-alignment</c>):
    /// <c>true</c> cuando el Puesto due\u00f1o tiene al menos una Vacante
    /// abierta. Se setea en <see cref="OnGetAsync"/> consultando
    /// <see cref="IVacanteApiClient.ExisteVacanteAbiertaParaPuestoAsync"/>.
    /// Default: <c>false</c> — degradación optimista alineada con la
    /// política de <see cref="IVacanteApiClient.ExisteVacanteAbiertaParaPuestoAsync"/>
    /// (que degrada a <c>false</c> ante fallo de transporte). La UI
    /// prefiere mostrar el botón NAV-007 y dejar que el usuario
    /// descubra que el camino no aplica, antes que ocultarlo
    /// silenciosamente.
    /// </summary>
    public bool HayVacanteAbierta { get; private set; }

    /// <summary>
    /// T-7.2: <c>true</c> cuando el Puesto tiene una Ocupaci\u00f3n
    /// activa. Calculado a partir de <see cref="TotalCount"/> y el
    /// listado vigente cargado. Conserva la semántica NAV-006 original
    /// (mostrar "Ver Ocupaci\u00f3n vigente" en lugar de "Nueva Ocupaci\u00f3n"
    /// cuando ya hay una).
    /// </summary>
    public bool HayOcupacionActiva => TotalCount > 0;

    // ── IOcupacionesCrossList ─────────────────────────────────────────────

    /// <inheritdoc/>
    string IOcupacionesCrossList.HeaderTitle
        => $"Ocupaciones vigentes del puesto";

    /// <inheritdoc/>
    string IOcupacionesCrossList.HeaderSubtitleLabel => "Puesto";

    /// <inheritdoc/>
    string IOcupacionesCrossList.HeaderSubtitleValue => PuestoNombre;

    /// <inheritdoc/>
    string? IOcupacionesCrossList.HeaderSubtitleBadge
        => string.IsNullOrWhiteSpace(PuestoCodigo) ? null : PuestoCodigo;

    /// <inheritdoc/>
    string IOcupacionesCrossList.HeaderSubtitleBadgeClass => "badge-soft-secondary";

    /// <inheritdoc/>
    string IOcupacionesCrossList.EmptyMessage
        => "Este puesto no tiene ocupaciones vigentes asignadas.";

    /// <inheritdoc/>
    string IOcupacionesCrossList.NotFoundHeading
        => "El puesto solicitado no está disponible.";

    /// <inheritdoc/>
    string IOcupacionesCrossList.NotFoundBody
        => "Es posible que el puesto haya sido desactivado, eliminado o que haya ocurrido un error al consultarlo.";

    /// <inheritdoc/>
    string IOcupacionesCrossList.NotFoundReturnUrl => "/organizacion/puestos";

    /// <inheritdoc/>
    string IOcupacionesCrossList.NotFoundReturnLabel
        => "Volver al listado de puestos";

    /// <inheritdoc/>
    string IOcupacionesCrossList.BackLinkUrl
        => $"/organizacion/puestos/detalles/{PuestoId:D}";

    /// <inheritdoc/>
    object? IOcupacionesCrossList.NewOcupacionRouteValues
        => HayVacanteAbierta && !HayOcupacionActiva
            ? new { puestoId = PuestoId }
            : null;

    /// <inheritdoc/>
    object? IOcupacionesCrossList.VerOcupacionVigenteRouteValues
    {
        get
        {
            if (!HayOcupacionActiva)
            {
                return null;
            }

            var firstId = Items.FirstOrDefault()?.Id;
            return firstId.HasValue ? new { id = firstId.Value } : null;
        }
    }

    /// <inheritdoc/>
    string? IOcupacionesCrossList.DisponibilidadMessage
        => !HayVacanteAbierta && !HayOcupacionActiva
            ? "No hay una Vacante abierta para este Puesto. Abra una Vacante para iniciar el flujo de cobertura."
            : null;

    /// <inheritdoc/>
    string IOcupacionesCrossList.CrossEntityColumnHeader => "Persona";

    /// <inheritdoc/>
    CrossEntityColumn IOcupacionesCrossList.ColumnSelector => CrossEntityColumn.Persona;

    /// <inheritdoc/>
    string IOcupacionesCrossList.CrossEntityCellClass => "fw-medium";

    /// <inheritdoc/>
    bool IOcupacionesCrossList.RenderCrossEntityCellAsBadge => false;

    /// <summary>
    /// T-7.2 (NAV-007): URL del botón "Abrir Vacante" con
    /// <c>puestoId</c> precargado y <c>returnUrl</c> hacia el
    /// <c>Puesto/Details</c>. Solo se rendereaa cuando NO hay Vacante
    /// abierta y el usuario es Administrador. El partial
    /// <c>_CrossList.cshtml</c> lo consume vía
    /// <see cref="IOcupacionesCrossList.AbrirVacanteUrl"/>.
    /// </summary>
    string? IOcupacionesCrossList.AbrirVacanteUrl
        => !HayVacanteAbierta && User.IsInRole(RolesSgv.Administrador)
            ? $"/Organizacion/Vacantes/Create?puestoId={PuestoId:D}&returnUrl=/Organizacion/Puestos/Details/{PuestoId:D}"
            : null;

    // ── Handler ──────────────────────────────────────────────────────────

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

        // T-7.2: una vez confirmada la lectura del Puesto, consultamos si
        // tiene una Vacante abierta para decidir el render del botón NAV-007.
        // Política de degradación unificada: tanto este cliente
        // (<c>VacanteApiClient.ExisteVacanteAbiertaParaPuestoAsync</c>) como
        // esta propiedad degradan a <c>false</c> ante fallo de transporte
        // para mostrar el botón NAV-007. Un fallo de transporte revela así
        // la acción y deja al usuario descubrir que el camino no aplica,
        // en vez de ocultar el botón silenciosamente.
        HayVacanteAbierta = await vacanteApiClient
            .ExisteVacanteAbiertaParaPuestoAsync(id, cancellationToken)
            .ConfigureAwait(false);
    }
}
