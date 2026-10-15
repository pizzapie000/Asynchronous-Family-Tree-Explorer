using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;

namespace Assignment14;

public static class Solve
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(180)
    };
    public static string TopApiUrl { get; } =
        (Environment.GetEnvironmentVariable("FAMILY_TREE_API_URL") ?? "http://127.0.0.1:8123").TrimEnd('/');

    // Bound simultaneous HTTP requests across both traversal strategies.
    private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(30);

    // This function retrieves JSON from the server
    public static async Task<JObject?> GetDataFromServerAsync(string url)
    {
        try
        {
            var jsonString = await HttpClient.GetStringAsync(url);
            return JObject.Parse(jsonString);
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Error fetching data from {url}: {e.Message}");
            return null;
        }
    }

    // This function takes in a person ID and retrieves a Person object
    private static async Task<Person?> FetchPersonAsync(long personId)
    {
        await Semaphore.WaitAsync();
        try
        {
            var personJson = await Solve.GetDataFromServerAsync($"{Solve.TopApiUrl}/person/{personId}");
            return personJson != null ? Person.FromJson(personJson.ToString()) : null;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    // This function takes in a family ID and gets out a Family object
    private static async Task<Family?> FetchFamilyAsync(long familyId)
    {
        await Semaphore.WaitAsync();
        try
        {
            var familyJson = await Solve.GetDataFromServerAsync($"{Solve.TopApiUrl}/family/{familyId}");
            return familyJson != null ? Family.FromJson(familyJson.ToString()) : null;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    // =======================================================================================================
    public static Task<bool> DepthFS(long familyId, Tree tree) =>
        DepthFirstCore(familyId, tree, new ConcurrentDictionary<long, byte>());

    private static async Task<bool> DepthFirstCore(long familyId, Tree tree,
        ConcurrentDictionary<long, byte> visitedFamilies)
    {
        if (familyId == 0) return false;
        if (!visitedFamilies.TryAdd(familyId, 0)) return true;

        var family = await FetchFamilyAsync(familyId);
        if (family == null) return false;

        tree.AddFamily(family);

        // get the husband wife and children concurrently
        var personTasks = new List<Task<Person?>>();
        if (family.HusbandId != 0 && !tree.PersonExists(family.HusbandId))
            personTasks.Add(FetchPersonAsync(family.HusbandId));
        if (family.WifeId != 0 && !tree.PersonExists(family.WifeId))
            personTasks.Add(FetchPersonAsync(family.WifeId));
        foreach (var childId in family.Children)
        {
            if (childId != 0 && !tree.PersonExists(childId))
                personTasks.Add(FetchPersonAsync(childId));
        }

        var people = await Task.WhenAll(personTasks);

        foreach (var person in people)
        {
            if (person != null)
                tree.AddPerson(person);
        }

        // recursively process the parents of husband and wife
        var parentTasks = new List<Task>();
        var husband = tree.GetPerson(family.HusbandId);
        if (husband != null && husband.ParentId != 0)
            parentTasks.Add(DepthFirstCore(husband.ParentId, tree, visitedFamilies));
        var wife = tree.GetPerson(family.WifeId);
        if (wife != null && wife.ParentId != 0)
            parentTasks.Add(DepthFirstCore(wife.ParentId, tree, visitedFamilies));

        await Task.WhenAll(parentTasks);

        return true;
    }

    // =======================================================================================================
    public static async Task<bool> BreathFS(long startFamilyId, Tree tree)
{
    if (startFamilyId == 0) return false;

    var familyQueue = new Queue<long>();
    var visitedFamilies = new HashSet<long>();

    familyQueue.Enqueue(startFamilyId);

    while (familyQueue.Count > 0)
    {
        var batch = new List<long>();
        while (familyQueue.Count > 0)
        {
            var famId = familyQueue.Dequeue();
            if (!visitedFamilies.Contains(famId))
            {
                batch.Add(famId);
                visitedFamilies.Add(famId);
            }
        }

        // Get all families in this group in parallel
        var familyTasks = batch.Select(famId => FetchFamilyAsync(famId)).ToList();
        var families = await Task.WhenAll(familyTasks);

        var personTasks = new List<Task<Person?>>();
        foreach (var family in families)
        {
            if (family == null) continue;
            tree.AddFamily(family);

            // Add husband, wife, children tasks
            if (family.HusbandId != 0 && !tree.PersonExists(family.HusbandId))
                personTasks.Add(FetchPersonAsync(family.HusbandId));
            if (family.WifeId != 0 && !tree.PersonExists(family.WifeId))
                personTasks.Add(FetchPersonAsync(family.WifeId));
            foreach (var childId in family.Children)
            {
                if (childId != 0 && !tree.PersonExists(childId))
                    personTasks.Add(FetchPersonAsync(childId));
            }
        }

        var people = await Task.WhenAll(personTasks);
        foreach (var person in people)
        {
            if (person != null)
                tree.AddPerson(person);
        }

        // queues the parent families for the next level
        foreach (var family in families)
        {
            if (family == null) continue;
            var husband = tree.GetPerson(family.HusbandId);
            if (husband != null && husband.ParentId != 0 && !visitedFamilies.Contains(husband.ParentId))
                familyQueue.Enqueue(husband.ParentId);
            var wife = tree.GetPerson(family.WifeId);
            if (wife != null && wife.ParentId != 0 && !visitedFamilies.Contains(wife.ParentId))
                familyQueue.Enqueue(wife.ParentId);
        }
    }

    return true;
}
}
