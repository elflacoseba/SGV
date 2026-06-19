namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Request para crear un nuevo Puesto.
/// </summary>
public sealed record CrearPuestoRequest(
    string Codigo,
    string Nombre,
    Guid UnidadOrganizativaId,
    Guid CargoId,
    Guid? PuestoSuperiorId = null,
    string? Descripcion = null
);

/// <summary>
/// Request para actualizar los campos editables de un Puesto existente.
/// Codigo NO está incluido — es inmutable tras la creación.
/// </summary>
public sealed record ActualizarPuestoRequest(
    string Nombre,
    string? Descripcion = null,
    Guid? PuestoSuperiorId = null
);
