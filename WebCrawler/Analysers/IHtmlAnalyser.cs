using System.Collections.Generic;

namespace WebCrawler.Analysers
{
    public interface IHtmlAnalyser : IAnalyser
    {
        IEnumerable<AnalyserResultItem> Analyse(HtmlAnalyseArgs args);
    }
}
