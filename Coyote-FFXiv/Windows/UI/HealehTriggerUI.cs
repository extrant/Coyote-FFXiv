using Coyote;
using Coyote.Utils;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Coyote.Gui;
public class HealthTriggerUI
{
    private readonly Configuration Configuration;
    private readonly Plugin Plugin;
    private readonly HealthWatcher HealthWatcher;
    private int selectedRuleIndex = -1;
    private bool isHealthTriggerRunning = false; // 用于控制逻辑运行的开关

    public HealthTriggerUI(Configuration configuration, Plugin plugin, HealthWatcher healthWatcher)
    {
        Configuration = configuration;
        Plugin = plugin;
        HealthWatcher = healthWatcher;

        // 绑定事件
        HealthWatcher.OnHealthChanged += HandleHealthChange;
    }

    public void Draw()
    {
        ImGui.BeginGroup(); // 顶部按钮组

        if (ImGui.Checkbox("总触发开关##HpChange", ref isHealthTriggerRunning))
        {
            if (isHealthTriggerRunning)
            {
                HealthWatcher.StartWatching();
            }
            else
            {
                HealthWatcher.StopWatching();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("新增规则##AddHealthRule"))
        {
            var newRule = new HealthTriggerRule();
            Configuration.HealthTriggerRules.Add(newRule);
            selectedRuleIndex = Configuration.HealthTriggerRules.Count - 1;
            Plugin.Configuration.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("删除选中规则##DeleteHealthRule"))
        {
            if (selectedRuleIndex >= 0 && selectedRuleIndex < Configuration.HealthTriggerRules.Count)
            {
                Configuration.HealthTriggerRules.RemoveAt(selectedRuleIndex);
                selectedRuleIndex = -1;
                Plugin.Configuration.Save();
            }
        }
        ImGui.EndGroup();

        ImGui.Separator();

        // 左侧规则列表
        ImGui.BeginChild("HealthRuleList", new Vector2(200, 0), true);
        for (int i = 0; i < Configuration.HealthTriggerRules.Count; i++)
        {
            var rule = Configuration.HealthTriggerRules[i];
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
        ImGui.BeginChild("HealthRuleDetails", new Vector2(0, 0), true);
        if (selectedRuleIndex >= 0 && selectedRuleIndex < Configuration.HealthTriggerRules.Count)
        {
            DrawRuleDetails(Configuration.HealthTriggerRules[selectedRuleIndex]);
        }
        else
        {
            ImGui.Text("请选择一个规则进行编辑");
        }
        ImGui.EndChild();
    }

    private void DrawRuleDetails(HealthTriggerRule selectedRule)
    {
        ImGui.Text($"编辑规则 {selectedRuleIndex + 1}");
        ImGui.Separator();

        // 规则名称
        string ruleName = selectedRule.Name ?? string.Empty;
        if (ImGui.InputText("规则名称##HealthRuleName", ref ruleName, 100))
        {
            selectedRule.Name = ruleName;
            Plugin.Configuration.Save();
        }

        // 启用规则
        bool isEnabled = selectedRule.IsEnabled;
        if (ImGui.Checkbox("启用规则##EnableHealthRule", ref isEnabled))
        {
            selectedRule.IsEnabled = isEnabled;
            Plugin.Configuration.Save();
        }

        // 触发模式
        int triggerMode = selectedRule.TriggerMode;
        if (ImGui.Combo("触发模式", ref triggerMode, "血量减少触发\0回血触发\0"))
        {
            selectedRule.TriggerMode = triggerMode;
            Plugin.Configuration.Save();
        }

        // 触发阈值
        int triggerThreshold = selectedRule.TriggerThreshold;
        if (ImGui.SliderInt("触发阈值(血量值)##HealthThreshold", ref triggerThreshold, 0, 10000))
        {
            selectedRule.TriggerThreshold = triggerThreshold;
            Plugin.Configuration.Save();
        }

        // 血量区间
        int minPercentage = selectedRule.MinPercentage;
        if (ImGui.SliderInt("触发区间最小血量##HealthMin", ref minPercentage, 0, 100))
        {
            selectedRule.MinPercentage = minPercentage;
            Plugin.Configuration.Save();
        }

        int maxPercentage = selectedRule.MaxPercentage;
        if (ImGui.SliderInt("触发区间最大血量##HealthMax", ref maxPercentage, 0, 100))
        {
            selectedRule.MaxPercentage = maxPercentage;
            Plugin.Configuration.Save();
        }

        // 开火强度
        int fireStrength = selectedRule.FireStrength;
        if (ImGui.SliderInt("一键开火强度##HealthFireStrength", ref fireStrength, 0, 40))
        {
            selectedRule.FireStrength = fireStrength;
            Plugin.Configuration.Save();
        }

        // 开火时间
        int fireTime = selectedRule.FireTime;
        if (ImGui.SliderInt("一键开火时间(ms)##HealthFireTime", ref fireTime, 0, 30000))
        {
            selectedRule.FireTime = fireTime;
            Plugin.Configuration.Save();
        }

        // 重置时间
        bool overrideTime = selectedRule.OverrideTime;
        if (ImGui.Checkbox("多次触发时，重置时间##HealthOverrideTime", ref overrideTime))
        {
            selectedRule.OverrideTime = overrideTime;
            Plugin.Configuration.Save();
        }

        // 波形ID
        string pulseId = selectedRule.PulseId ?? string.Empty;
        if (ImGui.InputText("波形ID##HealthPulseId", ref pulseId, 64))
        {
            selectedRule.PulseId = pulseId;
            Plugin.Configuration.Save();
        }

        // 保存更新
        if (ImGui.Button("保存规则##SaveRuleHP"))
        {
            //Configuration.chatTriggerRules[selectedRuleIndex] = selectedRule;
            HPTriggerRuleManager.SaveRules(Configuration.HealthTriggerRules);
            Plugin.Log.Info($"规则 {selectedRuleIndex + 1} 已更新");
        }
    }

    private void HandleHealthChange(int currentHp, int maxHp, int percentage)
    {
        // 处理血量变化逻辑
        Plugin.Log.Info($"血量变化: {currentHp}/{maxHp} ({percentage}%)");
    }
}
