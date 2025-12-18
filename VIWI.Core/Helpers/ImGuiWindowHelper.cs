using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace VIWI.Helpers;

/// <summary>
/// Helpers for positioning ImGui windows near UI elements/addons in a multi-monitor safe way.
/// </summary>
internal static class ImGuiWindowHelper
{
    /// <summary>
    /// Pick the best viewport for a point. If the point is inside a viewport, returns that viewport,
    /// otherwise returns the closest viewport by distance-to-rectangle.
    /// </summary>
    public static ImGuiViewportPtr FindBestViewport(Vector2 point)
    {
        var io = ImGui.GetPlatformIO();
        ImGuiViewportPtr best = ImGui.GetMainViewport();
        float bestDist = float.MaxValue;

        for (int i = 0; i < io.Viewports.Size; i++)
        {
            var vp = io.Viewports[i];
            var min = vp.WorkPos;
            var max = vp.WorkPos + vp.WorkSize;

            bool inside =
                point.X >= min.X && point.X <= max.X &&
                point.Y >= min.Y && point.Y <= max.Y;

            if (inside)
                return vp;

            float dx = point.X < min.X ? min.X - point.X : (point.X > max.X ? point.X - max.X : 0);
            float dy = point.Y < min.Y ? min.Y - point.Y : (point.Y > max.Y ? point.Y - max.Y : 0);
            float dist = dx * dx + dy * dy;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = vp;
            }
        }

        return best;
    }

    /// <summary>
    /// Clamp a position to the viewport work area with a margin.
    /// </summary>
    public static Vector2 ClampToViewport(Vector2 pos, ImGuiViewportPtr vp, float margin, Vector2 windowSize)
    {
        var minX = vp.Pos.X + margin;
        var minY = vp.Pos.Y + margin;

        var maxX = vp.Pos.X + vp.Size.X - margin - windowSize.X;
        var maxY = vp.Pos.Y + vp.Size.Y - margin - windowSize.Y;

        if (maxX < minX) maxX = minX;
        if (maxY < minY) maxY = minY;

        return new Vector2(
            Math.Clamp(pos.X, minX, maxX),
            Math.Clamp(pos.Y, minY, maxY)
        );
    }

    /// <summary>
    /// Returns true if a point is inside the viewport work area.
    /// </summary>
    public static bool IsPointInsideViewport(Vector2 point, ImGuiViewportPtr vp)
    {
        var min = vp.WorkPos;
        var max = vp.WorkPos + vp.WorkSize;

        return point.X >= min.X && point.X <= max.X &&
               point.Y >= min.Y && point.Y <= max.Y;
    }
}
