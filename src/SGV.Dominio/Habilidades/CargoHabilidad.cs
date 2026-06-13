using SGV.Dominio.Comun;
using SGV.Dominio.Organizacion;

namespace SGV.Dominio.Habilidades;

public sealed class CargoHabilidad : EntidadBase
{
    private CargoHabilidad()
    {
    }

    public CargoHabilidad(Guid cargoId, Guid habilidadId, Guid nivelRequeridoId, decimal ponderacion, bool esObligatoria)
    {
        if (ponderacion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ponderacion), "La ponderación debe ser mayor a cero.");
        }

        CargoId = cargoId;
        HabilidadId = habilidadId;
        NivelRequeridoId = nivelRequeridoId;
        Ponderacion = ponderacion;
        EsObligatoria = esObligatoria;
    }

    public Guid CargoId { get; private set; }

    public Cargo Cargo { get; private set; } = null!;

    public Guid HabilidadId { get; private set; }

    public Habilidad Habilidad { get; private set; } = null!;

    public Guid NivelRequeridoId { get; private set; }

    public NivelHabilidad NivelRequerido { get; private set; } = null!;

    public decimal Ponderacion { get; private set; }

    public bool EsObligatoria { get; private set; }
}
