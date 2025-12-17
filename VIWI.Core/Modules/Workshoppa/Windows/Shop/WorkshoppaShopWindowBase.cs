using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Numerics;
using VIWI.Modules.Workshoppa.External;

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
    public override void Draw() => DrawContent();
}