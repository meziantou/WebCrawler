using System.Collections.Generic;

namespace WebCrawler.Analysers
{
    public interface ICssAnalyser : IAnalyser
    {
        CssAnalyserTargets Targets { get; }
        IEnumerable<AnalyserResultItem> Analyse(CssAnalyseArgs args);
    }
}