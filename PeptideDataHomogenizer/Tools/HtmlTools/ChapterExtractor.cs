using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace PeptideDataHomogenizer.Tools.HtmlTools
{

    public static class ChapterExtractor
    {
        // Delegate for chapter header detection
        private delegate bool ChapterHeaderDetector(string line);

        // Map of PDF source identifiers to header detection methods
        private static readonly Dictionary<string, ChapterHeaderDetector> SourceDetectors = new()
            {
                { "wileyonlinelibrary.com", IsChapterHeaderInWiley },
                { "pubs.acs.org/", IsChapterHeaderInACS },
                { "default", DefaultChapterHeaderDetector }
            };

        public static string ExtractWithPdfPig(byte[] pdfBytes)
        {
            var sb = new StringBuilder();
            string? pdfSource = null;
            ChapterHeaderDetector? detector = null;

            using (var pdfStream = new MemoryStream(pdfBytes))
            using (var pdf = PdfDocument.Open(pdfStream))
            {
                // Read first page to detect source
                var firstPage = pdf.GetPages().FirstOrDefault();
                if (firstPage != null)
                {
                    var firstPageText = ContentOrderTextExtractor.GetText(firstPage);
                    pdfSource = DetectPdfSource(firstPageText);
                    if (pdfSource != null && SourceDetectors.TryGetValue(pdfSource, out var foundDetector))
                    {
                        detector = foundDetector;
                    }
                }

                // Default to a generic detector if none found
                detector ??= DefaultChapterHeaderDetector;

                foreach (var page in pdf.GetPages())
                {
                    var text = ContentOrderTextExtractor.GetText(page);

                    // Process text to detect structure
                    var lines = text.Split('\n');
                    foreach (var line in lines)
                    {
                        if (detector(line))
                        {
                            sb.AppendLine($"<h1>{line.Trim()}</h1>");
                        }
                        else
                        {
                            sb.AppendLine($"<p>{line.Trim()}</p>");
                        }
                    }
                }
            }
            return sb.ToString();
        }

        // Detects the PDF source by looking for known identifiers in the first page's text
        private static string? DetectPdfSource(string firstPageText)
        {
            foreach (var kvp in SourceDetectors)
            {
                if (kvp.Key != "default" && firstPageText.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        // Wiley chapter header detection
        private static bool IsChapterHeaderInWiley(string line)
        {
            string trimmedLine = line.Trim();
            
            if (string.IsNullOrEmpty(trimmedLine))
                return false;

            // Original format: "1 | I N T RO DU CT I O N"
            if (trimmedLine.Length > 5 && char.IsDigit(trimmedLine[0]) && trimmedLine.Contains('|') && trimmedLine.Any(char.IsUpper))
                return true;
                
            // Decimal section format: "4.7 | Statistical analysis"
            if (trimmedLine.Length > 5 && char.IsDigit(trimmedLine[0]) && trimmedLine.Contains('.') && 
                trimmedLine.Contains('|') && trimmedLine.Any(char.IsUpper))
                return true;
                
            // Special section headers like "AUTHOR CONTRIBUTIONS", "ACKNOWLEDGMENTS", etc.
            string[] specialSections = {
                "AUTHOR CONTRIBUTIONS", "ACKNOWLEDGMENTS", "ACKNOWLEDGEMENTS", "REFERENCES", 
                "DATA AVAILABILITY", "CONFLICT OF INTEREST STATEMENT", "ORCID", "FUNDING",
            };
            
            foreach (var section in specialSections)
            {
                // Check for exact match or section followed by statement/declaration
                if (trimmedLine.StartsWith(section, StringComparison.OrdinalIgnoreCase) || 
                    trimmedLine.Equals(section, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            
            // All uppercase with limited length could be a header
            if (trimmedLine.Length < 50 && trimmedLine.ToUpper() == trimmedLine && trimmedLine.Any(char.IsLetter))
                return true;
                
            // Format that looks like a reference section heading
            if (Regex.IsMatch(trimmedLine, @"^[0-9]+\.\s+[A-Z]"))
                return true;
                
            return false;
        }

        // ACS chapter header detection (1. INTRODUCTION)
        private static bool IsChapterHeaderInACS(string line)
        {
            // Example: 1. INTRODUCTION
            return line.Length > 5 && char.IsDigit(line[0]) && line[1] == '.' && line.Any(char.IsUpper);
        }

        // Default/generic chapter header detection
        private static bool DefaultChapterHeaderDetector(string line)
        {
            string trimmedLine = line.Trim();
            
            if (string.IsNullOrEmpty(trimmedLine))
                return false;
                
            // Common section headers in academic papers
            string[] commonSections = {
                "ABSTRACT", "INTRODUCTION", "METHODS", "RESULTS", "DISCUSSION",
                "CONCLUSION", "REFERENCES", "ACKNOWLEDGMENTS", "APPENDIX",
                "MATERIALS AND METHODS", "EXPERIMENTAL", "BACKGROUND", "LITERATURE REVIEW"
            };
            
            foreach (var section in commonSections)
            {
                if (trimmedLine.Equals(section, StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith(section + ":", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            
            // Numbered sections like "1. Introduction" or "2.3 Results"
            if (Regex.IsMatch(trimmedLine, @"^[0-9]+(\.[0-9]+)*\.?\s+[A-Z]"))
                return true;
                
            // Short all-caps lines might be headers
            if (trimmedLine.Length < 40 && trimmedLine.ToUpper() == trimmedLine && 
                trimmedLine.Any(char.IsLetter) && !trimmedLine.Contains("http"))
                return true;
                
            return false;
        }
    }
}
