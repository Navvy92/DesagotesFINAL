using Desagotes.Api.Models;
using Desagotes.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.RegularExpressions;
namespace Desagotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RemitosController : ControllerBase
{
    // Busca por remito (talonario + número) o por pedido
    [Authorize]
    [HttpGet("buscar")]

    public async Task<IActionResult> Buscar(int? talonario, int? nroRemito, string? pedido)
    {
        var tieneRemito = talonario.HasValue && nroRemito.HasValue;
        var tienePedido = !string.IsNullOrWhiteSpace(pedido);

        if (!tieneRemito && !tienePedido)
            return BadRequest(new { mensaje = "Indicá talonario y número de remito, o el pedido." });

        var q = from r in _db.Remitos
                join j in _db.Jornada on r.JornadaId equals j.Id
                join c in _db.Camioneros on j.CamioneroId equals c.Id
                join v in _db.Vehiculos on j.VehiculoId equals v.Id
                select new { r, j, c, v };

        if (tieneRemito)
            q = q.Where(x => x.r.Talonario == talonario && x.r.NroRemito == nroRemito);

        if (tienePedido)
        {
            var p = pedido!.Trim();
            q = q.Where(x => x.r.NroPedido == p);
        }

        var filas = await q
            .OrderByDescending(x => x.r.CargadoAt)
            .Take(50)
            .Select(x => new
            {
                x.r.Id,
                x.r.Talonario,
                x.r.NroRemito,
                x.r.Cliente,
                x.r.NroPedido,
                x.r.FotoRemito,
                x.r.FotoCamara,
                Fecha = x.j.CheckinAt,
                Camionero = x.c.Nombre,
                x.v.Patente
            })
            .ToListAsync();

        // Convertimos la ruta en disco en una URL que el navegador pueda abrir
        var resultado = filas.Select(f => new
        {
            f.Id,
            Remito = $"{f.Talonario}-{f.NroRemito}",
            f.Cliente,
            f.NroPedido,
            f.Fecha,
            f.Camionero,
            f.Patente,
            UrlFotoRemito = "/api/" + f.FotoRemito,
            UrlFotoCamara = "/api/" + f.FotoCamara
        });

        return Ok(resultado);
    }
    private readonly DesagotesContext _db;
    private readonly FotoService _fotos;

    public RemitosController(DesagotesContext db, FotoService fotos)
    {
        _db = db;
        _fotos = fotos;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromForm] RemitoRequest req)
    {
        // 1. Fotos
        var error = FotoService.Validar(req.FotoRemito, "Foto del remito")
                 ?? FotoService.Validar(req.FotoCamara, "Foto de la cámara");
        if (error != null)
            return BadRequest(new { mensaje = error });

        // 2. Datos del remito
        if (req.Talonario <= 0 || req.NroRemito <= 0)
            return BadRequest(new { mensaje = "Talonario y número de remito inválidos." });

        var cliente = req.Cliente?.Trim().ToUpperInvariant();
        if (cliente != "TELECOM" && cliente != "TELEFONICA")
            return BadRequest(new { mensaje = "Cliente inválido." });

        // 3. Pedido: opcional (puede estar pendiente), pero si viene debe cumplir el formato
        var pedido = string.IsNullOrWhiteSpace(req.NroPedido) ? null : req.NroPedido.Trim();
        if (pedido != null)
        {
            var patron = cliente == "TELEFONICA" ? @"^\d{6}$" : @"^\d{4,5}$";
            if (!Regex.IsMatch(pedido, patron))
                return BadRequest(new
                {
                    mensaje = cliente == "TELEFONICA"
                    ? "El pedido de Telefónica tiene 6 dígitos."
                    : "El pedido de Telecom tiene 4 o 5 dígitos."
                });
        }

        // 4. La jornada tiene que existir, estar cerrada y dentro del plazo
        var jornada = await _db.Jornada.FindAsync(req.JornadaId);
        if (jornada == null)
            return NotFound(new { mensaje = "Jornada inexistente." });

        if (jornada.Estado != "CERRADA")
            return Conflict(new { mensaje = "La jornada no admite más remitos." });

        if (Reglas.HayPlazo &&
    DateTime.UtcNow - jornada.CheckoutAt!.Value > TimeSpan.FromHours(Reglas.PlazoHorasRemitos))
            return Conflict(new { mensaje = $"Pasó el plazo de {Reglas.PlazoHorasRemitos} hs para cargar remitos." });

        // 5. No pasarse de la cantidad declarada
        var cargados = await _db.Remitos.CountAsync(r => r.JornadaId == jornada.Id);
        if (cargados >= jornada.RemitosDeclarados)
            return Conflict(new { mensaje = "Ya cargaste todos los remitos declarados." });

        // 6. Guardar fotos e insertar
        var rutaRemito = await _fotos.GuardarAsync(req.FotoRemito, "remitos");
        var rutaCamara = await _fotos.GuardarAsync(req.FotoCamara, "camaras");

        _db.Remitos.Add(new Remito
        {
            JornadaId = jornada.Id,
            Talonario = req.Talonario,
            NroRemito = req.NroRemito,
            Cliente = cliente,
            NroPedido = pedido,
            FotoRemito = rutaRemito,
            FotoCamara = rutaCamara
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            _fotos.Borrar(rutaRemito);
            _fotos.Borrar(rutaCamara);
            return Conflict(new { mensaje = $"El remito {req.Talonario}-{req.NroRemito} ya fue cargado." });
        }

        // 7. ¿Se completaron? Entonces el día queda validado
        cargados++;
        var validada = cargados == jornada.RemitosDeclarados;
        if (validada)
        {
            await _db.Jornada
                .Where(j => j.Id == jornada.Id && j.Estado == "CERRADA")
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Estado, "VALIDADA"));
        }

        return Ok(new { cargados, declarados = jornada.RemitosDeclarados, validada });
    }

    [Authorize]
    [HttpGet("listado")]
    public async Task<IActionResult> Listado()
    {
        var filas = await (
            from r in _db.Remitos
            join j in _db.Jornada on r.JornadaId equals j.Id
            join c in _db.Camioneros on j.CamioneroId equals c.Id
            orderby r.CargadoAt descending
            select new
            {
                r.Talonario,
                r.NroRemito,
                Camionero = c.Nombre,
                Fecha = j.CheckinAt
            }).Take(100).ToListAsync();

        return Ok(filas.Select(f => new
        {
            Remito = $"{f.Talonario}-{f.NroRemito}",
            f.Talonario,
            f.NroRemito,
            f.Camionero,
            f.Fecha
        }));
    }
}