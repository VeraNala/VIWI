using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Linq;
using System.Threading;

namespace VIWI.Modules.KitchenSink.Commands;

internal sealed class Leves : IDisposable
{
	private readonly IFramework _framework;
	private readonly IClientState _clientState;
    private readonly IPlayerState _playerState;
	private readonly IChatGui _chatGui;
	private readonly KitchenSinkConfig _configuration;

	public Leves(IFramework framework, IClientState clientState, IPlayerState playerState, IChatGui chatGui, KitchenSinkConfig configuration)
	{
		_framework = framework;
		_clientState = clientState;
        _playerState = playerState;
		_chatGui = chatGui;
		_configuration = configuration;
		_clientState.Login += OnLogin;
		if (_clientState.IsLoggedIn)
		{
			OnLogin();
		}
	}

	private unsafe void OnLogin()
	{
		KitchenSinkConfig.CharacterData characterData = _configuration.Characters.FirstOrDefault((KitchenSinkConfig.CharacterData x) => _playerState.ContentId == x.LocalContentId)!;
		if (characterData == null || !characterData.WarnAboutLeves)
		{
			return;
		}
		_framework.RunOnTick((Action)delegate
		{
			try
			{
                var qm = QuestManager.Instance();
                if (qm == null)
                    return;

                byte numLeveAllowances = qm->NumLeveAllowances;
                _chatGui.Print(new SeStringBuilder().AddUiForeground($"[KitchenSink] Leve Allowances: {numLeveAllowances}", (ushort)((numLeveAllowances < 80) ? 62u : 511u)).Build(), (string?)null, (ushort?)null);
			}
			catch
			{
			}
		}, TimeSpan.FromSeconds(2L), 0, default(CancellationToken));
	}

	public void Dispose()
	{
		_clientState.Login -= OnLogin;
	}
}
