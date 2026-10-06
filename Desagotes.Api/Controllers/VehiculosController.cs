using Desagotes.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Desagotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiculosController : ControllerBase
{
    private readonly DesagotesContext _db;

    public VehiculosController(DesagotesContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var lista = await _db.Vehiculos
            .Where(v => v.Activo == true)
            .OrderBy(v => v.Patente)
            .Select(v => new { v.Id, v.Patente })
            .ToListAsync();

        return Ok(lista);
    }
}