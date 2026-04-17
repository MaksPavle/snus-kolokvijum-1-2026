using IndustrialProcessingSystem.Configuration;

namespace IndustrialProcessingSystem;

internal class Program
{
    static void Main(string[] args)
    {
        var config = ConfigurationLoader.Load("config.xml");

        Console.WriteLine($"Workers: {config.WorkerCount}");
        Console.WriteLine($"MaxQueue: {config.MaxQueueSize}");

        foreach (var job in config.InitialJobs)
        {
            Console.WriteLine($"{job.Id} - {job.Type} - {job.Priority}");
        }
    }
}