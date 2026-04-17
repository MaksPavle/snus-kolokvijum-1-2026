using System.Xml.Linq;
using IndustrialProcessingSystem.Models;

namespace IndustrialProcessingSystem.Configuration;

public static class ConfigurationLoader
{
    public static SystemConfig Load(string path)
    {
        var doc = XDocument.Load(path);
        var root = doc.Root!;

        var config = new SystemConfig
        {
            WorkerCount = int.Parse(root.Element("WorkerCount")!.Value),
            MaxQueueSize = int.Parse(root.Element("MaxQueueSize")!.Value)
        };

        foreach (var jobElement in root.Element("InitialJobs")!.Elements("Job"))
        {
            config.InitialJobs.Add(new Job
            {
                Id = Guid.Parse(jobElement.Element("Id")!.Value),
                Type = Enum.Parse<JobType>(jobElement.Element("Type")!.Value),
                Payload = jobElement.Element("Payload")!.Value,
                Priority = int.Parse(jobElement.Element("Priority")!.Value)
            });
        }

        return config;
    }
}