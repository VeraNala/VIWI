using System;
using System.Collections.Generic;
using System.Linq;
using VIWI.Modules.Workshoppa.GameData;

namespace VIWI.Modules.Workshoppa;

public sealed class WorkshoppaConfig
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = false;

    public CurrentItem? CurrentlyCraftedItem { get; set; }
    public List<QueuedItem> ItemQueue { get; set; } = new();
    public bool EnableRepairKitCalculator { get; set; } = true;
    public bool EnableCeruleumTankCalculator { get; set; } = true;
    public bool EnableMudstoneCalculator { get; set; } = true;
    public TurnInMode Mode = TurnInMode.Normal;
    public List<Preset> Presets { get; set; } = new();

    public sealed class QueuedItem
    {
        public uint WorkshopItemId { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class CurrentItem
    {
        public uint WorkshopItemId { get; set; }
        public bool StartedCrafting { get; set; }
        public uint PhasesComplete { get; set; }
        public List<PhaseItem> ContributedItemsInCurrentPhase { get; set; } = new();

        public bool UpdateFromCraftState(CraftState craftState)
        {
            bool changed = false;
            if (PhasesComplete != craftState.StepsComplete)
            {
                PhasesComplete = craftState.StepsComplete;
                changed = true;
            }

            if (ContributedItemsInCurrentPhase.Count != craftState.Items.Count)
            {
                ContributedItemsInCurrentPhase = craftState.Items.Select(x => new PhaseItem
                {
                    ItemId = x.ItemId,
                    QuantityComplete = x.QuantityComplete,
                }).ToList();
                changed = true;
            }
            else
            {
                for (int i = 0; i < ContributedItemsInCurrentPhase.Count; ++i)
                {
                    var contributedItem = ContributedItemsInCurrentPhase[i];
                    var craftItem = craftState.Items[i];
                    if (contributedItem.ItemId != craftItem.ItemId)
                    {
                        contributedItem.ItemId = craftItem.ItemId;
                        changed = true;
                    }

                    if (contributedItem.QuantityComplete != craftItem.QuantityComplete)
                    {
                        contributedItem.QuantityComplete = craftItem.QuantityComplete;
                        changed = true;
                    }
                }
            }

            return changed;
        }
    }

    public sealed class PhaseItem
    {
        public uint ItemId { get; set; }
        public uint QuantityComplete { get; set; }
    }

    public sealed class Preset
    {
        public required Guid Id { get; set; }
        public required string Name { get; set; }
        public List<QueuedItem> ItemQueue { get; set; } = new();
    }
    public enum TurnInMode
    {
        Normal = 0,
        Leveling = 1,
    }
}
