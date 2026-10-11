using System.Globalization;
using System.Text.Json.Serialization;
using API.HealthChecks;
using API.Middleware;
using API.ModelBinding;
using Application;
using Application.Comun;
using Infrastructure;
using Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

// Cultura invariante: los números de los formularios (multipart) se interpretan con la
// cultura del proceso; así "4.65" es siempre 4.65, también en un servidor configurado en español.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Enums como códigos UPPER_SNAKE_CASE y sin valores numéricos. Se registra en las
// opciones de MVC (serialización de controladores) y en las de HTTP (las que usa OpenAPI).
JsonStringEnumConverter codigosEnum = new(CodigoEnum.Politica, allowIntegerValues: false);

builder.Services.AddControllers(options => options.ModelBinderProviders.Insert(0, new CodigoEnumModelBinderProvider()))
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(codigosEnum));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(codigosEnum));
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres");

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    using IServiceScope scope = app.Services.CreateScope();
    ReportesDbContext dbContext = scope.ServiceProvider.GetRequiredService<ReportesDbContext>();
    dbContext.Database.Migrate();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program { }