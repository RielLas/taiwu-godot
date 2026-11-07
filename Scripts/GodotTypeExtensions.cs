using Godot;
using TaiwuGu.Core;

namespace Taiwu_Godot.Scripts
{
    public static class GodotTypeExtensions
    {
        public static Vector2 ToGodot(this WorldVec2 v) => new Vector2(v.X, v.Y);
        public static Vector2I ToGodot(this GridCoord c) => new Vector2I(c.X, c.Y);

        public static WorldVec2 ToWorldVec2(this Vector2 v) => new WorldVec2(v.X, v.Y);
        public static GridCoord ToGridCoord(this Vector2I c) => new GridCoord(c.X, c.Y);
    }
}
