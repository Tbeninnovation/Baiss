using System.Data;
using Dapper;
using Baiss.Domain.Entities;
using Baiss.Infrastructure.Interfaces;
using Baiss.Application.Interfaces;

namespace Baiss.Infrastructure.Repositories;

public class SearchPathScoreRepository : ISearchPathScoreRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SearchPathScoreRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<SearchPathScore> CreateAsync(SearchPathScore searchPathScore)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO SearchPathScores (Id, Path, Score, ResponseChoiceId, CreatedAt)
            VALUES (@Id, @Path, @Score, @ResponseChoiceId, @CreatedAt)";

        var parameters = new
        {
            Id = searchPathScore.Id.ToString(),
            Path = searchPathScore.Path,
            Score = searchPathScore.Score,
            ResponseChoiceId = searchPathScore.ResponseChoiceId.ToString(),
            CreatedAt = searchPathScore.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        };

        await connection.ExecuteAsync(sql, parameters);
        return searchPathScore;
    }

    public async Task<SearchPathScore?> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT Id, Path, Score, ResponseChoiceId, CreatedAt
            FROM SearchPathScores
            WHERE Id = @Id";

        var result = await connection.QueryFirstOrDefaultAsync(sql, new { Id = id.ToString() });

        if (result == null) return null;

        return new SearchPathScore
        {
            Id = Guid.Parse(result.Id),
            Path = result.Path,
            Score = (float)result.Score,
            ResponseChoiceId = Guid.Parse(result.ResponseChoiceId),
            CreatedAt = DateTime.Parse(result.CreatedAt)
        };
    }

    public async Task<IEnumerable<SearchPathScore>> GetByResponseChoiceIdAsync(Guid responseChoiceId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT Id, Path, Score, ResponseChoiceId, CreatedAt
            FROM SearchPathScores
            WHERE ResponseChoiceId = @ResponseChoiceId
            ORDER BY Score DESC";

        var results = await connection.QueryAsync(sql, new { ResponseChoiceId = responseChoiceId.ToString() });

        return results.Select(result => new SearchPathScore
        {
            Id = Guid.Parse(result.Id),
            Path = result.Path,
            Score = (float)result.Score,
            ResponseChoiceId = Guid.Parse(result.ResponseChoiceId),
            CreatedAt = DateTime.Parse(result.CreatedAt)
        });
    }

    public async Task<IEnumerable<SearchPathScore>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT Id, Path, Score, ResponseChoiceId, CreatedAt
            FROM SearchPathScores
            ORDER BY CreatedAt DESC";

        var results = await connection.QueryAsync(sql);

        return results.Select(result => new SearchPathScore
        {
            Id = Guid.Parse(result.Id),
            Path = result.Path,
            Score = (float)result.Score,
            ResponseChoiceId = Guid.Parse(result.ResponseChoiceId),
            CreatedAt = DateTime.Parse(result.CreatedAt)
        });
    }

    public async Task UpdateAsync(SearchPathScore searchPathScore)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE SearchPathScores
            SET Path = @Path, Score = @Score, ResponseChoiceId = @ResponseChoiceId, CreatedAt = @CreatedAt
            WHERE Id = @Id";

        var parameters = new
        {
            Id = searchPathScore.Id.ToString(),
            Path = searchPathScore.Path,
            Score = searchPathScore.Score,
            ResponseChoiceId = searchPathScore.ResponseChoiceId.ToString(),
            CreatedAt = searchPathScore.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        };

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task DeleteAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            DELETE FROM SearchPathScores
            WHERE Id = @Id";

        await connection.ExecuteAsync(sql, new { Id = id.ToString() });
    }

    public async Task DeleteByResponseChoiceIdAsync(Guid responseChoiceId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            DELETE FROM SearchPathScores
            WHERE ResponseChoiceId = @ResponseChoiceId";

        await connection.ExecuteAsync(sql, new { ResponseChoiceId = responseChoiceId.ToString() });
    }
}
