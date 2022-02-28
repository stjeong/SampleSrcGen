using System;

namespace MyNamespace
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Book item = new Book("jst", 0);
            Console.WriteLine(item.Writer);

            Publisher publisher = new Publisher("kst");
            Console.WriteLine($"{publisher.Writer}, {publisher}");
        }
    }

    [AutoProp]
    public partial class Book
    {
        string writer = "";
        decimal isbn = 0M;
    }

    [AutoProp]
    public partial class Publisher
    {
        string writer = "";
        // int age;
    }
}