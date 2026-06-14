using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class InicialSgvo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedUserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmailConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PasswordHash = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SecurityStamp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhoneNumber = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhoneNumberConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Auditorias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OccurredAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EntityName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Operation = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OldValuesJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NewValuesJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangedPropertiesJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CorrelationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditorias", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Cargos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Codigo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nombre = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Descripcion = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nivel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ActiveCodigoUnique = table.Column<string>(type: "varchar(255)", nullable: true, computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cargos", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EstadosPostulacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Codigo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    EsTerminal = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EsTerminalPositivo = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosPostulacion", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EstadosVacante",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Codigo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    EsTerminal = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosVacante", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Habilidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Codigo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nombre = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Descripcion = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Categoria = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ActiveCodigoUnique = table.Column<string>(type: "varchar(255)", nullable: true, computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Habilidades", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NivelesHabilidad",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Codigo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ValorNumerico = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NivelesHabilidad", x => x.Id);
                    table.CheckConstraint("CK_NivelesHabilidad_ValorNumerico", "`ValorNumerico` BETWEEN 1 AND 4");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Personas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Legajo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nombres = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Apellidos = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TipoDocumento = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NumeroDocumento = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Telefono = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ActiveDocumentoUnique = table.Column<string>(type: "varchar(255)", nullable: true, computedColumnSql: "CASE WHEN `TipoDocumento` IS NOT NULL AND `NumeroDocumento` IS NOT NULL AND `IsDeleted` = 0 THEN CONCAT(`TipoDocumento`, ':', `NumeroDocumento`) ELSE NULL END")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActiveEmailUnique = table.Column<string>(type: "varchar(255)", nullable: true, computedColumnSql: "CASE WHEN `Email` IS NOT NULL AND `IsDeleted` = 0 THEN `Email` ELSE NULL END")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActiveLegajoUnique = table.Column<string>(type: "varchar(255)", nullable: true, computedColumnSql: "CASE WHEN `Legajo` IS NOT NULL AND `IsDeleted` = 0 THEN `Legajo` ELSE NULL END")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personas", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UnidadesOrganizativas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UnidadPadreId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Codigo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nombre = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TipoUnidad = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Descripcion = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VigenteDesde = table.Column<DateOnly>(type: "date", nullable: true),
                    VigenteHasta = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ActiveCodigoUnique = table.Column<string>(type: "varchar(255)", nullable: true, computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnidadesOrganizativas", x => x.Id);
                    table.CheckConstraint("CK_UnidadesOrganizativas_UnidadPadre", "`UnidadPadreId` IS NULL OR `UnidadPadreId` <> `Id`");
                    table.ForeignKey(
                        name: "FK_UnidadesOrganizativas_UnidadesOrganizativas_UnidadPadreId",
                        column: x => x.UnidadPadreId,
                        principalTable: "UnidadesOrganizativas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RoleId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClaimType = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClaimValue = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClaimType = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClaimValue = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderKey = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderDisplayName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RoleId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LoginProvider = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Value = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CargoHabilidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CargoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    HabilidadId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NivelRequeridoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Ponderacion = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    EsObligatoria = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargoHabilidades", x => x.Id);
                    table.CheckConstraint("CK_CargoHabilidades_Ponderacion", "`Ponderacion` > 0");
                    table.ForeignKey(
                        name: "FK_CargoHabilidades_Cargos_CargoId",
                        column: x => x.CargoId,
                        principalTable: "Cargos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CargoHabilidades_Habilidades_HabilidadId",
                        column: x => x.HabilidadId,
                        principalTable: "Habilidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CargoHabilidades_NivelesHabilidad_NivelRequeridoId",
                        column: x => x.NivelRequeridoId,
                        principalTable: "NivelesHabilidad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PersonaHabilidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PersonaId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    HabilidadId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NivelHabilidadId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    VerificadoAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Fuente = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonaHabilidades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonaHabilidades_Habilidades_HabilidadId",
                        column: x => x.HabilidadId,
                        principalTable: "Habilidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonaHabilidades_NivelesHabilidad_NivelHabilidadId",
                        column: x => x.NivelHabilidadId,
                        principalTable: "NivelesHabilidad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonaHabilidades_Personas_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Personas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Postulantes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PersonaId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Nombres = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Apellidos = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Telefono = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Fuente = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Observaciones = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActivePersonaIdUnique = table.Column<Guid>(type: "char(36)", nullable: true, computedColumnSql: "CASE WHEN `PersonaId` IS NOT NULL AND `IsDeleted` = 0 THEN `PersonaId` ELSE NULL END", collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Postulantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Postulantes_Personas_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Personas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Puestos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UnidadOrganizativaId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CargoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PuestoSuperiorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Codigo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nombre = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Descripcion = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ActiveCodigoUnique = table.Column<string>(type: "varchar(255)", nullable: true, computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Puestos", x => x.Id);
                    table.CheckConstraint("CK_Puestos_PuestoSuperior", "`PuestoSuperiorId` IS NULL OR `PuestoSuperiorId` <> `Id`");
                    table.ForeignKey(
                        name: "FK_Puestos_Cargos_CargoId",
                        column: x => x.CargoId,
                        principalTable: "Cargos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Puestos_Puestos_PuestoSuperiorId",
                        column: x => x.PuestoSuperiorId,
                        principalTable: "Puestos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Puestos_UnidadesOrganizativas_UnidadOrganizativaId",
                        column: x => x.UnidadOrganizativaId,
                        principalTable: "UnidadesOrganizativas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Ocupaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PersonaId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PuestoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: true),
                    TipoAsignacion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Observaciones = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActivePersonaIdUnique = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PersonaId` ELSE NULL END"),
                    ActivePuestoIdUnique = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ocupaciones", x => x.Id);
                    table.CheckConstraint("CK_Ocupaciones_Fechas", "`FechaFin` IS NULL OR `FechaFin` >= `FechaInicio`");
                    table.ForeignKey(
                        name: "FK_Ocupaciones_Personas_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Personas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ocupaciones_Puestos_PuestoId",
                        column: x => x.PuestoId,
                        principalTable: "Puestos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Vacantes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PuestoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EstadoVacanteId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FechaApertura = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FechaCierre = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Motivo = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Observaciones = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vacantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vacantes_EstadosVacante_EstadoVacanteId",
                        column: x => x.EstadoVacanteId,
                        principalTable: "EstadosVacante",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vacantes_Puestos_PuestoId",
                        column: x => x.PuestoId,
                        principalTable: "Puestos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HistorialEstadosVacante",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    VacanteId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EstadoAnteriorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    EstadoNuevoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ChangedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Motivo = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialEstadosVacante", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosVacante_EstadosVacante_EstadoAnteriorId",
                        column: x => x.EstadoAnteriorId,
                        principalTable: "EstadosVacante",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosVacante_EstadosVacante_EstadoNuevoId",
                        column: x => x.EstadoNuevoId,
                        principalTable: "EstadosVacante",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosVacante_Vacantes_VacanteId",
                        column: x => x.VacanteId,
                        principalTable: "Vacantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Postulaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    VacanteId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PostulanteId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EstadoPostulacionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FechaPostulacion = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PuntajeCompatibilidad = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    NivelCompatibilidad = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Observaciones = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Postulaciones", x => x.Id);
                    table.CheckConstraint("CK_Postulaciones_PuntajeCompatibilidad", "`PuntajeCompatibilidad` IS NULL OR (`PuntajeCompatibilidad` >= 0 AND `PuntajeCompatibilidad` <= 100)");
                    table.ForeignKey(
                        name: "FK_Postulaciones_EstadosPostulacion_EstadoPostulacionId",
                        column: x => x.EstadoPostulacionId,
                        principalTable: "EstadosPostulacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Postulaciones_Postulantes_PostulanteId",
                        column: x => x.PostulanteId,
                        principalTable: "Postulantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Postulaciones_Vacantes_VacanteId",
                        column: x => x.VacanteId,
                        principalTable: "Vacantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EvaluacionesPostulacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PostulacionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EvaluadoAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EvaluadoByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PuntajeTecnico = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    PuntajeEntrevista = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    PuntajeCompatibilidad = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Recomendacion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Observaciones = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluacionesPostulacion", x => x.Id);
                    table.CheckConstraint("CK_EvaluacionesPostulacion_PuntajeCompatibilidad", "`PuntajeCompatibilidad` IS NULL OR (`PuntajeCompatibilidad` >= 0 AND `PuntajeCompatibilidad` <= 100)");
                    table.CheckConstraint("CK_EvaluacionesPostulacion_PuntajeEntrevista", "`PuntajeEntrevista` IS NULL OR (`PuntajeEntrevista` >= 0 AND `PuntajeEntrevista` <= 100)");
                    table.CheckConstraint("CK_EvaluacionesPostulacion_PuntajeTecnico", "`PuntajeTecnico` IS NULL OR (`PuntajeTecnico` >= 0 AND `PuntajeTecnico` <= 100)");
                    table.ForeignKey(
                        name: "FK_EvaluacionesPostulacion_Postulaciones_PostulacionId",
                        column: x => x.PostulacionId,
                        principalTable: "Postulaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HistorialEstadosPostulacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PostulacionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EstadoAnteriorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    EstadoNuevoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ChangedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Observaciones = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialEstadosPostulacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosPostulacion_EstadosPostulacion_EstadoAnterio~",
                        column: x => x.EstadoAnteriorId,
                        principalTable: "EstadosPostulacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosPostulacion_EstadosPostulacion_EstadoNuevoId",
                        column: x => x.EstadoNuevoId,
                        principalTable: "EstadosPostulacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosPostulacion_Postulaciones_PostulacionId",
                        column: x => x.PostulacionId,
                        principalTable: "Postulaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "Administrador", "rol-Administrador", "Administrador", "ADMINISTRADOR" },
                    { "EvaluadorSeleccion", "rol-EvaluadorSeleccion", "EvaluadorSeleccion", "EVALUADORSELECCION" },
                    { "GestorOrganizacional", "rol-GestorOrganizacional", "GestorOrganizacional", "GESTORORGANIZACIONAL" },
                    { "Lector", "rol-Lector", "Lector", "LECTOR" },
                    { "RecursosHumanos", "rol-RecursosHumanos", "RecursosHumanos", "RECURSOSHUMANOS" }
                });

            migrationBuilder.InsertData(
                table: "Cargos",
                columns: new[] { "Id", "Codigo", "CreatedAt", "CreatedByUserId", "DeletedAt", "DeletedByUserId", "Descripcion", "IsActive", "IsDeleted", "Nivel", "Nombre", "UpdatedAt", "UpdatedByUserId" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000001"), "DECANO", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Directivo", "Decano", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000002"), "SECRETARIO", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Directivo", "Secretario", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000003"), "DIRECTOR", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Conducción media", "Director", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000004"), "JEFE_DEPARTAMENTO", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Conducción media", "Jefe de Departamento", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000005"), "ADMINISTRATIVO", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Operativo", "Administrativo", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000006"), "PROFESOR", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Académico", "Profesor", null, null }
                });

            migrationBuilder.InsertData(
                table: "EstadosPostulacion",
                columns: new[] { "Id", "Codigo", "EsTerminal", "EsTerminalPositivo", "Nombre", "Orden" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), "Postulado", false, false, "Postulado", 1 },
                    { new Guid("30000000-0000-0000-0000-000000000002"), "Preseleccionado", false, false, "Preseleccionado", 2 },
                    { new Guid("30000000-0000-0000-0000-000000000003"), "Entrevistado", false, false, "Entrevistado", 3 },
                    { new Guid("30000000-0000-0000-0000-000000000004"), "Aprobado", false, false, "Aprobado", 4 },
                    { new Guid("30000000-0000-0000-0000-000000000005"), "Rechazado", true, false, "Rechazado", 5 },
                    { new Guid("30000000-0000-0000-0000-000000000006"), "Contratado", true, true, "Contratado", 6 }
                });

            migrationBuilder.InsertData(
                table: "EstadosVacante",
                columns: new[] { "Id", "Codigo", "EsTerminal", "Nombre", "Orden" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "Abierta", false, "Abierta", 1 },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "EnSeleccion", false, "En Selección", 2 },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "Cubierta", true, "Cubierta", 3 },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "Cancelada", true, "Cancelada", 4 }
                });

            migrationBuilder.InsertData(
                table: "Habilidades",
                columns: new[] { "Id", "Categoria", "Codigo", "CreatedAt", "CreatedByUserId", "DeletedAt", "DeletedByUserId", "Descripcion", "IsActive", "IsDeleted", "Nombre", "UpdatedAt", "UpdatedByUserId" },
                values: new object[,]
                {
                    { new Guid("50000000-0000-0000-0000-000000000001"), "Conducción", "LIDERAZGO", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Liderazgo", null, null },
                    { new Guid("50000000-0000-0000-0000-000000000002"), "Conducción", "GESTION_PERSONAL", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Gestión de Personal", null, null },
                    { new Guid("50000000-0000-0000-0000-000000000003"), "Técnica", "SQL_SERVER", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "SQL Server", null, null },
                    { new Guid("50000000-0000-0000-0000-000000000004"), "Técnica", "EF_CORE", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Entity Framework Core", null, null },
                    { new Guid("50000000-0000-0000-0000-000000000005"), "Técnica", "DOTNET", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Programación .NET", null, null },
                    { new Guid("50000000-0000-0000-0000-000000000006"), "Dominio", "ADMINISTRACION_PUBLICA", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Administración Pública", null, null },
                    { new Guid("50000000-0000-0000-0000-000000000007"), "Académica", "DOCENCIA_UNIVERSITARIA", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Docencia Universitaria", null, null }
                });

            migrationBuilder.InsertData(
                table: "NivelesHabilidad",
                columns: new[] { "Id", "Codigo", "Nombre", "Orden", "ValorNumerico" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "Basico", "Básico", 1, (byte)1 },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "Intermedio", "Intermedio", 2, (byte)2 },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "Avanzado", "Avanzado", 3, (byte)3 },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "Experto", "Experto", 4, (byte)4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_CorrelationId",
                table: "Auditorias",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_EntityName_EntityId_OccurredAt",
                table: "Auditorias",
                columns: new[] { "EntityName", "EntityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_UserId_OccurredAt",
                table: "Auditorias",
                columns: new[] { "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CargoHabilidades_CargoId_HabilidadId",
                table: "CargoHabilidades",
                columns: new[] { "CargoId", "HabilidadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CargoHabilidades_HabilidadId",
                table: "CargoHabilidades",
                column: "HabilidadId");

            migrationBuilder.CreateIndex(
                name: "IX_CargoHabilidades_NivelRequeridoId",
                table: "CargoHabilidades",
                column: "NivelRequeridoId");

            migrationBuilder.CreateIndex(
                name: "IX_Cargos_ActiveCodigoUnique",
                table: "Cargos",
                column: "ActiveCodigoUnique",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cargos_IsDeleted",
                table: "Cargos",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Cargos_Nombre",
                table: "Cargos",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_EstadosPostulacion_Codigo",
                table: "EstadosPostulacion",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EstadosVacante_Codigo",
                table: "EstadosVacante",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluacionesPostulacion_EvaluadoAt",
                table: "EvaluacionesPostulacion",
                column: "EvaluadoAt");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluacionesPostulacion_IsDeleted",
                table: "EvaluacionesPostulacion",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluacionesPostulacion_PostulacionId",
                table: "EvaluacionesPostulacion",
                column: "PostulacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Habilidades_ActiveCodigoUnique",
                table: "Habilidades",
                column: "ActiveCodigoUnique",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Habilidades_Categoria",
                table: "Habilidades",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_Habilidades_IsDeleted",
                table: "Habilidades",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosPostulacion_EstadoAnteriorId",
                table: "HistorialEstadosPostulacion",
                column: "EstadoAnteriorId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosPostulacion_EstadoNuevoId",
                table: "HistorialEstadosPostulacion",
                column: "EstadoNuevoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosPostulacion_PostulacionId_ChangedAt",
                table: "HistorialEstadosPostulacion",
                columns: new[] { "PostulacionId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosVacante_EstadoAnteriorId",
                table: "HistorialEstadosVacante",
                column: "EstadoAnteriorId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosVacante_EstadoNuevoId",
                table: "HistorialEstadosVacante",
                column: "EstadoNuevoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosVacante_VacanteId_ChangedAt",
                table: "HistorialEstadosVacante",
                columns: new[] { "VacanteId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NivelesHabilidad_Codigo",
                table: "NivelesHabilidad",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NivelesHabilidad_ValorNumerico",
                table: "NivelesHabilidad",
                column: "ValorNumerico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ocupaciones_ActivePersonaIdUnique",
                table: "Ocupaciones",
                column: "ActivePersonaIdUnique",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ocupaciones_ActivePuestoIdUnique",
                table: "Ocupaciones",
                column: "ActivePuestoIdUnique",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ocupaciones_IsDeleted",
                table: "Ocupaciones",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Ocupaciones_PersonaId_FechaInicio_FechaFin",
                table: "Ocupaciones",
                columns: new[] { "PersonaId", "FechaInicio", "FechaFin" });

            migrationBuilder.CreateIndex(
                name: "IX_Ocupaciones_PuestoId_FechaInicio_FechaFin",
                table: "Ocupaciones",
                columns: new[] { "PuestoId", "FechaInicio", "FechaFin" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonaHabilidades_HabilidadId",
                table: "PersonaHabilidades",
                column: "HabilidadId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonaHabilidades_NivelHabilidadId",
                table: "PersonaHabilidades",
                column: "NivelHabilidadId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonaHabilidades_PersonaId_HabilidadId",
                table: "PersonaHabilidades",
                columns: new[] { "PersonaId", "HabilidadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Personas_ActiveDocumentoUnique",
                table: "Personas",
                column: "ActiveDocumentoUnique",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Personas_ActiveEmailUnique",
                table: "Personas",
                column: "ActiveEmailUnique",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Personas_ActiveLegajoUnique",
                table: "Personas",
                column: "ActiveLegajoUnique",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Personas_Apellidos_Nombres",
                table: "Personas",
                columns: new[] { "Apellidos", "Nombres" });

            migrationBuilder.CreateIndex(
                name: "IX_Personas_IsDeleted",
                table: "Personas",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Postulaciones_EstadoPostulacionId",
                table: "Postulaciones",
                column: "EstadoPostulacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Postulaciones_IsDeleted",
                table: "Postulaciones",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Postulaciones_PostulanteId",
                table: "Postulaciones",
                column: "PostulanteId");

            migrationBuilder.CreateIndex(
                name: "IX_Postulaciones_VacanteId_EstadoPostulacionId",
                table: "Postulaciones",
                columns: new[] { "VacanteId", "EstadoPostulacionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Postulaciones_VacanteId_PostulanteId",
                table: "Postulaciones",
                columns: new[] { "VacanteId", "PostulanteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Postulantes_ActivePersonaIdUnique",
                table: "Postulantes",
                column: "ActivePersonaIdUnique",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Postulantes_Apellidos_Nombres",
                table: "Postulantes",
                columns: new[] { "Apellidos", "Nombres" });

            migrationBuilder.CreateIndex(
                name: "IX_Postulantes_Email",
                table: "Postulantes",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Postulantes_IsDeleted",
                table: "Postulantes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Postulantes_PersonaId",
                table: "Postulantes",
                column: "PersonaId");

            migrationBuilder.CreateIndex(
                name: "IX_Puestos_ActiveCodigoUnique",
                table: "Puestos",
                column: "ActiveCodigoUnique",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Puestos_CargoId",
                table: "Puestos",
                column: "CargoId");

            migrationBuilder.CreateIndex(
                name: "IX_Puestos_IsDeleted",
                table: "Puestos",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Puestos_PuestoSuperiorId",
                table: "Puestos",
                column: "PuestoSuperiorId");

            migrationBuilder.CreateIndex(
                name: "IX_Puestos_UnidadOrganizativaId",
                table: "Puestos",
                column: "UnidadOrganizativaId");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesOrganizativas_ActiveCodigoUnique",
                table: "UnidadesOrganizativas",
                column: "ActiveCodigoUnique",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesOrganizativas_IsDeleted",
                table: "UnidadesOrganizativas",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesOrganizativas_Nombre",
                table: "UnidadesOrganizativas",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesOrganizativas_UnidadPadreId",
                table: "UnidadesOrganizativas",
                column: "UnidadPadreId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacantes_EstadoVacanteId",
                table: "Vacantes",
                column: "EstadoVacanteId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacantes_EstadoVacanteId_FechaApertura",
                table: "Vacantes",
                columns: new[] { "EstadoVacanteId", "FechaApertura" });

            migrationBuilder.CreateIndex(
                name: "IX_Vacantes_FechaApertura",
                table: "Vacantes",
                column: "FechaApertura");

            migrationBuilder.CreateIndex(
                name: "IX_Vacantes_IsDeleted",
                table: "Vacantes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Vacantes_PuestoId",
                table: "Vacantes",
                column: "PuestoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Auditorias");

            migrationBuilder.DropTable(
                name: "CargoHabilidades");

            migrationBuilder.DropTable(
                name: "EvaluacionesPostulacion");

            migrationBuilder.DropTable(
                name: "HistorialEstadosPostulacion");

            migrationBuilder.DropTable(
                name: "HistorialEstadosVacante");

            migrationBuilder.DropTable(
                name: "Ocupaciones");

            migrationBuilder.DropTable(
                name: "PersonaHabilidades");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Postulaciones");

            migrationBuilder.DropTable(
                name: "Habilidades");

            migrationBuilder.DropTable(
                name: "NivelesHabilidad");

            migrationBuilder.DropTable(
                name: "EstadosPostulacion");

            migrationBuilder.DropTable(
                name: "Postulantes");

            migrationBuilder.DropTable(
                name: "Vacantes");

            migrationBuilder.DropTable(
                name: "Personas");

            migrationBuilder.DropTable(
                name: "EstadosVacante");

            migrationBuilder.DropTable(
                name: "Puestos");

            migrationBuilder.DropTable(
                name: "Cargos");

            migrationBuilder.DropTable(
                name: "UnidadesOrganizativas");
        }
    }
}
