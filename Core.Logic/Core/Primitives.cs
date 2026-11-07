using System;

namespace TaiwuGu.Core
{
    public readonly record struct GridCoord(int X, int Y)
    {
        public int x => X;
        public int y => Y;
        public static readonly GridCoord Zero = new(0, 0);
        public static GridCoord zero => Zero;

        public static GridCoord operator +(GridCoord a, GridCoord b) => new(a.X + b.X, a.Y + b.Y);
        public static GridCoord operator -(GridCoord a, GridCoord b) => new(a.X - b.X, a.Y - b.Y);
        public static GridCoord operator *(GridCoord a, int d) => new(a.X * d, a.Y * d);
        public override string ToString() => $"({X}, {Y})";
    }

    public readonly record struct WorldVec2(float X, float Y)
    {
        public WorldVec2(float x, float y, float z) : this(x, y) { }

        public float x => X;
        public float y => Y;
        public float z => 0f;
        public static readonly WorldVec2 Zero = new(0f, 0f);
        public static WorldVec2 zero => Zero;

        public static WorldVec2 operator +(WorldVec2 a, WorldVec2 b) => new(a.X + b.X, a.Y + b.Y);
        public static WorldVec2 operator -(WorldVec2 a, WorldVec2 b) => new(a.X - b.X, a.Y - b.Y);
        public static WorldVec2 operator *(WorldVec2 a, float d) => new(a.X * d, a.Y * d);
        public static WorldVec2 operator /(WorldVec2 a, float d) => new(a.X / d, a.Y / d);

        public float Magnitude => MathF.Sqrt(X * X + Y * Y);
        public float SqrMagnitude => X * X + Y * Y;
        public static float Distance(WorldVec2 a, WorldVec2 b) => (a - b).Magnitude;
        public override string ToString() => $"({X:F2}, {Y:F2})";
    }

    public readonly record struct Color(float r, float g, float b, float a = 1f)
    {
        public static readonly Color White = new(1f, 1f, 1f, 1f);
        public static readonly Color Black = new(0f, 0f, 0f, 1f);
        public static readonly Color Red = new(1f, 0f, 0f, 1f);
        public static readonly Color Green = new(0f, 1f, 0f, 1f);
        public static readonly Color Blue = new(0f, 0f, 1f, 1f);
    }

    public static class Mathf
    {
        public const float PI = MathF.PI;
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
        public static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
        public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
        public static float Max(float a, float b) => MathF.Max(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Min(float a, float b) => MathF.Min(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Abs(float f) => MathF.Abs(f);
        public static int Abs(int v) => Math.Abs(v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Round(float f) => MathF.Round(f);
        public static int RoundToInt(float f) => (int)MathF.Round(f);
        public static int FloorToInt(float f) => (int)MathF.Floor(f);
        public static int CeilToInt(float f) => (int)MathF.Ceiling(f);
        public static float Pow(float f, float p) => MathF.Pow(f, p);
        public static float Sqrt(float f) => MathF.Sqrt(f);
    }

    public static class Debug
    {
        public static Action<string> LogHandler { get; set; } = msg => Console.WriteLine(msg);
        public static Action<string> LogWarningHandler { get; set; } = msg => Console.WriteLine("[WARN] " + msg);
        public static Action<string> LogErrorHandler { get; set; } = msg => Console.Error.WriteLine("[ERROR] " + msg);

        public static void Log(string message) => LogHandler?.Invoke(message);
        public static void LogWarning(string message) => LogWarningHandler?.Invoke(message);
        public static void LogError(string message) => LogErrorHandler?.Invoke(message);
    }
}
