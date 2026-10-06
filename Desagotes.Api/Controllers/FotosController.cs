using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
namespace Desagotes.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/fotos")]
public class FotosController : ControllerBase
{
    private readonly string _raiz;

    public FotosController(IWebHostEnvironment env)
    {
        _raiz = Path.GetFullPath(Path.Combine(env.ContentRootPath, "Fotos"));
    }

    [HttpGet("{**ruta}")]
    public IActionResult Get(string ruta)
    {
        var completa = Path.GetFullPath(Path.Combine(_raiz, ruta));

        // Que no se salga de la carpeta Fotos
        var dentro = completa.StartsWith(
            _raiz + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

        if (!dentro || !System.IO.File.Exists(completa))
            return NotFound();

        return PhysicalFile(completa, "image/jpeg");
    }
}