using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Numerics;
using VIWI.Helpers;

namespace VIWI.Modules.Workshoppa.Windows.Shop;

internal sealed unsafe class RegularShopController : IDisposable
{
    private readonly WorkshoppaShopWindowBase _parent;
    private readonly string _addonName;
    private readonly IPluginLog _log;
    private readonly IGameGui _gameGui;
    private readonly IAddonLifecycle _addonLifecycle;

    public RegularShopController(
        WorkshoppaShopWindowBase parent,
        string addonName,
        IPluginLog log,
        IGameGui gameGui,
        IAddonLifecycle addonLifecycle)
    {
        _parent = parent;
        _addonName = addonName;
        _log = log;
        _gameGui = gameGui;
        _addonLifecycle = addonLifecycle;
    }

    public ShopItemForSale? ItemForSale { get; set; }
    public ShopPurchaseState? PurchaseState { get; private set; }
    public bool AutoBuyEnabled => PurchaseState != null;

    public bool IsAwaitingYesNo
    {
        get => PurchaseState?.IsAwaitingYesNo ?? false;
        set { if (PurchaseState != null) PurchaseState.IsAwaitingYesNo = value; }
    }

    private void ShopPostSetup(AddonEvent type, AddonArgs args)
    {
        if (!_parent.IsEnabled)
        {
            ItemForSale = null;
            _parent.IsOpen = false;
            return;
        }

        var addr = args.Addon.Address;
        if (addr == nint.Zero) return;

        _parent.UpdateShopStock((AtkUnitBase*)addr);
        PostUpdateShopStock();

        if (ItemForSale != null)
            _parent.IsOpen = true;
    }

    private void ShopPreFinalize(AddonEvent type, AddonArgs args)
    {
        PurchaseState = null;
        _parent.RestoreExternalPluginState();
        _parent.IsOpen = false;
    }

    private void ShopPostUpdate(AddonEvent type, AddonArgs args)
    {
        if (!_parent.IsEnabled)
        {
            ItemForSale = null;
            _parent.IsOpen = false;
            return;
        }

        var addr = args.Addon.Address;
        if (addr == nint.Zero) return;

        var addon = (AtkUnitBase*)addr;

        _parent.UpdateShopStock(addon);
        PostUpdateShopStock();

        if (ItemForSale != null)
        {
            short x = 0, y = 0;
            addon->GetPosition(&x, &y);

            short width = 0, height = 0;
            addon->GetSize(&width, &height, true);
            x += width;

            // position our window to the right of the shop addon
            var desired = new Vector2(x, y);
            if (_parent.Position is { } pos && ((short)pos.X != x || (short)pos.Y != y))
                _parent.Position = desired;

            _parent.IsOpen = true;
        }
        else
        {
            _parent.IsOpen = false;
        }
    }

    private void PostUpdateShopStock()
    {
        if (ItemForSale != null && PurchaseState != null)
        {
            int ownedItems = (int)ItemForSale.Value.OwnedItems;
            if (PurchaseState.OwnedItems != ownedItems)
            {
                PurchaseState.OwnedItems = ownedItems;
                PurchaseState.NextStep = DateTime.Now.AddSeconds(0.25);
            }
        }
    }

    public int GetItemCount(uint itemId)
    {
        var inv = InventoryManager.Instance();
        return inv == null ? 0 : inv->GetInventoryItemCount(itemId, checkEquipped: false, checkArmory: false);
    }

    public int GetMaxItemsToPurchase()
    {
        if (ItemForSale == null) return 0;
        int currency = _parent.GetCurrencyCount();
        return ItemForSale.Value.Price == 0 ? 0 : (int)(currency / ItemForSale.Value.Price);
    }

    public void CancelAutoPurchase()
    {
        PurchaseState = null;
        _parent.RestoreExternalPluginState();
    }

    public void StartAutoPurchase(int toPurchase)
    {
        if (ItemForSale == null) return;
        PurchaseState = new ShopPurchaseState((int)ItemForSale.Value.OwnedItems + toPurchase, (int)ItemForSale.Value.OwnedItems);
        _parent.SaveExternalPluginState();
    }

    public void HandleNextPurchaseStep()
    {
        if (ItemForSale == null || PurchaseState == null) return;

        int maxStackSize = DetermineMaxStackSize(ItemForSale.Value.ItemId);
        if (maxStackSize == 0 && !HasFreeInventorySlot())
        {
            _log.Warning($"No free inventory slots, can't buy more {ItemForSale.Value.ItemName}");
            PurchaseState = null;
            _parent.RestoreExternalPluginState();
            return;
        }

        if (!PurchaseState.IsComplete)
        {
            if (PurchaseState.NextStep <= DateTime.Now &&
                AddonHelpers.TryGetAddonByName(_gameGui, _addonName, out AtkUnitBase* addonShop))
            {
                int buyNow = Math.Min(PurchaseState.ItemsLeftToBuy, maxStackSize);
                _log.Information($"Buying {buyNow}x {ItemForSale.Value.ItemName}");

                _parent.TriggerPurchase(addonShop, buyNow);

                PurchaseState.NextStep = DateTime.MaxValue;
                PurchaseState.IsAwaitingYesNo = true;
            }

            return;
        }

        _log.Information($"Stopping item purchase (desired = {PurchaseState.DesiredItems}, owned = {PurchaseState.OwnedItems})");
        PurchaseState = null;
        _parent.RestoreExternalPluginState();
    }

    public bool HasFreeInventorySlot() => CountFreeInventorySlots() > 0;

    public int CountFreeInventorySlots()
    {
        var inv = InventoryManager.Instance();
        if (inv == null) return 0;

        int count = 0;
        for (InventoryType t = InventoryType.Inventory1; t <= InventoryType.Inventory4; ++t)
        {
            var container = inv->GetInventoryContainer(t);
            for (int i = 0; i < container->Size; ++i)
            {
                var item = container->GetInventorySlot(i);
                if (item == null || item->ItemId == 0)
                    ++count;
            }
        }
        return count;
    }
    public unsafe int CountInventorySlotsWithCondition(uint itemId, Predicate<int> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
            return 0;

        int count = 0;
        for (InventoryType t = InventoryType.Inventory1; t <= InventoryType.Inventory4; ++t)
        {
            var container = inventoryManager->GetInventoryContainer(t);
            for (int i = 0; i < container->Size; ++i)
            {
                var item = container->GetInventorySlot(i);
                if (item == null || item->ItemId == 0)
                    continue;

                if (item->ItemId == itemId && predicate(item->Quantity))
                    ++count;
            }
        }

        return count;
    }

    private int DetermineMaxStackSize(uint itemId)
    {
        var inv = InventoryManager.Instance();
        if (inv == null) return 0;

        int max = 0;
        for (InventoryType t = InventoryType.Inventory1; t <= InventoryType.Inventory4; ++t)
        {
            var container = inv->GetInventoryContainer(t);
            for (int i = 0; i < container->Size; ++i)
            {
                var item = container->GetInventorySlot(i);
                if (item == null || item->ItemId == 0)
                    return 99;

                if (item->ItemId == itemId)
                {
                    max += (999 - item->Quantity);
                    if (max >= 99)
                        break;
                }
            }
        }
        return Math.Min(99, max);
    }
    private bool _enabled;

    public void Enable()
    {
        if (_enabled) return;
        _enabled = true;

        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, _addonName, ShopPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PreFinalize, _addonName, ShopPreFinalize);
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate, _addonName, ShopPostUpdate);
    }

    public void Disable()
    {
        if (!_enabled) return;
        _enabled = false;

        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, _addonName, ShopPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, _addonName, ShopPreFinalize);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, _addonName, ShopPostUpdate);

        PurchaseState = null;
        ItemForSale = null;
        _parent.RestoreExternalPluginState();
        _parent.IsOpen = false;
    }
    public void Dispose()
    {
        Disable();
    }
}
