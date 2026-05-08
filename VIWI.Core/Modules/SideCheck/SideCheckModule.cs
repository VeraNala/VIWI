using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using System;
using System.Numerics;
using VIWI.Core;
using static VIWI.Core.VIWIContext;

namespace VIWI.Modules.SideCheck
{
    public sealed class SideCheckModule : VIWIModuleBase<SideCheckConfig>
    {
        public const string ModuleName = "SideCheck";
        public const string ModuleVersion = "0.0.1";
        public override string Name => ModuleName;
        public override string Version => ModuleVersion;
        public SideCheckConfig _configuration => ModuleConfig;
        private VIWIConfig Core => CoreConfig;
        internal static SideCheckModule? Instance { get; private set; }
        public static bool Enabled => Instance?._configuration.Enabled ?? false;

        protected override SideCheckConfig CreateConfig() => new SideCheckConfig();
        protected override SideCheckConfig GetConfigBranch(VIWIConfig core) => core.SideCheck;
        protected override void SetConfigBranch(VIWIConfig core, SideCheckConfig _configuration) => core.SideCheck = _configuration;
        protected override bool GetEnabled(SideCheckConfig _configuration) => _configuration.Enabled;
        protected override void SetEnabledValue(SideCheckConfig _configuration, bool enabled) => _configuration.Enabled = enabled;
        public void SaveConfig() => Core.Save();

        private PositionalQuadrant _lastQuadrant = PositionalQuadrant.Unknown;
        private DateTime _nextDebugPrint = DateTime.MinValue;

        // ----------------------------
        // Module Base
        // ----------------------------
        public override void Initialize(VIWIConfig config)
        {
            Instance = this;
            base.Initialize(config);

            if (_configuration.Enabled)
                Enable();
        }
        public override void Enable()
        {
            Framework.Update += OnFrameworkUpdate;
            PluginLog.Information("[SideCheck] Enabled.");
        }

        public override void Disable()
        {
            Framework.Update -= OnFrameworkUpdate;
            _lastQuadrant = PositionalQuadrant.Unknown;

            PluginLog.Information("[SideCheck] Disabled.");
        }

        public override void Dispose()
        {
            Disable();

            if (Instance == this)
                Instance = null;

            PluginLog.Information("[SideCheck] Disposed.");
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            try
            {
                if (!_configuration.Enabled)
                    return;

                var player = ObjectTable?.LocalPlayer;
                if (player == null)
                {
                    SetQuadrant(PositionalQuadrant.Unknown);
                    return;
                }

                var target = TargetManager?.Target;
                if (target == null)
                {
                    SetQuadrant(PositionalQuadrant.Unknown);
                    return;
                }

                var targetPosition = target.Position;
                var targetRotation = target.Rotation;
                var playerPosition = player.Position;

                var quadrant = GetPlayerQuadrantRelativeToTarget(
                    targetPosition,
                    targetRotation,
                    playerPosition
                );

                SetQuadrant(quadrant);

                if (DateTime.Now >= _nextDebugPrint)
                {
                    _nextDebugPrint = DateTime.Now.AddSeconds(1);

                    PluginLog.Information(
                        $"[SideCheck] Enabled={_configuration.Enabled}, " +
                        $"PrintOnChange={_configuration.PrintOnChange}, " +
                        $"DebugLogging={_configuration.DebugLogging}, " +
                        $"Player={player.Name.TextValue}, " +
                        $"Target={target.Name.TextValue}, " +
                        $"TargetRot={RadiansToDegrees(targetRotation):F1}, " +
                        $"Quadrant={quadrant}"
                    );

                    ChatGui.Print($"[SideCheck] {target.Name.TextValue}: {quadrant}");
                }
            }
            catch (Exception ex)
            {
                _lastQuadrant = PositionalQuadrant.Unknown;

                if (_configuration.DebugLogging && DateTime.Now >= _nextDebugPrint)
                {
                    _nextDebugPrint = DateTime.Now.AddSeconds(1);
                    PluginLog.Warning(ex, "[SideCheck] Update failed.");
                }
            }
        }

        private void SetQuadrant(PositionalQuadrant quadrant)
        {
            if (_lastQuadrant == quadrant)
                return;

            _lastQuadrant = quadrant;

            if (_configuration.PrintOnChange)
            {
                try
                {
                    ChatGui?.Print($"[SideCheck] {quadrant}");
                }
                catch (Exception ex)
                {
                    PluginLog.Warning(ex, "[SideCheck] Failed to print quadrant change.");
                }
            }
        }

        public PositionalQuadrant CurrentQuadrant => _lastQuadrant;

        public static PositionalQuadrant GetPlayerQuadrantRelativeToTarget(
            Vector3 targetPosition,
            float targetRotation,
            Vector3 playerPosition)
        {
            Vector3 toPlayer = playerPosition - targetPosition;

            if (toPlayer.LengthSquared() < 0.0001f)
                return PositionalQuadrant.Unknown;

            float angleToPlayer = MathF.Atan2(toPlayer.X, toPlayer.Z);
            float delta = NormalizeRadians(angleToPlayer - targetRotation);

            if (MathF.Abs(delta) <= MathF.PI / 4f)
                return PositionalQuadrant.Front;

            if (MathF.Abs(delta) >= 3f * MathF.PI / 4f)
                return PositionalQuadrant.Rear;

            return delta > 0
                ? PositionalQuadrant.LeftFlank
                : PositionalQuadrant.RightFlank;
        }

        private static float NormalizeRadians(float angle)
        {
            while (angle <= -MathF.PI)
                angle += MathF.Tau;

            while (angle > MathF.PI)
                angle -= MathF.Tau;

            return angle;
        }

        private static float RadiansToDegrees(float radians)
            => radians * 180f / MathF.PI;
    }

    public enum PositionalQuadrant
    {
        Unknown,
        Front,
        LeftFlank,
        RightFlank,
        Rear
    }
}
