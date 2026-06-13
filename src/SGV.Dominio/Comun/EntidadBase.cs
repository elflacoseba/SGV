namespace SGV.Dominio.Comun;

public abstract class EntidadBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
