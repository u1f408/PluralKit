using System;
using System.Data;
using System.Reflection;

namespace PluralKit.Bot.Reflection;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var assemblyPath = Path.GetFullPath(args.FirstOrDefault(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "PluralKit.Bot.dll")));
        var assembly = Assembly.LoadFrom(assemblyPath);
        if (assembly == null)
        {
            Console.WriteLine($"Failed to load assembly: {assemblyPath}");
            return;
        }

        Dictionary<string, List<Dictionary<string, string?>>> commandGroups = new();
        assembly.GetType("PluralKit.Bot.CommandTree")!
            .GetFields()
            .Where(m => m.FieldType.ToString() == "PluralKit.Bot.Command")
            .Select(m => m.GetValue(null))
            .Select(m =>
            {
                var ty = m!.GetType();
                var key = (string)ty.GetProperty("Key")!.GetValue(m)!;
                var props = ty.GetProperties()
                    .Where(p => p.Name != "Key")
                    .Select(p => (p.Name, (string?)p.GetValue(m)))
                    .ToDictionary(f => f.Name, f => f.Item2);
                return (key, props);
            })
            .ToList()
            .ForEach(m =>
            {
                if (!commandGroups.ContainsKey(m.key))
                    commandGroups[m.key] = new();
                commandGroups[m.key].Add(m.props);
            });

        Console.WriteLine($"There are {commandGroups!.Count} command groups in the bot.");
        foreach ((var group, var commands) in commandGroups)
        {
            Console.WriteLine($"Command group \"{group}\":");
            foreach (var command in commands)
            {
                Console.WriteLine($"  {command["Usage"]}");
                foreach ((var prop, var val) in command.Where((k, _) => k.Key != "Usage"))
                {
                    Console.WriteLine($"    {prop}: {val}");
                }
            }
        }
    }
}
