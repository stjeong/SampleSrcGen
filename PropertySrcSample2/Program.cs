using System;

internal class Program
{
    static void Main(string[] args)
    {
        Book item = new Book("jst", 0);
        Console.WriteLine(item.Writer);
    }
}

[AutoProp]
public partial class Book
{
    string writer = "";
    decimal isbn = 0M;
}


