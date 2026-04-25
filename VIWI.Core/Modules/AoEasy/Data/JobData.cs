using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;
using VIWI.Core;

namespace VIWI.Modules.AoEasy
{
    public enum Job
    {
        Unknown = 0,
        Tank = 1,
        Healer = 2,
        MeleeDps = 3,
        PhysicalRangedDps = 4,
        MagicalRangedDps = 5,
        Crafter = 6,
        Gatherer = 7,
    }
    public sealed class JobInfo
    {
        public uint RowId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Abbreviation { get; init; } = string.Empty;
        public byte RawRole { get; init; }
        public Job Role { get; init; }
        public bool CanQueueForDuty { get; init; }
        public bool IsLimitedJob { get; init; }
        public bool IsBattleJob { get; init; }
    }

    public sealed class JobAbilityInfo
    {
        public uint Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public ushort IconId { get; init; }
        public byte Level { get; init; }

        public bool IsRoleAction { get; init; }
        public bool IsPlayerAction { get; init; }
        public bool IsPvP { get; init; }

        public byte CooldownGroup { get; init; }
        public ushort Recast100ms { get; init; }
        public sbyte Range { get; init; }

        public uint ClassJobId { get; init; }
        public string ClassJobCategoryName { get; init; } = string.Empty;
    }

    public static class JobData
    {
        // -----------------------------
        // Job metadata
        // -----------------------------
        private static readonly Dictionary<uint, JobInfo> Jobs = new();
        private static bool jobsInitialized;

        public static IReadOnlyDictionary<uint, JobInfo> AllJobs => Jobs;

        public static void InitializeJobs()
        {
            if (jobsInitialized)
                return;

            var sheet = VIWIContext.DataManager.GetExcelSheet<ClassJob>();
            if (sheet == null)
                return;

            foreach (var row in sheet)
            {
                if (row.RowId == 0)
                    continue;

                var name = row.Name.ExtractText();
                var abbr = row.Abbreviation.ExtractText();
                var role = row.Role;
                var mappedRole = MapRole(role);

                var info = new JobInfo
                {
                    RowId = row.RowId,
                    Name = name,
                    Abbreviation = abbr,
                    RawRole = role,
                    Role = mappedRole,
                    CanQueueForDuty = row.CanQueueForDuty,
                    IsLimitedJob = row.IsLimitedJob,
                    IsBattleJob = mappedRole is Job.Tank
                                              or Job.Healer
                                              or Job.MeleeDps
                                              or Job.PhysicalRangedDps
                                              or Job.MagicalRangedDps,
                };

                Jobs[row.RowId] = info;
            }

            jobsInitialized = true;
        }

        public static bool TryGet(uint rowId, out JobInfo info)
        {
            InitializeJobs();
            return Jobs.TryGetValue(rowId, out info!);
        }

        private static Job MapRole(byte role) => role switch
        {
            1 => Job.Tank,
            2 => Job.Healer,
            3 => Job.MeleeDps,
            4 => Job.PhysicalRangedDps,
            5 => Job.MagicalRangedDps,
            6 => Job.Crafter,
            7 => Job.Gatherer,
            _ => Job.Unknown,
        };

        // -----------------------------
        // Ability metadata
        // -----------------------------
        private static readonly Dictionary<uint, List<JobAbilityInfo>> AbilitiesByJob = new();
        private static bool abilitiesInitialized;
        public static void InitializeAbilities()
        {
            if (abilitiesInitialized)
                return;

            var actionSheet = VIWIContext.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (actionSheet == null)
                return;

            foreach (var row in actionSheet)
            {
                if (!row.IsPlayerAction || row.IsPvP)
                    continue;

                var jobId = row.ClassJob.RowId;
                if (jobId == 0)
                    continue;

                var name = row.Name.ExtractText();
                if (string.IsNullOrEmpty(name))
                    continue;

                if (!AbilitiesByJob.TryGetValue(jobId, out var list))
                {
                    list = new List<JobAbilityInfo>();
                    AbilitiesByJob[jobId] = list;
                }

                var ability = new JobAbilityInfo
                {
                    Id = row.RowId,
                    Name = name,
                    IconId = row.Icon,
                    Level = row.ClassJobLevel,
                    IsRoleAction = row.IsRoleAction,
                    IsPlayerAction = row.IsPlayerAction,
                    IsPvP = row.IsPvP,
                    CooldownGroup = row.CooldownGroup,
                    Recast100ms = row.Recast100ms,
                    Range = row.Range,
                    ClassJobId = jobId,
                    ClassJobCategoryName = row.ClassJobCategory.Value.Name.ExtractText() ?? string.Empty,
                };

                list.Add(ability);
            }
            foreach (var list in AbilitiesByJob.Values)
            {
                list.Sort((a, b) =>
                {
                    var levelCmp = a.Level.CompareTo(b.Level);
                    return levelCmp != 0
                        ? levelCmp
                        : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
                });
            }

            abilitiesInitialized = true;
        }
        public static IReadOnlyList<JobAbilityInfo> GetAbilitiesForJob(uint jobId)
        {
            InitializeAbilities();
            return AbilitiesByJob.TryGetValue(jobId, out var list)
                ? list
                : Array.Empty<JobAbilityInfo>();
        }
        public static IReadOnlyList<JobAbilityInfo> GetAbilitiesForLocalPlayer()
        {
            InitializeAbilities();

            var player = VIWIContext.PlayerState;
            if (player == null)
                return Array.Empty<JobAbilityInfo>();

            var jobId = player.ClassJob.RowId;
            return GetAbilitiesForJob(jobId);
        }
    }
}
