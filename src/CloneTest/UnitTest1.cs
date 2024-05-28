using System.Reflection;
using Clone;
using CloneGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CloneTest;

public class ClonerTests
{
    private Compilation inputCompilation = CreateCompilation(@"
using Clone;
using System.Collections.Generic;

namespace Test;

[Cloneable]
public partial class Child
{
    public int Id = 0;
}

[Cloneable]
partial class Base {
    int BaseId = 0;
}

enum EA {
    A = 1;
}

public partial class A {
    public int partialId = 0;
}

[Cloneable]
public partial class A : Base
{
    EA a;
    // private int i2 => i;
    // private int i3 { get { return 1; } }
    // private int i4 { get; }
    private int i { get; set; }
    public Child[][] AArr2;
    public Child[] AArr;
    public int[][] intArr2;
    public int[] intArr;
    public int Id = 0;

    [CloneIgnore]
    public int Id2 = 0;

    public List<int> Ints = new();

    public List<List<int>> ListOfList = new();
    public List<List<string>> ListOfListString = new();
    public Dictionary<string, string> Meta;
    public Dictionary<string, Dictionary<string, string>> MetaMeta;
    public Dictionary<int, int> IdMap;
    public Child Child;
}
");

    private static Compilation CreateCompilation(string source)
        => CSharpCompilation.Create("compilation",
            new[] { CSharpSyntaxTree.ParseText(source) },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CloneableAttribute).GetTypeInfo().Assembly.Location)
            },
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

    [SetUp]
    public void Setup()
    {
    }


    [Test]
    public void TestIncremental()
    {
        var generator = new CloneIncrementalGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation,
            out var diagnostics);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        Assert.That(runResult.Results[0].Exception, Is.EqualTo(null));

        Console.WriteLine(runResult.Results[0].GeneratedSources.Last().SourceText);
        // We can now assert things about the resulting compilation:
        // Assert.IsTrue(diagnostics.IsEmpty); // there were no diagnostics created by the generators
        // Assert.That(outputCompilation.SyntaxTrees.Count(),
        //     Is.EqualTo(2)); // we have two syntax trees, the original 'user' provided one, and the one added by the generator
        // Assert.IsTrue(outputCompilation.GetDiagnostics()
        //     .IsEmpty); // verify the compilation with the added source has no diagnostics
        //
        // // Or we can look at the results directly:
        // GeneratorDriverRunResult runResult = driver.GetRunResult();
        //
        // // The runResult contains the combined results of all generators passed to the driver
        // Assert.IsTrue(runResult.GeneratedTrees.Length == 1);
        // Assert.IsTrue(runResult.Diagnostics.IsEmpty);
        //
        // // Or you can access the individual results on a by-generator basis
        // GeneratorRunResult generatorResult = runResult.Results[0];
        // Assert.IsTrue(generatorResult.Generator == generator);
        // Assert.IsTrue(generatorResult.Diagnostics.IsEmpty);
        // Assert.IsTrue(generatorResult.GeneratedSources.Length == 1);
        // Assert.IsTrue(generatorResult.Exception is null);
    }
}
