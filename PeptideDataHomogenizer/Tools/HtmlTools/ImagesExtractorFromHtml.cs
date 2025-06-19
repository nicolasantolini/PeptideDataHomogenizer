using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using PeptideDataHomogenizer.Tools.WebScraper;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace PeptideDataHomogenizer.Tools.HtmlTools
{
    public static class ImagesExtractorFromHtml
    {

        public static Dictionary<string, List<string>> KeyWordsByUrl { get; set; } = new Dictionary<string, List<string>>
            {
                { "mdpi.com", new List<string> { "/article_deploy/" } },
                { "academic.oup.com", new List<string> { "nar_gkad" } },
                { "tandfonline.com", new List<string> { "/asset/" } }

            };

        public static async Task ExtractAndSaveImages(HtmlNode node, string baseUrl,
     [FromServices] IWebHostEnvironment webHostEnvironment,
     [FromServices] HttpClient httpClient)
        {
            Console.WriteLine("ExtractAndSaveImages called.");

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("User-Agent", RandomUserAgentGenerator.GetRandomUserAgent());
            httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            httpClient.DefaultRequestHeaders.Add("Referer", baseUrl);

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                Console.WriteLine("Base URL is null or whitespace. Exiting method.");
                return;
            }

            var imgNodes = node.SelectNodes("//img[@src]") ??
                           node.SelectNodes("//img[contains(@src, '.')]") ??
                           node.SelectNodes("//img");

            if (baseUrl.Contains("pubs.rsc.org"))
            {
                imgNodes = node.SelectNodes("//img[@data-original]") ??
                            node.SelectNodes("//img[contains(@data-original, '.')]") ??
                            node.SelectNodes("//img");

            }

            if (imgNodes == null)
            {
                Console.WriteLine("No <img> nodes with src found.");
                return;
            }
            //if the url is from nature.com, we need to extract the article nature id from the url https://www.nature.com/articles/s41422-025-01125-4
            var articleId = string.Empty;
            if (baseUrl.Contains("nature.com") && baseUrl.Contains("/articles/"))
            {
                var uri = new Uri(baseUrl);
                var segments = uri.Segments;
                if (segments.Length > 2)
                {
                    articleId = segments.Last().TrimEnd('/');
                    Console.WriteLine($"Extracted articleId: {articleId} from baseUrl: {baseUrl}");
                    if(KeyWordsByUrl.ContainsKey("nature.com"))
                    {
                        KeyWordsByUrl["nature.com"].Add(articleId);
                    }
                    else
                    {
                        KeyWordsByUrl.Add("nature.com", new List<string> { articleId });
                    }
                }
            }

            if (baseUrl.Contains("pubs.rsc.org") && baseUrl.Contains("/articlelanding/"))
            {
                var uri = new Uri(baseUrl);
                var segments = uri.Segments;
                if (segments.Length > 2)
                {
                    articleId = segments.Last().TrimEnd('/');
                    Console.WriteLine($"Extracted articlePubsRscId: {articleId} from baseUrl: {baseUrl}");
                    if (KeyWordsByUrl.ContainsKey("pubs.rsc.org"))
                    {
                        KeyWordsByUrl["pubs.rsc.org"].Add(articleId);
                    }
                    else
                    {
                        KeyWordsByUrl.Add("pubs.rsc.org", new List<string> { articleId });
                    }
                }
            }

            // https://pubs.acs.org/doi/10.1021/acs.jctc.4c00677
            if (baseUrl.Contains("pubs.acs.org"))
            {
                var uri = new Uri(baseUrl);
                var segments = uri.Segments;
                if (segments.Length > 2)
                {
                    // Extract the last segment after the last slash
                    var lastSegment = segments.Last().TrimEnd('/');
                    // Split by '.' and take the last part
                    articleId = lastSegment.Split('.').Last();
                    Console.WriteLine($"Extracted articlePubsAcsId: {articleId} from baseUrl: {baseUrl}");
                    if (KeyWordsByUrl.ContainsKey("pubs.acs.org"))
                    {
                        KeyWordsByUrl["pubs.acs.org"].Add(articleId);
                    }
                    else
                    {
                        KeyWordsByUrl.Add("pubs.acs.org", new List<string> { articleId });
                    }
                }
            }



            // Deduplicate images by src
            var distinctImgNodes = new List<HtmlNode>();
            var seenSrcAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Console.WriteLine("Starting deduplication of <img> nodes based on src attribute.");
            foreach (var imgNode in imgNodes)
            {
                var src = imgNode.GetAttributeValue("src", "").Trim();
                if (baseUrl.Contains("pubs.rsc.org"))
                {
                    src = imgNode.GetAttributeValue("data-original", "").Trim();
                }
                Console.WriteLine($"Found <img> node with src: '{src}'");

                if (!string.IsNullOrEmpty(src) && seenSrcAttributes.Add(src))
                {
                    Console.WriteLine($"Adding unique src: '{src}' to distinctImgNodes.");
                    distinctImgNodes.Add(imgNode);
                }
                else if (!string.IsNullOrEmpty(src))
                {
                    Console.WriteLine($"Duplicate src detected: '{src}', skipping.");
                }
                else
                {
                    Console.WriteLine("Empty src attribute found, skipping.");
                }
            }

            Console.WriteLine($"Deduplication complete. {distinctImgNodes.Count} unique <img> nodes found.");

            // Filter by keywords
            Console.WriteLine("Filtering <img> nodes by keywords.");
            var nodesToKeep = new List<string>();

            foreach (var imgNode in distinctImgNodes)
            {
                var src = imgNode.GetAttributeValue("src", "");
                if (baseUrl.Contains("pubs.rsc.org"))
                {
                    src = imgNode.GetAttributeValue("data-original", "");
                }
                Console.WriteLine($"Checking <img> node with src: '{src}' for keyword match.");

                if (KeyWordsByUrl.Any(kv => baseUrl.Contains(kv.Key) && kv.Value.Any(keyword => src.Contains(keyword))))
                {
                    Console.WriteLine($"Image src '{src}' contains one of the keywords. Keeping this node.");
                    nodesToKeep.Add(src);
                }
                else
                {
                    Console.WriteLine($"Image src '{src}' does not contain any of the keywords. Removing this node.");
                }
            }

            Console.WriteLine($"Keyword filtering complete. {nodesToKeep.Count} images kept.");

            Console.WriteLine("FILTERING FOR MDPI");

            //there are hidden duplicates in the nodesToKeep list, we need to remove them. example: ijms-24-06795-g009-550 and ijms-24-06795-g009

            if(baseUrl.Contains("mdpi.com") && nodesToKeep.Count > 0)
            {
                nodesToKeep = FilterFirstVersions(nodesToKeep);
            }

            Console.WriteLine("FILTERING FOR MDPI ENDED");

            if (nodesToKeep.Count == 0)
            {
                Console.WriteLine("No images remain after filtering. Exiting method.");
                return;
            }

            Console.WriteLine($"Found {nodesToKeep.Count} image nodes.");
            foreach (var imgNode in nodesToKeep)
            {
                Console.WriteLine($"Image src: {imgNode}");
            }

            // Save images
            var wwwrootPath = Path.Combine(webHostEnvironment.WebRootPath, "imagesTemp");
            Console.WriteLine($"Images will be saved to: {wwwrootPath}");
            Directory.CreateDirectory(wwwrootPath);

            foreach (var imgNode in nodesToKeep)
            {
                var src = imgNode;
                Console.WriteLine($"Processing image: {src}");

                try
                {
                    // Special handling for protocol-relative URLs (starting with //) for nature.com
                    if (src.StartsWith("//") && baseUrl.Contains("nature.com"))
                    {
                        src = "https:" + src;
                        Console.WriteLine($"Src starts with // and baseUrl contains nature.com. Updated src: {src}");
                    }
                    
                    if (!Uri.TryCreate(src, UriKind.Absolute, out var imageUri))
                    {
                        

                        Console.WriteLine($"Src is not an absolute URI: {src}. Attempting to combine with base URL: {baseUrl}");
                        if (!Uri.TryCreate(new Uri(baseUrl), src, out imageUri))
                        {
                            Console.WriteLine($"Failed to create URI from base URL and src: {src}");
                            continue;
                        }
                    }

                    Console.WriteLine($"Resolved image URI: {imageUri}");

                    using var request = new HttpRequestMessage(HttpMethod.Get, imageUri);

                    request.Headers.Add("Cookie", "__cf_bm=BnBKfaVkZfM53hSFbodjNyFnN.Ien4R6snspytRC914-1750251739-1.0.1.1-oYwkfCstgwJd9ZYEF0pm8CmUwLJdmPhFl0mKAGO_jeHr8bdxW7tZGRHq.hDNO4jCJBoUNUjPQRkgx1InlIvuUfF.uXi8oDYcjrOEmVTFEXI");

                    using var response = await httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(imageUri.AbsolutePath);
                    var fileExtension = Path.GetExtension(imageUri.AbsolutePath);
                    var fileName = $"{fileNameWithoutExtension}{fileExtension}";
                    var filePath = Path.Combine(wwwrootPath, fileName);

                    await using var fs = new FileStream(filePath, FileMode.Create);
                    await response.Content.CopyToAsync(fs);

                    Console.WriteLine($"Updated img src attribute to: /imagesTemp/{fileName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error downloading image {src}: {ex}");
                }
            }

            Console.WriteLine("ExtractAndSaveImages completed.");
        }


        public static List<string> FilterFirstVersions(List<string> imagePaths)
        {
            // Dictionary to track unique image identifiers
            var uniqueIdentifiers = new Dictionary<string, string>();
            var result = new List<string>();

            // Regex to extract the unique image identifier (e.g., "g001")
            var pattern = new Regex(@"(g\d{3,})", RegexOptions.Compiled);

            foreach (var path in imagePaths)
            {
                var match = pattern.Match(path);
                if (!match.Success) continue;

                var identifier = match.Groups[1].Value;  // e.g., "g001"

                // If this is the first time seeing this identifier, add to results
                if (!uniqueIdentifiers.ContainsKey(identifier))
                {
                    uniqueIdentifiers.Add(identifier, path);
                    result.Add(path);
                }
            }

            return result;
        }

        private static string GetFileExtension(string url)
        {
            var lastDot = url.LastIndexOf('.');
            if (lastDot == -1)
                return ".jpg";

            var extension = url.Substring(lastDot);
            if (extension.Length > 5)
                return ".jpg";

            return extension;
        }
    }
}
