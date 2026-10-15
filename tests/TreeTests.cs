using Assignment14;
using NUnit.Framework;

namespace Explorer.Tests;

public class TreeTests
{
    [Test]
    public void RepeatedPersonIdsAreStoredOnce()
    {
        var tree = new Tree(1);
        tree.AddPerson(new Person { Id = 42, Name = "First" });
        tree.AddPerson(new Person { Id = 42, Name = "Duplicate" });
        Assert.That(tree.PersonCount, Is.EqualTo(1));
        Assert.That(tree.GetPerson(42)?.Name, Is.EqualTo("First"));
    }

    [Test]
    public void ConcurrentPersonInsertionsPreserveUniqueRecords()
    {
        var tree = new Tree(1);
        Parallel.For(0, 10_000, i => tree.AddPerson(new Person { Id = i % 100 }));
        Assert.That(tree.PersonCount, Is.EqualTo(100));
        for (var id = 0; id < 100; id++) Assert.That(tree.PersonExists(id), Is.True);
    }

    [Test]
    public void ConcurrentFamilyInsertionsPreserveUniqueRecords()
    {
        var tree = new Tree(1);
        Parallel.For(0, 10_000, i => tree.AddFamily(new Family { Id = i % 50 }));
        Assert.That(tree.FamilyCount, Is.EqualTo(50));
        Assert.That(tree.GetFamily(49)?.Id, Is.EqualTo(49));
    }

    [Test]
    public void MissingRecordsReturnNull()
    {
        var tree = new Tree(1);
        Assert.That(tree.GetPerson(99), Is.Null);
        Assert.That(tree.GetFamily(99), Is.Null);
    }

    [Test]
    public void PersonDeserializerAcceptsNullRelationshipIds()
    {
        var person = Person.FromJson("{\"id\":7,\"name\":\"Example\",\"parent_id\":null,\"family_id\":null}");
        Assert.That(person?.Id, Is.EqualTo(7));
        Assert.That(person?.ParentId, Is.EqualTo(0));
        Assert.That(person?.FamilyId, Is.EqualTo(0));
    }

    [Test]
    public async Task ZeroFamilyIdsDoNotStartNetworkTraversal()
    {
        var tree = new Tree(0);
        Assert.That(await Solve.DepthFS(0, tree), Is.False);
        Assert.That(await Solve.BreathFS(0, tree), Is.False);
        Assert.That(tree.FamilyCount, Is.EqualTo(0));
    }
}
