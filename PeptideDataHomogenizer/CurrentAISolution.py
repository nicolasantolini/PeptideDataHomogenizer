import ollama
from ollama import chat
import json
from typing import List, Dict
from flask import Flask, request, jsonify
import re
from nltk.tokenize import sent_tokenize
import logging
logging.basicConfig(level=logging.DEBUG)  # Add this at the top

from pydantic import BaseModel
from flask_cors import CORS

class ProteinRecord(BaseModel):
    id: str
    type: str
    software_name: str | None
    software_version: str | None
    water_model: str | None

class ProteinRecords(BaseModel):
    records: list[ProteinRecord] | None

class BestSoftwareMatch(BaseModel):
    softwareIndex: list[int]

class BestWaterModelMatch(BaseModel):
    waterModelIndex: list[int]

app = Flask(__name__)
CORS(app)

def filter_water_model_matches(all_water_model_matches):
    """Filters duplicate water model matches, keeping only the best candidate per water model type.
    
    Rules:
    1. If multiple entries have the same water_model_type:
        - Prefer the one with a water_model (if any exists).
        - If no water_model exists, keep the one with the smallest distance.
    2. If two entries have the same water_model_type AND water_model, keep the one with the smallest distance.
    """
    filtered_matches = {}
    
    for match in all_water_model_matches:
        model_type = match["water_model_type"].lower()  # Case-insensitive comparison
        current_model = match["water_model"]
        current_distance = match["distance"]
        
        # If water model type not seen yet, add it
        if model_type not in filtered_matches:
            filtered_matches[model_type] = match
            continue
        
        # Get existing match details
        existing_match = filtered_matches[model_type]
        existing_model = existing_match["water_model"]
        existing_distance = existing_match["distance"]
        
        # Case 1: Current match has a model, existing doesn't → Keep current
        if current_model and not existing_model:
            filtered_matches[model_type] = match
        
        # Case 2: Both have models → Keep the one with smaller distance
        elif current_model and existing_model:
            if current_distance < existing_distance:
                filtered_matches[model_type] = match
        
        # Case 3: Neither has a model → Keep the one with smaller distance
        elif not current_model and not existing_model:
            if current_distance < existing_distance:
                filtered_matches[model_type] = match
    
    return list(filtered_matches.values())

def filter_software_matches(all_software_matches):
    """Filters duplicate software matches, keeping only the best candidate per software name.
    
    Rules:
    1. If multiple entries have the same software name:
        - Prefer the one with a version (if any exists).
        - If no versions exist, keep the one with the smallest distance.
    2. If two entries have the same software name AND version, keep the one with the smallest distance.
    """
    filtered_matches = {}
    
    for match in all_software_matches:
        software_name = match["software"]["name"].lower()  # Case-insensitive comparison
        current_version = match["software"]["version"]
        current_distance = match["distance"]
        
        # If software not seen yet, add it
        if software_name not in filtered_matches:
            filtered_matches[software_name] = match
            continue
        
        # Get existing match details
        existing_match = filtered_matches[software_name]
        existing_version = existing_match["software"]["version"]
        existing_distance = existing_match["distance"]
        
        # Case 1: Current match has a version, existing doesn't → Keep current
        if current_version and not existing_version:
            filtered_matches[software_name] = match
        
        # Case 2: Both have versions → Keep the one with smaller distance
        elif current_version and existing_version:
            if current_distance < existing_distance:
                filtered_matches[software_name] = match
        
        # Case 3: Neither has a version → Keep the one with smaller distance
        elif not current_version and not existing_version:
            if current_distance < existing_distance:
                filtered_matches[software_name] = match
    
    return list(filtered_matches.values())

def find_software_in_sentence(sentence):
    """Find software name and version in a single sentence, returns dict with separated values"""
    known_software = [
        'Abalone', 'ADF', 'Ascalaph Designer', 'Avogadro', 'BOSS','CHARRM','Folding@home', 
        'CP2K', 'Desmond', 'Discovery Studio', 'Espresso', 'fold.it', 'FoldX', 'GROMACS','HOOMD-blue','Schroedinger','Schroedinger Suite',
        'LAMMPS', 'MAPS',
        'MDynaMix', 'MOE', 'ms2', 'OpenMM', 'Orac', 'NAMD', 'NWChem',
         'PLUMED',
        'SAMSON', 'Scigress', 'Spartan', 'TeraChem', 'TINKER', 'VASP', 'YASARA'
    ]
    
    #print(f"\n=== DEBUGGING find_software_in_sentence() ===")
    #print(f"Analyzing sentence: '{sentence}...'")
    
    for software in known_software:
        #print(f"\nChecking for software: {software}")
        match = re.search(rf'\b{re.escape(software)}\b', sentence, re.IGNORECASE)
        
        if match:
            print(f"✅ Found software match: '{software}' at position {match.start()}")
            remaining_text = sentence[match.end():]  # Text after software name
            print(f"Text after software name: '{remaining_text[:40]}...'" if len(remaining_text) > 40 else f"Text after software name: '{remaining_text}'")
            
            version_match = re.search(
                rf'(?:version|v|ver|vers\.?)\s*[:=]?\s*([0-9]+(?:\.[0-9a-z]+)*)\b',
                remaining_text,
                re.IGNORECASE
            )

            version = None  # Initialize version as None
            if version_match:
                version = version_match.group(1)
                print(f"✅ Found version: '{version}' at position {version_match.start()}")
                print(f"Returning: {{'name': '{software}', 'version': '{version}'}}")
            #else:
                #print("⚠️  No version found after software name")
                #print(f"Returning: {{'name': '{software}', 'version': None}}")

            return {
                "name": software,
                "version": version  # Use the variable we properly set above
            }
    
    #print("\n⚠️  No known software detected in this sentence")
    return None

def find_water_model_in_sentence(sentence):
    """Find water model in a single sentence, returns a tuple of (general_type, specific_model) 
    where general_type is 'explicit' or 'implicit' and specific_model is the actual model name if found"""
    known_implicit_water_models = [
        'Quantum Models', 
        'Coarse Grained Solvent', 'Nonlinear PB', 'Linear PB', 'PB/SASA', 'GB/SASA/VOL',
        'SASA', 'GB', 'VOL', 'Distance-dependent dielectric', 'Implicit water', 
        'Implicit solvent', 'imp water', 'Martini'
    ]

    known_explicit_water_models = [
        'TIPS', 'SPC', 'TIP3P', 'SPC/E', 'BF', 'TIPS2', 'TIP4P', 
        'TIP4P-Ew', 'TIP4P/Ice', 'TIP4P/2005', 'OPC', 'TIP4P-D',
        'Explicit water', 'Explicit solvent', 'esp water'
        
    ]
    
    water_keywords = ['water', 'solvent', 'water model']

    explicit_water_keywords = [
        'explicit solvent', 'explicit water', 'explicit solvent model',
        'explicit water model', 'explicit solvent content',
        'Polarizable Explicit Solvent', 'Fixed charge explicit content'
    ]

    implicit_water_keywords = [
        'implicit water', 'implicit solvent model', 'implicit solvent content', 
        'implicit solvent'
    ]
    
    # First check if the sentence contains any water-related keywords
    has_water_context = any(
        re.search(rf'\b{keyword}\b', sentence, re.IGNORECASE) 
        for keyword in water_keywords
    )
    
    if not has_water_context:
        return None
    
    # Initialize return values
    general_type = None
    specific_model = None
    
    # Check for explicit water models first
    for model in known_explicit_water_models:
        # Base pattern
        pattern_parts = [re.escape(model)]
        
        # Add variations for spaced models
        if ' ' in model:
            # Hyphenated version
            pattern_parts.append(re.escape(model.replace(" ", "-")))
            # Version with optional whitespace (using string concatenation)
            pattern_parts.append(model.replace(" ", r"\s*"))
        
        # Combine patterns with word boundaries
        pattern = r'\b(?:' + '|'.join(pattern_parts) + r')\b'
        
        match = re.search(pattern, sentence, re.IGNORECASE)
        if match:
            general_type = 'explicit'
            if model.lower() not in [x.lower() for x in explicit_water_keywords]:
                specific_model = model
            return (general_type, specific_model)
    
    # Check for implicit water models
    for model in known_implicit_water_models:
        # Base pattern
        pattern_parts = [rf'\b{re.escape(model)}\b']
        
        # Add variations for spaced models
        if ' ' in model:
            pattern_parts.append(re.escape(model.replace(" ", "-")))
            # Version with optional whitespace (using string concatenation)
            pattern_parts.append(model.replace(" ", r"\s*"))
        
        pattern = '|'.join(pattern_parts)
        
        match = re.search(pattern, sentence, re.IGNORECASE)
        if match:
            general_type = 'implicit'
            # Only set specific model if it's not a generic implicit keyword
            if model.lower() not in [x.lower() for x in implicit_water_keywords]:
                specific_model = model
            return (general_type, specific_model)
    
    # Check for generic explicit/implicit keywords if no specific model found
    for keyword in explicit_water_keywords:
        if re.search(rf'\b{re.escape(keyword)}\b', sentence, re.IGNORECASE):
            return ('explicit', None)
    
    for keyword in implicit_water_keywords:
        if re.search(rf'\b{re.escape(keyword)}\b', sentence, re.IGNORECASE):
            return ('implicit', None)
    
    return None

def extract_md_data(text: str) -> List[Dict]:
    # Split the text into sentences
    sentences = [s.strip() for s in re.split(r'(?<!\w\.\w.)(?<![A-Z][a-z]\.)(?<=\.|\?)\s', text) if s.strip()]
    
    # Protein Sources (Multiple Extraction)
    protein_data = []
    protein_sources = []
    
    # 1. Extract simulated structures (AlphaFold/RosettaFold)
    simulated_matches = re.findall(r'\b(AlphaFold|RosettaFold)\b', text, re.I)
    for tool in simulated_matches:
        # Find the sentence containing the tool
        sentence = next((s for s in sentences if tool in s), None)
        sentence_index = sentences.index(sentence) if sentence in sentences else -1
        
        protein_sources.append({
            "id": tool,
            "type": "simulated",
            "sentence": sentence,
            "sentence_before": (sentences[sentence_index - 1]) if sentence_index > 0 else None,
            "sentence_after": (sentences[sentence_index + 1]) if sentence_index < len(sentences) - 1 else None
        })
    
    # 2. Extract experimental structures with strict PDB validation
    pdb_pattern = r'''(?x)  # Verbose mode for readability
        # Pattern 1: Multiple IDs (must come first)
        \b(?:PDB\s+(?:ID\s+)?codes?|IDs?)\s+((?:\d[A-Z0-9]{3}\s*(?:,|and|&)?\s*)+)\b |
        
        # Pattern 2: Single ID formats
        (?<![A-Za-z])(?:PDB\s+code|accession\s+code)\D*?(\d[A-Z0-9]{3})\b |
        \(PDB\s+(\d[A-Z0-9]{3})\) |
        (?<![A-Za-z])(?:Protein\s+Data\s+Bank|PDB)[^a-zA-Z0-9]*?(\d[A-Z0-9]{3})\b |
        (?<![A-Za-z])(?:PDB\s+entry|accession\s+code|ID)\D*?(\d[A-Z0-9]{3})\b |
        \b(\d[A-Z0-9]{3})(?=\s*(?:\(PDB\)|from\s+PDB)) |
        PDB\s+code\s*(\d[A-Z0-9]{3})\s*; |
        (?:Protein\s+Data\s+Bank\s+file|PDB\s+file)\s*(\d[A-Z0-9]{3})\b |
        
        # Fallback patterns
        (?<![A-Za-z])(?:PDB\s*(?:code)?|accession\s+code)\D*?(\d[A-Z0-9]{3})\b |
        \(PDB\s+(\d[A-Z0-9]{3})\) |
        \b(\d[A-Z0-9]{3})\b(?=.*?(?:structure|coordinates))
    '''

    required_keywords = [
        "protein data bank", "pdb", "protein entry", 
        "protein id", "accession code", "structure"
    ]

    def is_valid_pdb_match(sentence, pdb_id):
        """Final validation - requires at least one keyword"""
        return any(
            keyword in sentence.lower() 
            for keyword in required_keywords
        )
    
    haveSoftwaresBeenFound = False
    haveWaterModelsBeenFound = False

    # Store all software matches with their context and distance
    all_software_matches = []
    all_water_model_matches = []

    # Process each sentence for PDB IDs
    for sentence_index, sentence in enumerate(sentences):
        pdb_ids_in_sentence = set()
        #print(f"\nProcessing sentence: '{sentence}...'")
        
        # Single pass through the sentence
        for match in re.finditer(pdb_pattern, sentence, re.IGNORECASE | re.VERBOSE):
            # Handle multiple IDs case (group 1)
            if match.group(1):
                ids = re.findall(r'\d[A-Z0-9]{3}', match.group(1))
                for pdb_id in ids:
                    pdb_id = pdb_id.upper()
                    if not any(c.isalpha() for c in pdb_id[1:]):
                        print(f"❌ Rejected {pdb_id} (no letters in last 3 chars)")
                        break
                    if is_valid_pdb_match(sentence, pdb_id):
                        print(f"✅ Validated {pdb_id} from multi-ID match")
                        pdb_ids_in_sentence.add(pdb_id)
                    else:
                        print(f"❌ Rejected {pdb_id} (missing keywords)")
                continue
            
            # Handle single ID cases (groups 2-10)
            for i in range(2, 11):
                if match.group(i):
                    pdb_id = match.group(i).upper()
                    if len(pdb_id) == 4 and pdb_id[0].isdigit() and pdb_id not in pdb_ids_in_sentence:
                        if not any(c.isalpha() for c in pdb_id[1:]):
                            print(f"❌ Rejected {pdb_id} (no letters in last 3 chars)")
                            break
                        if is_valid_pdb_match(sentence, pdb_id):
                            print(f"✅ Validated {pdb_id} from pattern group {i-1}")
                            pdb_ids_in_sentence.add(pdb_id)
                        else:
                            print(f"❌ Rejected {pdb_id} (missing keywords)")
                    break
        
        # Add entries for each PDB ID found in this sentence
        if pdb_ids_in_sentence:
            sentence_index = sentences.index(sentence)
            for pdb_id in pdb_ids_in_sentence:
                protein_entry = ({
                    "id": pdb_id,
                    "type": "experimental",
                    "sentence": sentence,
                    "sentence_before": (sentences[sentence_index - 1]) if sentence_index > 0 else None,
                    "sentence_after": (sentences[sentence_index + 1]) if sentence_index < len(sentences) - 1 else None,
                    "software_name": None,  # New separate field
                    "software_version": None,  # New separate field
                    "software_sentence": None,
                    "water_model": None,  # New separate field
                    "water_model_sentence": None
                })

                if(haveSoftwaresBeenFound is False and haveWaterModelsBeenFound is False):
                    max_distance = max(len(sentences) - sentence_index - 1, sentence_index)  # Maximum possible distance in either direction

                    for distance in range(1, max_distance + 1):
                        # Check sentence after
                        idx = sentence_index + distance
                        if idx < len(sentences):
                            software_info = find_software_in_sentence(sentences[idx])
                            water_model_info = find_water_model_in_sentence(sentences[idx])
                            if water_model_info:
                                context = get_context(sentences, idx)
                                all_water_model_matches.append({
                                    "water_model": water_model_info[1],
                                    "water_model_type": water_model_info[0],
                                    "context": context,
                                    "distance": distance,
                                    "direction": "after"
                                })
                            if software_info:
                                context = get_context(sentences, idx)
                                all_software_matches.append({
                                    "software": software_info,
                                    "context": context,
                                    "distance": distance,
                                    "direction": "after"
                                })
                        
                        # Check sentence before
                        idx = sentence_index - distance
                        if idx >= 0:
                            software_info = find_software_in_sentence(sentences[idx])
                            water_model_info = find_water_model_in_sentence(sentences[idx])
                            if water_model_info:
                                context = get_context(sentences, idx)
                                all_water_model_matches.append({
                                    "water_model": water_model_info[1],
                                    "water_model_type": water_model_info[0],
                                    "context": context,
                                    "distance": distance,
                                    "direction": "before"
                                })
                            if software_info:
                                context = get_context(sentences, idx)
                                all_software_matches.append({
                                    "software": software_info,
                                    "context": context,
                                    "distance": distance,
                                    "direction": "before"
                                })
                    
                    # Sort software matches by distance (closest first)
                    all_software_matches.sort(key=lambda x: x["distance"])
                    all_water_model_matches.sort(key=lambda x: x["distance"])

                    all_software_matches = filter_software_matches(all_software_matches)
                    all_water_model_matches = filter_water_model_matches(all_water_model_matches)

                    #print(all_water_model_matches)

                    # # Remove duplicates
                    # seen_software = set()
                    # seen_water_models = set()
                    # all_software_matches = [
                    #     match for match in all_software_matches
                    #     if match["software"]["name"] not in seen_software and not seen_software.add(match["software"]["name"])
                    # ]
                    # all_water_model_matches = [
                    #     match for match in all_water_model_matches
                    #     if match["water_model"] not in seen_water_models and not seen_water_models.add(match["water_model"])
                    # ]
                    # Set flags to indicate that software and water models have been found
                    haveSoftwaresBeenFound = True
                    haveWaterModelsBeenFound = True
                
                # Write all software matches to file with PDB ID and original sentence
                with open("software.txt", "a", encoding="utf-8") as f:
                    f.write(f"\nPDB ID: {pdb_id}\n")
                    f.write(f"Protein sentence: {sentence}\n")
                    f.write(f"Protein sentence position: {sentence_index}\n")
                    f.write("Software matches (ordered by distance):\n")
                    for match in all_software_matches:
                        software = match["software"]
                        f.write(f"- {software['name']} (version: {software['version']}), distance: {match['distance']} {match['direction']}\n")
                        f.write(f"  Context: {match['context']}\n\n")
                
                with open("watermodel.txt","a",encoding = "utf-8") as f:
                    f.write(f"\nPDB ID: {pdb_id}\n")
                    f.write(f"Protein sentence: {sentence}\n")
                    f.write(f"Protein sentence position: {sentence_index}\n")
                    f.write("Water model matches (ordered by distance):\n")
                    for match in all_water_model_matches:
                        water_model = match["water_model"]
                        water_model_type = match["water_model_type"]
                        if water_model is not None:
                            f.write(f"- {water_model if water_model else 'unspecified'}, {water_model_type}, distance: {match['distance']} {match['direction']}\n")
                        else:
                            f.write(f"- {water_model_type} water model, distance: {match['distance']} {match['direction']}\n")
                        f.write(f"  Context: {match['context']}\n\n")
                
                # SOFTWARE AI CHECK 
                softwarePrompt = f"\nPDB ID: {pdb_id}\n"
                softwarePrompt += (f"Protein sentence: {sentence}\n")
                softwarePrompt += (f"Protein sentence position: {sentence_index}\n")
                softwarePrompt += (f"Number of software matches: {len(all_software_matches)}")
                softwarePrompt += ("Software matches (ordered by distance):\n")
                for match in all_software_matches:
                    software = match["software"]
                    softwarePrompt += (f"- {software['name']} (version: {software['version']}), distance: {match['distance']} {match['direction']}\n")
                    softwarePrompt += f"  Context: {match['context']}\n\n"
                if len(all_software_matches) != 0:
                    response = chat(
                        messages = [
                            {
                            'role':'user',
                            'content': softwarePrompt
                            }
                        ],
                        model = "SmartSoftwareFinderModel",
                        format = BestSoftwareMatch.model_json_schema()
                    )
                    bestSoftwareMatchIndex = BestSoftwareMatch.model_validate_json(response.message.content)
                    print(bestSoftwareMatchIndex)
                else:
                    print("no softwares found")

                #WATER MODEL AI CHECK
                waterPrompt = f"\nPDB ID: {pdb_id}\n"
                waterPrompt += (f"Protein sentence: {sentence}\n")
                waterPrompt += (f"Protein sentence position: {sentence_index}\n")
                waterPrompt += ("Water model matches (ordered by distance):\n")
                for match in all_water_model_matches:
                    water_model = match["water_model"]
                    water_model_type = match["water_model_type"]
                    if water_model is not None:
                        waterPrompt += (f"- {water_model if water_model else 'unspecified'} {water_model_type}, distance: {match['distance']} {match['direction']}\n")
                    else:
                        waterPrompt += (f"- {water_model_type} water model, distance: {match['distance']} {match['direction']}\n")
                    waterPrompt += f"  Context: {match['context']}\n\n"

                if(len(all_water_model_matches) != 0):
                    response = chat(
                        messages = [
                            {
                            'role':'user',
                            'content': waterPrompt
                            }
                        ],
                        model = "SmartWaterModelFinderModel",
                        format = BestWaterModelMatch.model_json_schema()
                    )
                    bestWaterModelMatchIndex = BestWaterModelMatch.model_validate_json(response.message.content)
                    print(bestWaterModelMatchIndex)
                else:
                    print(" no water models found")

                if len(all_software_matches) != 0:
                    # Create a copy of the original protein entry to preserve other data
                    original_protein_entry = protein_entry.copy()
                    
                    for software_idx in bestSoftwareMatchIndex.softwareIndex:
                        # Create a new entry for each software
                        software_entry = original_protein_entry.copy()
                        software_entry["software_name"] = all_software_matches[software_idx-1]["software"]["name"]
                        software_entry["software_version"] = all_software_matches[software_idx-1]["software"]["version"]
                        
                        if len(all_water_model_matches) != 0:
                            for water_model_idx in bestWaterModelMatchIndex.waterModelIndex:
                                # Create a new entry for each water model combination
                                water_model_entry = software_entry.copy()
                                water_model_entry["water_model"] = all_water_model_matches[water_model_idx-1]["water_model"]
                                water_model_entry["water_model_type"] = all_water_model_matches[water_model_idx-1]["water_model_type"]
                                
                                # Check for duplicates before adding
                                is_duplicate = any(
                                    existing_entry["id"] == water_model_entry["id"] and
                                    existing_entry["type"] == water_model_entry["type"] and
                                    existing_entry["software_name"] == water_model_entry["software_name"] and
                                    existing_entry["software_version"] == water_model_entry["software_version"] and
                                    existing_entry["water_model"] == water_model_entry["water_model"] and
                                    existing_entry["water_model_type"] == water_model_entry["water_model_type"]
                                    for existing_entry in protein_data
                                )

                                if not is_duplicate:
                                    protein_data.append(water_model_entry)
                        else:
                            # If no water models, just add the software entry
                            is_duplicate = any(
                                existing_entry["id"] == software_entry["id"] and
                                existing_entry["type"] == software_entry["type"] and
                                existing_entry["software_name"] == software_entry["software_name"] and
                                existing_entry["software_version"] == software_entry["software_version"] and
                                existing_entry.get("water_model", None) == software_entry.get("water_model", None) and
                                existing_entry.get("water_model_type", None) == water_model_entry.get("water_model_type",None)
                                for existing_entry in protein_data
                            )

                            if not is_duplicate:
                                protein_data.append(software_entry)

    print(protein_data)
    return protein_data
  

def get_context(sentences, sentence_index):
    """
    Get context window for a sentence (target + 2 before/after)
    Handles edge cases by reducing window size when near boundaries
    """
    context = sentences[sentence_index]  # Always include target sentence
    
    # Add preceding sentences (up to 2)
    for i in range(1, 3):
        prev_idx = sentence_index - i
        if prev_idx >= 0:
            context = sentences[prev_idx] + " " + context
    
    # Add following sentences (up to 2)
    for i in range(1, 3):
        next_idx = sentence_index + i
        if next_idx < len(sentences):
            context = context + " " + sentences[next_idx]
    
    return context.strip()

def pretty_print_protein_data(protein_data: List[Dict]):
    if(not protein_data):
        print("No protein data found.")
        print("-" * 40)
        return
    if(len(protein_data) == 0):
        print("No protein data found.")
        print("-" * 40)
        return
    i = 1
    for data in protein_data:
        print(f"Protein Source found number {i}: {data['protein_source']['id']}")
        print(f"Protein Sentence: {data['protein_source']['sentence']}")
        print(f"Protein Sentence Before: {data['protein_source']['sentence_before']}")
        print(f"Protein Sentence After: {data['protein_source']['sentence_after']}")
        print(f"Software: {data['software']}")
        print(f"Software Sentence: {data['software_sentence']}")
        print(f"Software Sentence Before: {data['software_sentence_before']}")
        print(f"Software Sentence After: {data['software_sentence_after']}")
        print(f"Water Model: {data['water_model']}")
        print(f"Water Model Sentence: {data['water_model_sentence']}")
        print(f"Water Model Sentence Before: {data['water_model_sentence_before']}")
        print(f"Water Model Sentence After: {data['water_model_sentence_after']}")
        print("-" * 40)
        i+=1


@app.route('/extract_md_data', methods=['POST'])
def extract_pdb_data_endpoint():
    data = request.json
    if not data or 'text' not in data:
        return jsonify({"error": "Invalid input. Please provide 'text' in the request body."}), 400
    
    #set content type to json
    content_type = request.headers.get('Content-Type')
    if content_type == 'application/json':
        # Process the JSON data
        pass
    else:
        return jsonify({"error": "Invalid content type. Please use 'application/json'."}), 400
    

    text = data['text']

    proteins = processText(text)
    
    return jsonify({
            "status": "success",
            "data": proteins
        })


def processText (text):
    prompt = """
    **ROLE:** Strict Protein Association Validator

    **RULES:**
    1. Input: List of protein records + context.
    2. Output: EXACT same records UNLESS:
    - Context EXPLICITLY states a mismatch (e.g., "1ABC used X" but record says Y).
    - Context denies usage (e.g., "no water model was used for 1DEF").
    3. Prohibited Actions:
    - Never infer/impute missing data.
    - Never modify 'type' or add fields.
    - Never omit records (even if context seems irrelevant).

    **DATA TO VALIDATE:**
    """

    result = extract_md_data(text)
    finalProteins = []
    for i in result:
        finalProtein = {
            "id": i["id"],
            "type": i["type"],
            "software_name": i["software_name"],
            "software_version": i["software_version"],
            "water_model": i["water_model"]
        }
        finalProteins.append(finalProtein)
    # resultJson = json.dumps(result)
    # prompt = prompt + resultJson
    # #WRITE PROMPT TO TEXT.TXT FILE
    # with open("text.txt", "a", encoding="utf-8") as f:
    #     f.write(prompt)
    # response = chat(
    #     messages = [
    #         {
    #         'role':'user',
    #         'content': prompt
    #         }
    #     ],
    #     model = "FirstTestModel",
    #     format = ProteinRecords.model_json_schema()
    # )
    # proteins = ProteinRecords.model_validate_json(response.message.content)
    print(finalProteins)
    return finalProteins

# Test with your text
if __name__ == "__main__":
    print("Flask starting...")  
    app.run(debug=True, host='0.0.0.0', port=5034)

    text = """Structural coordinates of PrP native were obtained from protein data bank file 1QLX. Structural coordinates of PrP intermediate were retrieved from previous report [8]. Classical molecular dynamics (MD) simulations were performed using Gromacs 2018 [34]. Protein topologies were generated with Amber14sb; ligands topologies were generated with AM1-BCC. Proteins were placed in dodecahedral box with periodic boundary conditions (with 1.2 nm minimum distance from the wall). The system was solvated with TIP3P water, the protein charge was neutralized with Na+ or Cl− counterions, and the concentration of NaCl was brought to 150 mM. Energy minimization was performed with the steepest descent algorithm. The system was then equilibrated for 500 ps in NVT using V-rescale algorithm. The NPT equilibration was performed using the V-rescale thermostat (310K) and the Parrinello−Rahman Barostat (reference pressure = 1 bar). Equilibration was performed with position restraints on heavy atoms. Equilibration was performed with position restraints on heavy atoms, trajectories were then generated releasing position restraints. This procedure was performed for each protein for 500 ns, leading to 2 us cumulative simulation time. Equilibration and productions were carried out using the leapfrog algorithm, with an integration step of 2 fs. The short-range cutoff for Coulomb interactions was set at 1.0 nm, employing particle mesh Ewald to treat long-range electrostatics. The cutoff for van der Waals interactions was set at 1.0 nm."""
    text1 = """The NMR structure of human prion protein (huPrP) was obtained from the Protein Data Bank (PDB) and the PDB entry was 1HJM, as shown in (Calzolai & Zahn, Citation2003). The prion protein was placed in the center of a cubic box with the volume of 65 × 65 × 65 Å3 soaked with water or ethanol. For ethanol system, roughly 140 ethanol molecules and 7885 water molecules were added into simulation box, and the volume fraction was 5%. All-atom MD simulations were carried out by using the GROMACS 5.1.5 package with the OPLS-AA force filed (Kaminski et al., Citation2001). Then the TIP3P water model was used to solvate the systems (Jorgensen et al., Citation1983). And three sodium ions were added to neutralize the overall charge of each system."""
    text2 = """Molecular dynamics simulation for all structures was done by GROMACS v542 package on running Linux Ubuntu 16.04 operating system. We used force field GROMOS96 43a143 for simulation. WT and mutant proteins were solvated in a cubic box with a dimension of 52.0 A,° including single-point-charge water. The structures were neutralized to create the electrically neutral simulation system by adding Na+ and Cl− ions at physiological pH. Energy minimization was performed for 1000 steps by the steepest descent methods. Then molecular dynamics simulation was carried out in three steps containing NVT-MD (constant number of particles [N], volume [V], and temperature [T]), NPT-MD (constant number of particles [N], pressure [P], and temperature[T]) for 1000 picoseconds (ps) at 300 K and the final simulation production were carried out at 300 k for 300 ns for WT protein, E200K, and G127V models. We then calculated the comparative analysis of structural deviations in the WT and mutant structures."""
    text3 = """The MD methods employed are the same as the previous studies (Zhang, Citation2010; Zhang & Zhang, Citation2013, Citation2014). Briefly, all simulations used the ff03 force field of the AMBER 11 package (Case et al., Citation2010). The systems were surrounded with a 12 Å layer of TIP3PBOX water molecules and neutralized by sodium ions using the XLEaP module of AMBER 11. To remove the unwanted bad contacts, the systems of the solvated proteins with their counter ions had been minimized mainly by the steepest descent method and followed by a small number of conjugate gradient steps on the data, until without any amino acid clash checked by the Swiss-Pdb Viewer 4.1.0 (http://spdbv.vital-it.ch/). Next, the solvated proteins were heated from 100 to 300 K in 1 ns duration. Three sets of initial velocities denoted as seed1, seed2, and seed3 are performed in parallel for stability (this will make each set of MD starting from different MD initial velocity implemented in Amber package we choose three different odd–real-number values for “ig”) – but for the NMR structure and the X-ray structure of RaPrPC, each set of the three has the same “ig” value in order to be able to make comparisons. The thermostat algorithm used is the Langevin thermostat algorithm in constant NVT ensembles. The SHAKE algorithm (only on bonds involving hydrogen) and PMEMD (Particle Mesh Ewald Molecular Dynamics) algorithm with non-bonded cutoff of 12 Å were used during heating. Equilibrations were reached in constant NPT ensembles under Langevin thermostat for 5 ns. After equilibrations, production MD phase was carried out at 300 K for 25 ns using constant pressure and temperature ensemble and the PMEMD algorithm with the same non-bonded cutoff of 12 Å during simulations. The step size for equilibration was 1 and 2 fs in the MD production runs. The structures were saved to file every 1000 steps. During the constant NVT and then NPT ensembles of PMEMD, periodic boundary conditions have been applied."""
    text4 = """TheNMR structures of the human wt-PrP (PDB code 1HJN; residues 125–228) (57) and its V210I mutant (PDB code 2LV1; residues 125–231) (50) contain the C-terminal globular domain used as initial coordinates (58) for molecular dynamics (MD) simulations. Before starting the simulation, we checked the effect of V210I mutation on its structural stability and aggregation rate using DUET (59) and AggreRATE-pred (60) programs, respectively. The resulting analyses showed that the mutant had been foreseen as structurally destabilizing (ΔΔG ∼ 3.64 kJ/mol) and exhibit the predicted aggregation rate of −0.26. Several groups have been extensively studied the effect of disease-associated mutation on prion protein before. (61−65) The previous simulation work performed by Chandrasekaran and Rajasekaran (2016) (61) emphasizes the influence of disease-related mutation V210I on prion protein by applying the OPLS-AA force field. By use of this force field, the ensemble of conformations produced during the simulation did not capture the refolding of PrP upon V210I mutation, i.e., the formation of extended β-sheet structure at the N-terminal region. Such an elongated structure is a novel indicator of misfolding and drove the conformational conversion of PrPC to PrPSc, critical information needed for understanding the molecular pathogenesis of prion diseases. Furthermore, Thompson et al. (66) performed molecular dynamics simulations using a set of force fields to study the molecular basis of PrPC misfolding followed by conformational conversion and found that only GROMOS96 53a6 and AMBER99SB force fields are capable of capturing the crucial elongated β-sheet structure during refolding. Moreover, another study also found a discrepancy in protein force fields in replicating the experimental state populations. However, the two force fields CHARMM 36m and GROMOS 54A7 replicate the conformational states that relate to the experimental results remarkably. These two force fields overcome others and are mainly developed to sample the ordered and disordered proteins. Besides, the GROMOS 54A7 force field even outperforms CHARMM 36m by mimicking accurate folding kinetics. (67) Therefore, we carried out this work to obtain the ensemble of experimentally related conformations with extended β-sheet structure, which may assist in understanding the molecular pathogenesis of prion diseases. Hence, in this work, the simulations are performed using GROMACS v5.1.1 (68) with the GROMOS96 53a6 force field. (69) The wt-PrP and its mutant structures have been immersed separately in the cubic box as solvated with explicit SPC water molecules (70) using periodic boundary conditions. The net charges of wt-PrP and its mutant were further neutralized by adding three Na+ ions in each system. Then, both of the structures are subjected to the steepest descent dependent energy minimization to avoid steric interference or inappropriate geometries if they exist, using a maximum of 50 000 steps and a convergence tolerance of not greater than 1000 kJ mol–1 nm–1. The LINCS algorithm (71) was used to constrain all bonds during the simulations. The long-range electrostatics is defined by the particle-mesh Ewald (PME) method (72) with a Fourier grid spacing of 0.16 nm and a Coulomb distance cutoff of 1.0 nm. A modified Berendsen thermostat algorithm V-rescale (73) was applied to preserve the temperature (300 K) for protein and non-protein coupling groups. Further, the Parrinello–Rahman (74) pressure coupling algorithm was employed to make a system with uniform scaling of box vectors with 1 bar under a constant temperature of 300 K. Finally, a production run of 200 ns simulations was carried out for both wt-PrP and its mutant and each conformation saved at a regular time interval of 10 ps. (75−78) Analyses of trajectories were performed using GROMACS in-built utilities such as gmx rms (root-mean-square deviation), gmx hbond (hydrogen bond), and gmx sham (free energy landscape). DSSP (Dictionary of Secondary Structure of Proteins) program (38) was applied to monitor the evolution of secondary structural elements in the wt-PrP and its mutant as a function of time. The side-chain hydrophobic contacts are computed when the C–C distance between the CHn group is ≤5.0 Å. The molecular representation was generated using PyMol (79) and YASARA. (80)"""
    text5 = """To study the effects of pH on the overall structure of native PrPC, the conventional MD simulations were performed first for huPrP90–231 at the neutral and acidic condition, respectively. Three parallel runs for each system were performed to confirm the reliability of obtained data. All MD simulations were performed using the Amber 14 package (64) with the Amber ff99SB force field. (65) The starting structure was then solvated into a cubic periodic box using the TIP3P water model, (66) and the box edges were set at least 10 Å around protein. To keep the electroneutrality of systems, one Cl– ion was added to the neutral system, and seven Cl– ions were added to the acidic system. After that, 2500 steps of the steepest decent minimization followed by 2500 steps of conjugate gradient minimization were performed for each system to eliminate unnatural collision. The systems were then warmed up from 0 to 310 K in the NVT ensemble by keeping the protein constrained, and the temperature was controlled by the Langevin thermostat. Subsequently, 1 ns equilibration MD simulations were carried out in the NPT ensemble and followed by 500 or 800 ns production MD simulations. A 2 fs time step was used to integrate the equations of motion. During the simulation, SHAKE algorithm (67) was utilized to constrain the hydrogen-involved bonds and the particle mesh Ewald method (68) was used for the calculation of electrostatic contributions to the nonbonded interactions with the nonbonded cutoff distance set to 10 Å."""
    text6 = """The 3D structure of human PrP was retrieved from the PDB database (https://www.rcsb.org, PDB ID: 1HJN) obtained by NMR at pH 7.0 (Calzolai and Zahn, 2003a), which contains the C-terminal globular domain of PrP consisting of residues 125–228. The globular domain contains three α-helices (H1, H2, H3) and two very short anti-parallel β-sheets (S1, S2) (Donne et al., 1997, Riek et al., 1996). The graphene sheet with the size 65 Å × 65 Å is used in this study, which is large enough for the prion protein. The adsorption of proteins on the graphene may depend on the initial orientation of the proteins relative to the surface, but significant peptide rotation was generally prevented by all-atom explicit-solvent MD simulation within nanosecond time scale. In our simulations, to ensure the enough interaction between graphene and prion, three initial orientations of prion protein were selected by keeping the long axis parallel to the graphene surface with the three typical faces facing graphene (shown in Fig. 1): 1) the H2 helix and the C-terminal of H1; 2) the beta-sheet and the N-terminals of the three helices (H1, H2 and H3); 3) H1 and the C-terminals of H2 and H3. Therefore, based on the three constructs, every part of PrP has a large probability to contact with graphene. In the initial complexes, the minimum distance between graphene and prion was set at least 5 Å, allowing PrP to freely rotate and adjust its orientation and to reduce the diffusion time approaching the graphene surface."""
    text61 = """We performed MD simulations with TIP3P (Jorgensen et al., 1983) waters using AMBER 10 software package (Case et al., 2008) and AMBER ff03 (Duan et al., 2003, Lee and Duan, 2004) force field since previous MD simulation studies have shown that AMBER ff03 can reproduce the secondary structures of peptides comparable to experimental measurements when the peptides are adsorbed on carbon nanotube or graphene (Balamurugan et al., 2010). Na+ ions were added to keep the systems neutral. The complexes and the isolated protein were placed in a rectangular box and truncated octahedral box, respectively. The distance between protein/graphene and box-boundary was set at least 11 Å. To reduce the solvent boxes, several carbon atoms of four corners in the graphene sheet were deleted here. The total atom numbers of three systems with graphene are 40663, 43189, 44287, respectively. Firstly, we used steepest decent method and conjugate gradient method to minimize each system. Subsequently, the systems were heated up from 0 to 310 K with a force weight of 2.0 kcal/(mol Å2) on the graphene. All bond lengths involving hydrogens were constrained using the SHAKE algorithm (Ryckaert et al., 1977), 2-fs time step was made use of integrating the equations of motion. The non-bonded cutoff distance was 10 Å and the long-range electrostatics interactions were calculated by using the Particle Mesh Ewald(PME) (Essmann et al., 1995) method. The Langevin thermostat was used to regulate the temperature of the system. All equilibration and subsequent MD stages were carried out in the isothermal isobaric (NPT) ensemble using a Berendsen barostat (Berendsen et al., 1984). There were no restrains on protein but a force constant of 2.0 kcal/mol Å2 on graphene. Each complex system was simulated for 100 ns to investigate the initial adsorption stage. Neutral pH and physiological temperature (310 K) are controlled in all simulations."""
    text6 = text6+" "+text61
    text7 = """Molecular Docking of the Predicted B. abortus Hsp60 Protein with Human Prion Protein (huPrP)-Derived Peptides

Human prion protein (huPrP) with full NMR structure (from amino acid 125 to 228, PDB ID: 1QLX) was chopped into four different fragments using PyMOL based on its secondary structures and the results of Edenhofer et al. [14]—125 to 173, 144 to 156, 174 to 223, and 180 to 210. Following the preparation of all huPrP-derived peptides and Hsp60 protein, molecular docking studies were carried out using High Ambiguity Driven DOCKing (HADDOCK) server [15]. In HADDOCK web portal, Hsp60 model was uploaded as the first molecule, and all peptides were uploaded separately as the second molecule. The putative binding site of Hsp60 protein was predicted to be on the apical domain using CASTp server [16]. Binding affinity (ΔG) of docked complexes was predicted using PRODIGY server [17]. The docked conformation from the top cluster with the highest Z score and binding affinity in kcal/mol was selected for the following molecular dynamics (MD) simulations.
Molecular Dynamics Simulations (MDs)

Molecular dynamics simulations were performed on the huPrP-derived peptides and Hsp60 complex obtained from previous studies using GROningen MAchine for Chemical Simulations (GROMACS) 2022.2 package [18] for 100 ns. Physical forces were implemented using the ff14sb protein force field to carry out MD simulations. Structures were solvated in a cubic box filled with TIP3P water molecules, followed by ionization and neutralization of the simulation system with Na+ and Cl− ions. The peptide–protein complexes were minimized in 50,000 steps using the steepest descent method. After minimization, 500 ps NVT (isothermal-isochoric) and 1 ns NPT (isothermal-isobaric) ensembles with the V-rescale temperature coupling and Parrinello-Rahman pressure coupling were used to equilibrate the system at 310 K and 1 atm. Leap-frog integrator was used with a step size of 2 fs. Bond lengths were constrained with the LINCS algorithm. The short-range van der Waals cutoff was 1.0 nm. Finally, three 100 ns molecular dynamics simulations were carried out for all complexes. Following the simulations, the output data were analyzed for root-mean-square deviation (RMSD) and root-mean-square fluctuation (RMSF). The relative binding free energy for all complexes was computed using MMPBSA approach with gmx_MMPBSA package [19]."""
    text8 = """Recently, the NMR molecular structures of wild-type, mutant I214V and mutant
S173N rabbit prion proteins (124-228) were released into the Protein Data Bank
Abbreviations: CJD: Creutzfeldt-Jakob disease; vCJD: variant Creutzfeldt-Jakob diseases; GSS:
Gerstmann-Sträussler-Scheinker syndrome; FFI: Fatal Familial Insomnia; BSE: bovine spongiform
encephalopathy; CWD: chronic wasting disease; MD: molecular dynamics; RMSD: root mean square
deviation; DoPrPC: dog prion protein; RaPrPC: rabbit prion protein.
862
Zhang
(18-20) with PDB ID codes 2FJ3, 2JOM, 2JOH respectively. Zhang (2010) studied
these NMR structures by MD simulations, and simulation results at 450 K in low
and neutral pH environments confirmed the structural stability of wild-type rab-
bit prion protein under neutral pH environment, but in low pH environment the
wild-type rabbit protein protein is without structural stability (21). Zhang (2009)
compared wild-type rabbit prion protein with human (PDB ID: 1QLX) and mouse
(PDB ID: 1AG2) prion proteins and concluded that wild-type rabbit prion protein
(124-228) does not have a structural stability at high temperature (10). In this paper
another set of MD simulation starting from different initial velocity (so-called
seed2) will be repeated for the wild-type rabbit prion protein. We call the MD
simulation initial velocity of (21) seed1. For both seed1 and seed2, the dog prion
protein (PDB ID: 1XYK) is studied in this paper.
All the MD simulations in this paper confirmed the structural stability of wild-type
dog prion protein under both neutral and low pH environments. The analyses of salt
bridges, hydrogen bonds and hydrophobic contacts for dog prion protein will be
done in order to seek reasons of the stability. The rest of this paper is arranged as
follows. We introduce the MD simulation materials and methods (similar as (21))
in Section 2. Section 3 mainly gives MD simulation results and their discussions.
Concluding remarks are given in the last section.
Materials and Methods
The MD simulation materials and methods for dog and rabbit prion proteins are
completely same as the ones of (21). Simulation initial structure for the dog and
rabbit prion proteins were built on DoPrPC(121-231) (PDB entry 1XYK) and
RaPrPC(124–228) (PDB entry 2FJ3), respectively. Simulations were done under
low pH and normal pH environments respectively. The simulations of dog prion
protein were done starting from two sets of initial velocities (seed1 and seed2).
The simulations of rabbit prion protein were done starting from the same seed1
and seed2, i.e. the same two sets of initial velocities. MD simulation experience of
the author showed that other additional seeds could not make much difference for
the MD simulations (10). All the simulations were performed with the AMBER
9 package (22), with analysis carried out using functionalities in AMBER 9 (22)
and AMBER 7 CARNAL (23). Graphs were drawn by XMGRACE of Grace
5.1.21, DSSP (24).
All simulations used the ff03 force field of the AMBER 9 package, in neutral
and low pH environments (where residues HIS, ASP, GLU were changed into
HIP, ASH, GLH respectively by the XLEaP module of AMBER 9 in order to
get the low pH environment). The systems were surrounded with a 12 Å layer of
TIP3PBOX water molecules and neutralized by sodium ions using XLEaP mod-
ule of AMBER 9. 15 Cl-, 14 Cl- and 4337 waters, 5909 waters were added for
the dog and rabbit prion wild-type proteins respectively for the low pH environ-
ment. The solvated proteins with their counterions were minimized mainly by
the steepest descent method and then a small number of conjugate gradient steps
were performed on the data, in order to remove bad hydrogen bond contacts.
Then the solvated proteins were heated from 100 to 450 K step by step during
3 ns. The thermostat algorithm used is the Langevin thermostat algorithm in
constant NVT ensembles. The SHAKE algorithm and PMEMD algorithm with
nonbonded cutoffs of 12 Å were used during the heating. Equilibrations were
done in constant NPT ensembles under Langevin thermostat for 5 ns. After
equilibrations, production MD phase was carried out at 450 K for 30 ns using
constant pressure and temperature ensemble and the PMEMD algorithm with
nonbonded cutoffs of 12 Å during simulations. Step size for equilibration was
0.5, and 1 fs for the production runs. The structures were saved to file every
1000 steps."""

    text9 = "Molecular dynamics (MD) simulations were run for M1 in complex with tiotropium (PDB 5XCV) (Thal et al., 2016), HTL9936, GSK1034702 or 77-LH-28-1. For M1 in complex with tiotropium the cocrystallized FLAG peptide bound to the intracellular side of the receptor was included. Input PDB files were processed with Schrödinger Maestro’s Protein Preparation tool (2020-3) by adding hydrogen atoms, modeling missing side chains, and determining the most relevant protonation states of residues and ligands at pH 7.4. Fusion proteins inserted in the ICL3 of the constructs were removed and the long unstructured loop was truncated with a tetraglycine region between the Ballesteros-Weinstein positions 5.68-6.24, following similar approaches by Dror et al. (Dror et al., 2009) and Miszta et al. (Miszta et al., 2018). The constructs were embedded in POPC phospholipids and solvated in TIP3P water and 150 mM NaCl. The systems were parameterised using OPLS3e (Roos et al., 2019)after optimization of the parameters of the ligands using Maestro’s Force Field Builder tool. Desmond 2020-3 was used for the MD simulations. Relaxation and equilibration to 300 K and 1 bar were run using the standard protocol for membrane proteins. Production runs were performed for 100 ns at 300 K and 1 bar in the NPT ensemble. Temperature and pressure were controlled with Nose-Hoover chain thermostat (Hoover, 1985) and the semiisotropic MTK barostat. The RESPA integrator was employed with timesteps of 2 fs, 2 fs and 6 fs for bonded, short-range nonbonded and long-range nonbonded interactions, respectively."
    text10 = '''Soluble peptides and proteins can undergo conformational changes and aggregate into threadlike, elongated insoluble intra- and extra-cellular accumulations known as amyloid fibrils. (1−8) Their presence is frequently linked to the pathology of neurodegenerative diseases, including Parkinson’s disease (PD) and Alzheimer’s disease (AD). (1,3,7−9) The sequences, structures, and physiological functions (if any) of amyloidogenic (poly)peptides are very diverse, yet they all share the common feature that under given conditions they can aggregate into amyloids. (1,9,10) Whether mature amyloid fibrils or oligomeric species are responsible for triggering neurodegeneration is not clear. A large variety of experimental and computational tools are used to shed light into the aggregation mechanisms of amyloidogenic structures and their toxicity, and to find strategies to block their advancement.
At first, amyloids were associated with disease and tissue damage, (6,8,9) but over the past years a growing body of evidence suggests that the self-assembly of certain (poly)peptides can have a functional role in healthy human cells and microorganisms. (8,10−13) Examples include the involvement in melanin synthesis, (14) storage of peptide hormones, (15) long-term memory consolidation, (16) biofilm formation, (11,17,18) and mediation of tumor necrosis. (19)
Amyloid formation is commonly described as a nucleation–elongation mechanism. (20−22) The nucleus is the unstable species that has the same probability to dissociate and to form a fibril. It acts as the initial template of aggregation for the free monomers in solution. The nucleation of pathological amyloids can be a one-step process in which monomers simultaneously adopt the fibrillar structure and aggregate, or it can involve a disordered aggregated state (two-step process). (7,23) In contrast, functional amyloids have been (so far) identified to nucleate in a single step. (12) While the nucleation is a rare stochastic event, the elongation is much faster and occurs through monomer binding at fibrillar ends.
Amyloid aggregation can be modulated by varying the solution pH (24,25) and temperature, (26,27) by adding cosolvents or osmolytes, (28) or by the presence of membranes. For example, the nucleation rate of α-synuclein can be increased in the presence of lipid membranes, (29) DOPC lipid vesicles accelerate the Aβ42 growth rate or can augment monomer-dependent secondary nucleation. (30) Furthermore, bilayers consisting of lipids commonly found in membranes of synaptic vesicles (DOPE, DOPC, DOPS, POPS, and cholesterol) do not enhance α-synuclein aggregation substantially, whereas DMPS and DLPS model membranes significantly increase its aggregation rate. (31) On the other hand, upon interaction with membranes amyloidogenic aggregates can have modulating or disruptive effect, resulting in cell dysfunction. (32,33) Amyloid fibrils attached to membranes have been observed to extract lipids, (33−36) and oligomeric aggregates were shown to perturb the membrane and disrupt the cellular function by insertion into lipid bilayers, which ultimately leads to leakage. (32) We refer the reader to a series of reviews addressing the interaction of amyloidogenic peptides with membranes in refs (37−39).
The molecular mechanisms underlying the formation of early stage aggregates, oligomers, and amyloid fibril are still elusive and pose a series of difficulties to classical simulation approaches, e.g., molecular dynamics. One of the most challenging aspects is recovering relevant experimental time- and length-scales. In classical molecular dynamics the computational demand depends on the simulated time and the number of atoms. The dependence on number of atoms is linear thanks to nonbonding cutoffs and neighboring lists. The size of a simulation system spans usually from a few nanometers and 103 to 104 atoms for single peptides to micrometers and 104 to 105 atoms for fibrils. The length of the longest simulations even on the fastest (dedicated) hardware is less than 1 ms. As a consequence the time scales of minutes to hours required for amyloid aggregation in vitro (40−42) are several orders of magnitude longer than those acessible by atomistic simulations. While experimental methods enable the monitoring over relevant time and length scales, simulations require special techniques to circumvent this problem. These include coarse-grained descriptions of (poly)peptides, simplified treatment of the aqueous solvent (implicit solvent) and/or membranes, and protocols for accelerating rare events (enhanced sampling). (43−45)
This review describes the challenges inherent to the simulations of amyloid forming (poly)peptides and their aggregates, as well as the difficulties in comparing to experimental data. In particular, this review will address the generic amyloid growth mechanisms and the associated kinetics from interdisciplinary, multiscale simulation, and experimental perspectives, with an emphasis on the complementarity between them. Next, it will provide a perspective on the future problems that can be tackled using computational methods, the predictive role of simulations, and their limitations. It lies outside the scope of this paper to review the force-fields or water models used when dealing with amyloid forming proteins or any experimental techniques, as these have been reviewed elsewhere. (46−49) It is unavoidable that this review does not include all simulation studies of (poly)peptide self-assembly. We made a selection of amyloidogenic (poly)peptides and tried to exhaustively mention the simulation studies that were carried out with them. Lists of human (poly)peptides that can form pathogenic and functional amyloids are provided in Tables 1 and 3, respectively, of ref (8).
Reference	Peptide	Model	Solvent	Method	Samplingb
Gsponer et al. (213)	Sup357 – 13	CHARMM19	implicit	MD	20 μs
Urbanc et al. (214)	Aβ42	CG	implicit	DMD	1 × 107 steps
 	Aβ40	 	 	 	1 × 107 steps
Barz et al. (200)	Aβ42	OPLS-AA	GBSA	MD	2.5 μs
 	Aβ40	 	 	 	2.5 μs
Sun et al. (215)	Aβ16 – 22	CG	EEF1	DMD	2 μs
 	hIAPP15 – 25	 	 	 	3 μs
 	hIAPP15 – 25(S20G)	 	 	 	3 μs
 	hIAPP19 – 29	 	 	 	3 μs
 	hIAPP19 – 29(S20G)	 	 	 	3 μs
 	hIAPP22 – 28	 	 	 	2 μs
 	α-syn 68 – 78	 	 	 	2 μs
Sun et al. (216)	hIAPP	CG	EEF1	DMD	10–25 μs
Collu et al. (217)	ovPrPSc171 – 226	GROMOS53a6	SPC	MD	2.2 μs
Carballo-Pacheco et al. (218)	Aβ25 – 35	OPLS-AA	TIP4P	MD	30 μs
 	kassinin	 	 	 	30 μs
 	neuromedin K	 	 	 	30 μs
'''

    text11='''

    COMPUTATIONAL DETAILS

    Bilayer and tetralayer fibrillar architectures for each heptapeptide have been built with an in-house script that computes the Cartesian coordinates of steric zippers of any length applying rigid rotations and translations to the coordinates of an individual strand, as done in Ref. 18. The N-terminus was acetylated and the C-terminus was amidated to match experimental conditions.15 This neutral capping has been applied to all systems to allow comparison among different architectures. Indeed, terminal charges are known to deeply affect fibril organization.19 The length of each β-sheet has been set to 20 strands so that each model is composed of 40 strands for bilayers and 80 strands for tetralayers (Fig. 1). A rectangular solvent box of TIP3P waters20 has been added around each fibril model with a minimum 10 Å water buffer around the solute in each direction.
    FIG. 1.
    FIG. 1. Front and side views of a SY7 bilayer and tetralayer fibril model.
    View largeDownload slide

    Front and side views of a SY7 bilayer and tetralayer fibril model.

    The stability of the different amyloid fibril architectures was addressed with MD simulations using the ff14SB force field21 with the Amber suite.22 Initial inter-sheet distances were set to relatively large values to avoid steric clashes, and the equilibration process involved a slow heating to production temperature alternating runs with geometrical restraints with short unrestrained dynamics bursts to allow for packing of side chains at the steric zipper. Trajectories were computed within the NPT ensemble at 300 K, lifting all restrains and using a Langevin thermostat and a Monte Carlo barostat23,24 until reasonably converged relative energies were obtained, that is, up to 100–300 ns, depending on the system (Table S1). An analogous approach was used to perform molecular dynamics simulation of individual strands, with a total production time of 300 ns. The relative energies of different fibril architectures for the same peptide compositions were evaluated by extracting 100 evenly spaced snapshots from the last 100 ns trajectory and subjecting them to a 500-step geometry minimization with the generalized Born implicit solvent model.25,26 This short minimization is necessary to remove temperature-induced steric clashes. Although this model is a crude approach, error cancellation is expected to provide reasonably accurate relative stability of different architectures, without accounting for differences in the water structure. Furthermore, since different architectures show different compactness, the number of water molecules may vary from one architecture to another, and thus, energies with explicit water molecules would not be comparable. Nevertheless, for some selected cases, we have also estimated the solvation energy with the more accurate analytical linearized Poisson–Boltzmann method.27 Results indicate that the solvent model has little impact on the computed relative energies between different architectures (less than 0.2 kcal mol−1 strand−1).

    The total energy of these minimized frames (internal energy from bonded and non-bonded force field terms plus solvation energy) were used to compute relative stabilities of the fibrillar architectures as follows: For each polypeptide composition (i.e., fibrils that share the same number and type of heptapeptides), the most stable system was set to zero, and the energies of the remaining systems were scaled accordingly. To allow direct comparison between the relative stabilities of bilayers (composed of 40 heptapeptides) and tetralayers (composed of 80 heptapeptides), these energies were then normalized by the number of heptapeptides in the model (dividing by 40 for the bilayers and by 80 for the tetralayers). For this reason, relative energies are greater or equal to zero and expressed in kcal mol−1 strand−1.

    Trajectory analysis was done on the 100 frames extracted from the last 100 ns molecular dynamics simulations. Fibrils twist angles were computed using vectors from the N atom of the amidated C-terminus to the carbonyl C atom of the acetylated N-terminus. For bilayers, these vectors were computed for the first and tenth strand of each of the two β-sheets. For each sheet, the angle between these two vectors was computed and the results were averaged. A similar procedure was applied to the calculation of the twist angle of tetralayers considering the first and fourth β-sheets. Hydrogen bonds were computed using cpptraj with a cutoff distance of 3 Å and a cutoff frequency of 0.5, meaning that they are considered only if they are robust (present in at least one half of the trajectory). In addition, Solvent Accessible Surface Area (SASA) values, as well as their variation with respect to the corresponding monomers (ΔSASA), were computed for all architectures using the surf option of Amber’s cpptraj. Average inter-sheet distances were also computed using the following procedure: for each frame, we iterated over each α-carbon (CA) of one β-sheet searching for the closest CA located on the opposite β-sheet. In this way, for each frame, we obtained a complete set of pairwise distances between the two sheets, from which we extracted a global mean value.
    RESULTS AND DISCUSSION

    As mentioned, we will here address the preferred architectures of both individual steric zippers (bilayers), and larger supramolecular structures formed by four β-sheet layers (tetralayers).
    Bilayer architectures

    Steric zipper organizations can be classified according to (i) intra-sheet organization, parallel (P) or antiparallel (AP), (ii) how side chains are packed at the interface, face-to-back (FB), face-to-face (FF), or face = back (FEQB), and (iii) the relative orientation of the β-sheets [up–up (UU), up–down (UD), or up = down (UEQD)],28 which, for our heptapeptides, leads to 11 non-equivalent architectures (Fig. 2).
    FIG. 2.
    FIG. 2. Front view of the 11 non-equivalent steric zipper architectures that can arise from the self-assembly of SY7. Tyrosines are represented in pale green and serines in red.
    View largeDownload slide

    Front view of the 11 non-equivalent steric zipper architectures that can arise from the self-assembly of SY7. Tyrosines are represented in pale green and serines in red.

    The relative energies, hydrogen bond contacts, mean inter-sheet distances, and nanofiber twist angles for the fibril models for NY7, NF7, SY7, SF7, and GY7 are shown in Table I. Since energy differences between UU and UD configurations are, in general, small (1–3 kcal mol−1 strand−1) and do not change observed trends, Table I only includes the most stable arrangement in each case. The full table with the 11 non-equivalent steric zippers architectures is given in the supplementary material in Table S2. Furthermore, most stable fibril models are shown in Fig. S1, and plots for relative energies, twist angle, and intersheet distances along the trajectories are given in Figs. S2–S4, respectively.
    TABLE I.

    Average relative energies and standard deviation (in brackets) in kcal mol−1 strand−1, number of inter-sheet backbone–side chain hydrogen bonds (i-BB–SC), number of inter-sheet side-chain–side chain hydrogen bonds (i-SC–SC), mean inter-sheet distances (ID) in Å, and twist angles in degrees for the bilayer systems. Relative energies are calculated with respect to the most stable architecture. Values for NY7, SY7 and GY7 are taken from Ref. 18.
    Architecture	Zippera	ΔE	i-BB–SC	i-SC–SC	ID	Twist
    NY7 
    AP-FEQB-UU 	b 	9.46 (0.56) 	0 	4 	9.2 	161.9 
    AP-FB-UEQD 	NY⋯NY 	8.31 (0.70) 	0 	25 	9.4 	164.2 
    AP-FF1-UEQD 	NY⋯YN 	11.96 (0.54) 	0 	13 	11.2 	166.1 
    AP-FF2-UEQD 	YN⋯NY 	9.67 (0.61) 	5 	36 	7.8 	159.1 
    P-FB-UD 	NY⋯NY 	2.00 (0.77) 	0 	20 	9.7 	16.6 
    P-FF1-UU 	NY⋯YN 	8.23 (0.91) 	0 	72 	11.4 	32.5 
    P-FF2-UU 	YN⋯NY 	0.00 (0.26) 	0 	31 	7.5 	15.9 
    NF7 
    AP-FEQB-UU 	b 	8.88 (0.52) 	2 	21 	9.1 	136.0 
    AP-FB-UEQD 	NF⋯NF 	9.85 (0.67) 	0 	0 	9.4 	136.5 
    AP-FF1-UEQD 	NF⋯FN 	11.42 (1.11) 	0 	0 	10.0 	149.7 
    AP-FF2-UEQD 	FN⋯NF 	7.76 (0.48) 	2 	35 	7.5 	148.9 
    P-FB1-UD 	NF⋯NF 	1.33 (0.63) 	0 	0 	9.6 	18.6 
    P-FF1-UU 	NF⋯FN 	3.11 (0.70) 	0 	0 	11.1 	7.0 
    P-FF2-UD 	FN⋯NF 	0.00 (0.79) 	0 	59 	7.8 	18.7 
    SY7 
    AP-FEQB-UU 	b 	12.40 (0.65) 	0 	3 	8.5 	109.8 
    AP-FB-UEQD 	SY⋯YS 	12.59 (0.74) 	8 	25 	8.3 	166.2 
    AP-FF1-UEQD 	SY⋯YS 	16.04 (0.39) 	0 	12 	11.2 	171.1 
    AP-FF2-UEQD 	YS⋯SY 	2.28 (0.43) 	19 	13 	5.6 	172.4 
    P-FB1-UD 	SY⋯SY 	9.72 (0.49) 	0 	30 	8.5 	11.6 
    P-FF1-UU 	SY⋯YS 	18.23 (0.71) 	0 	71 	11.4 	24.2 
    P-FF2-UU 	YS⋯SY 	0.00 (0.57) 	38 	69 	5.8 	37.8 
    SF7 
    AP-FEQB-UU 	b 	11.52 (0.43) 	0 	0 	8.4 	155.5 
    AP-FB-UEQDc 	SF⋯SF 	13.13 (0.52) 	⋯ 	⋯ 	⋯ 	⋯ 
    AP-FF1-UEQD 	SF⋯FS 	10.84 (0.61) 	0 	0 	10.3 	143.0 
    AP-FF2-UEQD 	FS⋯SF 	0.01 (0.41) 	11 	25 	5.6 	170.2 
    P-FB1-UU 	SF⋯SF 	12.55 (0.57) 	0 	0 	8.1 	28.6 
    P-FF1-UU 	SF⋯FS 	11.48 (0.44) 	0 	0 	11.0 	8.2 
    P-FF2-UU 	FS⋯SF 	0.00 (0.35) 	38 	78 	5.8 	33.7 
    GY7 
    AP-FEQB-UU 	b 	8.05 (0.47) 	16 	0 	7.8 	170.7 
    AP-FB-UEQD 	GY⋯GY 	13.40 (0.65) 	22 	0 	7.2 	128.5 
    AP-FF1-UEQDc 	GY⋯YG 	17.97 (1.16) 	⋯ 	⋯ 	⋯ 	⋯ 
    AP-FF2-UEQD 	YG⋯GY 	0.00 (0.57) 	0 	0 	4.2 	169.4 
    P-FB1-UD 	GY⋯GY 	12.37 (0.29) 	7 	0 	7.6 	4.6 
    P-FF1-UDc 	GY⋯YG 	21.24 (1.24) 	⋯ 	⋯ 	⋯ 	⋯ 
    P-FF2-UD 	YG⋯GY 	6.64 (0.74) 	0 	0 	4.3 	6.0 
    a

    Residues in bold are exposed to the solvent.
    b

    See Fig. 2.
    c

    Unstable fibrils (the fibrillar organization is not maintained along the molecular dynamics simulation).

    The most stable architecture for NY7, NF7, SY7, and SF7 systems exhibits a parallel β-sheet arrangement (P-FF2) with tyrosines/phenylalanines exposed to the solvent and the polar residues asparagine and serine packed at the interface due to the formation of a strong intra- and inter-sheet hydrogen bond network involving asparagine or serine residues. These architectures match the preferred arrangement of the related N- and Q-rich natural prion sequences, such as NNQQNY and QNNQQNY.28 In the case of serine, side chain–backbone carbonyl inter-sheet hydrogen bonds are also observed, which account for the larger twist observed for this system compared to NY7 (see, for instance, twist angles of P-FF2-UU structures of NY7 and SY7 in Table I). Because parallel arrangements allow the formation of stronger hydrogen bond contacts at the zipper due to a better interdigitation of side chains, this parallel configuration, and not the antiparallel, is the preferred one for these systems. Indeed, the number of stable asparagine or serine side chain hydrogen bonds is significantly larger in parallel structures than in the antiparallel ones, which results in a stronger stabilization. These results are in line with the consensus features characterizing amyloid organization, which include parallel-stranded in-register sheets, the stacking of identical residues on top of each other and β-sheet twisting.29 

    P-FF1 configurations, with tyrosines or phenylalanines at the interface, are significantly less stable, with relative energies with respect to the most stable P-FF2 one of 8.23 kcal mol−1 strand−1 (NY7), 3.11 kcal mol−1 strand−1 (NF7), and 18.23 kcal mol−1 strand−1 (SY7). Insights into the relative stability between different architectures can be obtained by decomposing the relative total energy into its gas phase and solvation energy contributions (Table S3). For NY7, NF7, and SY7, gas phase results, which account for the intrinsic different stabilities, indicate that architectures with asparagines/serines packed at the interface are clearly the preferred configurations due to the formation of a highly stable hydrogen bond network at the zipper, while P-FF1 configurations, with tyrosines or phenylalanines at the interface, are significantly less stable. Indeed, the gas-phase energy differences between P-FF1 and P-FF2 architectures are 17.35 kcal mol−1 strand−1 (NY7), 25.03 kcal mol−1 strand−1 (NF7), and 24.82 kcal mol−1 strand−1 (SY7). However, when accounting for the solvation energy, these differences decrease significantly up to 8.23 kcal mol−1 strand−1 (NY7), 3.11 kcal mol−1 strand−1 (NF7), and 18.23 kcal mol−1 strand−1 (SY7) because the solvation energy is, as expected, more stabilizing for asparagines/serines solvent exposed architectures (P-FF1) than for phenylalanies/tyrosines solvent exposed ones (P-FF2) (see Table S3). Contacts at the zipper seem thus to be the dominant factor to determine the stability of the different architectures in these systems. Noticeably, these P-FF2 architectures lead to more compact structures, i.e., the intersheet distance is smaller, and so, the SASA values for these configurations are the smallest ones and their diminution with respect to the monomer (ΔSASA) are the largest ones (Table S3). SASA values, however, do not clearly correlate with the stability of the different architectures since solute–solvent interactions need also to be considered. Indeed, these solute–solvent interactions are the main factor for the decrease observed in the energy difference between P-FF1 and P-FF2 architectures when going from NY7 (8.23 kcal mol−1 strand−1) to NF7 (3.11 kcal mol−1 strand−1). Note that the ΔEgp between FF1 and FF2 is larger in NF7 than in NY7, while the intersheet distances (and the SASA values) are similar in both NY7 and NF7 systems.

    Still, however, the polar steric organization appears to be the preferred one in NF7 because of the presence of four asparagines as compared to three phenylalanines, whose hydrogen bond contacts at the interface more than compensate phenylalanine’s smaller solvation energy. Indeed, simulations for FN7, with four phenylalanines and three asparagines, reveal that the parallel architecture, with phenylalanines packed at the interface and asparagines exposed to the solvent, is slightly more stable than the other face-to-face architecture in which asparagines form a polar zipper and phenylalanines are exposed to the solvent. Thus, the number of polar/hydrophobic residues in the heptapeptide determines whether the hydrophobic or the polar zipper will be the preferred organization, representing an important criterion for the rational design of self-assembling materials with tailored properties. On the other hand, it is well known that adjacent rows of asparagines and glutamines engage in a highly stabilizing hydrogen bond pattern called “polar clasp,” as we observe in our simulations.30 These favorable interactions contribute to the aggregation propensity of this residue, which is highly abundant in prion sequences.16 P-FB hybrid interfaces, with both residues at the steric zipper, show an intermediate stability with relative energy values of 2.00 kcal mol−1 strand−1 (NY7), 1.33 kcal mol−1 strand−1 (NF7), and 9.72 kcal mol−1 strand−1 (SY7) that result from a balance between the hydrophobicity of the residues composing the heptapeptide and the hydrogen bond contacts at the interface.

    For SF7, the lowest parallel (P-FF2-UU) and antiparallel (AP-FF2-UEQD) architectures, both with serines at the interface, are energetically equivalent, the energy difference being 0.01 kcal mol−1 strand−1. Furthermore, the P-FF1 arrangement, with serines exposed to the solvent, are somewhat more stable (11.48 kcal mol−1 strand−1) than the hybrid P-FB-UU(UD) ones (12.55 kcal mol−1 strand−1), in contrast to previous systems, which is attributed to a better packing in the present case.

    An interesting feature shared by SY7 and SF7 fibrils is the striking difference in the hydrogen bonding pattern and twist angle of the essentially isoenergetic P-FF2-UD and P-FF2-UU architectures (Table I and Table S1). Analysis of the molecular dynamics trajectories shows that while in both fibrils serines are packed at the fibril core, they engage in a quite different non-covalent interaction pattern. In the less twisted P-FF2-UU fibril [Fig. 3(a)], OH groups are mostly aligned along the fibril’s growth axis, resulting in rows of cooperative hydrogen bonds. In P-FF2-UD [Fig. 3(b)], a different interaction pattern is favored where serine side chains interact with the backbone carbonyl groups of the residues directly facing across the steric zipper. In this case, interactions are oriented diagonally with respect to the fibril growth axis, which imposes a further distortion of the β-sheet that increases the twist angle.
    FIG. 3.
    FIG. 3. Across-sheet hydrogen bond pattern of (a) bilayer SF7-PFF2-UU and (b) bilayer SF7-PFF2-UD. (c) Intra- and inter-sheet hydrogen bond pattern of the N⋯N zipper of tetralayer NF7-PFF2-a and (d) NF7-PFF2-b. (e) Asymmetric distribution of inter-sheet distances in the tetralayer GY7-APFF2. Only a fraction of the fibril is shown for clarity.
    View largeDownload slide

    Across-sheet hydrogen bond pattern of (a) bilayer SF7-PFF2-UU and (b) bilayer SF7-PFF2-UD. (c) Intra- and inter-sheet hydrogen bond pattern of the N⋯N zipper of tetralayer NF7-PFF2-a and (d) NF7-PFF2-b. (e) Asymmetric distribution of inter-sheet distances in the tetralayer GY7-APFF2. Only a fraction of the fibril is shown for clarity.

    This difference arises from the relative orientation of serine side chains in the UU vs UD architecture. In the first case, serine side chains face each other directly across the β-sheet, favoring side chain–side chain contacts. In the second, a lateral offset between the two planes favors mixed side chain–side chain and backbone–side chain contacts. An interesting question is whether these two alternative arrangements will be maintained in the tetralayer. Increasing the number of layers from two to four, the number of steric zippers will increase from one to three, which means that different interfaces will coexist in the fibrillar model. Intuition suggests that the twist angle of the resulting tetralayer will be determined by a trade-off between the features of the three individual steric zippers, which could penalize the extreme twist angle values observed in P-FF2-UD.

    An interesting recent work31 analyzes the differential effects of serine side chain interactions in amyloid formation by the islet amyloid polypeptide (IAPP), suggesting that intra-sheet networks of hydrogen-bonded serine side chains are not essential for amyloid formation. In the high-resolution crystallographic structure of the SSTNVG fragment of IAPP (PDB code 3DG1), serine side chains at the steric zipper have essentially the same organization as in our PFF2-UU systems, generating a similar side chain only hydrogen bond network. The crystallographic inter-sheet distance is 6.2 Å, which is larger than our molecular-dynamics based distances, which is 5.8 Å for both SF and SY PFF2-UU (Table I) and 5.5 Å for both SF and SY PFF2-UD (Table S1). This is likely due to the larger size of residues surrounding serine in the SSTNVG fragment. Inter-sheet side-chain/backbone hydrogen bonds require a shorter inter-sheet distance, which is essentially impossible to achieve if bulkier residues than serine are located at the steric zipper.

    For GY7, the antiparallel arrangement AP-FF2-UEQD, with glycines at the interface and tyrosines exposed to the solvent, is the most stable architecture, the lowest parallel configuration lying at 6.64 kcal mol−1 strand−1 higher in energy. All models that pack tyrosine at the interface and leave glycines exposed to the solvent, P-FF1-UD, P-FF1-UU and AP-FF1-UEQD, are not stable and disaggregate along the MD trajectory, whereas those with a hybrid packing, P-FB-UD and AP-FB-UEQD, are stable but exhibit relative energies that are about 12–13 kcal mol−1 strand−1. The preference for an antiparallel organization is due to the lack of hydrogen bond contacts between side chains. In this case, thus, the preferred intra-sheet organization is determined by backbone contacts, which are more stabilizing in an antiparallel organization. Since the glycine side chain is just a hydrogen atom, the inter-sheet distance in AP-FF2-UEQD is remarkably short (4.2 Å), thereby maximizing dispersion forces between the two β-sheets. As expected, the inter-sheet distance in GY7 is smaller than that obtained for NY7 and NF7 (7.5 and 7.7 Å), with asparagines at the interface, or for SY7 and SF7 (5.8 Å), with a serine packing, as this distance is mainly determined by the size of the side chains at the steric zipper.
    Tetralayer architectures

    We will now consider larger supramolecular structures formed by four β-sheets. As for individual zippers, they have been classified according to the (i) parallel (P) or antiparallel (AP) β-sheet arrangement, (ii) how side chains are packed at the interface, face-to-back (FB), face-to-face (FF), or face = back (FEQB), and (iii) the relative orientation of the β-sheets, depending on whether all β-sheets are up [UUUU, hereafter referred to (a)] or lie in an alternate manner [UDUD, hereafter referred to (b)]. Since previous results for the individual zipper have shown that for these systems the stability of UU and UD configurations do not vary significantly, we have not considered all possible U and D arrangements, just the two here described. Supramolecular structures considered are shown in Fig. 4. Furthermore, for NY7 and NF7, we only considered the parallel configurations since such a β-sheet arrangement was clearly found to be the most stable. However, for SY7 and SF7, we considered both parallel and antiparallel, since the two lowest structures of each kind are very close in energy, and for GY7, we considered only antiparallel ones.
    FIG. 4.
    FIG. 4. Front view of the 11 non-equivalent representative supramolecular architectures of 4SY enclosing four β-sheets. Tyrosines are represented in pale green, and serines are in red.
    View largeDownload slide

    Front view of the 11 non-equivalent representative supramolecular architectures of 4SY enclosing four β-sheets. Tyrosines are represented in pale green, and serines are in red.

    Relative energies, hydrogen bond contacts, inter-sheet distances, and twist angles are given in Table II. Furthermore, most stable fibril models are shown in Fig. S5, and plots for relative energies, twist angle, normalized number of hydrogen bonds, and intersheet distances along the molecular dynamics simulations are given in Figs. S6–S9, respectively. Relative energies for NY7 and SY7 indicate that the most stable structure (P-FF2) maintains the same residues exposed to the solvent as for individual zippers. That is, the preferred structure of NY7 and SY7 tetralayers (P-FF2) have the following arrangements YN⋯NY⋯YN⋯NY and YS⋯SY⋯YS⋯SY, respectively, with the two outer zippers having asparagines/serines packed at the interfaces and tyrosines exposed to the solvent, whereas the inner one is constituted by tyrosines. Noticeably, P-FF1 and P-FB energy differences with respect to P-FF2 decrease in the tetralayer compared to the bilayer, and the decomposition analysis (Table S5) indicates that both gas phase and solvation energy differences decrease upon enlarging the model, particularly the former one. In the case of NY7, such a decrease leads to a P-FB structure that is energetically very similar to P-FF2 (ΔE = 0.2 kcal mol−1 strand−1). Furthermore, in almost all cases, the fibril twist angle decreases when going from the bilayer to the tetralayer, i.e., larger supramolecular structures tend to planarize the fibril, which is accompanied by a significant increase in the robustness of side chain-side chain hydrogen bonds. For instance, for the most stable architecture of NY7, we identify 31 inter-sheet side chain-side chain (i-SC–SC) hydrogen bonds in the bilayer and 150 in the tetralayer, while the twist angle decreases from 15.9 to 5.4. Such behavior reminds the cooperative mechanism previously found in the polymer growth of BTA.32 
    TABLE II.

    Average relative energies and standard deviation (in brackets) in kcal mol−1 strand−1, number of inter-sheet backbone–side chain hydrogen bonds (i-BB–SC), number of inter-sheet side-chain-side chain hydrogen bonds (i-SC–SC), mean inter-sheet distances (ID), computed between the first and fourth β-sheets, in Å, and twist angles in degrees for the tetralayer systems. Relative energies are calculated with respect to the most stable architecture.
    Architecture	Zippera	ΔE	i-BB–SC	i-SC–SC	IDb	Twist
    NY7 
    P-FB1-b 	NY⋯NY⋯NY⋯NY 	0.63 (0.39) 	0 	90 	29.6 	11.2 
    P-FF1-a 	NY⋯YN⋯NY⋯YN 	2.60 (0.38) 	0 	199 	30.8 	6.1 
    P-FF2-a 	YN⋯NY⋯YN⋯NY 	0.00 (0.26) 	0 	150 	26.6 	5.4 
    NF7 
    P-FB1-a 	NF⋯NF⋯NF⋯NF 	0.93 (0.27) 	0 	0 	28.1 	6.9 
    P-FF1-a 	NF⋯FN⋯NF⋯FN 	1.23 (0.40) 	0 	10 	29.7 	6.2 
    P-FF2-b 	FN⋯NF⋯FN⋯NF 	0.00 (0.38) 	23 	41 	26.2 	5.9 
    SY7 
    AP-FEQB-a 	c 	11.70 (0.41) 	9 	30 	26.7 	157.9 
    AP-FB 	SY⋯SY⋯SY⋯SY 	11.25 (0.42) 	8 	46 	24.9 	165.7 
    AP-FF1 	SY⋯YS⋯SY⋯YS 	9.50 (0.53) 	19 	36 	26.7 	157.0 
    AP-FF2 	YS⋯SY⋯YS⋯SY 	1.03 (0.32) 	31 	50 	21.4 	165.2 
    P-FB1-b 	SY⋯SY⋯SY⋯SY 	4.92 (0.26) 	0 	242 	25.4 	8.4 
    P-FF1-a 	SY⋯YS⋯SY⋯YS 	7.07 (0.32) 	48 	191 	28.4 	14.4 
    P-FF2-b 	YS⋯SY⋯YS⋯SY 	0.00 (0.26) 	258 	32 	20.9 	59.9 
    SF7 
    AP-FEQB-a 	c 	11.60 (0.37) 	0 	2 	24.4 	145.7 
    AP-FB 	SF⋯SF⋯SF⋯SF 	14.01 (0.72) 	6 	0 	24.5 	165.4 
    AP-FF1 	SF⋯FS⋯SF⋯FS 	5.08 (0.47) 	32 	25 	25.8 	158.2 
    AP-FF2 	FS⋯SF⋯FS⋯SF 	0.00 (0.42) 	33 	39 	20.4 	169.9 
    P-FB1-a 	SF⋯SF⋯SF⋯SF 	13.80 (0.68) 	0 	0 	25.1 	13.4 
    P-FF1-a 	SF⋯FS⋯SF⋯FS 	5.40 (0.31) 	3 	78 	27.6 	6.6 
    P-FF2-a 	FS⋯SF⋯FS⋯SF 	0.16 (0.35) 	38 	156 	21.9 	9.0 
    GY7 
    AP-FEQB-a 	c 	3.46 (3.46) 	78 	0 	22.5 	171.4 
    AP-FB 	GY⋯GY⋯GY⋯GY 	10.11 (0.30) 	70 	0 	21.0 	148.2 
    AP-FF1 	GY⋯YG⋯GY⋯YG 	9.05 (0.41) 	2 	3 	25.0 	134.5 
    AP-FF2 	YG⋯GY⋯YG⋯GY 	0.00 (0.00) 	0 	7 	17.9 	170.7 
    a

    Residues in bold are exposed to the solvent.
    b

    Inter-sheet distance between the first and the fourth sheet.
    c

    See Fig. 4.

    A similar behavior is observed for SY7, for which the most stable P-FF2-UU structure of the bilayer (Table I) exhibits 69 i-SC–SC hydrogen bond contacts, whereas the analogous P-FF2-a one in the tetralayer (Table S2) shows 212 contacts, the twist angle decreasing from 37.8 to 6.2 (Table I and Table S2 of the supplementary material). For this heptapeptide, however, the most stable tetralayer organization is the P-FF2-b one, analogous to the P-FF2-UD structure of the bilayer, for which most contacts occur between the side chain of serine and the carbonyl backbone of the opposite β-sheet. The number of inter-sheet backbone–side chain (i-BB–SC) hydrogen bonds in the tetralayer (258) is roughly twice that of the bilayer (121), while the twist angle remains more or less the same (63.7 in the bilayer and 59.9 in the tetralayer). This observation suggests that the mixed backbone–side chain across-sheet interaction pattern [Fig. 3(b)] has more stringent geometric requirements than the one involving only side chains and forming linear hydrogen bond rows [Fig. 3(a)]. However, it seems reasonable to assume that due to its unique small size and hydrogen bonding capability, serine can indeed engage backbone groups into inter-sheet interactions. Indeed, it is well known that serines and tyrosines can form side chain–backbone hydrogen bonds. Such motifs are often observed in α-helices33 and are usually buried from the protein surface and shielded from solvent.

    For NF7, the most stable architecture is FN⋯NF⋯FN⋯NF with asparagines packed in the two outer zippers and tyrosines packed in the inner one and, as found for the bilayer, with phenylalanines exposed to the solvent. Similar trends to those identified for NY7 are observed when comparing the bilayer and tetralayer supramolecular structures. Energy differences between P-FF1 and P-FF2 architectures decrease (as do both the gas phase and solvation contributions, Table S3) when going from the bilayer to the tetralayer and the P-FF2 twist angle decreases. However, while the P-FF2-a architecture shows a strong intra- and inter-sheet hydrogen bond network [Fig. 3(c)] as for the bilayer P-FF2-UU, for PFF2-b, interdigitation of asparagine side chains involves backbone carbonyls in inter-sheet hydrogen bonds [Fig. 3(d)], as observed for SY7 and SF7 systems.

    For SF7, both AP-FF2-UEQD and P-FF2-UU, with phenylalanine exposed to the solvent and serine packed at the external zippers (FS⋯SF⋯FS⋯SF), exhibit very similar stabilities, the former being, however, slightly more stable, in contrast to the bilayer system for which both architectures were found to be energetically similar. This increase in stability of AP-FF2-UEQD in the tetralayer can be related to the appearance of new hydrogen bonds due to a more efficient side-chain packing of the whole fibril within this architecture. Furthermore, as found for the previous systems, relative energies of the other face-to-face organizations (P-FF1 and AP-FF1), with respect to the most stable, significantly decrease when going to the larger supramolecular system, since now relative stabilities are determined by the combination of the two types of zippers.

    Finally, results for GY7 indicate that, as found for the bilayer, the antiparallel AP-FF2-UEQD configuration, [YG⋯GY⋯YG⋯GY, Fig. 3(e)] with tyrosines exposed to the solvent, remains clearly the most stable assembly. In this architecture, the two outer zippers have glycine packed at the interface with very short inter-sheet distances (4.2 Å), while the internal one enclosing tyrosines exhibits a much larger value (11.4 Å) [Fig. 3(e)]. Because of the coexistence of both zippers, this structure does not disaggregate along the molecular dynamics trajectory, as found for the bilayer systems when tyrosines are packed at the interface. However, such a difference in inter-sheet distances between the external and the inner zippers makes this fibril more prone to disaggregate, in line with their lower stability and greater difficulty of experimental preparation.15 

    The SASA and ΔSASA values for all systems and architectures have also been computed (see Table S5). It can be observed that for NY7, NF7, and GY7, the most stable architecture is that with the smaller SASA value (or largest SASA decrease compared to the monomer). However, for SY7, the most stable P-FF2-b architecture shows a slightly larger SASA value than P-FF1-a probably due to the larger twist angle in the former. For SF7, with the preferred AP-FF2 organization, the SASA value is significantly larger than the less stable P-FF2, thereby pointing out that relative stabilities arise from a delicate balance between interactions at the zippers and fiber–solvent interactions.
    CONCLUSIONS

    We have presented a systematic molecular dynamics-based study on the atomistic architectures of self-assembled polar heptapeptides with a binary alternating pattern AB7, in which A is asparagine, serine, or glycine and B is tyrosine or phenylalanine. We have considered both the minimal system representing an individual zipper, formed by two β-sheets, and a larger supramolecular one, formed by four β-sheets for a total of three zippers.

    For NY7, NF7, SY7, and SF7, the preferred architecture of the individual zipper exhibits a parallel intra-sheet organization, with the polar residues asparagine/serine packed at the interface and tyrosine/phenylalanine exposed to the solvent. This structure is highly stabilized by the formation of a strong hydrogen bond network at the zipper. Such hydrogen bond networks are extremely tight and cooperative owing to the symmetry of the peptide sequence. In asparagine-rich zippers, these hydrogen bonds only involve side chain contacts, whereas in serine-rich zippers, they involve both side chain–side chain and side chain–backbone contacts of opposite β-sheets, the latter being responsible of the larger twist angles found in some SY7 and SF7 nanofibril structures. Although the corresponding antiparallel β-sheet organizations are always less stable, replacement of tyrosine by phenylalanine significantly reduces their relative energies with respect to the most stable parallel one, both architectures being energetically very similar for SF7. The replacement of tyrosine by phenylalanine also stabilizes the opposite face-to-face configuration, with tyrosine/phenylalanine zippers, due to the larger hydrophobic character of phenylalanine compared to tyrosine.

    For GY7, the most stable architecture shows an antiparallel β-sheet, with glycines packed at the interface and tyrosines exposed to the solvent. Such a different β-sheet organization, compared to previous systems, can be explained considering that, in the absence of side chains that are able to establish hydrogen bonds, the antiparallel organization allows maximizing intra-sheet contacts. Furthermore, due to the small size of glycine, this packing leads to a remarkable short inter-sheet distance that maximizes van der Waals interactions between the two β-sheets.

    Tetralayer supramolecular systems keep, in general, the preferred assembly found for the individual zipper. Furthermore, the following trends have been observed. First, relative energies between the two possible face-to-face organizations decrease upon enlarging our system from one (two β sheets) to three (four β-sheets) zippers. Second, in most cases, simulations with four β-sheets lead to less twisted fibril assemblies and an increased robustness of stabilizing contacts due to cooperativity. The only exceptions correspond to SF7 and SY7 architectures with a significant number of side chain–backbone contacts, for which the twist angle remains roughly the same. Finally, when two architectures of individual zippers are very close in energy, enlarging the supramolecular system can lead to an inversion of stability. In these cases, limitations of computational approaches used do not allow providing a definitive answer on which would be the preferred assembly, which, on the other hand, may also highly depend on the experimental conditions under which the fibril is synthetized.

    Overall, the present study shows how computational techniques can be an excellent complementary tool to obtain atomistic information on nanofibril architectures. This is essential to understand their catalytic activities and physico-chemical properties and design new improved artificial prion-inspired materials.

    '''

    #processText(text8)
