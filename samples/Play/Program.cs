using Clone;
using Mapster;
using Newtonsoft.Json;

var source = new A()
{
    Ints = [1, 2, 3, 4],
    ListOfList = [[1, 1], [2, 2], [3, 3]],
    ListOfListString = [["aa", "bb"], ["cc"]],
    Meta = new() { { "a", "b" } },
    IdMap = new() { { 1, 2 } },
    MetaMeta = new() { { "meta", new() { { "meta", "value" } } } },
    Child = new Child()
    {
        Id = Random.Shared.Next()
    },
    Children = new()
    {
        { 1, new Child() { Id = Random.Shared.Next() } },
        { 2, new FChild() { Id = Random.Shared.Next(), NameF = "F" } },
        { 3, new MChild() { Id = Random.Shared.Next(), NameM = "M" } },
    }
};

void Measure(Action action)
{
    DateTimeOffset start = DateTimeOffset.Now;
    long i = GC.GetAllocatedBytesForCurrentThread();

    action();
    Console.WriteLine($"total: {DateTimeOffset.Now - start}");
    Console.WriteLine($"bytes: {GC.GetAllocatedBytesForCurrentThread() - i}");
}

// 178620840

Measure(() =>
{
    for (int i = 0; i < 100000; i++)
    {
        var clone = Cloner.Make(source);
    }
});


Measure(() =>
{
    for (int i = 0; i < 100000; i++)
    {
        var clone = source.Adapt<A>();
    }
});

var item = new MChild() { Id = Random.Shared.Next(), NameM = "M" };

Console.WriteLine(JsonConvert.SerializeObject(Cloner.Make((Child)item)));

var clone = Cloner.Make(source);

string sourceStr = JsonConvert.SerializeObject(source);
string cloneStr = JsonConvert.SerializeObject(clone);
Console.WriteLine(sourceStr);
Console.WriteLine(cloneStr);

Console.WriteLine(sourceStr == cloneStr);

public partial class A
{
    public int Anthor = 0;
}

interface If
{
}

[Cloneable]
public partial class A : If
{
    public int Id = 0;

    public List<int> Ints;

    public List<List<int>> ListOfList;
    public List<List<string>> ListOfListString;
    public Dictionary<string, string> Meta;
    public Dictionary<string, Dictionary<string, string>> MetaMeta;

    public Dictionary<int, int> IdMap;
    public Child Child;
    public Dictionary<int, Child> Children;
}

[Cloneable]
public partial class Child
{
    public int Id = 0;
    public int Id2 = 0;
}


[Cloneable]
public partial class FChild : Child
{
    public string NameF;
}

[Cloneable]
public partial class MChild : Child
{
    public string NameM;
}
