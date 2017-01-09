using System.Collections.Generic;
using System.Linq;
using AngleSharp.Dom.Css;
using AngleSharp.Extensions;

namespace WebCrawler.Analysers.Css
{
    public class EmptyRuleAnalyser : ICssAnalyser
    {
        public CssAnalyserTargets Targets => CssAnalyserTargets.All;

        public IEnumerable<AnalyserResultItem> Analyse(CssAnalyseArgs args)
        {
            var node = args.Node;
            var hashSet = new HashSet<ICssNode>();

            var rules = node.GetAll<ICssStyleSheet>().SelectMany(styleSheet => styleSheet.Rules);
            foreach (var rule in rules)
            {
                if (hashSet.Add(rule))
                {
                    if (!rule.GetAll<ICssProperty>().Any())
                    {
                        yield return CreateEmptyRuleResultItem(args, rule);
                    }
                }
            }

            var groups = node.GetAll<ICssGroupingRule>();
            foreach (var group in groups)
            {
                if (hashSet.Add(group))
                {
                    if (!group.Rules.Any())
                    {
                        yield return CreateEmptyRuleResultItem(args, group);
                    }
                }
            }
        }

        private AnalyserResultItem CreateEmptyRuleResultItem(CssAnalyseArgs args, ICssNode node)
        {
            string excerpt = null;
            if (args.Target == CssAnalyserTargets.HtmlStyleAttribute)
            {
                excerpt = args.Element.OuterHtml;
            }

            if (excerpt == null)
            {
                excerpt = node.ToCss();
            }

            return new AnalyserResultItem()
            {
                Category = Categories.Performance,
                Type = AnalyserResultType.Warning,
                Message = "Rule is empty",
                Excerpt = excerpt
            };
        }

    }
}
