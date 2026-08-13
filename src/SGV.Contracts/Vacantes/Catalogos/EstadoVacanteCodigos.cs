namespace SGV.Contracts.Vacantes.Catalogos;

/// <summary>
/// Códigos canónicos del catálogo inmutable <c>EstadoVacante</c>
/// (issue #273, §273.2: residir en <c>Contracts</c> para que la capa de
/// Aplicación pueda resolver el estado "Abierta" sin acoplarse a un
/// <c>Guid</c> hardcoded de <c>Infraestructura</c>).
/// <para>
/// Los IDs correspondientes viven en
/// <c>SGV.Infraestructura.Persistencia.Catalogos.EstadoVacanteConstantes</c>
/// como single source of truth del seed. Esta clase expone sólo los
/// códigos (string) que la capa de Aplicación necesita para resolver
/// la regla "vacante nueva = Abierta" del catálogo en runtime.
/// </para>
/// </summary>
public static class EstadoVacanteCodigos
{
    /// <summary>Código del estado inicial de una vacante recién creada.</summary>
    public const string Abierta = "Abierta";

    /// <summary>Código del estado "En Selección" (post-abierto, postulante asignado).</summary>
    public const string EnSeleccion = "EnSeleccion";

    /// <summary>Código del estado terminal "Cubierta" (vacante con una Ocupación asociada).</summary>
    public const string Cubierta = "Cubierta";

    /// <summary>Código del estado terminal "Cancelada" (vacante cerrada sin cobertura).</summary>
    public const string Cancelada = "Cancelada";
}
