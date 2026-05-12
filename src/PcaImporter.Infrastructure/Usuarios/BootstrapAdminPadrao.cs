using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PcaImporter.Application.Usuarios;
using PcaImporter.Domain.Usuarios;

namespace PcaImporter.Infrastructure.Usuarios;

/// <summary>
/// No primeiro start (ou se todos os admins forem deletados), cria um admin padrão.
/// Login: admin · Senha: admin · Mostra aviso destacado nos logs para o operador trocar.
/// </summary>
public sealed class BootstrapAdminPadrao : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<BootstrapAdminPadrao> _log;

    public BootstrapAdminPadrao(IServiceProvider sp, ILogger<BootstrapAdminPadrao> log)
    {
        _sp = sp;
        _log = log;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var escopo = _sp.CreateScope();
        var servico = escopo.ServiceProvider.GetRequiredService<IServicoUsuarios>();
        var jaTemAdmin = await servico.ExisteAlgumAdminAsync(cancellationToken).ConfigureAwait(false);

        // Modo emergencial: PCA_RESET_ADMIN=1 reseta a senha do "admin" para "admin"
        // (ou cria caso não exista). Útil quando o operador esqueceu a senha.
        var resetar = Environment.GetEnvironmentVariable("PCA_RESET_ADMIN");
        if (!string.IsNullOrEmpty(resetar) && resetar != "0" && resetar.ToLower() != "false")
        {
            try
            {
                var existente = await servico.ObterPorLoginAsync("admin", cancellationToken).ConfigureAwait(false);
                if (existente is null)
                {
                    await CriarAdminPadraoAsync(servico, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await servico.AlterarSenhaAsync(existente.Id, "admin123", cancellationToken).ConfigureAwait(false);
                    _log.LogWarning("=========================================================");
                    _log.LogWarning(" PCA_RESET_ADMIN ativo. Senha do 'admin' resetada.");
                    _log.LogWarning("    login: admin");
                    _log.LogWarning("    senha: admin123");
                    _log.LogWarning(" Remova a env var no próximo start e troque a senha após logar.");
                    _log.LogWarning("=========================================================");
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Falha ao resetar admin via PCA_RESET_ADMIN.");
            }
            return;
        }

        if (jaTemAdmin) return;

        await CriarAdminPadraoAsync(servico, cancellationToken).ConfigureAwait(false);
    }

    private async Task CriarAdminPadraoAsync(IServicoUsuarios servico, CancellationToken ct)
    {
        try
        {
            await servico.CriarAsync(new CriarUsuarioInput(
                Login: "admin",
                Nome: "Administrador",
                Senha: "admin123",
                Papel: Papel.Admin
            ), criadoPorLogin: null, ct).ConfigureAwait(false);
            _log.LogWarning("=========================================================");
            _log.LogWarning(" Nenhum admin encontrado. Criado admin padrão:");
            _log.LogWarning("    login: admin");
            _log.LogWarning("    senha: admin123");
            _log.LogWarning(" TROQUE A SENHA NO PRIMEIRO LOGIN.");
            _log.LogWarning("=========================================================");
        }
        catch (UsuarioJaExisteException)
        {
            // Race entre instâncias — ok
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao criar admin padrão.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
