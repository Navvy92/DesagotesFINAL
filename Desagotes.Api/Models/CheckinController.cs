using Desagotes.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Authorization;

namespace Desagotes.Api.Controllers;

[Authorize(AuthenticationSchemes = "Camionero")]
[ApiController]
[Route("api/[controller]")]

public class CheckinController : ControllerBase
{
    private readonly DesagotesContext _db;
    private readonly IWebHostEnvironment _env;

    public CheckinController(DesagotesContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromForm] CheckinRequest req)
    {

        var camioneroId = User.CamioneroId();

        // 1. Validaciones básicas
        if (req.Foto == null || req.Foto.Length == 0)
            return BadRequest(new { mensaje = "La foto es obligatoria." });

        if (req.Foto.Length > 10_000_000)
            return BadRequest(new { mensaje = "La foto supera los 10 MB." });

        if (!req.Foto.ContentType.StartsWith("image/"))
            return BadRequest(new { mensaje = "El archivo debe ser una imagen." });

        if (req.Lat < -90 || req.Lat > 90 || req.Lng < -180 || req.Lng > 180)
            return BadRequest(new { mensaje = "Ubicación inválida." });

        // 2. ¿Existen y están activos el camionero y el vehículo?
        var camioneroOk = await _db.Camioneros
            .AnyAsync(c => c.Id == camioneroId && c.Activo == true);
        var vehiculoOk = await _db.Vehiculos
            .AnyAsync(v => v.Id == req.VehiculoId && v.Activo == true);

        if (!camioneroOk || !vehiculoOk)
            return BadRequest(new { mensaje = "Camionero o vehículo inválido." });

        // 3. Guardar la foto en disco con un nombre propio (nunca el del usuario)
        var carpeta = Path.Combine(_env.ContentRootPath, "Fotos", "checkin");
        Directory.CreateDirectory(carpeta);

        var nombreArchivo = $"{Guid.NewGuid():N}.jpg";
        var rutaCompleta = Path.Combine(carpeta, nombreArchivo);

        await using (var stream = System.IO.File.Create(rutaCompleta))
        {
            await req.Foto.CopyToAsync(stream);
        }

        // 4. Crear la jornada. Estado y hora los pone la base (DEFAULT).
        var jornada = new Jornada
        {
            CamioneroId = req.CamioneroId,
            VehiculoId = req.VehiculoId,
            CheckinFoto = $"Fotos/checkin/{nombreArchivo}",
            CheckinLat = req.Lat,
            CheckinLng = req.Lng,
            CheckinPrecisionM = req.PrecisionM
        };

        _db.Jornada.Add(jornada);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: "23505" } pg)
        {
            // Violó un índice único: ya hay una jornada abierta
            System.IO.File.Delete(rutaCompleta);

            var mensaje = pg.ConstraintName == "uq_jornada_abierta_vehiculo"
                ? "Ese vehículo ya tiene una jornada abierta."
                : "Ya tenés una jornada abierta.";

            return Conflict(new { mensaje });
        }

        return Ok(new { jornada.Id, jornada.Estado, jornada.CheckinAt });
    }

}