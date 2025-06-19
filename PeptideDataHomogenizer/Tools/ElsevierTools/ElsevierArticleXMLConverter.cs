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

        private static readonly XNamespace CeNs = "http://www.elsevier.com/xml/common/dtd";
        private static readonly XNamespace XocsNs = "http://www.elsevier.com/xml/xocs/dtd";
        private static readonly XNamespace JaNs = "http://www.elsevier.com/xml/ja/dtd";
        private static readonly XNamespace SvapiNS = "http://www.elsevier.com/xml/svapi/article/dtd";

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