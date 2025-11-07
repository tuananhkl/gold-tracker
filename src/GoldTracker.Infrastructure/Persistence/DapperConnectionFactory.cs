using Npgsql;

namespace GoldTracker.Infrastructure.Persistence;

public sealed class DapperConnectionFactory
{
  private readonly string _connectionString;

  public DapperConnectionFactory(string connectionString)
  {
    _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
  }

  public NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}

