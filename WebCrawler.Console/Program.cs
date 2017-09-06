using System.Threading.Tasks;
using WebCrawler;

class Program
{
    async static Task Main(string[] args)
    {
        if (args.Length == 0)
            return;

        using (Crawler crawler = new Crawler())
        {
            var result = await crawler.RunAsync(args[0]);
        }
    }
}