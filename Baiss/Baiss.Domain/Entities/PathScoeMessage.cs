
namespace Baiss.Domain.Entities;

public class SearchPathScore
{
    public Guid Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public float Score { get; set; }
    public Guid ResponseChoiceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ResponseChoice? ResponseChoice { get; set; }
}

public class ResponseChoice
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Message? Message { get; set; }
    public List<SearchPathScore> Paths { get; set; } = new List<SearchPathScore>();
}
