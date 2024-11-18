using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Newtonsoft.Json;
using System.IO;
using static Coyote.ChatWatcher;
using System.Net.Http;
using System.Text;

namespace Coyote;

public class ChatWatcher : IDisposable
{
    private readonly SortedSet<XivChatType> _watchedChannels = new();
    private bool _watchAllChannels;
    private string fireResponse;
    private Configuration _configuration;
    private readonly HttpClient httpClient = new HttpClient();
    public ChatWatcher(Configuration configuration)
    {
        _configuration = configuration;
        Plugin.Chat.CheckMessageHandled += OnCheckMessageHandled;
        Plugin.Chat.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        Plugin.Chat.CheckMessageHandled -= OnCheckMessageHandled;
        Plugin.Chat.ChatMessage -= OnChatMessage;
    }

    private static void CopySublist(IReadOnlyList<Payload> payloads, List<Payload> newPayloads, int from, int to)
    {
        while (from < to)
            newPayloads.Add(payloads[from++]);
    }


    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {

        Plugin.Log.Debug($"OnChatMessage {type}, {sender}, {message}, {isHandled}");

        foreach (var rule in _configuration.chatTriggerRules)
        {
            // 检查规则是否启用
            if (!rule.IsEnabled)
            {
                continue;
            }

            // 检查聊天类型是否匹配
            if (rule.ChatType != type)
            {
                continue;
            }

            // 检查发送者是否匹配
            if (rule.CheckSender && !sender.TextValue.Equals(rule.SenderName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // 检查消息内容是否匹配
            if (rule.MatchEntireMessage)
            {
                if (!message.TextValue.Equals(rule.Keyword, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }
            else
            {
                if (!message.TextValue.Contains(rule.Keyword, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            // 如果匹配，则处理
            Plugin.Chat.Print($"触发规则：类型 {rule.ChatType}, 发送者 {sender}, 消息 {message.TextValue}");

            // 调用开火逻辑
            TriggerFireAction(rule);
        }
    }

    private void OnCheckMessageHandled(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        Plugin.Log.Debug($"OnCheckMessageHandled {type}, {sender}, {message}, {isHandled}");
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
    private const string ConfigFilePath = "chatTriggerRules.json";

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
    private const string ConfigFilePath = "hpTriggerRules.json";

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
[Serializable]
public class ChatTriggerRule
{
    public XivChatType ChatType { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
    public bool MatchEntireMessage { get; set; }
    public bool CheckSender { get; set; }
    public bool IsEnabled { get; set; } = true; // 默认启用规则

    // 新增字段
    public int FireStrength { get; set; } = 0;
    public int FireTime { get; set; } = 0;
    public bool OverrideTime { get; set; } = false;
    public string PulseId { get; set; } = string.Empty;
}

[Serializable]
public class HealthTriggerRule
{
    public string Name { get; set; } = "新规则";
    public bool IsEnabled { get; set; } = true;
    public int TriggerMode { get; set; } = 1; // 1: 减少 2: 增加 3: 始终触发
    public int TriggerThreshold { get; set; } = 100; // 触发阈值（血量变化值）
    public int MinPercentage { get; set; } = 0; // 血量触发区间下限
    public int MaxPercentage { get; set; } = 100; // 血量触发区间上限

    // 开火相关配置
    public int FireStrength { get; set; } = 10; // 开火强度
    public int FireTime { get; set; } = 1000; // 开火时间，单位毫秒
    public bool OverrideTime { get; set; } = false; // 是否重置时间
    public string PulseId { get; set; } = string.Empty; // 波形ID
}

