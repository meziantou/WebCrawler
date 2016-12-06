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

namespace WebCrawler.Site
{
    public class WebSocketHandler : IDisposable
    {
        private WebSocket _socket;

        private WebSocketHandler(WebSocket socket)
        {
            _socket = socket;
        }

        public static async Task AcceptorAsync(HttpContext httpContext, Func<Task> next)
        {
            if (!httpContext.WebSockets.IsWebSocketRequest)
                return;

            var socket = await httpContext.WebSockets.AcceptWebSocketAsync();
            var handler = new WebSocketHandler(socket);
            await handler.InitAsync();
        }

        private async Task InitAsync()
        {
            var data = await ReceiveJsonAsync<StartCrawlingArgs>(CancellationToken.None);
            if (data == null)
            {
                await _socket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "", CancellationToken.None);
                return;
            }

            using (var crawler = new Crawler())
            {
                crawler.DocumentParsed += Crawler_DocumentParsed;
                crawler.DocumentUpdated += Crawler_DocumentUpdated;
                crawler.DocumentRefAdded += Crawler_DocumentRefAdded;
                try
                {
                    var result = await crawler.RunAsync(data.Url);
                }
                catch (Exception ex)
                {
                    await SendJsonAsync(new
                    {
                        Type = 3,
                        Exception = ex.ToString()
                    });
                }
            }
        }

        private void Crawler_DocumentRefAdded(object sender, DocumentRefAddedEventArgs e)
        {
            SendJsonAsync(new
            {
                Type = 4,
                DocumentRef = new ServiceDocumentRef(e.DocumentRef)
            });
        }

        private void Crawler_DocumentUpdated(object sender, DocumentEventArgs e)
        {
            //SendJsonAsync(new
            //{
            //    Type = 2,
            //    Document = new ServiceDocument(e.Document)
            //});
        }

        private void Crawler_DocumentParsed(object sender, DocumentEventArgs e)
        {
            SendJsonAsync(new
            {
                Type = 1,
                Document = new ServiceDocument(e.Document)
            });
        }

        private Task SendJsonAsync(object data, CancellationToken ct = default(CancellationToken))
        {
            var json = JsonConvert.SerializeObject(data);
            var buffer = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(buffer);
            return _socket.SendAsync(segment, WebSocketMessageType.Text, true, ct);
        }

        private async Task<T> ReceiveJsonAsync<T>(CancellationToken ct)
        {
            var json = await ReceiveStringAsync(ct);
            return JsonConvert.DeserializeObject<T>(json);
        }

        private async Task<string> ReceiveStringAsync(CancellationToken ct = default(CancellationToken))
        {
            ArraySegment<Byte> buffer = new ArraySegment<byte>(new Byte[8192]);
            WebSocketReceiveResult result = null;

            using (var ms = new MemoryStream())
            {
                do
                {
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

        public void Dispose()
        {
            _socket?.Dispose();
            _socket = null;
        }

        private class StartCrawlingArgs
        {
            public string Url { get; set; }
        }

        private class ServiceDocument
        {
            public ServiceDocument(Document document)
            {
                Id = document.Id;
                Url = document.Url;
                RedirectUrl = document.RedirectUrl;
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
