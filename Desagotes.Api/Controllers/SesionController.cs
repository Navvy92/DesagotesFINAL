using System.Security.Claims;
using Desagotes.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Desagotes.Api.Controllers;

[ApiController]
[Route("api/sesion")]
public class SesionController : ControllerBase
{
    private const string Esquema = "Camionero";
    private const int MaxIntentos = 5;
    private static readonly TimeSpan Bloqueo = TimeSpan.FromMinutes(15);
    private static readonly PasswordHasher<Camionero> Hasher = new();

    private readonly DesagotesContext _db;

    public SesionController(DesagotesContext db)
    {
        _db = db;
    }

    public record LoginDto(int CamioneroId, string? Pin);

    [EnableRateLimiting("pin")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        const string generico = "Nombre o PIN incorrecto.";
        var ahora = DateTime.UtcNow;

        var c = await _db.Camioneros.FirstOrDefaultAsync(x => x.Id == dto.CamioneroId && x.Activo == true);
        if (c == null)
        {
            await Registrar(null, "LOGIN_FALLIDO");
            await Task.Delay(500);
            return Unauthorized(new { mensaje = generico });
        }

        if (c.BloqueadoHasta > ahora)
        {
            await Registrar(c.Id, "LOGIN_BLOQUEADO");
            return StatusCode(429, new { mensaje = "Demasiados intentos. Esperá unos minutos o pedile a administración un PIN nuevo." });
        }

        if (c.PinHash == null)
            return Unauthorized(new { mensaje = "Todavía no tenés PIN. Pedíselo a administración." });

        var ok = Hasher.VerifyHashedPassword(c, c.PinHash, dto.Pin ?? "") != PasswordVerificationResult.Failed;
        if (!ok)
        {
            // Contador atómico: varios intentos simultáneos no se pisan
            await _db.Camioneros.Where(x => x.Id == c.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IntentosFallidos, x => x.IntentosFallidos + 1));
            var intentos = await _db.Camioneros.Where(x => x.Id == c.Id)
                .Select(x => x.IntentosFallidos).FirstAsync();

            if (intentos >= MaxIntentos)
            {
                var hasta = ahora + Bloqueo;
                await _db.Camioneros.Where(x => x.Id == c.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IntentosFallidos, 0)
                        .SetProperty(x => x.BloqueadoHasta, hasta));
            }

            await Registrar(c.Id, "LOGIN_FALLIDO");
            await Task.Delay(500);
            return Unauthorized(new { mensaje = generico });
        }

        await _db.Camioneros.Where(x => x.Id == c.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IntentosFallidos, 0)
                .SetProperty(x => x.BloqueadoHasta, (DateTime?)null));

        var identidad = new ClaimsIdentity(new[]
        {
            new Claim("camionero_id", c.Id.ToString()),
            new Claim(ClaimTypes.Name, c.Nombre),
            new Claim("pin_version", c.PinVersion.ToString())
        }, Esquema);

        await HttpContext.SignInAsync(Esquema, new ClaimsPrincipal(identidad),
            new AuthenticationProperties { IsPersistent = true });

        await Registrar(c.Id, "LOGIN_OK");
        return Ok(new { id = c.Id, nombre = c.Nombre });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(Esquema);
        return Ok();
    }

    [Authorize(AuthenticationSchemes = Esquema)]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new { id = User.CamioneroId(), nombre = User.Identity!.Name });

    private async Task Registrar(int? camioneroId, string evento)
    {
        var ua = Request.Headers.UserAgent.ToString();
        _db.AccesoLogs.Add(new AccesoLog
        {
            CamioneroId = camioneroId,
            Evento = evento,
            Ip = IpCliente(),
            Dispositivo = ua.Length > 300 ? ua[..300] : ua,
            CreadoAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    // Informativa: detrás de un túnel o proxy viene en estos encabezados
    private string IpCliente() =>
        Request.Headers["CF-Connecting-IP"].FirstOrDefault()
        ?? Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
        ?? HttpContext.Connection.RemoteIpAddress?.ToString()
        ?? "";
}