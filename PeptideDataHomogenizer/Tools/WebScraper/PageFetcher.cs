//using PuppeteerExtraSharp;
//using Entities;
//using Microsoft.AspNetCore.Mvc;
//using System.Text;
//using iTextSharp.text.pdf;
//using iTextSharp.text;
//using iTextSharp.text.pdf.parser;
//using System.Net.Http;
//using System.Text.Json;
//using System.Net;
//using PuppeteerSharp;
//using PuppeteerExtraSharp.Plugins.ExtraStealth;
//using PeptideDataHomogenizer.Tools.ElsevierTools;
//using PeptideDataHomogenizer.Tools.WebScraper;
//using PeptideDataHomogenizer.Tools.WileyTools;
//using PeptideDataHomogenizer.Tools.HtmlTools;

//public interface IPageFetcher
//{
//    Task<List<Entities.Chapter>> GetFullPageContentAsync(string url, CancellationToken ct = default, string title = "");
//    void Dispose();
//}

//public class PageFetcher : IPageFetcher, IDisposable
//{
//    private const int MaxPages = 10;

//    private IBrowser _sharedBrowser;
//    private readonly SemaphoreSlim _pageSemaphore = new(MaxPages, MaxPages);
//    private bool _disposed;
//    private Task<IBrowser> _browserInitializationTask;
//    private SemaphoreSlim _browserInitializationSemaphore = new(1, 1);

//    private IElsevierArticleFetcher _elsevierArticleFetcher;
//    private ArticleExtractorFromHtml _articleExtractor;
//    private WileyArticleDownloader _wileyArticleDownloader;
//    private HttpClient _http;

//    public PageFetcher([FromServices] IElsevierArticleFetcher elsevierArticleFetcher, [FromServices] ArticleExtractorFromHtml articleExtractor, [FromServices] HttpClient http, [FromServices] WileyArticleDownloader wileyArticleDownloader)
//    {
//        Console.WriteLine("PageFetcher initialized.");
//        _elsevierArticleFetcher = elsevierArticleFetcher;
//        _articleExtractor = articleExtractor;
//        _wileyArticleDownloader = wileyArticleDownloader;
//        _http = http;
//        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
//    }

//    private async Task<IBrowser> InitializeSharedBrowserAsync()
//    {
//        if (_sharedBrowser != null) return _sharedBrowser;

//        await _browserInitializationSemaphore.WaitAsync();
//        try
//        {
//            if (_sharedBrowser == null)
//            {
//                if (_browserInitializationTask == null)
//                {
//                    _browserInitializationTask = Task.Run(async () =>
//                    {
//                        try
//                        {
//                            var extra = new PuppeteerExtra();
//                            extra.Use(new StealthPlugin());
//                            await new BrowserFetcher().DownloadAsync();
//                            var browser = await extra.LaunchAsync(new LaunchOptions
//                            {
//                                Headless = true,
//                                Args = new[]
//                                {
//                                    "--no-sandbox",
//                                    "--disable-setuid-sandbox",
//                                    "--disable-dev-shm-usage",
//                                    "--disable-accelerated-2d-canvas",
//                                    "--disable-gpu",
//                                    "--window-size=1080,720", 
//                                    "--disable-dev-shm-usage",
//                                    "--disable-features=site-per-process",
//                                    "--auto-open-devtools-for-tabs",
//                                    "--disable-notifications"
//                                },
//                                Timeout = 300000,
//                                Browser = SupportedBrowser.Chrome,
                                
                                
//                            });
//                            return browser;
//                        }
//                        catch (Exception ex)
//                        {
//                            Console.WriteLine($"Failed to initialize shared browser: {ex.Message}");
//                            throw;
//                        }
//                    });
//                }

//                _sharedBrowser = await _browserInitializationTask;
//            }
//        }
//        finally
//        {
//            _browserInitializationSemaphore.Release();
//        }

//        return _sharedBrowser;
//    }

//    public async Task<List<Entities.Chapter>> GetFullPageContentAsync(string url, CancellationToken ct = default, string title = "")
//    {
//        if (string.IsNullOrEmpty(url))
//            throw new ArgumentNullException(nameof(url));

//        _browserInitializationTask = InitializeSharedBrowserAsync();
//        if(_sharedBrowser==null)
//            _sharedBrowser = await _browserInitializationTask;
        

//        if (_disposed) throw new ObjectDisposedException(nameof(PageFetcher));

//        await _pageSemaphore.WaitAsync(ct);
        
//        var page = await _sharedBrowser.NewPageAsync();
//        var content = string.Empty;
//        var chapters = new List<Entities.Chapter>();

//        try
//        {
//            await page.SetUserAgentAsync(RandomUserAgentGenerator.GetRandomUserAgent());

//            await page.SetRequestInterceptionAsync(true);
//            page.Request += (sender, e) =>
//            {
//                if (e.Request.ResourceType == ResourceType.Image ||
//                    e.Request.ResourceType == ResourceType.EventSource ||
//                    e.Request.ResourceType == ResourceType.Font)
//                {
//                    e.Request.AbortAsync();
//                }
//                else
//                {
//                    e.Request.ContinueAsync();
//                }
//            };

//            await page.GoToAsync(url, new NavigationOptions
//            {
//                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
//                Timeout = 300000
//            });


//            await ScrollToBottomAsync(page);
//            await Task.Delay(1000);

//            if (page.Url.StartsWith("https://www.sciencedirect.com"))
//            {
//                chapters = await _elsevierArticleFetcher.GetFullTextByDoi(url.Replace("https://doi.org/", ""));
//                if (chapters == null || !chapters.Any())
//                {
//                    Console.WriteLine($"ELSEVIER. No chapters found for DOI: {url}");
//                }
//            }
//            else if (page.Url.StartsWith("https://onlinelibrary.wiley.com/"))
//            {
//                //chapters = await _wileyArticleDownloader.DownloadArticleAsync(url, title);
//                if (chapters == null || !chapters.Any())
//                {
//                    Console.WriteLine($"WILEY. No chapters found for DOI: {url}");
//                }
//            }
//            else
//            {
//                content = await page.GetContentAsync();

//                chapters = _articleExtractor.ExtractChapters(content, title);
//            }
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine($"Error fetching page content: {ex}");
//            throw;
//        }
//        finally
//        {
//            if (!page.IsClosed)
//                await page.CloseAsync();

//            _pageSemaphore.Release();
//        }

//        return chapters;
//    }

    



//    private static async Task ScrollToBottomAsync(IPage page)
//    {
//        // First try a single jump to bottom (works for many pages)
//        try
//        {
//            await page.EvaluateExpressionAsync(@"
//                window.scrollTo(0, document.body.scrollHeight);
//            ");
//            await Task.Delay(200); // Short wait after jump
//            return;
//        }
//        catch { /* Fall through to incremental scroll if needed */ }

//        // Only use incremental scroll if needed
//        await page.EvaluateExpressionAsync(@"
//            new Promise(resolve => {
//                const distance = 500; // Bigger chunks
//                const delay = 100;    // Reduced frequency
//                let lastPosition = 0;
//                let samePositionCount = 0;
                
//                const timer = setInterval(() => {
//                    const scrollHeight = document.body.scrollHeight;
//                    const currentPosition = window.scrollY;
                    
//                    // Stop if we're at bottom or stuck
//                    if (currentPosition + window.innerHeight >= scrollHeight || 
//                        samePositionCount > 3) {
//                        clearInterval(timer);
//                        resolve();
//                        return;
//                    }
                    
//                    // Track if we're stuck
//                    if (currentPosition === lastPosition) {
//                        samePositionCount++;
//                    } else {
//                        samePositionCount = 0;
//                    }
//                    lastPosition = currentPosition;
                    
//                    window.scrollBy(0, distance);
//                }, delay);
//            });
//        ");
//    }

//    private void OnProcessExit(object sender, EventArgs e)
//    {
//        Dispose();
//    }

//    public void Dispose()
//    {

//        if (_sharedBrowser != null)
//        {
//            _sharedBrowser = null;
//        }

//        Console.WriteLine("BROWSER disposed.");
//    }
//}
