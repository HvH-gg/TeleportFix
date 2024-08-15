using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;
using CSSharpUtils.Extensions;
using CSSharpUtils.Utils;

namespace TeleportFix;

public class TeleportFixConfig : BasePluginConfig
{
    [JsonPropertyName("ChatPrefix")] public string ChatPrefix { get; set; } = "[{Red}Hv{DarkRed}H{Default}.gg]";
    [JsonPropertyName("ConfigVersion")] public override int Version { get; set; } = 1;
}

public class TeleportFix : BasePlugin, IPluginConfig<TeleportFixConfig>
{
    public override string ModuleName => "HvH.gg - Teleport/Crasher Fix";
    public override string ModuleVersion => "1.0.4";
    public override string ModuleAuthor => "imi-tat0r";
    
    public TeleportFixConfig Config { get; set; } = new();
    
    public required MemoryFunctionVoid<CCSPlayer_MovementServices, IntPtr> RunCommand;
    private readonly Dictionary<uint, float> _teleportBlockWarnings = new();

    public override void Load(bool hotReload)
    {
        base.Load(hotReload);

        Console.WriteLine("[HvH.gg] Hooking run command");
        
        RunCommand = new(GameData.GetSignature("RunCommand"));
        RunCommand.Hook(OnRunCommand, HookMode.Pre);
    }

    public void OnConfigParsed(TeleportFixConfig config)
    {
        Config = config;
        config.Update();
    }
    
    private HookResult OnRunCommand(DynamicHook h)
    {
        // check if the player is a valid player
        var player = h.GetParam<CCSPlayer_MovementServices>(0).Pawn.Value.Controller.Value?.As<CCSPlayerController>();
        if (!player.IsPlayer())
            return HookResult.Continue;
        
        // get the user command and view angles
        var userCmd = new CUserCmd(h.GetParam<IntPtr>(1));
        var viewAngles = userCmd.GetViewAngles();
        
        // no valid view angles or not infinite
        if (viewAngles is null || viewAngles.IsValid()) 
            return HookResult.Continue;
        
        // fix the view angles (prevents the player from using teleport or airstuck)
        viewAngles.Fix();

        // not warned yet or last warning was more than 3 seconds ago
        if (_teleportBlockWarnings.TryGetValue(player!.Index, out var lastWarningTime) &&
            !(lastWarningTime + 3 <= Server.CurrentTime)) 
            return HookResult.Changed;
        
        // print a warning to all players
        var feature = player.Pawn.Value!.As<CCSPlayerPawn>().OnGroundLastTick ? "teleport" : "airstuck";
        Server.PrintToChatAll($"{ChatUtils.FormatMessage(Config.ChatPrefix)} Player {ChatColors.Red}{player.PlayerName}{ChatColors.Default} tried using {ChatColors.Red}{feature}{ChatColors.Default}!");
        _teleportBlockWarnings[player.Index] = Server.CurrentTime;

        return HookResult.Changed;
    }
    
    [ConsoleCommand("css_reload_cfg", "Reload the config in the current session without restarting the server")]
    [RequiresPermissions("@css/generic")]
    [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnReloadConfigCommand(CCSPlayerController? player, CommandInfo info)
    {
        try
        {
            OnConfigParsed(new TeleportFixConfig().Reload());
        }
        catch (Exception e)
        {
            info.ReplyToCommand($"[HvH.gg] Failed to reload config: {e.Message}");
        }
    }
    
    public override void Unload(bool hotReload)
    {
        base.Unload(hotReload);
        RunCommand.Unhook(OnRunCommand, HookMode.Pre);
    }
}