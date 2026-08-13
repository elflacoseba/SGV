using System.Linq.Expressions;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Especificaciones;

/// <summary>
/// Predicados reutilizables sobre <see cref="VacanteEntity"/>.
/// Centralizan la noción de "abierta" (no soft-deleted y no cerrada)
/// para que cualquier futuro cambio de la regla (por ejemplo, agregar
/// un filtro por <c>EstadoVacanteId</c>) se propague a todo el código
/// de persistencia que la consume. Réplica semántica del concepto de
/// "vacante abierta" usado en <c>VacanteServicioComandos.CrearAsync</c>
/// (regla N4) y en <c>PuestoRepository.ListarDisponiblesAsync</c> (REQ-PTO-DISP-001).
/// </summary>
public static class VacanteEntitySpecs
{
    /// <summary>
    /// True cuando la Vacante está activa y no fue cerrada ni borrada
    /// lógicamente: <c>!IsDeleted AND FechaCierre IS NULL</c>. Compone
    /// con <c>IQueryable&lt;VacanteEntity&gt;.Where(...)</c> y con
    /// <c>.Any(...)</c> / <c>.AnyAsync(...)</c> / <c>.FirstOrDefaultAsync(...)</c>.
    /// </summary>
    public static readonly Expression<Func<VacanteEntity, bool>> EsAbierta =
        v => !v.IsDeleted && v.FechaCierre == null;
}
