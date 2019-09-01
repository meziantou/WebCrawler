# WebCrawler

WebCrawler allows to extract all accessible URLs from a website. It's built using [`.NET Core`](https://www.microsoft.com/net/core) and `.NET Standard 1.4`, so you can host it anywhere (Windows, Linux, Mac).

The crawler does not use regex to find links. Instead, Web pages are parsed using [AngleSharp](https://github.com/AngleSharp/AngleSharp), 
a parser which is built upon the official W3C specification. This allows to parse pages as a browser and handle tricky tags such as `base`.

For HTML files, URLs are extracted from:
- `<a href="...">`
- `<area href="...">`
- `<audio src="...">`
- `<iframe src="...">`
- `<img src="...">`
- `<img srcset="...">`
- `<link href="...">`
- `<object data="...">`
- `<script src="...">`
- `<source src="...">`
- `<source srcset="...">`
- `<track src="...">`
- `<video src="...">`
- `<video poster="...">`
- `<... style="...">` (*see CSS section*)

For CSS files, URLs are extracted from:
- `rule: url(...)`

## How to deploy on Azure (free)

You can deploy the website on Azure for free:

1. Create a free Web App
2. Enable WebSockets in Application Settings ([Introduction to WebSockets on Windows Azure Web Sites](https://azure.microsoft.com/fr-fr/blog/introduction-to-websockets-on-windows-azure-web-sites/), [Using Web Sockets with ASP.NET Core](http://www.softfluent.com/blog/dev/2016/12/11/Using-Web-Sockets-with-ASP-NET-Core))
3. Deploy the website using WebDeploy or FTP

## Blog posts

Some parts of the code are explained in blog posts:
- [Write your own DOM element factory for TypeScript](https://www.meziantou.net/write-your-own-dom-element-factory-for-typescript.htm)
- [Using Web Sockets with ASP.NET Core](https://www.meziantou.net/using-web-sockets-with-asp-net-core.htm)
- [Using Let’s encrypt with ASP.NET Core](https://www.meziantou.net/using-let-s-encrypt-with-asp-net-core.htm)
