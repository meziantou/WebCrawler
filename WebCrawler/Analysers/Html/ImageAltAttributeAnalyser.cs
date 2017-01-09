
using System;
using System.Collections.Generic;
using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;

namespace WebCrawler.Analysers.Html
{
    public class ImageAltAttributeAnalyser : IHtmlAnalyser
    {
        public IEnumerable<AnalyserResultItem> Analyse(HtmlAnalyseArgs args)
        {
            var document = args.HtmlDocument;
            var images = document.GetElementsByTagName("img");
            foreach (var image in images)
            {
                var src = image.GetAttribute("src");
                var alt = image.GetAttribute("alt");
                if (!string.IsNullOrEmpty(src) && string.IsNullOrEmpty(alt))
                {
                    yield return new AnalyserResultItem
                    {
                        Type = AnalyserResultType.Warning,
                        Category = Categories.Seo,
                        Excerpt = image.ToHtml(),
                        Message = "Image should have an \"alt\" attribute"
                    };
                }
            }
            /*
            INode node;
            while ((node = iterator.Next()) != null)
            {
                if (node.TextContent.StartsWith("[if ", StringComparison.OrdinalIgnoreCase))
                {
                    // Conditional comments should be removed?
                }
                else
                {
                    yield return new AnalyserResultItem
                    {
                        Type = AnalyserResultType.Warning,
                        Category = Categories.Performance,
                        Excerpt = node.ToHtml(),
                        Message = "Comments are useless"
                    };
                }
            }*/
        }
    }
}
