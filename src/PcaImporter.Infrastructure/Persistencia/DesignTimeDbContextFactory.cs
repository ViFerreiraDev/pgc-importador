using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PcaImporter.Infrastructure.Persistencia;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PcaDbContext>
{
    public PcaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PcaDbContext>()
            .UseSqlite("Data Source=pca-importer.db")
            .Options;

        return new PcaDbContext(options);
    }
}
