using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using AngleSharp;

namespace WebCrawler
{
    internal class Utilities
    {
        public static bool IsSameHost(string a, string b)
        {
            return new Url(a).Host == new Url(b).Host;
        }

        public static bool IsHtmlMimeType(string mimeType)
        {
            string[] mimeTypes =
            {
                "text/html",
                "application/xhtml+xml"
            };

            foreach (var type in mimeTypes)
            {
                if (string.Equals(mimeType, type, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool IsCssMimeType(string mimeType)
        {
            string[] mimeTypes =
            {
                "text/css"
            };

            foreach (var type in mimeTypes)
            {
                if (string.Equals(mimeType, type, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool IsJavaScriptMimeType(string mimeType)
        {
            string[] mimeTypes =
            {
                "application/ecmascript",
                "application/javascript",
                "application/x-ecmascript",
                "application/x-javascript",
                "text/ecmascript",
                "text/javascript",
                "text/javascript1.0",
                "text/javascript1.1",
                "text/javascript1.2",
                "text/javascript1.3",
                "text/javascript1.4",
                "text/javascript1.5",
                "text/jscript",
                "text/livescript",
                "text/x-ecmascript",
                "text/x-javascript"
            };

            foreach (var type in mimeTypes)
            {
                if (string.Equals(mimeType, type, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool IsRedirect(HttpStatusCode code)
        {
            return code == HttpStatusCode.Redirect || code == HttpStatusCode.MovedPermanently;
        }
        
        public static string ParseCssUrl(string value)
        {
            if (value == null)
                return null;

            value = value.Trim();
            if (value.StartsWith("url"))
            {
                var result = new StringBuilder(value.Length);
                bool isInParentheses = false;
                bool isInQuote = false;
                char quoteChar = '\0';
                for (int i = 3; i < value.Length; i++)
                {
                    char c = value[i];
                    if (c == ' ' && !isInQuote)
                        continue;

                    if (c == '(')
                    {
                        if (isInQuote)
                        {
                            result.Append(c);
                            continue;
                        }

                        if (isInParentheses)
                            return null;

                        isInParentheses = true;
                        continue;
                    }
                    else if (c == ')')
                    {
                        if (isInQuote)
                        {
                            result.Append(c);
                            continue;
                        }

                        if (!isInParentheses)
                        {
                            return null;
                        }

                        return result.ToString();
                    }
                    else if (c == '"' || c == '\'')
                    {
                        if (isInQuote)
                        {
                            if (c == quoteChar)
                            {
                                isInQuote = false;
                                continue;
                            }
                            else
                            {
                                result.Append(c);
                                continue;
                            }
                        }

                        isInQuote = true;
                        quoteChar = c;
                        result.Clear();
                    }
                    else
                    {
                        if (isInParentheses)
                        {
                            result.Append(c);
                        }
                    }
                }
            }

            return null;
        }
    }
}
