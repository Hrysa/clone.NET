using Clone;

var a = new A();
var clone = a.Clone();

public partial class A
{
}

interface If
{
}

[Cloneable]
public partial class A : If
{
    private int i = 0;

    private List<int> arr = new();
    private List<int> arr2 = new();
}