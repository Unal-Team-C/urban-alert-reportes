using Application.Interfaces.Eventos;
using Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Mensajeria.Outbox;

// Entrega al bus los eventos guardados en el outbox (spec 002 RF-002 a RF-004; plan D-001, D-007).
// Cada vuelta toma un lote de pendientes con FOR UPDATE SKIP LOCKED dentro de una transacción: dos tareas de ECS
// nunca toman la misma fila. Si el proceso muere tras publicar y antes del COMMIT, la fila se vuelve a entregar
// (al menos una vez); Auditoría descarta la repetición por idEvento.
public class DespachadorOutbox(
    IServiceScopeFactory scopes,
    OutboxOpciones opciones,
    TimeProvider reloj,
    ILogger<DespachadorOutbox> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan intervalo = TimeSpan.FromMilliseconds(opciones.IntervaloMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Un lote lleno indica que hay más pendientes: se sigue sin esperar el intervalo.
                while (await ProcesarLoteAsync(stoppingToken) == opciones.Lote)
                {
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Base no disponible u otro fallo de la vuelta: se reintenta en la siguiente.
                logger.LogError(ex, "Falló una vuelta del despachador del outbox");
            }

            try
            {
                await Task.Delay(intervalo, reloj, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // Procesa un lote y devuelve cuántos mensajes tomó. Público para las pruebas de integración.
    public async Task<int> ProcesarLoteAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopes.CreateScope();
        ReportesDbContext db = scope.ServiceProvider.GetRequiredService<ReportesDbContext>();
        IEventPublisher publicador = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        await using IDbContextTransaction transaccion = await db.Database.BeginTransactionAsync(cancellationToken);

        DateTime ahora = reloj.GetUtcNow().UtcDateTime;
        List<MensajeOutbox> lote = await db.MensajesOutbox
            .FromSql($"""
                SELECT * FROM "MensajesOutbox"
                WHERE "Estado" = 'PENDIENTE' AND "ProximoIntento" <= {ahora}
                ORDER BY "RegistradoEn"
                LIMIT {opciones.Lote}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (MensajeOutbox mensaje in lote)
            await EntregarAsync(mensaje, publicador, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);

        return lote.Count;
    }

    private async Task EntregarAsync(MensajeOutbox mensaje, IEventPublisher publicador, CancellationToken cancellationToken)
    {
        try
        {
            object evento = SerializacionEventos.Deserializar(mensaje.Tipo, mensaje.Contenido);

            // Tope por publicación: un transporte que no responde no retiene el lote (ni sus filas bloqueadas).
            using CancellationTokenSource limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limite.CancelAfter(TimeSpan.FromSeconds(opciones.TimeoutPublicacionSegundos));
            await publicador.PublicarAsync(evento, limite.Token);

            mensaje.MarcarEntregado(reloj.GetUtcNow().UtcDateTime);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            string motivo = ex is OperationCanceledException
                ? $"La publicación superó {opciones.TimeoutPublicacionSegundos} s."
                : $"{ex.GetType().Name}: {ex.Message}";

            mensaje.RegistrarFallo(motivo, reloj.GetUtcNow().UtcDateTime, opciones.MaxIntentos);

            if (mensaje.Estado == EstadoMensajeOutbox.Fallido)
                logger.LogError(
                    "Evento {IdEvento} ({Tipo}) marcado FALLIDO tras {Intentos} intentos: {Motivo}",
                    mensaje.Id, mensaje.Tipo, mensaje.Intentos, motivo);
            else
                logger.LogWarning(
                    "No se pudo entregar el evento {IdEvento} ({Tipo}), intento {Intentos}; siguiente a las {ProximoIntento:O}: {Motivo}",
                    mensaje.Id, mensaje.Tipo, mensaje.Intentos, mensaje.ProximoIntento, motivo);
        }
    }
}
