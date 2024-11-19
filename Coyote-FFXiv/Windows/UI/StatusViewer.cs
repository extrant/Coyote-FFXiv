using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Interface;
using Dalamud.Utility;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using ECommons.SimpleGui;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using ECommons;
using System.Linq;
using Coyote.Utils;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Interface.Textures.TextureWraps;
namespace Coyote.Gui;
public class BuffIconSelector
{
    private static Vector2 JobIconSize => new(24f, 24f);
    public static readonly Vector2 StatusIconSize = new(24, 32);
    private bool? IsFCStatus = null;
    private bool? IsStackable = null;
    private bool AutoFill = false;
    private List<Job> Jobs = [];
    private string Filter = "";
    public List<uint> IconArray = [];
    private bool Fullscreen = false;
    public uint SelectedIconID; // 当前选中的 IconID
    public MyStatus Delegate;  // 当前选中的状态对象

    private Configuration Configuration; // 配置对象
    private Plugin Plugin; // 插件实例


    public BuffIconSelector(Configuration configuration, Plugin plugin)
    {
        Delegate = new MyStatus();
        var statusSheet = Plugin.DataManager.GetExcelSheet<Status>();
        Configuration = configuration;
        foreach (var status in statusSheet)
        {
            if (IconArray.Contains(status.Icon)) continue; // 去重
            if (status.Icon == 0) continue; // 跳过无效图标
            if (string.IsNullOrEmpty(status.Name.ExtractText())) continue; // 跳过无效名称
            IconArray.Add(status.Icon); // 添加图标 ID
        }
    }



    public void Draw()
    {
        if (Plugin.ClientState.LocalPlayer != null)
        {
            var statusList = Plugin.ClientState.LocalPlayer.StatusList;

            if (statusList != null && statusList.Count() > 0)
            {
                foreach (var status in statusList)
                {
                    if (status == null || status.StatusId == 0) continue;

                    ImGui.Text($"Status Name: {status.GameData.Name}");
                }
            }
            else
            {
                ImGui.Text("No active statuses.");
            }
        }
        else
        {
            ImGui.Text("LocalPlayer not available.");
        }
        var statusInfos = IconArray.Select(GetIconInfo).Where(x => x.HasValue).Cast<IconInfo>();

        ImGui.SetNextItemWidth(150f);
        ImGui.InputTextWithHint("##search", "筛选...", ref Filter, 50);
        ImGui.SameLine();
        ImGui.Checkbox("自动填充数据", ref AutoFill);
        ImGuiEx.HelpMarker("使用游戏本身关于图标的数据自动填充到标题和描述。要求这些字段留空或否则以前填写的内容不会被修改。");
        ImGui.SameLine();
        ImGuiEx.Text("种类/职业：");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120f);
        if (ImGui.BeginCombo("##job", Jobs.Select(x => x.ToString().Replace("_", " ")).PrintRange(out var fullList)))
        {
            foreach (var cond in Enum.GetValues<Job>().Where(x => !x.IsUpgradeable()).OrderByDescending(x => Plugin.DataManager.GetExcelSheet<ClassJob>().GetRow((uint)x).Role))
            {
                if (cond == Job.ADV) continue;
                var name = cond.ToString().Replace("_", " ");
                var texture = TexturesHelper.GetTextureFromIconId((uint)cond.GetIcon());
                if (texture != null)
                {
                    ImGui.Image(texture.ImGuiHandle, JobIconSize);
                    ImGui.SameLine();
                }
                ImGuiEx.CollectionCheckbox(name, cond, Jobs);
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        ImGuiEx.Text("排序：");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f);
        ImGuiEx.EnumCombo("##order", ref C.IconSortOption);

        if (ImGui.BeginChild("child"))
        {
            if (ImGui.CollapsingHeader("增益状态效果"))
            {
                DrawIconTable(statusInfos.Where(x => x.Type == StatusType.强化状态).OrderBy(x => x.IconID));
            }
            if (ImGui.CollapsingHeader("减益状态效果"))
            {
                DrawIconTable(statusInfos.Where(x => x.Type == StatusType.弱化状态).OrderBy(x => x.IconID));
            }
            if (ImGui.CollapsingHeader("其他状态效果"))
            {
                DrawIconTable(statusInfos.Where(x => x.Type == StatusType.其他状态).OrderBy(x => x.IconID));
            }
        }
        ImGui.EndChild();
    }

    private void DrawIconTable(System.Collections.Generic.IEnumerable<IconInfo> infos)
    {
        infos = infos
            .Where(x => Filter == "" || (x.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase) || x.IconID.ToString().Contains(Filter)))
            .Where(x => IsFCStatus == null || IsFCStatus == x.IsFCBuff)
            .Where(x => IsStackable == null || IsStackable == x.IsStackable)
            .Where(x => Jobs.Count == 0 || (Jobs.Any(j => x.ClassJobCategory.IsJobInCategory(j.GetUpgradedJob()) || x.ClassJobCategory.IsJobInCategory(j.GetDowngradedJob())) && x.ClassJobCategory.RowId > 1));
        //if (C.IconSortOption == SortOption.Alphabetical) infos = infos.OrderBy(x => x.Name);
        //if (C.IconSortOption == SortOption.Numerical) infos = infos.OrderBy(x => x.IconID);
        if (!infos.Any())
        {
            ImGuiEx.Text(EColor.RedBright, $"没有与筛选条件匹配的元素。");
        }
        var cols = Math.Clamp((int)(ImGui.GetWindowSize().X / 200f), 1, 10);
        if (ImGui.BeginTable("StatusTable", cols, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
        {
            for (var i = 0; i < cols; i++)
            {
                ImGui.TableSetupColumn($"Col{i}");
            }
            var index = 0;
            foreach (var info in infos)
            {
                if (index % cols == 0) ImGui.TableNextRow();
                index++;
                ImGui.TableNextColumn();

                IDalamudTextureWrap tex = null;

                try
                {
                    // 尝试加载指定图标
                    tex = TexturesHelper.GetTextureFromIconId(info.IconID);
                }
                catch (Exception ex)
                {
                    try
                    {
                        tex = TexturesHelper.GetTextureFromIconId(0);
                    }
                    catch (Exception fallbackEx)
                    {
                        tex = null; // 如果默认图标也失败，则设置为 null
                    }
                }
                if (tex != null)
                {
                    ImGui.Image(tex.ImGuiHandle, StatusIconSize);
                    ImGui.SameLine();
                    ImGuiEx.Tooltip($"{info.IconID}");
                    if (ImGui.Button($"复制##{info.IconID}"))
                    {
                        // 将 info.Name 复制到剪贴板
                        ImGui.SetClipboardText(info.Name);

                        // 提供反馈（例如日志或提示）
                        Plugin.Chat.Print($"已复制名称: {info.Name}");
                    }
                    ImGui.SameLine();
                    ImGui.Text(info.Name);

                }
            }
            ImGui.EndTable();
        }
    }

    private static Dictionary<uint, IconInfo?> IconInfoCache = [];
    public static IconInfo? GetIconInfo(uint iconID)
    {
        if (IconInfoCache.TryGetValue(iconID, out var iconInfo))
        {
            return iconInfo;
        }
        else
        {
            var data = Plugin.DataManager.GetExcelSheet<Status>().FirstOrDefault(x => x.Icon == iconID);
            if (data == null)
            {
                IconInfoCache[iconID] = null;
                return null;
            }
            var info = new IconInfo()
            {
                Name = data.Name.ExtractText(),
                IconID = iconID,
                Type = data.CanIncreaseRewards == 1 ? StatusType.其他状态 : (data.StatusCategory == 2 ? StatusType.弱化状态 : StatusType.强化状态),
                ClassJobCategory = data.ClassJobCategory.Value,
                IsFCBuff = data.IsFcBuff,
                IsStackable = data.MaxStacks > 1,
                Description = data.Description.ExtractText(),

            };
            IconInfoCache[iconID] = info;
            return info;
        }
    }

    public struct IconInfo
    {
        public string Name;
        public uint IconID;
        public StatusType Type;
        public bool IsStackable;
        public ClassJobCategory ClassJobCategory;
        public bool IsFCBuff;
        public string Description;
    }

    public enum StatusType
    {
        强化状态, 弱化状态, 其他状态
    }

    public class C
    {
        // 图标排序选项
        public static SortOption IconSortOption = SortOption.Alphabetical;

        // 收藏的图标列表
        

        // 切换收藏状态
        public void Toggle(uint iconID)
        {
            if (Configuration.FavIcons.Contains(iconID))
            {
                Configuration.FavIcons.Remove(iconID);
            }
            else
            {
                Configuration.FavIcons.Add(iconID);
            }
        }
    }

    // 排序选项枚举
    public enum SortOption
    {
        Alphabetical,
        Numerical
    }

    public class MyStatus
    {
        public int IconID { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
