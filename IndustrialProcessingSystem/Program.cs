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

            var system = new ProcessingSystem(
                config.WorkerCount,
                config.MaxQueueSize,
                "jobs.log");

            Console.WriteLine("Submitting jobs from config...");

            var handles = new List<JobHandle>();

            foreach (var job in config.InitialJobs)
            {
                handles.Add(system.Submit(job));
            }

            Console.WriteLine();
            Console.WriteLine("Top jobs currently in queue:");

            foreach (var job in system.GetTopJobs(5))
            {
                Console.WriteLine($"{job.Id} | {job.Type} | Priority={job.Priority} | Payload={job.Payload}");
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

            Console.WriteLine();
            Console.WriteLine("Trying duplicate submit for idempotency test...");

            if (config.InitialJobs.Count > 0)
            {
                var duplicateJob = config.InitialJobs[0];
                var duplicateHandle = system.Submit(duplicateJob);

                try
                {
                    int duplicateResult = await duplicateHandle.Result;
                    Console.WriteLine($"Duplicate submit returned existing result for {duplicateHandle.Id}: {duplicateResult}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Duplicate submit failed for {duplicateHandle.Id}: {ex.Message}");
                }
            }

            await system.StopAsync();

            Console.WriteLine();
            Console.WriteLine("Processing finished.");
            Console.WriteLine("Check jobs.log file in bin\\Debug\\net8.0");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
        }
    }
}