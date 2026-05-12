using Microsoft.EntityFrameworkCore;
using PcaImporter.Infrastructure.Persistencia.Entidades;

namespace PcaImporter.Infrastructure.Persistencia;

public class PcaDbContext : DbContext
{
    public PcaDbContext(DbContextOptions<PcaDbContext> options) : base(options)
    {
    }

    public DbSet<TokenSessaoEntity> TokensSessao => Set<TokenSessaoEntity>();
    public DbSet<HistoricoImportacaoEntity> HistoricoImportacoes => Set<HistoricoImportacaoEntity>();
    public DbSet<CatalogoMaterialEntity> CatalogoMateriais => Set<CatalogoMaterialEntity>();
    public DbSet<UsuarioEntity> Usuarios => Set<UsuarioEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TokenSessaoEntity>(b =>
        {
            b.ToTable("tokens_sessao");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.RefreshToken).IsRequired();
            b.Property(x => x.AtualizadoEm).IsRequired();
        });

        modelBuilder.Entity<HistoricoImportacaoEntity>(b =>
        {
            b.ToTable("historico_importacoes");
            b.HasKey(x => x.Id);
            b.Property(x => x.IdPlanilha).IsRequired().HasMaxLength(120);
            b.Property(x => x.UrlOriginal).IsRequired();
            b.Property(x => x.IdExecucao).IsRequired().HasMaxLength(40);
            b.Property(x => x.ValorTotal).HasColumnType("TEXT").HasDefaultValue(0m);
            b.HasIndex(x => x.IdPlanilha);
        });

        modelBuilder.Entity<UsuarioEntity>(b =>
        {
            b.ToTable("usuarios");
            b.HasKey(x => x.Id);
            b.Property(x => x.Login).IsRequired().HasMaxLength(60);
            b.Property(x => x.Nome).IsRequired().HasMaxLength(120);
            b.Property(x => x.SenhaHash).IsRequired();
            b.Property(x => x.Salt).IsRequired();
            b.Property(x => x.Papel).HasConversion<int>();
            b.HasIndex(x => x.Login).IsUnique();
        });

        modelBuilder.Entity<CatalogoMaterialEntity>(b =>
        {
            b.ToTable("catalogo_materiais");
            b.HasKey(x => x.CodigoItem);
            b.Property(x => x.CodigoItem).ValueGeneratedNever();
            b.Property(x => x.NomeGrupo).IsRequired();
            b.Property(x => x.NomeClasse).IsRequired();
            b.Property(x => x.NomePdm).IsRequired();
            b.Property(x => x.DescricaoItem).IsRequired();
            b.HasIndex(x => x.CodigoClasse);
            b.HasIndex(x => x.CodigoPdm);
        });
    }
}
