using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    /// <summary>
    /// Issue #273 (Slice B): el catálogo <c>EstadosVacante</c> tiene la fila
    /// <c>Codigo='EnSeleccion'</c> con un <c>Nombre</c> mal codificado
    /// (mojibake clásico UTF-8 → Latin-1: "En SelecciÃ³n" en lugar de
    /// "En Selección"). El seed en <c>DatosSemilla.cs</c> ya emite UTF-8
    /// correcto, así que el problema es de filas pre-existentes en bases
    /// con encoding heredado o inserts manuales con charset mal
    /// negociado.
    ///
    /// Esta migración aplica un <c>UPDATE</c> idempotente: sólo afecta
    /// filas que aún muestran el mojibake <c>Ã³</c>. Filas con encoding
    /// correcto ("En Selección" sin "Ã³") no se tocan, lo que permite
    /// correr la migración múltiples veces sin efecto colateral.
    ///
    /// La detección usa <c>LIKE '%Ã³%'</c> como firma canónica del
    /// mojibake: la 'ó' UTF-8 son los bytes <c>0xC3 0xB3</c>, que Latin-1
    /// renderiza como "Ã³". Si la columna está en UTF-8 correcto (lo que
    /// ocurre tras <c>Migrate</c> desde un cliente bien configurado),
    /// los bytes <c>0xC3 0xB3</c> se interpretan como "ó" directamente y
    /// el LIKE no matchea.
    ///
    /// <c>Down()</c> queda vacío: no se puede revertir la limpieza de
    /// datos sin reintroducir el bug. La columna se considera
    /// forward-only.
    /// </summary>
    public partial class FixEstadoVacanteEnSeleccionEncoding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- Issue #273 (Slice B): reparar el mojibake "Ã³" → "ó" en la fila
                -- Codigo='EnSeleccion' del catálogo EstadosVacante. Idempotente:
                -- sólo afecta filas que aún muestran el byte mal codificado.
                UPDATE `EstadosVacante`
                SET `Nombre` = 'En Selección'
                WHERE `Codigo` = 'EnSeleccion'
                  AND `Nombre` LIKE '%Ã³%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only: no se revierte la limpieza de datos.
        }
    }
}
