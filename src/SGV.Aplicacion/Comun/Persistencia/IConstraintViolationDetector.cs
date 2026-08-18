using Microsoft.EntityFrameworkCore;

namespace SGV.Aplicacion.Comun.Persistencia;

/// <summary>
/// Detects whether a <see cref="DbUpdateException"/> represents an expected
/// constraint violation (e.g. duplicate key, FK violation) as opposed to a
/// transient failure (deadlock, timeout, etc.).
/// </summary>
public interface IConstraintViolationDetector
{
    /// <summary>
    /// Returns <see langword="true"/> when the exception indicates an expected
    /// constraint violation that should surface as a 409 Conflict.
    /// Returns <see langword="false"/> for transient failures that should
    /// propagate as 500 Internal Server Error.
    /// </summary>
    bool IsConstraintViolation(DbUpdateException exception);

    /// <summary>
    /// Returns the unique-index name when the exception indicates a
    /// duplicate-key violation (MySQL/MariaDB error 1062), or
    /// <see langword="null"/> otherwise. Permite a los servicios mapear
    /// violaciones a códigos de error específicos por constraint.
    /// </summary>
    /// <remarks>
    /// Cambio <c>vacantes-hardening</c> D-4: necesario para distinguir
    /// <c>IX_Ocupaciones_VacanteIdUnique</c> (race de doble cobertura)
    /// de otras unique-violations no relacionadas.
    /// </remarks>
    string? GetUniqueConstraintName(DbUpdateException exception);
}

