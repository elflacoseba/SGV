namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Base de las entidades de persistencia de Infraestructura.
/// Solo contiene el identificador, análogo a <c>EntidadBase</c> del Dominio.
/// </summary>
public abstract class EntityBase
{
    public Guid Id { get; set; }
}
