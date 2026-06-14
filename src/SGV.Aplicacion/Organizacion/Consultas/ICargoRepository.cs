using SGV.Aplicacion.Comun.Persistencia;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Consultas;

/// <summary>
/// Read-only repository for Cargo queries.
/// </summary>
public interface ICargoRepository : IReadOnlyRepository<Cargo>
{
}
