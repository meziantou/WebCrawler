using System.Collections.Generic;

namespace WebCrawler
{
    public class CrawlResult
    {
        public string Address { get; set; }
        public IList<Document> Documents { get; } = new List<Document>();
    }
}
