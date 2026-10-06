using System.Security.Claims;
using Desagotes.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace Desagotes.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private static readonly PasswordHasher<Admin> Hasher = new();
    private readonly DesagotesContext _db;

    public AuthController(DesagotesContext db)
    {
        _db = db;
    }

    public record LoginRequest(string Usuario, string Password);

    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {


        var nombre = req.Usuario.Trim().ToLowerInvariant();
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Usuario == nombre);

        var ok = admin != null &&
                 Hasher.VerifyHashedPassword(admin, admin.PasswordHash, req.Password)
                     != PasswordVerificationResult.Failed;

        if (!ok)
        {
            await Task.Delay(500);   // frena un poco los intentos automáticos
            // Mismo mensaje para "usuario inexistente" y "clave mala": no revela cuál falló
            return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos." });
        }

        var identidad = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, admin!.Usuario) },
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identidad));

        return Ok(new { usuario = admin.Usuario });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok();
    }

    // La pantalla lo usa para saber si ya hay una sesión abierta
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new { usuario = User.Identity!.Name });
}