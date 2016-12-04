using System;
using System.Collections.Generic;

namespace WebCrawler
{
    public class Document
    {
        public Document()
        {
            Id = Guid.NewGuid();
        }

        public Guid Id { get; set; }
        public string OriginalUrl { get; set; }
        public string Url { get; set; }
        public IList<DocumentRef> ReferencedBy { get; } = new List<DocumentRef>();
        public DateTime CrawledOn { get; set; }
        public System.Net.HttpStatusCode StatusCode { get; set; }
        public IDictionary<string, string> Headers { get; set; }
        public string Title { get; internal set; }
    }
}