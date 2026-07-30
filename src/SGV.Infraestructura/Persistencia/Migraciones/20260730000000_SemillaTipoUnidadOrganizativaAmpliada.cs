using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SGV.Infraestructura.Persistencia.Catalogos;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    public partial class SemillaTipoUnidadOrganizativaAmpliada : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "TiposUnidadOrganizativa",
                columns: new[] { "Id", "Codigo", "Nombre" },
                values: new object[,]
                {
                    { TipoUnidadOrganizativaConstantes.SedeId,            "Sede",            "Sede"            },
                    { TipoUnidadOrganizativaConstantes.RegionId,           "Region",          "Región"          },
                    { TipoUnidadOrganizativaConstantes.GerenciaId,         "Gerencia",        "Gerencia"        },
                    { TipoUnidadOrganizativaConstantes.VicepresidenciaId,  "Vicepresidencia", "Vicepresidencia" },
                    { TipoUnidadOrganizativaConstantes.SubgerenciaId,      "Subgerencia",     "Subgerencia"     },
                    { TipoUnidadOrganizativaConstantes.CoordinacionId,     "Coordinacion",    "Coordinación"    },
                    { TipoUnidadOrganizativaConstantes.SeccionId,          "Seccion",         "Sección"         },
                    { TipoUnidadOrganizativaConstantes.OficinaId,          "Oficina",         "Oficina"         },
                    { TipoUnidadOrganizativaConstantes.EquipoId,           "Equipo",          "Equipo"          },
                    { TipoUnidadOrganizativaConstantes.CelulaId,           "Celula",          "Célula"          },
                    { TipoUnidadOrganizativaConstantes.PlantaId,           "Planta",          "Planta"          },
                    { TipoUnidadOrganizativaConstantes.SucursalId,         "Sucursal",        "Sucursal"        },
                    { TipoUnidadOrganizativaConstantes.EscuelaId,          "Escuela",         "Escuela"         }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("This migration is forward-only. The TipoUnidadOrganizativa catalog is append-only.");
        }
    }
}
