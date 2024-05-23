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

[Cloneable]
public partial class A : If
{
    private int i = 0;
    private List<int> arr = new();
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

        Console.WriteLine(runResult.Results[0].GeneratedSources.First().SourceText);
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