using ObeliskLauncher.ARK;

namespace ObeliskLauncher.Data;

static class ActiveGameManager
{
    static IGameContext? s_current;

    public static bool IsConfigured => s_current is not null;

    public static IGameContext Current => s_current ?? throw new InvalidOperationException("Active game is not configured.");

    public static void Configure(string gameId, string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Game root path is required.", nameof(rootPath));

        string normalizedPath = Path.GetFullPath(rootPath);
        s_current = new CatalogGameContext(gameId.ToUpperInvariant(), normalizedPath);
    }

    public static void SetRootPath(string rootPath)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Active game is not configured.");

        Configure(Current.Id, rootPath);
    }
}