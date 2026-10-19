using System.Text.Json;
using Application.Reportes.Eventos;
using Domain.Reportes;
using Infrastructure.Mensajeria.Outbox;

namespace UnitTests.Infrastructure;

public class SerializacionEventosTests
{
    private static readonly DateTime Ahora = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private static ReporteCreadoEvent Evento() => new(
        Guid.Parse("0199d2f1-0000-7000-8000-000000000001"),
        new ReporteEventoDto(
            Guid.Parse("0199d2f1-0000-7000-8000-000000000002"),
            EstadoReporte.EnIntervencion,
            Guid.Parse("0199d2f1-0000-7000-8000-000000000003"),
            Guid.Parse("0199d2f1-0000-7000-8000-000000000004"),
            new DateTime(2026, 10, 11, 15, 30, 0, DateTimeKind.Utc)));

    [Fact]
    public void Deserializar_RecuperaElMismoEvento()
    {
        ReporteCreadoEvent original = Evento();

        object leido = SerializacionEventos.Deserializar(
            SerializacionEventos.NombreDeTipo(original), SerializacionEventos.Serializar(original));

        Assert.Equal(original, Assert.IsType<ReporteCreadoEvent>(leido));
    }

    [Fact]
    public void Serializar_UsaCamelCaseYCodigosDeEnum()
    {
        using JsonDocument json = JsonDocument.Parse(SerializacionEventos.Serializar(Evento()));

        Assert.Equal("EN_INTERVENCION", json.RootElement.GetProperty("reporte").GetProperty("estado").GetString());
        Assert.Equal("0199d2f1-0000-7000-8000-000000000001", json.RootElement.GetProperty("idEvento").GetString());
    }

    [Fact]
    public void Deserializar_FallaConUnTipoNoRegistrado()
    {
        Assert.Throws<InvalidOperationException>(() => SerializacionEventos.Deserializar("Otro", "{}"));
    }

    [Fact]
    public void Crear_UsaElIdDelEventoYQuedaPendiente()
    {
        MensajeOutbox mensaje = MensajeOutbox.Crear(Evento(), Ahora);

        Assert.Equal(Evento().IdEvento, mensaje.Id);
        Assert.Equal(nameof(ReporteCreadoEvent), mensaje.Tipo);
        Assert.Equal(EstadoMensajeOutbox.Pendiente, mensaje.Estado);
        Assert.Equal(Ahora, mensaje.ProximoIntento);
        Assert.Equal(0, mensaje.Intentos);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 8)]
    [InlineData(9, 300)] // 2^9 = 512 s, limitado a 5 min
    public void RegistrarFallo_EsperaExponencialConTope(int intentos, int segundosEsperados)
    {
        MensajeOutbox mensaje = MensajeOutbox.Crear(Evento(), Ahora);

        for (int i = 0; i < intentos; i++)
            mensaje.RegistrarFallo("bus no disponible", Ahora, maxIntentos: 10);

        Assert.Equal(EstadoMensajeOutbox.Pendiente, mensaje.Estado);
        Assert.Equal(Ahora.AddSeconds(segundosEsperados), mensaje.ProximoIntento);
    }

    [Fact]
    public void RegistrarFallo_QuedaFallidoAlLlegarAlMaximoYRecortaElError()
    {
        MensajeOutbox mensaje = MensajeOutbox.Crear(Evento(), Ahora);

        mensaje.RegistrarFallo("x", Ahora, maxIntentos: 2);
        mensaje.RegistrarFallo(new string('e', 5000), Ahora, maxIntentos: 2);

        Assert.Equal(EstadoMensajeOutbox.Fallido, mensaje.Estado);
        Assert.Equal(MensajeOutbox.UltimoErrorMaxLength, mensaje.UltimoError!.Length);
    }
}
