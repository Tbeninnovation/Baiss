using System.Data;
using Dapper;
using Baiss.Domain.Entities;

namespace Baiss.Infrastructure.Db.TypeHandlers;

/// <summary>
/// Dapper type handler to convert SenderType enum to string for database storage
/// and back to enum when reading from database
/// </summary>
public class SenderTypeHandler : SqlMapper.TypeHandler<SenderType>
{
    public override void SetValue(IDbDataParameter parameter, SenderType value)
    {
        // Convert enum to string for database storage
        parameter.Value = value.ToString(); // This will be "USER" or "ASSISTANT"
    }

    public override SenderType Parse(object value)
    {
        // Convert string from database back to enum
        if (value is string stringValue)
        {
            return stringValue.ToUpper() switch
            {
                "USER" => SenderType.USER,
                "ASSISTANT" => SenderType.ASSISTANT,
                _ => SenderType.USER // Default fallback
            };
        }

        // Handle legacy cases where the value might be an integer
        if (value is int intValue)
        {
            return intValue switch
            {
                0 => SenderType.USER,
                1 => SenderType.ASSISTANT,
                _ => SenderType.USER
            };
        }

        return SenderType.USER; // Default fallback
    }
}
