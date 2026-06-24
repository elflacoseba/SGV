using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SGV.Aplicacion.Comun.Persistencia;

namespace SGV.Infraestructura.Persistencia;

/// <summary>
/// MySQL/MariaDB implementation of <see cref="IConstraintViolationDetector"/>.
/// Inspects the inner <see cref="MySqlException"/> for known constraint-violation
/// error codes.
/// </summary>
public sealed class MySqlConstraintViolationDetector : IConstraintViolationDetector
{
    /// <summary>
    /// MySQL/MariaDB error codes for expected constraint violations:
    /// <list type="bullet">
    ///   <item><description>1062: Duplicate entry (unique constraint)</description></item>
    ///   <item><description>1169: Cannot delete or update a parent row (FK constraint)</description></item>
    ///   <item><description>1451: Cannot delete or update a parent row (FK constraint)</description></item>
    ///   <item><description>1452: Cannot add or update a child row (FK constraint)</description></item>
    ///   <item><description>4025: Constraint violation</description></item>
    /// </list>
    /// </summary>
    public bool IsConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is MySqlException mysqlEx &&
               mysqlEx.Number is 1062 or 1169 or 1451 or 1452 or 4025;
    }
}
