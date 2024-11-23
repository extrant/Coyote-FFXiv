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
using System.Linq;
using System.Diagnostics;
using Coyote.Gui;
using Coyote.Utils;

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
    private ChatTriggerUI chatTriggerUI;
    private HealthWatcher healthWatcher;
    private HealthTriggerUI healthTriggerUI;
    private BuffIconSelector buffIconSelector;
    private BuffTriggerUI buffTriggerUI;
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
        chatTriggerUI = new ChatTriggerUI(plugin.Configuration, plugin);
        healthWatcher = new HealthWatcher(plugin);
        healthTriggerUI = new HealthTriggerUI(plugin.Configuration, plugin, healthWatcher);
        buffIconSelector = new BuffIconSelector(plugin.Configuration, plugin);
        buffTriggerUI = new BuffTriggerUI(plugin.Configuration, plugin);

        if (Plugin.ClientState.LocalPlayer != null)
        {
            previousHp = (int)Plugin.ClientState.LocalPlayer.CurrentHp;
        }

        fireResponse = "还没有返回消息哦";

    }


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

        if (ImGui.Button("QQ交流群", new System.Numerics.Vector2(buttonWidth, 0)))
        {
            OpenWebPage("https://qm.qq.com/q/Dvft7wxPWg");
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("加入QQ交流群");
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

        if (ImGui.BeginTabItem("关于/更新日志"))
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

            if (ImGui.BeginTabItem("状态触发"))
            {
                buffTriggerUI.Draw();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("状态大全"))
            {
                BuffIconSelector();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawChatTrigger()
    {
        chatTriggerUI.Draw();
    }


    private void DrawHealthTrigger()
    {
        healthTriggerUI.Draw();
    }

    private void BuffIconSelector()
    { 
        buffIconSelector.Draw();
    }

    private void DrawAboutPage()
    {
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(1.0f, 0.0f, 0.0f, 1.0f)); // Red color
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(1.0f, 0.5f, 0.5f, 1.0f)); // Lighter red when hovered
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.8f, 0.0f, 0.0f, 1.0f)); // Darker red when active
        if (ImGui.CollapsingHeader("2024/11/23 更新文本指令，修复配置文件存储位置(重要!) 感谢AtmoOmen(啵啵)"))
        {
            ImGui.Text("文本指令已经添加，可以通过ACT触发器自定义高级玩法。或由其他插件通过指令触发。");
            ImGui.Text("本次更新会导致配置文件丢失，如果需要恢复的话：\n原来的配置文件位于你的游戏exe(ffxiv_dx11.exe)路径下，文件名称为\n BuffTriggerConfig.json\n chatTriggerRules.json\n hpTriggerRules.json\n然后复制到 AppData\\Roaming\\XIVLauncherCN\\pluginConfigs\\Coyote-FFXiv\\ 位置即可。");
        }
        ImGui.PopStyleColor(3);
        if (ImGui.CollapsingHeader("2024/11/18 增加状态触发"))
        {
            ImGui.Text("请注意优先级顺序。");
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


