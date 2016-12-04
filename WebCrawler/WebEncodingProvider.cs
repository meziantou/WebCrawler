using System;
using System.Text;

namespace WebCrawler
{
    public class WebEncodingProvider : EncodingProvider
    {
        public static WebEncodingProvider Instance { get; } = new WebEncodingProvider();

        public override Encoding GetEncoding(int codepage)
        {
            return null;
        }

        public override Encoding GetEncoding(string name)
        {
            string[] utf8 = { "utf-8", "\"utf-8\"" };
            foreach (var encoding in utf8)
            {
                if (string.Equals(name, encoding, StringComparison.OrdinalIgnoreCase))
                    return Encoding.UTF8;
            }

            return null;
        }
    }
}