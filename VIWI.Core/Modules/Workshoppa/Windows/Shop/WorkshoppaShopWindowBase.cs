using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Numerics;
using VIWI.Modules.Workshoppa.External;
using VIWI.Helpers;

namespace VIWI.Modules.Workshoppa.Windows.Shop;

internal abstract unsafe class WorkshoppaShopWindowBase : Window, IDisposable
{
    protected readonly IPluginLog _pluginLog;
    protected readonly ExternalPluginHandler _externalPluginHandler;

    protected RegularShopController Shop { get; }
    protected WorkshoppaShopWindowBase(
        string windowName,
        string addonName,
        IPluginLog pluginLog,
        IGameGui gameGui,
        IAddonLifecycle addonLifecycle,
        ExternalPluginHandler externalPluginHandler)
        : base(windowName)
    {
        _pluginLog = pluginLog;
        _externalPluginHandler = externalPluginHandler;
        
        Shop = new RegularShopController(
            this,
            addonName,
            pluginLog,
            gameGui,
            addonLifecycle
        );

        RespectCloseHotkey = true;
        IsOpen = false;
        Position = new Vector2(100, 100);
        PositionCondition = ImGuiCond.Always;
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoCollapse;

    }
    private bool followAddon = true;

    public bool FollowAddon
    {
        get => followAddon;
        set => followAddon = value;
    }
    private bool _userMoved;
    public void EnableShopListeners() => Shop.Enable();
    public void DisableShopListeners() => Shop.Disable();
    public void Dispose() => Shop.Dispose();
    public void SaveExternalPluginState() => _externalPluginHandler.Save();
    public void RestoreExternalPluginState() => _externalPluginHandler.Restore();
    public abstract bool IsEnabled { get; }
    public bool AutoBuyEnabled => Shop.PurchaseState != null;

    public bool IsAwaitingYesNo
    {
        get => Shop.PurchaseState?.IsAwaitingYesNo ?? false;
        set
        {
            if (Shop.PurchaseState != null)
                Shop.PurchaseState.IsAwaitingYesNo = value;
        }
    }
    public abstract int GetCurrencyCount();
    public abstract void UpdateShopStock(AtkUnitBase* addon);
    public abstract void TriggerPurchase(AtkUnitBase* addonShop, int buyNow);

    protected abstract void DrawContent();
    public override void Draw()
    {
        if (ImGui.IsWindowAppearing() == false)
        {
            if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows) &&
                ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                _userMoved = true;
                FollowAddon = false;
            }
        }
        DrawContent();
    }
    internal void SnapToAddonClamped(short addonX, short addonY, short addonW, short addonH, Vector2 offset)
    {
        var desired = new Vector2(addonX + addonW, addonY) + offset;

        var vp = ImGuiWindowHelper.FindBestViewport(desired);

        const float margin = 12f;

        var size = (this.Size is { X: > 1, Y: > 1 })
            ? this.Size.Value
            : new Vector2(300, 200);

        var clamped = ImGuiWindowHelper.ClampToViewport(desired, vp, margin, size);

        if (Position is not Vector2 current || Vector2.Distance(current, clamped) > 2f)
        {
            Position = clamped;
            PositionCondition = ImGuiCond.Appearing;
        }
    }
    /* REVISIT LATER IDK
    protected void DrawFollowControls()
    {
        ImGui.Checkbox("Follow shop window", ref followAddon);

        ImGui.SameLine();
        if (ImGui.Button("Reset position"))
        {
            FollowAddon = true;
            Position = new Vector2(100, 100);
            PositionCondition = ImGuiCond.Appearing;
        }

        ImGui.Separator();
    }*/
}