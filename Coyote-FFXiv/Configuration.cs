using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using static Coyote.ChatWatcher;
using System.Collections.Generic;

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

    public bool UseAll { get; set; } = false;
    public List<ChatTriggerRule> chatTriggerRules;
    public List<HealthTriggerRule> HealthTriggerRules { get; set; } = new List<HealthTriggerRule>();

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }


}
