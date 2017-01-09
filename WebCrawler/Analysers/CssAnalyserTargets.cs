using System;

namespace WebCrawler.Analysers
{
    [Flags]
    public enum CssAnalyserTargets
    {
        None = 0,
        StyleSheet = 1,
        HtmlStyleTag = 2,
        HtmlStyleAttribute = 4,

        All = StyleSheet | HtmlStyleTag | HtmlStyleAttribute
    }
}