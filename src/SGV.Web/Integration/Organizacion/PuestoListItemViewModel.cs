using System.Net;
using SGV.Contracts.Comun;
// Type alias (DEC-1) que conserva el nombre `PuestoListQuery` para los
// consumidores web, re-dirigiendo al record canónico de Contracts.
using PuestoListQuery = SGV.Contracts.Organizacion.Consultas.Dtos.PuestoListQuery;

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
///
/// <para>
/// A partir del change <c>2026-07-13-taxonomia-errores-commandresult</c>
/// (issue #125):
/// </para>
/// <list type="bullet">
///   <item><description><see cref="StatusCode"/> migra de
///   <see cref="HttpStatusCode"/> non-nullable a
///   <see cref="HttpStatusCode?"/> nullable para alinearse con los demás
///   <c>*DeleteResult</c> y absorber el caso "204 sin status code" sin
///   inconsistencias.</description></item>
///   <item><description>Se agrega <see cref="Categoria"/> para alinear la
///   forma con los demás <c>*DeleteResult</c> y permitir que la Razor Page
///   ramifique por la nueva taxonomía común
///   <see cref="ErrorCategoria"/>.</description></item>
/// </list>
/// </summary>
public sealed record PuestoDeleteResult(
    bool Succeeded,
    HttpStatusCode? StatusCode,
    string? Code,
    string? Message,
    ErrorCategoria Categoria = ErrorCategoria.NotFound);
