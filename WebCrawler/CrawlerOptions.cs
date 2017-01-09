using System.Collections.Generic;
using System.Text.RegularExpressions;
using WebCrawler.Analysers;

namespace WebCrawler
{
    public class CrawlerOptions
    {
        public string DefaultAcceptLanguage { get; set; } = "en-US,en;q=0.8";
        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/54.0.2840.99 Safari/537.36";
        public int MaxConcurrency { get; set; } = 16;
        public IList<Regex> Includes { get; set; } = new List<Regex>();
        public IList<IAnalyser> Analysers { get; } = new List<IAnalyser>();
    }
}