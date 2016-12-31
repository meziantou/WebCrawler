using System.Net.WebSockets;
using System.Threading;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Serialization;

namespace WebCrawler.Site
{
    public class WebSocketHandler
    {
        private readonly WebSocket _socket;
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        private WebSocketHandler(WebSocket socket)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));
            _socket = socket;
        }

        public static async Task AcceptorAsync(HttpContext httpContext, Func<Task> next)
        {
            if (!httpContext.WebSockets.IsWebSocketRequest)
                return;

            using (var socket = await httpContext.WebSockets.AcceptWebSocketAsync())
            {
                var handler = new WebSocketHandler(socket);
                await handler.ProcessAsync(httpContext.RequestAborted);
            }
        }

        private async Task ProcessAsync(CancellationToken ct = default(CancellationToken))
        {
            var data = await ReceiveJsonAsync<StartCrawlingArgs>(ct);
            if (data == null)
            {
                await _socket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "", ct);
                return;
            }

            var options = new CrawlerOptions();
            if (!string.IsNullOrWhiteSpace(data.UrlIncludePatterns))
            {
                using (var reader = new StringReader(data.UrlIncludePatterns))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var regex = new Regex(line, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                        options.Includes.Add(regex);
                    }
                }
            }

            using (var crawler = new Crawler(options))
            {
                crawler.DocumentParsed += async (sender, e) =>
                {
                    await SendJsonAsync(new
                    {
                        Type = 1,
                        Document = new ServiceDocument(e.Document)
                    }, ct);

                };

                crawler.DocumentRefAdded += async (sender, e) =>
                {
                    await SendJsonAsync(new
                    {
                        Type = 2,
                        DocumentRef = new ServiceDocumentRef(e.DocumentRef)
                    }, ct);
                };

                try
                {
                    await crawler.RunAsync(data.Url, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await SendJsonAsync(new
                    {
                        Type = 3,
                        Exception = ex.ToString()
                    }, ct);
                }
            }
        }

        private JsonSerializerSettings CreateJsonSettings()
        {
            return new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        private async Task SendJsonAsync(object data, CancellationToken ct = default(CancellationToken))
        {
            ct.ThrowIfCancellationRequested();

            var json = JsonConvert.SerializeObject(data, CreateJsonSettings());
            var buffer = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(buffer);

            await _semaphoreSlim.WaitAsync(ct);
            try
            {
                await _socket.SendAsync(segment, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private async Task<T> ReceiveJsonAsync<T>(CancellationToken ct)
        {
            var json = await ReceiveStringAsync(ct);
            return JsonConvert.DeserializeObject<T>(json, CreateJsonSettings());
        }

        private async Task<string> ReceiveStringAsync(CancellationToken ct = default(CancellationToken))
        {
            var buffer = new ArraySegment<byte>(new byte[8192]);
            using (var ms = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    ct.ThrowIfCancellationRequested();

                    result = await _socket.ReceiveAsync(buffer, ct);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);
                if (result.MessageType != WebSocketMessageType.Text)
                    throw new Exception("Unexpected message");

                using (var reader = new StreamReader(ms, Encoding.UTF8))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        private class StartCrawlingArgs
        {
            public string Url { get; set; }
            public string UrlIncludePatterns { get; set; }
        }

        private class ServiceDocument
        {
            public ServiceDocument(Document document)
            {
                Id = document.Id;
                Url = document.Url;
                RedirectUrl = document.RedirectUrl;
                Language = document.Language;
                StatusCode = document.StatusCode;
                CrawledOn = document.CrawledOn;
                RequestHeaders = document.RequestHeaders;
                ResponseHeaders = document.ResponseHeaders;
                ErrorMessage = document.ErrorMessage;
                FullErrorMessage = document.FullErrorMessage;
                ReasonPhrase = document.ReasonPhrase;
                if (document.ReferencedBy != null)
                {
                    lock (document.ReferencedBy)
                    {
                        ReferencedBy = document.ReferencedBy.Select(docRef => new ServiceDocumentRef(docRef)).ToList();
                    }
                }
            }

            public DateTime CrawledOn { get; }
            public string Language { get; set; }
            public IDictionary<string, string> RequestHeaders { get; }
            public IDictionary<string, string> ResponseHeaders { get; }
            public Guid Id { get; }
            public string RedirectUrl { get; }
            public HttpStatusCode StatusCode { get; }
            public string Url { get; }
            public string ErrorMessage { get; }
            public string FullErrorMessage { get; }
            public string ReasonPhrase { get; set; }
            public IList<ServiceDocumentRef> ReferencedBy { get; }
        }

        private class ServiceDocumentRef
        {
            public ServiceDocumentRef(DocumentRef documentRef)
            {
                Excerpt = documentRef.Excerpt;
                SourceDocumentId = documentRef.SourceDocument.Id;
                TargetDocumentId = documentRef.TargetDocument.Id;
                SourceDocumentUrl = documentRef.SourceDocument.Url;
                TargetDocumentUrl = documentRef.TargetDocument.Url;
            }

            public Guid SourceDocumentId { get; set; }
            public Guid TargetDocumentId { get; set; }
            public string SourceDocumentUrl { get; set; }
            public string TargetDocumentUrl { get; set; }
            public string Excerpt { get; set; }
        }
    }
}
