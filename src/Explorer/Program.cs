using Assignment14;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Contains("--help"))
        {
            Console.WriteLine("Usage: dotnet run --project src/Explorer -- [--strategy dfs|bfs|both] [--generations 1..6]");
            return 0;
        }

        try
        {
            var strategy = "both";
            var generations = 3;
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "--strategy" && i + 1 < args.Length) strategy = args[++i].ToLowerInvariant();
                else if (args[i] == "--generations" && i + 1 < args.Length && int.TryParse(args[++i], out var count)) generations = count;
                else throw new ArgumentException("Unknown or incomplete option. Use --help for usage.");
            }
            if (strategy is not ("dfs" or "bfs" or "both") || generations is < 1 or > 6)
                throw new ArgumentException("Strategy must be dfs, bfs, or both; generations must be between 1 and 6.");

            Logger.Configure(logToFile: false);
            if (strategy is "dfs" or "both") await RunAsync("Depth-first", generations, Solve.DepthFS);
            if (strategy is "bfs" or "both") await RunAsync("Breadth-first", generations, Solve.BreathFS);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Unable to complete traversal: {error.Message}");
            Console.Error.WriteLine("Start the sample server first and check FAMILY_TREE_API_URL if using a custom address.");
            return 1;
        }
    }

    private static async Task RunAsync(string label, int generations, Func<long, Tree, Task<bool>> traverse)
    {
        var initialized = await Solve.GetDataFromServerAsync($"{Solve.TopApiUrl}/start/{generations}");
        if (initialized == null) throw new InvalidOperationException("The server did not initialize the sample tree.");
        var root = await Solve.GetDataFromServerAsync(Solve.TopApiUrl)
            ?? throw new InvalidOperationException("The server did not return a starting family.");
        var startId = (long?)root["start_family_id"]
            ?? throw new InvalidOperationException("The response did not contain start_family_id.");
        var tree = new Tree(startId);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        if (!await traverse(startId, tree)) throw new InvalidOperationException("The starting family could not be retrieved.");
        timer.Stop();
        var totals = await Solve.GetDataFromServerAsync($"{Solve.TopApiUrl}/end")
            ?? throw new InvalidOperationException("The server did not return verification totals.");
        if (tree.PersonCount != (int?)totals["people"] || tree.FamilyCount != (int?)totals["families"])
            throw new InvalidOperationException("Retrieved counts do not match the sample server; the traversal is incomplete.");

        Console.WriteLine($"{label}: generations={generations}, people={tree.PersonCount}, families={tree.FamilyCount}, elapsed={timer.Elapsed.TotalSeconds:F3}s");
        Console.WriteLine("VERIFIED: retrieved people and families match the sample server.");
    }
}
