using Desagotes.Api.Models;
using Desagotes.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Desagotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CheckoutController : ControllerBase
{
    private readonly DesagotesContext _db;
    private readonly FotoService _fotos;

    public CheckoutController(DesagotesContext db, FotoService fotos)
    {
        _db = db;
        _fotos = fotos;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromForm] CheckoutRequest req)
    {
        var errorFoto = FotoService.Validar(req.Foto, "Foto");
        if (errorFoto != null)
            return BadRequest(new { mensaje = errorFoto });

        if (req.RemitosDeclarados < 0 || req.RemitosDeclarados > 50)
            return BadRequest(new { mensaje = "Cantidad de remitos inválida." });

        // Buscar la jornada abierta de este camionero
        var jornada = await _db.Jornada
            .Where(j => j.CamioneroId == req.CamioneroId && j.Estado == "ABIERTA")
            .FirstOrDefaultAsync();

        if (jornada == null)
            return Conflict(new { mensaje = "No tenés una jornada abierta." });

        var ruta = await _fotos.GuardarAsync(req.Foto, "checkout");

        // Si declaró 0 remitos, no queda nada por cargar: se valida directo
        var nuevoEstado = req.RemitosDeclarados == 0 ? "VALIDADA" : "CERRADA";

        // Un solo UPDATE condicionado al estado: si dos pedidos llegan a la vez,
        // solo uno encuentra la jornada todavía 'ABIERTA'
        var filas = await _db.Jornada
            .Where(j => j.Id == jornada.Id && j.Estado == "ABIERTA")
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Estado, nuevoEstado)
                .SetProperty(j => j.CheckoutAt, DateTime.UtcNow)
                .SetProperty(j => j.CheckoutFoto, ruta)
                .SetProperty(j => j.RemitosDeclarados, req.RemitosDeclarados));

        if (filas == 0)
        {
            _fotos.Borrar(ruta);
            return Conflict(new { mensaje = "La jornada ya fue cerrada." });
        }

        return Ok(new { jornadaId = jornada.Id, estado = nuevoEstado });
    }
}