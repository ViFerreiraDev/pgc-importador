namespace PcaImporter.Application.Compras.Dfd;

public interface IRegistroDfdAtual
{
    DfdCriadoDto? Obter();

    void Definir(DfdCriadoDto dfd);

    void Limpar();

    IReadOnlyList<MaterialServicoCriadoDto> ListarItens();

    void RegistrarItem(MaterialServicoCriadoDto item);

    IReadOnlyList<ResponsavelCriadoDto> ListarResponsaveis();

    void RegistrarResponsavel(ResponsavelCriadoDto responsavel);
}
