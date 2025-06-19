using Entities;
using Entities.RegexData;
using Microsoft.AspNetCore.Mvc;
using PeptideDataHomogenizer.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PeptideDataHomogenizer.Tools.RegexExtractors
{


    // Register this class as Singleton in DI: services.AddSingleton<PDBRecordsExtractor>();
    public class PDBRecordsExtractor
    {
        private readonly DatabaseDataHandler _databaseDataHandler;
        private readonly LLMSimulationLengthExtractor _llmSimulationLengthExtractor;

        public PDBRecordsExtractor([FromServices] DatabaseDataHandler databaseDataHandler, [FromServices] LLMSimulationLengthExtractor llmSimulationLengthExtractor)
        {
            _databaseDataHandler = databaseDataHandler;
            _llmSimulationLengthExtractor = llmSimulationLengthExtractor;
        }

        public async Task<List<ProteinData>> ExtractMdData(string text, List<string> KnownSoftwareNames, List<string> ImplicitWaterNames, List<string> ExplicitWaterNames, List<string> KnownForceFields, List<string> KnownMethods, List<string> KnownIons)
        {
            var proteinDataList = new List<ProteinData>();


            // 1. Extract simulated structures (AlphaFold, RosettaFold)
            var simulatedMatches = Regex.Matches(text, @"\b(AlphaFold|RosettaFold)\b", RegexOptions.IgnoreCase);
            foreach (Match match in simulatedMatches)
            {
                proteinDataList.Add(new ProteinData
                {
                    ProteinId = match.Value,
                    Classification = "simulated"
                });
            }
            proteinDataList = proteinDataList
                .GroupBy(pd => pd.ProteinId.ToUpperInvariant())
                .Select(g => g.First())
                .ToList();

            // 2. Extract experimental structures (PDB IDs) sentence by sentence
            var requiredKeywords = new[]
            {
                    "protein data bank", "pdb", "protein entry",
                    "protein id", "accession code", "structure"
                };

            var pdbPattern = @"(?ix)  # Verbose mode for readability and ignore case
                # Pattern 1: Multiple IDs (must come first)
                \b(?:PDB\s+(?:ID\s+)?codes?|IDs?)\s+((?:[0-9][A-Z0-9]{3}\s*(?:,|and|&)?\s*)+)\b |

                # Pattern 2: Single ID formats (strict boundaries)
                (?<![A-Za-z0-9])(?:PDB\s+code|accession\s+code)\D*?([0-9][A-Z0-9]{3})(?![A-Za-z0-9])\b |
                \(PDB\s+([0-9][A-Z0-9]{3})(?![A-Za-z0-9])\) |
                (?<![A-Za-z0-9])(?:Protein\s+Data\s+Bank|PDB)[^a-zA-Z0-9]*?([0-9][A-Z0-9]{3})(?![A-Za-z0-9])\b |
                (?<![A-Za-z0-9])(?:PDB\s+entry|accession\s+code|ID)\D*?([0-9][A-Z0-9]{3})(?![A-Za-z0-9])\b |
                \b([0-9][A-Z0-9]{3})(?=\s*(?:\(PDB\)|from\s+PDB))(?![A-Za-z0-9]) |
                PDB\s+code\s*([0-9][A-Z0-9]{3})(?![A-Za-z0-9])\s*; |
                (?:Protein\s+Data\s+Bank\s+file|PDB\s+file)\s*([0-9][A-Z0-9]{3})(?![A-Za-z0-9])\b |

                # Fallback patterns (must not be part of a longer word)
                (?<![A-Za-z0-9])(?:PDB\s*(?:code)?|accession\s+code)\D*?([0-9][A-Z0-9]{3})(?![A-Za-z0-9])\b |
                \(PDB\s+([0-9][A-Z0-9]{3})(?![A-Za-z0-9])\) |
                (?<![A-Za-z0-9])\b([0-9][A-Z0-9]{3})\b(?=.*?(?:structure|coordinates))(?![A-Za-z0-9])";

            var foundPdbIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Split text into sentences for context extraction
            var sentences = Regex.Split(text, @"(?<=[\.!\?])\s+");

            foreach (var sentence in sentences)
            {
                // Check if sentence contains any required keyword
                bool containsRequiredKeyword = requiredKeywords.Any(kw =>
                    Regex.IsMatch(sentence, $@"\b{Regex.Escape(kw)}\b", RegexOptions.IgnoreCase));

                if (!containsRequiredKeyword)
                    continue;

                var pdbMatches = Regex.Matches(sentence, pdbPattern);

                foreach (Match match in pdbMatches)
                {
                    for (int i = 1; i < match.Groups.Count; i++)
                    {
                        if (match.Groups[i].Success)
                        {
                            var ids = match.Groups[i].Value
                                .Split(new[] { ',', ' ', ';', '&', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(id => id.Trim());

                            foreach (var pdbId in ids)
                            {
                                if (pdbId.Length == 4 &&
                                    char.IsDigit(pdbId[0]) &&
                                    pdbId.Skip(1).Any(c => char.IsLetter(c)))
                                {
                                    if (foundPdbIds.Add(pdbId.ToUpperInvariant()))
                                    {
                                        proteinDataList.Add(new ProteinData
                                        {
                                            ProteinId = pdbId.ToUpperInvariant(),
                                            Classification = "experimental",
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Find all software and water models in the text
            var softwareSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var softwareVersionDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var waterModelSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var forceFieldSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var simulationMethodSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var temperaturesSet = new HashSet<double>();
            var ionsAndConcentrationsSet = new HashSet<(string Ion, double Concentration)>();
            var durationTimesSet = new HashSet<int>();

            var seenSentences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sentence in sentences)
            {
                // Skip if this sentence has already been processed
                if (seenSentences.Contains(sentence.Trim()))
                    continue;
                seenSentences.Add(sentence.Trim());

                var software = FindSoftwareInSentence(sentence, KnownSoftwareNames);
                if (software != null && !string.IsNullOrWhiteSpace(software.SoftwareName))
                {
                    var name = software.SoftwareName;

                    // Try to extract version if present
                    var versionMatch = Regex.Match(name, @"([0-9]+(?:\.[0-9a-z]+)*)", RegexOptions.IgnoreCase);

                    if (versionMatch.Success)
                    {
                        if (name.Contains(versionMatch.Groups[1].Value))
                        {
                            name = name.Replace(versionMatch.Groups[1].Value, "").Trim();
                        }
                        softwareVersionDict[name] = versionMatch.Groups[1].Value;
                    }
                    else
                    {
                        softwareVersionDict[name] = null;
                    }

                    if (!softwareSet.Any(s => s.Contains(name)))
                        softwareSet.Add(name);
                }

                var waterModel = FindWaterModelInSentence(sentence, ExplicitWaterNames, ImplicitWaterNames);
                if (waterModel != null)
                {
                    var wmName = waterModel.WaterModelName ?? waterModel.WaterModelType;
                    if (!string.IsNullOrWhiteSpace(wmName))
                        waterModelSet.Add(wmName);
                }



                var forceFields = FindForceFieldInSentence(sentence, KnownForceFields);


                foreach (var ff in forceFields)
                {

                    if (!string.IsNullOrWhiteSpace(ff.SoftwareName) && !forceFieldSet.Any(m => m.Contains(ff.SoftwareName)))
                    {

                        forceFieldSet.Add(ff.SoftwareName);
                    }
                    else
                    {

                    }
                }

                var forceFieldList = forceFieldSet.ToList();
                var itemsToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < forceFieldList.Count; i++)
                {
                    if (itemsToRemove.Contains(forceFieldList[i])) continue;

                    for (int j = i + 1; j < forceFieldList.Count; j++)
                    {
                        if (itemsToRemove.Contains(forceFieldList[j])) continue;

                        if (forceFieldList[i].Contains(forceFieldList[j]))
                        {
                            itemsToRemove.Add(forceFieldList[j]);
                        }
                        else if (forceFieldList[j].Contains(forceFieldList[i]))
                        {
                            itemsToRemove.Add(forceFieldList[i]);
                            break;
                        }
                    }
                }

                foreach (var item in itemsToRemove)
                {
                    forceFieldSet.Remove(item);
                }



                var methods = FindMethodModelInSentence(sentence, KnownMethods);
                foreach (var method in methods)
                {
                    if (!string.IsNullOrWhiteSpace(method.MethodName) && !simulationMethodSet.Any(m => m.Contains(method.MethodName)))
                        simulationMethodSet.Add(method.MethodName);
                }

                var temperatures = FindTemperaturesInSentence(sentence);
                foreach (var temp in temperatures)
                {
                    if (temp > 0 && !temperaturesSet.Contains(temp))
                    {
                        temperaturesSet.Add(temp);
                    }
                }
                //sort the temperatures in ascending order
                temperaturesSet = temperaturesSet.OrderBy(t => t).ToHashSet();

                var ionsAndConcentrations = FindIonsAndConcentrationsAsync(sentence, KnownIons);
                foreach (var ion in ionsAndConcentrations)
                {
                    if (!string.IsNullOrWhiteSpace(ion.Item1) && ion.Item2 > 0)
                    {
                        ionsAndConcentrationsSet.Add((ion.Item1, ion.Item2));
                    }
                }

                var durations = await FindSimulationLengthAsync(sentence);
                foreach (var duration in durations)
                {
                    if (duration > 0 && !durationTimesSet.Contains(duration))
                    {
                        durationTimesSet.Add(duration);
                    }
                }


            }

            // If no software found, add a null/empty entry to allow for combinations
            if (softwareSet.Count == 0)
                softwareSet.Add(null);
            if (waterModelSet.Count == 0)
                waterModelSet.Add(null);
            if (forceFieldSet.Count == 0)
                forceFieldSet.Add(null);
            if (simulationMethodSet.Count == 0)
                simulationMethodSet.Add(null);
            if (temperaturesSet.Count == 0)
                temperaturesSet.Add(0.0); // Default temperature if none found
            if (ionsAndConcentrationsSet.Count == 0)
                ionsAndConcentrationsSet.Add(("", 0.0)); // Default ion if none found
            if (durationTimesSet.Count == 0)
                durationTimesSet.Add(0); // Default duration if none found


            // Build all combinations of ProteinId, Software, WaterModel
            var expandedList = new List<ProteinData>();
            foreach (var pd in proteinDataList)
            {
                foreach (var software in softwareSet)
                {
                    foreach (var waterModel in waterModelSet)
                    {
                        foreach (var forceField in forceFieldSet)
                        {
                            foreach (var method in simulationMethodSet)
                            {
                                foreach (var temperature in temperaturesSet)
                                {
                                    foreach (var ion in ionsAndConcentrationsSet)
                                    {
                                        foreach (var duration in durationTimesSet)
                                        {
                                            expandedList.Add(new ProteinData
                                            {
                                                ProteinId = pd.ProteinId,
                                                Classification = pd.Classification,
                                                SoftwareName = software,
                                                SoftwareVersion = software != null && softwareVersionDict.ContainsKey(software) ? softwareVersionDict[software] : null,
                                                WaterModel = waterModel,
                                                ForceField = forceField,
                                                SimulationMethod = method,
                                                Temperature = temperature,
                                                Ions = ion.Ion,
                                                IonConcentration = ion.Concentration,
                                                SimulationLength = duration,
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Return distinct list by ProteinId, Classification, SoftwareName, WaterModel
            var distinctList = expandedList
                .GroupBy(x => new
                {
                    x.ProteinId,
                    x.Classification,
                    SoftwareName = x.SoftwareName?.ToLower(),
                    WaterModel = x.WaterModel?.ToLower(),
                    ForceFieldSoftware = x.ForceField?.ToLower(),
                    SimulationMethod = x.SimulationMethod?.ToLower(),
                    x.Temperature,
                    x.Ions,
                    x.IonConcentration,
                    x.SimulationLength
                })
                .Select(g => g.First())
                .ToList();

            return distinctList;
        }

        private static readonly List<string> WaterKeywords = new List<string>
                {
                    "water", "solvent", "water model"
                };

        private static readonly List<string> ExplicitWaterKeywords = new List<string>
                {
                    "explicit solvent", "explicit water", "explicit solvent model",
                    "explicit water model", "explicit solvent content",
                    "Polarizable Explicit Solvent", "Fixed charge explicit content"
                };

        private static readonly List<string> ImplicitWaterKeywords = new List<string>
                {
                    "implicit water", "implicit solvent model", "implicit solvent content",
                    "implicit solvent"
                };

        private SimulationSoftware FindSoftwareInSentence(string sentence, List<string> KnownSoftware)
        {
            // First check if this sentence is about simulation at all
            if (!Regex.IsMatch(sentence, @"\bsimul", RegexOptions.IgnoreCase))
            {
                return null;
            }

            foreach (var software in KnownSoftware)
            {
                // Look for software name followed by optional parenthesized version or space+version
                var match = Regex.Match(sentence,
                    $@"\b{Regex.Escape(software)}\b(?:\((\d+(?:\.\d+)*)\)|[\s\-](\d+(?:\.\d+)*))?",
                    RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    string version = null;

                    // Check for parenthesized version (group 1)
                    if (match.Groups[1].Success)
                    {
                        version = match.Groups[1].Value;
                    }
                    // Check for space/hyphen separated version (group 2)
                    else if (match.Groups[2].Success)
                    {
                        version = match.Groups[2].Value;
                    }
                    else
                    {
                        // If no immediate version, look in nearby text (within 20 chars)
                        var remainingText = sentence.Substring(match.Index + match.Length);
                        var nearbyText = remainingText.Length > 20 ? remainingText.Substring(0, 20) : remainingText;

                        var versionMatch = Regex.Match(
                            nearbyText,
                            @"(?:version|v|ver|vers\.?|\(|\[|\,)\s*[:=]?\s*([0-9]+(?:\.[0-9a-z]+)*)\b",
                            RegexOptions.IgnoreCase
                        );

                        if (versionMatch.Success)
                        {
                            version = versionMatch.Groups[1].Value;
                        }
                    }

                    // Skip if version looks like a citation (digits in square brackets)
                    if (version != null && Regex.IsMatch(version, @"^\[\d+\]$"))
                    {
                        version = null;
                    }

                    var softwareName = software;
                    if (!string.IsNullOrEmpty(version))
                    {
                        softwareName += $" {version}";
                    }

                    return new SimulationSoftware
                    {
                        SoftwareName = softwareName
                    };
                }
            }

            return null;
        }

        private WaterModel FindWaterModelInSentence(string sentence, List<string> KnownExplicitWaterModels, List<string> KnownImplicitWaterModels)
        {
            // First check if the sentence contains any water-related keywords
            bool hasWaterContext = WaterKeywords.Any(keyword =>
                Regex.IsMatch(sentence, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase));

            if (!hasWaterContext)
            {
                return null;
            }

            // Check for explicit water models first, ordered by descending length
            foreach (var model in KnownExplicitWaterModels.OrderByDescending(m => m.Length))
            {
                var patternParts = new List<string> { Regex.Escape(model) };

                if (model.Contains(" "))
                {
                    patternParts.Add(Regex.Escape(model.Replace(" ", "-")));
                    patternParts.Add(model.Replace(" ", @"\s*"));
                }

                var pattern = $@"\b(?:{string.Join("|", patternParts)})\b";

                if (Regex.IsMatch(sentence, pattern, RegexOptions.IgnoreCase))
                {
                    var isGenericKeyword = ExplicitWaterKeywords.Any(x =>
                        string.Equals(x, model, StringComparison.OrdinalIgnoreCase));

                    return new WaterModel
                    {
                        WaterModelName = isGenericKeyword ? null : model,
                        WaterModelType = "explicit"
                    };
                }
            }

            // Check for implicit water models, ordered by descending length
            foreach (var model in KnownImplicitWaterModels.OrderByDescending(m => m.Length))
            {
                var patternParts = new List<string> { $@"\b{Regex.Escape(model)}\b" };

                if (model.Contains(" "))
                {
                    patternParts.Add(Regex.Escape(model.Replace(" ", "-")));
                    patternParts.Add(model.Replace(" ", @"\s*"));
                }

                var pattern = string.Join("|", patternParts);

                if (Regex.IsMatch(sentence, pattern, RegexOptions.IgnoreCase))
                {
                    var isGenericKeyword = ImplicitWaterKeywords.Any(x =>
                        string.Equals(x, model, StringComparison.OrdinalIgnoreCase));

                    return new WaterModel
                    {
                        WaterModelName = isGenericKeyword ? null : model,
                        WaterModelType = "implicit"
                    };
                }
            }

            return null;
        }

        private List<ForceFieldSoftware> FindForceFieldInSentence(string sentence, List<string> KnownForceFields)
        {
            sentence = System.Web.HttpUtility.HtmlDecode(sentence)
                       .Replace("&nbsp;", " ")
                       .Replace("  ", " ");

            if (!sentence.Contains("force field", StringComparison.OrdinalIgnoreCase) &&
                !sentence.Contains("force fields", StringComparison.OrdinalIgnoreCase))
            {
                return new List<ForceFieldSoftware>();
            }

            var results = new HashSet<ForceFieldSoftware>(new ForceFieldSoftwareComparer());

            foreach (var ff in KnownForceFields)
            {
                bool foundAny = false;

                // Pattern 1: Attached version (no space) - e.g., CHARMM36m
                var attachedPattern = $@"\b{Regex.Escape(ff)}[0-9][0-9A-Za-z\-]*";
                foreach (Match m in Regex.Matches(sentence, attachedPattern, RegexOptions.IgnoreCase))
                {
                    var softwareName = m.Value;
                    var versionPart = softwareName.Substring(ff.Length).TrimStart(' ', '-', '(').TrimEnd(')', ' ');
                    if (versionPart.Any(char.IsDigit))
                    {
                        results.Add(new ForceFieldSoftware { SoftwareName = softwareName });
                        foundAny = true;
                    }
                }

                // Pattern 2: Parentheses version - e.g., CHARMM(36m)
                var parenPattern = $@"\b{Regex.Escape(ff)}\s*\(([^)]+)\)";
                foreach (Match m in Regex.Matches(sentence, parenPattern, RegexOptions.IgnoreCase))
                {
                    if (m.Groups.Count >= 2 && m.Groups[1].Success)
                    {
                        var version = m.Groups[1].Value.Trim();
                        if (version.Any(char.IsDigit))
                        {
                            results.Add(new ForceFieldSoftware { SoftwareName = $"{ff} {version}" });
                            foundAny = true;
                        }
                    }
                }

                // Pattern 3: Space/dash separated version - e.g., CHARMM-36m
                var spacedPattern = $@"\b{Regex.Escape(ff)}[\s\-]+[0-9A-Za-z\-]+";
                foreach (Match m in Regex.Matches(sentence, spacedPattern, RegexOptions.IgnoreCase))
                {
                    var softwareName = m.Value;
                    var versionPart = softwareName.Substring(ff.Length).TrimStart(' ', '-', '(').TrimEnd(')', ' ');
                    if (versionPart.Any(char.IsDigit))
                    {
                        results.Add(new ForceFieldSoftware { SoftwareName = softwareName });
                        foundAny = true;
                    }
                }

                // Always add the name-only match if present, regardless of version
                if (Regex.IsMatch(sentence, $@"\b{Regex.Escape(ff)}\b", RegexOptions.IgnoreCase))
                {
                    results.Add(new ForceFieldSoftware { SoftwareName = ff });
                    foundAny = true;
                }
            }

            return results.ToList();
        }

        // Helper to avoid duplicates (remains the same)
        private class ForceFieldSoftwareComparer : IEqualityComparer<ForceFieldSoftware>
        {
            public bool Equals(ForceFieldSoftware x, ForceFieldSoftware y)
                => x?.SoftwareName.Equals(y?.SoftwareName, StringComparison.OrdinalIgnoreCase) ?? false;

            public int GetHashCode(ForceFieldSoftware obj)
                => obj.SoftwareName?.ToLower().GetHashCode() ?? 0;
        }

        private List<SimulationMethod> FindMethodModelInSentence(string sentence, List<string> knownMethods)
        {
            var foundMethods = new List<SimulationMethod>();

            if (string.IsNullOrEmpty(sentence) || knownMethods == null || !knownMethods.Any())
            {
                return foundMethods;
            }

            string lowerSentence = sentence.ToLower();

            foreach (var method in knownMethods)
            {
                if (string.IsNullOrEmpty(method))
                {
                    continue;
                }

                string lowerMethod = method.ToLower();
                int index = lowerSentence.IndexOf(lowerMethod);

                while (index != -1)
                {
                    // Check word boundaries
                    bool startOk = (index == 0) || !char.IsLetter(sentence[index - 1]);
                    bool endOk = (index + method.Length == sentence.Length) ||
                                 !char.IsLetter(sentence[index + method.Length]);

                    if (startOk && endOk)
                    {
                        foundMethods.Add(new SimulationMethod
                        {
                            MethodName = method // Using original case from knownMethods
                        });
                    }

                    index = lowerSentence.IndexOf(lowerMethod, index + 1);
                }
            }

            return foundMethods;
        }


        private List<double> FindTemperaturesInSentence(string sentence)
        {
            List<double> temperatures = new List<double>();
            if (sentence.Length < 6)
            {
                return temperatures;
            }

            // Updated regex pattern: requiring that after "K" no letter follows (so "kJ" won't count)
            var pattern = @"(?:^|[\s,;])(\d+\.\d+|\d+)(?=\s*K(?![A-Za-z]))";
            var matches = Regex.Matches(sentence, pattern, RegexOptions.IgnoreCase);

            // Check for figure reference patterns
            var figurePattern = @"Fig(?:ure)?\.?\s*\d+[a-zA-Z]?";

            foreach (Match match in matches)
            {
                // Get the position of the current match
                int position = match.Index;

                // Check if there's a figure reference before this number (within reasonable distance)
                string contextBefore = position >= 10 ? sentence.Substring(Math.Max(0, position - 10), Math.Min(10, position)) : sentence.Substring(0, position);

                // Skip this match if it appears to be part of a figure reference
                if (Regex.IsMatch(contextBefore, figurePattern, RegexOptions.IgnoreCase))
                {
                    continue;
                }

                if (double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double temp))
                {
                    temperatures.Add(temp);
                }
                else
                {

                }
            }

            return temperatures;
        }

        /// <summary>
        /// Finds known ions and their concentrations in a sentence, converting all concentration values to mM (millimolar)
        /// </summary>
        /// <param name="sentence">The text to search</param>
        /// <returns>A list of tuples containing ion name and concentration in mM</returns>
        //public List<(string, double)> FindIonsAndConcentrationsAsync(string sentence, List<string> knownIons)
        //{
        //    var result = new List<(string, double)>();

        //    // Find all ions in the sentence
        //    var ionMatches = new List<(string Ion, int Position, int Length)>();
        //    foreach (var ion in knownIons)
        //    {
        //        var pattern = $@"\b{Regex.Escape(ion)}\b";
        //        foreach (Match match in Regex.Matches(sentence, pattern, RegexOptions.IgnoreCase))
        //        {
        //            ionMatches.Add((ion, match.Index, match.Length));
        //        }
        //    }

        //    if (!ionMatches.Any())
        //        return result;

        //    // Find all concentration values with units
        //    var concentrationPattern = @"(\d+\.?\d*)\s*(µ|u|m|n|p)?(M)(?!\w)";
        //    var concentrationMatches = new List<(double Value, string Unit, int Position)>();

        //    foreach (Match match in Regex.Matches(sentence, concentrationPattern, RegexOptions.IgnoreCase))
        //    {
        //        if (double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        //        {
        //            string unit = match.Groups[2].Success ? match.Groups[2].Value.ToLower() : "";

        //            // Convert to mM (millimolar) based on unit prefix
        //            double convertedValue = unit switch
        //            {
        //                "µ" or "u" => value / 1000,        // µM (micromolar) to mM (divide by 1000)
        //                "m" => value,                      // mM (millimolar) - keep as is
        //                "" => value * 1000,                // M (molar) to mM (multiply by 1000)
        //                "n" => value / 1000000,            // nM (nanomolar) to mM (divide by 1,000,000)
        //                "p" => value / 1000000000,         // pM (picomolar) to mM (divide by 1,000,000,000)
        //                _ => value                         // Default case
        //            };

        //            concentrationMatches.Add((convertedValue, unit + "M", match.Index));
        //        }
        //    }

        //    if (!concentrationMatches.Any())
        //        return result;

        //    // Associate each ion with the nearest concentration
        //    foreach (var ion in ionMatches)
        //    {
        //        // Find the nearest concentration for this ion
        //        var nearestConcentration = concentrationMatches
        //            .OrderBy(c => Math.Abs(c.Position - (ion.Position + ion.Length / 2)))
        //            .FirstOrDefault();

        //        // Add the ion and its concentration (in mM)
        //        result.Add((ion.Ion, nearestConcentration.Value));
        //    }

        //    return result;
        //}

        public List<(string, double)> FindIonsAndConcentrationsAsync(string sentence, List<string> knownIons)
        {
            var result = new List<(string, double)>();
            var processedIonPositions = new HashSet<int>();

            // Step 1: Extract and process parenthesized chunks
            var parenMatches = Regex.Matches(sentence, @"\((.*?)\)");
            foreach (Match m in parenMatches)
            {
                string inner = m.Groups[1].Value;
                var parenChunks = inner.Split(',').Select(c => c.Trim()).ToList();
                ProcessChunks(parenChunks, knownIons, result, processedIonPositions, sentence);
            }

            // Step 2: Process non-parenthesized segments
            string withoutParen = Regex.Replace(sentence, @"\(.*?\)", " ");
            var outerChunks = withoutParen.Split(',').Select(c => c.Trim()).ToList();
            ProcessChunks(outerChunks, knownIons, result, processedIonPositions, sentence);

            // Step 3: Fallback for unprocessed ions
            ProcessRemainingIons(sentence, knownIons, result, processedIonPositions);

            return result;
        }

        private void ProcessChunks(List<string> chunks, List<string> knownIons,
            List<(string, double)> result, HashSet<int> processedIonPositions, string fullSentence)
        {
            foreach (var chunk in chunks)
            {
                if (string.IsNullOrWhiteSpace(chunk)) continue;

                // Find concentrations first
                var concentrations = new List<(double Value, int Position)>();
                var concentrationPattern = @"(\d+\.?\d*)\s*(µ|u|m|n|p)?(M)(?!\w)";
                foreach (Match match in Regex.Matches(chunk, concentrationPattern, RegexOptions.IgnoreCase))
                {
                    if (double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    {
                        string unit = match.Groups[2].Success ? match.Groups[2].Value.ToLower() : "";
                        double convertedValue = unit switch
                        {
                            "µ" or "u" => value / 1000,        // µM → mM
                            "m" => value,                      // mM → mM
                            "" => value * 1000,                // M → mM
                            "n" => value / 1000000,            // nM → mM
                            "p" => value / 1000000000,         // pM → mM
                            _ => value
                        };
                        concentrations.Add((convertedValue, match.Index));
                    }
                }
                if (!concentrations.Any()) continue;

                // Find and process ions
                foreach (var ion in knownIons)
                {
                    var pattern = $@"\b{Regex.Escape(ion)}\b";
                    foreach (Match match in Regex.Matches(chunk, pattern, RegexOptions.IgnoreCase))
                    {
                        int absPosition = fullSentence.IndexOf(chunk) + match.Index;
                        if (processedIonPositions.Contains(absPosition)) continue;

                        int ionMid = match.Index + match.Length / 2;
                        var nearest = concentrations
                            .OrderBy(c => Math.Abs(c.Position - ionMid))
                            .First();

                        result.Add((ion, nearest.Value));
                        processedIonPositions.Add(absPosition);
                    }
                }
            }
        }

        private void ProcessRemainingIons(string sentence, List<string> knownIons,
            List<(string, double)> result, HashSet<int> processedIonPositions)
        {
            var concentrationPattern = @"(\d+\.?\d*)\s*(µ|u|m|n|p)?(M)(?!\w)";
            var concentrations = new List<(double Value, int Position)>();

            // Find all concentrations in sentence
            foreach (Match match in Regex.Matches(sentence, concentrationPattern, RegexOptions.IgnoreCase))
            {
                if (double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    string unit = match.Groups[2].Success ? match.Groups[2].Value.ToLower() : "";
                    double convertedValue = unit switch
                    {
                        "µ" or "u" => value / 1000,
                        "m" => value,
                        "" => value * 1000,
                        "n" => value / 1000000,
                        "p" => value / 1000000000,
                        _ => value
                    };
                    concentrations.Add((convertedValue, match.Index));
                }
            }
            if (!concentrations.Any()) return;

            // Process unmatched ions
            foreach (var ion in knownIons)
            {
                var pattern = $@"\b{Regex.Escape(ion)}\b";
                foreach (Match match in Regex.Matches(sentence, pattern, RegexOptions.IgnoreCase))
                {
                    if (processedIonPositions.Contains(match.Index)) continue;

                    int ionMid = match.Index + match.Length / 2;
                    var nearest = concentrations
                        .OrderBy(c => Math.Abs(c.Position - ionMid))
                        .First();

                    result.Add((ion, nearest.Value));
                    processedIonPositions.Add(match.Index);
                }
            }
        }

        public async Task<List<int>> FindSimulationLengthAsync(string sentence)
        {
            //if sentence contains " ns ", pass sentence to LLM extractor
            if ((sentence.Contains(" ns ", StringComparison.OrdinalIgnoreCase) ||
                sentence.Contains(" ns.", StringComparison.OrdinalIgnoreCase) ||
                sentence.Contains(" ns;", StringComparison.OrdinalIgnoreCase) ||
                sentence.Contains(" ns (", StringComparison.OrdinalIgnoreCase) ||
                sentence.Contains(" ns(", StringComparison.OrdinalIgnoreCase) ||
                sentence.Contains(" ns,", StringComparison.OrdinalIgnoreCase) &&
                (sentence.Contains("simulation", StringComparison.OrdinalIgnoreCase) ||
                sentence.Contains("simulations", StringComparison.OrdinalIgnoreCase))))
            {
                var durations = await _llmSimulationLengthExtractor.ExtractSimulationTimeAsync(sentence);

                foreach (var duration in durations)
                {

                }
                if (durations != null && durations.Count > 0)
                {
                    return durations.Select(d => int.TryParse(d, out int result) ? result : 0).ToList();
                }
                else
                {
                    return new List<int>();
                }
            }
            else
            {
                return new List<int>();
            }
        }
    }
}

