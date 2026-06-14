using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Seguridad;
using SGV.Infraestructura;
using SGV.Infraestructura.Persistencia;
using SGV.Api.Seguridad;

var builder = WebApplication.CreateBuilder(args);

// Controllers and problem details
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "SGV Read-Only API",
        Version = "v1",
        Description = "Read-only HTTP API for SGV organizational structure and skills data."
    });
});

// MySQL DbContext with audit interceptor
builder.Services.AddScoped<AuditoriaSaveChangesInterceptor>();
var connectionString = builder.Configuration.GetConnectionString("SgvDatabase");
builder.Services.AddDbContext<SgvDbContext>((sp, options) =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
           .AddInterceptors(sp.GetRequiredService<AuditoriaSaveChangesInterceptor>());
});

// Anonymous / system user for audit trail
builder.Services.AddScoped<IUsuarioActual, UsuarioActualAnonimo>();

// Infrastructure services (repositories, UoW, query services)
builder.Services.AddInfraestructuraServicios();

var app = builder.Build();

// Middleware pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
