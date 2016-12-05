using System;
using WebCrawler;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
            return;

        using (Crawler crawler = new Crawler())
        {
            var result = crawler.RunAsync(args[0]).Result;
        }
    }
}