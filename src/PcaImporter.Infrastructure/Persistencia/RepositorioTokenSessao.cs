using Microsoft.EntityFrameworkCore;
using PcaImporter.Application.Token;
using PcaImporter.Infrastructure.Persistencia.Entidades;

namespace PcaImporter.Infrastructure.Persistencia;

public sealed class RepositorioTokenSessao : IRepositorioTokenSessao
{
    private readonly IDbContextFactory<PcaDbContext> _factory;
    private readonly TimeProvider _tempo;

    public RepositorioTokenSessao(IDbContextFactory<PcaDbContext> factory, TimeProvider tempo)
    {
        _factory = factory;
        _tempo = tempo;
    }

    public async Task<string?> LerRefreshAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var entity = await ctx.TokensSessao.FindAsync([TokenSessaoEntity.IdFixo], ct).ConfigureAwait(false);
        return entity?.RefreshToken;
    }

    public async Task SalvarRefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var entity = await ctx.TokensSessao.FindAsync([TokenSessaoEntity.IdFixo], ct).ConfigureAwait(false);
        if (entity is null)
        {
            ctx.TokensSessao.Add(new TokenSessaoEntity
            {
                Id = TokenSessaoEntity.IdFixo,
                RefreshToken = refreshToken,
                AtualizadoEm = _tempo.GetUtcNow()
            });
        }
        else
        {
            entity.RefreshToken = refreshToken;
            entity.AtualizadoEm = _tempo.GetUtcNow();
        }
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task LimparAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await ctx.TokensSessao.Where(t => t.Id == TokenSessaoEntity.IdFixo).ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}
