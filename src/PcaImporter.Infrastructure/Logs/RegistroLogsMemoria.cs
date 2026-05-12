using PcaImporter.Application.Logs;

namespace PcaImporter.Infrastructure.Logs;

public sealed class RegistroLogsMemoria : IRegistroLogs
{
    private const int Capacidade = 1000;

    private readonly LinkedList<LogEvento> _eventos = new();
    private readonly Lock _trava = new();
    private readonly TimeProvider _tempo;
    private long _proximoId = 1;

    public RegistroLogsMemoria(TimeProvider tempo)
    {
        _tempo = tempo;
    }

    public void Registrar(NivelLog nivel, string categoria, string mensagem, string? detalhes = null)
    {
        lock (_trava)
        {
            var evento = new LogEvento(_proximoId++, _tempo.GetUtcNow(), nivel, categoria, mensagem, detalhes);
            _eventos.AddFirst(evento);
            while (_eventos.Count > Capacidade)
            {
                _eventos.RemoveLast();
            }
        }
    }

    public PaginaLogsDto Consultar(int pagina, int tamanhoPagina, NivelLog? nivelMinimo = null, string? categoria = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamanhoPagina < 1) tamanhoPagina = 20;
        if (tamanhoPagina > 200) tamanhoPagina = 200;

        lock (_trava)
        {
            IEnumerable<LogEvento> q = _eventos;
            if (nivelMinimo is not null) q = q.Where(e => (int)e.Nivel >= (int)nivelMinimo);
            if (!string.IsNullOrWhiteSpace(categoria)) q = q.Where(e => e.Categoria == categoria);

            var lista = q.ToList();
            var total = lista.Count;
            var itens = lista.Skip((pagina - 1) * tamanhoPagina).Take(tamanhoPagina).ToList();

            return new PaginaLogsDto(itens, pagina, tamanhoPagina, total);
        }
    }
}
