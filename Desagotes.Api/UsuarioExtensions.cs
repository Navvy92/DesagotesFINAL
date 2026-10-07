using System.Security.Claims;

namespace Desagotes.Api;

public static class UsuarioExtensions
{
    public static int CamioneroId(this ClaimsPrincipal u) =>
        int.Parse(u.FindFirst("camionero_id")?.Value ?? "0");
}