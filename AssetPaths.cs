using System.IO;

namespace advent;

internal static class AssetPaths
{
    public static string Cat(string fileName) => Path.Combine("assets", "cat", fileName);
    public static string Error(string fileName) => Path.Combine("assets", "error", fileName);
    public static string Santa(string fileName) => Path.Combine("assets", "santa", fileName);
    public static string SpaceInvaders(string fileName) => Path.Combine("assets", "space-invaders", fileName);
}
