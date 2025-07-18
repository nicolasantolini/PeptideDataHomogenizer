using Entities;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using PeptideDataHomogenizer.Tools.WebScraper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PeptideDataHomogenizer.Tools.HtmlTools
{
    public class ImagesExtractorFromHtml
    {
        private HttpClient HttpClient { get; }
        private IWebHostEnvironment WebHostEnvironment { get; }

        public static Dictionary<string, List<string>> KeyWordsByUrl { get; } = new Dictionary<string, List<string>>
        {
            { "mdpi.com", new List<string> { "/article_deploy/" } },
            { "academic.oup.com", new List<string> { "nar_gkad" } },
            { "tandfonline.com", new List<string> { "/asset/" } }
        };

        public ImagesExtractorFromHtml(
            [FromServices] HttpClient httpClient,
            [FromServices] IWebHostEnvironment webHostEnvironment)
        {
            HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            WebHostEnvironment = webHostEnvironment ?? throw new ArgumentNullException(nameof(webHostEnvironment));
            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            HttpClient.DefaultRequestHeaders.Clear();
            HttpClient.DefaultRequestHeaders.Add("User-Agent", RandomUserAgentGenerator.GetRandomUserAgent());
            HttpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            HttpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            HttpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            HttpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            HttpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<List<ImageHolder>> ExtractImagesAsync(HtmlNode node, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                Console.WriteLine("Base URL is null or whitespace. Exiting method.");
                return new List<ImageHolder>();
            }
            HttpClient.DefaultRequestHeaders.Referrer = new Uri(baseUrl);

            ExtractArticleIdForSpecialDomains(baseUrl);

            var imgNodes = GetImageNodes(node, baseUrl);
            if (imgNodes == null || !imgNodes.Any())
            {
                Console.WriteLine("No image nodes found.");
                return new List<ImageHolder>();
            }

            var imageInfos = new List<ImageInfo>();
            foreach (var imgNode in imgNodes)
            {
                var imageInfo = ProcessImageNode(imgNode, baseUrl);
                if (imageInfo != null)
                {
                    imageInfos.Add(imageInfo);
                }
            }

            // Deduplicate by source
            var distinctImages = imageInfos
                .GroupBy(info => info.Src, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            // Filter by keywords
            var filteredImages = FilterImagesByKeywords(distinctImages, baseUrl);

            // Special filtering for MDPI
            if (baseUrl.Contains("mdpi.com"))
            {
                var filteredSrcs = FilterFirstVersions(filteredImages.Select(i => i.Src).ToList());
                filteredImages = filteredImages.Where(i => filteredSrcs.Contains(i.Src)).ToList();
            }

            Console.WriteLine($"Found {filteredImages.Count} images to process.");
            return await ProcessAndSaveImagesAsync(filteredImages, baseUrl);
        }

        private void ExtractArticleIdForSpecialDomains(string baseUrl)
        {
            if (baseUrl.Contains("nature.com") && baseUrl.Contains("/articles/"))
            {
                UpdateKeywords("nature.com", GetLastUrlSegment(baseUrl));
            }
            else if (baseUrl.Contains("pubs.rsc.org") && baseUrl.Contains("/articlelanding/"))
            {
                UpdateKeywords("pubs.rsc.org", GetLastUrlSegment(baseUrl));
            }
            else if (baseUrl.Contains("pubs.acs.org"))
            {
                var lastSegment = GetLastUrlSegment(baseUrl);
                if (!string.IsNullOrEmpty(lastSegment))
                {
                    UpdateKeywords("pubs.acs.org", lastSegment.Split('.').Last());
                }
            }
        }

        private string GetLastUrlSegment(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Segments.LastOrDefault()?.TrimEnd('/');
            }
            catch
            {
                return null;
            }
        }

        private void UpdateKeywords(string domain, string articleId)
        {
            if (string.IsNullOrEmpty(articleId)) return;

            if (KeyWordsByUrl.TryGetValue(domain, out var keywords))
            {
                if (!keywords.Contains(articleId))
                {
                    keywords.Add(articleId);
                }
            }
            else
            {
                KeyWordsByUrl.Add(domain, new List<string> { articleId });
            }
        }

        private HtmlNodeCollection GetImageNodes(HtmlNode node, string baseUrl)
        {
            if (baseUrl.Contains("pubs.rsc.org"))
            {
                return node.SelectNodes("//img[@data-original]") ??
                       node.SelectNodes("//img[contains(@data-original, '.')]") ??
                       node.SelectNodes("//img");
            }
            return node.SelectNodes("//img[@src]") ??
                   node.SelectNodes("//img[contains(@src, '.')]") ??
                   node.SelectNodes("//img");
        }

        private ImageInfo ProcessImageNode(HtmlNode imgNode, string baseUrl)
        {
            var src = GetSourceAttribute(imgNode, baseUrl);
            if (string.IsNullOrEmpty(src))
            {
                Console.WriteLine("Image source is empty. Skipping.");
                return null;
            }

            // Only allow valid image extensions
            var validExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".tiff" };
            var uri = new Uri(src, UriKind.RelativeOrAbsolute);
            string path = uri.IsAbsoluteUri ? uri.AbsolutePath : src;
            string ext = Path.GetExtension(path).ToLowerInvariant();

            if (!validExtensions.Contains(ext))
            {
                Console.WriteLine($"Image source '{src}' does not have a valid image extension. Skipping.");
                return null;
            }

            return new ImageInfo
            {
                Src = src,
                Caption = ExtractCaption(imgNode, baseUrl)
            };
        }

        private string GetSourceAttribute(HtmlNode imgNode, string baseUrl)
        {
            if (baseUrl.Contains("pubs.rsc.org"))
            {
                return imgNode.GetAttributeValue("data-original", null) ??
                       imgNode.GetAttributeValue("src", null);
            }
            return imgNode.GetAttributeValue("src", null) ??
                   imgNode.GetAttributeValue("data-original", null);
        }

        private string ExtractCaption(HtmlNode imgNode, string baseUrl)
        {
            // MDPI special handling
            if (baseUrl.Contains("mdpi.com"))
            {
                var figWrapper = imgNode.SelectSingleNode(".//ancestor::div[contains(@class, 'html-fig-wrap')]");
                if (figWrapper != null)
                {
                    var captionNode = figWrapper.SelectSingleNode(".//div[contains(@class, 'html-fig_description')]");
                    if (captionNode != null)
                        return captionNode.InnerText.Trim();
                }
            }

            // Standard caption extraction
            var caption = FindFigCaption(imgNode);
            if (!string.IsNullOrEmpty(caption)) return caption;

            caption = FindAdjacentCaption(imgNode);
            if (!string.IsNullOrEmpty(caption)) return caption;

            caption = GetParentCaption(imgNode);
            if (!string.IsNullOrEmpty(caption)) return caption;

            return GetAltText(imgNode);
        }

        private string FindFigCaption(HtmlNode imgNode)
        {
            var figCaption = imgNode.SelectSingleNode(".//following-sibling::figcaption") ??
                             imgNode.SelectSingleNode(".//preceding-sibling::figcaption") ??
                             imgNode.ParentNode.SelectSingleNode(".//figcaption");

            return figCaption?.InnerText.Trim();
        }

        private string FindAdjacentCaption(HtmlNode imgNode)
        {
            var nextSibling = imgNode.NextSibling;
            while (nextSibling != null)
            {
                if (nextSibling.NodeType == HtmlNodeType.Element &&
                    nextSibling.Name == "p" &&
                    nextSibling.InnerText.Contains("Figure", StringComparison.OrdinalIgnoreCase))
                {
                    return nextSibling.InnerText.Trim();
                }
                nextSibling = nextSibling.NextSibling;
            }
            return null;
        }

        private string GetParentCaption(HtmlNode imgNode)
        {
            var figureParent = imgNode.Ancestors("figure").FirstOrDefault();
            return figureParent?.GetAttributeValue("title", null) ??
                   figureParent?.GetAttributeValue("aria-label", null);
        }

        private string GetAltText(HtmlNode imgNode) =>
            imgNode.GetAttributeValue("alt", "");

        private List<ImageInfo> FilterImagesByKeywords(List<ImageInfo> images, string baseUrl)
        {
            var domain = KeyWordsByUrl.Keys.FirstOrDefault(k => baseUrl.Contains(k));
            if (domain == null || !KeyWordsByUrl.TryGetValue(domain, out var keywords))
                return images;

            return images
                .Where(info => keywords.Any(kw => info.Src.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        public static List<string> FilterFirstVersions(List<string> imagePaths)
        {
            var uniqueIdentifiers = new Dictionary<string, string>();
            var pattern = new Regex(@"(g\d{3,})", RegexOptions.Compiled);

            foreach (var path in imagePaths)
            {
                var match = pattern.Match(path);
                if (!match.Success) continue;

                var identifier = match.Groups[1].Value;
                if (!uniqueIdentifiers.ContainsKey(identifier))
                {
                    uniqueIdentifiers.Add(identifier, path);
                }
            }
            return uniqueIdentifiers.Values.ToList();
        }

        private async Task<List<ImageHolder>> ProcessAndSaveImagesAsync(List<ImageInfo> imageInfos, string baseUrl)
        {
            var wwwrootPath = Path.Combine(WebHostEnvironment.WebRootPath, "imagesTemp");
            Directory.CreateDirectory(wwwrootPath);

            var results = new List<ImageHolder>();
            foreach (var imageInfo in imageInfos)
            {
                try
                {
                    var imageHolder = await DownloadAndSaveImageAsync(imageInfo, baseUrl, wwwrootPath);
                    if (imageHolder != null)
                    {
                        results.Add(imageHolder);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing image {imageInfo.Src}: {ex.Message}");
                }
            }
            return results;
        }

        private async Task<ImageHolder> DownloadAndSaveImageAsync(ImageInfo imageInfo, string baseUrl, string savePath)
        {
            var imageUrl = GetAbsoluteImageUrl(imageInfo.Src, baseUrl);
            if (string.IsNullOrEmpty(imageUrl)) return null;

            var response = await MakeRequestWithCookies(imageUrl);

            // Special handling for MDPI 403 responses
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                Console.WriteLine("Retrying after image result was forbidden 403");
                response = await MakeRequestWithCookies(imageUrl);
            }

            response.EnsureSuccessStatusCode();

            // Get the original filename from the URL
            var uri = new Uri(imageUrl);
            var originalFileName = Path.GetFileName(uri.LocalPath);

            // Sanitize the filename to remove any query parameters or fragments
            var cleanFileName = originalFileName.Split('?')[0].Split('#')[0];

            // Read image bytes directly from response
            var imageBytes = await response.Content.ReadAsByteArrayAsync();

            // Get content type from response headers or fallback to extension
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrEmpty(contentType))
            {
                var ext = Path.GetExtension(cleanFileName).ToLowerInvariant();
                contentType = ext switch
                {
                    ".png" => "image/png",
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    ".webp" => "image/webp",
                    ".svg" => "image/svg+xml",
                    ".tiff" => "image/tiff",
                    _ => "application/octet-stream"
                };
            }

            return new ImageHolder
            {
                Caption = imageInfo.Caption,
                ImageData = imageBytes,
                FileName = cleanFileName,
                ContentType = contentType
            };
        }

        public static async Task<HttpResponseMessage> MakeRequestWithCookies(string url)
        {
            // Create a handler to manage cookies for this specific request
            var handler = new HttpClientHandler
            {
                UseCookies = false // Disable automatic cookie handling
            };

            using (var client = new HttpClient(handler))
            {
                // Create the request message
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                // Add all cookies to the Cookie header
                var cookieContainer = new CookieContainer();

                // Add each cookie to the container
                cookieContainer.Add(new Uri(url), new Cookie("__cf_bm", "gebbyuqa8BybFm.mJEdrg7GcyU5HraohowzYMt02yik-1751465958-1.0.1.1-nSPld5erJNz4cVrTKYK5XmB0y5jXCpEpoBEgPwtSegvZiGY5PPZpYfpbPSgEsmuSlEE4lEIgGw3Lz8E5qDVMLRnhSkzSeUkQumn_3jtmmoc"));
                cookieContainer.Add(new Uri(url), new Cookie("_ga", "GA1.1.539943036.1751466452"));
                cookieContainer.Add(new Uri(url), new Cookie("_ga_Q72ZQB7GTR", "GS2.1.s1751466451$o1$g0$t1751466451$j60$l0$h0"));
                cookieContainer.Add(new Uri(url), new Cookie("_ga_XP5JV6H8Q6", "GS2.1.s1751466451$o1$g0$t1751466451$j60$l0$h0"));
                cookieContainer.Add(new Uri(url), new Cookie("ACSEnt", "747788_6105_1751466357985"));
                cookieContainer.Add(new Uri(url), new Cookie("ACSPubs2", "YnVzaW5lc3NOYW1lID0gIC0gYnVzaW5lc3NXZWJzaXRlID0gIC0gY2l0eSA9ICAtIGNvbnRpbmVudCA9ICAtIGNvdW50cnkgPSAgLSBjb3VudHJ5Q29kZSA9ICAtIGlwTmFtZSA9ICAtIGlwVHlwZSA9ICAtIGlzcCA9ICAtIGxhdCA9ICAtIGxvbiA9ICAtIG1lc3NhZ2UgPSBObyBBUEkgS2V5IGZvdW5kLCBwbGVhc2UgZ2V0IHlvdXIgQVBJIEtleSBhdCBodHRwczovL2V4dHJlbWUtaXAtbG9va3VwLmNvbSAtIG9yZyA9ICAtIHF1ZXJ5ID0gMTQ1LjEwOS45LjIyOSAtIHJlZ2lvbiA9ICAtIHN0YXR1cyA9IGZhaWwgLSB0aW1lem9uZSA9ICAtIHV0Y09mZnNldCA9ICAtIDsgcGF0aD0vOyBkb21haW49LmFjcy5vcmc="));
                cookieContainer.Add(new Uri(url), new Cookie("cf_clearance", ".zawtMwnBjqKvSmcrTgBhV2Tx_NrPIcmM27HzAQrBxw-1751466454-1.2.1.1-JWiOvi2uoO890QR7n.qZmF_PnPrHVt82oBW9Wrr3qeyBMr7Kc2N08_1SXUgO89cRo10XvmeebUC7OSs64HyMeY1JjRJ3Ct.CwaF903qfrCU0JzbP4gYmobAkDG8NuXUrIZqkUftkxtjdW0vAkqwunzXFh.ovewdf2ZTMa953qrvtempjDzTKqRlbc01mQQfLoaHMHmIpbq7OD_XFe6oWveeWsZnPlCicQx2d7FsvZDk"));
                cookieContainer.Add(new Uri(url), new Cookie("ELOQUA", "GUID=5A0F53CC860B4856AA3DA505FCEF3024"));
                cookieContainer.Add(new Uri(url), new Cookie("incap_ses_1686_2209364", "6V/xCJARWCzyJnrUYuBlF9ZBZWgAAAAADUCJONpcEIjhld5WSSvyzQ=="));
                cookieContainer.Add(new Uri(url), new Cookie("JSESSIONID", "E04595D9A7D2684E8EFC8D6D9477D429"));
                cookieContainer.Add(new Uri(url), new Cookie("MACHINE_LAST_SEEN", "2025-07-02T07:27:31.976-07:00"));
                cookieContainer.Add(new Uri(url), new Cookie("MAID", "KpNmCo6uTltrnCSUdcUSSA=="));
                cookieContainer.Add(new Uri(url), new Cookie("nlbi_2209364", "U7A2ZsozOCepSH8o/VWePAAAAACfrb6U10ujune1HlCeHGxF"));
                cookieContainer.Add(new Uri(url), new Cookie("osano_consentmanager", "z6Qyj9yfO7afn7v8nqckF88tt5etpYeLSKAl8OiBpJBO90uhLxEj-GlKAcsNLM8oc1nzBiNIr0WcbU6MNCU_YBL0WkdqniqhMHYyFTiJULVndW2WvskUuvA1hNBpi1cHeNZJ2oAG46A0Vd-PHUckCJ-njBleOY3F_dIX6EGW35HJy4fGMr3aHPTbdB-UXG2QKP9k-mLgMuFgOByVAWnR1ptez0V-1hgHcW7lbzlK7lkFZJZtI0qZjXJ4RJkDCkVeMsXbCVdGuS516yntUQpEowHdGBVOwpFOGeoAzTU8-uZw7DOQQIbWj3ct3pdtyTLa"));
                cookieContainer.Add(new Uri(url), new Cookie("osano_consentmanager_uuid", "a9df2a83-60b7-42ac-87f1-e790bc5e62e2"));
                cookieContainer.Add(new Uri(url), new Cookie("visid_incap_2209364", "hkywaZtaSpOvPa7UDbT4cNZBZWgAAAAAQUIPAAAAAADI9EWvrpimkNS1DN3DTgF9"));

                // Get the cookie header string
                string cookieHeader = cookieContainer.GetCookieHeader(new Uri(url));

                // Add the Cookie header to the request
                request.Headers.Add("Cookie", cookieHeader);

                // Make the request
                return await client.SendAsync(request);
            }
        }

        private bool FileExists(string directory, string filename)
        {
            return File.Exists(Path.Combine(directory, filename));
        }

        private string GetAbsoluteImageUrl(string src, string baseUrl)
        {
            try
            {
                // Special handling for MDPI image URLs
                if (baseUrl.Contains("mdpi.com"))
                {
                    // Original behavior - simple URI combination
                    return new Uri(new Uri(baseUrl), src).AbsoluteUri;
                }

                // Keep other special cases (like nature.com)
                if (src.StartsWith("//") && baseUrl.Contains("nature.com"))
                {
                    return "https:" + src;
                }

                return new Uri(new Uri(baseUrl), src).AbsoluteUri;
            }
            catch (UriFormatException)
            {
                Console.WriteLine($"Invalid URI: baseUrl={baseUrl}, src={src}");
                return null;
            }
        }

        private class ImageInfo
        {
            public string Src { get; set; }
            public string Caption { get; set; }
        }
    }

    
}