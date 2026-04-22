using System.Text;
using System.Text.Json;
using TACTLib.Client;
using TACTLib.Client.HandlerArgs;
using TACTLib.Core.Product.Tank;
using TankLib;
using TankLib.STU;
using TankLib.STU.Types;
using TankLib.STU.Types.Enums;

ManifestCryptoHandler.AttemptFallbackManifests = true;

string cascPath = args.Length > 0 ? args[0] : "/Volumes/172.20.31.59/BattleNet/Overwatch";
string outDir = args.Length > 1 ? args[1] : "./dump_output";
Directory.CreateDirectory(outDir);

TACTLib.Logger.OnInfo += (cat, msg) => Console.Error.WriteLine($"[INFO] {cat}: {msg}");
TACTLib.Logger.OnWarn += (cat, msg) => Console.Error.WriteLine($"[WARN] {cat}: {msg}");
TACTLib.Logger.OnError += (cat, msg) => Console.Error.WriteLine($"[ERR] {cat}: {msg}");

var clientArgs = new ClientCreateArgs {
    HandlerArgs = new ClientCreateArgs_Tank {
        ManifestRegion = ClientCreateArgs_Tank.REGION_CN,
    },
    TextLanguage = "zhCN",
    SpeechLanguage = "zhCN",
};

var client = new ClientHandler(cascPath, clientArgs);
if (client.ProductHandler is not ProductHandler_Tank tank) {
    Console.Error.WriteLine("Not Tank product"); return;
}
Console.Error.WriteLine($"Assets loaded: {tank.m_assets.Count}");

string? ReadString(ulong guid) {
    if (guid == 0) return null;
    try {
        using var s = tank.OpenFile(guid);
        return s == null ? null : ((string?)new teString(s))?.TrimEnd();
    } catch { return null; }
}

T? ReadSTU<T>(ulong guid) where T : STUInstance {
    if (guid == 0) return null;
    try {
        using var s = tank.OpenFile(guid);
        if (s == null) return null;
        return new teStructuredData(s).GetInstance<T>();
    } catch { return null; }
}

// ============ 1. Heroes + Loadouts ============
Console.Error.WriteLine("Dumping heroes...");
var heroList = new List<object>();

foreach (var kvp in tank.m_assets) {
    if (teResourceGUID.Type(kvp.Key) != 0x75) continue;
    var hero = ReadSTU<STUHero>(kvp.Key);
    if (hero == null) continue;

    var heroIdx = teResourceGUID.Index(kvp.Key);
    var heroName = ReadString(hero.m_0EDCE350);
    var isHero = hero.m_64DC571F > 0;

    var loadouts = new List<object>();
    if (hero.m_heroLoadout != null) {
        foreach (var lr in hero.m_heroLoadout) {
            if (lr == 0) continue;
            var lo = ReadSTU<STULoadout>(lr);
            if (lo == null) continue;
            var loIdx = teResourceGUID.Index(lr);
            loadouts.Add(new {
                guid_index = $"0x{loIdx:X}",
                name_zhCN = ReadString(lo.m_name),
                description_zhCN = ReadString(lo.m_description),
                category = lo.m_category.ToString(),
                category_id = (int)lo.m_category,
                button = ReadString(ReadSTU<STU_C5243F93>(lo.m_logicalButton)?.m_name),
                is_hidden = lo.m_0E679979 >= 1,
            });
        }
    }

    // ConfigVars from loadout (ability parameters)
    var configVars = new List<object>();
    if (hero.m_heroLoadout != null) {
        foreach (var lr in hero.m_heroLoadout) {
            if (lr == 0) continue;
            var lo = ReadSTU<STULoadout>(lr);
            if (lo == null) continue;
            // STULoadout has m_C59F05B1 (STU_2FA1A54E[]) which may contain config vars
        }
    }

    // Gameplay entity → statescript graph chain
    object? gameplayInfo = null;
    if (hero.m_gameplayEntity != 0) {
        var entDef = ReadSTU<STUEntityDefinition>(hero.m_gameplayEntity);
        if (entDef != null) {
            var comps = new List<object>();
            if (entDef.m_componentMap != null) {
                foreach (var comp in entDef.m_componentMap) {
                    comps.Add(new {
                        key = $"0x{comp.Key:X}",
                        type = comp.Value?.GetType().Name ?? "null",
                    });
                }
            }
            gameplayInfo = new {
                entity_guid = $"0x{teResourceGUID.Index(hero.m_gameplayEntity):X}",
                component_count = comps.Count,
                components = comps,
            };
        }
    }

    heroList.Add(new {
        hero_id = $"0x{heroIdx:X}",
        hero_id_dec = heroIdx,
        guid = $"0x{kvp.Key:X16}",
        name_zhCN = heroName,
        internal_name = hero.m_internalName?.Value,
        is_hero = isHero,
        gender = hero.m_gender.ToString(),
        size = hero.m_heroSize.ToString(),
        color = $"#{(int)(hero.m_heroColor.R*255):X2}{(int)(hero.m_heroColor.G*255):X2}{(int)(hero.m_heroColor.B*255):X2}",
        loadouts = loadouts,
        gameplay_entity = gameplayInfo,
    });
}

var heroJson = JsonSerializer.Serialize(heroList, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(Path.Combine(outDir, "heroes.json"), heroJson);
Console.Error.WriteLine($"Wrote {heroList.Count} heroes to heroes.json");

// ============ 2. All Loadouts (type 0x9E) ============
Console.Error.WriteLine("Dumping loadouts...");
var loadoutList = new List<object>();

foreach (var kvp in tank.m_assets) {
    if (teResourceGUID.Type(kvp.Key) != 0x9E) continue;
    var lo = ReadSTU<STULoadout>(kvp.Key);
    if (lo == null) continue;
    var loIdx = teResourceGUID.Index(kvp.Key);
    loadoutList.Add(new {
        loadout_id = $"0x{loIdx:X}",
        guid = $"0x{kvp.Key:X16}",
        name_zhCN = ReadString(lo.m_name),
        description_zhCN = ReadString(lo.m_description),
        category = lo.m_category.ToString(),
        category_id = (int)lo.m_category,
        button = ReadString(ReadSTU<STU_C5243F93>(lo.m_logicalButton)?.m_name),
        is_hidden = lo.m_0E679979 >= 1,
        texture_guid = lo.m_texture != 0 ? $"0x{teResourceGUID.Index(lo.m_texture):X}" : null,
    });
}

File.WriteAllText(Path.Combine(outDir, "loadouts.json"),
    JsonSerializer.Serialize(loadoutList, new JsonSerializerOptions { WriteIndented = true }));
Console.Error.WriteLine($"Wrote {loadoutList.Count} loadouts to loadouts.json");

// ============ 3. Statescript via entity definition → StatescriptComponent → graph GUIDs ============
Console.Error.WriteLine("Dumping statescript data...");
var ssDataList = new List<object>();

foreach (var kvp in tank.m_assets) {
    if (teResourceGUID.Type(kvp.Key) != 0x75) continue;
    var hero = ReadSTU<STUHero>(kvp.Key);
    if (hero == null || hero.m_64DC571F == 0 || hero.m_gameplayEntity == 0) continue;

    var heroIdx = teResourceGUID.Index(kvp.Key);
    var heroName = ReadString(hero.m_0EDCE350);

    try {
        using var entStream = tank.OpenFile(hero.m_gameplayEntity);
        if (entStream == null) continue;
        var entStu = new teStructuredData(entStream);
        var entDef = entStu.GetInstance<STUEntityDefinition>();
        if (entDef?.m_componentMap == null) continue;

        // Find STUStatescriptComponent in entity definition
        STUStatescriptComponent? ssComp = null;
        foreach (var comp in entDef.m_componentMap) {
            if (comp.Value is STUStatescriptComponent sc) { ssComp = sc; break; }
        }
        if (ssComp == null) continue;

        // Collect graph references and overrides
        var graphEntries = new List<object>();
        if (ssComp.m_B634821A != null) {
            foreach (var gwo in ssComp.m_B634821A) {
                var graphGuid = (ulong)gwo.m_graph;

                // Try to load the actual graph
                var syncVars = new List<object>();
                var schemaEntries = new List<object>();
                if (graphGuid != 0) {
                    var graph = ReadSTU<STUStatescriptGraph>(graphGuid);
                    if (graph != null) {
                        if (graph.m_syncVars != null) {
                            foreach (var sv in graph.m_syncVars) {
                                syncVars.Add(new {
                                    identifier_guid = sv.m_0D09D2D9 != 0 ? $"0x{(ulong)sv.m_0D09D2D9:X16}" : null,
                                    identifier_index = sv.m_0D09D2D9 != 0 ? $"0x{teResourceGUID.Index(sv.m_0D09D2D9):X}" : null,
                                    type = sv.m_56341592.ToString(),
                                    flag = sv.m_AC9480C7,
                                });
                            }
                        }
                        if (graph.m_publicSchema?.m_entries != null) {
                            foreach (var se in graph.m_publicSchema.m_entries) {
                                schemaEntries.Add(new {
                                    identifier_guid = se.m_0D09D2D9 != 0 ? $"0x{(ulong)se.m_0D09D2D9:X16}" : null,
                                    identifier_index = se.m_0D09D2D9 != 0 ? $"0x{teResourceGUID.Index(se.m_0D09D2D9):X}" : null,
                                    value_type = se.m_value?.GetType().Name,
                                });
                            }
                        }
                    }
                }

                // Extract node → var bindings via RemoteSyncVar + node labels
                var nodeVarRefs = new List<object>();
                int _nodeTotal = 0, _nodeNull = 0;
                if (graphGuid != 0) {
                    var graph2 = ReadSTU<STUStatescriptGraph>(graphGuid);
                    _nodeTotal = graph2?.m_nodes?.Length ?? -1;
                    if (graph2?.m_nodes != null) {
                        foreach (var node in graph2.m_nodes) {
                            if (node == null) { _nodeNull++; continue; }
                            var nodeType = node.GetType().Name;
                            var nodeLabel = node.m_049CA107?.Value?.TrimEnd('\0')?.Trim();
                            var nodeId = node.m_uniqueID;

                            // m_BF5B22B7 = output sync vars, m_8BF03679 = input sync vars
                            var writeVars = new List<object>();
                            if (node.m_BF5B22B7 != null) {
                                foreach (var sv in node.m_BF5B22B7) {
                                    if (sv?.m_0D09D2D9 != 0) writeVars.Add(new {
                                        identifier_index = $"0x{teResourceGUID.Index(sv.m_0D09D2D9):X}",
                                        type = sv.m_56341592.ToString(),
                                    });
                                }
                            }
                            var readVars = new List<object>();
                            if (node.m_8BF03679 != null) {
                                foreach (var sv in node.m_8BF03679) {
                                    if (sv?.m_0D09D2D9 != 0) readVars.Add(new {
                                        identifier_index = $"0x{teResourceGUID.Index(sv.m_0D09D2D9):X}",
                                        type = sv.m_56341592.ToString(),
                                    });
                                }
                            }

                            if (writeVars.Count > 0 || readVars.Count > 0 || !string.IsNullOrEmpty(nodeLabel)) {
                                nodeVarRefs.Add(new {
                                    node_type = nodeType, node_id = nodeId, label = nodeLabel,
                                    writes = writeVars, reads = readVars,
                                });
                            }

                            // Also keep old ConfigVar extraction for non-sync data
                            var nodeType2 = nodeType;
                            var refs = new List<object>();

                            // Recursively extract all ConfigVar references from node
                            void ExtractVarRefs(object obj, string prefix, int depth) {
                                if (obj == null || depth > 4) return;
                                foreach (var field in obj.GetType().GetFields()) {
                                    var val = field.GetValue(obj);
                                    if (val == null) continue;
                                    var fname = prefix + field.Name;

                                    if (val is STUConfigVar cv) {
                                        var cvTypeName = cv.GetType().Name;
                                        var guid = cv.m_EE729DCB;
                                        bool isDynamic = cvTypeName == "STU_076E0DBA" || cvTypeName == "STUConfigVarDynamic";

                                        if (guid != 0 || isDynamic) {
                                            string role = fname.Contains("Cooldown") ? "cooldown" :
                                                          fname.Contains("Stack") ? "stack" :
                                                          fname.Contains("out_") ? "output_var" :
                                                          fname.Contains("modifyHealth") ? "health_mod" :
                                                          isDynamic ? "dynamic_ref" : "config";
                                            refs.Add(new {
                                                field = fname, field_type = cvTypeName, role,
                                                identifier_index = guid != 0 && teResourceGUID.Type(guid) == 0x1C
                                                    ? $"0x{teResourceGUID.Index(guid):X}" : null,
                                                guid = guid != 0 ? $"0x{guid:X16}" : null,
                                            });
                                        }
                                    }

                                    if (val is STUStatescriptAbilityVarToSet[] varSets) {
                                        int vi = 0;
                                        foreach (var vs in varSets) {
                                            if (vs?.m_out_Var != null) ExtractVarRefs(vs.m_out_Var, $"{fname}[{vi}].out_Var.", depth+1);
                                            if (vs?.m_value != null) ExtractVarRefs(vs.m_value, $"{fname}[{vi}].value.", depth+1);
                                            vi++;
                                        }
                                    }

                                    // Recurse into STU sub-objects that might contain more ConfigVars
                                    if (val is STUInstance subInst && !(val is STUConfigVar) && depth < 3) {
                                        ExtractVarRefs(subInst, fname + ".", depth + 1);
                                    }
                                }
                            }
                            ExtractVarRefs(node, "", 0);

                            if (refs.Count > 0) {
                                nodeVarRefs.Add(new {
                                    node_type = nodeType,
                                    var_refs = refs,
                                });
                            }
                        }
                    }
                }

                // Overrides from GraphWithOverrides
                var overrides = new List<object>();
                if (gwo.m_1EB5A024 != null) {
                    foreach (var ov in gwo.m_1EB5A024) {
                        overrides.Add(new {
                            identifier_guid = ov.m_0D09D2D9 != 0 ? $"0x{(ulong)ov.m_0D09D2D9:X16}" : null,
                            identifier_index = ov.m_0D09D2D9 != 0 ? $"0x{teResourceGUID.Index(ov.m_0D09D2D9):X}" : null,
                            value_type = ov.m_value?.GetType().Name,
                        });
                    }
                }

                graphEntries.Add(new {
                    graph_guid = graphGuid != 0 ? $"0x{graphGuid:X16}" : null,
                    graph_index = graphGuid != 0 ? $"0x{teResourceGUID.Index(graphGuid):X}" : null,
                    sync_var_count = syncVars.Count,
                    sync_vars = syncVars,
                    schema_count = schemaEntries.Count,
                    public_schema = schemaEntries,
                    override_count = overrides.Count,
                    overrides = overrides,
                    node_var_ref_count = nodeVarRefs.Count,
                    node_var_refs = nodeVarRefs,
                    _diag_nodes_total = _nodeTotal,
                    _diag_nodes_null = _nodeNull,
                });
            }
        }

        // Component-level schema
        var compSchema = new List<object>();
        if (ssComp.m_2D9815BA?.m_entries != null) {
            foreach (var se in ssComp.m_2D9815BA.m_entries) {
                compSchema.Add(new {
                    identifier_guid = se.m_0D09D2D9 != 0 ? $"0x{(ulong)se.m_0D09D2D9:X16}" : null,
                    identifier_index = se.m_0D09D2D9 != 0 ? $"0x{teResourceGUID.Index(se.m_0D09D2D9):X}" : null,
                    value_type = se.m_value?.GetType().Name,
                });
            }
        }

        ssDataList.Add(new {
            hero_id = $"0x{heroIdx:X}",
            hero_name = heroName,
            graph_count = graphEntries.Count,
            graphs = graphEntries,
            component_schema_count = compSchema.Count,
            component_schema = compSchema,
            client_only = ssComp.m_clientOnly,
        });
    } catch (Exception ex) {
        Console.Error.WriteLine($"  Hero 0x{heroIdx:X} ss error: {ex.Message}");
    }
}

File.WriteAllText(Path.Combine(outDir, "statescript_data.json"),
    JsonSerializer.Serialize(ssDataList, new JsonSerializerOptions { WriteIndented = true }));
Console.Error.WriteLine($"Wrote {ssDataList.Count} hero statescript data to statescript_data.json");

// ============ 4. Resolve all identifier GUIDs to names ============
Console.Error.WriteLine("Resolving identifier names...");

// Collect all unique identifier GUIDs from statescript data
var idSet = new HashSet<ulong>();
foreach (var h in ssDataList) {
    // ssDataList items are anonymous types, need to cast via JSON round-trip
}

// Simpler: re-scan the JSON to collect identifier indices, then resolve
var ssJson = JsonSerializer.Deserialize<JsonElement>(
    File.ReadAllText(Path.Combine(outDir, "statescript_data.json")));

foreach (var hero in ssJson.EnumerateArray()) {
    foreach (var graph in hero.GetProperty("graphs").EnumerateArray()) {
        foreach (var sv in graph.GetProperty("sync_vars").EnumerateArray()) {
            if (sv.TryGetProperty("identifier_guid", out var g) && g.ValueKind == JsonValueKind.String) {
                var guidStr = g.GetString()!;
                if (ulong.TryParse(guidStr.Replace("0x",""), System.Globalization.NumberStyles.HexNumber, null, out var guid))
                    idSet.Add(guid);
            }
        }
        foreach (var prop in new[]{"public_schema","overrides"}) {
            if (graph.TryGetProperty(prop, out var arr)) {
                foreach (var se in arr.EnumerateArray()) {
                    if (se.TryGetProperty("identifier_guid", out var g) && g.ValueKind == JsonValueKind.String) {
                        var guidStr = g.GetString()!;
                        if (ulong.TryParse(guidStr.Replace("0x",""), System.Globalization.NumberStyles.HexNumber, null, out var guid))
                            idSet.Add(guid);
                    }
                }
            }
        }
    }
}
Console.Error.WriteLine($"Unique identifier GUIDs to resolve: {idSet.Count}");

// Try multiple resolution strategies:
// 1. Read as teString (type 0x7C string format: [1byte][1byte][UTF8])
// 2. Read raw bytes and look for ASCII
var idNames = new Dictionary<string, object>();
int resolved = 0, failed = 0;
foreach (var guid in idSet) {
    var idx = teResourceGUID.Index(guid);
    var idxStr = $"0x{idx:X}";

    // Strategy 1: try to read as-is from CASC
    try {
        using var s = tank.OpenFile(guid);
        if (s != null) {
            var bytes = new byte[s.Length];
            s.Read(bytes, 0, bytes.Length);

            // Try teString format: skip first 2 bytes, rest is UTF-8
            string? name = null;
            if (bytes.Length > 2) {
                name = Encoding.UTF8.GetString(bytes, 2, bytes.Length - 2).TrimEnd('\0').Trim();
            }
            if (string.IsNullOrEmpty(name) && bytes.Length > 0) {
                name = Encoding.UTF8.GetString(bytes).TrimEnd('\0').Trim();
            }

            if (!string.IsNullOrEmpty(name)) {
                idNames[idxStr] = new { index = idxStr, guid = $"0x{guid:X16}", name = name };
                resolved++;
                continue;
            }
        }
    } catch { }

    // Strategy 2: try reading the GUID as STU instance
    try {
        var stu_id = ReadSTU<STUIdentifier>(guid);
        // STUIdentifier is empty, but try to get name from parent fields
    } catch { }

    failed++;
    idNames[idxStr] = new { index = idxStr, guid = $"0x{guid:X16}", name = (string?)null };
}

File.WriteAllText(Path.Combine(outDir, "identifier_names.json"),
    JsonSerializer.Serialize(idNames.Values.ToList(), new JsonSerializerOptions { WriteIndented = true }));
Console.Error.WriteLine($"Resolved: {resolved}, Unresolved: {failed}");
Console.Error.WriteLine("Done!");
