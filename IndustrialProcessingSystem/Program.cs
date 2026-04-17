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
            if (File.Exists("jobs.log"))
            {
                File.Delete("jobs.log");
            }

            var config = ConfigurationLoader.Load("config.xml");

            var system = new ProcessingSystem(
                config.WorkerCount,
                config.MaxQueueSize,
                "jobs.log");

            using var reportService = new ReportService(system, "Reports");

            Console.WriteLine("Submitting jobs from config...");

            var handles = new List<JobHandle>();

            foreach (var job in config.InitialJobs)
            {
                handles.Add(system.Submit(job));
            }

            Console.WriteLine();
            Console.WriteLine("Starting producer threads...");

            var producerCts = new CancellationTokenSource();

            var producers = new List<Task>();

            for (int i = 0; i < 2; i++) // 2 producer niti
            {
                int producerId = i;

                producers.Add(Task.Run(async () =>
                {
                    var random = new Random();

                    while (!producerCts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            Job job;

                            if (random.Next(2) == 0)
                            {
                                job = new Job
                                {
                                    Type = JobType.Prime,
                                    Payload = $"numbers:{random.Next(5000, 15000)},threads:{random.Next(1, 4)}",
                                    Priority = random.Next(1, 4)
                                };
                            }
                            else
                            {
                                job = new Job
                                {
                                    Type = JobType.IO,
                                    Payload = $"delay:{random.Next(500, 3000)}",
                                    Priority = random.Next(1, 4)
                                };
                            }

                            var handle = system.Submit(job);

                            Console.WriteLine($"[PRODUCER {producerId}] Submitted {job.Type} job {handle.Id}");

                            await Task.Delay(random.Next(500, 1500), producerCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }, producerCts.Token));
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

            Console.WriteLine();
            Console.WriteLine("If you want to generate at least one report, wait 1 minute before pressing Enter.");
            Console.WriteLine("Press Enter to stop the system...");
            Console.ReadLine();

            producerCts.Cancel();
            await Task.WhenAll(producers);

            await system.StopAsync();

            Console.WriteLine();
            Console.WriteLine("Processing finished.");
            Console.WriteLine("Check jobs.log in bin\\Debug\\net8.0");
            Console.WriteLine("Check Reports folder in bin\\Debug\\net8.0\\Reports");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
        }
    }
}