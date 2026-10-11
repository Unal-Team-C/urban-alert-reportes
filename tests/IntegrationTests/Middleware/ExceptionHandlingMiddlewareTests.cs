using API.Middleware;
using Application.Geoespacial;
using Domain.Reportes;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntegrationTests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_MapeaTransicionEstadoInvalida_A400()
    {
        int statusCode = await InvocarConExcepcion(
            new TransicionEstadoInvalidaException(EstadoReporte.Reportado, EstadoReporte.Asignado));

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    [Fact]
    public async Task InvokeAsync_MapeaDbUpdateConcurrencyException_A409()
    {
        int statusCode = await InvocarConExcepcion(new DbUpdateConcurrencyException());

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
    }

    [Fact]
    public async Task InvokeAsync_MapeaGeoespacialNoDisponible_A503()
    {
        int statusCode = await InvocarConExcepcion(new GeoespacialNoDisponibleException("caído"));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCode);
    }

    [Fact]
    public async Task InvokeAsync_MapeaCoordenadaInvalida_A400()
    {
        int statusCode = await InvocarConExcepcion(new CoordenadaInvalidaException("fuera de Bogotá"));

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    [Fact]
    public async Task InvokeAsync_MapeaExcepcionNoControlada_A500()
    {
        int statusCode = await InvocarConExcepcion(new InvalidOperationException("falla inesperada"));

        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
    }

    private static async Task<int> InvocarConExcepcion(Exception excepcion)
    {
        ExceptionHandlingMiddleware middleware = new(
            _ => throw excepcion,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        return context.Response.StatusCode;
    }
}
