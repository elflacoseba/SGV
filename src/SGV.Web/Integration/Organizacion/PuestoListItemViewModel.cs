using System.Net;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// View model de grilla para el listado web de puestos activos.
/// </summary>
public sealed record PuestoListItemViewModel(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string UnidadOrganizativaNombre,
    string CargoNombre,
    Guid? PuestoSuperiorId)
{
    /// <summary>
    /// Etiqueta compuesta <c>Codigo — Nombre</c> usada por el <c>SelectList</c>
    /// de "Puesto superior" en los formularios de Create/Edit (PR 3).
    /// </summary>
    public string CodigoYNombre => $"{Codigo} — {Nombre}";
}

/// <summary>
/// Resultado de la baja lógica de un puesto traducida desde la API.
/// </summary>
public sealed record PuestoDeleteResult(bool Succeeded, HttpStatusCode? StatusCode, string? Code, string? Message);

/// <summary>
/// Contrato de consulta para el listado web de puestos. El backend de Puestos
/// no expone un endpoint segmentado (<c>/consulta?status=...</c>), por lo que
/// los filtros se aplican en memoria sobre <c>GetAllAsync()</c>. <c>Status</c>
/// se conserva para forward-compat con el futuro endpoint segmentado y para
/// que el toggle "Eliminadas" (deshabilitado en este slice) tenga un valor
/// coherente.
/// </summary>
public sealed record PuestoListQuery(string? Search, string? Sort, string? Status, int Page)
{
    /// <summary>Segmento por defecto: puestos activos.</summary>
    public const string SegmentoActivas = "activas";

    /// <summary>Segmento de puestos eliminados lógicamente (deshabilitado en este slice).</summary>
    public const string SegmentoEliminadas = "eliminadas";

    /// <summary>Consulta vacía: primer página del segmento activo sin filtros.</summary>
    public static PuestoListQuery Empty { get; } = new(null, null, SegmentoActivas, 1);
}
