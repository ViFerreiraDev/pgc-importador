using PcaImporter.Application.Importacao;

namespace PcaImporter.Infrastructure.Importacao;

internal sealed class EstadoImportacaoMutavel
{
    public string Id { get; init; } = string.Empty;
    public EstadoImportacao Estado { get; set; } = EstadoImportacao.Pendente;
    public DateTimeOffset IniciadaEm { get; init; }
    public DateTimeOffset? ConcluidaEm { get; set; }
    public int TotalEtapas { get; set; }
    public int EtapasConcluidas { get; set; }
    public string? EtapaAtual { get; set; }
    public long? IdArtefatoCriado { get; set; }
    public long? IdFormalizacaoCriado { get; set; }
    public int? NumeroDfd { get; set; }
    public int? AnoDfd { get; set; }
    public int TotalMateriais { get; set; }
    public int MateriaisAdicionados { get; set; }
    public string? UltimoErro { get; set; }
    public List<EventoImportacao> Eventos { get; } = new();

    public StatusImportacao Snapshot() => new(
        Id, Estado, IniciadaEm, ConcluidaEm,
        TotalEtapas, EtapasConcluidas, EtapaAtual,
        IdArtefatoCriado, IdFormalizacaoCriado, NumeroDfd, AnoDfd,
        TotalMateriais, MateriaisAdicionados, UltimoErro,
        Eventos.ToList()
    );
}

public sealed class RegistroImportacoes
{
    private readonly Lock _trava = new();
    private readonly Dictionary<string, EstadoImportacaoMutavel> _porId = new();

    internal EstadoImportacaoMutavel CriarNova()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var estado = new EstadoImportacaoMutavel
        {
            Id = id,
            IniciadaEm = DateTimeOffset.UtcNow,
        };
        lock (_trava)
        {
            _porId[id] = estado;
            // limita a 50 importacoes recentes em memoria
            if (_porId.Count > 50)
            {
                var maisAntiga = _porId.Values.OrderBy(e => e.IniciadaEm).First();
                _porId.Remove(maisAntiga.Id);
            }
        }
        return estado;
    }

    public StatusImportacao? Obter(string id)
    {
        lock (_trava)
        {
            return _porId.TryGetValue(id, out var e) ? e.Snapshot() : null;
        }
    }

    public IReadOnlyList<StatusImportacao> ListarRecentes(int limite)
    {
        lock (_trava)
        {
            return _porId.Values
                .OrderByDescending(e => e.IniciadaEm)
                .Take(limite)
                .Select(e => e.Snapshot())
                .ToList();
        }
    }

    internal void Atualizar(string id, Action<EstadoImportacaoMutavel> mutar)
    {
        lock (_trava)
        {
            if (_porId.TryGetValue(id, out var e))
            {
                mutar(e);
            }
        }
    }
}
