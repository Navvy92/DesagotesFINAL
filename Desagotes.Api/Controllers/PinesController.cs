using System.Security.Cryptography;
using Desagotes.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Desagotes.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/admin")]
public class PinesController : ControllerBase
{
    private static readonly PasswordHasher<Camionero> Hasher = new();
    private readonly DesagotesContext _db;

    public PinesController(DesagotesContext db)
    {
        _db = db;
    }

    // Genera un PIN nuevo (o el primero). Se muestra una sola vez.
    [HttpPost("camioneros/{id:int}/pin")]
    public async Task<IActionResult> Generar(int id)
    {
        var c = await _db.Camioneros.FindAsync(id);
        if (c == null) return NotFound();

        var pin = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        c.PinHash = Hasher.HashPassword(c, pin);
        c.PinVersion++;                 // invalida las sesiones abiertas
        c.IntentosFallidos = 0;
        c.BloqueadoHasta = null;

        _db.AccesoLogs.Add(new AccesoLog
        {
            CamioneroId = id,
            Evento = $"PIN_GENERADO por {User.Identity!.Name}",
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            CreadoAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        Response.Headers.CacheControl = "no-store";
        return Ok(new { pin });
    }

    [HttpGet("accesos")]
    public async Task<IActionResult> Accesos(int? camioneroId)
    {
        var q = _db.AccesoLogs.AsQueryable();
        if (camioneroId.HasValue)
            q = q.Where(a => a.CamioneroId == camioneroId);

        var filas = await (
            from a in q
            join cc in _db.Camioneros on a.CamioneroId equals cc.Id into grupo
            from c in grupo.DefaultIfEmpty()
            orderby a.CreadoAt descending
            select new
            {
                a.Evento,
                Camionero = c != null ? c.Nombre : "(desconocido)",
                a.Ip,
                a.Dispositivo,
                a.CreadoAt
            }).Take(200).ToListAsync();

        return Ok(filas);
    }
}