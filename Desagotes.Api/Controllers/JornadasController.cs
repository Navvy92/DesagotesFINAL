using Desagotes.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Desagotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JornadasController : ControllerBase
{
    private readonly DesagotesContext _db;

    public JornadasController(DesagotesContext db)
    {
        _db = db;
    }

    // ABIERTO: historial del camionero (últimos 30 días)
    [Authorize(AuthenticationSchemes = "Camionero")]
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var camioneroId = User.CamioneroId();
    // ... el resto queda igual
    
        var desde = DateTime.UtcNow.AddDays(-30);

        var jornadas = await _db.Jornada
            .Where(j => j.CamioneroId == camioneroId && j.CheckinAt >= desde)
            .OrderByDescending(j => j.CheckinAt)
            .Select(j => new
            {
                j.Id,
                j.Estado,
                j.CheckinAt,
                j.CheckoutAt,
                j.RemitosDeclarados,
                Cargados = _db.Remitos.Count(r => r.JornadaId == j.Id),
                Patente = _db.Vehiculos
                    .Where(v => v.Id == j.VehiculoId)
                    .Select(v => v.Patente)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var ahora = DateTime.UtcNow;
        var resultado = jornadas.Select(j =>
        {
            DateTime? limite = Reglas.HayPlazo
                ? j.CheckoutAt?.AddHours(Reglas.PlazoHorasRemitos)
                : null;
            return new
            {
                j.Id,
                j.Estado,
                j.Patente,
                j.CheckinAt,
                j.CheckoutAt,
                Declarados = j.RemitosDeclarados,
                j.Cargados,
                Faltan = (j.RemitosDeclarados ?? 0) - j.Cargados,
                LimiteCarga = limite,
                Vencida = j.Estado == "CERRADA" && limite != null && limite < ahora
            };
        });

        return Ok(resultado);
    }

    // ABIERTO: remitos ya cargados de una jornada
    [HttpGet("{id:int}/remitos")]


    // PROTEGIDO: todas las jornadas cerradas con remitos faltantes
    [Authorize]
    [HttpGet("pendientes")]
    public async Task<IActionResult> Pendientes()
    {
        var filas = await (
            from j in _db.Jornada
            join c in _db.Camioneros on j.CamioneroId equals c.Id
            join v in _db.Vehiculos on j.VehiculoId equals v.Id
            where j.Estado == "CERRADA"
            select new
            {
                j.Id,
                Camionero = c.Nombre,
                v.Patente,
                j.CheckoutAt,
                Declarados = j.RemitosDeclarados,
                Cargados = _db.Remitos.Count(r => r.JornadaId == j.Id)
            }).ToListAsync();

        var ahora = DateTime.UtcNow;
        var resultado = filas
            .Where(f => f.Cargados < (f.Declarados ?? 0))
            .Select(f =>
            {
                DateTime? limite = Reglas.HayPlazo
                    ? f.CheckoutAt!.Value.AddHours(Reglas.PlazoHorasRemitos)
                    : null;
                return new
                {
                    f.Id,
                    f.Camionero,
                    f.Patente,
                    f.CheckoutAt,
                    f.Declarados,
                    f.Cargados,
                    Faltan = (f.Declarados ?? 0) - f.Cargados,
                    HorasRestantes = limite.HasValue
                        ? Math.Round((limite.Value - ahora).TotalHours, 1) : (double?)null,
                    Vencida = limite.HasValue && limite.Value < ahora
                };
            })
            .OrderBy(x => x.HorasRestantes ?? double.MaxValue)
            .ThenBy(x => x.CheckoutAt);

        return Ok(resultado);
    }
}