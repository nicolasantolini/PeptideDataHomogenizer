using System.Text.RegularExpressions;

namespace PeptideDataHomogenizer.Tools.HtmlTools
{
    public class PDBContextDataExtractor
    {

        public List<(string classification, string organism, string method)> ExtractInfoFromHtml(string html)
        {
            var results = new List<(string, string, string)>();

            // Extract Classification
            string classificationPattern = @"<li id=""header_classification"".*?<a.*?>(.*?)<\/a>";
            Match classificationMatch = Regex.Match(html, classificationPattern, RegexOptions.Singleline);
            string classification = classificationMatch.Success ? classificationMatch.Groups[1].Value.Trim() : "Not found";

            // Extract Organism(s)
            string organismPattern = @"<li id=""header_organism"".*?<a.*?>(.*?)<\/a>";
            Match organismMatch = Regex.Match(html, organismPattern, RegexOptions.Singleline);
            string organism = organismMatch.Success ? organismMatch.Groups[1].Value.Trim() : "Not found";

            // Extract SimulationMethod
            string methodPattern = @"<li id=""exp_header_0_method"".*?<strong>Method:&nbsp;<\/strong>(.*?)<\/li>";
            Match methodMatch = Regex.Match(html, methodPattern, RegexOptions.Singleline);
            string method = methodMatch.Success ? methodMatch.Groups[1].Value.Trim() : "Not found";

            results.Add((classification, organism, method));
            return results;
        }
    }
}
