using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Logging;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Windows.Forms.VisualStyles;

namespace VIWI.Modules.Workshoppa
{

    internal static class WorkshoppaHelpers
    {
        public const ushort PreferredWorldBonusStatusId = 1411;
        private static readonly HashSet<uint> FabricationStationBaseIds = new()
        {
            2005236,
            2005238,
            2005240,
            2007821,
            2011588,
        };
        internal static bool TryGetNearestFabricationStation(out IGameObject? obj)
        {
            obj = null;

            if (!Player.Available || Player.Object == null)
                return false;

            var playerPos = Player.Object.Position;

            obj = Svc.Objects
                .Where(x => x != null && x.IsTargetable && FabricationStationBaseIds.Contains(x.BaseId)).OrderBy(x => Vector3.DistanceSquared(x.Position, playerPos)).FirstOrDefault();

            return obj != null;
        }

        internal static bool Lockon()
        {
            if (TryGetNearestFabricationStation(out var obj) && obj != null)
            {
                if (Svc.Targets.Target?.Address != obj.Address)
                {
                    Svc.Targets.Target = obj;
                    return false;
                }

                Chat.ExecuteCommand("/lockon on");
                return true;
            }

            return false;
        }

        internal static bool Approach()
        {
            if (TryGetNearestFabricationStation(out var obj) &&
                obj != null &&
                Svc.Targets.Target?.Address == obj.Address)
            {
                Chat.ExecuteCommand("/automove on");
                return true;
            }

            return false;
        }

        internal static bool AutomoveOffStation()
        {
            if (!Player.Available || Player.Object == null)
                return false;

            if (TryGetNearestFabricationStation(out var obj) &&
                obj != null &&
                Svc.Targets.Target?.Address == obj.Address)
            {
                if (Vector3.Distance(obj.Position, Player.Object.Position) < 3.2f)
                {
                    Chat.ExecuteCommand("/automove off");
                    return true;
                }
            }

            return false;
        }

    public static bool HasStatus(IPlayerCharacter player, ushort statusId)
        {
            foreach (var s in player.StatusList)
            {
                if (s.StatusId == statusId)
                    return true;
            }
            return false;
        }
        public static (int qtyText, bool eligible, string statusText) ComputeRow(IDataManager dm, IPlayerState ps, ClassJob? job, int targetLevel, bool hasPreferredWorldBonus, int minRequiredLevel, int expPerMaterial, int materialsPerTurnin)
        {
            targetLevel = ClampTargetLevel(targetLevel);

            if (job == null)
                return (0, false, "Job data missing");

            int lvl = ps.GetClassJobLevel(job.Value);
            if (lvl <= 0)
                return (0, false, "Not unlocked");

            if (lvl < minRequiredLevel)
                return (0, false, $"Requires Lv {minRequiredLevel} (you are Lv {lvl})");

            if (lvl >= 90)
                return (0, false, "Already Lv 90");

            int mult = (hasPreferredWorldBonus && lvl < 90) ? 2 : 1;

            int expRemaining = ExpCalc.GetExpRemainingToLevel(dm, ps, job.Value, targetLevel);

            int mats = CalcTurninMaterialCount(expRemaining, expPerMaterial, materialsPerTurnin, mult);
            return (mats, true, "OK");
        }

        public static ClassJob? GetJobByAbbrev(IDataManager dm, string abbrev)
        {
            var sheet = dm.GetExcelSheet<ClassJob>();
            if (sheet == null) return null;

            foreach (var row in sheet)
            {
                if (row.RowId == 0) continue;
                if (row.Abbreviation.ToString() == abbrev)
                    return row;
            }

            return null;
        }

        public static int ClampTargetLevel(int value)
        {
            if (value < 1) return 1;
            if (value > 90) return 90;
            return value;
        }

        private static int CeilDiv(int numerator, int denominator)
        {
            if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
            if (numerator <= 0) return 0;
            return (numerator + denominator - 1) / denominator;
        }

        private static int CalcTurninMaterialCount(int expRemaining, int expPerMaterial, int materialsPerTurnin, int expGainMultiplier)
        {
            if (expRemaining <= 0) return 0;
            if (expGainMultiplier < 1) expGainMultiplier = 1;

            int expPerTurnin = expPerMaterial * materialsPerTurnin;
            int effectiveExpPerTurnin = expPerTurnin * expGainMultiplier;

            int turnins = CeilDiv(expRemaining, effectiveExpPerTurnin);
            return turnins * materialsPerTurnin;
        }
    }
}
