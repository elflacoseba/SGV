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
}
