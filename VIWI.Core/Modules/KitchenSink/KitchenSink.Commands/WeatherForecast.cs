using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace VIWI.Modules.KitchenSink.Commands;

internal sealed class WeatherForecast : Window, IDisposable
{
    private readonly IClientState _clientState;
    private readonly ICommandManager _commandManager;
    private readonly IChatGui _chatGui;
    private readonly ITextureProvider _textureProvider;
    private readonly ExcelSheet<Weather> _weatherSheet;

    private bool _enabled;
    private int _count = 20;

    public WeatherForecast(
        IClientState clientState,
        IDataManager dataManager,
        ICommandManager commandManager,
        IChatGui chatGui,
        ITextureProvider textureProvider)
        : base("Weather Forecast###KsWeatherForecast")
    {
        _clientState = clientState;
        _commandManager = commandManager;
        _chatGui = chatGui;
        _textureProvider = textureProvider;

        _weatherSheet = dataManager.GetExcelSheet<Weather>(null, null)!;

        IsOpen = true;

        Flags |= ImGuiWindowFlags.NoScrollbar
              | ImGuiWindowFlags.NoScrollWithMouse
              | ImGuiWindowFlags.AlwaysAutoResize;

        _commandManager.AddHandler("/whatweather", new CommandInfo(ProcessCommand) { ShowInHelp = false });
    }

    private void ProcessCommand(string command, string arguments)
    {
        if (!string.IsNullOrWhiteSpace(arguments) &&
            int.TryParse(arguments.Trim(), out var result) &&
            result > 0)
        {
            _enabled = true;
            _count = result;
        }
        else
        {
            _enabled = !_enabled;
        }

        _chatGui.Print($"Weather overlay is now {(_enabled ? "enabled" : "disabled")}.", null, null);
    }

    public unsafe override bool DrawConditions()
    {
        if (!_enabled || !_clientState.IsLoggedIn)
            return false;

        var wm = WeatherManager.Instance();
        if (wm == null)
            return false;

        if (wm->HasIndividualWeather((ushort)_clientState.TerritoryType))
            return false;

        return true;
    }

    public unsafe override void Draw()
    {
        var wm = WeatherManager.Instance();
        if (wm == null)
            return;

        var territoryType = (ushort)_clientState.TerritoryType;

        for (int i = 0; i < _count; i++)
        {
            if (i > 0)
                ImGui.SameLine();

            var weatherId = (uint)wm->GetWeatherForDaytime(territoryType, i);
            var row = _weatherSheet.GetRow(weatherId);

            if (row.RowId == 0)
            {
                ImGui.TextUnformatted("?");
                continue;
            }
            var lookup = new GameIconLookup((uint)row.Icon, false, true, null);

            IDalamudTextureWrap? tex = null;
            try
            {
                tex = _textureProvider.GetFromGameIcon(lookup).GetWrapOrDefault();

                if (tex != null)
                {
                    ImGui.Image(tex.Handle, tex.Size / 2f);
                }
                else
                {
                    ImGui.TextUnformatted(row.Name.ToString());
                }
            }
            finally
            {
                tex?.Dispose();
            }
        }
    }

    public void Dispose()
    {
        _commandManager.RemoveHandler("/whatweather");
    }
}
