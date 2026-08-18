using FusionRpg.Core.Effects;

namespace FusionRpg.Core.Lawn;

/// <summary>
/// Unity-free lawn index / GUI math. Injector <c>LawnCoords</c> is the Mouse/Board adapter.
/// </summary>
public static class LawnCoordMath
{
    public const int DefaultLastCol = 9;
    public const int DefaultLastRow = 4;

    public static int ClampIndex(int value, int lastInclusive)
    {
        if (lastInclusive < 0) lastInclusive = 0;
        if (value < 0) return 0;
        if (value > lastInclusive) return lastInclusive;
        return value;
    }

    public static float HalfCellY(float yRow0, float yRow1)
    {
        var half = Math.Abs(yRow0 - yRow1) * 0.5f;
        return half > 1e-4f ? half : 0f;
    }

    public static float GuiX(float screenPointX, float pixelRectX) =>
        screenPointX + pixelRectX;

    public static (float X, float Y) GuiPoint(
        float screenHeight,
        float screenPointX,
        float screenPointY,
        float pixelRectX,
        float pixelRectY,
        float t) =>
        (
            GuiX(screenPointX, pixelRectX),
            DamageFxFloaterRules.GuiY(screenHeight, screenPointY, pixelRectY, t)
        );
}
