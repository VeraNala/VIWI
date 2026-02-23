using System;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

public static class ExpCalc
{
    // EXP needed to go from `level` to `level+1`
    public static uint GetExpToNextLevel(IDataManager dataManager, int level)
    {
        var sheet = dataManager.GetExcelSheet<ParamGrow>();
        var row = sheet?.GetRow((uint)level);

        if (row == null)
            throw new InvalidOperationException($"ParamGrow row not found for level {level}");

        return (uint)Math.Max(0, row.Value.ExpToNext);
    }

    // Remaining EXP to reach targetLevel for a specific job
    public static int GetExpRemainingToLevel(IDataManager dataManager, IPlayerState playerState, ClassJob job, int targetLevel)
    {
        int currentLevel = playerState.GetClassJobLevel(job);
        if (targetLevel <= currentLevel)
            return 0;

        long total = 0;

        long currentExpInLevel = playerState.GetClassJobExperience(job);

        uint toNext = GetExpToNextLevel(dataManager, currentLevel);
        total += Math.Max(0, (long)toNext - currentExpInLevel);

        for (int lv = currentLevel + 1; lv < targetLevel; lv++)
            total += GetExpToNextLevel(dataManager, lv);

        if (total <= 0) return 0;
        if (total > int.MaxValue) return int.MaxValue;
        return (int)total;
    }
}