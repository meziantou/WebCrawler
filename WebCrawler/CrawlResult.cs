using System.Collections.Generic;

namespace WebCrawler
{
    public class CrawlResult
    {
        public IList<string> Urls { get; set; }
        public IList<Document> Documents { get; } = new List<Document>();
    }
}
