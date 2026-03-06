using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VIWI.Modules.KitchenSink.Commands;

public sealed unsafe class WeaponIcons : IDisposable
{
    public float OverlayOffsetX { get; set; } = 0f;
    public float OverlayOffsetY { get; set; } = 0f;

    private static class BaseColors
    {
        public const uint White = 91000;
        public const uint Combat = 62401;
        public const uint GatheringCrafting = 62502;
    }

    private static readonly IReadOnlyDictionary<string, uint> DirectIconMappingsByAbbr =
        new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            ["ADV"] = 91012,

            // DoH/DoL
            ["CRP"] = BaseColors.GatheringCrafting + 0,
            ["BSM"] = BaseColors.GatheringCrafting + 1,
            ["ARM"] = BaseColors.GatheringCrafting + 2,
            ["GSM"] = BaseColors.GatheringCrafting + 3,
            ["LTW"] = BaseColors.GatheringCrafting + 4,
            ["WVR"] = BaseColors.GatheringCrafting + 5,
            ["ALC"] = BaseColors.GatheringCrafting + 6,
            ["CUL"] = BaseColors.GatheringCrafting + 7,
            ["MIN"] = BaseColors.GatheringCrafting + 8,
            ["BTN"] = BaseColors.GatheringCrafting + 9,
            ["FSH"] = BaseColors.GatheringCrafting + 10,

            // Base classes
            ["GLA"] = BaseColors.Combat + 0,
            ["PGL"] = BaseColors.Combat + 1,
            ["MRD"] = BaseColors.Combat + 2,
            ["LNC"] = BaseColors.Combat + 3,
            ["ARC"] = BaseColors.Combat + 4,
            ["CNJ"] = BaseColors.Combat + 5,
            ["THM"] = BaseColors.Combat + 6,
            ["ACN"] = BaseColors.Combat + 7,
            ["ROG"] = BaseColors.Combat + 9,

            // Jobs
            ["PLD"] = BaseColors.Combat + 0,
            ["MNK"] = BaseColors.Combat + 1,
            ["WAR"] = BaseColors.Combat + 2,
            ["DRG"] = BaseColors.Combat + 3,
            ["BRD"] = BaseColors.Combat + 4,
            ["WHM"] = BaseColors.Combat + 5,
            ["BLM"] = BaseColors.Combat + 6,
            ["SMN"] = BaseColors.Combat + 7,
            ["SCH"] = BaseColors.Combat + 8,
            ["NIN"] = BaseColors.Combat + 9,
            ["MCH"] = BaseColors.Combat + 10,
            ["DRK"] = BaseColors.Combat + 11,
            ["AST"] = BaseColors.Combat + 12,
            ["SAM"] = BaseColors.Combat + 13,
            ["RDM"] = BaseColors.Combat + 14,
            ["BLU"] = BaseColors.Combat + 15,
            ["GNB"] = BaseColors.Combat + 16,
            ["DNC"] = BaseColors.Combat + 17,
            ["RPR"] = BaseColors.Combat + 18,
            ["SGE"] = BaseColors.Combat + 19,
            ["VPR"] = BaseColors.Combat + 20,
            ["PCT"] = BaseColors.Combat + 21,
        };

    private readonly IGameGui _gameGui;
    private readonly IKeyState _keyState;
    private readonly IDataManager _dataManager;
    private readonly ITextureProvider _textureProvider;
    private readonly IPluginLog _log;
    private readonly KitchenSinkConfig _configuration;

    private readonly Dictionary<uint, List<string>> _categoryIdToJobAbbrs = new();
    private readonly Dictionary<uint, CachedLookup> _cachedCategoryIcons = new();
    private readonly Dictionary<uint, CachedLookup> _cachedItemIcons = new();

    private bool _built;
    private Vector2 _nonClientOffset = Vector2.Zero;
    private DateTime _nextNonClientUpdate = DateTime.MinValue;

    public WeaponIcons(
        IGameGui gameGui,
        IKeyState keyState,
        IDataManager dataManager,
        ITextureProvider textureProvider,
        IPluginLog log,
        KitchenSinkConfig config)
    {
        _gameGui = gameGui;
        _keyState = keyState;
        _dataManager = dataManager;
        _textureProvider = textureProvider;
        _log = log;
        _configuration = config;
    }

    public void Dispose()
    {
    }

    public void Draw()
    {
        if (!_configuration.WeaponIconsEnabled)
            return;

        if (_configuration.WeaponIconsRequireCtrl && !_keyState[VirtualKey.CONTROL])
            return;

        if (!_built)
        {
            try
            {
                BuildCategoryJobMaps();
                BuildCategoryIconMaps();
                _built = true;
                _log.Information("[WeaponIcons] Ready.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[WeaponIcons] Failed to build mappings");
                return;
            }
        }
        UpdateNonClientOffset();

        DrawOverlay(dl => DrawArmouryOverlay(dl));
    }

    private static void DrawOverlay(Action<ImDrawListPtr> draw)
    {
        var vp = ImGui.GetMainViewport();

        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
        ImGui.SetNextWindowViewport(vp.ID);

        const ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoInputs |
            ImGuiWindowFlags.NoNav |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

        ImGui.Begin("##VIWI_WeaponIcons_OverlayLayer", flags);

        var dl = ImGui.GetWindowDrawList();
        draw(dl);

        ImGui.End();

        ImGui.PopStyleVar(3);
    }

    private void DrawArmouryOverlay(ImDrawListPtr draw)
    {
        var addonPtr = _gameGui.GetAddonByName("ArmouryBoard", 1);
        if (addonPtr == null || addonPtr.Address == nint.Zero)
            return;

        var unitBase = (AtkUnitBase*)addonPtr.Address;
        if (unitBase == null || !unitBase->IsVisible)
            return;

        var armoury = (AddonArmouryBoard*)unitBase;

        var iom = ItemOrderModule.Instance();
        if (iom == null)
            return;

        var selectedTab = armoury->TabIndex;
        var sorter = iom->ArmourySorter[selectedTab];
        if (sorter == null)
            return;

        int count = (int)sorter.Value->Items.Count;

        for (int i = 0; i < count; i++)
        {
            var invItem = sorter.Value->GetInventoryItem(i);
            if (invItem == null || invItem->ItemId == 0)
                continue;

            uint nodeId = (uint)(71 + i);
            var node = (AtkComponentNode*)armoury->AtkUnitBase.GetNodeById(nodeId);
            if (node == null)
                continue;

            var res = &node->AtkResNode;

            GetNodeScreenRect_FromUnitBaseRootRelative(unitBase, res, out var p0, out var p1);

            var nc = _nonClientOffset;

            p0 += new Vector2(0, nc.Y);
            p1 += new Vector2(0, nc.Y);

            if (OverlayOffsetX != 0f || OverlayOffsetY != 0f)
            {
                var nudge = new Vector2(OverlayOffsetX, OverlayOffsetY);
                p0 += nudge;
                p1 += nudge;
            }

            float w = p1.X - p0.X;
            float h = p1.Y - p0.Y;
            if (w <= 0 || h <= 0)
                continue;

            var lookup = FindIconForItem(invItem->ItemId);
            var tex = lookup.GetTexture(_textureProvider);
            if (tex == null || !tex.TryGetWrap(out var wrap, out _))
                continue;

            var img = wrap.Handle;
            if (img == nint.Zero)
                continue;

            Vector2 iconP0, iconP1;

            if (!_configuration.WeaponIconsMiniMode)
            {
                iconP0 = p0;
                iconP1 = p1;
            }
            else
            {
                float mini = MathF.Floor(MathF.Min(w, h) * 0.5f);
                iconP0 = new Vector2(p0.X, p1.Y - mini);
                iconP1 = iconP0 + new Vector2(mini, mini);
            }

            if (lookup is TexturePathLookup tp)
            {
                var uv0 = tp.TextureLocation / wrap.Size;
                var uv1 = (tp.TextureLocation + tp.TextureSize) / wrap.Size;
                draw.AddImage(img, iconP0, iconP1, uv0, uv1);
            }
            else
            {
                draw.AddImage(img, iconP0, iconP1);
            }

            // Debug Boxes:
            // draw.AddRect(p0, p1, 0xFF00FFFF, 0f, ImDrawFlags.None, 2f);
        }
    }

    private CachedLookup FindIconForItem(uint itemId)
    {
        if (_cachedItemIcons.TryGetValue(itemId, out var cached))
            return cached;

        var sheet = _dataManager.GetExcelSheet<Item>();
        if (sheet == null)
        {
            cached = new NoIconLookup(0, []);
            _cachedItemIcons[itemId] = cached;
            return cached;
        }

        var item = sheet.GetRow(itemId);
        if (item.RowId == 0)
        {
            cached = new NoIconLookup(0, []);
            _cachedItemIcons[itemId] = cached;
            return cached;
        }

        uint categoryId = item.ClassJobCategory.RowId;

        if (_cachedCategoryIcons.TryGetValue(categoryId, out var icon))
        {
            _cachedItemIcons[itemId] = icon;
            return icon;
        }

        var jobs = _categoryIdToJobAbbrs.TryGetValue(categoryId, out var j) ? j : new List<string>();
        cached = new NoIconLookup(categoryId, jobs);
        _cachedItemIcons[itemId] = cached;
        return cached;
    }

    private void BuildCategoryJobMaps()
    {
        var classJobs = _dataManager.GetExcelSheet<ClassJob>();
        var categories = _dataManager.GetExcelSheet<ClassJobCategory>();
        if (classJobs == null || categories == null)
            return;

        var validAbbr = classJobs
            .Where(r => r.RowId != 0 && !string.IsNullOrWhiteSpace(r.Abbreviation.ToString()))
            .Select(r => r.Abbreviation.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var boolProps = typeof(ClassJobCategory)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(bool))
            .ToArray();

        foreach (var cat in categories)
        {
            if (cat.RowId == 0)
                continue;

            var list = new List<string>();

            foreach (var p in boolProps)
            {
                var name = p.Name;
                if (!validAbbr.Contains(name))
                    continue;

                bool enabled;
                try { enabled = (bool)p.GetValue(cat)!; }
                catch { continue; }

                if (enabled)
                    list.Add(name);
            }

            list.Sort(StringComparer.OrdinalIgnoreCase);
            _categoryIdToJobAbbrs[cat.RowId] = list;
        }
    }

    private void BuildCategoryIconMaps()
    {
        foreach (var (categoryId, abbrs) in _categoryIdToJobAbbrs)
        {
            var icon = MapCategory(categoryId, abbrs);
            if (icon != null)
                _cachedCategoryIcons[categoryId] = icon;
        }
    }

    private CachedLookup? MapCategory(uint categoryId, List<string> abbrs)
    {
        var jobs = abbrs
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (jobs.Count == 0)
            return null;

        NormalizeBaseJobs(jobs);
        jobs = jobs.OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();

        if (jobs.Count == 1 && DirectIconMappingsByAbbr.TryGetValue(jobs[0], out var iconId))
            return new CachedGameIconLookup(categoryId, jobs, new GameIconLookup(iconId));

        // Special case: ACN splits to SMN/SCH, but soul crystals are "shared-ish"
        if (jobs.Count == 2 &&
            jobs.Contains("SMN", StringComparer.OrdinalIgnoreCase) &&
            jobs.Contains("SCH", StringComparer.OrdinalIgnoreCase))
            return new CachedGameIconLookup(categoryId, jobs, new GameIconLookup(62300));

        // Role icons
        if (jobs.All(IsTank)) return new CachedGameIconLookup(categoryId, jobs, new GameIconLookup(62581));
        if (jobs.All(IsHealer)) return new CachedGameIconLookup(categoryId, jobs, new GameIconLookup(62582));
        if (jobs.All(IsMelee)) return new CachedGameIconLookup(categoryId, jobs, new GameIconLookup(62583));
        if (jobs.All(IsPhysicalRanged)) return new CachedGameIconLookup(categoryId, jobs, new GameIconLookup(62586));
        if (jobs.All(IsCaster)) return new CachedGameIconLookup(categoryId, jobs, new GameIconLookup(62587));

        // Crafter/Gatherer texture buttons
        if (jobs.All(IsCrafter))
            return new TexturePathLookup(categoryId, jobs, "ui/uld/togglebutton_hr1.tex", new Vector2(140, 40), new Vector2(38));
        if (jobs.All(IsGatherer))
            return new TexturePathLookup(categoryId, jobs, "ui/uld/togglebutton_hr1.tex", new Vector2(180, 40), new Vector2(38));

        // Scouting-like / phys ranged
        if (jobs.All(j => IsScoutingLike(j) || IsPhysicalRanged(j)))
            return new CachedGameIconLookup(categoryId, jobs, new GameIconLookup(176 + BaseColors.White));

        // Mixed DoWoM
        if (jobs.All(j => IsTank(j) || IsHealer(j) || IsMelee(j) || IsPhysicalRanged(j) || IsCaster(j)))
            return new CachedGameIconLookup(categoryId, jobs, new GameIconLookup(62146));

        // All classes and etc
        if (categoryId == 1)
            return new CachedGameIconLookup(categoryId, jobs, new GameIconLookup(62145));

        return null;
    }

    private static void NormalizeBaseJobs(List<string> jobs)
    {
        static void RemoveBaseIfHasJob(List<string> list, string baseAbbr, string jobAbbr)
        {
            if (list.Contains(baseAbbr, StringComparer.OrdinalIgnoreCase) &&
                list.Contains(jobAbbr, StringComparer.OrdinalIgnoreCase))
                list.RemoveAll(x => x.Equals(baseAbbr, StringComparison.OrdinalIgnoreCase));
        }

        RemoveBaseIfHasJob(jobs, "GLA", "PLD");
        RemoveBaseIfHasJob(jobs, "PGL", "MNK");
        RemoveBaseIfHasJob(jobs, "MRD", "WAR");
        RemoveBaseIfHasJob(jobs, "LNC", "DRG");
        RemoveBaseIfHasJob(jobs, "ARC", "BRD");
        RemoveBaseIfHasJob(jobs, "CNJ", "WHM");
        RemoveBaseIfHasJob(jobs, "THM", "BLM");
        RemoveBaseIfHasJob(jobs, "ROG", "NIN");

        if (jobs.Contains("ACN", StringComparer.OrdinalIgnoreCase) &&
            (jobs.Contains("SMN", StringComparer.OrdinalIgnoreCase) ||
             jobs.Contains("SCH", StringComparer.OrdinalIgnoreCase)))
        {
            jobs.RemoveAll(x => x.Equals("ACN", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static bool IsTank(string abbr) =>
        abbr.Equals("PLD", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("WAR", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("DRK", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("GNB", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("GLA", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("MRD", StringComparison.OrdinalIgnoreCase);

    private static bool IsHealer(string abbr) =>
        abbr.Equals("WHM", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("SCH", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("AST", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("SGE", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("CNJ", StringComparison.OrdinalIgnoreCase);

    private static bool IsMelee(string abbr) =>
        abbr.Equals("MNK", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("DRG", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("NIN", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("SAM", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("RPR", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("VPR", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("PGL", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("LNC", StringComparison.OrdinalIgnoreCase);

    private static bool IsPhysicalRanged(string abbr) =>
        abbr.Equals("BRD", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("MCH", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("DNC", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("ARC", StringComparison.OrdinalIgnoreCase);

    private static bool IsCaster(string abbr) =>
        abbr.Equals("BLM", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("SMN", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("RDM", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("PCT", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("BLU", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("ACN", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("THM", StringComparison.OrdinalIgnoreCase);

    private static bool IsCrafter(string abbr) =>
        abbr.Equals("CRP", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("BSM", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("ARM", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("GSM", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("LTW", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("WVR", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("ALC", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("CUL", StringComparison.OrdinalIgnoreCase);

    private static bool IsGatherer(string abbr) =>
        abbr.Equals("MIN", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("BTN", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("FSH", StringComparison.OrdinalIgnoreCase);

    private static bool IsScoutingLike(string abbr) =>
        abbr.Equals("ROG", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("NIN", StringComparison.OrdinalIgnoreCase) ||
        abbr.Equals("VPR", StringComparison.OrdinalIgnoreCase);

    // ---- Icon lookup types ----

    private abstract record CachedLookup(uint CategoryId, List<string> Jobs)
    {
        public abstract ISharedImmediateTexture? GetTexture(ITextureProvider textureProvider);
    }

    private sealed record NoIconLookup(uint CategoryId, List<string> Jobs)
        : CachedLookup(CategoryId, Jobs)
    {
        public override ISharedImmediateTexture? GetTexture(ITextureProvider textureProvider) => null;
    }

    private sealed record CachedGameIconLookup(uint CategoryId, List<string> Jobs, GameIconLookup Icon)
        : CachedLookup(CategoryId, Jobs)
    {
        public override ISharedImmediateTexture GetTexture(ITextureProvider textureProvider) =>
            textureProvider.GetFromGameIcon(Icon);
    }

    private sealed record TexturePathLookup(uint CategoryId, List<string> Jobs, string TexturePath, Vector2 TextureLocation, Vector2 TextureSize)
        : CachedLookup(CategoryId, Jobs)
    {
        public override ISharedImmediateTexture GetTexture(ITextureProvider textureProvider) =>
            textureProvider.GetFromGame(TexturePath);
    }


    private static void GetNodePosAndScale_UntilRoot(
        AtkResNode* node,
        AtkResNode* stopAtRoot,
        out float x, out float y,
        out float sx, out float sy)
    {
        x = node->X;
        y = node->Y;
        sx = node->ScaleX;
        sy = node->ScaleY;

        var p = node->ParentNode;
        while (p != null && p != stopAtRoot)
        {
            x = p->X + x * p->ScaleX;
            y = p->Y + y * p->ScaleY;

            sx *= p->ScaleX;
            sy *= p->ScaleY;

            p = p->ParentNode;
        }

        if (p == stopAtRoot && stopAtRoot != null)
        {
            x *= stopAtRoot->ScaleX;
            y *= stopAtRoot->ScaleY;

            sx *= stopAtRoot->ScaleX;
            sy *= stopAtRoot->ScaleY;
        }
    }

    private static void GetNodeScreenRect_FromUnitBaseRootRelative(
        AtkUnitBase* unitBase,
        AtkResNode* node,
        out Vector2 p0,
        out Vector2 p1)
    {
        var root = unitBase->RootNode;
        if (root == null)
        {
            float x = unitBase->X + node->X;
            float y = unitBase->Y + node->Y;
            float w = node->Width * node->ScaleX;
            float h = node->Height * node->ScaleY;

            p0 = new Vector2(x, y);
            p1 = new Vector2(x + w, y + h);
            return;
        }

        GetNodePosAndScale_UntilRoot(node, root, out var xRel, out var yRel, out var sx, out var sy);

        float screenX = unitBase->X + xRel;
        float screenY = unitBase->Y + yRel;

        float w2 = node->Width * sx;
        float h2 = node->Height * sy;

        p0 = new Vector2(screenX, screenY);
        p1 = new Vector2(screenX + w2, screenY + h2);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(nint hWnd, ref POINT lpPoint);

    private static Vector2 GetNonClientOffset()
    {
        var hwnd = Process.GetCurrentProcess().MainWindowHandle;
        if (hwnd == nint.Zero)
            return Vector2.Zero;

        if (!GetWindowRect(hwnd, out var wr))
            return Vector2.Zero;

        var pt = new POINT { X = 0, Y = 0 };
        if (!ClientToScreen(hwnd, ref pt))
            return Vector2.Zero;

        return new Vector2(pt.X - wr.Left, pt.Y - wr.Top);
    }

    private void UpdateNonClientOffset()
    {
        if (DateTime.UtcNow < _nextNonClientUpdate)
            return;

        _nextNonClientUpdate = DateTime.UtcNow.AddMilliseconds(250);

        try
        {
            _nonClientOffset = GetNonClientOffset();
        }
        catch
        {
            _nonClientOffset = Vector2.Zero;
        }
    }
}

public static unsafe class ItemOrderModuleSorterExtensions
{
    public static InventoryItem* GetInventoryItem(ref this ItemOrderModuleSorter sorter, long slotIndex)
    {
        if (sorter.Items.LongCount <= slotIndex) return null;

        var item = sorter.Items[slotIndex].Value;
        if (item == null) return null;

        var container = InventoryManager.Instance()->GetInventoryContainer(sorter.InventoryType + item->Page);
        if (container == null) return null;

        return container->GetInventorySlot(item->Slot);
    }
}