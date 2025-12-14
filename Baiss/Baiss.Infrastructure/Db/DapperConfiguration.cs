using Dapper;
using Baiss.Domain.Entities;
using Baiss.Infrastructure.Db.TypeHandlers;
using System.Text.Json;

namespace Baiss.Infrastructure.Db;

/// <summary>
/// Configuration for Dapper type handlers and mappings
/// </summary>
public static class DapperConfiguration
{
    private static bool _isInitialized = false;
    private static readonly object _lock = new object();

    /// <summary>
    /// Initializes Dapper type handlers. This method is safe to call multiple times.
    /// </summary>
    public static void Initialize()
    {
        if (_isInitialized)
            return;

        lock (_lock)
        {
            if (_isInitialized)
                return;

            // Register type handlers
            SqlMapper.AddTypeHandler(new SenderTypeHandler());
            SqlMapper.AddTypeHandler(new JsonDocumentTypeHandler());

            // You can add more type handlers here as needed

            _isInitialized = true;
        }
    }

    /// <summary>
    /// Forces re-initialization of type handlers (for testing purposes)
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _isInitialized = false;
        }
    }
}
