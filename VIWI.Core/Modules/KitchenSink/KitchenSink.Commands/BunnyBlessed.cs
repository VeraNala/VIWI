using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Pictomancy;

namespace VIWI.Modules.KitchenSink.Commands;

internal sealed class BunnyBlessed : IDisposable
{
	private static readonly List<Vector3> CarrotLocations;

	private readonly IDalamudPluginInterface _pluginInterface;

	private readonly IClientState _clientState;

	private readonly IFramework _framework;

	private readonly ICommandManager _commandManager;

	private readonly IChatGui _chatGui;

	private readonly IObjectTable _objectTable;

	private readonly List<Vector3> _unknownLocations = new List<Vector3>();

	private readonly Queue<Vector3> _checkedLocations = new Queue<Vector3>();

	private Vector3 _lastPlayerPosition = Vector3.Zero;

	private bool _enabled;

	public BunnyBlessed(IDalamudPluginInterface pluginInterface, IClientState clientState, IFramework framework, ICommandManager commandManager, IChatGui chatGui, IObjectTable objectTable)
	{
		_pluginInterface = pluginInterface;
		_clientState = clientState;
		_framework = framework;
		_commandManager = commandManager;
		_chatGui = chatGui;
		_objectTable = objectTable;
		PictoService.Initialize(pluginInterface);
		_clientState.TerritoryChanged += OnTerritoryChanged;
		_pluginInterface.UiBuilder.Draw += Draw;
        _framework.Update += OnFrameworkUpdate;
        _commandManager.AddHandler("/bunbun", new CommandInfo(ProcessCommand) { ShowInHelp = false });
		UpdateMapMarkers();
	}

	private void OnTerritoryChanged(uint territory)
	{
		if (_enabled && territory == 1252)
		{
			UpdateMapMarkers();
			return;
		}
		_unknownLocations.Clear();
		_checkedLocations.Clear();
		ResetMarkers();
	}

	private void OnFrameworkUpdate(IFramework framework)
	{
		IPlayerCharacter localPlayer = _objectTable.LocalPlayer!;
		_lastPlayerPosition = ((localPlayer != null) ? ((IGameObject)localPlayer).Position : Vector3.Zero);
	}

	private void ProcessCommand(string command, string arguments)
	{
		_enabled = !_enabled;
		_chatGui.Print("Bunny locations are now " + (_enabled ? "marked" : "disabled") + ".", (string?)null, (ushort?)null);
		OnTerritoryChanged(_clientState.TerritoryType);
	}

	private void Draw()
	{
		if (!_enabled || !_clientState.IsLoggedIn || _clientState.TerritoryType != 1252 || _lastPlayerPosition == Vector3.Zero)
		{
			return;
		}
		PctDrawList val = PictoService.Draw((ImDrawListPtr?)null, (PctDrawHints?)null)!;
		try
		{
			if (val == null)
			{
				return;
			}
			bool flag = false;
			foreach (Vector3 carrot in CarrotLocations)
			{
				float num = Vector3.Distance(carrot, _lastPlayerPosition);
				if (!(num < 130f))
				{
					continue;
				}
				bool flag2 = ((IEnumerable<IGameObject>)_objectTable).Any((IGameObject x) => x.BaseId == 2010139 && Vector3.Distance(carrot, x.Position) < 1f);
				if (num < 105f || flag2)
				{
					if (flag2)
					{
						val.AddLine(_lastPlayerPosition, carrot, 0f, 4278222847u, 5f);
						if (_checkedLocations.Count != 1 || _checkedLocations.Peek() != carrot)
						{
							_unknownLocations.Clear();
							_checkedLocations.Clear();
							_checkedLocations.Enqueue(carrot);
							_unknownLocations.AddRange(CarrotLocations.Except(_checkedLocations));
							UpdateMapMarkers();
						}
					}
					else if (!_checkedLocations.Contains(carrot) && _unknownLocations.Remove(carrot))
					{
						_checkedLocations.Enqueue(carrot);
						UpdateMapMarkers();
					}
				}
				flag = flag || flag2;
			}
			if (flag)
			{
				return;
			}
			bool flag3 = true;
			foreach (Vector3 item in _unknownLocations.OrderBy((Vector3 x) => Vector3.Distance(_lastPlayerPosition, x)).Take(3))
			{
				Vector3 zero = Vector3.Zero;
				val.AddLine(_lastPlayerPosition + zero, item, 0f, flag3 ? 4278255615u : uint.MaxValue, (float)((!flag3) ? 1 : 3));
				val.AddText(item, uint.MaxValue, $"{Vector3.Distance(_lastPlayerPosition, item):N1}", 1f);
				flag3 = false;
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void UpdateMapMarkers()
	{
		ResetMarkers();
		if (_unknownLocations.Count == 0)
		{
			if (_checkedLocations.Count == CarrotLocations.Count)
			{
				_unknownLocations.Add(_checkedLocations.Dequeue());
			}
			else
			{
				_checkedLocations.Clear();
				_unknownLocations.AddRange(CarrotLocations);
			}
		}
		foreach (Vector3 unknownLocation in _unknownLocations)
		{
			SetMarkers(unknownLocation, unknownLocation, 25207u);
		}
	}

    private unsafe void ResetMarkers()
    {
        var map = AgentMap.Instance();
        if (map == null) return;
        map->ResetMapMarkers();
        map->ResetMiniMapMarkers();
    }

    private unsafe void SetMarkers(Vector3 worldPos, Vector3 mapPos, uint iconId, int scale = 0)
    {
        var map = AgentMap.Instance();
        if (map == null) return;

        if (map->AddMapMarker(mapPos, iconId, scale, (byte*)null, 3, 0))
            map->AddMiniMapMarker(worldPos, iconId, scale);
    }

    public void Dispose()
	{
		_commandManager.RemoveHandler("/bunbun");
        _framework.Update -= OnFrameworkUpdate;
        _clientState.TerritoryChanged -= OnTerritoryChanged;
		_pluginInterface.UiBuilder.Draw -= Draw;
		PictoService.Dispose();
	}

	static BunnyBlessed()
	{
		int num = 25;
		List<Vector3> list = new List<Vector3>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<Vector3> span = CollectionsMarshal.AsSpan(list);
		int num2 = 0;
		span[num2] = new Vector3(-439.0463f, 115.82392f, 184.4665f);
		num2++;
		span[num2] = new Vector3(-727.8528f, 81.47683f, 328.9311f);
		num2++;
		span[num2] = new Vector3(-701.8768f, 201f, 718.7181f);
		num2++;
		span[num2] = new Vector3(-743.601f, 96.39003f, 84.43998f);
		num2++;
		span[num2] = new Vector3(248.9159f, 55.999996f, 791.1138f);
		num2++;
		span[num2] = new Vector3(-174.0473f, 121.00001f, 107.6488f);
		num2++;
		span[num2] = new Vector3(-575.6361f, 162.39511f, 668.7043f);
		num2++;
		span[num2] = new Vector3(-273.0878f, 75f, 850.0336f);
		num2++;
		span[num2] = new Vector3(650.2321f, 108f, 141.1927f);
		num2++;
		span[num2] = new Vector3(-84.73673f, 2.999999f, -796.0166f);
		num2++;
		span[num2] = new Vector3(-843.8602f, 83.657074f, -36.78173f);
		num2++;
		span[num2] = new Vector3(-400.528f, 2.999999f, -518.3032f);
		num2++;
		span[num2] = new Vector3(827.2007f, 108f, -156.4444f);
		num2++;
		span[num2] = new Vector3(772.3591f, 70.3f, 531.1259f);
		num2++;
		span[num2] = new Vector3(845.5334f, 98f, 777.4331f);
		num2++;
		span[num2] = new Vector3(720.4133f, 120f, 271.05f);
		num2++;
		span[num2] = new Vector3(865.0009f, 95.99958f, -214.6744f);
		num2++;
		span[num2] = new Vector3(283.6546f, 55.999996f, 587.3107f);
		num2++;
		span[num2] = new Vector3(-710.266f, 3f, -451.5128f);
		num2++;
		span[num2] = new Vector3(-806.5123f, 107f, 887.6146f);
		num2++;
		span[num2] = new Vector3(-771.6308f, 5f, -694.0016f);
		num2++;
		span[num2] = new Vector3(-490.3187f, 3f, -741.0153f);
		num2++;
		span[num2] = new Vector3(466.2025f, 70.3f, 563.2519f);
		num2++;
		span[num2] = new Vector3(477.4074f, 96.10128f, 138.6543f);
		num2++;
		span[num2] = new Vector3(-554.0244f, 110.698654f, -365.897f);
		CarrotLocations = list;
	}
}
