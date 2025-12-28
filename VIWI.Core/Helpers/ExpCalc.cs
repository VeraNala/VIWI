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

    public static long GetExpRemainingToLevel(
       IDataManager dataManager,
       IPlayerState playerState,
       int currentLevel,
       int targetLevel)
    {
        if (targetLevel <= currentLevel)
            return 0;

        long total = 0;

        // EXP already earned in the current level
        long currentExpInLevel =
            playerState.GetClassJobExperience(playerState.ClassJob.Value);

        // Remaining EXP to next level
        uint toNext = GetExpToNextLevel(dataManager, currentLevel);
        total += Math.Max(0, toNext - currentExpInLevel);

        // Full levels in between
        for (int lv = currentLevel + 1; lv < targetLevel; lv++)
            total += GetExpToNextLevel(dataManager, lv);

        return total;
    }
}
