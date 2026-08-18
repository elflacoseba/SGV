using System.Text.RegularExpressions;
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
        ///   <item><description>1644: SIGNAL raised by trigger (issue #277 — trigger anti-ciclos)</description></item>
        ///   <item><description>4025: Constraint violation</description></item>
        /// </list>
        /// </summary>
        public bool IsConstraintViolation(DbUpdateException exception)
        {
            return exception.InnerException is MySqlException mysqlEx &&
                   mysqlEx.Number is 1062 or 1169 or 1451 or 1452 or 1644 or 4025;
        }

        /// <summary>
        /// Para una duplicate-key (1062), devuelve el nombre del índice
        /// violado. Mensaje típico de MySQL/MariaDB:
        /// <c>Duplicate entry 'X' for key 'Ocupaciones.IX_Ocupaciones_VacanteIdUnique'</c>.
        /// </summary>
        public string? GetUniqueConstraintName(DbUpdateException exception)
        {
            if (exception.InnerException is not MySqlException mysqlEx || mysqlEx.Number != 1062)
            {
                return null;
            }

            // Capturamos el nombre del índice entre comillas simples o
            // backticks. MySQL 8 usa backticks; MariaDB usa comillas.
            var match = UniqueKeyRegex.Match(mysqlEx.Message);
            if (!match.Success) return null;
            return match.Groups["n1"].Success
                ? match.Groups["n1"].Value
                : match.Groups["n2"].Value;
        }

        private static readonly Regex UniqueKeyRegex = new(
            @"for key (?:'(?<n1>[^']+)'|`(?<n2>[^`]+)`)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }
