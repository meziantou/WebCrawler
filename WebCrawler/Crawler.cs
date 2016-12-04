using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Dom.Events;
using AngleSharp.Dom.Html;

namespace WebCrawler
{
    public class Crawler
    {
        private ConcurrentQueue<DiscoveredUrl> _toProcess = new ConcurrentQueue<DiscoveredUrl>();
        public event EventHandler<DocumentEventArgs> DocumentParsed;
        public event EventHandler<DocumentEventArgs> DocumentUpdated;

        private bool IsSameHost(string a, string b)
        {
            return new Url(a).Host == new Url(b).Host;
        }

        public async Task<CrawlResult> RunAsync(string address, CancellationToken ct = default(CancellationToken))
        {
            var result = new CrawlResult();

            _toProcess.Enqueue(new DiscoveredUrl { Url = address });
            DiscoveredUrl toBeProcessed;
            while (_toProcess.TryDequeue(out toBeProcessed))
            {
                // Test the domain, same domain as address or external by 1 level 
                if (!IsSameHost(address, toBeProcessed.Url))
                {
                    if (toBeProcessed.Document == null || !IsSameHost(address, toBeProcessed.Document.Url))
                        continue;
                }

                // Already processed
                var existingDocument = result.Documents.FirstOrDefault(d => d.OriginalUrl == toBeProcessed.Url || d.Url == toBeProcessed.Url);
                if (existingDocument != null)
                {
                    DocumentRef documentRef = new DocumentRef();
                    documentRef.Document = toBeProcessed.Document;
                    documentRef.Excerpt = toBeProcessed.Excerpt;
                    existingDocument.ReferencedBy.Add(documentRef);
                    OnDocumentUpdated(existingDocument);
                    continue;
                }

                // Console.WriteLine(toBeProcessed.Url);
                var doc = await GetAsync(toBeProcessed.Url, ct);

                if (toBeProcessed.Document != null)
                {
                    doc.ReferencedBy.Add(new DocumentRef { Document = toBeProcessed.Document, Excerpt = toBeProcessed.Excerpt });
                }

                result.Documents.Add(doc);
                OnDocumentParsed(doc);
            }

            return result;
        }

        private async Task<Document> GetAsync(string address, CancellationToken ct = default(CancellationToken))
        {
            var doc = new Document();
            doc.CrawledOn = DateTime.UtcNow;
            doc.OriginalUrl = address;

            var config = Configuration.Default
                .WithDefaultLoader()
                .WithLocaleBasedEncoding();

            var browsingContext = BrowsingContext.New(config);
            browsingContext.Requested += (sender, e) =>
            {
                var requestEvent = e as RequestEvent;
                if (requestEvent == null)
                    return;

                doc.OriginalUrl = requestEvent.Request.Address.Href;
                doc.Url = requestEvent.Response.Address.Href;
                doc.StatusCode = requestEvent.Response.StatusCode;
                doc.Headers = Clone(requestEvent.Response.Headers);
            };

            var document = await browsingContext.OpenAsync(new Url(address), ct);
            if (doc.Url == null)
            {
                doc.Url = address;
            }

            doc.Title = document.Title;

            foreach (var node in document.All)
            {
                // TODO sourceset
                // TODO css background, url(...)
                if (node is IHtmlAnchorElement anchor)
                {
                    Enqueue(doc, anchor.Href, node);
                }
                else if (node is IHtmlScriptElement script)
                {
                    Enqueue(doc, script.Source, node);
                }
                else if (node is IHtmlLinkElement link)
                {
                    Enqueue(doc, link.Href, node);
                }
                else if (node is IHtmlImageElement image)
                {
                    Enqueue(doc, image.Source, node);
                }
                else if (node is IHtmlSourceElement source)
                {
                    Enqueue(doc, source.Source, node);
                }
            }

            return doc;
        }

        private void Enqueue(Document document, string url, IElement node)
        {
            if (!string.IsNullOrEmpty(url))
            {
                _toProcess.Enqueue(new DiscoveredUrl { Url = url, Document = document, Excerpt = GetExcerpt(node) });
            }
        }

        private string GetExcerpt(IElement node)
        {
            return node.OuterHtml;
        }

        protected virtual void OnDocumentParsed(Document document)
        {
            DocumentParsed?.Invoke(this, new DocumentEventArgs(document));
        }

        protected virtual void OnDocumentUpdated(Document document)
        {
            DocumentUpdated?.Invoke(this, new DocumentEventArgs(document));
        }

        private IDictionary<string, string> Clone(IDictionary<string, string> dict)
        {
            var clone = new Dictionary<string, string>();
            foreach (var kvp in dict)
            {
                clone.Add(kvp.Key, kvp.Value);
            }

            return clone;
        }
    }
}