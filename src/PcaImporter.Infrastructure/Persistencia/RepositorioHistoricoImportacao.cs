using Microsoft.EntityFrameworkCore;
using PcaImporter.Application.Importacao;
using PcaImporter.Infrastructure.Persistencia.Entidades;

namespace PcaImporter.Infrastructure.Persistencia;

public sealed class RepositorioHistoricoImportacao : IRepositorioHistoricoImportacao
{
    private readonly IDbContextFactory<PcaDbContext> _factory;

    public RepositorioHistoricoImportacao(IDbContextFactory<PcaDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<HistoricoImportacaoDto?> BuscarPorIdPlanilhaAsync(string idPlanilha, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        // Duplicado só conta se a importação anterior teve sucesso (gerou DFD real no Compras).
        // Tentativas falhas podem ser refeitas livremente sem alerta.
        var e = await ctx.HistoricoImportacoes
            .Where(x => x.IdPlanilha == idPlanilha && x.Sucesso)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return e is null ? null : Mapear(e);
    }

    public async Task RegistrarAsync(HistoricoImportacaoDto r, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        ctx.HistoricoImportacoes.Add(new HistoricoImportacaoEntity
        {
            IdPlanilha = r.IdPlanilha,
            UrlOriginal = r.UrlOriginal,
            ImportadaEm = r.ImportadaEm,
            IdExecucao = r.IdExecucao,
            NumeroDfd = r.NumeroDfd,
            AnoDfd = r.AnoDfd,
            IdArtefato = r.IdArtefato,
            IdFormalizacaoDemanda = r.IdFormalizacaoDemanda,
            TotalMateriais = r.TotalMateriais,
            ValorTotal = r.ValorTotal,
            Sucesso = r.Sucesso,
            MensagemErro = r.MensagemErro,
            LinhaErro = r.LinhaErro,
            Descricao = r.Descricao,
            UsuarioLogin = r.UsuarioLogin,
        });
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<HistoricoImportacaoDto>> ListarAsync(int limite = 200, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var lista = await ctx.HistoricoImportacoes
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(limite)
            .ToListAsync(ct).ConfigureAwait(false);
        return lista.Select(Mapear).ToList();
    }

    public async Task<int> LimparAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await ctx.HistoricoImportacoes.ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }

    public async Task<MetricasImportacaoDto> ObterMetricasAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var total = await ctx.HistoricoImportacoes.Where(x => x.Sucesso).CountAsync(ct).ConfigureAwait(false);
        if (total == 0) return new MetricasImportacaoDto(0, 0, 0m);
        var itens = await ctx.HistoricoImportacoes.Where(x => x.Sucesso).SumAsync(x => x.TotalMateriais, ct).ConfigureAwait(false);
        var valor = await ctx.HistoricoImportacoes.Where(x => x.Sucesso).SumAsync(x => x.ValorTotal, ct).ConfigureAwait(false);
        return new MetricasImportacaoDto(total, itens, valor);
    }

    private static HistoricoImportacaoDto Mapear(HistoricoImportacaoEntity e) =>
        new(e.Id, e.IdPlanilha, e.UrlOriginal, e.ImportadaEm, e.IdExecucao,
            e.NumeroDfd, e.AnoDfd, e.IdArtefato, e.IdFormalizacaoDemanda, e.TotalMateriais, e.ValorTotal,
            e.Sucesso, e.MensagemErro, e.LinhaErro, e.Descricao, e.UsuarioLogin);
}
