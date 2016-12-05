namespace WebCrawler
{
    public class CrawlerOptions
    {
        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/54.0.2840.99 Safari/537.36";
        public int MaxConcurrency { get; set; } = 8;
    }
}