using Application.Imagenes;
using Application.Interfaces.ActualizarEstadoReporte;
using Application.Interfaces.ActualizarNivelEmergenciaReporte;
using Application.Interfaces.AsignarResponsableReporte;
using Application.Interfaces.CrearReporte;
using Application.Interfaces.EliminarReporte;
using Application.Interfaces.ObtenerReportePorId;
using Application.Interfaces.ObtenerReportes;
using Application.Interfaces.RechazarReporte;
using Application.Reportes.ActualizarEstadoReporte;
using Application.Reportes.ActualizarNivelEmergenciaReporte;
using Application.Reportes.AsignarResponsableReporte;
using Application.Reportes.Catalogo;
using Application.Reportes.CrearReporte;
using Application.Reportes.EliminarReporte;
using Application.Reportes.ObtenerReportePorId;
using Application.Reportes.ObtenerReportes;
using Application.Reportes.RechazarReporte;
using Domain.Reportes;
using Microsoft.AspNetCore.Mvc;
using API.DTOs.Reportes;
using Application.Reportes;

namespace API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ReportesController : ControllerBase
{
    private readonly ICrearReporteHandler _crearReporteHandler;
    private readonly IObtenerReportesHandler _obtenerReportesHandler;
    private readonly IObtenerReportePorIdHandler _obtenerReportePorIdHandler;
    private readonly IActualizarEstadoReporteHandler _actualizarEstadoReporteHandler;
    private readonly IActualizarNivelEmergenciaReporteHandler _actualizarNivelEmergenciaReporteHandler;
    private readonly IAsignarResponsableReporteHandler _asignarResponsableReporteHandler;
    private readonly IRechazarReporteHandler _rechazarReporteHandler;
    private readonly IEliminarReporteHandler _eliminarReporteHandler;

    public ReportesController(
        ICrearReporteHandler crearReporteHandler,
        IObtenerReportesHandler obtenerReportesHandler,
        IObtenerReportePorIdHandler obtenerReportePorIdHandler,
        IActualizarEstadoReporteHandler actualizarEstadoReporteHandler,
        IActualizarNivelEmergenciaReporteHandler actualizarNivelEmergenciaReporteHandler,
        IAsignarResponsableReporteHandler asignarResponsableReporteHandler,
        IRechazarReporteHandler rechazarReporteHandler,
        IEliminarReporteHandler eliminarReporteHandler)
    {
        _crearReporteHandler = crearReporteHandler;
        _obtenerReportesHandler = obtenerReportesHandler;
        _obtenerReportePorIdHandler = obtenerReportePorIdHandler;
        _actualizarEstadoReporteHandler = actualizarEstadoReporteHandler;
        _actualizarNivelEmergenciaReporteHandler = actualizarNivelEmergenciaReporteHandler;
        _asignarResponsableReporteHandler = asignarResponsableReporteHandler;
        _rechazarReporteHandler = rechazarReporteHandler;
        _eliminarReporteHandler = eliminarReporteHandler;
    }

    [HttpPost]
    [Consumes("application/json")]
    [EndpointSummary("Crear un reporte")]
    [EndpointDescription("""
        Crea un nuevo reporte de daño urbano.

        El cliente debe proporcionar la categoría y el tipo de reporte
        (códigos de GET /api/v1/Reportes/catalogo; el tipo debe
        pertenecer a la categoría), descripción, la ubicación
        (latitud y longitud dentro de Bogotá) y URL de la imagen.

        La ubicación se registra de forma síncrona en el servicio
        Geoespacial, que devuelve el identificador de coordenada que
        se guarda en el reporte. Si Geoespacial no está disponible,
        el reporte no se crea (503).

        idUsuario es el usuario que crea el reporte. Mientras no exista
        autenticación lo envía el cliente; es opcional y, si no llega,
        se asigna un usuario genérico.

        La fecha de creación, el identificador del reporte y el
        nivel de emergencia inicial son establecidos internamente
        por el servicio.
        """)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CrearReporte(
        [FromBody] CrearReporteRequest request,
        CancellationToken cancellationToken)
    {
        CrearReporteCommand command = new CrearReporteCommand(
            request.Categoria,
            request.Tipo,
            request.Descripcion,
            request.Latitud,
            request.Longitud,
            request.UrlImagen,
            IdUsuario: request.IdUsuario);

        Guid idReporte = await _crearReporteHandler.Handle(command, cancellationToken);

        return Created(
            $"/api/v1/Reportes/{idReporte}",
            new
            {
                IdReporte = idReporte,
                message = "Reporte creado"
            });
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [EndpointSummary("Crear un reporte con imagen desde el dispositivo")]
    [EndpointDescription("""
        Igual que crear un reporte, pero la imagen se sube como archivo
        en lugar de enviar su URL. Se envía como multipart/form-data con
        los campos categoria, tipo, descripcion, latitud, longitud,
        idUsuario (opcional) e imagen (JPEG o PNG de hasta 3,5 MB; el formato se valida
        por el contenido del archivo).

        Reportes recibe el archivo junto con el reporte, lo sube de forma
        síncrona al servicio Multimedia (que la aloja y devuelve su URL
        definitiva, guardada en urlImagen) y guarda también el nombre
        original del archivo (nombreImagen). Si Multimedia no está
        disponible, el reporte no se crea (503).
        """)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    // Margen sobre el máximo de la imagen para el resto de campos del formulario.
    [RequestSizeLimit(ValidadorImagen.TamanoMaximoBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ValidadorImagen.TamanoMaximoBytes + 1024 * 1024)]
    public async Task<IActionResult> CrearReporteConImagen(
        [FromForm] CrearReporteConImagenRequest request,
        CancellationToken cancellationToken)
    {
        await using Stream imagen = request.Imagen.OpenReadStream();

        CrearReporteCommand command = new CrearReporteCommand(
            request.Categoria,
            request.Tipo,
            request.Descripcion,
            request.Latitud,
            request.Longitud,
            UrlImagen: null,
            new ImagenAdjunta(imagen, request.Imagen.Length, request.Imagen.FileName),
            request.IdUsuario);

        Guid idReporte = await _crearReporteHandler.Handle(command, cancellationToken);

        return Created(
            $"/api/v1/Reportes/{idReporte}",
            new
            {
                IdReporte = idReporte,
                message = "Reporte creado"
            });
    }

    [HttpGet("catalogo")]
    [EndpointSummary("Obtener el catálogo de reportes")]
    [EndpointDescription("""
        Obtiene las categorías de reporte con sus tipos.

        Los códigos son los valores que se envían en "categoria" y
        "tipo" al crear un reporte; los nombres son los textos
        para mostrar al usuario.
        """)]
    [ProducesResponseType<IReadOnlyList<CategoriaReporteDto>>(StatusCodes.Status200OK)]
    public IActionResult ObtenerCatalogo() => Ok(CategoriaReporteDto.DesdeCatalogo());

    [HttpGet]
    [EndpointSummary("Obtener reportes")]
    [EndpointDescription("""
        Obtiene una página de reportes registrados en el sistema.

        Permite filtrar opcionalmente por estado, nivel de emergencia y
        tipo de reporte (códigos de GET /api/v1/Reportes/catalogo).
        Los filtros se combinan. El tamaño de página está limitado a un
        máximo de 100 elementos.
        """)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerReportes(
        [FromQuery] EstadoReporte? estado,
        [FromQuery] NivelEmergencia? nivelEmergencia,
        [FromQuery] TipoReporte? tipo,
        [FromQuery] int pagina,
        [FromQuery] int tamanoPagina,
        CancellationToken cancellationToken)
    {
        int paginaNormalizada = pagina < 1 ? 1 : pagina;
        int tamanoPaginaNormalizado = tamanoPagina switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => tamanoPagina
        };

        PaginaDto<ReporteDto> reportes = await _obtenerReportesHandler.Handle(
            new ObtenerReportesQuery(estado, nivelEmergencia, tipo, paginaNormalizada, tamanoPaginaNormalizado),
            cancellationToken);

        return Ok(reportes);
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Obtener un reporte por ID")]
    [EndpointDescription("""
        Obtiene la información detallada de un reporte utilizando
        su identificador único.

        El identificador debe ser un UUID válido.
        """)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerReporte(
        Guid id,
        CancellationToken cancellationToken)
    {
        ReporteDto? reporte = await _obtenerReportePorIdHandler.Handle(new ObtenerReportePorIdQuery(id), cancellationToken);

        return reporte is null ? NotFound() : Ok(reporte);
    }

    [HttpPatch("{id:guid}/estado")]
    [EndpointSummary("Actualizar el estado de un reporte")]
    [EndpointDescription("""
        Actualiza el estado actual de un reporte.

        El reporte debe existir y el nuevo estado debe ser válido
        según las reglas del dominio.
        """)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActualizarEstado(
        Guid id,
        [FromBody] ActualizarEstadoRequest request,
        CancellationToken cancellationToken)
    {
        bool actualizado = await _actualizarEstadoReporteHandler.Handle(
            new ActualizarEstadoReporteCommand(id, request.Estado), cancellationToken);

        return actualizado ? Ok() : NotFound();
    }

    [HttpPatch("{id:guid}/nivel-emergencia")]
    [EndpointSummary("Actualizar el nivel de emergencia")]
    [EndpointDescription("""
        Actualiza el nivel de emergencia asignado a un reporte.

        El reporte debe existir y el nivel de emergencia debe ser válido.
        """)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActualizarNivelEmergencia(
        Guid id,
        [FromBody] ActualizarNivelEmergenciaRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.NivelEmergencia))
        {
            return BadRequest(new { message = "El nivel de emergencia indicado no es válido." });
        }

        bool actualizado = await _actualizarNivelEmergenciaReporteHandler.Handle(
            new ActualizarNivelEmergenciaReporteCommand(id, request.NivelEmergencia), cancellationToken);

        return actualizado ? Ok() : NotFound();
    }

    [HttpPatch("{id:guid}/asignacion")]
    [EndpointSummary("Asignar responsable a un reporte")]
    [EndpointDescription("""
        Asigna un usuario responsable a un reporte.

        El responsable debe corresponder a un usuario válido
        dentro del sistema.
        """)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AsignarResponsable(
        Guid id,
        [FromBody] AsignarResponsableRequest request,
        CancellationToken cancellationToken)
    {
        bool asignado = await _asignarResponsableReporteHandler.Handle(
            new AsignarResponsableReporteCommand(id, request.IdResponsable), cancellationToken);

        return asignado ? Ok() : NotFound();
    }

    [HttpPut("{id:guid}/rechazo")]
    [EndpointSummary("Rechazar un reporte")]
    [EndpointDescription("""
        Rechaza un reporte y registra el motivo del rechazo.

        El motivo del rechazo es obligatorio.
        """)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RechazarReporte(
        Guid id,
        [FromBody] RechazarReporteRequest request,
        CancellationToken cancellationToken)
    {
        bool rechazado = await _rechazarReporteHandler.Handle(
            new RechazarReporteCommand(id, request.Motivo), cancellationToken);

        return rechazado ? Ok() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    [EndpointSummary("Eliminar un reporte")]
    [EndpointDescription("""
        Elimina un reporte utilizando su identificador único.

        El reporte debe existir para poder ser eliminado.
        """)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarReporte(Guid id, CancellationToken cancellationToken)
    {
        bool eliminado = await _eliminarReporteHandler.Handle(new EliminarReporteCommand(id), cancellationToken);

        return eliminado ? NoContent() : NotFound();
    }
}
