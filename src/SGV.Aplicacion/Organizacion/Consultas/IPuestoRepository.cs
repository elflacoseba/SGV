using SGV.Aplicacion.Comun.Persistencia;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Consultas;

/// <summary>
/// Read-only repository for Puesto queries. Includes related entity support.
/// </summary>
public interface IPuestoRepository : IReadOnlyRepository<Puesto>
{
}
