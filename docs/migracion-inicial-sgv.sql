IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [Auditorias] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NULL,
        [OccurredAt] datetime2 NOT NULL,
        [EntityName] nvarchar(200) NOT NULL,
        [EntityId] nvarchar(100) NOT NULL,
        [Operation] nvarchar(50) NOT NULL,
        [OldValuesJson] nvarchar(max) NULL,
        [NewValuesJson] nvarchar(max) NULL,
        [ChangedPropertiesJson] nvarchar(max) NULL,
        [CorrelationId] uniqueidentifier NULL,
        CONSTRAINT [PK_Auditorias] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [Cargos] (
        [Id] uniqueidentifier NOT NULL,
        [Codigo] nvarchar(50) NOT NULL,
        [Nombre] nvarchar(200) NOT NULL,
        [Descripcion] nvarchar(1000) NULL,
        [Nivel] nvarchar(50) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedByUserId] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedByUserId] nvarchar(450) NULL,
        CONSTRAINT [PK_Cargos] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [EstadosPostulacion] (
        [Id] uniqueidentifier NOT NULL,
        [Codigo] nvarchar(50) NOT NULL,
        [Nombre] nvarchar(100) NOT NULL,
        [Orden] int NOT NULL,
        [EsTerminal] bit NOT NULL,
        [EsTerminalPositivo] bit NOT NULL,
        CONSTRAINT [PK_EstadosPostulacion] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [EstadosVacante] (
        [Id] uniqueidentifier NOT NULL,
        [Codigo] nvarchar(50) NOT NULL,
        [Nombre] nvarchar(100) NOT NULL,
        [Orden] int NOT NULL,
        [EsTerminal] bit NOT NULL,
        CONSTRAINT [PK_EstadosVacante] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [Habilidades] (
        [Id] uniqueidentifier NOT NULL,
        [Codigo] nvarchar(50) NOT NULL,
        [Nombre] nvarchar(200) NOT NULL,
        [Descripcion] nvarchar(1000) NULL,
        [Categoria] nvarchar(100) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedByUserId] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedByUserId] nvarchar(450) NULL,
        CONSTRAINT [PK_Habilidades] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [NivelesHabilidad] (
        [Id] uniqueidentifier NOT NULL,
        [Codigo] nvarchar(50) NOT NULL,
        [Nombre] nvarchar(100) NOT NULL,
        [ValorNumerico] tinyint NOT NULL,
        [Orden] int NOT NULL,
        CONSTRAINT [PK_NivelesHabilidad] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_NivelesHabilidad_ValorNumerico] CHECK ([ValorNumerico] BETWEEN 1 AND 4)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [Personas] (
        [Id] uniqueidentifier NOT NULL,
        [Legajo] nvarchar(50) NULL,
        [Nombres] nvarchar(100) NOT NULL,
        [Apellidos] nvarchar(100) NOT NULL,
        [Email] nvarchar(320) NULL,
        [TipoDocumento] nvarchar(50) NULL,
        [NumeroDocumento] nvarchar(50) NULL,
        [Telefono] nvarchar(50) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedByUserId] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedByUserId] nvarchar(450) NULL,
        CONSTRAINT [PK_Personas] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [UnidadesOrganizativas] (
        [Id] uniqueidentifier NOT NULL,
        [UnidadPadreId] uniqueidentifier NULL,
        [Codigo] nvarchar(50) NOT NULL,
        [Nombre] nvarchar(200) NOT NULL,
        [TipoUnidad] nvarchar(50) NOT NULL,
        [Descripcion] nvarchar(1000) NULL,
        [VigenteDesde] date NULL,
        [VigenteHasta] date NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedByUserId] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedByUserId] nvarchar(450) NULL,
        CONSTRAINT [PK_UnidadesOrganizativas] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_UnidadesOrganizativas_UnidadPadre] CHECK ([UnidadPadreId] IS NULL OR [UnidadPadreId] <> [Id]),
        CONSTRAINT [FK_UnidadesOrganizativas_UnidadesOrganizativas_UnidadPadreId] FOREIGN KEY ([UnidadPadreId]) REFERENCES [UnidadesOrganizativas] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [CargoHabilidades] (
        [Id] uniqueidentifier NOT NULL,
        [CargoId] uniqueidentifier NOT NULL,
        [HabilidadId] uniqueidentifier NOT NULL,
        [NivelRequeridoId] uniqueidentifier NOT NULL,
        [Ponderacion] decimal(5,2) NOT NULL,
        [EsObligatoria] bit NOT NULL,
        CONSTRAINT [PK_CargoHabilidades] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CargoHabilidades_Ponderacion] CHECK ([Ponderacion] > 0),
        CONSTRAINT [FK_CargoHabilidades_Cargos_CargoId] FOREIGN KEY ([CargoId]) REFERENCES [Cargos] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CargoHabilidades_Habilidades_HabilidadId] FOREIGN KEY ([HabilidadId]) REFERENCES [Habilidades] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CargoHabilidades_NivelesHabilidad_NivelRequeridoId] FOREIGN KEY ([NivelRequeridoId]) REFERENCES [NivelesHabilidad] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [PersonaHabilidades] (
        [Id] uniqueidentifier NOT NULL,
        [PersonaId] uniqueidentifier NOT NULL,
        [HabilidadId] uniqueidentifier NOT NULL,
        [NivelHabilidadId] uniqueidentifier NOT NULL,
        [VerificadoAt] datetime2 NULL,
        [Fuente] nvarchar(100) NULL,
        CONSTRAINT [PK_PersonaHabilidades] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PersonaHabilidades_Habilidades_HabilidadId] FOREIGN KEY ([HabilidadId]) REFERENCES [Habilidades] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PersonaHabilidades_NivelesHabilidad_NivelHabilidadId] FOREIGN KEY ([NivelHabilidadId]) REFERENCES [NivelesHabilidad] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PersonaHabilidades_Personas_PersonaId] FOREIGN KEY ([PersonaId]) REFERENCES [Personas] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [Postulantes] (
        [Id] uniqueidentifier NOT NULL,
        [PersonaId] uniqueidentifier NULL,
        [Nombres] nvarchar(100) NULL,
        [Apellidos] nvarchar(100) NULL,
        [Email] nvarchar(320) NULL,
        [Telefono] nvarchar(50) NULL,
        [Fuente] nvarchar(100) NULL,
        [Observaciones] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedByUserId] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedByUserId] nvarchar(450) NULL,
        CONSTRAINT [PK_Postulantes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Postulantes_Personas_PersonaId] FOREIGN KEY ([PersonaId]) REFERENCES [Personas] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [Puestos] (
        [Id] uniqueidentifier NOT NULL,
        [UnidadOrganizativaId] uniqueidentifier NOT NULL,
        [CargoId] uniqueidentifier NOT NULL,
        [PuestoSuperiorId] uniqueidentifier NULL,
        [Codigo] nvarchar(50) NOT NULL,
        [Nombre] nvarchar(200) NOT NULL,
        [Descripcion] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedByUserId] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedByUserId] nvarchar(450) NULL,
        CONSTRAINT [PK_Puestos] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Puestos_PuestoSuperior] CHECK ([PuestoSuperiorId] IS NULL OR [PuestoSuperiorId] <> [Id]),
        CONSTRAINT [FK_Puestos_Cargos_CargoId] FOREIGN KEY ([CargoId]) REFERENCES [Cargos] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Puestos_Puestos_PuestoSuperiorId] FOREIGN KEY ([PuestoSuperiorId]) REFERENCES [Puestos] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Puestos_UnidadesOrganizativas_UnidadOrganizativaId] FOREIGN KEY ([UnidadOrganizativaId]) REFERENCES [UnidadesOrganizativas] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [Ocupaciones] (
        [Id] uniqueidentifier NOT NULL,
        [PersonaId] uniqueidentifier NOT NULL,
        [PuestoId] uniqueidentifier NOT NULL,
        [FechaInicio] date NOT NULL,
        [FechaFin] date NULL,
        [TipoAsignacion] nvarchar(50) NOT NULL,
        [Observaciones] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedByUserId] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedByUserId] nvarchar(450) NULL,
        CONSTRAINT [PK_Ocupaciones] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Ocupaciones_Fechas] CHECK ([FechaFin] IS NULL OR [FechaFin] >= [FechaInicio]),
        CONSTRAINT [FK_Ocupaciones_Personas_PersonaId] FOREIGN KEY ([PersonaId]) REFERENCES [Personas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Ocupaciones_Puestos_PuestoId] FOREIGN KEY ([PuestoId]) REFERENCES [Puestos] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [Vacantes] (
        [Id] uniqueidentifier NOT NULL,
        [PuestoId] uniqueidentifier NOT NULL,
        [EstadoVacanteId] uniqueidentifier NOT NULL,
        [FechaApertura] datetime2 NOT NULL,
        [FechaCierre] datetime2 NULL,
        [Motivo] nvarchar(500) NOT NULL,
        [Observaciones] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedByUserId] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedByUserId] nvarchar(450) NULL,
        CONSTRAINT [PK_Vacantes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Vacantes_EstadosVacante_EstadoVacanteId] FOREIGN KEY ([EstadoVacanteId]) REFERENCES [EstadosVacante] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Vacantes_Puestos_PuestoId] FOREIGN KEY ([PuestoId]) REFERENCES [Puestos] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [HistorialEstadosVacante] (
        [Id] uniqueidentifier NOT NULL,
        [VacanteId] uniqueidentifier NOT NULL,
        [EstadoAnteriorId] uniqueidentifier NULL,
        [EstadoNuevoId] uniqueidentifier NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        [ChangedByUserId] nvarchar(450) NULL,
        [Motivo] nvarchar(500) NULL,
        CONSTRAINT [PK_HistorialEstadosVacante] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HistorialEstadosVacante_EstadosVacante_EstadoAnteriorId] FOREIGN KEY ([EstadoAnteriorId]) REFERENCES [EstadosVacante] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HistorialEstadosVacante_EstadosVacante_EstadoNuevoId] FOREIGN KEY ([EstadoNuevoId]) REFERENCES [EstadosVacante] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HistorialEstadosVacante_Vacantes_VacanteId] FOREIGN KEY ([VacanteId]) REFERENCES [Vacantes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [Postulaciones] (
        [Id] uniqueidentifier NOT NULL,
        [VacanteId] uniqueidentifier NOT NULL,
        [PostulanteId] uniqueidentifier NOT NULL,
        [EstadoPostulacionId] uniqueidentifier NOT NULL,
        [FechaPostulacion] datetime2 NOT NULL,
        [PuntajeCompatibilidad] decimal(5,2) NULL,
        [NivelCompatibilidad] nvarchar(50) NULL,
        [Observaciones] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedByUserId] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedByUserId] nvarchar(450) NULL,
        CONSTRAINT [PK_Postulaciones] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Postulaciones_PuntajeCompatibilidad] CHECK ([PuntajeCompatibilidad] IS NULL OR ([PuntajeCompatibilidad] >= 0 AND [PuntajeCompatibilidad] <= 100)),
        CONSTRAINT [FK_Postulaciones_EstadosPostulacion_EstadoPostulacionId] FOREIGN KEY ([EstadoPostulacionId]) REFERENCES [EstadosPostulacion] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Postulaciones_Postulantes_PostulanteId] FOREIGN KEY ([PostulanteId]) REFERENCES [Postulantes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Postulaciones_Vacantes_VacanteId] FOREIGN KEY ([VacanteId]) REFERENCES [Vacantes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [EvaluacionesPostulacion] (
        [Id] uniqueidentifier NOT NULL,
        [PostulacionId] uniqueidentifier NOT NULL,
        [EvaluadoAt] datetime2 NOT NULL,
        [EvaluadoByUserId] nvarchar(450) NULL,
        [PuntajeTecnico] decimal(5,2) NULL,
        [PuntajeEntrevista] decimal(5,2) NULL,
        [PuntajeCompatibilidad] decimal(5,2) NULL,
        [Recomendacion] nvarchar(50) NULL,
        [Observaciones] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedByUserId] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedByUserId] nvarchar(450) NULL,
        CONSTRAINT [PK_EvaluacionesPostulacion] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_EvaluacionesPostulacion_PuntajeCompatibilidad] CHECK ([PuntajeCompatibilidad] IS NULL OR ([PuntajeCompatibilidad] >= 0 AND [PuntajeCompatibilidad] <= 100)),
        CONSTRAINT [CK_EvaluacionesPostulacion_PuntajeEntrevista] CHECK ([PuntajeEntrevista] IS NULL OR ([PuntajeEntrevista] >= 0 AND [PuntajeEntrevista] <= 100)),
        CONSTRAINT [CK_EvaluacionesPostulacion_PuntajeTecnico] CHECK ([PuntajeTecnico] IS NULL OR ([PuntajeTecnico] >= 0 AND [PuntajeTecnico] <= 100)),
        CONSTRAINT [FK_EvaluacionesPostulacion_Postulaciones_PostulacionId] FOREIGN KEY ([PostulacionId]) REFERENCES [Postulaciones] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE TABLE [HistorialEstadosPostulacion] (
        [Id] uniqueidentifier NOT NULL,
        [PostulacionId] uniqueidentifier NOT NULL,
        [EstadoAnteriorId] uniqueidentifier NULL,
        [EstadoNuevoId] uniqueidentifier NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        [ChangedByUserId] nvarchar(450) NULL,
        [Observaciones] nvarchar(1000) NULL,
        CONSTRAINT [PK_HistorialEstadosPostulacion] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HistorialEstadosPostulacion_EstadosPostulacion_EstadoAnteriorId] FOREIGN KEY ([EstadoAnteriorId]) REFERENCES [EstadosPostulacion] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HistorialEstadosPostulacion_EstadosPostulacion_EstadoNuevoId] FOREIGN KEY ([EstadoNuevoId]) REFERENCES [EstadosPostulacion] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HistorialEstadosPostulacion_Postulaciones_PostulacionId] FOREIGN KEY ([PostulacionId]) REFERENCES [Postulaciones] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'EsTerminal', N'EsTerminalPositivo', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[EstadosPostulacion]'))
        SET IDENTITY_INSERT [EstadosPostulacion] ON;
    EXEC(N'INSERT INTO [EstadosPostulacion] ([Id], [Codigo], [EsTerminal], [EsTerminalPositivo], [Nombre], [Orden])
    VALUES (''30000000-0000-0000-0000-000000000001'', N''Postulado'', CAST(0 AS bit), CAST(0 AS bit), N''Postulado'', 1),
    (''30000000-0000-0000-0000-000000000002'', N''Preseleccionado'', CAST(0 AS bit), CAST(0 AS bit), N''Preseleccionado'', 2),
    (''30000000-0000-0000-0000-000000000003'', N''Entrevistado'', CAST(0 AS bit), CAST(0 AS bit), N''Entrevistado'', 3),
    (''30000000-0000-0000-0000-000000000004'', N''Aprobado'', CAST(0 AS bit), CAST(0 AS bit), N''Aprobado'', 4),
    (''30000000-0000-0000-0000-000000000005'', N''Rechazado'', CAST(1 AS bit), CAST(0 AS bit), N''Rechazado'', 5),
    (''30000000-0000-0000-0000-000000000006'', N''Contratado'', CAST(1 AS bit), CAST(1 AS bit), N''Contratado'', 6)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'EsTerminal', N'EsTerminalPositivo', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[EstadosPostulacion]'))
        SET IDENTITY_INSERT [EstadosPostulacion] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'EsTerminal', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[EstadosVacante]'))
        SET IDENTITY_INSERT [EstadosVacante] ON;
    EXEC(N'INSERT INTO [EstadosVacante] ([Id], [Codigo], [EsTerminal], [Nombre], [Orden])
    VALUES (''20000000-0000-0000-0000-000000000001'', N''Abierta'', CAST(0 AS bit), N''Abierta'', 1),
    (''20000000-0000-0000-0000-000000000002'', N''EnSeleccion'', CAST(0 AS bit), N''En Selección'', 2),
    (''20000000-0000-0000-0000-000000000003'', N''Cubierta'', CAST(1 AS bit), N''Cubierta'', 3),
    (''20000000-0000-0000-0000-000000000004'', N''Cancelada'', CAST(1 AS bit), N''Cancelada'', 4)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'EsTerminal', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[EstadosVacante]'))
        SET IDENTITY_INSERT [EstadosVacante] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'Nombre', N'Orden', N'ValorNumerico') AND [object_id] = OBJECT_ID(N'[NivelesHabilidad]'))
        SET IDENTITY_INSERT [NivelesHabilidad] ON;
    EXEC(N'INSERT INTO [NivelesHabilidad] ([Id], [Codigo], [Nombre], [Orden], [ValorNumerico])
    VALUES (''10000000-0000-0000-0000-000000000001'', N''Basico'', N''Básico'', 1, CAST(1 AS tinyint)),
    (''10000000-0000-0000-0000-000000000002'', N''Intermedio'', N''Intermedio'', 2, CAST(2 AS tinyint)),
    (''10000000-0000-0000-0000-000000000003'', N''Avanzado'', N''Avanzado'', 3, CAST(3 AS tinyint)),
    (''10000000-0000-0000-0000-000000000004'', N''Experto'', N''Experto'', 4, CAST(4 AS tinyint))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'Nombre', N'Orden', N'ValorNumerico') AND [object_id] = OBJECT_ID(N'[NivelesHabilidad]'))
        SET IDENTITY_INSERT [NivelesHabilidad] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Auditorias_CorrelationId] ON [Auditorias] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Auditorias_EntityName_EntityId_OccurredAt] ON [Auditorias] ([EntityName], [EntityId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Auditorias_UserId_OccurredAt] ON [Auditorias] ([UserId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CargoHabilidades_CargoId_HabilidadId] ON [CargoHabilidades] ([CargoId], [HabilidadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_CargoHabilidades_HabilidadId] ON [CargoHabilidades] ([HabilidadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_CargoHabilidades_NivelRequeridoId] ON [CargoHabilidades] ([NivelRequeridoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Cargos_Codigo] ON [Cargos] ([Codigo]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Cargos_IsDeleted] ON [Cargos] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Cargos_Nombre] ON [Cargos] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EstadosPostulacion_Codigo] ON [EstadosPostulacion] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EstadosVacante_Codigo] ON [EstadosVacante] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_EvaluacionesPostulacion_EvaluadoAt] ON [EvaluacionesPostulacion] ([EvaluadoAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_EvaluacionesPostulacion_IsDeleted] ON [EvaluacionesPostulacion] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_EvaluacionesPostulacion_PostulacionId] ON [EvaluacionesPostulacion] ([PostulacionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Habilidades_Categoria] ON [Habilidades] ([Categoria]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Habilidades_Codigo] ON [Habilidades] ([Codigo]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Habilidades_IsDeleted] ON [Habilidades] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_HistorialEstadosPostulacion_EstadoAnteriorId] ON [HistorialEstadosPostulacion] ([EstadoAnteriorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_HistorialEstadosPostulacion_EstadoNuevoId] ON [HistorialEstadosPostulacion] ([EstadoNuevoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_HistorialEstadosPostulacion_PostulacionId_ChangedAt] ON [HistorialEstadosPostulacion] ([PostulacionId], [ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_HistorialEstadosVacante_EstadoAnteriorId] ON [HistorialEstadosVacante] ([EstadoAnteriorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_HistorialEstadosVacante_EstadoNuevoId] ON [HistorialEstadosVacante] ([EstadoNuevoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_HistorialEstadosVacante_VacanteId_ChangedAt] ON [HistorialEstadosVacante] ([VacanteId], [ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NivelesHabilidad_Codigo] ON [NivelesHabilidad] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NivelesHabilidad_ValorNumerico] ON [NivelesHabilidad] ([ValorNumerico]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Ocupaciones_IsDeleted] ON [Ocupaciones] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Ocupaciones_PersonaId] ON [Ocupaciones] ([PersonaId]) WHERE [FechaFin] IS NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Ocupaciones_PersonaId_FechaInicio_FechaFin] ON [Ocupaciones] ([PersonaId], [FechaInicio], [FechaFin]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Ocupaciones_PuestoId] ON [Ocupaciones] ([PuestoId]) WHERE [FechaFin] IS NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Ocupaciones_PuestoId_FechaInicio_FechaFin] ON [Ocupaciones] ([PuestoId], [FechaInicio], [FechaFin]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_PersonaHabilidades_HabilidadId] ON [PersonaHabilidades] ([HabilidadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_PersonaHabilidades_NivelHabilidadId] ON [PersonaHabilidades] ([NivelHabilidadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PersonaHabilidades_PersonaId_HabilidadId] ON [PersonaHabilidades] ([PersonaId], [HabilidadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Personas_Apellidos_Nombres] ON [Personas] ([Apellidos], [Nombres]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Personas_Email] ON [Personas] ([Email]) WHERE [Email] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Personas_IsDeleted] ON [Personas] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Personas_Legajo] ON [Personas] ([Legajo]) WHERE [Legajo] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Personas_TipoDocumento_NumeroDocumento] ON [Personas] ([TipoDocumento], [NumeroDocumento]) WHERE [TipoDocumento] IS NOT NULL AND [NumeroDocumento] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Postulaciones_EstadoPostulacionId] ON [Postulaciones] ([EstadoPostulacionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Postulaciones_IsDeleted] ON [Postulaciones] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Postulaciones_PostulanteId] ON [Postulaciones] ([PostulanteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Postulaciones_VacanteId_EstadoPostulacionId] ON [Postulaciones] ([VacanteId], [EstadoPostulacionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Postulaciones_VacanteId_PostulanteId] ON [Postulaciones] ([VacanteId], [PostulanteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Postulantes_Apellidos_Nombres] ON [Postulantes] ([Apellidos], [Nombres]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Postulantes_Email] ON [Postulantes] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Postulantes_IsDeleted] ON [Postulantes] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Postulantes_PersonaId] ON [Postulantes] ([PersonaId]) WHERE [PersonaId] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Puestos_CargoId] ON [Puestos] ([CargoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Puestos_Codigo] ON [Puestos] ([Codigo]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Puestos_IsDeleted] ON [Puestos] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Puestos_PuestoSuperiorId] ON [Puestos] ([PuestoSuperiorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Puestos_UnidadOrganizativaId] ON [Puestos] ([UnidadOrganizativaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UnidadesOrganizativas_Codigo] ON [UnidadesOrganizativas] ([Codigo]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_UnidadesOrganizativas_IsDeleted] ON [UnidadesOrganizativas] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_UnidadesOrganizativas_Nombre] ON [UnidadesOrganizativas] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_UnidadesOrganizativas_UnidadPadreId] ON [UnidadesOrganizativas] ([UnidadPadreId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Vacantes_EstadoVacanteId] ON [Vacantes] ([EstadoVacanteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Vacantes_EstadoVacanteId_FechaApertura] ON [Vacantes] ([EstadoVacanteId], [FechaApertura]) WHERE [FechaCierre] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Vacantes_FechaApertura] ON [Vacantes] ([FechaApertura]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Vacantes_IsDeleted] ON [Vacantes] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    CREATE INDEX [IX_Vacantes_PuestoId] ON [Vacantes] ([PuestoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022804_InicialSgvo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260613022804_InicialSgvo', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022933_AgregarDatosSemillaBase'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
        SET IDENTITY_INSERT [AspNetRoles] ON;
    EXEC(N'INSERT INTO [AspNetRoles] ([Id], [ConcurrencyStamp], [Name], [NormalizedName])
    VALUES (N''Administrador'', N''rol-Administrador'', N''Administrador'', N''ADMINISTRADOR''),
    (N''EvaluadorSeleccion'', N''rol-EvaluadorSeleccion'', N''EvaluadorSeleccion'', N''EVALUADORSELECCION''),
    (N''GestorOrganizacional'', N''rol-GestorOrganizacional'', N''GestorOrganizacional'', N''GESTORORGANIZACIONAL''),
    (N''Lector'', N''rol-Lector'', N''Lector'', N''LECTOR''),
    (N''RecursosHumanos'', N''rol-RecursosHumanos'', N''RecursosHumanos'', N''RECURSOSHUMANOS'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
        SET IDENTITY_INSERT [AspNetRoles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022933_AgregarDatosSemillaBase'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'CreatedAt', N'CreatedByUserId', N'DeletedAt', N'DeletedByUserId', N'Descripcion', N'IsActive', N'IsDeleted', N'Nivel', N'Nombre', N'UpdatedAt', N'UpdatedByUserId') AND [object_id] = OBJECT_ID(N'[Cargos]'))
        SET IDENTITY_INSERT [Cargos] ON;
    EXEC(N'INSERT INTO [Cargos] ([Id], [Codigo], [CreatedAt], [CreatedByUserId], [DeletedAt], [DeletedByUserId], [Descripcion], [IsActive], [IsDeleted], [Nivel], [Nombre], [UpdatedAt], [UpdatedByUserId])
    VALUES (''40000000-0000-0000-0000-000000000001'', N''DECANO'', ''0001-01-01T00:00:00.0000000'', NULL, NULL, NULL, NULL, CAST(1 AS bit), CAST(0 AS bit), N''Directivo'', N''Decano'', NULL, NULL),
    (''40000000-0000-0000-0000-000000000002'', N''SECRETARIO'', ''0001-01-01T00:00:00.0000000'', NULL, NULL, NULL, NULL, CAST(1 AS bit), CAST(0 AS bit), N''Directivo'', N''Secretario'', NULL, NULL),
    (''40000000-0000-0000-0000-000000000003'', N''DIRECTOR'', ''0001-01-01T00:00:00.0000000'', NULL, NULL, NULL, NULL, CAST(1 AS bit), CAST(0 AS bit), N''Conducción media'', N''Director'', NULL, NULL),
    (''40000000-0000-0000-0000-000000000004'', N''JEFE_DEPARTAMENTO'', ''0001-01-01T00:00:00.0000000'', NULL, NULL, NULL, NULL, CAST(1 AS bit), CAST(0 AS bit), N''Conducción media'', N''Jefe de Departamento'', NULL, NULL),
    (''40000000-0000-0000-0000-000000000005'', N''ADMINISTRATIVO'', ''0001-01-01T00:00:00.0000000'', NULL, NULL, NULL, NULL, CAST(1 AS bit), CAST(0 AS bit), N''Operativo'', N''Administrativo'', NULL, NULL),
    (''40000000-0000-0000-0000-000000000006'', N''PROFESOR'', ''0001-01-01T00:00:00.0000000'', NULL, NULL, NULL, NULL, CAST(1 AS bit), CAST(0 AS bit), N''Académico'', N''Profesor'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'CreatedAt', N'CreatedByUserId', N'DeletedAt', N'DeletedByUserId', N'Descripcion', N'IsActive', N'IsDeleted', N'Nivel', N'Nombre', N'UpdatedAt', N'UpdatedByUserId') AND [object_id] = OBJECT_ID(N'[Cargos]'))
        SET IDENTITY_INSERT [Cargos] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022933_AgregarDatosSemillaBase'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Categoria', N'Codigo', N'CreatedAt', N'CreatedByUserId', N'DeletedAt', N'DeletedByUserId', N'Descripcion', N'IsActive', N'IsDeleted', N'Nombre', N'UpdatedAt', N'UpdatedByUserId') AND [object_id] = OBJECT_ID(N'[Habilidades]'))
        SET IDENTITY_INSERT [Habilidades] ON;
    EXEC(N'INSERT INTO [Habilidades] ([Id], [Categoria], [Codigo], [CreatedAt], [CreatedByUserId], [DeletedAt], [DeletedByUserId], [Descripcion], [IsActive], [IsDeleted], [Nombre], [UpdatedAt], [UpdatedByUserId])
    VALUES (''50000000-0000-0000-0000-000000000001'', N''Conducción'', N''LIDERAZGO'', ''0001-01-01T00:00:00.0000000'', NULL, NULL, NULL, NULL, CAST(1 AS bit), CAST(0 AS bit), N''Liderazgo'', NULL, NULL),
    (''50000000-0000-0000-0000-000000000002'', N''Conducción'', N''GESTION_PERSONAL'', ''0001-01-01T00:00:00.0000000'', NULL, NULL, NULL, NULL, CAST(1 AS bit), CAST(0 AS bit), N''Gestión de Personal'', NULL, NULL),
    (''50000000-0000-0000-0000-000000000003'', N''Técnica'', N''SQL_SERVER'', ''0001-01-01T00:00:00.0000000'', NULL, NULL, NULL, NULL, CAST(1 AS bit), CAST(0 AS bit), N''SQL Server'', NULL, NULL),
    (''50000000-0000-0000-0000-000000000004'', N''Técnica'', N''EF_CORE'', ''0001-01-01T00:00:00.0000000'', NULL, NULL, NULL, NULL, CAST(1 AS bit), CAST(0 AS bit), N''Entity Framework Core'', NULL, NULL),
    (''50000000-0000-0000-0000-000000000005'', N''Técnica'', N''DOTNET'', ''0001-01-01T00:00:00.0000000'', NULL, NULL, NULL, NULL, CAST(1 AS bit), CAST(0 AS bit), N''Programación .NET'', NULL, NULL),
    (''50000000-0000-0000-0000-000000000006'', N''Dominio'', N''ADMINISTRACION_PUBLICA'', ''0001-01-01T00:00:00.0000000'', NULL, NULL, NULL, NULL, CAST(1 AS bit), CAST(0 AS bit), N''Administración Pública'', NULL, NULL),
    (''50000000-0000-0000-0000-000000000007'', N''Académica'', N''DOCENCIA_UNIVERSITARIA'', ''0001-01-01T00:00:00.0000000'', NULL, NULL, NULL, NULL, CAST(1 AS bit), CAST(0 AS bit), N''Docencia Universitaria'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Categoria', N'Codigo', N'CreatedAt', N'CreatedByUserId', N'DeletedAt', N'DeletedByUserId', N'Descripcion', N'IsActive', N'IsDeleted', N'Nombre', N'UpdatedAt', N'UpdatedByUserId') AND [object_id] = OBJECT_ID(N'[Habilidades]'))
        SET IDENTITY_INSERT [Habilidades] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613022933_AgregarDatosSemillaBase'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260613022933_AgregarDatosSemillaBase', N'9.0.0');
END;

COMMIT;
GO

