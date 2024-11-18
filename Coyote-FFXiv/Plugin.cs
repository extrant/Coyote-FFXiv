using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons;
using Dalamud.Hooking;
using System;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using System.Linq;
using System.Runtime.InteropServices;
using static Coyote.ChatWatcher;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Coyote;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider Hook { get; private set; } = null!;
    [PluginService] public static IChatGui Chat { get; private set; } = null!;
    private const string CommandName = "/coyote";
    private const string CommandName2 = "/coyotefire";

    private readonly HttpClient httpClient = new HttpClient();
    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SamplePlugin");

    private MainWindow MainWindow { get; init; }

    
    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // you might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Coyote.png");

        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(MainWindow);


        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "/coyote 打开UI"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        Configuration.chatTriggerRules = ChatTriggerRuleManager.LoadRules();
        Configuration.HealthTriggerRules = HPTriggerRuleManager.LoadRules();
        Plugin.Log.Info("触发规则已加载。");
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        ChatTriggerRuleManager.SaveRules(Configuration.chatTriggerRules);
        HPTriggerRuleManager.SaveRules(Configuration.HealthTriggerRules);
        Plugin.Log.Info("触发规则已保存。");
    }

    private void OnCommand(string command, string args)
    {

        ToggleMainUI();
    }

    private string fireResponse;
    private async void OnSFire(string command, string args)
    {
        Log.Debug($"{command}+{args}");
        Plugin.Chat.Print($"{args[0]},{args[1]},{args[2]}");

        try
        {
            var requestContent = new
            {
                strength = args[1],
                time = args[0],
                @override = true,
                pulseId = ""
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestContent),
                Encoding.UTF8,
                "application/json"
            );

            string url = $"{Configuration.HttpServer}/api/v2/game/{Configuration.ClientID}/action/fire";
            var response = await httpClient.PostAsync(url, jsonContent);

            if (response.IsSuccessStatusCode)
            {
                fireResponse = await response.Content.ReadAsStringAsync();
                Plugin.Chat.Print(fireResponse);

            }
            else
            {
                fireResponse = $"开火失败: {response.StatusCode}";
                Plugin.Chat.PrintError(fireResponse);

            }
        }
        catch (Exception ex)
        {
            fireResponse = $"开火错误: {ex.Message}";
            Plugin.Chat.PrintError(fireResponse);
        }



    }
    private void DrawUI() => WindowSystem.Draw();

    public void ToggleMainUI() => MainWindow.Toggle();

}