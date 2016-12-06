using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Dom.Css;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;
using AngleSharp.Parser.Css;

namespace WebCrawler
{
    public class Crawler : IDisposable
    {
        private readonly CrawlerOptions _options;
        private HttpClient _client;
        private readonly BlockingCollection<DiscoveredUrl> _documentToProcess = new BlockingCollection<DiscoveredUrl>();
        private int _processingThreadCount;

        public event EventHandler<DocumentEventArgs> DocumentParsed;
        public event EventHandler<DocumentRefAddedEventArgs> DocumentRefAdded;
        public event EventHandler<DocumentEventArgs> DocumentUpdated;

        static Crawler()
        {
            Encoding.RegisterProvider(WebEncodingProvider.Instance);
        }

        public Crawler() : this(null)
        {
        }

        public Crawler(CrawlerOptions options)
        {
            if (options == null)
            {
                options = new CrawlerOptions();
            }

            _options = options;
        }

        private void EnsureHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                ClientCertificateOptions = ClientCertificateOption.Manual,
                CheckCertificateRevocationList = true,
            };

            _client = new HttpClient(handler, true);
            _client.DefaultRequestHeaders.Add("User-Agent", _options.UserAgent);
        }

        public async Task<CrawlResult> RunAsync(string address, CancellationToken ct = default(CancellationToken))
        {
            EnsureHttpClient();

            var result = new CrawlResult();
            result.Address = address;

            _documentToProcess.Add(new DiscoveredUrl { Url = address }, ct);

            var maxConcurrency = _options.MaxConcurrency;
            var tasks = new Task<Task>[maxConcurrency];
            for (var i = 0; i < maxConcurrency; i++)
            {
                tasks[i] = Task.Run<Task>(() => ProcessCollectionAsync(result, ct), ct);
            }

            await Task.WhenAll(tasks.Select(task => task.Unwrap()));
            return result;
        }

        private async Task ProcessCollectionAsync(CrawlResult result, CancellationToken ct)
        {
            foreach (var item in _documentToProcess.GetConsumingEnumerable(ct))
            {
                Interlocked.Increment(ref _processingThreadCount);
                try
                {
                    await ProcessItemAsync(result, item, ct).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _processingThreadCount);
                    if (_processingThreadCount == 0 && _documentToProcess.Count == 0)
                    {
                        _documentToProcess.CompleteAdding();
                    }
                }
            }
        }

        private async Task ProcessItemAsync(CrawlResult result, DiscoveredUrl toBeProcessed, CancellationToken ct)
        {
            // Test the domain, same domain as address or external by 1 level 
            if (!toBeProcessed.IsRedirect && !Utilities.IsSameHost(result.Address, toBeProcessed.Url))
            {
                if (toBeProcessed.Document == null || !Utilities.IsSameHost(result.Address, toBeProcessed.Document.Url))
                    return;
            }

            // Already processed
            Document existingDocument;
            lock (result.Documents)
            {
                existingDocument = result.Documents.FirstOrDefault(d => d.Url == toBeProcessed.Url);
            }

            if (existingDocument != null)
            {
                DocumentRef documentRef = new DocumentRef();
                documentRef.SourceDocument = toBeProcessed.Document;
                documentRef.TargetDocument = existingDocument;
                documentRef.Excerpt = toBeProcessed.Excerpt;
                lock (existingDocument.ReferencedBy)
                {
                    existingDocument.ReferencedBy.Add(documentRef);
                }

                OnDocumentRefAdded(documentRef);
                OnDocumentUpdated(existingDocument);
                return;
            }

            var doc = await GetAsync(toBeProcessed.Url, ct).ConfigureAwait(false);
            if (toBeProcessed.Document != null)
            {
                lock (doc.ReferencedBy)
                {
                    doc.ReferencedBy.Add(new DocumentRef { TargetDocument = doc, SourceDocument = toBeProcessed.Document, Excerpt = toBeProcessed.Excerpt });
                }
            }

            lock (result.Documents)
            {
                result.Documents.Add(doc);
            }

            OnDocumentParsed(doc);
        }

        private async Task<Document> GetAsync(string address, CancellationToken ct = default(CancellationToken))
        {
            var doc = new Document();
            doc.CrawledOn = DateTime.UtcNow;
            doc.Url = address;
            try
            {
                using (var response = await _client.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                {
                    doc.StatusCode = response.StatusCode;
                    doc.ReasonPhrase = response.ReasonPhrase;
                    doc.RequestHeaders = Combine(CloneHeaders(response.RequestMessage.Headers), CloneHeaders(response.RequestMessage.Content?.Headers));
                    doc.ResponseHeaders = Combine(CloneHeaders(response.Headers), CloneHeaders(response.Content?.Headers));

                    if (Utilities.IsRedirect(response.StatusCode))
                    {
                        if (response.Headers.TryGetValues("Location", out var locationHeader))
                        {
                            var location = locationHeader.FirstOrDefault();
                            doc.RedirectUrl = new Url(new Url(address), location).Href;
                            EnqueueRedirect(doc, doc.RedirectUrl, ct);
                        }
                    }
                    else
                    {
                        if (response.Content != null)
                        {
                            var contentType = response.Content?.Headers.ContentType?.MediaType;
                            if (contentType == null || Utilities.IsHtmlMimeType(contentType))
                            {
                                await HandleHtmlAsync(doc, response, ct).ConfigureAwait(false);
                            }
                            else if (Utilities.IsCssMimeType(contentType))
                            {
                                await HandleCssAsync(doc, response, ct).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                doc.ErrorMessage = GetErrorMessage(ex);
                doc.FullErrorMessage = ex.ToString();
            }

            return doc;
        }

        public string GetErrorMessage(Exception ex)
        {
            string message = ex.Message;
            string s = ex.GetType().FullName + ": " + message;

            if (ex.InnerException != null)
            {
                s += Environment.NewLine + " ---> " + GetErrorMessage(ex.InnerException);
            }

            return s;
        }

        private async Task HandleHtmlAsync(Document document, HttpResponseMessage response, CancellationToken ct)
        {
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var config = Configuration.Default
                .WithDefaultLoader()
                .WithLocaleBasedEncoding();

            var browsingContext = BrowsingContext.New(config);
            var htmlDocument = await browsingContext.OpenAsync(action => action.Address(document.Url).Content(content).Headers(response.Headers).Status(response.StatusCode), ct).ConfigureAwait(false);

            document.Title = htmlDocument.Title;
            GatherUrls(document, htmlDocument, ct);
        }

        private void GatherUrls(Document document, IDocument htmlDocument, CancellationToken ct)
        {
            foreach (var node in htmlDocument.All)
            {
                if (node is IHtmlAnchorElement anchorElement)
                {
                    Enqueue(document, anchorElement.Href, node, ct);
                }
                else if (node is IHtmlScriptElement scriptElement)
                {
                    if (!string.IsNullOrEmpty(scriptElement.Source))
                    {
                        if (string.IsNullOrEmpty(scriptElement.Type) || Utilities.IsJavaScriptMimeType(scriptElement.Type))
                        {
                            var href = new Url(scriptElement.BaseUrl, scriptElement.Source).Href;
                            Enqueue(document, href, node, ct);
                        }
                    }
                }
                else if (node is IHtmlLinkElement linkElement)
                {
                    Enqueue(document, linkElement.Href, node, ct);
                }
                else if (node is IHtmlImageElement imageElement)
                {
                    Enqueue(document, imageElement.Source, node, ct);
                    foreach (var url in Utilities.ParseScrSet(imageElement.SourceSet))
                    {
                        Enqueue(document, url, node, ct);
                    }
                }
                else if (node is IHtmlSourceElement sourceElement)
                {
                    Enqueue(document, sourceElement.Source, node, ct);
                    foreach (var url in Utilities.ParseScrSet(sourceElement.SourceSet))
                    {
                        Enqueue(document, url, node, ct);
                    }
                }
                else if (node is IHtmlTrackElement trackElement)
                {
                    Enqueue(document, trackElement.Source, node, ct);
                }
                else if (node is IHtmlObjectElement objectElement)
                {
                    Enqueue(document, objectElement.Source, node, ct);
                }
                else if (node is IHtmlAudioElement audioElement)
                {
                    Enqueue(document, audioElement.Source, node, ct);
                }
                else if (node is IHtmlVideoElement videoElement)
                {
                    Enqueue(document, videoElement.Source, node, ct);
                    Enqueue(document, videoElement.Poster, node, ct);
                }
                else if (node is IHtmlInlineFrameElement frameElement)
                {
                    Enqueue(document, frameElement.Source, node, ct);
                }
                else if (node is IHtmlAreaElement areaElement)
                {
                    Enqueue(document, areaElement.Href, node, ct);
                }
                else if (node is IHtmlStyleElement styleElement)
                {
                    HandleCss(document, styleElement.InnerHtml, ct);
                }

                var style = node.GetAttribute("style");
                if (!string.IsNullOrWhiteSpace(style))
                {
                    HandleCss(document, "dummy{" + style + "}", ct);
                }
            }
        }

        private async Task HandleCssAsync(Document document, HttpResponseMessage response, CancellationToken ct)
        {
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var cssParser = new CssParser();
            var stylesheet = cssParser.ParseStylesheet(content);
            HandleCss(document, stylesheet, ct);
        }

        private void HandleCss(Document document, string css, CancellationToken ct)
        {
            var cssParser = new CssParser();
            var stylesheet = cssParser.ParseStylesheet(css);
            HandleCss(document, stylesheet, ct);
        }

        private void HandleCss(Document document, ICssNode node, CancellationToken ct)
        {
            GatherUrls(document, node, ct);

            foreach (var child in node.Children)
            {
                HandleCss(document, child, ct);
            }
        }

        private void GatherUrls(Document document, ICssNode node, CancellationToken ct)
        {
            if (node is ICssProperty propertyRule)
            {
                string value = propertyRule.Value;
                if (value != null)
                {
                    var parts = value.Split(',');
                    foreach (var part in parts)
                    {
                        var url = Utilities.ParseCssUrl(part);
                        if (url != null)
                        {
                            var finalUrl = new Url(new Url(document.Url), url);
                            Enqueue(document, finalUrl.Href, propertyRule, ct);
                        }
                    }
                }
            }
        }

        private void Enqueue(Document document, string url, string excerpt, CancellationToken ct)
        {
            if (!MustProcessUrl(url))
                return;

            // Remove fragment
            var parsedUrl = new Url(url);
            parsedUrl.Fragment = null;

            var discoveredUrl = new DiscoveredUrl();
            discoveredUrl.Url = parsedUrl.Href;
            discoveredUrl.Document = document;
            discoveredUrl.Excerpt = excerpt;

            _documentToProcess.Add(discoveredUrl, ct);
        }

        private void Enqueue(Document document, string url, IElement node, CancellationToken ct)
        {
            Enqueue(document, url, GetExcerpt(node), ct);
        }

        private void Enqueue(Document document, string url, ICssNode node, CancellationToken ct)
        {
            Enqueue(document, url, GetExcerpt(node), ct);
        }

        private void EnqueueRedirect(Document document, string url, CancellationToken ct)
        {
            if (!MustProcessUrl(url))
                return;

            // Remove fragment
            var parsedUrl = new Url(url);
            parsedUrl.Fragment = null;

            var discoveredUrl = new DiscoveredUrl
            {
                Url = parsedUrl.Href,
                Document = document,
                IsRedirect = true
            };

            _documentToProcess.Add(discoveredUrl, ct);
        }

        private bool MustProcessUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetExcerpt(IElement node)
        {
            if (node == null)
                return null;

            return node.OuterHtml;
        }

        private static string GetExcerpt(ICssNode node)
        {
            if (node == null)
                return null;

            return node.ToCss();
        }

        protected virtual void OnDocumentParsed(Document document)
        {
            DocumentParsed?.Invoke(this, new DocumentEventArgs(document));
        }

        protected virtual void OnDocumentUpdated(Document document)
        {
            DocumentUpdated?.Invoke(this, new DocumentEventArgs(document));
        }

        protected virtual void OnDocumentRefAdded(DocumentRef documentRef)
        {
            DocumentRefAdded?.Invoke(this, new DocumentRefAddedEventArgs(documentRef));
        }

        private static IDictionary<string, string> CloneHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> dict)
        {
            var clone = new Dictionary<string, string>();
            if (dict != null)
            {
                foreach (var kvp in dict)
                {
                    clone.Add(kvp.Key, string.Join(", ", kvp.Value));
                }
            }

            return clone;
        }

        private static IDictionary<string, string> Combine(params IDictionary<string, string>[] dicts)
        {
            IDictionary<string, string> dict = new Dictionary<string, string>();
            foreach (var dictionary in dicts)
            {
                foreach (var kvp in dictionary)
                {
                    dict.Add(kvp.Key, kvp.Value);
                }
            }

            return dict;
        }

        public void Dispose()
        {
            _client?.Dispose();
            _client = null;
        }
    }
}