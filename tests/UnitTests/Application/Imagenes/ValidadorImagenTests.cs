using Application.Imagenes;

namespace UnitTests.Application.Imagenes;

public class ValidadorImagenTests
{
    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01];
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    public static readonly byte[] Webp = [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBP"u8, .. "VP8 "u8];

    public static TheoryData<byte[], string, string> FormatosValidos => new()
    {
        { Jpeg, "image/jpeg", ".jpg" },
        { Png, "image/png", ".png" },
    };

    [Theory]
    [MemberData(nameof(FormatosValidos))]
    public async Task ValidarAsync_DetectaElFormatoPorElContenido(byte[] contenido, string tipoContenido, string extension)
    {
        FormatoImagen formato = await ValidadorImagen.ValidarAsync(new MemoryStream(contenido), contenido.Length, CancellationToken.None);

        Assert.Equal(tipoContenido, formato.TipoContenido);
        Assert.Equal(extension, formato.Extension);
    }

    [Fact]
    public async Task ValidarAsync_DejaElStreamEnSuPosicionInicial()
    {
        MemoryStream stream = new(Png);

        await ValidadorImagen.ValidarAsync(stream, Png.Length, CancellationToken.None);

        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task ValidarAsync_RechazaUnArchivoQueNoEsImagen()
    {
        byte[] pdf = "%PDF-1.7\n%"u8.ToArray();

        await Assert.ThrowsAsync<ImagenInvalidaException>(
            () => ValidadorImagen.ValidarAsync(new MemoryStream(pdf), pdf.Length, CancellationToken.None));
    }

    [Fact]
    public async Task ValidarAsync_RechazaUnWebp()
    {
        // Multimedia solo acepta JPEG y PNG: un WebP válido también se rechaza aquí.
        await Assert.ThrowsAsync<ImagenInvalidaException>(
            () => ValidadorImagen.ValidarAsync(new MemoryStream(Webp), Webp.Length, CancellationToken.None));
    }

    [Fact]
    public async Task ValidarAsync_RechazaUnArchivoVacio()
    {
        await Assert.ThrowsAsync<ImagenInvalidaException>(
            () => ValidadorImagen.ValidarAsync(new MemoryStream(), 0, CancellationToken.None));
    }

    [Fact]
    public async Task ValidarAsync_RechazaUnArchivoMayorAlMaximo()
    {
        await Assert.ThrowsAsync<ImagenInvalidaException>(
            () => ValidadorImagen.ValidarAsync(new MemoryStream(Jpeg), ValidadorImagen.TamanoMaximoBytes + 1, CancellationToken.None));
    }
}
