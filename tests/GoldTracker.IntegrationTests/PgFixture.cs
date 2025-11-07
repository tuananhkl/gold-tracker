using System.Data;
using Dapper;
using Npgsql;
using Testcontainers.PostgreSql;

namespace GoldTracker.IntegrationTests;

public sealed class PgFixture : IAsyncLifetime
{
  private readonly PostgreSqlContainer _container;
  private string _connectionString = string.Empty;

  public PgFixture()
  {
    _container = new PostgreSqlBuilder()
      .WithImage("postgres:16")
      .WithDatabase("gold")
      .WithUsername("gold")
      .WithPassword("gold")
      .Build();
  }

  public string ConnectionString => _connectionString;

  public async Task InitializeAsync()
  {
    await _container.StartAsync();
    _connectionString = _container.GetConnectionString();

    // Apply schema from Flyway migrations
    var migrations = new[]
    {
      "V1__init_schema.sql",
      "V2__core_tables.sql",
      "V3__views_and_functions.sql"
    };

    using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();

    // Find migration files relative to solution root
    // Try multiple possible paths
    var possiblePaths = new[]
    {
      Path.Combine(Directory.GetCurrentDirectory(), "db", "flyway", "sql"),
      Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../db/flyway/sql")),
      Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db/flyway/sql")),
      "/home/derrick/gold-tracker/db/flyway/sql"
    };
    
    string? basePath = null;
    foreach (var path in possiblePaths)
    {
      if (Directory.Exists(path))
      {
        basePath = path;
        break;
      }
    }
    
    if (basePath is null)
      throw new DirectoryNotFoundException($"Could not find db/flyway/sql directory. Tried: {string.Join(", ", possiblePaths)}");
    foreach (var migration in migrations)
    {
      var sqlPath = Path.Combine(basePath, migration);
      if (!File.Exists(sqlPath))
        throw new FileNotFoundException($"Migration file not found: {sqlPath}");
      var sql = await File.ReadAllTextAsync(sqlPath);
      
      // Use NpgsqlCommand directly to execute multi-statement SQL
      await using var cmd = new NpgsqlCommand(sql, conn);
      await cmd.ExecuteNonQueryAsync();
    }
  }

  public async Task DisposeAsync()
  {
    await _container.DisposeAsync();
  }
}

