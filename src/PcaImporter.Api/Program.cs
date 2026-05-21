using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using PcaImporter.Api.Hubs;
using PcaImporter.Application.Validacao;
using PcaImporter.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(opt =>
{
    opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// Persiste as chaves de Data Protection num diretório do volume.
// Sem isso, cookies de auth seriam invalidados a cada restart do container.
var caminhoChavesDP = builder.Configuration["DataProtection:KeysPath"]
    ?? Environment.GetEnvironmentVariable("DP_KEYS_PATH")
    ?? (OperatingSystem.IsLinux() ? "/data/dp-keys" : null);
if (!string.IsNullOrWhiteSpace(caminhoChavesDP))
{
    try { Directory.CreateDirectory(caminhoChavesDP); }
    catch { /* mantém ephemeral se não conseguir criar */ }

    if (Directory.Exists(caminhoChavesDP))
    {
        builder.Services
            .AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(caminhoChavesDP))
            .SetApplicationName("PcaImporter");
    }
}

// Cookie-based auth — sem Identity (rolamos hash próprio).
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.Cookie.Name = "pca.auth";
        opt.Cookie.HttpOnly = true;
        opt.Cookie.SameSite = SameSiteMode.Lax;
        opt.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        opt.SlidingExpiration = true;
        opt.ExpireTimeSpan = TimeSpan.FromHours(8);
        // Para chamadas /api e /hubs: 401/403 em vez de redirect HTML
        opt.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api") || ctx.Request.Path.StartsWithSegments("/hubs"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
        opt.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api") || ctx.Request.Path.StartsWithSegments("/hubs"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

builder.Services.AdicionarInfrastructure(builder.Configuration);

builder.Services.AddHostedService<TokenHubBroadcaster>();
builder.Services.AddSingleton<IEventosListaValidacao, ListaValidacaoBroadcaster>();

var app = builder.Build();

await app.Services.AplicarMigracoesAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// Hub é read-only (apenas envia eventos). Qualquer usuário autenticado pode ouvir.
app.MapHub<TokenHub>(TokenHub.Caminho).RequireAuthorization();
app.MapHub<ListaValidacaoHub>(ListaValidacaoHub.Caminho).RequireAuthorization();

app.MapFallbackToFile("index.html");

app.Run();
