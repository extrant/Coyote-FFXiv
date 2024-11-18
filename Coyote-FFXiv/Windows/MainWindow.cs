using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Coyote;
using System.Numerics;
using System.Text.Json;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text;
using static Coyote.ChatWatcher;
using System.Linq;
using System.Diagnostics;

public class MainWindow : Window, IDisposable
{
    private readonly HttpClient httpClient = new HttpClient();
    private string GoatImagePath;
    private Plugin Plugin;
    private int previousHp;
    private bool isHealthDecreasing;
    private string fireResponse;
    private ApiResponse parsedResponse; // 用于存储解析后的 API 数据
    private Configuration Configuration;
    

    private int selectedTab = 0; // 当前选中的选项卡

    public MainWindow(Plugin plugin, string goatImagePath)
        : base("Coyote-FFXiv##Dalamud1",ImGuiWindowFlags.NoResize)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(800, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        GoatImagePath = goatImagePath;
        Plugin = plugin;
        Configuration = plugin.Configuration;

        if (Plugin.ClientState.LocalPlayer != null)
        {
            previousHp = (int)Plugin.ClientState.LocalPlayer.CurrentHp;
        }

        fireResponse = "还没有返回消息哦";

    }






    private bool isChatTriggerRunning = false; // 用于控制逻辑运行的开关
    private bool isHealthTriggerRunning = false; // 用于控制逻辑运行的开关


    public static void OpenWebPage(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception e)
        {
            Console.WriteLine("无法打开网页: " + e.Message);
        }
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }

    public override void Draw()
    {

        float padding = 10.0f; // 右边距
        float buttonWidth = 100.0f; // 每个按钮的宽度
        float totalButtonWidth = 3 * buttonWidth + 2 * ImGui.GetStyle().ItemSpacing.X; // 三个按钮加上两个间距

        // 获取窗口的宽度
        float windowWidth = ImGui.GetWindowWidth();

        // 计算按钮开始的 X 位置，以右对齐
        float startX = windowWidth - totalButtonWidth - padding;

        // 设置第一个按钮的 X 位置
        ImGui.Text("本插件完全免费，不要听信一切需要付费的话术！");
        ImGui.SameLine();
        ImGui.SetCursorPosX(startX);

        if (ImGui.Button("Discord", new System.Numerics.Vector2(buttonWidth, 0)))
        {
            OpenWebPage("https://discord.gg/g8QKPAnCBa");
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("点击前往Discord。");
        }
        ImGui.SameLine();

        if (ImGui.Button("Github", new System.Numerics.Vector2(buttonWidth, 0)))
        {
            OpenWebPage("https://github.com/extrant");
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("点击前往Github。");
        }
        ImGui.SameLine();

        if (ImGui.Button("爱发电", new System.Numerics.Vector2(buttonWidth, 0)))
        {
            OpenWebPage("https://afdian.com/a/Sincraft0515");
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("所有插件均为免费提供，您无需支付任何费用\n如果您选择赞助，这将是一种无偿捐赠，我们不会因此提供任何形式的承诺或回报\n在决定赞助之前，请仔细考虑");
        }



        ImGui.BeginTabBar("MainTabBar");

        if (ImGui.BeginTabItem("首页"))
        {
            DrawHomePage();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("触发"))
        {
            DrawTriggerPage();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("关于"))
        {
            DrawAboutPage();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }
    //主页绘制
    private void DrawHomePage()
    {
        string HttpServer = Plugin.Configuration.HttpServer;
        if (ImGui.InputText("CoyoteIP", ref HttpServer, 64))
        {
            Plugin.Configuration.HttpServer = HttpServer;
            Configuration.Save();
        }
        string ClientID = Plugin.Configuration.ClientID;
        if (ImGui.InputText("ClientID", ref ClientID, 64))
        {
            Plugin.Configuration.ClientID = ClientID;
            Configuration.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("测试连通性"))
        {
            TriggerTestAction();
        }

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1, 0, 0, 1), "请保证在安全、清醒、自愿的情况下使用\n严禁体内存在电子/金属植入物者、心脑血管疾病患者、孕妇、儿童或无法及时操作主机的人群使用\n严禁将电极置于心脏投影区（或任何可能使电流经过心脏的位置），以及头部、颈部、皮肤破损处等位置\n严禁在驾驶或操纵机器等危险情况下使用\n请勿在同一部位连续使用30分钟以上，以免造成损伤\n请勿在输出状态下移动电极，以免造成刺痛或灼伤\n在直播过程中使用可能会导致直播间被封禁，风险自负\n在使用前需要完整阅读郊狼产品安全须知，并设置好强度上限保护。");
        DrawApiResponse();
    }
    private ChatWatcher chatWatcher;
    private void DrawTriggerPage()
    {
        if (ImGui.BeginTabBar("TriggerTabBar"))
        {
            if (ImGui.BeginTabItem("血量触发"))
            {
                DrawHealthTrigger();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("聊天触发"))
            {
                DrawChatTrigger();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("还在做哈"))
            {
                ImGui.Text("这是另一个占位触发。");
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }


    private ChatTriggerRule newRule = new ChatTriggerRule(); // 用于输入新规则
                                                             //聊天触发绘制相关
    private int selectedRuleIndex = -1; // 当前选中的规则索引

    private void DrawChatTrigger()
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
            ImGui.TextColored(new Vector4(1, 0, 0, 1),"注:如果关键词什么都不填会导致无条件触发！");
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

    private int selectedHealthRuleIndex = -1; // 当前选中的血量触发规则索引

    private void DrawHealthTrigger()
    {
        ImGui.BeginGroup(); // 顶部按钮组

        if (ImGui.Checkbox("总触发开关##HpChange", ref isHealthTriggerRunning))
        {
            if (isHealthTriggerRunning)
            {
                Plugin.Framework.Update += this.OnFrameworkUpdateForHpChange;
            }
            else
            {
                Plugin.Framework.Update -= this.OnFrameworkUpdateForHpChange;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("新增规则##AddHealthRule"))
        {
            var newRule = new HealthTriggerRule();
            Configuration.HealthTriggerRules.Add(newRule);
            selectedHealthRuleIndex = Configuration.HealthTriggerRules.Count - 1;
            Plugin.Configuration.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("删除选中规则##DeleteHealthRule"))
        {
            if (selectedHealthRuleIndex >= 0 && selectedHealthRuleIndex < Configuration.HealthTriggerRules.Count)
            {
                Configuration.HealthTriggerRules.RemoveAt(selectedHealthRuleIndex);
                selectedHealthRuleIndex = -1;
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

            if (ImGui.Selectable($"{i + 1}. {rule.Name}", selectedHealthRuleIndex == i))
            {
                selectedHealthRuleIndex = i;
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
        if (selectedHealthRuleIndex >= 0 && selectedHealthRuleIndex < Configuration.HealthTriggerRules.Count)
        {
            var selectedRule = Configuration.HealthTriggerRules[selectedHealthRuleIndex];

            ImGui.Text($"编辑规则 {selectedHealthRuleIndex + 1}");
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
            if (ImGui.SliderInt("触发区间最小血量(%)##HealthMin", ref minPercentage, 0, 100))
            {
                selectedRule.MinPercentage = minPercentage;
                Plugin.Configuration.Save();
            }

            int maxPercentage = selectedRule.MaxPercentage;
            if (ImGui.SliderInt("触发区间最大血量(%)##HealthMax", ref maxPercentage, 0, 100))
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
            if (ImGui.Button("保存规则##SaveRule"))
            {
                Configuration.HealthTriggerRules[selectedHealthRuleIndex] = selectedRule;
                HPTriggerRuleManager.SaveRules(Configuration.HealthTriggerRules);
                Plugin.Log.Info($"规则 {selectedHealthRuleIndex + 1} 已更新");
            }
        }
        else
        {
            ImGui.Text("请选择一个规则进行编辑");
        }
        ImGui.EndChild();
    }


    private void OnFrameworkUpdateForHpChange(IFramework framework)
    {
        if (Plugin.ClientState.LocalPlayer == null || Configuration.HealthTriggerRules.Count == 0)
            return;

        var localPlayer = Plugin.ClientState.LocalPlayer;
        int currentHp = (int)localPlayer.CurrentHp;
        int currentHpPercentage = (int)((localPlayer.CurrentHp / (float)localPlayer.MaxHp) * 100);

        foreach (var rule in Configuration.HealthTriggerRules)
        {
            if (!rule.IsEnabled)
                continue;

            // 检查触发区间
            if (currentHpPercentage < rule.MinPercentage || currentHpPercentage > rule.MaxPercentage)
                continue;

            bool shouldTrigger = false;

            // 根据触发模式决定逻辑
            switch (rule.TriggerMode)
            {
                case 0: // 血量减少触发
                    shouldTrigger = currentHp < previousHp &&
                                    Math.Abs(previousHp - currentHp) >= rule.TriggerThreshold;
                    break;

                case 1: // 回血触发
                    shouldTrigger = currentHp > previousHp &&
                                    Math.Abs(previousHp - currentHp) >= rule.TriggerThreshold;
                    break;
            }

            if (shouldTrigger)
            {
                TriggerFireAction(rule); // 根据规则参数触发开火
            }
        }

        previousHp = (int)localPlayer.CurrentHp; // 更新上一次的血量
    }



    private void DrawAboutPage()
    {
        var goatImage = Plugin.TextureProvider.GetFromFile(GoatImagePath).GetWrapOrDefault();
        if (goatImage != null)
        {
            ImGui.Image(goatImage.ImGuiHandle, new Vector2(goatImage.Width, goatImage.Height));
        }
        else
        {
            ImGui.Text("图片未找到。");
        }


    }

    private async void TriggerFireAction(HealthTriggerRule rule)
    {
        try
        {
            var requestContent = new
            {
                strength = rule.FireStrength,
                time = rule.FireTime,
                @override = rule.OverrideTime,
                pulseId = rule.PulseId
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



    //客户端配置相关
    private async void TriggerTestAction()
    {
        try
        {
            var testUrl = $"{Plugin.Configuration.HttpServer}/api/v2/game/{Plugin.Configuration.ClientID}";
            var response = await httpClient.GetAsync(testUrl);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            fireResponse = "测试成功！";

            // 解析返回的 JSON
            parsedResponse = JsonSerializer.Deserialize<ApiResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            fireResponse = $"测试失败: {ex.Message}";
            parsedResponse = null;
        }
    }

    private void DrawApiResponse()
    {
        if (parsedResponse != null)
        {
            if (ImGui.CollapsingHeader("当前状态"))
            {
                //ImGui.Text("解析后的API返回数据:");
                ImGui.Separator();

                // Status 和 Code
                ImGui.Text($"状态: {parsedResponse.Status}");
                ImGui.SameLine();
                ImGui.TextColored(parsedResponse.Status == 1 ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                                  parsedResponse.Status == 1 ? "正常" : "异常");
                ImGui.Text($"返回代码: {parsedResponse.Code}");

                // 基础强度配置
                if (parsedResponse.StrengthConfig != null)
                {
                    ImGui.Text("基础强度配置:");
                    ImGui.BulletText($"基础强度: {parsedResponse.StrengthConfig.Strength}");
                    ImGui.BulletText($"随机强度: {parsedResponse.StrengthConfig.RandomStrength}");
                }

                // 游戏配置
                if (parsedResponse.GameConfig != null)
                {
                    ImGui.Text("游戏配置:");
                    ImGui.BulletText($"强度变化间隔: [{parsedResponse.GameConfig.StrengthChangeInterval[0]}, {parsedResponse.GameConfig.StrengthChangeInterval[1]}] 秒");
                    ImGui.BulletText($"启用B通道: {parsedResponse.GameConfig.EnableBChannel}");
                    ImGui.BulletText($"B通道强度倍数: {parsedResponse.GameConfig.BChannelStrengthMultiplier}");
                    ImGui.BulletText($"波形ID: {parsedResponse.GameConfig.PulseId}");
                    ImGui.BulletText($"波形播放模式: {parsedResponse.GameConfig.PulseMode}");
                    ImGui.BulletText($"波形变化间隔: {parsedResponse.GameConfig.PulseChangeInterval} 秒");
                }

                // 客户端强度
                if (parsedResponse.ClientStrength != null)
                {
                    ImGui.Text("客户端强度:");
                    ImGui.BulletText($"当前强度: {parsedResponse.ClientStrength.Strength}");
                    ImGui.BulletText($"强度上限: {parsedResponse.ClientStrength.Limit}");
                }

                // 当前波形 ID
                ImGui.Text($"当前波形ID: {parsedResponse.CurrentPulseId}");
            }
        }
        else
        {
            ImGui.TextWrapped(fireResponse);
        }
    }


    public class ApiResponse
    {
        public int Status { get; set; }
        public string Code { get; set; }
        public StrengthConfig StrengthConfig { get; set; }
        public GameConfig GameConfig { get; set; }
        public ClientStrength ClientStrength { get; set; }
        public string CurrentPulseId { get; set; }
    }

    public class StrengthConfig
    {
        public int Strength { get; set; }
        public int RandomStrength { get; set; }
    }

    public class GameConfig
    {
        public int[] StrengthChangeInterval { get; set; }
        public bool EnableBChannel { get; set; }
        public double BChannelStrengthMultiplier { get; set; }
        public string PulseId { get; set; }
        public string PulseMode { get; set; }
        public int PulseChangeInterval { get; set; }
    }

    public class ClientStrength
    {
        public int Strength { get; set; }
        public int Limit { get; set; }
    }

}


