using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PcaImporter.Application.Importacao;
using PcaImporter.Infrastructure.Persistencia;

namespace PcaImporter.Infrastructure.Tests.Persistencia;

public sealed class RepositorioHistoricoImportacaoTests : IAsyncDisposable
{
    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    private readonly DbContextOptions<PcaDbContext> _opcoes;

    public RepositorioHistoricoImportacaoTests()
    {
        _conexao.Open();
        _opcoes = new DbContextOptionsBuilder<PcaDbContext>()
            .UseSqlite(_conexao)
            .Options;

        using var ctx = new PcaDbContext(_opcoes);
        ctx.Database.EnsureCreated();
    }

    [Fact]
    public async Task MarcarDfdExcluido_LiberaPlanilhaSemApagarAuditoria()
    {
        var repositorio = new RepositorioHistoricoImportacao(new Fabrica(_opcoes));
        await repositorio.RegistrarAsync(CriarHistorico("planilha-1", 101));

        Assert.NotNull(await repositorio.BuscarPorIdPlanilhaAsync("planilha-1"));

        var alterado = await repositorio.MarcarDfdExcluidoAsync(101, "usuario.teste");

        Assert.True(alterado);
        Assert.Null(await repositorio.BuscarPorIdPlanilhaAsync("planilha-1"));
        Assert.NotNull(await repositorio.BuscarPorIdArtefatoAsync(101));

        await using var ctx = new PcaDbContext(_opcoes);
        var registro = await ctx.HistoricoImportacoes.SingleAsync();
        Assert.NotNull(registro.DfdExcluidoEm);
        Assert.Equal("usuario.teste", registro.DfdExcluidoPorLogin);
    }

    [Fact]
    public async Task MarcarDfdExcluido_PreservaDuplicidadeDeOutroDfdAtivo()
    {
        var repositorio = new RepositorioHistoricoImportacao(new Fabrica(_opcoes));
        await repositorio.RegistrarAsync(CriarHistorico("planilha-1", 101));
        await repositorio.RegistrarAsync(CriarHistorico("planilha-1", 202));

        await repositorio.MarcarDfdExcluidoAsync(202, "usuario.teste");

        var anteriorAtivo = await repositorio.BuscarPorIdPlanilhaAsync("planilha-1");
        Assert.NotNull(anteriorAtivo);
        Assert.Equal(101, anteriorAtivo.IdArtefato);
    }

    private static HistoricoImportacaoDto CriarHistorico(string idPlanilha, long idArtefato) =>
        new(
            Id: 0,
            IdPlanilha: idPlanilha,
            UrlOriginal: $"https://docs.google.com/spreadsheets/d/{idPlanilha}",
            ImportadaEm: DateTimeOffset.UtcNow,
            IdExecucao: Guid.NewGuid().ToString("N"),
            NumeroDfd: 1,
            AnoDfd: 2026,
            IdArtefato: idArtefato,
            IdFormalizacaoDemanda: idArtefato + 1,
            TotalMateriais: 1,
            ValorTotal: 10m,
            Sucesso: true,
            MensagemErro: null,
            LinhaErro: null,
            Descricao: "Teste",
            UsuarioLogin: "usuario.teste");

    public async ValueTask DisposeAsync() => await _conexao.DisposeAsync();

    private sealed class Fabrica(DbContextOptions<PcaDbContext> opcoes) : IDbContextFactory<PcaDbContext>
    {
        public PcaDbContext CreateDbContext() => new(opcoes);

        public Task<PcaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
