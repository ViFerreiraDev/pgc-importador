using Microsoft.EntityFrameworkCore;
using PcaImporter.Application.Usuarios;
using PcaImporter.Domain.Usuarios;
using PcaImporter.Infrastructure.Persistencia;
using PcaImporter.Infrastructure.Persistencia.Entidades;

namespace PcaImporter.Infrastructure.Usuarios;

public sealed class ServicoUsuarios : IServicoUsuarios
{
    private readonly IDbContextFactory<PcaDbContext> _factory;

    public ServicoUsuarios(IDbContextFactory<PcaDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<UsuarioDto?> AutenticarAsync(string login, string senha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(senha)) return null;
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var u = await ctx.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Login == login.ToLowerInvariant(), ct).ConfigureAwait(false);
        if (u is null) return null;
        if (!HasherSenha.Verificar(senha, u.SenhaHash, u.Salt)) return null;
        return Mapear(u);
    }

    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var lista = await ctx.Usuarios.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct).ConfigureAwait(false);
        return lista.Select(Mapear).ToList();
    }

    public async Task<UsuarioDto?> ObterPorLoginAsync(string login, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(login)) return null;
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var u = await ctx.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Login == login.ToLowerInvariant(), ct).ConfigureAwait(false);
        return u is null ? null : Mapear(u);
    }

    public async Task<UsuarioDto> CriarAsync(CriarUsuarioInput input, string? criadoPorLogin, CancellationToken ct = default)
    {
        var login = (input.Login ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(login)) throw new ArgumentException("Login obrigatório.", nameof(input));
        if (string.IsNullOrWhiteSpace(input.Nome)) throw new ArgumentException("Nome obrigatório.", nameof(input));
        if (string.IsNullOrWhiteSpace(input.Senha) || input.Senha.Length < 6)
            throw new ArgumentException("Senha precisa ter pelo menos 6 caracteres.", nameof(input));

        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        if (await ctx.Usuarios.AnyAsync(x => x.Login == login, ct).ConfigureAwait(false))
        {
            throw new UsuarioJaExisteException(login);
        }

        var (hash, salt) = HasherSenha.Gerar(input.Senha);
        var u = new UsuarioEntity
        {
            Login = login,
            Nome = input.Nome.Trim(),
            SenhaHash = hash,
            Salt = salt,
            Papel = input.Papel,
            CriadoEm = DateTimeOffset.UtcNow,
            CriadoPorLogin = criadoPorLogin?.ToLowerInvariant(),
        };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
        return Mapear(u);
    }

    public async Task<bool> AlterarSenhaAsync(int id, string novaSenha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(novaSenha) || novaSenha.Length < 6) return false;
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var u = await ctx.Usuarios.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (u is null) return false;
        var (hash, salt) = HasherSenha.Gerar(novaSenha);
        u.SenhaHash = hash;
        u.Salt = salt;
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RemoverAsync(int id, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var u = await ctx.Usuarios.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (u is null) return false;
        // Não remove o último admin
        if (u.Papel == Papel.Admin)
        {
            var totalAdmins = await ctx.Usuarios.CountAsync(x => x.Papel == Papel.Admin, ct).ConfigureAwait(false);
            if (totalAdmins <= 1) return false;
        }
        ctx.Usuarios.Remove(u);
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> ExisteAlgumAdminAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await ctx.Usuarios.AnyAsync(x => x.Papel == Papel.Admin, ct).ConfigureAwait(false);
    }

    private static UsuarioDto Mapear(UsuarioEntity u) =>
        new(u.Id, u.Login, u.Nome, u.Papel, u.CriadoEm, u.CriadoPorLogin);
}
