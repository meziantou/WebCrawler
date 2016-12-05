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
        public string Url { get; set; }
        public string RedirectUrl { get; set; }
        public IList<DocumentRef> ReferencedBy { get; } = new List<DocumentRef>();
        public DateTime CrawledOn { get; set; }
        public System.Net.HttpStatusCode StatusCode { get; set; }
        public IDictionary<string, string> RequestHeaders { get; set; }
        public IDictionary<string, string> ResponseHeaders { get; set; }
        public string Title { get; internal set; }
        public string ErrorMessage { get; set; }
        public string FullErrorMessage { get; set; }
        public string ReasonPhrase { get; set; }
    }
}