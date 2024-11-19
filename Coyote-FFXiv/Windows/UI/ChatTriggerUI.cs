using Coyote.Utils;
using Dalamud.Game.Text;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;


namespace Coyote.Gui;
public class ChatTriggerUI
{
    private Configuration Configuration; // 配置对象
    private Plugin Plugin; // 插件实例
    private int selectedRuleIndex = -1; // 当前选中的规则索引
    private bool isChatTriggerRunning = false; // 聊天触发的开关状态
    private ChatWatcher chatWatcher; // 聊天监听器

    public ChatTriggerUI(Configuration configuration, Plugin plugin)
    {
        Configuration = configuration;
        Plugin = plugin;
    }


    public void Draw()
    {
        ImGui.BeginGroup(); // 开始一个组，用于按钮布局
        if (ImGui.Checkbox("总触发开关##ChatChange", ref isChatTriggerRunning))
        {
            if (isChatTriggerRunning)
            {
                chatWatcher = new ChatWatcher(Configuration);
            }
            else
            {
                chatWatcher.Dispose();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("新增规则##AddEmptyRule"))
        {
            var newRule = new ChatTriggerRule
            {
                ChatType = XivChatType.Say,
                SenderName = string.Empty,
                Keyword = string.Empty,
                MatchEntireMessage = false,
                CheckSender = false,
                IsEnabled = true // 默认启用规则
            };
            Configuration.chatTriggerRules.Add(newRule);
            selectedRuleIndex = Configuration.chatTriggerRules.Count - 1; // 选中新添加的规则
            ChatTriggerRuleManager.SaveRules(Configuration.chatTriggerRules);
            Plugin.Chat.Print("新规则已添加");
        }
        ImGui.SameLine();
        if (ImGui.Button("删除选中规则##DeleteSelectedRule"))
        {
            if (selectedRuleIndex >= 0 && selectedRuleIndex < Configuration.chatTriggerRules.Count)
            {
                Configuration.chatTriggerRules.RemoveAt(selectedRuleIndex);
                ChatTriggerRuleManager.SaveRules(Configuration.chatTriggerRules);
                selectedRuleIndex = -1;
                Plugin.Chat.Print("选中的规则已删除");
            }
            else
            {
                Plugin.Chat.Print("未选择规则，无法删除");
            }
        }

        

        ImGui.EndGroup();

        ImGui.Separator();

        // 左侧规则列表
        ImGui.BeginChild("RuleList", new Vector2(200, 0), true);
        for (int i = 0; i < Configuration.chatTriggerRules.Count; i++)
        {
            var rule = Configuration.chatTriggerRules[i];
            if (rule.IsEnabled)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 1, 0, 1)); // 绿色文本
            }

            if (ImGui.Selectable($"规则 {i + 1}: {rule.Keyword}", selectedRuleIndex == i))
            {
                selectedRuleIndex = i;
            }

            if (rule.IsEnabled)
            {
                ImGui.PopStyleColor(); // 恢复默认文本颜色
            }
        }
        ImGui.EndChild();

        ImGui.SameLine();

        // 右侧规则详细信息
        ImGui.BeginChild("RuleDetails", new Vector2(0, 0), true);

        if (selectedRuleIndex >= 0 && selectedRuleIndex < Configuration.chatTriggerRules.Count)
        {
            var selectedRule = Configuration.chatTriggerRules[selectedRuleIndex];

            ImGui.Text($"编辑规则 {selectedRuleIndex + 1}");
            ImGui.Separator();


            // 启用/禁用规则
            bool isEnabled = selectedRule.IsEnabled;
            if (ImGui.Checkbox("启用规则##EnableRule", ref isEnabled))
            {
                selectedRule.IsEnabled = isEnabled;
                ChatTriggerRuleManager.SaveRules(Configuration.chatTriggerRules);
            }


            // 聊天类型
            ImGui.Text("聊天类型");
            if (ImGui.BeginCombo("##EditChatType", selectedRule.ChatType.ToString()))
            {
                foreach (XivChatType chatType in Enum.GetValues(typeof(XivChatType)))
                {
                    bool isSelected = selectedRule.ChatType == chatType;
                    if (ImGui.Selectable(chatType.ToString(), isSelected))
                    {
                        selectedRule.ChatType = chatType;
                    }
                    if (isSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }

            // 检查发送者
            bool checkSender = selectedRule.CheckSender;
            if (ImGui.Checkbox("检查发送者##EditCheckSender", ref checkSender))
            {
                selectedRule.CheckSender = checkSender;
            }

            // 发送者输入框（仅在检查发送者时显示）
            if (selectedRule.CheckSender)
            {
                string senderName = selectedRule.SenderName ?? string.Empty;
                if (ImGui.InputText("发送者##EditSender", ref senderName, 100))
                {
                    selectedRule.SenderName = senderName;
                }
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "注:技术原因不建议使用，什么时候这个提示没了就说明完全支持了！");
            }

            // 消息关键词
            string keyword = selectedRule.Keyword ?? string.Empty;
            if (ImGui.InputText("关键词##EditKeyword", ref keyword, 100))
            {
                selectedRule.Keyword = keyword;
            }
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "注:如果关键词什么都不填会导致无条件触发！");
            selectedRule.Keyword = keyword;

            // 匹配全文
            bool matchEntireMessage = selectedRule.MatchEntireMessage;
            if (ImGui.Checkbox("匹配全文##EditMatchEntireMessage", ref matchEntireMessage))
            {
                selectedRule.MatchEntireMessage = matchEntireMessage;
            }

            // 新增触发规则的配置项
            int fireStrength = selectedRule.FireStrength;
            if (ImGui.SliderInt("一键开火强度", ref fireStrength, 0, 40))
            {
                selectedRule.FireStrength = fireStrength;
            }

            int fireTime = selectedRule.FireTime;
            if (ImGui.SliderInt("一键开火时间(ms)", ref fireTime, 0, 30000))
            {
                selectedRule.FireTime = fireTime;
            }

            bool overrideTime = selectedRule.OverrideTime;
            if (ImGui.Checkbox("多次触发时，重置时间", ref overrideTime))
            {
                selectedRule.OverrideTime = overrideTime;
            }

            string pulseId = selectedRule.PulseId ?? string.Empty;
            if (ImGui.InputText("波形ID", ref pulseId, 64))
            {
                selectedRule.PulseId = pulseId;
            }

            // 保存更新
            if (ImGui.Button("保存规则##SaveRule"))
            {
                Configuration.chatTriggerRules[selectedRuleIndex] = selectedRule;
                ChatTriggerRuleManager.SaveRules(Configuration.chatTriggerRules);
                Plugin.Log.Info($"规则 {selectedRuleIndex + 1} 已更新");
            }
        }
        else
        {
            ImGui.Text("请选择一个规则进行编辑");
        }

        ImGui.EndChild();

    }

}
