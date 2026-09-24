using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Mono.Cecil;

internal static class Program
{
    private static string[] SearchPaths = Array.Empty<string>();
    private static int Failures;

    private static int Main(string[] args)
    {
        if (args.Length != 3 && args.Length != 4) { System.Console.Error.WriteLine("Usage: GameCompatibilityCheck <plugin.dll> <original Managed directory> <BepInEx core directory> [DropThat.dll]"); return 2; }
        SearchPaths = new[] { Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), Path.GetDirectoryName(Path.GetFullPath(args[0]))! };
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            string name = new AssemblyName(e.Name).Name + ".dll";
            string? file = SearchPaths.Select(p => Path.Combine(p, name)).FirstOrDefault(File.Exists);
            return file == null ? null : Assembly.LoadFrom(file);
        };
        try { Run(Path.GetFullPath(args[0]), args.Length == 4 ? Path.GetFullPath(args[3]) : null); }
        catch (Exception e) { Fail(e.ToString()); }
        System.Console.WriteLine($"Compatibility checks: {Failures} failure(s). No Unity scene or network session was executed.");
        return Failures == 0 ? 0 : 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string pluginPath, string? dropThatPath)
    {
        CheckReferences(pluginPath);
        foreach (string name in new[] { "assembly_valheim", "assembly_guiutils", "assembly_utils", "UnityEngine.CoreModule" })
        {
            string actual = Path.GetFullPath(Assembly.Load(name).Location);
            string expected = Path.Combine(SearchPaths[0], name + ".dll");
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Runtime loaded a different reference: " + actual);
        }
        System.Console.WriteLine("Runtime reference paths match the original Managed directory.");
        // BepInEx's static core config is created by the plugin-registry fixtures. Keep it
        // outside the installed game and user config, without starting the real Chainloader.
        string fixtureConfig = Path.Combine(Path.GetTempPath(), "CreatureManager-check-" + Guid.NewGuid(), "config");
        typeof(BepInEx.Paths).GetProperty("ConfigPath")!.SetValue(null, fixtureConfig);
        typeof(BepInEx.Paths).GetProperty("BepInExConfigPath")!.SetValue(null, Path.Combine(fixtureConfig, "BepInEx.cfg"));
        // Offline fixture: accept ServerSync's scheduled startup action without creating a Unity object
        // or installing detours. The original game and plugin assemblies are never rewritten.
        Type helperType = typeof(BepInEx.ThreadingHelper);
        object helper = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(helperType);
        helperType.GetField("_invokeLock", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(helper, new object());
        helperType.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, helper);
        Assembly plugin = Assembly.LoadFrom(pluginPath);
        Type entry = plugin.GetType("CreatureManager.CreatureManagerPlugin", true)!;
        var log = BepInEx.Logging.Logger.CreateLogSource("CreatureManager compatibility check");
        log.LogEvent += (_, e) =>
        {
            System.Console.WriteLine($"LOG {e.Level}: {e.Data}");
            if ((e.Level & (BepInEx.Logging.LogLevel.Error | BepInEx.Logging.LogLevel.Warning)) != 0) Fail("Patch resolver/transpiler warning");
        };
        entry.GetProperty("Log", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.SetValue(null, log);
        CheckPatches(plugin);
        LootContracts.Run(plugin, dropThatPath);
        ManagedContracts.Run(plugin);
    }

    private static void CheckReferences(string pluginPath)
    {
        using var resolver = new DefaultAssemblyResolver();
        foreach (string path in SearchPaths) resolver.AddSearchDirectory(path);
        using var module = ModuleDefinition.ReadModule(pluginPath, new ReaderParameters { AssemblyResolver = resolver });
        int references = 0;
        foreach (var type in module.GetTypes())
        foreach (var method in type.Methods.Where(m => m.HasBody))
        foreach (var instruction in method.Body.Instructions)
        {
            if (!(instruction.Operand is MemberReference member)) continue;
            string scope = (member is TypeReference referencedType ? referencedType.Scope?.Name : member.DeclaringType?.Scope?.Name) ?? "";
            if (!(scope.StartsWith("assembly_", StringComparison.Ordinal) || scope.StartsWith("Unity", StringComparison.Ordinal) || scope == "gui_framework" || scope == "Splatform")) continue;
            references++;
            try
            {
                if (member is FieldReference field)
                {
                    var resolved = field.Resolve();
                    if (resolved == null) Fail($"Unresolved field: {member} in {method.FullName}");
                    else if (resolved.IsLiteral && (instruction.OpCode.Code == Mono.Cecil.Cil.Code.Ldsfld || instruction.OpCode.Code == Mono.Cecil.Cil.Code.Stsfld || instruction.OpCode.Code == Mono.Cecil.Cil.Code.Ldsflda)) Fail($"Literal used as storage: {member}");
                }
                else if (member is MethodReference called && called.Resolve() == null) Fail($"Unresolved method: {member} in {method.FullName}");
                else if (member is TypeReference referred && referred.Resolve() == null) Fail($"Unresolved type: {member}");
            }
            catch (Exception e) { Fail($"Resolve {member}: {e.Message}"); }
        }
        System.Console.WriteLine($"Checked {references} game/Unity IL references against {SearchPaths[0]}.");
        string[] access = module.Assembly.CustomAttributes.Where(a => a.AttributeType.Name == "IgnoresAccessChecksToAttribute").Select(a => (string)a.ConstructorArguments[0].Value).ToArray();
        if (!access.Contains("assembly_valheim") || !access.Contains("assembly_guiutils")) Fail("Missing publicizer runtime attributes in merged plugin.");
        System.Console.WriteLine("Final access attributes: " + string.Join(", ", access));
    }

    private static void CheckPatches(Assembly plugin)
    {
        int count = 0, transpilers = 0;
        foreach (Type type in plugin.GetTypes())
        {
            var attributes = type.GetCustomAttributes(typeof(HarmonyPatch), false).Cast<HarmonyPatch>().ToArray();
            if (attributes.Length == 0) continue;
            try
            {
                var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo? provider = methods.FirstOrDefault(m => m.Name == "TargetMethods" || m.Name == "TargetMethod");
                List<MethodBase> targets;
                if (provider != null)
                {
                    object result = provider.Invoke(null, Array.Empty<object>())!;
                    targets = result is IEnumerable sequence ? sequence.Cast<MethodBase>().ToList() : new List<MethodBase> { (MethodBase)result };
                    if (targets.Count == 0) Fail("No target: " + type.FullName);
                }
                else
                {
                    var individual = methods.Where(m => m.IsDefined(typeof(HarmonyPatch), false)).ToArray();
                    if (individual.Length > 0)
                    {
                        foreach (MethodInfo patch in individual)
                        {
                            var patchInfo = new HarmonyMethod();
                            foreach (var a in patch.GetCustomAttributes(typeof(HarmonyPatch), false).Cast<HarmonyPatch>()) a.info.CopyTo(patchInfo);
                            MethodInfo? original = AccessTools.DeclaredMethod(patchInfo.declaringType, patchInfo.methodName, patchInfo.argumentTypes);
                            if (original == null) { Fail("No method-level target: " + patch); continue; }
                            count++;
                            CheckParameters(original, patch);
                            System.Console.WriteLine($"TARGET {type.FullName}.{patch.Name} -> {original.DeclaringType}.{original}");
                        }
                        continue;
                    }
                    var info = new HarmonyMethod();
                    foreach (var a in attributes) a.info.CopyTo(info);
                    MethodBase? target = info.methodType == MethodType.Constructor
                        ? AccessTools.DeclaredConstructor(info.declaringType, info.argumentTypes)
                        : info.methodType == MethodType.Getter ? AccessTools.DeclaredPropertyGetter(info.declaringType, info.methodName)
                        : info.methodType == MethodType.Setter ? AccessTools.DeclaredPropertySetter(info.declaringType, info.methodName)
                        : AccessTools.DeclaredMethod(info.declaringType, info.methodName, info.argumentTypes);
                    if (target == null) { Fail("No target: " + type.FullName); continue; }
                    targets = new List<MethodBase> { target };
                }
                foreach (MethodBase target in targets)
                {
                    count++;
                    System.Console.WriteLine($"TARGET {type.FullName} -> {target.DeclaringType}.{target}");
                    foreach (MethodInfo patch in methods.Where(m => m.Name == "Prefix" || m.Name == "Postfix" || m.Name == "Finalizer")) CheckParameters(target, patch);
                    MethodInfo? transpiler = methods.FirstOrDefault(m => m.Name == "Transpiler");
                    if (transpiler == null) continue;
                    var original = PatchProcessor.GetOriginalInstructions(target);
                    object?[] arguments = transpiler.GetParameters().Select(p => p.ParameterType == typeof(MethodBase) ? (object)target : original).ToArray();
                    var rewritten = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, arguments)!).ToList();
                    if (rewritten.Count <= original.Count) Fail("Transpiler did not inject instructions: " + type.FullName + " -> " + target.Name);
                    transpilers++;
                    System.Console.WriteLine($"TRANSPILE {target.Name}: {original.Count} -> {rewritten.Count}");
                }
            }
            catch (Exception e) { Fail(type.FullName + ": " + e); }
        }
        System.Console.WriteLine($"Checked {count} Harmony targets and {transpilers} transpiler applications (without installing native detours).");
    }

    private static void CheckParameters(MethodBase original, MethodInfo patch)
    {
        foreach (ParameterInfo parameter in patch.GetParameters())
        {
            string name = parameter.Name!;
            if (name == "__instance")
            {
                if (!parameter.ParameterType.IsAssignableFrom(original.DeclaringType)) Fail("Instance type mismatch: " + patch);
                continue;
            }
            if (name == "__result")
            {
                Type resultType = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
                if (!(original is MethodInfo method) || !resultType.IsAssignableFrom(method.ReturnType)) Fail("Result type mismatch: " + patch);
                continue;
            }
            if (name.StartsWith("___", StringComparison.Ordinal))
            {
                FieldInfo? field = AccessTools.Field(original.DeclaringType, name.Substring(3));
                Type injectedType = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
                if (field == null || !injectedType.IsAssignableFrom(field.FieldType)) Fail("Injected field mismatch: " + patch + " " + name);
                continue;
            }
            if (name == "__state")
            {
                var prefix = patch.DeclaringType!.GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var state = prefix?.GetParameters().FirstOrDefault(p => p.Name == "__state");
                if (state != null)
                {
                    Type saved = state.ParameterType.IsByRef ? state.ParameterType.GetElementType()! : state.ParameterType;
                    Type read = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
                    if (saved != read) Fail("State type mismatch: " + patch);
                }
                continue;
            }
            if (name.StartsWith("__", StringComparison.Ordinal)) continue;
            var source = original.GetParameters().FirstOrDefault(p => p.Name == name);
            if (source == null) { Fail($"Missing parameter {name}: {patch.DeclaringType}.{patch.Name} -> {original}"); continue; }
            Type a = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
            Type b = source.ParameterType.IsByRef ? source.ParameterType.GetElementType()! : source.ParameterType;
            if (!a.IsAssignableFrom(b)) Fail($"Parameter type {name}: {a} != {b}");
        }
    }

    private static void Fail(string message) { Failures++; System.Console.Error.WriteLine("FAIL " + message); }
}
