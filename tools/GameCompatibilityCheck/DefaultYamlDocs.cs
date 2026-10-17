using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Mono.Cecil;
using MonoMod.Utils;

// Export/check first-launch templates from the compiled plugin, without initializing
// Unity-dependent manager classes. No user/game configuration is read or rewritten.
internal static class DefaultYamlDocs
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();

    internal static void Run(Assembly plugin)
    {
        string? directory = Environment.GetEnvironmentVariable("CREATUREMANAGER_DEFAULT_YAML_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        directory = Path.GetFullPath(directory);
        bool write = Environment.GetEnvironmentVariable("CREATUREMANAGER_WRITE_DEFAULT_YAML") == "1";
        var templates = new[]
        {
            ("levels.yml", "CreatureDomainManager", "BuildDefaultLevelOverrideYaml"),
            ("karma.yml", "CreatureKarmaManager", "BuildDefaultYaml"),
            ("factions.yml", "CreatureFactionManager", "BuildDefaultOverrideYaml"),
            ("creatures.yml", "CreatureDomainManager", "BuildDefaultOverrideYaml"),
            ("ai.yml", "CreatureDomainManager", "BuildDefaultAiOverrideYaml"),
            ("attacks.yml", "CreatureDomainManager", "BuildDefaultAttackOverrideYaml"),
            ("projectile.yml", "CreatureDomainManager", "BuildDefaultProjectileOverrideYaml")
        };
        var generated = new Dictionary<string, string>();
        Type yamlStream = plugin.GetType("YamlDotNet.RepresentationModel.YamlStream", true)!;
        foreach (var template in templates)
        {
            Type owner = plugin.GetType("CreatureManager." + template.Item2, true)!;
            string yaml = (string)Copy(owner.GetMethod(template.Item3, Static)!).Invoke(null, Array.Empty<object>())!;
            // Validate every output before writing any documentation file.
            using var reader = new StringReader(yaml);
            yamlStream.GetMethod("Load", new[] { typeof(TextReader) })!.Invoke(Activator.CreateInstance(yamlStream), new object[] { reader });
            generated.Add(template.Item1, Normalize(yaml));
        }

        if (write) Directory.CreateDirectory(directory);
        foreach (var file in generated)
        {
            string path = Path.Combine(directory, file.Key);
            bool matches = File.Exists(path) && Normalize(File.ReadAllText(path)) == file.Value;
            if (matches) continue;
            if (!write) throw new InvalidOperationException("Default YAML documentation is missing or stale: " + path);
            File.WriteAllText(path, file.Value, new UTF8Encoding(false));
        }
        System.Console.WriteLine($"Default YAML documentation: {generated.Count} templates parsed and {(write ? "exported" : "matched")} at {directory}. No Unity scene or user configuration was used.");
    }

    private static string Normalize(string yaml) => yaml.Replace("\r\n", "\n");

    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo existing)) return existing;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(DefaultYamlDocs);
        foreach (var instruction in copy.Definition.Body.Instructions)
        {
            if (!(instruction.Operand is MethodReference called) || called.DeclaringType.FullName != source.DeclaringType!.FullName) continue;
            var method = (MethodInfo)called.ResolveReflection();
            if (!method.IsStatic) continue;
            // The preset method's invalid-value fallback is recursive. Generated templates
            // enumerate only valid presets; keep the original unreachable fallback call.
            if (method == source) continue;
            instruction.Operand = copy.Module.ImportReference(Copy(method));
        }
        return Copies[source] = copy.Generate();
    }
}
