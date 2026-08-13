using System.Linq.Expressions;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Especificaciones;

/// <summary>
/// Predicados reutilizables sobre <see cref="OcupacionEntity"/>.
/// Centralizan la noción de "vigente" (no soft-deleted y no finalizada)
/// y su complemento "no vigente" para que cualquier futuro cambio de la
/// regla (por ejemplo, agregar un bound de <c>FechaInicio</c>) se propague
/// a todo el código de persistencia que la consume.
/// <para>
/// Réplica a nivel de entidad de la semántica de <c>Ocupacion.EsVigente</c>
/// del agregado de dominio, evitando hidratar la Ocupacion cuando sólo se
/// necesita el predicado.
/// </para>
/// </summary>
public static class OcupacionEntitySpecs
{
    /// <summary>
    /// True cuando la Ocupacion está activa y no fue finalizada ni
    /// borrada lógicamente: <c>!IsDeleted AND FechaFin IS NULL</c>.
    /// Compone con <c>IQueryable&lt;OcupacionEntity&gt;.Where(...)</c> y
    /// con <c>.Any(...)</c> / <c>.AnyAsync(...)</c> / <c>.FirstOrDefaultAsync(...)</c>.
    /// </summary>
    public static readonly Expression<Func<OcupacionEntity, bool>> EsVigente =
        o => !o.IsDeleted && o.FechaFin == null;

    /// <summary>
    /// Complemento estricto de <see cref="EsVigente"/>: True cuando la
    /// Ocupacion fue finalizada o borrada lógicamente
    /// (<c>IsDeleted OR FechaFin != null</c>). Usado por el segmento
    /// "Eliminadas" de los listados paginados para mantener simetría con
    /// <see cref="EsVigente"/>: cualquier evolución de la regla base se
    /// propaga automáticamente a su complemento sin duplicar la expresión
    /// inline.
    /// </summary>
    public static readonly Expression<Func<OcupacionEntity, bool>> NoEsVigente =
        o => o.IsDeleted || o.FechaFin != null;
}
