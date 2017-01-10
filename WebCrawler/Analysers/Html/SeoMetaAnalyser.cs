using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;

namespace WebCrawler.Analysers.Html
{
    public class SeoMetaAnalyser : IHtmlAnalyser
    {
        public IEnumerable<AnalyserResultItem> Analyse(HtmlAnalyseArgs args)
        {
            var document = args.HtmlDocument;

            yield return AnalyseTitle(document);
            yield return AnalyseDescription(document);

            foreach(var item in AnalyseOpenGraphTags(document)) yield return item;
            foreach(var item in AnalyseTwitterTags(document)) yield return item;
            foreach(var item in AnalyseFacebookTags(document)) yield return item;
            foreach(var item in AnalyseAppLinkTags(document)) yield return item;
            foreach(var item in AnalyseAppLinkTags(document)) yield return item;
        }

        private AnalyserResultItem AnalyseDescription(IDocument document)
        {
            var description = document.QuerySelectorAll<IHtmlMetaElement>("meta").LastOrDefault(meta => string.Equals(meta.Name, "description", StringComparison.OrdinalIgnoreCase))?.Content;
            if (string.IsNullOrEmpty(description))
            {
                return new AnalyserResultItem
                {
                    Category = Categories.Seo,
                    Type = AnalyserResultType.Warning,
                    Message = "Page has no description"
                };
            }
            else
            {
                return new AnalyserResultItem
                {
                    Category = Categories.Seo,
                    Type = AnalyserResultType.Info,
                    Message = "Page has a description",
                    Excerpt = description
                };
            }
        }

        private AnalyserResultItem AnalyseTitle(IDocument document)
        {
            var title = document.Title;
            if (string.IsNullOrEmpty(title))
            {
                return new AnalyserResultItem
                {
                    Category = Categories.Seo,
                    Type = AnalyserResultType.Warning,
                    Message = "Page has no title"
                };
            }
            else
            {
                return new AnalyserResultItem
                {
                    Category = Categories.Seo,
                    Type = AnalyserResultType.Info,
                    Message = "Page has a title",
                    Excerpt = title
                };
            }
        }

        private IEnumerable<AnalyserResultItem> AnalyseOpenGraphTags(IDocument document)
        {
            var metas = document.QuerySelectorAll<IHtmlMetaElement>("meta").Where(meta => meta.GetAttribute("property")?.StartsWith("og:", StringComparison.OrdinalIgnoreCase) == true);
            var sb = new StringBuilder();
            foreach (var meta in metas)
            {
                sb.Append(meta.GetAttribute("property"));
                sb.Append(meta.GetAttribute(": "));
                sb.AppendLine(meta.GetAttribute(meta.Content));
            }

            if(sb.Length == 0)
                yield break;

            yield return new AnalyserResultItem
            {
                Category = Categories.Seo,
                Type = AnalyserResultType.Info,
                Message = "Page has Open Graph meta tags",
                Excerpt = sb.ToString()
            };
        }

        private IEnumerable<AnalyserResultItem> AnalyseFacebookTags(IDocument document)
        {
            var metas = document.QuerySelectorAll<IHtmlMetaElement>("meta").Where(meta => meta.GetAttribute("property")?.StartsWith("fb:", StringComparison.OrdinalIgnoreCase) == true);
            var sb = new StringBuilder();
            foreach (var meta in metas)
            {
                sb.Append(meta.GetAttribute("property"));
                sb.Append(meta.GetAttribute(": "));
                sb.AppendLine(meta.GetAttribute(meta.Content));
            }

            if (sb.Length == 0)
                yield break;

            yield return new AnalyserResultItem
            {
                Category = Categories.Seo,
                Type = AnalyserResultType.Info,
                Message = "Page has Facebook meta tags",
                Excerpt = sb.ToString()
            };
        }
        
        private IEnumerable<AnalyserResultItem> AnalyseTwitterTags(IDocument document)
        {
            var metas = document.QuerySelectorAll<IHtmlMetaElement>("meta").Where(meta => meta.Name?.StartsWith("twitter:", StringComparison.OrdinalIgnoreCase) == true);
            var sb = new StringBuilder();
            foreach (var meta in metas)
            {
                sb.Append(meta.Name);
                sb.Append(meta.GetAttribute(": "));
                sb.AppendLine(meta.GetAttribute(meta.Content));
            }

            if (sb.Length == 0)
                yield break;

            yield return new AnalyserResultItem
            {
                Category = Categories.Seo,
                Type = AnalyserResultType.Info,
                Message = "Page has Twitter meta tags",
                Excerpt = sb.ToString()
            };
        }

        private IEnumerable<AnalyserResultItem> AnalyseAppLinkTags(IDocument document)
        {
            var metas = document.QuerySelectorAll<IHtmlMetaElement>("meta").Where(meta => meta.GetAttribute("property")?.StartsWith("al:", StringComparison.OrdinalIgnoreCase) == true);
            var sb = new StringBuilder();
            foreach (var meta in metas)
            {
                sb.Append(meta.GetAttribute("property"));
                sb.Append(meta.GetAttribute(": "));
                sb.AppendLine(meta.GetAttribute(meta.Content));
            }

            if (sb.Length == 0)
                yield break;

            yield return new AnalyserResultItem
            {
                Category = Categories.Seo,
                Type = AnalyserResultType.Info,
                Message = "Page has App Link meta tags",
                Excerpt = sb.ToString()
            };
        }

    }
}
