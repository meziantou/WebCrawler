using System.Collections.Generic;

namespace WebCrawler
{
    public class CrawlResult
    {
        public IList<Document> Documents { get; } = new List<Document>();
    }
}
