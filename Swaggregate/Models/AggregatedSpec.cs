namespace Swaggregate.Models;

public class AggregatedSpec
{
    public string Title { get; set; } = string.Empty;
    public List<ServiceGroup> Services { get; set; } = new();
    public DateTime FetchedAt { get; set; }
}
