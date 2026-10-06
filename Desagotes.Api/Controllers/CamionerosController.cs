using Desagotes.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Desagotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CamionerosController : ControllerBase
{
    private readonly DesagotesContext _db;

    public CamionerosController(DesagotesContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var lista = await _db.Camioneros
            .Where(c => c.Activo == true)
            .OrderBy(c => c.Nombre)
            .Select(c => new { c.Id, c.Nombre })
            .ToListAsync();

        return Ok(lista);
    }
}