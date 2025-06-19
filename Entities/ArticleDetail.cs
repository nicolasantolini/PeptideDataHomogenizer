using System.Xml.Linq;

namespace Entities
{

    public class SearchResults
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<string> ArticlesPubMedIds { get; set; }
    }



    // Supporting Classes
    public class AuthorInfo
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }

    public class JournalInfo
    {
        public string Title { get; set; }
        public string ISOAbbreviation { get; set; }
    }


    public class ArticleDetail
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Abstract { get; set; }
        public List<Chapter> FullText { get; set; } // Not directly available in XML; could be fetched separately
        public string DOI { get; set; }
        public string PMID { get; set; }
        public List<AuthorInfo> Authors { get; set; }
        public JournalInfo Journal { get; set; }
        public Dictionary<string, string> Links { get; set; }
        public List<ProteinData> ProteinRecords { get; set; } = new List<ProteinData>();
        public DateTime? PubDate { get; set; } // New property for publication date

        public static ArticleDetail FromXml(XElement articleXml)
        {
            // Helper to parse month (can be numeric or abbreviated)
            int ParseMonth(string month)
            {
                if (int.TryParse(month, out int m))
                    return m;
                try
                {
                    return DateTime.ParseExact(month, "MMM", System.Globalization.CultureInfo.InvariantCulture).Month;
                }
                catch
                {
                    return 1;
                }
            }

            // Extract PubDate from Journal > JournalIssue > PubDate
            DateTime? pubDate = null;
            var pubDateElement = articleXml.Descendants("Journal")
                .Descendants("JournalIssue")
                .Descendants("PubDate")
                .FirstOrDefault();

            if (pubDateElement != null)
            {
                var year = pubDateElement.Element("Year")?.Value;
                var month = pubDateElement.Element("Month")?.Value;
                var day = pubDateElement.Element("Day")?.Value;

                if (!string.IsNullOrEmpty(year))
                {
                    int y = int.Parse(year);
                    int m = !string.IsNullOrEmpty(month) ? ParseMonth(month) : 1;
                    int d = !string.IsNullOrEmpty(day) ? int.Parse(day) : 1;
                    try
                    {
                        pubDate = new DateTime(y, m, d);
                    }
                    catch
                    {
                        pubDate = null;
                    }
                }
            }

            var detail = new ArticleDetail
            {
                // Extract PMID/ID
                Id = articleXml.Descendants("PMID").FirstOrDefault()?.Value,
                PMID = articleXml.Descendants("PMID").FirstOrDefault()?.Value,

                // Title
                Title = articleXml.Descendants("ArticleTitle").FirstOrDefault()?.Value,

                // Abstract (combine all AbstractText nodes)
                Abstract = string.Join("\n", articleXml.Descendants("AbstractText")
                    .Select(x => x.Value)),

                // DOI
                DOI = articleXml.Descendants("ELocationID")
                    .FirstOrDefault(x => x.Attribute("EIdType")?.Value == "doi")?.Value,

                // Authors
                Authors = articleXml.Descendants("Author")
                    .Select(a => new AuthorInfo
                    {
                        FirstName = a.Element("ForeName")?.Value,
                        LastName = a.Element("LastName")?.Value
                    }).ToList(),

                // Journal Info
                Journal = new JournalInfo
                {
                    Title = articleXml.Descendants("Journal").Elements("Title").FirstOrDefault()?.Value,
                    ISOAbbreviation = articleXml.Descendants("ISOAbbreviation").FirstOrDefault()?.Value
                },

                // PubDate
                PubDate = pubDate
            };

            // Links (e.g., DOI URL, PubMed URL)
            detail.Links = new Dictionary<string, string>
            {
                { "DOI", $"https://doi.org/{detail.DOI}" },
                { "PubMed", $"https://pubmed.ncbi.nlm.nih.gov/{detail.PMID}" }
            };

            return detail;
        }

        public string AuthorsToString()
        {
            if (Authors == null || !Authors.Any())
                return string.Empty;
            return string.Join(", ", Authors.Select(a => $"{a.LastName} {a.FirstName}"));
        }
    }

}
