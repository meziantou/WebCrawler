using System.Collections.Generic;

namespace WebCrawler.Analysers
{
    public interface ICssAnalyser : IAnalyser
    {
        IEnumerable<AnalyserResultItem> Analyse(CssAnalyseArgs args);
    }
}