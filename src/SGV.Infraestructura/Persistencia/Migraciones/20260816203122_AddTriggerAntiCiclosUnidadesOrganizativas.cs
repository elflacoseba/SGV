using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <summary>
    /// Issue #277 — defensa en profundidad a nivel de base de datos:
    /// crea dos triggers BEFORE INSERT y BEFORE UPDATE sobre
    /// <c>UnidadesOrganizativas</c> que rechazan cualquier cambio que forme
    /// un ciclo transitivo en la jerarquía activa. La capa de aplicación
    /// traduce la violación a <c>409 CicloJerarquico</c> vía
    /// <c>MySqlConstraintViolationDetector</c> (MySQL error code 1644).
    ///
    /// La detección usa una CTE recursiva que recorre la cadena de padres
    /// partiendo del candidato a nuevo padre (<c>NEW.UnidadPadreId</c>) y
    /// recorre hacia arriba hasta <c>depth &lt; 32</c>; si algún id
    /// coincide con <c>NEW.Id</c>, hay un ciclo y se emite
    /// <c>SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CicloJerarquico'</c>.
    /// MySQL 8.0+ soporta CTEs recursivas dentro de triggers.
    /// </summary>
    public partial class AddTriggerAntiCiclosUnidadesOrganizativas : Migration
    {
        private const string TriggerMensajeCiclo = "CicloJerarquico";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BEFORE INSERT: la fila es nueva; NEW.Id ya está asignado por
            // el dominio (Guid.NewGuid()) antes de SaveChanges.
            migrationBuilder.Sql(@$"
CREATE TRIGGER trg_UnidadesOrganizativas_BeforeInsert_Ciclo
BEFORE INSERT ON UnidadesOrganizativas
FOR EACH ROW
BEGIN
  IF NEW.UnidadPadreId IS NOT NULL THEN
    SET @sgv_ciclo_count := 0;
    WITH RECURSIVE padre_chain (id, depth) AS (
      SELECT NEW.UnidadPadreId, 0
      UNION ALL
      SELECT u.UnidadPadreId, p.depth + 1
      FROM UnidadesOrganizativas u
      INNER JOIN padre_chain p ON u.Id = p.id
      WHERE u.IsDeleted = 0 AND p.depth < 32
    )
    SELECT COUNT(*) INTO @sgv_ciclo_count FROM padre_chain WHERE id = NEW.Id;
    IF @sgv_ciclo_count > 0 THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '{TriggerMensajeCiclo}';
    END IF;
  END IF;
END
");

            // BEFORE UPDATE: validar que el cambio de UnidadPadreId no
            // introduzca un ciclo. Si NEW.UnidadPadreId == OLD.UnidadPadreId
            // el CTE termina rapidamente sin señal.
            migrationBuilder.Sql(@$"
CREATE TRIGGER trg_UnidadesOrganizativas_BeforeUpdate_Ciclo
BEFORE UPDATE ON UnidadesOrganizativas
FOR EACH ROW
BEGIN
  IF NEW.UnidadPadreId IS NOT NULL THEN
    SET @sgv_ciclo_count := 0;
    WITH RECURSIVE padre_chain (id, depth) AS (
      SELECT NEW.UnidadPadreId, 0
      UNION ALL
      SELECT u.UnidadPadreId, p.depth + 1
      FROM UnidadesOrganizativas u
      INNER JOIN padre_chain p ON u.Id = p.id
      WHERE u.IsDeleted = 0 AND p.depth < 32
    )
    SELECT COUNT(*) INTO @sgv_ciclo_count FROM padre_chain WHERE id = NEW.Id;
    IF @sgv_ciclo_count > 0 THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '{TriggerMensajeCiclo}';
    END IF;
  END IF;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_UnidadesOrganizativas_BeforeInsert_Ciclo;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_UnidadesOrganizativas_BeforeUpdate_Ciclo;");
        }
    }
}
