using SGV.Dominio.Comun;

namespace SGV.Dominio.Seleccion;

public sealed class HistorialEstadoPostulacion : EntidadBase
{
    private HistorialEstadoPostulacion()
    {
    }

    public HistorialEstadoPostulacion(Guid postulacionId, Guid? estadoAnteriorId, Guid estadoNuevoId, DateTime changedAt, string? changedByUserId, string? observaciones)
    {
        PostulacionId = postulacionId;
        EstadoAnteriorId = estadoAnteriorId;
        EstadoNuevoId = estadoNuevoId;
        ChangedAt = changedAt;
        ChangedByUserId = ValidacionesDominio.Opcional(changedByUserId, nameof(ChangedByUserId), 450);
        Observaciones = ValidacionesDominio.Opcional(observaciones, nameof(Observaciones), 1000);
    }

    public Guid PostulacionId { get; private set; }

    public Postulacion Postulacion { get; private set; } = null!;

    public Guid? EstadoAnteriorId { get; private set; }

    public EstadoPostulacion? EstadoAnterior { get; private set; }

    public Guid EstadoNuevoId { get; private set; }

    public EstadoPostulacion EstadoNuevo { get; private set; } = null!;

    public DateTime ChangedAt { get; private set; }

    public string? ChangedByUserId { get; private set; }

    public string? Observaciones { get; private set; }
}
