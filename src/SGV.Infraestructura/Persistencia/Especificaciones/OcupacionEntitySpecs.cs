using System.Linq.Expressions;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Especificaciones;

/// <summary>
/// Predicados reutilizables sobre <see cref="OcupacionEntity"/>.
/// Centralizan la noción de "vigente" (no soft-deleted y no finalizada)
/// para que cualquier futuro cambio de la regla (por ejemplo, agregar
/// un bound de <c>FechaInicio</c>) se propague a todo el código de
/// persistencia que la consume. Replica a nivel de entidad la
/// semántica de <c>Ocupacion.EsVigente</c> del agregado de dominio,
/// evitando hidratar la Ocupacion cuando sólo se necesita el predicado.
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
}
