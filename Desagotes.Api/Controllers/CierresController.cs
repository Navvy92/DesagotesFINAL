using Desagotes.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Desagotes.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/jornadas")]
public class CierresController : ControllerBase
{
    private readonly DesagotesContext _db;

    public CierresController(DesagotesContext db)
    {
        _db = db;
    }

    public record CierreDto(int RemitosDeclarados, DateTimeOffset? HoraSalida, string? Nota);
    public record ValidarDto(string? Nota);

    // Jornadas sin check-out (las "olvidadas")
    [HttpGet("abiertas")]
    public async Task<IActionResult> Abiertas()
    {
        var filas = await (
            from j in _db.Jornada
            join c in _db.Camioneros on j.CamioneroId equals c.Id
            join v in _db.Vehiculos on j.VehiculoId equals v.Id
            where j.Estado == "ABIERTA"
            orderby j.CheckinAt
            select new { j.Id, Camionero = c.Nombre, v.Patente, j.CheckinAt }).ToListAsync();

        var ahora = DateTime.UtcNow;
        return Ok(filas.Select(f => new
        {
            f.Id,
            f.Camionero,
            f.Patente,
            f.CheckinAt,
            Horas = Math.Round((ahora - f.CheckinAt).TotalHours, 1)
        }));
    }

    // Cierre administrativo de una jornada abierta
    [HttpPost("{id:int}/cerrar")]
    public async Task<IActionResult> Cerrar(int id, [FromBody] CierreDto dto)
    {
        if (dto.RemitosDeclarados < 0 || dto.RemitosDeclarados > 50)
            return BadRequest(new { mensaje = "Cantidad de remitos inválida." });

        var j = await _db.Jornada.FindAsync(id);
        if (j == null) return NotFound();
        if (j.Estado != "ABIERTA")
            return Conflict(new { mensaje = "La jornada ya no está abierta." });

        var ahora = DateTime.UtcNow;
        var salida = dto.HoraSalida?.UtcDateTime ?? ahora;
        if (salida < j.CheckinAt || salida > ahora.AddMinutes(5))
            return BadRequest(new { mensaje = "La hora de salida no es válida." });

        var cargados = await _db.Remitos.CountAsync(r => r.JornadaId == id);
        var estado = cargados >= dto.RemitosDeclarados ? "VALIDADA" : "CERRADA";
        var admin = User.Identity!.Name;
        var nota = string.IsNullOrWhiteSpace(dto.Nota) ? null : dto.Nota.Trim();

        var filas = await _db.Jornada
            .Where(x => x.Id == id && x.Estado == "ABIERTA")
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Estado, estado)
                .SetProperty(x => x.CheckoutAt, salida)
                .SetProperty(x => x.RemitosDeclarados, dto.RemitosDeclarados)
                .SetProperty(x => x.CierreAdmin, true)
                .SetProperty(x => x.CierreAdminPor, admin)
                .SetProperty(x => x.CierreAdminAt, ahora)
                .SetProperty(x => x.NotaAdmin, nota));

        if (filas == 0)
            return Conflict(new { mensaje = "La jornada ya fue cerrada." });

        return Ok(new { estado });
    }

    // Validación manual de una jornada con remitos faltantes (exige motivo)
    [HttpPost("{id:int}/validar")]
    public async Task<IActionResult> Validar(int id, [FromBody] ValidarDto dto)
    {
        var nota = dto.Nota?.Trim();
        if (string.IsNullOrWhiteSpace(nota))
            return BadRequest(new { mensaje = "Escribí el motivo de la validación manual." });

        var admin = User.Identity!.Name;
        var filas = await _db.Jornada
            .Where(x => x.Id == id && x.Estado == "CERRADA")
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Estado, "VALIDADA")
                .SetProperty(x => x.CierreAdmin, true)
                .SetProperty(x => x.CierreAdminPor, admin)
                .SetProperty(x => x.CierreAdminAt, DateTime.UtcNow)
                .SetProperty(x => x.NotaAdmin, nota));

        return filas == 0
            ? Conflict(new { mensaje = "La jornada no está pendiente de validación." })
            : Ok();
    }

    // Remitos de una jornada, con fotos, para que el admin los revise
    [HttpGet("{id:int}/remitos")]
    public async Task<IActionResult> Remitos(int id)
    {
        var lista = await _db.Remitos
            .Where(r => r.JornadaId == id)
            .OrderBy(r => r.CargadoAt)
            .Select(r => new { r.Talonario, r.NroRemito, r.Cliente, r.NroPedido, r.FotoRemito, r.FotoCamara })
            .ToListAsync();

        return Ok(lista.Select(r => new
        {
            Remito = $"{r.Talonario}-{r.NroRemito}",
            r.Cliente,
            r.NroPedido,
            UrlFotoRemito = "/api/" + r.FotoRemito,
            UrlFotoCamara = "/api/" + r.FotoCamara
        }));
    }
}