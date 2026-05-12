using PcaImporter.Application.Compras.Dfd;

namespace PcaImporter.Infrastructure.Compras.Dfd;

public sealed class RegistroDfdAtual : IRegistroDfdAtual
{
    private readonly Lock _trava = new();
    private DfdCriadoDto? _atual;
    private readonly List<MaterialServicoCriadoDto> _itens = new();
    private readonly List<ResponsavelCriadoDto> _responsaveis = new();

    public DfdCriadoDto? Obter()
    {
        lock (_trava) { return _atual; }
    }

    public void Definir(DfdCriadoDto dfd)
    {
        lock (_trava)
        {
            _atual = dfd;
            _itens.Clear();
            _responsaveis.Clear();
        }
    }

    public void Limpar()
    {
        lock (_trava)
        {
            _atual = null;
            _itens.Clear();
            _responsaveis.Clear();
        }
    }

    public IReadOnlyList<MaterialServicoCriadoDto> ListarItens()
    {
        lock (_trava) { return _itens.ToList(); }
    }

    public void RegistrarItem(MaterialServicoCriadoDto item)
    {
        lock (_trava) { _itens.Add(item); }
    }

    public IReadOnlyList<ResponsavelCriadoDto> ListarResponsaveis()
    {
        lock (_trava) { return _responsaveis.ToList(); }
    }

    public void RegistrarResponsavel(ResponsavelCriadoDto r)
    {
        lock (_trava) { _responsaveis.Add(r); }
    }
}
