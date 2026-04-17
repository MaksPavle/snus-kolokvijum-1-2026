using IndustrialProcessingSystem.Configuration;
using IndustrialProcessingSystem.Models;
using IndustrialProcessingSystem.Services;

namespace IndustrialProcessingSystem;

internal class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var config = ConfigurationLoader.Load("config.xml");

            var system = new ProcessingSystem(config.WorkerCount, config.MaxQueueSize);

            Console.WriteLine("Submitting initial jobs from config...");

            var handles = new List<JobHandle>();

            foreach (var job in config.InitialJobs)
            {
                var handle = system.Submit(job);
                handles.Add(handle);
            }

            var randomJobs = new List<Job>
            {
                new Job
                {
                    Id = Guid.NewGuid(),
                    Type = JobType.Prime,
                    Payload = "20000,4",
                    Priority = 2
                },
                new Job
                {
                    Id = Guid.NewGuid(),
                    Type = JobType.IO,
                    Payload = "800",
                    Priority = 1
                }
            };

            foreach (var job in randomJobs)
            {
                var handle = system.Submit(job);
                handles.Add(handle);
            }

            Console.WriteLine();
            Console.WriteLine("Top jobs currently in queue:");

            foreach (var job in system.GetTopJobs(5))
            {
                Console.WriteLine($"{job.Id} | {job.Type} | Priority={job.Priority}");
            }

            Console.WriteLine();
            Console.WriteLine("Waiting for results...");

            foreach (var handle in handles)
            {
                try
                {
                    int result = await handle.Result;
                    Console.WriteLine($"Job {handle.Id} completed with result: {result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Job {handle.Id} failed: {ex.Message}");
                }
            }

            await system.StopAsync();

            Console.WriteLine();
            Console.WriteLine("Processing finished.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
        }
    }
}