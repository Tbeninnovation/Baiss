using System.Data;
using System.Text.Json;
using Dapper;

namespace Baiss.Infrastructure.Db.TypeHandlers;

/// <summary>
/// Dapper type handler to convert JsonDocument to string for database storage
/// and back to JsonDocument when reading from database
/// </summary>
public class JsonDocumentTypeHandler : SqlMapper.TypeHandler<JsonDocument>
{
    public override void SetValue(IDbDataParameter parameter, JsonDocument? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
        }
        else
        {
            // Convert JsonDocument to JSON string for database storage
            using var stream = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            value.WriteTo(writer);
            writer.Flush();
            parameter.Value = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    public override JsonDocument? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        // Convert string from database back to JsonDocument
        if (value is string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return null;
            }

            return JsonDocument.Parse(stringValue);
        }

        return null;
    }
}
