using Desagotes.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);
Desagotes.Api.Reglas.PlazoHorasRemitos =
    builder.Configuration.GetValue<int>("Reglas:PlazoHorasRemitos", 48);
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddScoped<Desagotes.Api.Services.FotoService>();
builder.Services.AddDbContext<DesagotesContext>(opciones =>
    opciones.UseNpgsql(connectionString: builder.Configuration.GetConnectionString("Desagotes")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "desagotes.auth";
        o.Cookie.HttpOnly = true;                    // JavaScript no puede leerla
        o.Cookie.SameSite = SameSiteMode.Strict;     // no se envía desde otros sitios
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;  // solo por HTTPS
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        // Para una API: responder 401/403 en vez de redirigir a una página de login
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };


    })
        .AddCookie("Camionero", o =>
        {
            o.Cookie.Name = "desagotes.camionero";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.ExpireTimeSpan = TimeSpan.FromDays(30);
            o.SlidingExpiration = true;
            o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };

            // En cada pedido: ¿el camionero sigue activo y el PIN es el mismo de cuando entró?
            o.Events.OnValidatePrincipal = async ctx =>
            {
                var db = ctx.HttpContext.RequestServices.GetRequiredService<DesagotesContext>();
                var ok = int.TryParse(ctx.Principal?.FindFirst("camionero_id")?.Value, out var id)
                      && int.TryParse(ctx.Principal?.FindFirst("pin_version")?.Value, out var ver)
                      && await db.Camioneros.AnyAsync(c => c.Id == id && c.Activo == true && c.PinVersion == ver);
                if (!ok)
                {
                    ctx.RejectPrincipal();
                    await ctx.HttpContext.SignOutAsync("Camionero");
                }
            };
        }); ;

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    o.AddPolicy("pin", ctx => RateLimitPartition.GetFixedWindowLimiter(
    ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
    _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 30,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    }));
});
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DesagotesContext>();
    if (!db.Admins.Any())
    {
        var usuario = app.Configuration["AdminInicial:Usuario"];
        var clave = app.Configuration["AdminInicial:Password"];
        if (!string.IsNullOrWhiteSpace(usuario) && !string.IsNullOrWhiteSpace(clave))
        {
            var nuevo = new Desagotes.Api.Models.Admin { Usuario = usuario.Trim().ToLowerInvariant() };
            nuevo.PasswordHash = new PasswordHasher<Desagotes.Api.Models.Admin>().HashPassword(nuevo, clave);
            db.Admins.Add(nuevo);
            db.SaveChanges();
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();
