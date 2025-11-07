namespace GoldTracker.Application.Contracts.Repositories;

public interface IDbConnectionFactory
{
  System.Data.IDbConnection CreateConnection();
}

