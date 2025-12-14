// Baiss.Infrastructure/Db/DapperConnectionFactory.cs
using System.Data;
using Microsoft.Data.Sqlite;
using Baiss.Infrastructure.Interfaces;

namespace Baiss.Infrastructure.Db;

public class DapperConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public DapperConnectionFactory() : this(DbConstants.ConnectionString)
    {
    }

    public DapperConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
        // Ensure Dapper configuration is initialized
        DapperConfiguration.Initialize();
    }

    public IDbConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public async Task<IDbConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
