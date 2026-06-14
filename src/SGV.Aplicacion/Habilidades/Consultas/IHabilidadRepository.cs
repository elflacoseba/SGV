using SGV.Aplicacion.Comun.Persistencia;
using SGV.Dominio.Habilidades;

namespace SGV.Aplicacion.Habilidades.Consultas;

/// <summary>
/// Read-only repository for Habilidad queries.
/// </summary>
public interface IHabilidadRepository : IReadOnlyRepository<Habilidad>
{
}
