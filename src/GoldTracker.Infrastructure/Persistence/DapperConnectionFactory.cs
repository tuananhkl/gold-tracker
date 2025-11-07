using GoldTracker.Application.Contracts.Repositories;
using Npgsql;

namespace GoldTracker.Infrastructure.Persistence;

public sealed class DapperConnectionFactory : IDbConnectionFactory
{
  private readonly string _connectionString;

  public DapperConnectionFactory(string connectionString)
  {
    _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
  }

  public NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connectionString);

  System.Data.IDbConnection IDbConnectionFactory.CreateConnection() => CreateConnection();
}

