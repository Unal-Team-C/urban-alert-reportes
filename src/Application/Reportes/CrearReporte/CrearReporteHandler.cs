using Application.Imagenes;
using Application.Interfaces.CrearReporte;
using Application.Interfaces.Geoespacial;
using Application.Interfaces.Multimedia;
using Application.Interfaces.Reportes;
using Application.Reportes.Eventos;
using Domain.Reportes;

namespace Application.Reportes.CrearReporte;

public class CrearReporteHandler : ICrearReporteHandler
{
    private readonly IReporteRepository _reporteRepository;
    private readonly IGeoespacialClient _geoespacialClient;
    private readonly IMultimediaClient _multimediaClient;

    public CrearReporteHandler(
        IReporteRepository reporteRepository,
        IGeoespacialClient geoespacialClient,
        IMultimediaClient multimediaClient)
    {
        _reporteRepository = reporteRepository;
        _geoespacialClient = geoespacialClient;
        _multimediaClient = multimediaClient;
    }

    public async Task<Guid> Handle(CrearReporteCommand command, CancellationToken cancellationToken)
    {
        // Sin autenticación todavía: el usuario lo envía el cliente. Si no llega, se asigna uno
        // genérico hasta que exista un proveedor de identidad del cual tomar el usuario actual.
        Guid idUsuario = command.IdUsuario ?? Guid.NewGuid();

        string? nombreImagen = null;
        FormatoImagen? formatoImagen = null;
        if (command.Imagen is not null)
        {
            // Reportes valida el formato y tamaño localmente (sin red, falla rápido) antes de
            // subir la imagen al servicio de Multimedia, que la aloja y devuelve su URL definitiva.
            formatoImagen = await ValidadorImagen.ValidarAsync(command.Imagen.Contenido, command.Imagen.Tamano, cancellationToken);
            nombreImagen = NombreDeArchivo(command.Imagen.NombreArchivo);
        }

        // Se valida el reporte antes de subir la imagen a Multimedia o llamar a Geoespacial, para
        // no subir imágenes ni registrar coordenadas de reportes que nunca se van a crear.
        Reporte reporte = new Reporte(
            command.Categoria,
            command.Tipo,
            command.Descripcion,
            command.UrlImagen,
            nombreImagen,
            idUsuario);

        if (command.Imagen is not null)
        {
            string urlImagen = await _multimediaClient.SubirImagenAsync(
                command.Imagen.Contenido,
                command.Imagen.Tamano,
                nombreImagen!,
                formatoImagen!.TipoContenido,
                command.Latitud,
                command.Longitud,
                cancellationToken);

            reporte.AsignarUrlImagen(urlImagen);
        }

        Guid idCoordenada = await _geoespacialClient.AsignarCoordenadaAsync(
            reporte.Id, command.Latitud, command.Longitud, cancellationToken);

        reporte.AsignarCoordenada(idCoordenada);

        ReporteCreadoEvent evento = new(
            Guid.CreateVersion7(),
            new ReporteEventoDto(reporte.Id, reporte.Estado, reporte.IdCoordenada, reporte.IdUsuario, reporte.Fecha));

        // El evento se guarda con el reporte (outbox) y se entrega en segundo plano: un fallo del bus ya
        // no hace fallar una creación que sí ocurrió (spec 002 RF-001, RF-005).
        await _reporteRepository.AgregarAsync(reporte, evento, cancellationToken);

        return reporte.Id;
    }

    // Algunos navegadores envían la ruta completa ("C:\fakepath\foto.jpg"): solo interesa el
    // nombre. Si es muy largo se recorta conservando la extensión.
    private static string NombreDeArchivo(string nombreEnviado)
    {
        string nombre = nombreEnviado.Split('/', '\\').Last().Trim();
        if (nombre.Length == 0 || nombre is "." or "..")
            return "imagen";

        if (nombre.Length <= Reporte.NombreImagenMaxLength)
            return nombre;

        string extension = Path.GetExtension(nombre);
        if (extension.Length > 10)
            extension = "";

        return nombre[..(Reporte.NombreImagenMaxLength - extension.Length)] + extension;
    }
}
