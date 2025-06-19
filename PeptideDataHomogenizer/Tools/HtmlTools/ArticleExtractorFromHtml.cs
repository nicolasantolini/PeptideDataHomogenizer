namespace PeptideDataHomogenizer.Tools.HtmlTools
{
    using Entities;
    using HtmlAgilityPack;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using Microsoft.Extensions.DependencyInjection;
    using PeptideDataHomogenizer.Tools.WebScraper;

    public class ArticleExtractorFromHtml
        {
            private static readonly List<string> BlockedChapters = new List<string>
            {
                "Acknowledgments",
                "References",
                "Authors",
                "Recommended articles",
                "Citations",
                "Altmetrics",
                "Metrics",
                "Notes",
                "Competing interests",
                "Author contributions",
                "Submission history",
                "Keywords",
                "Copyright",
                "Classifications",
                "Published in",
                "Information",
                "Supporting Information",
                "Affiliations",
                "Cite this article",
                "View options",
                "PDF format",
                "Login options",
                "Recommend to a librarian",
                "Purchase options",
                "Share",
                "Restore content access",
                "Further reading in this issue",
                "Sign",
                "Cookie",
                "Alert",
                "Citing",
                "Most read",
                "Most cited",
                "Subscribers",
                "Views",
                "Article Content",
                "Cookies",
                "Extra",
                "Figure",
                "Full",
                "Table",
                "Graphical",
                "Conflicts of Interest",
                "Password",
                "Log in",
                "Related",
                "Funding",
                "Account",
                "Username",
                "Journals",
                "Topics",
                "Author",
                "Initiative",
                "Notice",
                "Menu",
                "Help",
                "Feedback",
                "Supplement",
                "Statement",
                "Follow",
                "Guideline",
                "Contribution",
                "Access Statistics",
                "Privacy",
                "Similar works",
                "Search",
                "Navigation",
                "Acknowledgements",
                "Ethics",
                "Review",
                "Consent",
                "Note",
                "Rights and permissions",
                "About",
                "License Summary",
                "Publication History",
                "You have not visited any articles yet",
                "Connect with AIP Publishing",
                "Explore",
                "DATA AVAILABILITY",
                "SUPPLEMENTARY MATERIAL",
                "Similar content being viewed by others",
                "partners can use this purpose",
                "Login",
                "Register",
                "Download",
                "permissions",
                "Terms &amp; Conditions",
                "Abstract"

            };

            private readonly IWebHostEnvironment _hostingEnvironment;
            private readonly HttpClient _httpClient;

            public ArticleExtractorFromHtml(IWebHostEnvironment hostingEnvironment, HttpClient httpClient)
            {
                _hostingEnvironment = hostingEnvironment;
                _httpClient = httpClient;
                _httpClient.DefaultRequestHeaders.Add("User-Agent", RandomUserAgentGenerator.GetRandomUserAgent());
                _httpClient.Timeout = TimeSpan.FromSeconds(30); // Set a reasonable timeout for HTTP requests
        }


        public List<Chapter> ExtractChapters(string html, string title,string url)
        {
            if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(title))
            {
                return new List<Chapter>();
            }
            //write html to wwwroot/html.txt
            var htmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "html.txt");
            Directory.CreateDirectory(_hostingEnvironment.WebRootPath); // Ensure the directory exists
            File.AppendAllText(htmlFilePath, html);


            //extract url base from url
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var baseUri))
            {
                baseUri = new Uri("https://example.com"); // Default base URL if none provided
            }

            //replace all &nbsp;
            html = html.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&#x27;", "'").Replace("&#x22;", "\"").Replace("&#x3C;", "<").Replace("&#x3E;", ">").Replace("&#x26;", "&");
            // Load the HTML into HtmlAgilityPack
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            Console.WriteLine(baseUri.ToString());
            ImagesExtractorFromHtml.ExtractAndSaveImages(doc.DocumentNode, baseUri.ToString(),_hostingEnvironment,_httpClient).Wait();

            RemoveUnwantedNodes(doc.DocumentNode);
            

            var chapters = ExtractChaptersFromCleanDocument(doc, title);

            return chapters;
        }

        

        

        private List<Chapter> ExtractChaptersFromCleanDocument(HtmlDocument doc, string title)
            {
                var chapters = new List<Chapter>();
                var headings = doc.DocumentNode.SelectNodes("//h1|//h2|//h3|//h4|//h5|//h6");

                if (headings != null)
                {
                    foreach (var heading in headings)
                    {
                        if (heading == null || heading.InnerText == null)
                            continue;
                        var headingText = heading.InnerText.Trim();
                        if (BlockedChapters.Any(b => headingText.StartsWith(b, StringComparison.OrdinalIgnoreCase) || b.StartsWith(headingText, StringComparison.OrdinalIgnoreCase) || headingText.Contains(b, StringComparison.OrdinalIgnoreCase) || headingText.Contains(title, StringComparison.OrdinalIgnoreCase) || title.Contains(headingText, StringComparison.OrdinalIgnoreCase)))
                            continue;
                        
                        var chapter = new Chapter
                        {
                            Title = FormatText(heading).Trim(),
                            Content = GetContentUntilNextHeading(heading)
                        };
                        if (chapter.Content.Length < 100)
                        {
                            continue;
                        }
                        chapters.Add(chapter);
                    }
                }
                else
                {
                    chapters.Add(new Chapter
                    {
                        Title = "Impossible to understand text",
                        Content = FormatText(doc.DocumentNode)
                    });
                }
            //if there are more than 5 chapters which are empty, remove them, try alternative
            if (chapters.Count(chapter => string.IsNullOrWhiteSpace(chapter.Content)) > 5 || chapters.Count(c=> c.Content.Length>70)<=3)
            {
                Console.WriteLine("TRYING ALTERNATIVE");
                chapters = ExtractChaptersAlternative(doc.DocumentNode.OuterHtml,title);
            }
            //print chapters to json
            if (chapters.Count <2 || chapters.Count(chapter => string.IsNullOrWhiteSpace(chapter.Content)) > 5 || chapters.Count(c => c.Content.Length > 300) <= 3)
            {
                Console.WriteLine("TRYING SECOND ALTERNATIVE");
                chapters = ExtractChaptersSecondAlternative(doc.DocumentNode.OuterHtml);
            }
            chapters.RemoveAll(chapter => string.IsNullOrWhiteSpace(chapter.Content));

            return chapters;
            }

        public List<Chapter> ExtractChaptersAlternative(string htmlContent,string title)
        {
            var chapters = new List<Chapter>();
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // First get all potential content sections
            var allContentSections = doc.DocumentNode.SelectNodes("//div[contains(@class, 'article-section-wrapper')]");
            // Get all headings
            var headingNodes = doc.DocumentNode.SelectNodes("//h2[@class='section-title jumplink-heading'] | //h3[@class='section-title jumplink-heading']");

            if (headingNodes == null) return chapters;

            // Create dictionary of content sections by parent ID for faster lookup
            var contentDict = new Dictionary<string, List<HtmlNode>>();
            if (allContentSections != null)
            {
                foreach (var section in allContentSections)
                {
                    var parentId = section.GetAttributeValue("data-section-parent-id", null);
                    if (parentId != null)
                    {
                        if (!contentDict.ContainsKey(parentId))
                            contentDict[parentId] = new List<HtmlNode>();
                        contentDict[parentId].Add(section);
                    }
                }
            }
            foreach (var headingNode in headingNodes)
            {
                var headingText = headingNode.InnerText.Trim();
                if (BlockedChapters.Any(b => headingText.StartsWith(b, StringComparison.OrdinalIgnoreCase) || b.StartsWith(headingText, StringComparison.OrdinalIgnoreCase) || headingText.Contains(b, StringComparison.OrdinalIgnoreCase) || headingText.Contains(title, StringComparison.OrdinalIgnoreCase) || title.Contains(headingText, StringComparison.OrdinalIgnoreCase)))
                    continue;
                var chapter = new Chapter
                {
                    Title = headingText,
                    Content = ""
                };

                var parentId = headingNode.GetAttributeValue("id", null);

                if (parentId != null && contentDict.TryGetValue(parentId, out var contentSections))
                {

                    var paragraphs = new List<string>();
                    foreach (var section in contentSections)
                    {
                        var sectionParagraphs = section.SelectNodes(".//p");
                        if (sectionParagraphs != null)
                        {
                            foreach (var para in sectionParagraphs)
                            {
                                var text = para.InnerText.Trim();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    paragraphs.Add(text);
                                }
                            }
                        }
                    }

                    chapter.Content = string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
                }
                else
                {
                    Console.WriteLine("No content sections found for this heading");
                }
                if (chapter.Content.Length < 100)
                {
                    continue;
                }

                chapters.Add(chapter);
            }

            return chapters;
        }

        private string GetContentUntilNextHeading(HtmlNode heading)
            {
                var sb = new StringBuilder();
                var node = heading.NextSibling;

                while (node != null && !IsHeading(node))
                {
                    sb.Append(FormatText(node));
                    node = node.NextSibling;
                }

                return sb.ToString().Trim();
            }

        public List<Chapter> ExtractChaptersSecondAlternative(string htmlContent)
        {
            var chapters = new List<Chapter>();
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // Try to find article body - this will depend on the actual HTML structure
            var articleBody = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'article-body')]")
                             ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class, 't-html')]")
                             ?? doc.DocumentNode;

            // First try to find explicit headings
            var headingNodes = articleBody.SelectNodes(".//*[self::h1 or self::h2 or self::h3 or self::h4 or self::h5 or self::h6 or contains(@class, 'heading')]");

            // If no explicit headings found, look for any elements that might serve as section dividers
            if (headingNodes == null || headingNodes.Count == 0)
            {
                headingNodes = articleBody.SelectNodes(".//*[contains(@class, 'section') or contains(@class, 'title')]");
            }

            // If still no headings found, fall back to full text
            if (headingNodes == null || headingNodes.Count == 0)
            {
                chapters.Add(new Chapter
                {
                    Title = "Full Text",
                    Content = FormatText(articleBody)
                });
                return chapters;
            }

            // Process each heading
            for (int i = 0; i < headingNodes.Count; i++)
            {
                var headingNode = headingNodes[i];
                var headingText = FormatText(headingNode).Trim();

                // Skip blocked chapters
                if (BlockedChapters.Any(b =>
                    headingText.StartsWith(b, StringComparison.OrdinalIgnoreCase) ||
                    b.StartsWith(headingText, StringComparison.OrdinalIgnoreCase) ||
                    headingText.Contains(b, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // Get content until next heading
                var contentBuilder = new StringBuilder();
                var currentNode = headingNode.NextSibling;

                while (currentNode != null && (i == headingNodes.Count - 1 || currentNode != headingNodes[i + 1]))
                {
                    if (currentNode.NodeType == HtmlNodeType.Element)
                    {
                        var paragraphText = FormatText(currentNode).Trim();
                        if (!string.IsNullOrEmpty(paragraphText))
                        {
                            contentBuilder.AppendLine(paragraphText);
                            contentBuilder.AppendLine(); // Add extra line between paragraphs
                        }
                    }
                    currentNode = currentNode.NextSibling;
                }

                var content = contentBuilder.ToString().Trim();

                if(content.Length < 100)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(content) || !string.IsNullOrEmpty(headingText))
                {
                    chapters.Add(new Chapter
                    {
                        Title = headingText,
                        Content = content
                    });
                }
            }

            // If we ended up with no chapters (maybe all were blocked), return full text
            if (chapters.Count == 0)
            {
                chapters.Add(new Chapter
                {
                    Title = "Full Text",
                    Content = articleBody.ToString()
                });
            }

            return chapters;
        }


        private int GetHeadingLevel(HtmlNode node)
        {
            if (node.NodeType != HtmlNodeType.Element)
                return 0;

            var classAttr = node.GetAttributeValue("class", "");
            if (string.IsNullOrEmpty(classAttr))
                return 0;

            var match = System.Text.RegularExpressions.Regex.Match(classAttr, @"h--heading(\d)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int level))
                return level;

            return 0;
        }

        private void RemoveUnwantedNodes(HtmlNode node)
            {
                var unwantedTags = new[] { "script", "style", "noscript", "iframe", "svg", "nav", "footer", "header" };
                foreach (var tag in unwantedTags)
                {
                    var nodes = node.Descendants(tag).ToArray();
                    foreach (var n in nodes)
                    {
                        n.Remove();
                    }
                }

                var comments = node.Descendants()
                    .Where(n => n.NodeType == HtmlNodeType.Comment)
                    .ToArray();
                foreach (var c in comments)
                {
                    c.Remove();
                }

                var emptyNodes = node.Descendants()
                    .Where(n => n.NodeType == HtmlNodeType.Element &&
                               !n.HasChildNodes &&
                               string.IsNullOrWhiteSpace(n.InnerText))
                    .ToArray();
                foreach (var e in emptyNodes)
                {
                    e.Remove();
                }
            }

            private string FormatText(HtmlNode node)
            {
                StringBuilder sb = new StringBuilder();

                foreach (var child in node.ChildNodes)
                {
                    switch (child.NodeType)
                    {
                        case HtmlNodeType.Text:
                            var text = ((HtmlTextNode)child).Text;
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                sb.Append(text.Trim());
                                sb.Append(' ');
                            }
                            break;

                        case HtmlNodeType.Element:
                            if (child.Name == "p" || child.Name == "div")
                            {
                                sb.AppendLine();
                                sb.Append(FormatText(child));
                                sb.AppendLine();
                            }
                            else if (child.Name == "br")
                            {
                                sb.AppendLine();
                            }
                            else if (IsHeading(child))
                            {
                            }
                            else
                            {
                                sb.Append(FormatText(child));
                            }
                            break;
                    }
                }

                return sb.ToString();
            }

            private bool IsHeading(HtmlNode node)
            {
                return node.NodeType == HtmlNodeType.Element &&
                       node.Name.Length == 2 &&
                       node.Name[0] == 'h' &&
                       char.IsDigit(node.Name[1]);
            }
        }

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddArticleExtractor(this IServiceCollection services)
        {
            services.AddSingleton<ArticleExtractorFromHtml>();
            services.AddHttpClient();
            return services;
        }
    }

}

