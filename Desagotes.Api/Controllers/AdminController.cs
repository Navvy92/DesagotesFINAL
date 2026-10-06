using System.Text.RegularExpressions;
using Desagotes.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Desagotes.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly DesagotesContext _db;

    public AdminController(DesagotesContext db)
    {
        _db = db;
    }

    public record CamioneroDto(string Nombre);
    public record VehiculoDto(string Patente);
    public record ActivoDto(bool Activo);

    // Acepta AAA123 (vieja) y AB123CD (Mercosur)
    private static readonly Regex PatenteValida =
        new(@"^([A-Z]{3}\d{3}|[A-Z]{2}\d{3}[A-Z]{2})$", RegexOptions.Compiled);

    private static string NormalizarPatente(string? p) =>
        Regex.Replace((p ?? "").ToUpperInvariant(), @"[\s-]", "");

    // ---------- Camioneros ----------
    [HttpGet("camioneros")]
    public async Task<IActionResult> ListarCamioneros() =>
        Ok(await _db.Camioneros.OrderBy(c => c.Nombre)
            .Select(c => new { c.Id, c.Nombre, c.Activo }).ToListAsync());

    [HttpPost("camioneros")]
    public async Task<IActionResult> NuevoCamionero([FromBody] CamioneroDto dto)
    {
        var nombre = dto.Nombre?.Trim();
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 100)
            return BadRequest(new { mensaje = "Nombre inválido." });

        if (await _db.Camioneros.AnyAsync(c => c.Nombre.ToLower() == nombre.ToLower()))
            return Conflict(new { mensaje = "Ya existe un camionero con ese nombre." });

        var nuevo = new Camionero { Nombre = nombre };
        _db.Camioneros.Add(nuevo);
        await _db.SaveChangesAsync();
        return Ok(new { nuevo.Id, nuevo.Nombre, nuevo.Activo });
    }

    [HttpPut("camioneros/{id:int}")]
    public async Task<IActionResult> EditarCamionero(int id, [FromBody] CamioneroDto dto)
    {
        var nombre = dto.Nombre?.Trim();
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 100)
            return BadRequest(new { mensaje = "Nombre inválido." });

        var c = await _db.Camioneros.FindAsync(id);
        if (c == null) return NotFound();

        if (await _db.Camioneros.AnyAsync(x => x.Id != id && x.Nombre.ToLower() == nombre.ToLower()))
            return Conflict(new { mensaje = "Ya existe un camionero con ese nombre." });

        c.Nombre = nombre;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPatch("camioneros/{id:int}/activo")]
    public async Task<IActionResult> ActivoCamionero(int id, [FromBody] ActivoDto dto)
    {
        var c = await _db.Camioneros.FindAsync(id);
        if (c == null) return NotFound();

        if (!dto.Activo && await _db.Jornada.AnyAsync(j => j.CamioneroId == id && j.Estado == "ABIERTA"))
            return Conflict(new { mensaje = "Tiene una jornada abierta. Esperá a que haga el check-out." });

        c.Activo = dto.Activo;
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ---------- Vehículos ----------
    [HttpGet("vehiculos")]
    public async Task<IActionResult> ListarVehiculos() =>
        Ok(await _db.Vehiculos.OrderBy(v => v.Patente)
            .Select(v => new { v.Id, v.Patente, v.Activo }).ToListAsync());

    [HttpPost("vehiculos")]
    public async Task<IActionResult> NuevoVehiculo([FromBody] VehiculoDto dto)
    {
        var patente = NormalizarPatente(dto.Patente);
        if (!PatenteValida.IsMatch(patente))
            return BadRequest(new { mensaje = "Patente inválida (ej: AB123CD o ABC123)." });

        var nuevo = new Vehiculo { Patente = patente };
        _db.Vehiculos.Add(nuevo);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return Conflict(new { mensaje = "Esa patente ya existe." });
        }
        return Ok(new { nuevo.Id, nuevo.Patente, nuevo.Activo });
    }

    [HttpPut("vehiculos/{id:int}")]
    public async Task<IActionResult> EditarVehiculo(int id, [FromBody] VehiculoDto dto)
    {
        var patente = NormalizarPatente(dto.Patente);
        if (!PatenteValida.IsMatch(patente))
            return BadRequest(new { mensaje = "Patente inválida (ej: AB123CD o ABC123)." });

        var v = await _db.Vehiculos.FindAsync(id);
        if (v == null) return NotFound();

        v.Patente = patente;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return Conflict(new { mensaje = "Esa patente ya existe." });
        }
        return Ok();
    }

    [HttpPatch("vehiculos/{id:int}/activo")]
    public async Task<IActionResult> ActivoVehiculo(int id, [FromBody] ActivoDto dto)
    {
        var v = await _db.Vehiculos.FindAsync(id);
        if (v == null) return NotFound();

        if (!dto.Activo && await _db.Jornada.AnyAsync(j => j.VehiculoId == id && j.Estado == "ABIERTA"))
            return Conflict(new { mensaje = "Ese camión tiene una jornada abierta." });

        v.Activo = dto.Activo;
        await _db.SaveChangesAsync();
        return Ok();
    }
}