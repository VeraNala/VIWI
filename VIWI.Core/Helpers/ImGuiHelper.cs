namespace VIWI.Helpers
{
    using System.Numerics;

    internal static class ImGuiHelper
    {
        public static readonly Vector4 RainbowColorStart = new(1, 0, 1, 1);
        public static readonly Vector4 RainbowColorEnd = new(1, 0.6f, 0, 1);
        public static readonly Vector4 VersionColor = new(0, 1, 1, 1);
        public static readonly Vector4 LinkColor = new(0, 200, 238, 1);

        public static readonly Vector4 White = new(1, 1, 1, 1);

        public static readonly Vector4 RoleTankColor = new(0, 0.8f, 1, 1);
        public static readonly Vector4 RoleHealerColor = new(0, 1, 0, 1);
        public static readonly Vector4 RoleDPSColor = new(1, 0, 0, 1);
        public static readonly Vector4 RoleAllRounderColor = new(1, 1, 0.5f, 1);

        public static readonly Vector4 StateGoodColor = new(0, 1, 0, 1);
        public static readonly Vector4 StateBadColor = new(1, 0, 0, 1);
    }
}