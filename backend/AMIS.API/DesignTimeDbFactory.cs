using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AMIS.API;

public sealed class DesignTimeDbFactory : IDesignTimeDbContextFactory<AppDb> {
    public AppDb CreateDbContext(string[] args) {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("ConnectionStrings__Default is required for migrations.");
        return new AppDb(new DbContextOptionsBuilder<AppDb>().UseNpgsql(connection).Options);
    }
}
