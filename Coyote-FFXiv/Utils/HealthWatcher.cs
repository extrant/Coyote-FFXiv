using Coyote;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using ImGuiNET;
using System;
using static MainWindow;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Net.Http;

namespace Coyote.Utils;
public class HealthWatcher
{
    private readonly Plugin Plugin;
    private int previousHp;
    private string fireResponse;
    public event Action<int, int, int> OnHealthChanged; // 事件触发时传递当前 HP、最大 HP 和百分比
    private Configuration _configuration;
    private readonly HttpClient httpClient = new HttpClient();

    public HealthWatcher(Plugin plugin)
    {
        Plugin = plugin;
        _configuration = plugin.Configuration;
        if (Plugin.ClientState.LocalPlayer != null)
        {
            previousHp = (int)Plugin.ClientState.LocalPlayer.CurrentHp;
        }
    }

    public void StartWatching()
    {
        Plugin.Framework.Update += OnFrameworkUpdateForHpChange;
    }

    public void StopWatching()
    {
        Plugin.Framework.Update -= OnFrameworkUpdateForHpChange;
    }

    private void OnFrameworkUpdateForHpChange(IFramework framework)
    {
        if (Plugin.ClientState.LocalPlayer == null || _configuration.HealthTriggerRules.Count == 0)
            return;

        var localPlayer = Plugin.ClientState.LocalPlayer;
        int currentHp = (int)localPlayer.CurrentHp;
        int currentHpPercentage = (int)((localPlayer.CurrentHp / (float)localPlayer.MaxHp) * 100);

        foreach (var rule in _configuration.HealthTriggerRules)
        {
            if (!rule.IsEnabled)
                continue;

            // 检查触发区间
            if (currentHpPercentage < rule.MinPercentage || currentHpPercentage > rule.MaxPercentage)
                //Plugin.Log.Debug("不通过");
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

}
