namespace PeptideDataHomogenizer.Tools.HtmlTools
{
    using Entities;
    using HtmlAgilityPack;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    

    public static class HtmlTableExtractor
    {
        public static List<ExtractedTable> ExtractTablesFromHtml(string html)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var tables = htmlDoc.DocumentNode.SelectNodes("//table");
            if (tables == null || !tables.Any())
            {
                return new List<ExtractedTable>();
            }

            var result = new List<ExtractedTable>();

            foreach (var table in tables)
            {
                var extractedTable = new ExtractedTable();

                extractedTable.Caption = ExtractTableCaption(table);

                var rows = table.SelectNodes(".//tr");
                if (rows == null || !rows.Any()) continue;

                var headers = new List<string>();
                var headerRow = table.SelectSingleNode(".//tr[th]");

                if (headerRow != null)
                {
                    headers = headerRow.Elements("th")
                        .Select(th => th.InnerText.Trim())
                        .ToList();
                }
                else
                {
                    var firstDataRow = table.SelectSingleNode(".//tr[td]");
                    if (firstDataRow != null)
                    {
                        var colCount = firstDataRow.Elements("td").Count();
                        headers = Enumerable.Range(1, colCount).Select(i => $"Column {i}").ToList();
                    }
                }

                foreach (var row in rows)
                {
                    if (row == headerRow) continue;

                    var cells = row.Elements("td").ToList();
                    if (!cells.Any()) continue;

                    var rowData = new Dictionary<string, string>();

                    for (int i = 0; i < cells.Count; i++)
                    {
                        var header = i < headers.Count ? headers[i] : $"Column {i + 1}";
                        rowData[header] = cells[i].InnerText.Trim();
                    }

                    extractedTable.Rows.Add(rowData);
                }

                result.Add(extractedTable);
            }

            return result;
        }

        private static string ExtractTableCaption(HtmlNode table)
        {
            // 1. Check for direct <caption> element inside table
            var directCaption = table.SelectSingleNode(".//caption");
            if (directCaption != null)
            {
                return directCaption.InnerText.Trim();
            }

            // 2. Check for <figcaption> in parent <figure> element
            var figureParent = table.Ancestors("figure").FirstOrDefault();
            if (figureParent != null)
            {
                var figCaption = figureParent.SelectSingleNode(".//figcaption");
                if (figCaption != null)
                {
                    return figCaption.InnerText.Trim();
                }
            }

            // 3. Check for preceding sibling paragraph or heading with common patterns
            var prevSibling = table.PreviousSibling;
            while (prevSibling != null)
            {
                if (prevSibling.NodeType == HtmlNodeType.Element)
                {
                    // Check for common caption patterns in preceding elements
                    string text = prevSibling.InnerText.Trim();

                    // Skip empty elements
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        // Check for elements that often contain captions
                        if (prevSibling.Name == "p" ||
                            prevSibling.Name.StartsWith("h") && char.IsDigit(prevSibling.Name[1]))
                        {
                            return text;
                        }

                        // Check for elements with class names indicating captions
                        var classNames = prevSibling.GetAttributeValue("class", "").ToLower();
                        if (classNames.Contains("caption") ||
                            classNames.Contains("table-title") ||
                            classNames.Contains("tbl-caption"))
                        {
                            return text;
                        }
                    }
                }
                prevSibling = prevSibling.PreviousSibling;
            }

            // 4. Check for following sibling paragraph with common patterns
            var nextSibling = table.NextSibling;
            while (nextSibling != null)
            {
                if (nextSibling.NodeType == HtmlNodeType.Element)
                {
                    string text = nextSibling.InnerText.Trim();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        // Check for elements that might contain captions after table
                        if (nextSibling.Name == "p")
                        {
                            var classNames = nextSibling.GetAttributeValue("class", "").ToLower();
                            if (classNames.Contains("caption") ||
                                classNames.Contains("table-note") ||
                                classNames.Contains("source"))
                            {
                                return text;
                            }
                        }
                    }
                }
                nextSibling = nextSibling.NextSibling;
            }

            // 5. Check for aria-label or aria-describedby attributes
            var ariaLabel = table.GetAttributeValue("aria-label", null);
            if (!string.IsNullOrWhiteSpace(ariaLabel))
            {
                return ariaLabel.Trim();
            }

            var ariaDescribedBy = table.GetAttributeValue("aria-describedby", null);
            if (!string.IsNullOrWhiteSpace(ariaDescribedBy))
            {
                var describedByElement = table.OwnerDocument.GetElementbyId(ariaDescribedBy);
                if (describedByElement != null)
                {
                    return describedByElement.InnerText.Trim();
                }
            }

            // 6. Check for table summary attribute
            var summary = table.GetAttributeValue("summary", null);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                return summary.Trim();
            }

            // 7. Check for parent div with caption-related class
            var parentDiv = table.Ancestors("div").FirstOrDefault();
            if (parentDiv != null)
            {
                var divClass = parentDiv.GetAttributeValue("class", "").ToLower();
                if (divClass.Contains("table-container") || divClass.Contains("tbl-wrapper"))
                {
                    var potentialCaption = parentDiv.SelectSingleNode(".//*[contains(@class, 'caption')]");
                    if (potentialCaption != null)
                    {
                        return potentialCaption.InnerText.Trim();
                    }
                }
            }

            // 8. Return empty string if no caption found
            return string.Empty;
        }
    }
}
