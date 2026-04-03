using UnityEngine;

namespace GoodwillPreview;

internal static class Compat {
    public static Rect MiddlePartPixels(this Rect rect, float width, float height) =>
        new(rect.center.x - width / 2f, rect.center.y - height / 2f, width, height);
}
