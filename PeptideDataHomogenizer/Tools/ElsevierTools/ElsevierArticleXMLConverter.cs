using Entities;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Xml.Linq;

namespace PeptideDataHomogenizer.Tools.ElsevierTools
{
    public class ElsevierArticleXMLConverter
    {
        // Add a new property to the Section class to fix the error
        public class Section
        {
            public string Id { get; set; }
            public string Label { get; set; }
            public string Title { get; set; }
            public List<Paragraph> Paragraphs { get; } = new List<Paragraph>();
            public List<ListItem> ListItems { get; } = new List<ListItem>();

            // Add this property to allow nested sections
            public List<Section> Sections { get; } = new List<Section>();
        }

        public class Paragraph
        {
            public string Id { get; set; }
            public string Text { get; set; }
            public List<CrossReference> CrossReferences { get; } = new List<CrossReference>();
        }

        public class ListItem
        {
            public string Id { get; set; }
            public string Label { get; set; }
            public string Text { get; set; }
        }

        public class CrossReference
        {
            public string Id { get; set; }
            public string RefId { get; set; }
            public string Text { get; set; }
        }

        public List<Section> Sections { get; } = new List<Section>();
        public List<ExtractedTable> Tables { get; } = new List<ExtractedTable>();
        public List<ImageHolder> Images { get; } = new List<ImageHolder>();

        private static readonly XNamespace CeNs = "http://www.elsevier.com/xml/common/dtd";
        private static readonly XNamespace XocsNs = "http://www.elsevier.com/xml/xocs/dtd";
        private static readonly XNamespace JaNs = "http://www.elsevier.com/xml/ja/dtd";
        private static readonly XNamespace SvapiNS = "http://www.elsevier.com/xml/svapi/article/dtd";
        private static readonly XNamespace CalsNs = "http://www.elsevier.com/xml/common/cals/dtd";

        public static ElsevierArticleXMLConverter ParseArticleBody(string xmlContent)
        {
            var converter = new ElsevierArticleXMLConverter();

            try
            {
                var doc = XDocument.Parse(xmlContent);

                // Locate the 'full-text-retrieval-response' element using Descendants
                var fullTextRetrievalResponse = doc.Descendants(SvapiNS + "full-text-retrieval-response").First();

                Console.WriteLine(SvapiNS+ "full-text-retrieval-response");

                if (fullTextRetrievalResponse != null)
                {
                    // Locate the 'originalText' element using Descendants
                    var originalText = fullTextRetrievalResponse.Descendants(SvapiNS + "originalText").First();

                    if (originalText != null)
                    {
                        // Locate the 'doc' element using Descendants
                        var docElement = originalText.Descendants(XocsNs + "doc").First();

                        if (docElement != null)
                        {
                            // Locate the 'serial-item' element using Descendants
                            var serialItem = docElement.Descendants(XocsNs + "serial-item").First();

                            if (serialItem != null)
                            {
                                // Locate the 'article' element using Descendants
                                var article = serialItem.Descendants(JaNs + "article").First();

                                if (article != null)
                                {
                                    // Locate the 'body' element using Descendants
                                    var body = article.Descendants(JaNs + "body").First();

                                    if (body != null)
                                    {
                                        foreach (var ceSections in body.Descendants(CeNs + "sections"))
                                        {
                                            ProcessSections(ceSections, converter);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("No 'body' element found in the XML.");
                                    }
                                    ProcessTables(article, converter);
                                    ProcessImages(article, converter);
                                }
                                else
                                {
                                    Console.WriteLine("No 'article' element found in the XML.");
                                }
                            }
                            else
                            {
                                Console.WriteLine("No 'serial-item' element found in the XML.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("No 'doc' element found in the XML.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("No 'originalText' element found in the XML.");
                    }
                }
                else
                {
                    Console.WriteLine("No 'full-text-retrieval-response' element found in the XML.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing XML: {ex.Message}");
            }

            return converter;
        }

        private static void ProcessImages(XElement articleElement, ElsevierArticleXMLConverter converter)
        {
            var floats = articleElement.Descendants(CeNs + "floats").FirstOrDefault();
            if (floats == null) return;

            var figures = floats.Descendants(CeNs + "figure");
            foreach (var figure in figures)
            {
                var caption = figure.Element(CeNs + "caption")?.Value ?? string.Empty;
                var link = figure.Element(CeNs + "link");
                var href = link?.Attribute(XNamespace.Get("http://www.w3.org/1999/xlink") + "href")?.Value;

                if (!string.IsNullOrEmpty(href))
                {
                    // Extract pii and ref from href (format: "pii:S0045206825003694/gr1")
                    var parts = href.Split('/');
                    if (parts.Length == 2)
                    {
                        var pii = parts[0].Replace("pii:", "");
                        var refId = parts[1];
                        var imageUrl = $"https://api.elsevier.com/content/object/pii/{pii}/ref/{refId}/high";

                        converter.Images.Add(new ImageHolder
                        {
                            Caption = caption,
                            FileName = imageUrl // Store URL for later download
                        });
                    }
                }
            }
        }

        private static void ProcessTables(XElement articleElement, ElsevierArticleXMLConverter converter)
        {
            Console.WriteLine("Entering ProcessTables...");
            var floats = articleElement.Descendants(CeNs + "floats").FirstOrDefault();
            if (floats == null)
            {
                Console.WriteLine("No <ce:floats> element found.");
                return;
            }

            var tables = floats.Descendants(CeNs + "table").ToList();
            Console.WriteLine($"Found {tables.Count} <ce:table> elements.");

            foreach (var table in tables)
            {
                Console.WriteLine("Processing a <ce:table> element...");

                var captionElement = table.Element(CeNs + "caption");
                Console.WriteLine(captionElement != null ? "Found <ce:caption> in table." : "No <ce:caption> in table.");

                var newTable = new ExtractedTable
                {
                    Caption = captionElement != null ? ExtractCaption(captionElement) : "No caption"
                };

                // Process table content - note the different namespace for table elements

                var tgroup = table.Element(CalsNs + "tgroup");
                if (tgroup != null)
                {
                    Console.WriteLine("Found <tgroup> in table.");


                    // Get column specifications
                    var colspecs = tgroup.Elements(CalsNs + "colspec").ToList();
                    var colnames = colspecs.Select(c => c.Attribute("colname")?.Value).ToList();
                    Console.WriteLine($"Found {colnames.Count} columns: {string.Join(", ", colnames)}");

                    // Process headers
                    var headers = new List<string>();
                    var thead = tgroup.Element(CalsNs + "thead");
                    if (thead != null)
                    {
                        headers = thead.Descendants(CeNs + "entry")
                            .Select(e => ExtractEntryText(e))
                            .ToList();
                        Console.WriteLine($"Found table headers: {string.Join(", ", headers)}");
                    }
                    else
                    {
                        Console.WriteLine("No <thead> found in <tgroup>.");
                    }

                    // Process rows
                    // Process rows
                    var tbody = tgroup.Elements(CalsNs + "tbody");
                    if (tbody != null)
                    {
                        var rows = tbody.Elements(CalsNs + "row").ToList();
                        Console.WriteLine($"Found {rows.Count} <row> elements in <tbody>.");

                        foreach (var row in rows)
                        {
                            var rowDict = new Dictionary<string, string>();
                            var entries = row.Elements(CeNs + "entry").ToList();
                            Console.WriteLine($"Processing row with {entries.Count} entries.");

                            // Try first approach (using colname attributes)
                            if (entries.Any(e => e.Attribute("colname") != null))
                            {
                                Console.WriteLine("Processing entries using colname attributes");
                                // Existing colname-based approach
                                var headerMapping = new Dictionary<string, string>();
                                for (int i = 0; i < colnames.Count; i++)
                                {
                                    var header = i < headers.Count ? headers[i] : colnames[i];
                                    headerMapping[colnames[i]] = header;
                                }

                                foreach (var entry in entries)
                                {
                                    var colname = entry.Attribute("colname")?.Value;
                                    if (!string.IsNullOrEmpty(colname) && headerMapping.ContainsKey(colname))
                                    {
                                        var entryText = ExtractEntryText(entry);
                                        rowDict[headerMapping[colname]] = entryText;
                                        Console.WriteLine($"Entry: Column='{colname}', Header='{headerMapping[colname]}', Value='{entryText}'");
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("Processing entries using positional matching");
                                // Fall back to positional matching
                                for (int i = 0; i < entries.Count && i < colnames.Count; i++)
                                {
                                    var header = i < headers.Count ? headers[i] : colnames[i];
                                    var entryText = ExtractEntryText(entries[i]);
                                    rowDict[header] = entryText;
                                    Console.WriteLine($"Entry: Position={i}, Header='{header}', Value='{entryText}'");
                                }
                            }

                            // Ensure all columns are present in the row
                            for (int i = 0; i < colnames.Count; i++)
                            {
                                var header = i < headers.Count ? headers[i] : colnames[i];
                                if (!rowDict.ContainsKey(header))
                                {
                                    rowDict[header] = string.Empty;
                                }
                            }

                            newTable.Rows.Add(rowDict);
                        }
                    }
                    else
                    {
                        Console.WriteLine("No <tbody> found in <tgroup>.");
                    }
                }
                else
                {
                    Console.WriteLine("No <tgroup> found in table.");
                }

                converter.Tables.Add(newTable);
                Console.WriteLine("Added new ExtractedTable to converter.Tables.");
            }

            //pretty print resulting tables
            Console.WriteLine("Processed all tables. Total tables found: " + converter.Tables.Count);
            foreach (var table in converter.Tables)
            {
                Console.WriteLine($"Table Caption: {table.Caption}");
                Console.WriteLine("Rows:");
                foreach (var row in table.Rows)
                {
                    Console.WriteLine(string.Join(", ", row.Select(kv => $"{kv.Key}: {kv.Value}")));
                }
            }


            Console.WriteLine("Exiting ProcessTables.");
        }

        private static string ExtractCaption(XElement captionElement)
        {
            if (captionElement == null) return "No caption";

            // Handle simple-para and its content
            var simplePara = captionElement.Element(CeNs + "simple-para");
            if (simplePara != null)
            {
                return string.Concat(simplePara.Nodes()
                    .Select(n => n.NodeType == System.Xml.XmlNodeType.Text ?
                        ((XText)n).Value :
                        (n as XElement)?.Value ?? ""));
            }

            return captionElement.Value;
        }

        private static string ExtractEntryText(XElement entry)
        {
            if (entry == null) return string.Empty;

            // Handle entry content which might contain formatting elements
            return string.Concat(entry.Nodes()
                .Select(n =>
                {
                    if (n.NodeType == System.Xml.XmlNodeType.Text)
                        return ((XText)n).Value;

                    var element = n as XElement;
                    if (element != null)
                    {
                        // Handle specific elements
                        if (element.Name == CeNs + "italic" || element.Name.LocalName == "italic")
                            return $"({element.Value})";
                        if (element.Name == CeNs + "br" || element.Name.LocalName == "br")
                            return " ";
                    }
                    return string.Empty;
                })).Trim();
        }

        private static void ProcessSections(XElement ceSections, ElsevierArticleXMLConverter converter)
        {
            var sections = ceSections.Descendants(CeNs + "section");
            foreach (var section in sections)
            {
                if(section==null)
                {
                    Console.WriteLine("Section is null");
                    continue;
                }

                var label = section.Descendants(CeNs + "label")?.FirstOrDefault()?.Value;

                var title = section.Descendants(CeNs + "section-title")?.FirstOrDefault()?.Value;
                if (string.IsNullOrEmpty(title))
                {
                    Console.WriteLine("Title is null or empty");
                    continue;
                }
                var newSection = new Section
                {
                    Label = label,
                    Title = title
                };

                ProcessParagraphs(section, newSection);
                //ProcessLists(section, newSection);
                ProcessSubsections(section, newSection);

                converter.Sections.Add(newSection);
            }
        }

        private static void ProcessParagraphs(XElement parentElement, Section section)
        {
            var paragraphs = parentElement.Descendants(CeNs + "para");
            foreach (var para in paragraphs)
            {
                var paragraph = new Paragraph
                {
                    Id = para.Attribute("id")?.Value,
                    Text = ExtractTextWithRefs(para)
                };

                section.Paragraphs.Add(paragraph);
            }
        }

        private static string ExtractTextWithRefs(XElement element)
        {
            // Recursively extract text, including <ce:cross-ref> and <ce:inter-ref> as inline text
            return string.Concat(element.Nodes().Select(node =>
            {
                if (node.NodeType == System.Xml.XmlNodeType.Text)
                {
                    return ((XText)node).Value;
                }
                else if (node.NodeType == System.Xml.XmlNodeType.Element)
                {
                    var el = (XElement)node;
                    if (el.Name == CeNs + "cross-ref" || el.Name == CeNs + "inter-ref")
                    {
                        // Use the inner text of the reference (e.g., "1L2Y", "34", "Fig. 3")
                        return el.Value;
                    }
                    // Recursively process other elements
                    return ExtractTextWithRefs(el);
                }
                return string.Empty;
            })).Trim();
        }


        private static void ProcessLists(XElement sectionElement, Section section)
        {
            var lists = sectionElement.Descendants(CeNs + "list");
            foreach (var list in lists)
            {
                var items = list.Descendants(CeNs + "list-item");
                foreach (var item in items)
                {
                    section.ListItems.Add(new ListItem
                    {
                        Id = item.Attribute("id")?.Value,
                        Label = item.Element(CeNs + "label")?.Value,
                        Text = item.Element(CeNs + "para")?.Value
                    });
                }
            }
        }

        private static void ProcessSubsections(XElement sectionElement, Section parentSection)
        {
            // Handle nested sections recursively
            var subsections = sectionElement.Descendants(CeNs + "section");
            foreach (var subsection in subsections)
            {
                var newSubsection = new Section
                {
                    Id = subsection.Attribute("id")?.Value,
                    Label = subsection.Element(CeNs + "label")?.Value,
                    Title = subsection.Element(CeNs + "section-title")?.Value
                };

                ProcessParagraphs(subsection, newSubsection);
                ProcessLists(subsection, newSubsection);
                ProcessSubsections(subsection, newSubsection);

                parentSection.Sections.Add(newSubsection);
            }
        }
    }
}