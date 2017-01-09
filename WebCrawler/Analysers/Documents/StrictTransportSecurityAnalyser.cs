using System;
using System.Collections.Generic;

namespace WebCrawler.Analysers.Documents
{
    public class StrictTransportSecurityAnalyser : IDocumentAnalyser
    {
        private const string DocumentationUrl = "https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Strict-Transport-Security";

        private bool IsHttps(Document document)
        {
            if (document.Url == null)
                return false;

            return document.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private string GetHstsHeader(Document document)
        {
            if (document.ResponseHeaders.TryGetValue("Strict-Transport-Security", out var header))
                return header;

            return null;
        }

        public IEnumerable<AnalyserResultItem> Analyse(AnalyseArgs args)
        {
            if (IsHttps(args.Document))
            {
                var header = GetHstsHeader(args.Document);
                if (header == null)
                {
                    yield return new AnalyserResultItem()
                    {
                        Category = Categories.Security,
                        Type = AnalyserResultType.Warning,
                        Message = "Strict-Transport-Security header not found",
                        DocumentationUrl = DocumentationUrl
                    };
                }
                else
                {
                    yield return new AnalyserResultItem()
                    {
                        Category = Categories.Security,
                        Type = AnalyserResultType.Good,
                        Message = "Strict-Transport-Security header found",
                        FullMessage = header,
                        DocumentationUrl = DocumentationUrl
                    };
                }
            }
        }
    }
}