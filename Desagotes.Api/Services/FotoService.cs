namespace Desagotes.Api.Services;

public class FotoService
{
    private const long MaxBytes = 10_000_000;
    private readonly string _raiz;

    public FotoService(IWebHostEnvironment env)
    {
        _raiz = env.ContentRootPath;
    }

    // Devuelve un mensaje de error, o null si la foto está bien
    public static string? Validar(IFormFile? foto, string nombreCampo)
    {
        if (foto == null || foto.Length == 0)
            return $"{nombreCampo}: la foto es obligatoria.";
        if (foto.Length > MaxBytes)
            return $"{nombreCampo}: la foto supera los 10 MB.";
        if (!foto.ContentType.StartsWith("image/"))
            return $"{nombreCampo}: el archivo debe ser una imagen.";
        return null;
    }

    // Guarda en disco y devuelve la ruta relativa para la base
    public async Task<string> GuardarAsync(IFormFile foto, string subcarpeta)
    {
        var carpeta = Path.Combine(_raiz, "Fotos", subcarpeta);
        Directory.CreateDirectory(carpeta);

        var nombre = $"{Guid.NewGuid():N}.jpg";
        await using var stream = File.Create(Path.Combine(carpeta, nombre));
        await foto.CopyToAsync(stream);

        return $"Fotos/{subcarpeta}/{nombre}";
    }

    public void Borrar(string rutaRelativa)
    {
        var completa = Path.Combine(_raiz, rutaRelativa);
        if (File.Exists(completa)) File.Delete(completa);
    }
}