using SGV.Aplicacion.Comun.Persistencia;
using SGV.Dominio.Habilidades;

namespace SGV.Aplicacion.Habilidades.Consultas;

/// <summary>
/// Read-only repository contract for the NivelHabilidad catalog.
/// Only query operations; levels are managed via seed data / migrations.
/// </summary>
public interface INivelHabilidadRepository : IReadOnlyRepository<NivelHabilidad>
{
}
