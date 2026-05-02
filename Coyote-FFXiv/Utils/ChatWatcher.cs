using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;

namespace Coyote.Utils;

public class ChatWatcher : IDisposable
{
    private readonly SortedSet<XivChatType> _watchedChannels = new();
    private readonly Hook<PrintMessageDelegate> _printMessageHook;
    private bool _watchAllChannels;
    private string fireResponse;
    private Configuration _configuration;
    private readonly HttpClient httpClient = new HttpClient();
    public unsafe ChatWatcher(Configuration configuration)
    {
        _configuration = configuration;
        
        //_printMessageHook = Plugin.Hook.HookFromAddress<PrintMessageDelegate>(Plugin.SigScanner.ScanText("E8 ?? ?? ?? ?? 8B D8 48 8D 4D 00 ?? ?? ?? ?? ??"), this.HandlePrintMessageDetour);
        Plugin.Chat.CheckMessageHandled += OnCheckMessageHandled;
        Plugin.Chat.ChatMessage += OnChatMessage;
        //_printMessageHook.Enable();
    }
    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    private unsafe delegate uint PrintMessageDelegate(RaptureLogModule* manager, XivChatType chatType, Utf8String* sender, Utf8String* message, int timestamp, byte silent);
    public void Dispose()
    {
        Plugin.Chat.CheckMessageHandled -= OnCheckMessageHandled;
        Plugin.Chat.ChatMessage -= OnChatMessage;
        //_printMessageHook.Disable();
        //_printMessageHook.Dispose();
    }

    private static void CopySublist(IReadOnlyList<Payload> payloads, List<Payload> newPayloads, int from, int to)
    {
        while (from < to)
            newPayloads.Add(payloads[from++]);
    }


    private void OnChatMessage(IHandleableChatMessage message)
    {
        var type = message.LogKind;           // 原来是 type 参数
        var sender = message.Sender;          // SeString
        var msg = message.Message;         // SeString

        Plugin.Log.Debug($"OnChatMessage {type}, {sender}, {msg}, Handled={message.IsHandled}");
        foreach (var rule in _configuration.chatTriggerRules)
        {
            if (!rule.IsEnabled) continue;
            if (rule.ChatType != type) continue;

            // 检查发送者
            if (rule.CheckSender && !sender.TextValue.Equals(rule.SenderName, StringComparison.OrdinalIgnoreCase))
                continue;

            // 检查消息内容
            var messageText = msg.TextValue;
            if (rule.MatchEntireMessage)
            {
                if (!messageText.Equals(rule.Keyword, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            else
            {
                if (!messageText.Contains(rule.Keyword, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            Plugin.Chat.Print($"触发规则：类型 {rule.ChatType}, 发送者 {sender}, 消息 {messageText}");

            TriggerFireAction(rule);

            // 如果需要阻止原消息显示，可以调用：
            // message.PreventOriginal();
        }
    }

    private void OnCheckMessageHandled(IHandleableChatMessage message)
    {
        var type = message.LogKind;
        Plugin.Log.Debug($"OnCheckMessageHandled {type}, {message.Sender}, {message.Message}, Handled={message.IsHandled}");

        // 如果你在这里也需要做过滤，可以同样处理 message
    }



    private async void TriggerFireAction(ChatTriggerRule rule)
    {
        try
        {
            var requestContent = new
            {
                strength = rule.FireStrength, // 使用规则的开火强度
                time = rule.FireTime,         // 使用规则的开火时间
                @override = rule.OverrideTime, // 使用规则的重置时间配置
                pulseId = rule.PulseId        // 使用规则的波形ID
            };

            var jsonContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(requestContent),
                Encoding.UTF8,
                "application/json"
            );

            string url = $"{_configuration.HttpServer}/api/v2/game/{_configuration.ClientID}/action/fire";
            var response = await httpClient.PostAsync(url, jsonContent);

            if (response.IsSuccessStatusCode)
            {
                fireResponse = await response.Content.ReadAsStringAsync();

            }
            else
            {
                fireResponse = $"开火失败: {response.StatusCode}";
                Plugin.Log.Warning(fireResponse);
                _configuration.Log = fireResponse;
            }
        }
        catch (Exception ex)
        {
            fireResponse = $"开火错误: {ex.Message}";
            Plugin.Log.Warning(fireResponse);
            _configuration.Log = fireResponse;
        }
    }

}


public class ChatTriggerRuleManager
{
    private static string ConfigFilePath =>
        Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), "chatTriggerRules.json");

    public static void SaveRules(List<ChatTriggerRule> rules)
    {
        try
        {
            var json = JsonConvert.SerializeObject(rules, Formatting.Indented);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"保存规则失败: {ex.Message}");
        }
    }

    public static List<ChatTriggerRule> LoadRules()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
                return new List<ChatTriggerRule>();

            var json = File.ReadAllText(ConfigFilePath);
            return JsonConvert.DeserializeObject<List<ChatTriggerRule>>(json) ?? new List<ChatTriggerRule>();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"加载规则失败: {ex.Message}");
            return new List<ChatTriggerRule>();
        }
    }


}
public class HPTriggerRuleManager
{
    private static string ConfigFilePath =>
        Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), "hpTriggerRules.json");

    public static void SaveRules(List<HealthTriggerRule> rules)
    {
        try
        {
            var json = JsonConvert.SerializeObject(rules, Formatting.Indented);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"保存规则失败: {ex.Message}");
        }
    }

    public static List<HealthTriggerRule> LoadRules()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
                return new List<HealthTriggerRule>();

            var json = File.ReadAllText(ConfigFilePath);
            return JsonConvert.DeserializeObject<List<HealthTriggerRule>>(json) ?? new List<HealthTriggerRule>();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"加载规则失败: {ex.Message}");
            
            return new List<HealthTriggerRule>();
        }
    }


}


