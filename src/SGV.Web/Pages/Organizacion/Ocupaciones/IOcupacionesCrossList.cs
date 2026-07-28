using SGV.Web.Integration.Ocupaciones;

namespace SGV.Web.Pages.Organizacion.Ocupaciones;

/// <summary>
/// Contrato compartido por las dos Razor Pages de navegación cruzada
/// de Ocupaciones (REQ-OCC-NAV-001..006): <c>PersonaOcupaciones</c> y
/// <c>PuestoOcupaciones</c>. El partial <c>_CrossList.cshtml</c> lo
/// consume para renderear la grilla con filtro contextual fijo.
/// </summary>
/// <remarks>
/// Espejo del patrón <see cref="IOcupacionForm"/>: la vista concentra
/// toda la lógica de UI (card + IsNotFound + tabla + footer) y el
/// PageModel aportante sólo conoce los textos y URLs propios de su
/// entidad dueña. No conoce HTML ni Razor.
/// <para>
/// Decisión de diseño explícita: la vista cruzada no expone paginación
/// porque las ocupaciones vigentes de una sola persona o un solo puesto
/// suelen ser ≤ 20 en la práctica (REQ-OCC-NAV-001/002). Si el volumen
/// crece por encima del soft-cap, el cambio se localiza en este
/// partial sin tocar las páginas.
/// </para>
/// </remarks>
public interface IOcupacionesCrossList
{
    /// <summary>Filas visibles en la página actual.</summary>
    IReadOnlyList<OcupacionListItemViewModel> Items { get; }

    /// <summary>Total de ocupaciones vigentes asociadas a la entidad dueña.</summary>
    int TotalCount { get; }

    /// <summary>
    /// Mensaje de error visible cuando falla la carga del listado por
    /// transporte (red, timeout, JSON malformado). <c>null</c> cuando
    /// no hay error.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Indica que la persona o puesto dueño no está disponible
    /// (inexistente o inactivo). La vista debe mostrar un estado
    /// recuperable sin invocar el listado.
    /// </summary>
    bool IsNotFound { get; }

    /// <summary>Indica si el usuario actual tiene rol Administrador (gating del botón "Nueva").</summary>
    bool EsAdministrador { get; }

    /// <summary>Título principal del card (p.ej. "Ocupaciones vigentes de Ana García").</summary>
    string HeaderTitle { get; }

    /// <summary>Etiqueta del subtítulo (p.ej. "Persona" o "Puesto"). Se renderea en texto plano (HTML-escaped).</summary>
    string HeaderSubtitleLabel { get; }

    /// <summary>Valor destacado del subtítulo (p.ej. "Ana García" o "Analista"). Se renderea en <c>&lt;strong&gt;</c> HTML-escaped.</summary>
    string HeaderSubtitleValue { get; }

    /// <summary>Badge opcional al final del subtítulo (p.ej. código de puesto). <c>null</c> oculta el badge.</summary>
    string? HeaderSubtitleBadge { get; }

    /// <summary>Clases CSS del badge del subtítulo. Se ignora cuando <see cref="HeaderSubtitleBadge"/> es <c>null</c>.</summary>
    string HeaderSubtitleBadgeClass { get; }

    /// <summary>Mensaje de estado vacío cuando la entidad dueña no tiene ocupaciones.</summary>
    string EmptyMessage { get; }

    /// <summary>Título del estado NotFound (p.ej. "La persona solicitada no está disponible").</summary>
    string NotFoundHeading { get; }

    /// <summary>Cuerpo del estado NotFound con copy para casos inactivo/inexistente/error de transporte.</summary>
    string NotFoundBody { get; }

    /// <summary>URL de retorno del estado NotFound (p.ej. "/personas").</summary>
    string NotFoundReturnUrl { get; }

    /// <summary>Etiqueta del botón de retorno del estado NotFound (p.ej. "Volver al listado de personas").</summary>
    string NotFoundReturnLabel { get; }

    /// <summary>URL del botón "Volver" del footer (Details dueño).</summary>
    string BackLinkUrl { get; }

    /// <summary>
    /// Route values para el botón "Nueva ocupación" admin-gated. <c>null</c>
    /// deshabilita el botón aunque el usuario sea admin (no se usa hoy).
    /// </summary>
    object? NewOcupacionRouteValues { get; }

    /// <summary>Etiqueta de la columna que muestra la entidad opuesta (p.ej. "Puesto" o "Persona").</summary>
    string CrossEntityColumnHeader { get; }

    /// <summary>Selector de la propiedad del <see cref="OcupacionListItemViewModel"/> a mostrar en la celda.</summary>
    CrossEntityColumn ColumnSelector { get; }

    /// <summary>Clases CSS adicionales para la celda <c>&lt;td&gt;</c> (p.ej. "fw-medium" en PersonaOcupaciones, vacío en PuestoOcupaciones).</summary>
    string CrossEntityCellClass { get; }

    /// <summary>
    /// Si <c>true</c>, el valor de la celda se envuelve en un
    /// <c>&lt;span class="badge badge-soft-secondary fs-xs"&gt;</c>.
    /// PersonaOcupaciones lo usa para el Puesto; PuestoOcupaciones usa
    /// texto plano con <see cref="CrossEntityCellClass"/>.
    /// </summary>
    bool RenderCrossEntityCellAsBadge { get; }
}

/// <summary>
/// Selector de cuál propiedad de <see cref="OcupacionListItemViewModel"/>
/// se muestra en la columna "entidad opuesta" de la grilla cruzada.
/// </summary>
public enum CrossEntityColumn
{
    /// <summary>Mostrar <c>PuestoNombre</c> (caso PersonaOcupaciones).</summary>
    Puesto,
    /// <summary>Mostrar <c>PersonaNombre</c> (caso PuestoOcupaciones).</summary>
    Persona
}
