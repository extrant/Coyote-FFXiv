using Dalamud.Plugin.Services;
using ECommons;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Coyote.Utils;
using System.Net.Http;
using Coyote;
namespace Coyote.Gui;
public class BuffTriggerUI
{
    private static string ConfigFilePath =>
    Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), "BuffTriggerConfig.json");
    private List<BuffTriggerRule> BuffTriggerRules = new();
    private int selectedRuleIndex = -1;
    private string fireResponse;
    private bool isBuffTriggerRunning = false;
    private readonly Dictionary<string, double> BuffCooldowns = new();
    private Configuration Configuration; // 配置对象
    private Plugin Plugin; // 插件实例
    private readonly HttpClient httpClient = new HttpClient();
    public BuffTriggerUI (Configuration configuration, Plugin plugin)
    {
        Configuration = configuration;
        Plugin = plugin;
        LoadConfig();
    }

    public void Draw()
    {
        ImGui.BeginGroup();

        if (ImGui.Checkbox("总触发开关##BuffTrigger", ref isBuffTriggerRunning))
        {
            if (isBuffTriggerRunning)
            {
                Plugin.Framework.Update += OnFrameworkUpdateForBuff;
            }
            else
            {
                Plugin.Framework.Update -= OnFrameworkUpdateForBuff;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("新增规则##AddBuffRule"))
        {
            var newRule = new BuffTriggerRule
            {
                Name = $"新规则 {BuffTriggerRules.Count + 1}",
                BuffID = 0,
                BuffName = string.Empty,
                IsEnabled = true
            };
            BuffTriggerRules.Add(newRule);
            selectedRuleIndex = BuffTriggerRules.Count - 1;
            SaveConfig();
        }

        ImGui.SameLine();
        if (ImGui.Button("删除选中规则##DeleteBuffRule"))
        {
            if (selectedRuleIndex >= 0 && selectedRuleIndex < BuffTriggerRules.Count)
            {
                BuffTriggerRules.RemoveAt(selectedRuleIndex);
                selectedRuleIndex = -1;
                SaveConfig();
            }
        }

        ImGui.EndGroup();
        ImGui.Separator();

        // 左侧规则列表
        ImGui.BeginChild("BuffRuleList", new Vector2(200, 0), true);
        for (int i = 0; i < BuffTriggerRules.Count; i++)
        {
            var rule = BuffTriggerRules[i];
            if (rule.IsEnabled)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 1, 0, 1)); // 绿色文本
            }

            if (ImGui.Selectable($"{i + 1}. {rule.Name}", selectedRuleIndex == i))
            {
                selectedRuleIndex = i;
            }

            if (rule.IsEnabled)
            {
                ImGui.PopStyleColor(); // 恢复默认颜色
            }
        }
        ImGui.EndChild();

        ImGui.SameLine();

        // 右侧规则详细信息
        ImGui.BeginChild("BuffRuleDetails", new Vector2(0, 0), true);
        if (selectedRuleIndex >= 0 && selectedRuleIndex < BuffTriggerRules.Count)
        {
            DrawRuleDetails(BuffTriggerRules[selectedRuleIndex]);
        }
        else
        {
            ImGui.Text("请选择一个规则进行编辑");
        }
        ImGui.EndChild();
    }

    private void DrawRuleDetails(BuffTriggerRule selectedRule)
    {
        ImGui.Text($"编辑规则 {selectedRuleIndex + 1}");
        ImGui.Separator();

        // 规则名称
        string ruleName = selectedRule.Name ?? string.Empty;
        if (ImGui.InputText("规则名称##BuffRuleName", ref ruleName, 100))
        {
            selectedRule.Name = ruleName;
            SaveConfig();
        }

        // 启用规则
        bool isEnabled = selectedRule.IsEnabled;
        if (ImGui.Checkbox("启用规则##EnableBuffRule", ref isEnabled))
        {
            selectedRule.IsEnabled = isEnabled;
            SaveConfig();
        }

        // Buff 名称
        string buffName = selectedRule.BuffName ?? string.Empty;
        if (ImGui.InputText("Buff 名称##BuffName", ref buffName, 100))
        {
            selectedRule.BuffName = buffName;
            SaveConfig();
        }


        // 新增触发规则的配置项
        int fireStrength = selectedRule.FireStrength;
        if (ImGui.SliderInt("一键开火强度", ref fireStrength, 0, 40))
        {
            selectedRule.FireStrength = fireStrength;
        }

        string pulseId = selectedRule.PulseId ?? string.Empty;
        if (ImGui.InputText("波形ID", ref pulseId, 64))
        {
            selectedRule.PulseId = pulseId;
        }
        // 保存更新
        if (ImGui.Button("保存规则##SaveRuleSTA"))
        {
            SaveConfig();
            Plugin.Log.Info($"规则 {selectedRuleIndex + 1} 已更新");
        }

    }

    public void SaveConfig()
    {
        var json = JsonSerializer.Serialize(BuffTriggerRules, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigFilePath, json);
    }

    public void LoadConfig()
    {
        if (File.Exists(ConfigFilePath))
        {
            var json = File.ReadAllText(ConfigFilePath);
            BuffTriggerRules = JsonSerializer.Deserialize<List<BuffTriggerRule>>(json) ?? new List<BuffTriggerRule>();
        }
    }

    public class BuffTriggerRule
    {
        public string Name { get; set; } = "新规则"; // 规则名称
        public uint BuffID { get; set; } // Buff 图标 ID
        public string BuffName { get; set; } = string.Empty; // Buff 名称
        public bool IsEnabled { get; set; } = true; // 是否启用规则

        // 开火相关配置
        public int FireStrength { get; set; } = 10; // 开火强度
        public int FireTime { get; set; } = 1000; // 开火时间，单位毫秒
        public bool OverrideTime { get; set; } = false; // 是否重置时间
        public string PulseId { get; set; } = string.Empty; // 波形ID


    }

    private readonly Dictionary<string, float> BuffLastTriggerTime = new(); // 用于记录 Buff 上次触发的整数秒时间
    private void OnFrameworkUpdateForBuff(IFramework framework)
    {
        var localPlayer = Plugin.ClientState.LocalPlayer;
        if (localPlayer == null || !isBuffTriggerRunning) return;

        var activeStatuses = localPlayer.StatusList
            .Where(s => s.GameData != null && !string.IsNullOrEmpty(s.GameData.Name.ExtractText()))
            .ToDictionary(s => s.GameData.Name.ExtractText(), s => s.RemainingTime);

        

        foreach (var rule in BuffTriggerRules)
        {
            if (!rule.IsEnabled) continue;

            // 如果 Buff 名称在玩家状态中
            if (activeStatuses.TryGetValue(rule.BuffName, out var remainingTime))
            {
                // 检查剩余时间是否为整数
                var remainingTimeInt = (int)Math.Floor(remainingTime);

                // 如果是整数并且不同于上一次触发的时间，则触发
                if (remainingTimeInt > 0 && (!BuffLastTriggerTime.ContainsKey(rule.BuffName) || BuffLastTriggerTime[rule.BuffName] != remainingTimeInt))
                {
                    //Plugin.Chat.Print($"触发 Buff 规则: {rule.Name} (Buff: {rule.BuffName}, 剩余时间: {remainingTime:F2}s)");
                    TriggerFireAction(rule.FireStrength, remainingTimeInt* 1000, rule.OverrideTime, rule.PulseId);

            // 更新上次触发时间
            BuffLastTriggerTime[rule.BuffName] = remainingTimeInt;
                }

                // 如果 remainingTime 为 0，打印提醒信息，但不会触发
                if (remainingTime == 0)
                {
                    continue;
                }
            }
            else if (BuffCooldowns.ContainsKey(rule.BuffName))
        {
            // 如果 Buff 不再存在，则重置冷却和触发时间
            //Plugin.Chat.Print($"Buff 已消失，重置规则: {rule.Name} (Buff: {rule.BuffName})");
            TriggerFireAction(0, 0, true, rule.PulseId);
            BuffCooldowns.Remove(rule.BuffName);
            BuffLastTriggerTime.Remove(rule.BuffName); // 清除触发时间记录
        }
        }
    }

    private async void TriggerFireAction(int FireStrength, int FireTime, bool OverrideTime, string PulseId)
    {
        try
        {
            var requestContent = new
            {
                strength = FireStrength,
                time = FireTime,
                @override = OverrideTime,
                pulseId = PulseId
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestContent),
                Encoding.UTF8,
                "application/json"
            );

            string url = $"{Plugin.Configuration.HttpServer}/api/v2/game/{Plugin.Configuration.ClientID}/action/fire";
            var response = await httpClient.PostAsync(url, jsonContent);

            if (response.IsSuccessStatusCode)
            {
                fireResponse = await response.Content.ReadAsStringAsync();
            }
            else
            {
                fireResponse = $"开火失败: {response.StatusCode}";
                Plugin.Log.Warning(fireResponse);
                Plugin.Configuration.Log = fireResponse;

            }
        }
        catch (Exception ex)
        {
            fireResponse = $"开火错误: {ex.Message}";
            Plugin.Log.Warning(fireResponse);
            Plugin.Configuration.Log = fireResponse;
        }
    }

}
