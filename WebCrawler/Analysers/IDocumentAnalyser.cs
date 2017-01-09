using System.Collections.Generic;

namespace WebCrawler.Analysers
{
    public interface IDocumentAnalyser : IAnalyser
    {
        IEnumerable<AnalyserResultItem> Analyse(AnalyseArgs args);
    }
}