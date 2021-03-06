﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Dom.Css;
using AngleSharp.Dom.Events;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;
using AngleSharp.Parser.Css;
using WebCrawler.Analysers;

namespace WebCrawler
{
    public class Crawler : IDisposable
    {
        private readonly CrawlerOptions _options;
        private HttpClient _client;
        private readonly BlockingCollection<DiscoveredUrl> _discoveredUrls = new BlockingCollection<DiscoveredUrl>();
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
            var handler = new HttpClientHandler()
            {
                AllowAutoRedirect = false,
                ClientCertificateOptions = ClientCertificateOption.Manual,
                CheckCertificateRevocationList = true
            };

            var retryHandler = new RetryDelegatingHandler(handler);


            _client = new HttpClient(retryHandler, true);
            _client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

            if (!string.IsNullOrEmpty(_options.UserAgent))
            {
                _client.DefaultRequestHeaders.Add("User-Agent", _options.UserAgent);
            }

            if (!string.IsNullOrEmpty(_options.DefaultAcceptLanguage))
            {
                _client.DefaultRequestHeaders.Add("Accept-Language", _options.DefaultAcceptLanguage);
            }
        }

        private IEnumerable<string> GetRootUrls(string value)
        {
            var urls = value.Split(',');
            foreach (var url in urls.Select(url => url.Trim()))
            {
                if (!IsHttpProtocol(url))
                {
                    yield return "http://" + url;
                }
                else
                {
                    yield return url;
                }
            }
        }

        public async Task<CrawlResult> RunAsync(string url, CancellationToken ct = default(CancellationToken))
        {
            if (url == null) throw new ArgumentNullException(nameof(url));

            EnsureHttpClient();

            var result = new CrawlResult();
            result.Urls = GetRootUrls(url).ToList();

            foreach (var item in result.Urls)
            {
                _discoveredUrls.Add(new DiscoveredUrl { Url = item }, ct);
            }

            var maxConcurrency = _options.MaxConcurrency;
            var tasks = new Task<Task>[maxConcurrency];
            for (var i = 0; i < maxConcurrency; i++)
            {
                tasks[i] = Task.Run<Task>(() => ProcessCollectionAsync(result, ct), ct);
            }

            await Task.WhenAll(tasks.Select(task => task.Unwrap())).ConfigureAwait(false);
            return result;
        }

        private async Task ProcessCollectionAsync(CrawlResult result, CancellationToken ct)
        {
            foreach (var item in _discoveredUrls.GetConsumingEnumerable(ct))
            {
                ct.ThrowIfCancellationRequested();

                Interlocked.Increment(ref _processingThreadCount);
                try
                {
                    await ProcessItemAsync(result, item, ct).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _processingThreadCount);
                    if (_processingThreadCount == 0 && _discoveredUrls.Count == 0)
                    {
                        _discoveredUrls.CompleteAdding();
                    }
                }
            }
        }

        private bool IsSameHost(CrawlResult result, string url)
        {
            foreach (var u in result.Urls)
            {
                if (Utilities.IsSameHost(u, url))
                    return true;
            }

            return false;
        }

        private bool MustProcess(CrawlResult result, DiscoveredUrl discoveredUrl)
        {
            if (discoveredUrl.SourceDocument == null) // root page
                return true;

            if (discoveredUrl.IsRedirect) // we go to the redicted page
                return true;

            var isSameHost = IsSameHost(result, discoveredUrl.Url);
            if (!isSameHost && IsSameHost(result, discoveredUrl.SourceDocument.Url)) // External link by one level
                return true;

            if (isSameHost) // same domain
                return true;

            if (_options.Includes != null)
            {
                foreach (var include in _options.Includes)
                {
                    if (include.IsMatch(discoveredUrl.Url))
                        return true;
                }
            }

            return false;
        }

        private async Task ProcessItemAsync(CrawlResult result, DiscoveredUrl discoveredUrl, CancellationToken ct)
        {
            // Test the domain, same domain as start url or external by 1 level 
            if (!MustProcess(result, discoveredUrl))
                return;

            // Already processed
            Document existingDocument;
            lock (result.Documents)
            {
                existingDocument = result.Documents.FirstOrDefault(d => discoveredUrl.IsSame(d));
            }

            if (existingDocument != null)
            {
                AddReference(discoveredUrl, existingDocument);
                return;
            }

            var doc = await GetAsync(discoveredUrl, ct).ConfigureAwait(false);
            lock (result.Documents)
            {
                existingDocument = result.Documents.FirstOrDefault(d => doc.IsSame(d)); // Another thread as processed the same URL at the same time
                if (existingDocument != null)
                {
                    AddReference(discoveredUrl, existingDocument);
                    return;
                }
            }

            if (discoveredUrl.SourceDocument != null)
            {
                lock (doc.ReferencedBy)
                {
                    doc.ReferencedBy.Add(new DocumentRef { SourceDocument = discoveredUrl.SourceDocument, TargetDocument = doc, Excerpt = discoveredUrl.Excerpt });
                }
            }

            lock (result.Documents)
            {
                result.Documents.Add(doc);
            }

            OnDocumentParsed(doc);
        }

        private void AddReference(DiscoveredUrl discoveredUrl, Document document)
        {
            var documentRef = new DocumentRef();
            documentRef.SourceDocument = discoveredUrl.SourceDocument;
            documentRef.TargetDocument = document;
            documentRef.Excerpt = discoveredUrl.Excerpt;
            lock (document.ReferencedBy)
            {
                document.ReferencedBy.Add(documentRef);
            }

            OnDocumentRefAdded(documentRef);
            OnDocumentUpdated(document);
        }

        private async Task<Document> GetAsync(DiscoveredUrl discoveredUrl, CancellationToken ct = default(CancellationToken))
        {
            var doc = new Document();
            doc.CrawledOn = DateTime.UtcNow;
            doc.Url = discoveredUrl.Url;
            doc.Language = discoveredUrl.Language;
            try
            {
                using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, doc.Url))
                {
                    requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

                    if (discoveredUrl.Language != null)
                    {
                        requestMessage.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(doc.Language));
                    }

                    using (var response = await _client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
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
                                EnqueueRedirect(doc, GetAbsoluteUrl(new Url(doc.Url), location), ct);
                            }
                        }
                        else
                        {
                            if (response.Content != null)
                            {
                                var contentType = response.Content?.Headers.ContentType?.MediaType;
                                if (contentType == null || Utilities.IsHtmlMimeType(contentType))
                                {
                                    var htmlDocument = await HandleHtmlAsync(doc, response, ct).ConfigureAwait(false);

                                    foreach (var analyser in _options.Analysers.OfType<IHtmlAnalyser>())
                                    {
                                        AddAnalyserResult(doc, analyser.Analyse(new HtmlAnalyseArgs(doc, htmlDocument)));
                                    }

                                    var htmlStyleTagAnalysers = _options.Analysers.OfType<ICssAnalyser>().Where(analyser => (analyser.Targets & CssAnalyserTargets.HtmlStyleTag) == CssAnalyserTargets.HtmlStyleTag).ToList();
                                    if (htmlStyleTagAnalysers.Any())
                                    {
                                        var elements = htmlDocument.QuerySelectorAll<IHtmlStyleElement>("style").ToList();
                                        foreach (var analyser in htmlStyleTagAnalysers)
                                        {
                                            foreach (var element in elements)
                                            {
                                                AddAnalyserResult(doc, analyser.Analyse(new CssAnalyseArgs(doc, CssAnalyserTargets.HtmlStyleTag, ParseCss(element.InnerHtml), element)));
                                            }
                                        }
                                    }

                                    var htmlStyleAttributeAnalysers = _options.Analysers.OfType<ICssAnalyser>().Where(analyser => (analyser.Targets & CssAnalyserTargets.HtmlStyleAttribute) == CssAnalyserTargets.HtmlStyleAttribute).ToList();
                                    if (htmlStyleAttributeAnalysers.Any())
                                    {
                                        var elements = htmlDocument.QuerySelectorAll("*[style]").ToList();
                                        foreach (var analyser in htmlStyleAttributeAnalysers)
                                        {
                                            foreach (var element in elements)
                                            {
                                                var style = element.GetAttribute("style");
                                                var rule = CreateCssRuleFromInlineStyle(style);
                                                AddAnalyserResult(doc, analyser.Analyse(new CssAnalyseArgs(doc, CssAnalyserTargets.HtmlStyleAttribute, ParseCss(rule), element)));
                                            }
                                        }
                                    }


                                }
                                else if (Utilities.IsCssMimeType(contentType))
                                {
                                    var stylesheet = await HandleCssAsync(doc, response, ct).ConfigureAwait(false);

                                    foreach (var analyser in _options.Analysers.OfType<ICssAnalyser>())
                                    {
                                        AddAnalyserResult(doc, analyser.Analyse(new CssAnalyseArgs(doc, CssAnalyserTargets.StyleSheet, stylesheet)));
                                    }
                                }

                                foreach (var analyser in _options.Analysers.OfType<IDocumentAnalyser>())
                                {
                                    AddAnalyserResult(doc, analyser.Analyse(new AnalyseArgs(doc)));
                                }
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

        private void AddAnalyserResult(Document document, IEnumerable<AnalyserResultItem> items)
        {
            foreach (var item in items)
            {
                document.Analysers.Add(item);
            }
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

        private async Task<IDocument> HandleHtmlAsync(Document document, HttpResponseMessage response, CancellationToken ct)
        {
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var config = Configuration.Default
                .WithDefaultLoader()
                .WithLocaleBasedEncoding();

            var browsingContext = BrowsingContext.New(config);
            browsingContext.ParseError += (sender, e) =>
            {
                var ev = e as HtmlErrorEvent;
                if (ev != null)
                {
                    var error = new HtmlError();
                    error.Column = ev.Position.Column;
                    error.Position = ev.Position.Position;
                    error.Line = ev.Position.Line;
                    error.Message = ev.Message;
                    error.Code = ev.Code;

                    var excerptStart = Math.Max(0, (error.Position - 1) - 50);
                    var excerptEnd = Math.Min(content.Length, (error.Position - 1) + 50);
                    error.Excerpt = content.Substring(excerptStart, excerptEnd - excerptStart);
                    error.ExcerptPosition = error.Position - excerptStart;

                    document.HtmlErrors.Add(error);
                }
            };

            var htmlDocument = await browsingContext.OpenAsync(action => action.Address(document.Url).Content(content).Headers(response.Headers).Status(response.StatusCode), ct).ConfigureAwait(false);

            document.Title = htmlDocument.Title;
            GatherUrls(document, htmlDocument, ct);

            return htmlDocument;
        }

        private void GatherUrls(Document document, IDocument htmlDocument, CancellationToken ct)
        {
            /* <meta http-equiv="refresh" content="0; url=http://example.com/" /> */
            if (htmlDocument.Head != null)
            {
                var metas = htmlDocument.Head.GetElementsByTagName("meta").OfType<IHtmlMetaElement>();
                foreach (var meta in metas)
                {
                    if (string.Equals("refresh", meta.HttpEquivalent, StringComparison.OrdinalIgnoreCase) && meta.Content != null)
                    {
                        var content = meta.Content;
                        var parts = content.Split(';');
                        foreach (var part in parts)
                        {
                            var value = part.Trim();

                            const string urlPrefix = "url=";
                            if (value.StartsWith(urlPrefix))
                            {
                                var url = GetAbsoluteUrl(meta.BaseUrl, value.Substring(urlPrefix.Length));
                                EnqueueRedirect(document, meta, url, ct);
                                return; // Do not parse page content    
                            }
                        }
                    }
                }
            }

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
                            var href = GetAbsoluteUrl(scriptElement.BaseUrl, scriptElement.Source);
                            Enqueue(document, href, node, ct);
                        }
                    }
                }
                else if (node is IHtmlLinkElement linkElement)
                {
                    // <link href="/" rel="alternate" hreflang="es">

                    if (string.Equals(linkElement.Relation, "alternate", StringComparison.OrdinalIgnoreCase))
                    {
                        var lang = linkElement.GetAttribute("hreflang");
                        if (!string.IsNullOrEmpty(lang))
                        {
                            Enqueue(document, linkElement.Href, lang, node, ct);
                        }
                    }

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
                    HandleCss(document, node.BaseUri, styleElement.InnerHtml, ct);
                }

                var style = node.GetAttribute("style");
                if (!string.IsNullOrWhiteSpace(style))
                {
                    HandleCss(document, node.BaseUri, CreateCssRuleFromInlineStyle(style), ct);
                }
            }
        }

        private string CreateCssRuleFromInlineStyle(string style)
        {
            return "webcrawler_generated_rule{" + style + "}";
        }

        private CssParserOptions CreateCssParserOptions()
        {
            var options = new CssParserOptions();
            options.IsIncludingUnknownDeclarations = true;
            options.IsIncludingUnknownRules = true;
            options.IsStoringTrivia = true;
            options.IsToleratingInvalidConstraints = true;
            options.IsToleratingInvalidSelectors = true;
            options.IsToleratingInvalidValues = true;
            return options;
        }

        private async Task<ICssStyleSheet> HandleCssAsync(Document document, HttpResponseMessage response, CancellationToken ct)
        {
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var cssParser = new CssParser(CreateCssParserOptions());
            var stylesheet = cssParser.ParseStylesheet(content);
            HandleCss(document, null, stylesheet, ct);
            return stylesheet;
        }

        private void HandleCss(Document document, string baseUrl, string css, CancellationToken ct)
        {
            var stylesheet = ParseCss(css);
            HandleCss(document, baseUrl, stylesheet, ct);
        }

        private ICssStyleSheet ParseCss(string css)
        {
            var cssParser = new CssParser(CreateCssParserOptions());
            var stylesheet = cssParser.ParseStylesheet(css);
            return stylesheet;
        }

        private void HandleCss(Document document, string baseUrl, ICssNode node, CancellationToken ct)
        {
            GatherUrls(document, baseUrl, node, ct);

            foreach (var child in node.Children)
            {
                HandleCss(document, baseUrl, child, ct);
            }
        }

        private void GatherUrls(Document document, string baseUrl, ICssNode node, CancellationToken ct)
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
                            var finalUrl = GetAbsoluteUrl(new Url(baseUrl ?? document.Url), url);
                            Enqueue(document, finalUrl, propertyRule, ct);
                        }
                    }
                }
            }
        }

        private void Enqueue(Document document, string url, string language, string excerpt, CancellationToken ct)
        {
            if (!MustEnqueueUrl(url))
                return;

            // Remove fragment
            var parsedUrl = new Url(url);
            parsedUrl.Fragment = null;

            var discoveredUrl = new DiscoveredUrl();
            discoveredUrl.Url = parsedUrl.Href;
            discoveredUrl.Language = language;
            discoveredUrl.SourceDocument = document;
            discoveredUrl.Excerpt = excerpt;

            _discoveredUrls.Add(discoveredUrl, ct);
        }

        private void Enqueue(Document document, string url, IElement node, CancellationToken ct)
        {
            var fullUrl = GetAbsoluteUrl(node.BaseUrl, url);
            Enqueue(document, fullUrl, null, GetExcerpt(node), ct);
        }

        private void Enqueue(Document document, string url, string language, IElement node, CancellationToken ct)
        {
            var fullUrl = GetAbsoluteUrl(node.BaseUrl, url);
            Enqueue(document, fullUrl, language, GetExcerpt(node), ct);
        }

        private string GetAbsoluteUrl(Url baseUrl, string url)
        {
            var u = new Url(url);
            if (u.IsAbsolute)
                return u.Href;

            var fullUrl = new Url(baseUrl, url);
            return fullUrl.Href;
        }

        private void Enqueue(Document document, string url, ICssNode node, CancellationToken ct)
        {
            Enqueue(document, url, null, GetExcerpt(node), ct);
        }

        private void EnqueueRedirect(Document document, IElement node, string url, CancellationToken ct)
        {
            if (!MustEnqueueUrl(url))
                return;

            // Remove fragment
            var parsedUrl = new Url(url);
            parsedUrl.Fragment = null;

            var redirectUrl = parsedUrl.Href;
            document.RedirectUrl = redirectUrl;

            if (document.IsSelfOrInRedirections(redirectUrl))
            {
                document.IsRedirectionLoop = true;
                return;
            }

            var discoveredUrl = new DiscoveredUrl
            {
                Url = redirectUrl,
                SourceDocument = document,
                IsRedirect = true,
                Excerpt = GetExcerpt(node)
            };

            _discoveredUrls.Add(discoveredUrl, ct);
        }

        private void EnqueueRedirect(Document document, string url, CancellationToken ct)
        {
            EnqueueRedirect(document, null, url, ct);
        }

        private bool MustEnqueueUrl(string url)
        {
            return IsHttpProtocol(url);
        }

        private static bool IsHttpProtocol(string url)
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