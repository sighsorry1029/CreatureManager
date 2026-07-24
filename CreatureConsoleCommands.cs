using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace CreatureManager;

internal static class CreatureConsoleCommands
{
    private enum SpawnAutocompleteField
    {
        None,
        Prefab,
        Modifier
    }

    private const string SpawnCommandName = "cm:spawn";
    private const int MaximumCommandSpawnLevel = 100;
    private static readonly List<string> ReferenceDomainOptions = new() { "creature", "ai", "attack", "loadout", "projectile", "texture", "levelvisual" };
    private static readonly List<string> FullDomainOptions = new() { "creature" };
    private static readonly List<string> EmptyAutocompleteOptions = new();
    private static readonly char[] ArgumentSeparators = { ' ', '\t' };
    private static Terminal.ConsoleCommand? SpawnCommand;
    private static bool Registered;

    internal static void Register()
    {
        if (Registered)
        {
            return;
        }

        Registered = true;
        new Terminal.ConsoleCommand(
            "cm:reference",
            "Write generated CreatureManager reference files. Usage: cm:reference creature|ai|attack|loadout|projectile|texture|levelvisual",
            WriteReference,
            optionsFetcher: GetReferenceDomainOptions);
        new Terminal.ConsoleCommand(
            "cm:full",
            "Write generated CreatureManager full scaffold YAML. Usage: cm:full creature",
            WriteFull,
            optionsFetcher: GetFullDomainOptions);
        SpawnCommand = new Terminal.ConsoleCommand(
            SpawnCommandName,
            $"Spawn a creature with optional modifiers and an exact trailing level from 1 to {MaximumCommandSpawnLevel}. Usage: cm:spawn <prefab> [modifier1,modifier2,modifier3,modifier4] [level]",
            Spawn,
            isCheat: true,
            isNetwork: true,
            onlyServer: true,
            optionsFetcher: GetSpawnPrefabOptions,
            remoteCommand: true,
            onlyAdmin: true);
        new Terminal.ConsoleCommand(
            "cm:karma",
            "Show or set current 3x3 zone-neighborhood Karma. Usage: cm:karma [value]",
            Karma,
            isCheat: true,
            isNetwork: true,
            onlyServer: true,
            remoteCommand: true,
            onlyAdmin: true);
    }

    private static List<string> GetReferenceDomainOptions()
    {
        return ReferenceDomainOptions;
    }

    private static List<string> GetFullDomainOptions()
    {
        return FullDomainOptions;
    }

    private static List<string> GetSpawnPrefabOptions()
    {
        List<GameObject> prefabs = CreaturePrefabRegistry.GetCreaturePrefabs();
        List<string> options = new(prefabs.Count);
        foreach (GameObject prefab in prefabs)
        {
            options.Add(prefab.name);
        }

        return options;
    }

    internal static void InvalidateSpawnAutocompleteOptions()
    {
        if (SpawnCommand != null)
        {
            SpawnCommand.m_tabOptions = null;
        }
    }

    internal static void AdjustSpawnAutocomplete(Terminal terminal, ref string word, ref List<string> options)
    {
        if (!TryGetSpawnAutocompleteContext(terminal, out SpawnAutocompleteField field, out string currentToken))
        {
            return;
        }

        switch (field)
        {
            case SpawnAutocompleteField.Prefab:
                word = currentToken;
                if (options == null || options.Count == 0)
                {
                    options = GetSpawnPrefabOptions();
                }
                break;
            case SpawnAutocompleteField.Modifier:
                options = GetModifierAutocompleteOptions(currentToken, out word);
                break;
            default:
                word = currentToken;
                options = EmptyAutocompleteOptions;
                break;
        }
    }

    private static bool TryGetSpawnAutocompleteContext(
        Terminal terminal,
        out SpawnAutocompleteField field,
        out string currentToken)
    {
        field = SpawnAutocompleteField.None;
        currentToken = "";
        if (terminal.m_input == null)
        {
            return false;
        }

        TMP_InputField input = (TMP_InputField)terminal.m_input;
        string inputText = input.text ?? "";
        int caretPosition = Mathf.Clamp(input.caretPosition, 0, inputText.Length);
        string commandLine = inputText.Substring(0, caretPosition).TrimStart();
        if (terminal.m_tabPrefix != '\0' && commandLine.Length > 0 && commandLine[0] == terminal.m_tabPrefix)
        {
            commandLine = commandLine.Substring(1);
        }

        if (!commandLine.StartsWith(SpawnCommandName, StringComparison.OrdinalIgnoreCase) ||
            commandLine.Length <= SpawnCommandName.Length ||
            !char.IsWhiteSpace(commandLine[SpawnCommandName.Length]))
        {
            return false;
        }

        string argumentText = commandLine.Substring(SpawnCommandName.Length);
        string arguments = argumentText.TrimStart(ArgumentSeparators);
        if (arguments.Length == 0)
        {
            field = SpawnAutocompleteField.Prefab;
            return true;
        }

        int prefabEnd = IndexOfWhitespace(arguments);
        if (prefabEnd < 0)
        {
            field = SpawnAutocompleteField.Prefab;
            currentToken = arguments;
            return true;
        }

        string trailing = arguments.Substring(prefabEnd);
        string modifierOrLevel = trailing.TrimStart(ArgumentSeparators);
        if (modifierOrLevel.Length == 0)
        {
            field = SpawnAutocompleteField.Modifier;
            return true;
        }

        int lastNonWhitespace = LastNonWhitespaceIndex(modifierOrLevel);
        if (lastNonWhitespace < modifierOrLevel.Length - 1)
        {
            if (modifierOrLevel[lastNonWhitespace] == ',')
            {
                field = SpawnAutocompleteField.Modifier;
                currentToken = modifierOrLevel;
            }

            return true;
        }

        int currentPartStart = lastNonWhitespace;
        while (currentPartStart >= 0 && !char.IsWhiteSpace(modifierOrLevel[currentPartStart]))
        {
            currentPartStart--;
        }

        string currentPart = modifierOrLevel.Substring(currentPartStart + 1);
        if (currentPartStart >= 0)
        {
            int previousNonWhitespace = LastNonWhitespaceIndex(modifierOrLevel, currentPartStart);
            if (previousNonWhitespace < 0 || modifierOrLevel[previousNonWhitespace] != ',')
            {
                return true;
            }
        }
        else if (int.TryParse(currentPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return true;
        }

        field = SpawnAutocompleteField.Modifier;
        currentToken = modifierOrLevel;
        return true;
    }

    private static int IndexOfWhitespace(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (char.IsWhiteSpace(value[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static int LastNonWhitespaceIndex(string value, int exclusiveEnd = -1)
    {
        int index = exclusiveEnd < 0 ? value.Length - 1 : Mathf.Min(exclusiveEnd - 1, value.Length - 1);
        while (index >= 0 && char.IsWhiteSpace(value[index]))
        {
            index--;
        }

        return index;
    }

    private static List<string> GetModifierAutocompleteOptions(string modifierToken, out string currentModifier)
    {
        string[] parts = modifierToken.Split(new[] { ',' }, StringSplitOptions.None);
        currentModifier = parts[parts.Length - 1].Trim();
        HashSet<string> selected = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < parts.Length - 1; index++)
        {
            string modifier = parts[index].Trim();
            if (modifier.Length > 0)
            {
                selected.Add(modifier);
            }
        }

        if (selected.Count >= 4)
        {
            return EmptyAutocompleteOptions;
        }

        IReadOnlyList<string> knownModifiers = CreatureModifierManager.GetKnownModifierKeys();
        List<string> options = new(knownModifiers.Count - selected.Count);
        foreach (string modifier in knownModifiers)
        {
            if (!selected.Contains(modifier))
            {
                options.Add(modifier);
            }
        }

        return options;
    }

    private static void WriteReference(Terminal.ConsoleEventArgs args)
    {
        CreatureAssetOwnerCatalog.RefreshMappings();
        string scope = GetScope(args);
        if (!IsKnownScope(scope, args, "cm:reference", includeTexture: true))
        {
            return;
        }

        string path;
        string error;
        bool wrote = scope switch
        {
            "ai" => CreatureDomainManager.TryWriteAiReferenceConfigurationFile(out path, out error),
            "attack" => CreatureDomainManager.TryWriteAttackReferenceConfigurationFile(out path, out error),
            "loadout" => CreatureDomainManager.TryWriteCreatureLoadoutReferenceConfigurationFile(out path, out error),
            "projectile" => CreatureDomainManager.TryWriteProjectileReferenceConfigurationFile(out path, out error),
            "texture" => CreatureDomainManager.TryWriteTextureReferenceConfigurationFile(out path, out error),
            "levelvisual" => CreatureDomainManager.TryWriteLevelVisualReferenceConfigurationFile(out path, out error),
            _ => CreatureDomainManager.TryWriteReferenceConfigurationFile(out path, out error)
        };

        if (wrote)
        {
            args.Context?.AddString($"Wrote {NormalizeScope(scope)} reference to {path}");
        }
        else
        {
            args.Context?.AddString(error);
        }
    }

    private static void WriteFull(Terminal.ConsoleEventArgs args)
    {
        CreatureAssetOwnerCatalog.RefreshMappings();
        string scope = GetScope(args);
        if (scope != "creature")
        {
            args.Context?.AddString("Syntax: cm:full creature");
            return;
        }

        string path;
        string error;
        bool wrote = CreatureDomainManager.TryWriteFullScaffoldConfigurationFile(out path, out error);

        if (wrote)
        {
            args.Context?.AddString($"Wrote {NormalizeScope(scope)} full scaffold to {path}");
        }
        else
        {
            args.Context?.AddString(error);
        }
    }

    private static void Karma(Terminal.ConsoleEventArgs args)
    {
        if (!RequireAuthoritativeAdmin(args))
        {
            return;
        }

        Player? player = Player.m_localPlayer;
        if (player == null)
        {
            args.Context?.AddString("No local player.");
            return;
        }

        ExecuteKarma(args, player.transform.position, message => args.Context?.AddString(message));
    }

    private static void ExecuteKarma(Terminal.ConsoleEventArgs args, Vector3 playerPosition, Action<string> reply)
    {
        if (args.Length == 1)
        {
            reply(CreatureKarmaManager.GetDebugLine(playerPosition));
            return;
        }

        if (args.Length != 2 ||
            !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
            float.IsNaN(value) ||
            float.IsInfinity(value) ||
            value < 0f)
        {
            reply("Syntax: cm:karma [non-negative value]");
            return;
        }

        CreatureKarmaManager.SetDebugKarma(playerPosition, value);
        reply(CreatureKarmaManager.GetDebugLine(playerPosition));
    }

    private static void Spawn(Terminal.ConsoleEventArgs args)
    {
        if (!RequireAuthoritativeAdmin(args))
        {
            return;
        }

        if (!CreatureLevelManager.IsLevelSystemEnabled())
        {
            args.Context?.AddString("CreatureManager level system is disabled.");
            return;
        }

        Player? player = Player.m_localPlayer;
        if (player == null)
        {
            args.Context?.AddString("No local player.");
            return;
        }

        ExecuteSpawn(
            args,
            player.transform.position,
            player.transform.rotation,
            message => args.Context?.AddString(message));
    }

    private static void ExecuteSpawn(
        Terminal.ConsoleEventArgs args,
        Vector3 playerPosition,
        Quaternion playerRotation,
        Action<string> reply)
    {
        if (!TryParseSpawnArguments(args, out string prefabName, out int level, out List<string> modifiers, out string error))
        {
            reply(error);
            return;
        }

        GameObject? prefab = CreaturePrefabRegistry.GetPrefab(prefabName);
        if (prefab == null)
        {
            reply($"Creature prefab '{prefabName}' was not found.");
            return;
        }

        if (prefab.GetComponent<Character>() == null || CreaturePrefabRegistry.IsPlayerPrefab(prefab))
        {
            reply($"Prefab '{prefabName}' is not a supported non-player creature.");
            return;
        }

        Vector3 position = GetCommandSpawnPosition(playerPosition, playerRotation * Vector3.forward);
        Quaternion rotation = Quaternion.Euler(0f, playerRotation.eulerAngles.y, 0f);
        GameObject? spawned = null;
        CreatureManagerSpawnLifecycle.BeginSourceContext(CreatureSpawnSourceKind.Managed);
        try
        {
            spawned = UnityEngine.Object.Instantiate(prefab, position, rotation);
        }
        catch (Exception ex)
        {
            error = $"Failed to spawn '{prefabName}': {ex.Message}";
        }
        finally
        {
            CreatureManagerSpawnLifecycle.EndSourceContext();
        }

        if (spawned == null)
        {
            reply(error.Length > 0 ? error : $"Failed to spawn '{prefabName}'.");
            return;
        }

        Character? character = spawned.GetComponent<Character>();
        if (character == null ||
            !CreatureModifierManager.TryApplyForcedModifiers(character, modifiers, out error) ||
            !CreatureLevelManager.TryApplyForcedLevel(character, level, out error))
        {
            CleanupFailedSpawn(spawned);
            reply(error.Length > 0 ? error : $"Failed to initialize '{prefabName}'.");
            return;
        }

        CreatureManagerCharacterLifecycle.ApplyLevelAndModifiers(character);
        string modifierText = modifiers.Count > 0 ? string.Join(", ", modifiers) : "none";
        reply($"Spawned {prefab.name} at level {level} with modifiers: {modifierText}.");
    }

    private static void CleanupFailedSpawn(GameObject spawned)
    {
        try
        {
            ZNetView? nview = spawned.GetComponent<ZNetView>();
            if (ZNetScene.instance != null &&
                nview != null &&
                nview.IsValid() &&
                nview.GetZDO() != null)
            {
                ZNetScene.instance.Destroy(spawned);
                return;
            }
        }
        catch
        {
            // Fall back to local destruction when network registration is incomplete.
        }

        UnityEngine.Object.Destroy(spawned);
    }

    internal static bool TryHandleAuthenticatedRemoteAdminCommand(ZNet znet, ZRpc rpc, string command)
    {
        string commandName = GetRemoteCommandName(command);
        if (!string.Equals(commandName, SpawnCommandName, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(commandName, "cm:karma", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!znet.IsServer() || rpc == null)
        {
            return false;
        }

        Action<string> reply = message =>
        {
            try
            {
                znet.RemotePrint(rpc, message);
            }
            catch
            {
                // The authenticated peer may disconnect while the server is executing
                // the command. Command handling must not escape through the Harmony prefix.
            }
        };
        try
        {
            // ZNet.InternalCommand is reached only after vanilla or ServerDevcommands has
            // authorized the RPC. Rechecking the vanilla admin list here would reject
            // permissions granted by ServerDevcommands.
            ZNetPeer? peer = znet.GetPeer(rpc);
            if (peer == null ||
                !peer.IsReady() ||
                peer.m_characterID.IsNone() ||
                ZDOMan.instance == null ||
                ZNetScene.instance == null)
            {
                reply("Could not resolve the remote admin's active player.");
                return true;
            }

            ZDO playerZdo = ZDOMan.instance.GetZDO(peer.m_characterID);
            GameObject? playerPrefab = playerZdo != null
                ? ZNetScene.instance.GetPrefab(playerZdo.GetPrefab())
                : null;
            if (playerZdo == null ||
                playerZdo.GetOwner() != peer.m_uid ||
                playerPrefab == null ||
                playerPrefab.GetComponent<Player>() == null)
            {
                reply("Could not validate the remote admin's active player.");
                return true;
            }

            Vector3 playerPosition = playerZdo.GetPosition();
            Quaternion playerRotation = playerZdo.GetRotation();
            if (!IsFinite(playerPosition))
            {
                reply("Could not validate the remote admin's player position.");
                return true;
            }

            if (!IsFinite(playerRotation))
            {
                playerRotation = Quaternion.identity;
            }

            Terminal.ConsoleEventArgs args = new(command, Console.instance);
            if (string.Equals(commandName, SpawnCommandName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CreatureLevelManager.IsLevelSystemEnabled())
                {
                    reply("CreatureManager level system is disabled.");
                    return true;
                }

                ExecuteSpawn(args, playerPosition, playerRotation, reply);
            }
            else
            {
                ExecuteKarma(args, playerPosition, reply);
            }

            string hostName = rpc.GetSocket()?.GetHostName() ?? "";
            CreatureManagerPlugin.Log.LogInfo(
                $"Remote admin '{hostName}' executed CreatureManager command '{commandName}'.");
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"Failed to execute remote CreatureManager command '{commandName}': {ex.Message}");
            reply("CreatureManager could not execute the remote command. Check the server log.");
        }

        return true;
    }

    private static string GetRemoteCommandName(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return "";
        }

        int separator = command.IndexOfAny(ArgumentSeparators);
        return (separator >= 0 ? command.Substring(0, separator) : command).Trim();
    }

    private static bool RequireAuthoritativeAdmin(Terminal.ConsoleEventArgs args)
    {
        if (ZNet.instance != null &&
            ZNet.instance.IsServer() &&
            ZNet.instance.LocalPlayerIsAdminOrHost())
        {
            return true;
        }

        args.Context?.AddString("This command must be run locally by the server host.");
        return false;
    }

    private static bool TryParseSpawnArguments(
        Terminal.ConsoleEventArgs args,
        out string prefabName,
        out int level,
        out List<string> modifiers,
        out string error)
    {
        prefabName = args.Length >= 2 ? (args[1] ?? "").Trim() : "";
        level = 1;
        modifiers = new List<string>();
        error = "";
        if (prefabName.Length == 0)
        {
            error = "Syntax: cm:spawn <prefab> [modifier1,modifier2,modifier3,modifier4] [level]";
            return false;
        }

        int modifierEnd = args.Length;
        if (args.Length >= 3 &&
            int.TryParse(args[args.Length - 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedLevel))
        {
            if (parsedLevel < 1 || parsedLevel > MaximumCommandSpawnLevel)
            {
                error = $"level must be an integer from 1 to {MaximumCommandSpawnLevel}.";
                return false;
            }

            level = parsedLevel;
            modifierEnd--;
        }

        if (modifierEnd > 2)
        {
            List<string> modifierParts = new(modifierEnd - 2);
            for (int index = 2; index < modifierEnd; index++)
            {
                modifierParts.Add(args[index] ?? "");
            }

            string modifierList = string.Join(" ", modifierParts).Trim();
            string[] requested = modifierList.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (modifierList.Length == 0 || requested.Length == 0 ||
                !CreatureModifierManager.TryNormalizeForcedModifierKeys(requested, out modifiers, out error))
            {
                error = error.Length > 0 ? error : "At least one modifier must be specified.";
                return false;
            }
        }

        return true;
    }

    private static Vector3 GetCommandSpawnPosition(Vector3 playerPosition, Vector3 playerForward)
    {
        Vector3 forward = playerForward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        Vector3 position = playerPosition + forward * 3f;
        if (position.y >= 4000f)
        {
            return position + Vector3.up * 0.5f;
        }

        if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(position + Vector3.up * 100f, out float groundHeight))
        {
            position.y = groundHeight + 0.5f;
        }
        else if (WorldGenerator.instance != null)
        {
            position.y = WorldGenerator.instance.GetHeight(position.x, position.z) + 0.5f;
        }
        else
        {
            position.y += 0.5f;
        }

        return position;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(Quaternion value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static string GetScope(Terminal.ConsoleEventArgs args)
    {
        return args.Length >= 2 ? (args[1] ?? "").Trim().ToLowerInvariant() : "";
    }

    private static bool IsKnownScope(string scope, Terminal.ConsoleEventArgs args, string command, bool includeTexture)
    {
        if (scope is "creature" or "ai" or "attack")
        {
            return true;
        }

        if (includeTexture && scope is ("loadout" or "projectile" or "texture" or "levelvisual"))
        {
            return true;
        }

        args.Context?.AddString(includeTexture
            ? $"Syntax: {command} creature|ai|attack|loadout|projectile|texture|levelvisual"
            : $"Syntax: {command} creature|ai|attack");
        return false;
    }

    private static string NormalizeScope(string scope)
    {
        return scope switch
        {
            "ai" => "ai",
            "attack" => "attack",
            "loadout" => "loadout",
            "projectile" => "projectile",
            "texture" => "texture",
            "levelvisual" => "levelvisual",
            _ => "creature"
        };
    }

}
