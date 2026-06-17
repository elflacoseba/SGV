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
        Title = "SGV API",
        Version = "v1",
        Description = "HTTP API for SGV organizational structure, skills data, and organizational-unit management."
    });

    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
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
