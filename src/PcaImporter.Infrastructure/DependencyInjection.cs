using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PcaImporter.Application.Catalogo;
using PcaImporter.Application.Compras.Catalogo;
using PcaImporter.Application.Compras.Dfd;
using PcaImporter.Application.Importacao;
using PcaImporter.Application.Logs;
using PcaImporter.Application.Token;
using PcaImporter.Application.Usuarios;
using PcaImporter.Infrastructure.Catalogo;
using PcaImporter.Infrastructure.Compras;
using PcaImporter.Infrastructure.Compras.Catalogo;
using PcaImporter.Infrastructure.Compras.Dfd;
using PcaImporter.Infrastructure.Importacao;
using PcaImporter.Infrastructure.Logs;
using PcaImporter.Infrastructure.Persistencia;
using PcaImporter.Infrastructure.Usuarios;

namespace PcaImporter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<ComprasGovOptions>(config.GetSection(ComprasGovOptions.SecaoConfig));

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IRegistroLogs, RegistroLogsMemoria>();

        var conexaoSqlite = config.GetConnectionString("Sqlite") ?? "Data Source=pca-importer.db";
        services.AddDbContextFactory<PcaDbContext>(opt => opt.UseSqlite(conexaoSqlite));

        services.AddSingleton<IRepositorioTokenSessao, RepositorioTokenSessao>();
        services.AddSingleton<IRepositorioHistoricoImportacao, RepositorioHistoricoImportacao>();
        services.AddSingleton<IRepositorioCatalogoMaterial, RepositorioCatalogoMaterial>();
        services.AddSingleton<IServicoUsuarios, ServicoUsuarios>();
        services.AddHostedService<BootstrapAdminPadrao>();

        // Base de referência de preços/quantitativos (somente leitura).
        var caminhoRefDb = config["PrecosReferencia:CaminhoDb"]
            ?? Environment.GetEnvironmentVariable("PCA_REF_DB")
            ?? Path.Combine(AppContext.BaseDirectory, "comprasgov.db");
        services.AddSingleton<IRepositorioPrecosReferencia>(sp =>
            new RepositorioPrecosReferencia(
                caminhoRefDb,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RepositorioPrecosReferencia>>()));

        services.AddHttpClient<IComprasGovTokenClient, ComprasGovTokenClient>((sp, http) =>
        {
            var opcoes = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComprasGovOptions>>().Value;
            http.BaseAddress = new Uri(opcoes.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(opcoes.Token.TimeoutHttpSegundos);
        });

        services.AddSingleton<GerenciadorTokenSessao>();
        services.AddSingleton<IGerenciadorTokenSessao>(sp => sp.GetRequiredService<GerenciadorTokenSessao>());

        services.AddHostedService<KeepAliveTokenWorker>();
        services.AddHostedService<BootstrapTokenAoIniciar>();

        services.AddHttpClient<IComprasGovDfdClient, ComprasGovDfdClient>((sp, http) =>
        {
            var opcoes = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComprasGovOptions>>().Value;
            http.BaseAddress = new Uri(opcoes.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(opcoes.Token.TimeoutHttpSegundos);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        });

        services.AddSingleton<IRegistroDfdAtual, RegistroDfdAtual>();

        services.AddHttpClient<IComprasGovCatalogoClient, ComprasGovCatalogoClient>((sp, http) =>
        {
            var opcoes = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComprasGovOptions>>().Value;
            http.BaseAddress = new Uri(opcoes.BaseUrlDadosAbertos);
            http.Timeout = TimeSpan.FromSeconds(opcoes.Token.TimeoutHttpSegundos);
        });

        services.AddSingleton<RegistroImportacoes>();
        services.AddSingleton<IServicoImportacao, ServicoImportacao>();
        services.AddHttpClient("google-sheets", http =>
        {
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("PcaImporter/1.0");
        });

        services.AddHttpClient("dadosabertos", http =>
        {
            http.BaseAddress = new Uri("https://dadosabertos.compras.gov.br/");
            http.Timeout = TimeSpan.FromSeconds(60);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("PcaImporter/1.0");
            http.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        });
        services.AddSingleton<IServicoSincronizacaoCatalogo, ServicoSincronizacaoCatalogo>();

        return services;
    }

    public static async Task AplicarMigracoesAsync(this IServiceProvider sp, CancellationToken ct = default)
    {
        var factory = sp.GetRequiredService<IDbContextFactory<PcaDbContext>>();
        await using var ctx = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await ctx.Database.MigrateAsync(ct).ConfigureAwait(false);
    }
}
