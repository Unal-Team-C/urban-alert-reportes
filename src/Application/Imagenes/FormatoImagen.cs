namespace Application.Imagenes;

public sealed record FormatoImagen(string TipoContenido, string Extension)
{
    public static readonly FormatoImagen Jpeg = new("image/jpeg", ".jpg");
    public static readonly FormatoImagen Png = new("image/png", ".png");
}
