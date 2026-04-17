using IndustrialProcessingSystem.Models;

namespace IndustrialProcessingSystem.Infrastructure;

public class JobExecutionRecord
{
    public Guid JobId { get; set; }
    public JobType Type { get; set; }
    public bool Success { get; set; }
    public int? Result { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime Timestamp { get; set; }
}