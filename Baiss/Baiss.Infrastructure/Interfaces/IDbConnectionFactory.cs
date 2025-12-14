using System.Data;

namespace Baiss.Infrastructure.Interfaces;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
    Task<IDbConnection> CreateConnectionAsync();
}
