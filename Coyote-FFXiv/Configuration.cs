using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using Dalamud.Game.Text;

namespace Coyote;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    // ImGui 控制参数
    public int fireStrength { get; set; } = 20; // 一键开火强度
    public int fireTime { get; set; } = 500; // 一键开火时间
    public bool overrideTime { get; set; } = false; // 是否重置时间
    public string pulseId { get; set; } = ""; // 波形 ID
    public string HttpServer { get; set; } = "http://127.0.0.1:8920"; // coyote server
    public string ClientID { get; set; } = ""; // 客户端ID

    public string Log { get; set; } = "";
    public static HashSet<uint> FavIcons { get; set; } = new HashSet<uint>();
    public bool UseAll { get; set; } = false;
    public List<ChatTriggerRule> chatTriggerRules;
    public List<HealthTriggerRule> HealthTriggerRules { get; set; } = new List<HealthTriggerRule>();

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
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
