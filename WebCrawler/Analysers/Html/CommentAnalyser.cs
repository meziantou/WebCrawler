using System;
using System.Collections.Generic;
using AngleSharp.Dom;
using AngleSharp.Extensions;

namespace WebCrawler.Analysers.Html
{
    public class CommentAnalyser : IHtmlAnalyser
    {
        public IEnumerable<AnalyserResultItem> Analyse(HtmlAnalyseArgs args)
        {
            var document = args.HtmlDocument;
            var iterator = document.CreateNodeIterator(document);
            INode node;
            while ((node = iterator.Next()) != null)
            {
                if (node.NodeType != NodeType.Comment)
                    continue;

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
            }
        }
    }
}
