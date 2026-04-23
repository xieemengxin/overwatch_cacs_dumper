using System.Text;
using System.Text.Encodings.Web;
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

// JSON 输出配置：CJK 直接输出 UTF-8，不转义
var jsonOpt = new JsonSerializerOptions {
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};

string? ReadString(ulong guid) {
    if (guid == 0) return null;
    try {
        using var s = tank.OpenFile(guid);
        return s == null ? null : ((string?)new teString(s))?.TrimEnd('\0', ' ', '\n', '\r');
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

var heroJson = JsonSerializer.Serialize(heroList, jsonOpt);
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
    JsonSerializer.Serialize(loadoutList, jsonOpt));
Console.Error.WriteLine($"Wrote {loadoutList.Count} loadouts to loadouts.json");

// ============ 3. Statescript via entity definition → StatescriptComponent → graph GUIDs ============
Console.Error.WriteLine("Dumping entity & statescript data...");
var componentList = new List<object>();
var graphDataList = new List<object>();

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

        // Generic component field extractor — recursively dumps ALL field values
        List<object> DumpFields(object obj, int depth = 0) {
            var result = new List<object>();
            if (obj == null || depth > 4) return result;
            foreach (var field in obj.GetType().GetFields()) {
                var val = field.GetValue(obj);
                if (val == null) continue;
                var fname = field.Name;
                if (fname.StartsWith("m_posIn") || fname == "Usage") continue;

                // Primitives
                if (val is float fv3) { if (fv3 != 0) result.Add(new { field = fname, type = "float", value = (object)fv3 }); }
                else if (val is double dv) { if (dv != 0) result.Add(new { field = fname, type = "double", value = (object)dv }); }
                else if (val is int iv3) { result.Add(new { field = fname, type = "int", value = (object)iv3 }); }
                else if (val is uint uiv) { if (uiv != 0) result.Add(new { field = fname, type = "uint", value = (object)uiv }); }
                else if (val is long lv) { if (lv != 0) result.Add(new { field = fname, type = "long", value = (object)lv }); }
                else if (val is ulong ulv) { if (ulv != 0) result.Add(new { field = fname, type = "ulong", value = (object)$"0x{ulv:X}" }); }
                else if (val is byte bv3) { if (bv3 != 0) result.Add(new { field = fname, type = "byte", value = (object)bv3 }); }
                else if (val is bool bov) { if (bov) result.Add(new { field = fname, type = "bool", value = (object)true }); }
                else if (val is teString ts2) {
                    var sv = ts2.Value?.TrimEnd('\0')?.Trim();
                    if (!string.IsNullOrEmpty(sv)) result.Add(new { field = fname, type = "string", value = (object)sv });
                }
                // GUID / Asset references
                else if (val is teResourceGUID guid2 && (ulong)guid2 != 0) {
                    result.Add(new { field = fname, type = "guid",
                        value = (object)$"0x{teResourceGUID.Index(guid2):X}.{teResourceGUID.Type(guid2):X3}" });
                }
                // Enums
                else if (val.GetType().IsEnum) {
                    result.Add(new { field = fname, type = "enum", value = (object)$"{val}" });
                }
                // teStructuredDataAssetRef<T> → extract GUID
                else if (val is ISerializable_STU serSTU) {
                    var guidField = val.GetType().GetField("GUID");
                    if (guidField?.GetValue(val) is teResourceGUID arefGuid && (ulong)arefGuid != 0) {
                        result.Add(new { field = fname, type = "asset_ref",
                            value = (object)$"0x{teResourceGUID.Index(arefGuid):X}.{teResourceGUID.Type(arefGuid):X3}" });
                    }
                }
                // STU sub-instances
                else if (val is STUInstance sub2 && depth < 3) {
                    var subFields = DumpFields(sub2, depth + 1);
                    if (subFields.Count > 0)
                        result.Add(new { field = fname, type = sub2.GetType().Name, children = subFields });
                }
                // Arrays of STU instances
                else if (val is STUInstance[] arrStu && arrStu.Length > 0 && depth < 2) {
                    var items = new List<object>();
                    for (int ai2 = 0; ai2 < arrStu.Length && ai2 < 16; ai2++) {
                        if (arrStu[ai2] == null) continue;
                        var sf = DumpFields(arrStu[ai2], depth + 1);
                        if (sf.Count > 0) items.Add(new { index = ai2, type = arrStu[ai2].GetType().Name, fields = sf });
                    }
                    if (items.Count > 0)
                        result.Add(new { field = fname, type = "array", count = arrStu.Length, items });
                }
                // Arrays of asset refs
                else if (val is Array arrGen && arrGen.Length > 0 && arrGen.GetType().GetElementType()?.IsAssignableTo(typeof(ISerializable_STU)) == true) {
                    var refs = new List<string>();
                    foreach (var item in arrGen) {
                        var gf = item?.GetType().GetField("GUID");
                        if (gf?.GetValue(item) is teResourceGUID rg && (ulong)rg != 0)
                            refs.Add($"0x{teResourceGUID.Index(rg):X}.{teResourceGUID.Type(rg):X3}");
                    }
                    if (refs.Count > 0)
                        result.Add(new { field = fname, type = "guid_array", count = refs.Count, value = (object)refs });
                }
            }
            return result;
        }

        // Extract ALL entity components
        var componentDump = new List<object>();
        foreach (var comp in entDef.m_componentMap) {
            if (comp.Value == null) continue;
            var compType = comp.Value.GetType().Name;
            var fields = DumpFields(comp.Value);
            componentDump.Add(new {
                component_key = $"0x{comp.Key:X8}",
                component_type = compType,
                field_count = fields.Count,
                fields = fields,
            });
        }

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

                // Extract full node data: sync var bindings + ConfigVar literal values
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

                            // Sync var bindings (writes/reads)
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

                            // Extract ALL ConfigVar literal values from node fields
                            var configValues = new List<object>();
                            void ExtractValues(object obj, string prefix, int depth) {
                                if (obj == null || depth > 3) return;
                                foreach (var field in obj.GetType().GetFields()) {
                                    var val = field.GetValue(obj);
                                    if (val == null) continue;
                                    var fname = prefix + field.Name;

                                    if (val is STUConfigVarFloat fv) {
                                        configValues.Add(new { field = fname, type = "float", value = (object)fv.m_value });
                                    } else if (val is STUConfigVarInt iv) {
                                        configValues.Add(new { field = fname, type = "int", value = (object)iv.m_value });
                                    } else if (val is STUConfigVarBool bv) {
                                        configValues.Add(new { field = fname, type = "bool", value = (object)(bv.m_value != 0) });
                                    } else if (val is STUConfigVar cv) {
                                        var cvType = cv.GetType().Name;
                                        bool isDynamic = cvType == "STU_076E0DBA" || cvType == "STUConfigVarDynamic";
                                        if (isDynamic && cv.m_EE729DCB != 0) {
                                            var g2 = cv.m_EE729DCB;
                                            configValues.Add(new { field = fname, type = "dynamic",
                                                value = (object)(teResourceGUID.Type(g2) == 0x1C ? $"var:0x{teResourceGUID.Index(g2):X}" : $"ref:0x{g2:X}") });
                                        } else if (isDynamic) {
                                            configValues.Add(new { field = fname, type = "dynamic", value = (object)"(bound)" });
                                        }
                                    }

                                    // Recurse into sub-objects (but not ConfigVar which we handled above)
                                    if (val is STUInstance sub && !(val is STUConfigVar) && depth < 2) {
                                        ExtractValues(sub, fname + ".", depth + 1);
                                    }
                                    // Handle arrays of STU instances
                                    if (val is STUInstance[] arr) {
                                        for (int ai = 0; ai < arr.Length && ai < 8; ai++) {
                                            if (arr[ai] != null) ExtractValues(arr[ai], $"{fname}[{ai}].", depth + 1);
                                        }
                                    }
                                }
                            }
                            ExtractValues(node, "", 0);

                            if (writeVars.Count > 0 || readVars.Count > 0 || configValues.Count > 0 || !string.IsNullOrEmpty(nodeLabel)) {
                                nodeVarRefs.Add(new {
                                    node_type = nodeType, node_id = nodeId, label = nodeLabel,
                                    writes = writeVars, reads = readVars,
                                    config = configValues,
                                });
                            }

                            // Legacy code compatibility marker
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

        // Collect per-hero data into separate lists
        componentList.Add(new {
            hero_id = $"0x{heroIdx:X}",
            hero_name = heroName,
            components = componentDump,
        });

        graphDataList.Add(new {
            hero_id = $"0x{heroIdx:X}",
            hero_name = heroName,
            graph_count = graphEntries.Count,
            graphs = graphEntries,
            component_schema_count = compSchema.Count,
            component_schema = compSchema,
            client_only = ssComp.m_clientOnly,
        });
    } catch (Exception ex) {
        Console.Error.WriteLine($"  Hero 0x{heroIdx:X} error: {ex.Message}");
    }
}

// Write split output files
File.WriteAllText(Path.Combine(outDir, "entity_components.json"),
    JsonSerializer.Serialize(componentList, jsonOpt));
Console.Error.WriteLine($"Wrote {componentList.Count} heroes to entity_components.json");

// Split graphs: sync_vars + schema into one file, node details into another
var graphSyncList = new List<object>();
var graphNodeList = new List<object>();
foreach (var gd in graphDataList) {
    // Use reflection-free approach: serialize then re-parse
    var je = JsonSerializer.SerializeToElement(gd, jsonOpt);
    var hid = je.GetProperty("hero_id").GetString();
    var hname = je.GetProperty("hero_name").GetString();

    var syncGraphs = new List<object>();
    var nodeGraphs = new List<object>();
    foreach (var g in je.GetProperty("graphs").EnumerateArray()) {
        // Sync vars + schema (lightweight)
        syncGraphs.Add(new {
            graph_guid = g.TryGetProperty("graph_guid", out var gg) ? gg.GetString() : null,
            graph_index = g.TryGetProperty("graph_index", out var gi) ? gi.GetString() : null,
            sync_var_count = g.GetProperty("sync_var_count").GetInt32(),
            sync_vars = JsonSerializer.Deserialize<JsonElement>(g.GetProperty("sync_vars").GetRawText()),
            schema_count = g.GetProperty("schema_count").GetInt32(),
            public_schema = JsonSerializer.Deserialize<JsonElement>(g.GetProperty("public_schema").GetRawText()),
            override_count = g.GetProperty("override_count").GetInt32(),
            overrides = JsonSerializer.Deserialize<JsonElement>(g.GetProperty("overrides").GetRawText()),
        });
        // Node details (heavy)
        nodeGraphs.Add(new {
            graph_index = g.TryGetProperty("graph_index", out var gi2) ? gi2.GetString() : null,
            node_var_ref_count = g.GetProperty("node_var_ref_count").GetInt32(),
            node_var_refs = JsonSerializer.Deserialize<JsonElement>(g.GetProperty("node_var_refs").GetRawText()),
        });
    }

    graphSyncList.Add(new { hero_id = hid, hero_name = hname, graphs = syncGraphs });
    graphNodeList.Add(new { hero_id = hid, hero_name = hname, graphs = nodeGraphs });
}

File.WriteAllText(Path.Combine(outDir, "graph_sync_vars.json"),
    JsonSerializer.Serialize(graphSyncList, jsonOpt));
Console.Error.WriteLine($"Wrote {graphSyncList.Count} heroes to graph_sync_vars.json");

File.WriteAllText(Path.Combine(outDir, "graph_nodes.json"),
    JsonSerializer.Serialize(graphNodeList, jsonOpt));
Console.Error.WriteLine($"Wrote {graphNodeList.Count} heroes to graph_nodes.json");

// ============ 3.5. Graph Topology Dump (nodes + plugs + links + config tree) ============
// 这个 dump 是给蓝图编辑器用的,保留完整 graph 拓扑:
//   - 所有 nodes (每个 node 的 type, label, writes/reads, input/output plugs, config tree)
//   - 所有 links (plug-to-plug 连接)
//   - entries / states 列表
// 每个 plug 有稳定 ID:"hero:graph:node:field[idx]"
Console.Error.WriteLine("Dumping graph topology (full nodes + plugs + links)...");
var topologyList = new List<object>();

// 收集 plug 子节点(field 中嵌入的 STUGraphPlug 实例)
List<(string field, object plug)> CollectPlugs(object node) {
    var result = new List<(string, object)>();
    foreach (var f in node.GetType().GetFields()) {
        var v = f.GetValue(node);
        if (v == null) continue;
        if (v is STUGraphPlug singlePlug) {
            result.Add((f.Name, singlePlug));
        } else if (v is Array arr) {
            var et = arr.GetType().GetElementType();
            if (et != null && (et == typeof(STUGraphPlug) || et.IsSubclassOf(typeof(STUGraphPlug)))) {
                for (int i = 0; i < arr.Length; i++) {
                    var item = arr.GetValue(i);
                    if (item != null) result.Add(($"{f.Name}[{i}]", item));
                }
            }
        }
    }
    return result;
}

// helper: resolve asset GUID to a human-readable name where possible.
// Returns (resolved_name, resolved_kind) or (null, null).
(string?, string?) ResolveAsset(ulong guid) {
    if (guid == 0) return (null, null);
    var type = teResourceGUID.Type(guid);
    try {
        if (type == 0x7C) {
            // locale string
            var s = ReadString(guid);
            if (!string.IsNullOrEmpty(s)) return (s, "string");
        } else if (type == 0x9E) {
            // STULoadout
            var lo = ReadSTU<STULoadout>(guid);
            if (lo != null) {
                var n = ReadString(lo.m_name);
                return (n ?? "(loadout)", "loadout");
            }
        } else if (type == 0x75) {
            // STUHero
            var h = ReadSTU<STUHero>(guid);
            if (h != null) {
                var n = ReadString(h.m_0EDCE350);
                return (n ?? "(hero)", "hero");
            }
        }
    } catch { }
    return (null, null);
}

// 简化 ConfigVar 子树:递归到 depth=2,捕获 literal/dynamic/array + enum_type + asset name lookup
List<object> DumpConfigTree(object obj, int depth = 0) {
    var result = new List<object>();
    if (obj == null || depth > 3) return result;
    foreach (var f in obj.GetType().GetFields()) {
        var v = f.GetValue(f.IsStatic ? null : obj);
        if (v == null) continue;
        var fname = f.Name;
        if (fname.StartsWith("m_posIn") || fname == "Usage") continue;

        // skip plugs (handled separately)
        if (v is STUGraphPlug) continue;
        if (v is Array a0 && a0.GetType().GetElementType() is Type et0 &&
            (et0 == typeof(STUGraphPlug) || et0.IsSubclassOf(typeof(STUGraphPlug)))) continue;

        // ConfigVar literal values
        if (v is STUConfigVarFloat fv) result.Add(new { field = fname, kind = "float", value = (object)fv.m_value });
        else if (v is STUConfigVarInt iv) result.Add(new { field = fname, kind = "int", value = (object)iv.m_value });
        else if (v is STUConfigVarBool bv) result.Add(new { field = fname, kind = "bool", value = (object)(bv.m_value != 0) });
        else if (v is STUConfigVar cv) {
            var cvType = cv.GetType().Name;
            bool isDynamic = cvType == "STU_076E0DBA" || cvType == "STUConfigVarDynamic";
            // m_identifier (uint16 var id) — directly embedded var reference
            uint embeddedVarId = 0;
            try {
                var idField = cv.GetType().GetField("m_identifier");
                if (idField != null) {
                    var idVal = idField.GetValue(cv);
                    if (idVal != null) embeddedVarId = Convert.ToUInt32(idVal);
                }
            } catch { }
            if (isDynamic && cv.m_EE729DCB != 0) {
                var g = cv.m_EE729DCB;
                bool isVar = teResourceGUID.Type(g) == 0x1C;
                if (isVar) {
                    result.Add(new { field = fname, kind = "var_ref", cv_type = cvType,
                        var_id = $"0x{teResourceGUID.Index(g):X}" });
                } else {
                    var (rname, rkind) = ResolveAsset(g);
                    result.Add(new { field = fname, kind = "asset_ref", cv_type = cvType,
                        asset_guid = $"0x{teResourceGUID.Index(g):X}.{teResourceGUID.Type(g):X3}",
                        resolved_name = rname, resolved_kind = rkind });
                }
            } else if (embeddedVarId != 0) {
                // direct var reference via m_identifier
                result.Add(new { field = fname, kind = "var_ref", cv_type = cvType,
                    var_id = $"0x{embeddedVarId:X}" });
            } else {
                // unresolved ConfigVar: try recurse
                if (depth < 2) {
                    var sub = DumpConfigTree(cv, depth + 1);
                    if (sub.Count > 0)
                        result.Add(new { field = fname, kind = "configvar", cv_type = cvType, children = sub });
                }
            }
        }
        // Primitive
        else if (v is float || v is double || v is int || v is uint || v is long || v is ulong || v is byte || v is short || v is ushort) {
            result.Add(new { field = fname, kind = "primitive", value = v });
        }
        else if (v is bool bbv) {
            if (bbv) result.Add(new { field = fname, kind = "bool", value = (object)true });
        }
        else if (v is teString ts) {
            var s = ts.Value?.TrimEnd('\0');
            if (!string.IsNullOrEmpty(s)) result.Add(new { field = fname, kind = "string", value = (object)s });
        }
        else if (v.GetType().IsEnum) {
            // enum_type lets the UI categorize; value is the OWLib field name (often xHASH).
            result.Add(new { field = fname, kind = "enum",
                enum_type = v.GetType().Name,
                value = (object)v.ToString() });
        }
        // Asset reference (raw GUID)
        else if (v is teResourceGUID g2 && (ulong)g2 != 0) {
            var (rname, rkind) = ResolveAsset((ulong)g2);
            result.Add(new { field = fname, kind = "guid",
                value = (object)$"0x{teResourceGUID.Index(g2):X}.{teResourceGUID.Type(g2):X3}",
                resolved_name = rname, resolved_kind = rkind });
        }
        else if (v is ISerializable_STU sa) {
            var gf = v.GetType().GetField("GUID");
            if (gf?.GetValue(v) is teResourceGUID ag && (ulong)ag != 0) {
                var (rname, rkind) = ResolveAsset((ulong)ag);
                result.Add(new { field = fname, kind = "asset_ref",
                    asset_guid = $"0x{teResourceGUID.Index(ag):X}.{teResourceGUID.Type(ag):X3}",
                    resolved_name = rname, resolved_kind = rkind });
            }
        }
        // Recurse into STU sub-instance
        else if (v is STUInstance sub && depth < 2) {
            var subFields = DumpConfigTree(sub, depth + 1);
            if (subFields.Count > 0)
                result.Add(new { field = fname, kind = "struct", type = sub.GetType().Name, children = subFields });
        }
        // Array of STU instances (configvar arrays)
        else if (v is STUInstance[] arr2 && arr2.Length > 0 && depth < 2) {
            var items = new List<object>();
            for (int i = 0; i < arr2.Length && i < 8; i++) {
                if (arr2[i] == null) continue;
                var sf = DumpConfigTree(arr2[i], depth + 1);
                if (sf.Count > 0) items.Add(new { index = i, type = arr2[i].GetType().Name, fields = sf });
            }
            if (items.Count > 0)
                result.Add(new { field = fname, kind = "array", count = arr2.Length, items });
        }
    }
    return result;
}

foreach (var kvp in tank.m_assets) {
    if (teResourceGUID.Type(kvp.Key) != 0x75) continue;
    var hero = ReadSTU<STUHero>(kvp.Key);
    if (hero == null || hero.m_64DC571F == 0 || hero.m_gameplayEntity == 0) continue;
    var heroIdx = teResourceGUID.Index(kvp.Key);
    var heroName = ReadString(hero.m_0EDCE350);

    STUEntityDefinition? entDef;
    try { entDef = ReadSTU<STUEntityDefinition>(hero.m_gameplayEntity); }
    catch { continue; }
    if (entDef?.m_componentMap == null) continue;

    STUStatescriptComponent? ssC = null;
    foreach (var c in entDef.m_componentMap)
        if (c.Value is STUStatescriptComponent sc) { ssC = sc; break; }
    if (ssC?.m_B634821A == null) continue;

    var heroGraphs = new List<object>();
    int gi = 0;
    foreach (var gwo in ssC.m_B634821A) {
        var graphGuid = (ulong)gwo.m_graph;
        if (graphGuid == 0) continue;
        STUStatescriptGraph? graph;
        try { graph = ReadSTU<STUStatescriptGraph>(graphGuid); } catch { continue; }
        if (graph == null) continue;

        var gIdx = teResourceGUID.Index(graphGuid);
        var prefix = $"h{heroIdx:X}:g{gIdx:X}";

        // Pass 1: build object→(node_idx, field) map for link resolution.
        // NOTE: empty/default plug instances may be SHARED across nodes (singleton).
        // We only register a plug object if it actually has links — that ensures
        // the lookup target during link resolution is meaningful.
        var plugObjToOwner = new Dictionary<object, (int nodeIdx, string field)>(ReferenceEqualityComparer.Instance);
        if (graph.m_nodes != null) {
            for (int ni = 0; ni < graph.m_nodes.Length; ni++) {
                var node = graph.m_nodes[ni];
                if (node == null) continue;
                foreach (var (field, plug) in CollectPlugs(node)) {
                    var p = (STUGraphPlug)plug;
                    // Only map plugs that have outgoing links (the link target side
                    // — the "input" plug of a link — is the one we need to resolve).
                    if (p.m_links != null && p.m_links.Length > 0) {
                        if (!plugObjToOwner.ContainsKey(plug))
                            plugObjToOwner[plug] = (ni, field);
                    }
                }
            }
        }
        // Second pass: also register plug objects that appear as link.m_inputPlug,
        // so we can resolve "to" side. Walk all links once.
        if (graph.m_nodes != null) {
            for (int ni = 0; ni < graph.m_nodes.Length; ni++) {
                var node = graph.m_nodes[ni];
                if (node == null) continue;
                foreach (var (field, plug) in CollectPlugs(node)) {
                    var p = (STUGraphPlug)plug;
                    if (p.m_links == null) continue;
                    foreach (var lk in p.m_links) {
                        if (lk == null) continue;
                        var other = (lk.m_outputPlug == plug) ? lk.m_inputPlug : lk.m_outputPlug;
                        if (other == null) continue;
                        if (plugObjToOwner.ContainsKey(other)) continue;
                        // search for owner of `other` plug
                        for (int nj = 0; nj < graph.m_nodes.Length; nj++) {
                            var nodeJ = graph.m_nodes[nj];
                            if (nodeJ == null) continue;
                            foreach (var (jf, jp) in CollectPlugs(nodeJ)) {
                                if (object.ReferenceEquals(jp, other)) {
                                    plugObjToOwner[other] = (nj, jf);
                                    goto found;
                                }
                            }
                        }
                        found: ;
                    }
                }
            }
        }

        // Build entries / states index sets (subset of m_nodes by reference)
        var entryIndices = new HashSet<int>();
        var stateIndices = new HashSet<int>();
        if (graph.m_nodes != null) {
            var nodeIdx = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
            for (int ni = 0; ni < graph.m_nodes.Length; ni++)
                if (graph.m_nodes[ni] != null) nodeIdx[graph.m_nodes[ni]] = ni;
            if (graph.m_entries != null)
                foreach (var e in graph.m_entries)
                    if (e != null && nodeIdx.TryGetValue(e, out var idx)) entryIndices.Add(idx);
            if (graph.m_states != null)
                foreach (var s in graph.m_states)
                    if (s != null && nodeIdx.TryGetValue(s, out var idx)) stateIndices.Add(idx);
        }

        // Pass 2: dump nodes with full plug + link info
        var nodes = new List<object>();
        var allLinks = new List<object>();  // also flat list of links for top-level access

        if (graph.m_nodes != null) {
            for (int ni = 0; ni < graph.m_nodes.Length; ni++) {
                var node = graph.m_nodes[ni];
                if (node == null) continue;
                var nodeType = node.GetType().Name;

                // gather plugs — plug_id is stable (node_idx + field path),
                // does NOT depend on object identity (which may be shared/default).
                var inputPlugs = new List<object>();
                var outputPlugs = new List<object>();
                foreach (var (field, plug) in CollectPlugs(node)) {
                    var plugId = $"{prefix}:n{ni}:{field}";
                    var plugType = plug.GetType().Name;
                    var plugObj = (STUGraphPlug)plug;
                    var linkRefs = new List<object>();
                    if (plugObj.m_links != null) {
                        foreach (var lk in plugObj.m_links) {
                            if (lk == null) continue;
                            // 解析连接的另一端
                            var other = (lk.m_outputPlug == plug) ? lk.m_inputPlug : lk.m_outputPlug;
                            string? otherId = null;
                            int? otherNodeIdx = null;
                            if (other != null && plugObjToOwner.TryGetValue(other, out var owner)) {
                                otherId = $"{prefix}:n{owner.nodeIdx}:{owner.field}";
                                otherNodeIdx = owner.nodeIdx;
                            }
                            var direction = (lk.m_outputPlug == plug) ? "out_to" : "in_from";
                            linkRefs.Add(new { dir = direction, other_plug_id = otherId, other_node_index = otherNodeIdx });
                            // record at link level too
                            if (lk.m_outputPlug == plug && otherId != null) {
                                allLinks.Add(new { from = plugId, to = otherId, from_node = ni, to_node = otherNodeIdx });
                            }
                        }
                    }
                    var entry = new {
                        plug_id = plugId,
                        field,
                        plug_type = plugType,
                        link_count = plugObj.m_links?.Length ?? 0,
                        links = linkRefs,
                    };
                    bool isInput = plugType.Contains("Input");
                    if (isInput) inputPlugs.Add(entry); else outputPlugs.Add(entry);
                }

                // writes/reads
                var writes = new List<object>();
                if (node.m_BF5B22B7 != null)
                    foreach (var sv in node.m_BF5B22B7)
                        if (sv?.m_0D09D2D9 != 0)
                            writes.Add(new { var_id = $"0x{teResourceGUID.Index(sv.m_0D09D2D9):X}" });
                var reads = new List<object>();
                if (node.m_8BF03679 != null)
                    foreach (var sv in node.m_8BF03679)
                        if (sv?.m_0D09D2D9 != 0)
                            reads.Add(new { var_id = $"0x{teResourceGUID.Index(sv.m_0D09D2D9):X}" });

                // config tree (excluding plugs/svs)
                var configTree = DumpConfigTree(node);

                nodes.Add(new {
                    node_index = ni,
                    node_id = node.m_uniqueID,
                    type = nodeType,
                    type_short = nodeType.Replace("STUStatescript", "").Replace("STU_", "Unk_"),
                    label = node.m_049CA107?.Value?.TrimEnd('\0', ' ', '\n', '\r'),
                    is_entry = entryIndices.Contains(ni),
                    is_state = stateIndices.Contains(ni),
                    input_plugs = inputPlugs,
                    output_plugs = outputPlugs,
                    writes,
                    reads,
                    config = configTree,
                });
            }
        }

        heroGraphs.Add(new {
            graph_index = $"0x{gIdx:X}",
            graph_guid = $"0x{graphGuid:X16}",
            slot_index = gi,
            node_count = nodes.Count,
            link_count = allLinks.Count,
            entry_count = graph.m_entries?.Length ?? 0,
            state_count = graph.m_states?.Length ?? 0,
            sync_var_count = graph.m_syncVars?.Length ?? 0,
            nodes,
            links = allLinks,
        });
        gi++;
    }

    topologyList.Add(new {
        hero_id = $"0x{heroIdx:X}",
        hero_name = heroName,
        graph_count = heroGraphs.Count,
        graphs = heroGraphs,
    });
}

// Split into per-hero files (single combined file is ~330MB).
{
    var topoDir = Path.Combine(outDir, "graph_topology");
    Directory.CreateDirectory(topoDir);
    var indexEntries = new List<object>();
    long sumNodes = 0, sumLinks = 0;
    foreach (var h in topologyList) {
        var je = JsonSerializer.SerializeToElement(h, jsonOpt);
        var hidStr = je.GetProperty("hero_id").GetString() ?? "?";  // "0xXXX"
        var fileName = $"{hidStr}.json";
        File.WriteAllText(Path.Combine(topoDir, fileName), JsonSerializer.Serialize(h, jsonOpt));
        long n = 0, l = 0;
        var graphList = new List<object>();
        foreach (var g in je.GetProperty("graphs").EnumerateArray()) {
            int nc = g.GetProperty("node_count").GetInt32();
            int lc = g.GetProperty("link_count").GetInt32();
            n += nc; l += lc;
            graphList.Add(new {
                graph_index = g.GetProperty("graph_index").GetString(),
                slot_index = g.GetProperty("slot_index").GetInt32(),
                node_count = nc,
                link_count = lc,
            });
        }
        sumNodes += n; sumLinks += l;
        indexEntries.Add(new {
            hero_id = hidStr,
            hero_name = je.GetProperty("hero_name").GetString(),
            file = fileName,
            graph_count = je.GetProperty("graph_count").GetInt32(),
            total_nodes = n,
            total_links = l,
            graphs = graphList,
        });
    }
    File.WriteAllText(Path.Combine(topoDir, "index.json"),
        JsonSerializer.Serialize(indexEntries, jsonOpt));
    Console.Error.WriteLine($"Wrote {topologyList.Count} hero topology files to graph_topology/ (nodes={sumNodes}, links={sumLinks})");
}

// ============ 4. Var DisplayName Coverage Probe ============
// Walk every STULoadout.m_C59F05B1 → STU_2FA1A54E.m_E4768446 (STU_BCD1C634)
// Each STU_BCD1C634 has m_id (var identifier) + m_type (STU_6649A4C0)
// STU_6649A4C0.m_81125A2C is STU_78BECDEB[], each carrying (m_id, m_displayName)
Console.Error.WriteLine("Probing state var displayName coverage...");

var varNameMap = new Dictionary<uint, (string? name, HashSet<string> sources, ulong typeGuid)>();
var allTypeGuids = new HashSet<ulong>();  // every STU_6649A4C0 we've seen referenced
int loadoutTotal = 0, loadoutWithVarArr = 0, varEntriesTotal = 0, varEntriesWithType = 0;

// Pull the text out of an STU_A7F15A16 (display name wrapper) or fall back to raw teString.
string? ReadDisplayName(ulong dnGuid) {
    if (dnGuid == 0) return null;
    try {
        var dnSTU = ReadSTU<STU_A7F15A16>(dnGuid);
        var v = dnSTU?.m_text.Value;
        if (!string.IsNullOrEmpty(v)) return v.TrimEnd('\0',' ','\n','\r');
    } catch { }
    return ReadString(dnGuid);
}

// Pass 1: Scan every STULoadout.m_C59F05B1 (loadout var slots) to find (var_id, type_guid) pairs.
// Each slot's m_E4768446 (STU_BCD1C634) tells us which var and which type descriptor is used.
bool diagEntryPrinted = false;
foreach (var kvp in tank.m_assets) {
    if (teResourceGUID.Type(kvp.Key) != 0x9E) continue;
    var lo = ReadSTU<STULoadout>(kvp.Key);
    if (lo == null) continue;
    loadoutTotal++;
    if (lo.m_C59F05B1 == null || lo.m_C59F05B1.Length == 0) continue;
    loadoutWithVarArr++;
    // One-shot diag: dump every field of the first non-empty entry so we can see where
    // the STU_6649A4C0 type ref actually lives (m_type is always 0 in shipping data).
    if (!diagEntryPrinted && lo.m_C59F05B1.Length > 0 && lo.m_C59F05B1[0]?.m_E4768446 != null) {
        var e = lo.m_C59F05B1[0].m_E4768446;
        Console.Error.WriteLine($"  -- DIAG: first STU_BCD1C634 entry (loadout 0x{teResourceGUID.Index(kvp.Key):X}, '{ReadString(lo.m_name)}') --");
        Console.Error.WriteLine($"    runtime type: {e.GetType().Name}, fields: {e.GetType().GetFields().Length}");
        foreach (var f in e.GetType().GetFields()) {
            var v = f.GetValue(e);
            string s;
            if (v == null) s = "null";
            else s = $"{v.GetType().Name}: {v}";
            Console.Error.WriteLine($"    .{f.Name} ({f.FieldType.Name}) = {s}");
        }
        // Also try StructuredData raw — walk the parent STU_2FA1A54E chain
        var parentItem = lo.m_C59F05B1[0];
        Console.Error.WriteLine($"    parent STU_2FA1A54E fields: {parentItem.GetType().GetFields().Length}");
        foreach (var f in parentItem.GetType().GetFields()) {
            var v = f.GetValue(parentItem);
            Console.Error.WriteLine($"    parent.{f.Name} = {(v == null ? "null" : v.GetType().Name)}");
        }
        diagEntryPrinted = true;
    }
    var loIdx = teResourceGUID.Index(kvp.Key);
    var loName = ReadString(lo.m_name) ?? "?";
    var srcTag = $"0x{loIdx:X}({loName})";

    foreach (var item in lo.m_C59F05B1) {
        var entry = item?.m_E4768446;
        if (entry == null) continue;
        varEntriesTotal++;
        ulong idGuid = entry.m_id;
        if (idGuid == 0) continue;
        uint varId = teResourceGUID.Index(idGuid);
        ulong typeGuid = entry.m_type;
        // STU_BCD1C634 inherits STUConfigVar through STU_9A88FA41 — m_EE729DCB is the REAL
        // reference field (confirmed via runtime: m_type is always 0 in shipping data).
        // The inherited STUConfigVar.m_EE729DCB carries the STU_6649A4C0 GUID instead.
        ulong configRefGuid = entry.m_EE729DCB;
        if (typeGuid != 0) {
            varEntriesWithType++;
            allTypeGuids.Add(typeGuid);
        }
        if (configRefGuid != 0) {
            allTypeGuids.Add(configRefGuid);
        }

        if (!varNameMap.TryGetValue(varId, out var rec)) {
            rec = (null, new HashSet<string>(), typeGuid != 0 ? typeGuid : configRefGuid);
        }
        rec.sources.Add(srcTag);
        varNameMap[varId] = rec;
    }
}

// Pass 2: For every type GUID (STU_6649A4C0) we've discovered, load it and walk m_81125A2C
// (STU_78BECDEB[]) — each entry is (var_id → displayName). This is THE source of var names:
// STU_A7F15A16.m_text holds the SCREAMING_SNAKE string, and every var in the same enum group
// also gets dumped (sibling vars: we learn names for vars never seen in any loadout slot).
Console.Error.WriteLine($"  -- Pass 2: Walking {allTypeGuids.Count} STU_6649A4C0 type descriptors for displayName extraction --");
int typesLoaded = 0, typesWithEntries = 0, typeEntriesSeen = 0, typeEntriesNamed = 0;
int nameHitsKnown = 0, nameHitsNew = 0;
foreach (var typeGuid in allTypeGuids) {
    STU_6649A4C0? typeStu;
    try { typeStu = ReadSTU<STU_6649A4C0>(typeGuid); }
    catch { continue; }
    if (typeStu == null) continue;
    typesLoaded++;
    if (typeStu.m_81125A2C == null || typeStu.m_81125A2C.Length == 0) continue;
    typesWithEntries++;
    var typeIdx = teResourceGUID.Index(typeGuid);
    var typeSrcTag = $"type:0x{typeIdx:X}";

    foreach (var e in typeStu.m_81125A2C) {
        if (e == null) continue;
        typeEntriesSeen++;
        ulong eIdGuid = e.m_id;
        if (eIdGuid == 0) continue;
        uint innerVid = teResourceGUID.Index(eIdGuid);
        string? dn = ReadDisplayName(e.m_displayName);
        if (string.IsNullOrEmpty(dn)) continue;
        typeEntriesNamed++;

        if (varNameMap.TryGetValue(innerVid, out var rec)) {
            // Already seen this var via a loadout — fill in the name.
            if (string.IsNullOrEmpty(rec.name)) {
                rec.name = dn;
                varNameMap[innerVid] = rec;
                nameHitsKnown++;
            } else if (rec.name != dn) {
                // Different type descriptor giving a different name — keep the first one,
                // but tag the source so we can audit conflicts offline.
                rec.sources.Add($"{typeSrcTag}(alt:{dn})");
                varNameMap[innerVid] = rec;
            }
        } else {
            // Sibling var never referenced by any loadout slot — still useful context.
            varNameMap[innerVid] = (dn, new HashSet<string> { typeSrcTag }, typeGuid);
            nameHitsNew++;
        }
    }
}
Console.Error.WriteLine($"    loaded {typesLoaded}/{allTypeGuids.Count} types, {typesWithEntries} have m_81125A2C entries");
Console.Error.WriteLine($"    enum entries: {typeEntriesSeen} seen, {typeEntriesNamed} named ({(typeEntriesSeen > 0 ? 100.0*typeEntriesNamed/typeEntriesSeen : 0):F1}%)");
Console.Error.WriteLine($"    name hits: filled {nameHitsKnown} previously-null vars + added {nameHitsNew} sibling vars");

int namedCount = varNameMap.Count(kv => !string.IsNullOrEmpty(kv.Value.name));
Console.Error.WriteLine($"  Loadouts: {loadoutTotal} total, {loadoutWithVarArr} with m_C59F05B1");
Console.Error.WriteLine($"  Var entries: {varEntriesTotal} total, {varEntriesWithType} with type");
Console.Error.WriteLine($"  Unique var IDs: {varNameMap.Count}, NAMED: {namedCount} ({(varNameMap.Count > 0 ? 100.0*namedCount/varNameMap.Count : 0):F1}%)");

// Build syncVarIds set early (re-used by DIAG and cross-check)
var syncVarIds = new HashSet<uint>();
foreach (var hd in graphSyncList) {
    var je0 = JsonSerializer.SerializeToElement(hd, jsonOpt);
    foreach (var g in je0.GetProperty("graphs").EnumerateArray())
        foreach (var sv in g.GetProperty("sync_vars").EnumerateArray())
            if (sv.TryGetProperty("identifier_index", out var ii) && ii.ValueKind == JsonValueKind.String) {
                var s = ii.GetString();
                if (s != null && s.StartsWith("0x") && uint.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null, out var v))
                    syncVarIds.Add(v);
            }
}

// === DIAG A: open raw type=0x1C Identifier file for first 10 var IDs and dump bytes ===
Console.Error.WriteLine("  -- Probing raw type=0x1C Identifier file contents (5 loadout vars + 5 sync vars) --");
int diagDone = 0;
var probeVars = new List<uint>();
probeVars.AddRange(varNameMap.Keys.Take(5));
probeVars.AddRange(syncVarIds.Take(5));
foreach (var vid in probeVars.Distinct()) {
    if (diagDone >= 10) break;
    diagDone++;
    ulong identGuid = (ulong)new teResourceGUID(0).WithIndex(vid).WithType(0x1C);
    bool inAssets = tank.m_assets.ContainsKey(identGuid);
    try {
        using var s = tank.OpenFile(identGuid);
        if (s == null) { Console.Error.WriteLine($"    var 0x{vid:X} (guid 0x{identGuid:X16}): NULL stream, in_manifest={inAssets}"); continue; }
        var buf = new byte[Math.Min(64, s.Length)];
        var read = s.Read(buf, 0, buf.Length);
        var hex = string.Join(" ", buf.Take(read).Select(b => b.ToString("X2")));
        var asc = string.Concat(buf.Take(read).Select(b => b >= 0x20 && b < 0x7F ? (char)b : '.'));
        Console.Error.WriteLine($"    var 0x{vid:X} (guid 0x{identGuid:X16}, size={s.Length}, in_manifest={inAssets}): {hex} [{asc}]");
    } catch (Exception ex) {
        Console.Error.WriteLine($"    var 0x{vid:X} (guid 0x{identGuid:X16}, in_manifest={inAssets}): err {ex.GetType().Name}: {ex.Message}");
    }
}

// === DIAG F: scan FieldInfoBags for fields OWLib didn't declare on STUStatescriptGraph / STULoadout / STUStatescriptComponent ===
Console.Error.WriteLine("  -- Probing for OWLib-unknown fields in graph/loadout/component STU files --");

// Collect declared hashes from OWLib classes via reflection
HashSet<uint> DeclaredHashes(Type t) {
    var set = new HashSet<uint>();
    Type? cur = t;
    while (cur != null && cur != typeof(object)) {
        foreach (var f in cur.GetFields()) {
            var attr = f.GetCustomAttributes(typeof(STUFieldAttribute), false).FirstOrDefault() as STUFieldAttribute;
            if (attr != null) set.Add(attr.Hash);
        }
        cur = cur.BaseType;
    }
    return set;
}

var graphDecl = DeclaredHashes(typeof(STUStatescriptGraph));
var loadoutDecl = DeclaredHashes(typeof(STULoadout));
var ssCompDecl = DeclaredHashes(typeof(STUStatescriptComponent));
Console.Error.WriteLine($"  OWLib-declared fields: STUStatescriptGraph={graphDecl.Count}, STULoadout={loadoutDecl.Count}, STUStatescriptComponent={ssCompDecl.Count}");

// Get a few sample assets and dump their FieldInfoBags

// Find a graph guid (first hero's first graph)
ulong probeGraphGuid = 0, probeLoadoutGuid = 0, probeSSCompGuid = 0;
foreach (var kvp in tank.m_assets) {
    if (probeLoadoutGuid == 0 && teResourceGUID.Type(kvp.Key) == 0x9E) {
        probeLoadoutGuid = kvp.Key;
    }
    if (probeGraphGuid == 0 && teResourceGUID.Type(kvp.Key) == 0x58) {  // 0x58 might be graph
        probeGraphGuid = kvp.Key;
    }
    if (probeLoadoutGuid != 0 && probeGraphGuid != 0) break;
}

// More reliable: walk a hero to find a real graph guid
foreach (var kvp in tank.m_assets) {
    if (teResourceGUID.Type(kvp.Key) != 0x75) continue;
    var hero = ReadSTU<STUHero>(kvp.Key);
    if (hero?.m_gameplayEntity == 0 || hero == null) continue;
    var entDef = ReadSTU<STUEntityDefinition>(hero.m_gameplayEntity);
    if (entDef?.m_componentMap == null) continue;
    foreach (var c in entDef.m_componentMap) {
        if (c.Value is STUStatescriptComponent ssc && ssc.m_B634821A != null && ssc.m_B634821A.Length > 0) {
            probeGraphGuid = ssc.m_B634821A[0].m_graph;
            // for ssComp we need an entity GUID where StatescriptComponent is embedded; use entity guid
            probeSSCompGuid = hero.m_gameplayEntity;
            break;
        }
    }
    if (probeGraphGuid != 0) break;
}

void ProbeFile(string label, ulong guid, HashSet<uint> declared, uint markerHash) {
    if (guid == 0) { Console.Error.WriteLine($"    {label}: no sample GUID"); return; }
    Console.Error.WriteLine($"    {label} sample 0x{guid:X16}:");
    try {
        using var fs = tank.OpenFile(guid);
        if (fs == null) { Console.Error.WriteLine($"      file null"); return; }
        var data = new teStructuredData(fs, keepOpen: true);
        Console.Error.WriteLine($"      Format={data.Format}, Instances={data.Instances?.Length ?? 0}, FieldInfoBags={data.FieldInfoBags?.Count ?? -1}");
        if (data.FieldInfoBags == null) { Console.Error.WriteLine($"      (V1 format — no field bags)"); return; }
        int bagsWithMarker = 0;
        for (int b = 0; b < data.FieldInfoBags.Count; b++) {
            var bag = data.FieldInfoBags[b];
            var bagHashes = bag.Select(f => f.Hash).ToHashSet();
            if (!bagHashes.Contains(markerHash)) continue;
            bagsWithMarker++;
            var unknown = bag.Where(f => !declared.Contains(f.Hash)).ToList();
            Console.Error.WriteLine($"      bag[{b}] has {bag.Count} fields, OWLib-declared {bag.Count(f => declared.Contains(f.Hash))}, UNKNOWN: {unknown.Count}");
            foreach (var u in unknown.Take(20)) {
                Console.Error.WriteLine($"        unknown: hash=0x{u.Hash:X8} size={u.Size}");
            }
        }
        if (bagsWithMarker == 0) Console.Error.WriteLine($"      no bag with marker hash 0x{markerHash:X8}");
    } catch (Exception ex) {
        Console.Error.WriteLine($"      err: {ex.GetType().Name}: {ex.Message}");
    }
}
ProbeFile("graph",   probeGraphGuid,   graphDecl,   0x44D31832u);
ProbeFile("loadout", probeLoadoutGuid, loadoutDecl, 0xB48F1D22u);
ProbeFile("entity",  probeSSCompGuid,  ssCompDecl,  0xB634821Au);  // entity contains ss component

// === DIAG G: scan ALL instance type hashes in a graph file, find OWLib-unknown types and bag-level unknown fields ===
Console.Error.WriteLine("  -- Deep scan of graph file: instance types + all bags --");
if (probeGraphGuid != 0) {
    using var fs = tank.OpenFile(probeGraphGuid);
    if (fs != null) {
        var data = new teStructuredData(fs, keepOpen: true);
        // Group instances by type hash, count, and check OWLib recognition
        var typeStats = new Dictionary<uint, (int count, string typeName, int unknownCount)>();
        if (data.InstanceInfo != null) {
            for (int i = 0; i < data.InstanceInfo.Count; i++) {
                var ii = data.InstanceInfo[i];
                var instance = data.Instances[i];
                string tname = instance?.GetType().Name ?? "<UNKNOWN_TYPE>";
                if (!typeStats.TryGetValue(ii.Hash, out var stat)) stat = (0, tname, 0);
                stat.count++;
                stat.typeName = tname;
                typeStats[ii.Hash] = stat;
            }
        }
        Console.Error.WriteLine($"    Graph 0x{probeGraphGuid:X16}: {data.InstanceInfo?.Count ?? 0} instances, {typeStats.Count} distinct types, {data.FieldInfoBags?.Count ?? 0} field bags");
        // Show top 20 most common types
        Console.Error.WriteLine($"    Top types by count:");
        foreach (var kv in typeStats.OrderByDescending(x => x.Value.count).Take(20)) {
            string mark = kv.Value.typeName == "<UNKNOWN_TYPE>" ? " <- OWLib UNKNOWN!" : "";
            Console.Error.WriteLine($"      hash=0x{kv.Key:X8} {kv.Value.typeName,-50} count={kv.Value.count}{mark}");
        }
        // Show OWLib-unknown types
        var unknownTypes = typeStats.Where(kv => kv.Value.typeName == "<UNKNOWN_TYPE>").ToList();
        Console.Error.WriteLine($"    OWLib-unknown types: {unknownTypes.Count}");
        foreach (var kv in unknownTypes.Take(10))
            Console.Error.WriteLine($"      hash=0x{kv.Key:X8} count={kv.Value.count}");

        // Now: which bags are completely unknown to OWLib?
        // First build hash -> declared fields for every known type
        if (data.FieldInfoBags != null) {
            // For each bag, count how many fields are declared by ANY OWLib type
            var allDeclaredHashes = new HashSet<uint>();
            // Collect declared field hashes from all OWLib STU types
            var allTypes = typeof(STUInstance).Assembly.GetTypes()
                .Where(t => typeof(STUInstance).IsAssignableFrom(t) && !t.IsAbstract);
            foreach (var t in allTypes) {
                Type? cur = t;
                while (cur != null && cur != typeof(object)) {
                    foreach (var f in cur.GetFields()) {
                        var attr = f.GetCustomAttributes(typeof(STUFieldAttribute), false).FirstOrDefault() as STUFieldAttribute;
                        if (attr != null) allDeclaredHashes.Add(attr.Hash);
                    }
                    cur = cur.BaseType;
                }
            }
            Console.Error.WriteLine($"    Total OWLib-declared field hashes (across all types): {allDeclaredHashes.Count}");
            // Find bags with completely-unknown fields
            int totalFields = 0, unknownFields = 0;
            var bagsWithUnknown = new List<(int bagIdx, int total, int unknown, List<(uint hash, int size)> samples)>();
            for (int b = 0; b < data.FieldInfoBags.Count; b++) {
                var bag = data.FieldInfoBags[b];
                totalFields += bag.Count;
                var unknownInBag = bag.Where(f => !allDeclaredHashes.Contains(f.Hash)).ToList();
                unknownFields += unknownInBag.Count;
                if (unknownInBag.Count > 0)
                    bagsWithUnknown.Add((b, bag.Count, unknownInBag.Count,
                        unknownInBag.Take(8).Select(f => (f.Hash, f.Size)).ToList()));
            }
            Console.Error.WriteLine($"    Total fields across all bags: {totalFields}, OWLib-unknown across ALL types: {unknownFields}");
            Console.Error.WriteLine($"    Bags containing OWLib-unknown fields: {bagsWithUnknown.Count}");
            foreach (var (bagIdx, total, unk, samples) in bagsWithUnknown.Take(10)) {
                Console.Error.WriteLine($"      bag[{bagIdx}] total={total} unknown={unk}");
                foreach (var (hash, size) in samples)
                    Console.Error.WriteLine($"        unk hash=0x{hash:X8} size={size}");
            }
        }
    }
}

// === DIAG I: cross-language scan + look for "broadcast" / "syncvar" / id_ patterns ===
Console.Error.WriteLine("  -- Trying enUS language to find more strings (CN may be lossy) --");
// Re-init client with enUS, only if available
ClientHandler? enClient = null;
ProductHandler_Tank? enTank = null;
try {
    var enArgs = new ClientCreateArgs {
        HandlerArgs = new ClientCreateArgs_Tank { ManifestRegion = ClientCreateArgs_Tank.REGION_CN },
        TextLanguage = "enUS",
        SpeechLanguage = "enUS",
    };
    enClient = new ClientHandler(cascPath, enArgs);
    enTank = enClient.ProductHandler as ProductHandler_Tank;
    if (enTank == null) {
        Console.Error.WriteLine("    enUS handler null — language not installed");
    } else {
        Console.Error.WriteLine($"    enUS Assets loaded: {enTank.m_assets.Count}");
        int enTotal = 0, enNonEmpty = 0, enDifferentFromZh = 0;
        var enStatescriptHits = new List<(uint idx, string s)>();
        foreach (var k in enTank.m_assets.Keys) {
            if (teResourceGUID.Type(k) != 0x7C) continue;
            enTotal++;
            try {
                using var fs = enTank.OpenFile(k);
                if (fs == null) continue;
                var s = ((string?)new teString(fs))?.TrimEnd('\0', ' ', '\n', '\r');
                if (string.IsNullOrEmpty(s)) continue;
                enNonEmpty++;
                if (s.IndexOf("statescript", StringComparison.OrdinalIgnoreCase) >= 0
                    || s.IndexOf("syncvar", StringComparison.OrdinalIgnoreCase) >= 0
                    || s.IndexOf("broadcast", StringComparison.OrdinalIgnoreCase) >= 0
                    || s.IndexOf("st_", StringComparison.OrdinalIgnoreCase) >= 0
                    || s.StartsWith("ID_", StringComparison.OrdinalIgnoreCase)) {
                    enStatescriptHits.Add((teResourceGUID.Index(k), s));
                }
            } catch { }
        }
        Console.Error.WriteLine($"    enUS type=0x7C: total={enTotal}, non-empty={enNonEmpty}");
        Console.Error.WriteLine($"    enUS statescript-like hits: {enStatescriptHits.Count}");
        foreach (var (idx, s) in enStatescriptHits.Take(50))
            Console.Error.WriteLine($"      0x{idx:X8} = {s.Substring(0, Math.Min(120, s.Length)).Replace('\n','|').Replace('\r','|')}");
    }
} catch (Exception ex) {
    Console.Error.WriteLine($"    enUS init err: {ex.Message}");
}

// === DIAG H: scan ALL type=0x7C strings for var-like names ===
Console.Error.WriteLine("  -- Dumping ALL type=0x7C strings, grep for var-like names --");
int strTotal = 0, strNonEmpty = 0, strEncrypted = 0;
var allStrings = new Dictionary<uint, string>();  // index -> string
var varLikeKeywords = new[] { "Cooldown", "Ability", "WeaponSpread", "WeaponVolley", "ChaseVar",
    "HealthPool", "ModifyHealth", "DeflectProjectiles", "Damage", "StateAbility",
    "SetVar", "Stack", "Untargetable", "Stealth", "Reveal", "Buff", "Debuff",
    "Hack", "Sleep", "Slow", "Freeze", "Nano", "AntiHeal", "HealBoost",
    "MaxHP", "BaseHP", "CurHP", "Armor", "Shield", "UltCharge", "Position",
    "Statescript", "SyncVar", "StateVar", "Identifier", "Hanzo", "Mei",
    "Tracer", "Sombra", "Reaper", "Kiriko", "Zenyatta", "DVa" };
var matched = new Dictionary<string, List<(uint idx, string str)>>();
foreach (var k in varLikeKeywords) matched[k] = new List<(uint, string)>();

foreach (var kvp in tank.m_assets) {
    if (teResourceGUID.Type(kvp.Key) != 0x7C) continue;
    strTotal++;
    string? s = ReadString(kvp.Key);
    if (s == null) { strEncrypted++; continue; }
    if (s.Length == 0) continue;
    strNonEmpty++;
    var idx = teResourceGUID.Index(kvp.Key);
    allStrings[idx] = s;
    foreach (var k in varLikeKeywords) {
        if (s.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) {
            matched[k].Add((idx, s));
            break;
        }
    }
}
Console.Error.WriteLine($"  type=0x7C: total={strTotal}, non-empty={strNonEmpty}, encrypted/null={strEncrypted}");
int totalMatched = matched.Sum(kv => kv.Value.Count);
Console.Error.WriteLine($"  Var-like keyword matches: {totalMatched}");
foreach (var kv in matched.Where(x => x.Value.Count > 0).OrderByDescending(x => x.Value.Count)) {
    Console.Error.WriteLine($"    {kv.Key}: {kv.Value.Count} matches");
    foreach (var (idx, str) in kv.Value.Take(5))
        Console.Error.WriteLine($"      0x{idx:X8} = {str.Substring(0, Math.Min(80, str.Length)).Replace('\n','|').Replace('\r','|')}");
}

// Save all type 0x7C strings to JSON for offline grep
File.WriteAllText(Path.Combine(outDir, "all_locale_strings.json"),
    JsonSerializer.Serialize(allStrings.OrderBy(x => x.Key)
        .Select(x => new { idx = $"0x{x.Key:X}", str = x.Value }).ToList(), jsonOpt));
Console.Error.WriteLine($"  Wrote {allStrings.Count} strings to all_locale_strings.json");

// Cross-check: do any var IDs from sync_vars / loadout map appear as a string GUID index?
int varIdHitsAsStr = 0;
foreach (var v in syncVarIds) {
    if (allStrings.ContainsKey(v)) {
        varIdHitsAsStr++;
        if (varIdHitsAsStr <= 5)
            Console.Error.WriteLine($"  !!! var_id 0x{v:X} matches string idx 0x{v:X}: {allStrings[v]}");
    }
}
Console.Error.WriteLine($"  sync var IDs that match a string GUID index: {varIdHitsAsStr} / {syncVarIds.Count}");

// === DIAG E: count CASC asset types ===
Console.Error.WriteLine("  -- CASC manifest asset count by type --");
var typeCount = new Dictionary<ushort, int>();
foreach (var k in tank.m_assets.Keys) {
    var t = teResourceGUID.Type(k);
    typeCount[t] = typeCount.GetValueOrDefault(t) + 1;
}
foreach (var t in new ushort[] { 0x1C, 0x9E, 0x75, 0x58, 0x5F, 0x68 }) {
    Console.Error.WriteLine($"    type 0x{t:X3}: {typeCount.GetValueOrDefault(t)} assets");
}
Console.Error.WriteLine($"  -- Top 10 most common types --");
foreach (var kv in typeCount.OrderByDescending(x => x.Value).Take(10))
    Console.Error.WriteLine($"    type 0x{kv.Key:X3}: {kv.Value}");

// === DIAG C: dump one StatescriptComponent's m_2D9815BA (component-level schema) entries ===
Console.Error.WriteLine("  -- Probing StatescriptComponent.m_2D9815BA component schema entries --");
int compDiag = 0;
foreach (var kvp in tank.m_assets) {
    if (compDiag >= 1) break;
    if (teResourceGUID.Type(kvp.Key) != 0x75) continue;
    var hero = ReadSTU<STUHero>(kvp.Key);
    if (hero == null || hero.m_64DC571F == 0 || hero.m_gameplayEntity == 0) continue;
    var entDef = ReadSTU<STUEntityDefinition>(hero.m_gameplayEntity);
    if (entDef?.m_componentMap == null) continue;
    STUStatescriptComponent? ssC = null;
    foreach (var c in entDef.m_componentMap)
        if (c.Value is STUStatescriptComponent ssc) { ssC = ssc; break; }
    if (ssC?.m_2D9815BA?.m_entries == null) continue;
    Console.Error.WriteLine($"    Hero 0x{teResourceGUID.Index(kvp.Key):X} {ReadString(hero.m_0EDCE350)}: {ssC.m_2D9815BA.m_entries.Length} schema entries");
    foreach (var se in ssC.m_2D9815BA.m_entries.Take(10)) {
        if (se == null) continue;
        ulong idg = se.m_0D09D2D9;
        var valTy = se.m_value?.GetType().Name ?? "null";
        Console.Error.WriteLine($"      id=0x{teResourceGUID.Index(idg):X8} value={valTy}");
        if (se.m_value != null) {
            foreach (var f in se.m_value.GetType().GetFields().Take(5)) {
                var v = f.GetValue(se.m_value);
                if (v != null && v.ToString() != "0" && v.ToString() != "")
                    Console.Error.WriteLine($"        {f.Name} = {v}");
            }
        }
    }
    compDiag++;
}

// === DIAG D: dump one Graph's first sync_var with full STU dump (any extra fields?) ===
Console.Error.WriteLine("  -- Probing STUStatescriptSyncVar runtime fields (first 3) --");
int svDiag = 0;
foreach (var kvp in tank.m_assets) {
    if (svDiag >= 3) break;
    if (teResourceGUID.Type(kvp.Key) != 0x75) continue;
    var hero = ReadSTU<STUHero>(kvp.Key);
    if (hero?.m_gameplayEntity == 0 || hero == null) continue;
    var entDef = ReadSTU<STUEntityDefinition>(hero.m_gameplayEntity);
    if (entDef?.m_componentMap == null) continue;
    STUStatescriptComponent? ssC = null;
    foreach (var c in entDef.m_componentMap)
        if (c.Value is STUStatescriptComponent ssc) { ssC = ssc; break; }
    if (ssC?.m_B634821A == null) continue;
    foreach (var gwo in ssC.m_B634821A) {
        if (svDiag >= 3) break;
        var graph = ReadSTU<STUStatescriptGraph>(gwo.m_graph);
        if (graph?.m_syncVars == null) continue;
        foreach (var sv in graph.m_syncVars.Take(3)) {
            if (sv?.m_0D09D2D9 == 0) continue;
            svDiag++;
            Console.Error.WriteLine($"    SyncVar id=0x{teResourceGUID.Index(sv.m_0D09D2D9):X} type={sv.m_56341592} flag={sv.m_AC9480C7}");
            foreach (var f in sv.GetType().GetFields()) {
                var v = f.GetValue(sv);
                Console.Error.WriteLine($"      {f.Name} ({f.FieldType.Name}) = {v}");
            }
        }
        // Also probe the 6 STUIdentifier[] arrays in graph
        Console.Error.WriteLine($"    Graph 0x{teResourceGUID.Index(gwo.m_graph):X}: m_44D31832={graph.m_44D31832?.Length ?? 0} m_A1183166={graph.m_A1183166?.Length ?? 0} m_CC881252={graph.m_CC881252?.Length ?? 0} m_0D92E2AF={graph.m_0D92E2AF?.Length ?? 0} m_27651F70={graph.m_27651F70?.Length ?? 0} m_9CEC6985={graph.m_9CEC6985?.Length ?? 0}");
        if (graph.m_44D31832 != null && graph.m_44D31832.Length > 0)
            Console.Error.WriteLine($"      m_44D31832[0]: 0x{(ulong)graph.m_44D31832[0]:X16}");
        break;
    }
    break;  // one hero
}

// === DIAG B: pick one loadout, dump its 10 STUConfigVar slots to see if any have STUIdentifier reference w/ name ===
Console.Error.WriteLine("  -- Probing STULoadout's named ConfigVar slots --");
foreach (var kvp in tank.m_assets) {
    if (teResourceGUID.Type(kvp.Key) != 0x9E) continue;
    var lo = ReadSTU<STULoadout>(kvp.Key);
    if (lo == null) continue;
    var loIdx = teResourceGUID.Index(kvp.Key);
    var loName = ReadString(lo.m_name) ?? "?";
    bool any = false;
    foreach (var f in lo.GetType().GetFields()) {
        if (!f.Name.StartsWith("m_")) continue;
        if (f.FieldType.Name != "STUConfigVar" && !typeof(STUConfigVar).IsAssignableFrom(f.FieldType)) continue;
        var v = f.GetValue(lo);
        if (v == null) continue;
        var cv = v as STUConfigVar;
        if (cv == null) continue;
        // dump cv runtime type + key fields
        var subFields = cv.GetType().GetFields();
        var summary = string.Join(", ", subFields.Take(4).Select(sf => {
            var sv = sf.GetValue(cv);
            return $"{sf.Name}={sv}";
        }));
        Console.Error.WriteLine($"    {loIdx:X}({loName}).{f.Name} = {cv.GetType().Name}: {summary}");
        any = true;
    }
    if (any) break;  // only one loadout for diag
}

int syncVarsTotal = syncVarIds.Count;
int syncVarsNamed = syncVarIds.Count(v => varNameMap.TryGetValue(v, out var r) && !string.IsNullOrEmpty(r.name));
int syncVarsCovered = syncVarIds.Count(v => varNameMap.ContainsKey(v));
Console.Error.WriteLine($"  Cross-check vs graph sync_vars: {syncVarsTotal} unique sync var IDs");
Console.Error.WriteLine($"    appear in loadout map:   {syncVarsCovered} ({100.0*syncVarsCovered/Math.Max(1,syncVarsTotal):F1}%)");
Console.Error.WriteLine($"    have a displayName:      {syncVarsNamed} ({100.0*syncVarsNamed/Math.Max(1,syncVarsTotal):F1}%)");

var varNameList = varNameMap
    .OrderBy(kv => kv.Key)
    .Select(kv => new {
        var_id = $"0x{kv.Key:X}",
        var_id_dec = kv.Key,
        display_name = kv.Value.name,
        type_guid = $"0x{kv.Value.typeGuid:X16}",
        source_count = kv.Value.sources.Count,
        sources = kv.Value.sources.Take(5).ToList(),
    }).ToList();
File.WriteAllText(Path.Combine(outDir, "var_names.json"),
    JsonSerializer.Serialize(varNameList, jsonOpt));
Console.Error.WriteLine($"Wrote {varNameList.Count} var name records to var_names.json");

// ============ 5. Entity Origin Tracing ============
// For each hero, walk its statescript graphs and find every node that spawns an entity:
//   - STUStatescriptActionCreateEntity.m_entityDef (STUConfigVar → ulong m_EE729DCB)
//   - STUStatescriptStateCreateEntity.m_entityDef
//   - STUStatescriptWeaponProjectileEntity.m_entityDef (used by WeaponVolley)
//   - STUStatescriptWeaponProjectileEntity.m_915AF62D[] (fallback static entity refs)
//   - Raw entity asset refs in WeaponVolley's m_12F599BD / m_EA805F5D (any teResourceGUID with type=0x003)
// Output: entity_id → { heroes_that_spawn, graphs, loadouts (via logicalButton match), source_nodes }
// This is the primary way to turn anonymous entity_id 0x2658 into "Symmetra Sentry Turret" —
// we rely on the game data itself, not a hand-maintained enum.
Console.Error.WriteLine("Tracing entity origins (CreateEntity + WeaponProjectileEntity)...");

// One-shot diag: find any CreateEntity node in any graph and dump its m_entityDef's full field layout.
// This tells us whether ConfigVar has an extra GUID field we've been missing.
bool diagCreateDone = false;
foreach (var kvp in tank.m_assets) {
    if (diagCreateDone) break;
    if (teResourceGUID.Type(kvp.Key) != 0x75) continue;
    var h = ReadSTU<STUHero>(kvp.Key);
    if (h?.m_gameplayEntity == 0 || h == null) continue;
    STUEntityDefinition? ed;
    try { ed = ReadSTU<STUEntityDefinition>(h.m_gameplayEntity); } catch { continue; }
    if (ed?.m_componentMap == null) continue;
    STUStatescriptComponent? sc = null;
    foreach (var c in ed.m_componentMap) if (c.Value is STUStatescriptComponent ssc) { sc = ssc; break; }
    if (sc?.m_B634821A == null) continue;
    foreach (var gwo in sc.m_B634821A) {
        if (diagCreateDone) break;
        var g = ReadSTU<STUStatescriptGraph>(gwo.m_graph);
        if (g?.m_nodes == null) continue;
        foreach (var n in g.m_nodes) {
            if (n == null) continue;
            if (n is STUStatescriptActionCreateEntity ace) {
                Console.Error.WriteLine($"  -- DIAG: sample ActionCreateEntity in hero 0x{teResourceGUID.Index(kvp.Key):X} --");
                if (ace.m_entityDef != null) {
                    Console.Error.WriteLine($"    m_entityDef runtime type: {ace.m_entityDef.GetType().Name}");
                    foreach (var f in ace.m_entityDef.GetType().GetFields()) {
                        var v = f.GetValue(ace.m_entityDef);
                        Console.Error.WriteLine($"    m_entityDef.{f.Name} ({f.FieldType.Name}) = {(v == null ? "null" : v.ToString())}");
                    }
                } else Console.Error.WriteLine("    m_entityDef is null");
                diagCreateDone = true;
                break;
            }
            if (n is STUStatescriptStateWeaponVolley wv && wv.m_projectileEntity != null) {
                if (!diagCreateDone) {
                    Console.Error.WriteLine($"  -- DIAG: sample WeaponVolley.m_projectileEntity in hero 0x{teResourceGUID.Index(kvp.Key):X} --");
                    var pe = wv.m_projectileEntity;
                    Console.Error.WriteLine($"    m_projectileEntity runtime type: {pe.GetType().Name}");
                    if (pe.m_entityDef != null) {
                        Console.Error.WriteLine($"    m_entityDef runtime type: {pe.m_entityDef.GetType().Name}");
                        foreach (var f in pe.m_entityDef.GetType().GetFields()) {
                            var v = f.GetValue(pe.m_entityDef);
                            Console.Error.WriteLine($"    m_entityDef.{f.Name} ({f.FieldType.Name}) = {(v == null ? "null" : v.ToString())}");
                        }
                    }
                    if (pe.m_915AF62D != null) {
                        Console.Error.WriteLine($"    m_915AF62D.Length = {pe.m_915AF62D.Length}");
                        for (int i = 0; i < Math.Min(3, pe.m_915AF62D.Length); i++) {
                            var a = pe.m_915AF62D[i];
                            Console.Error.WriteLine($"    m_915AF62D[{i}].GUID = 0x{(ulong)a.GUID:X16}");
                        }
                    }
                }
            }
        }
    }
}


// quick lookups
var heroNameMap = new Dictionary<uint, string?>();         // hero_id → name
var loadoutNameById = new Dictionary<uint, string?>();     // loadout_id → name
var loadoutButtonById = new Dictionary<uint, string?>();   // loadout_id → logicalButton name
var heroLoadoutsMap = new Dictionary<uint, List<(uint lid, string? name, string? button, string category)>>();

foreach (var kvp in tank.m_assets) {
    if (teResourceGUID.Type(kvp.Key) == 0x75) {
        var h = ReadSTU<STUHero>(kvp.Key);
        if (h != null) heroNameMap[teResourceGUID.Index(kvp.Key)] = ReadString(h.m_0EDCE350);
    } else if (teResourceGUID.Type(kvp.Key) == 0x9E) {
        var lo = ReadSTU<STULoadout>(kvp.Key);
        if (lo != null) {
            var lid = teResourceGUID.Index(kvp.Key);
            loadoutNameById[lid] = ReadString(lo.m_name);
            loadoutButtonById[lid] = ReadString(ReadSTU<STU_C5243F93>(lo.m_logicalButton)?.m_name);
        }
    }
}
// Per-hero loadout list (for fuzzy graph→ability inference)
foreach (var kvp in tank.m_assets) {
    if (teResourceGUID.Type(kvp.Key) != 0x75) continue;
    var hero = ReadSTU<STUHero>(kvp.Key);
    if (hero == null || hero.m_heroLoadout == null) continue;
    var hid = teResourceGUID.Index(kvp.Key);
    var list = new List<(uint, string?, string?, string)>();
    foreach (var lr in hero.m_heroLoadout) {
        if (lr == 0) continue;
        var lo = ReadSTU<STULoadout>(lr);
        if (lo == null) continue;
        var lid = teResourceGUID.Index(lr);
        list.Add((lid, ReadString(lo.m_name), ReadString(ReadSTU<STU_C5243F93>(lo.m_logicalButton)?.m_name), lo.m_category.ToString()));
    }
    heroLoadoutsMap[hid] = list;
}

// entity_id → list of creation contexts
var entityOrigins = new Dictionary<uint, List<object>>();
void AddOrigin(uint entId, uint heroId, uint graphIdx, uint nodeId, string sourceField, string nodeType, uint? slotIdx) {
    if (!entityOrigins.TryGetValue(entId, out var list)) {
        list = new List<object>();
        entityOrigins[entId] = list;
    }
    list.Add(new {
        hero_id = $"0x{heroId:X}",
        hero_name = heroNameMap.TryGetValue(heroId, out var hn) ? hn : null,
        graph_index = $"0x{graphIdx:X}",
        slot_index = slotIdx,
        node_id = nodeId,
        node_type = nodeType,
        source_field = sourceField,
    });
}

// Resolve any ConfigVar-like instance back to the underlying STU asset GUIDs (entity or other).
// In shipping data the interesting refs live in SUBCLASS fields — e.g.:
//   STU_8556841E.m_entityDef : teStructuredDataAssetRef<STUEntityDefinition>   ← projectile entity
//   STU_E991477D.m_entityDef : teStructuredDataAssetRef<STUEntityDefinition>   ← CreateEntity target
// The base STUConfigVar.m_EE729DCB is often 0; never rely on it alone.
// We reflect across all fields and collect any non-zero teStructuredDataAssetRef GUID.
IEnumerable<ulong> ConfigVarStaticGuids(STUConfigVar? cv) {
    if (cv == null) yield break;
    // Base field first (covers plain STUConfigVar)
    if (cv.m_EE729DCB != 0 && teResourceGUID.Type(cv.m_EE729DCB) != 0x1C)
        yield return cv.m_EE729DCB;
    // Then all subclass fields via reflection.
    foreach (var f in cv.GetType().GetFields()) {
        var v = f.GetValue(cv);
        if (v == null) continue;
        // Any teStructuredDataAssetRef<T> has an implicit ulong conversion.
        if (v is ISerializable_STU) {
            var gf = v.GetType().GetField("GUID");
            if (gf?.GetValue(v) is teResourceGUID rg) {
                ulong g = (ulong)rg;
                if (g != 0 && teResourceGUID.Type(g) != 0x1C) yield return g;
            }
        }
    }
}

// Deep reflection scanner: walk any object graph and collect every teStructuredDataAssetRef
// whose target is an entity (GUID type == 0x003). Handles nested structs, arrays, and
// subclass fields — catches things like STUHero.m_322C521A, STUEntityDefinition.m_8B9A461F,
// and component.m_entity that the per-node scan missed. Recursion is capped to keep runtime
// bounded even on deeply nested STU instances.
const int kDeepMaxDepth = 5;
void CollectEntityAssetRefsDeep(object? root, Action<ulong, string> visit, string pathPrefix, int depth, HashSet<object>? visited) {
    if (root == null || depth > kDeepMaxDepth) return;
    visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
    if (!root.GetType().IsValueType && !visited.Add(root)) return;  // cycle guard
    foreach (var f in root.GetType().GetFields()) {
        var v = f.GetValue(root);
        if (v == null) continue;
        var path = pathPrefix + "." + f.Name;

        // Case A: single teStructuredDataAssetRef<T>
        if (v is ISerializable_STU ss) {
            var gf = v.GetType().GetField("GUID");
            if (gf?.GetValue(v) is teResourceGUID rg && (ulong)rg != 0 && teResourceGUID.Type((ulong)rg) == 0x003) {
                visit((ulong)rg, path);
            }
            continue;  // don't recurse into an asset ref wrapper
        }
        // Case B: array of teStructuredDataAssetRef<T>
        if (v is Array arr) {
            var et = arr.GetType().GetElementType();
            if (et != null && typeof(ISerializable_STU).IsAssignableFrom(et)) {
                for (int i = 0; i < arr.Length; i++) {
                    var item = arr.GetValue(i);
                    if (item == null) continue;
                    var gf2 = item.GetType().GetField("GUID");
                    if (gf2?.GetValue(item) is teResourceGUID rg2 && (ulong)rg2 != 0 && teResourceGUID.Type((ulong)rg2) == 0x003) {
                        visit((ulong)rg2, $"{path}[{i}]");
                    }
                }
                continue;
            }
            // Array of STU instances — recurse.
            if (et != null && typeof(STUInstance).IsAssignableFrom(et)) {
                for (int i = 0; i < arr.Length; i++) {
                    var item = arr.GetValue(i);
                    if (item != null) CollectEntityAssetRefsDeep(item, visit, $"{path}[{i}]", depth + 1, visited);
                }
                continue;
            }
        }
        // Case C: Recurse into nested STU instances (skip primitives / enums / strings).
        if (v is STUInstance inst) {
            CollectEntityAssetRefsDeep(inst, visit, path, depth + 1, visited);
        }
        // Case D: ConfigVar — also check its m_EE729DCB directly.
        if (v is STUConfigVar cv && cv.m_EE729DCB != 0 && teResourceGUID.Type(cv.m_EE729DCB) == 0x003) {
            visit(cv.m_EE729DCB, $"{path}.m_EE729DCB");
        }
    }
}

int traceHeroCount = 0, traceGraphCount = 0, traceEntityRefs = 0;
int heroLevelRefs = 0, componentLevelRefs = 0;
foreach (var kvp in tank.m_assets) {
    if (teResourceGUID.Type(kvp.Key) != 0x75) continue;
    var hero = ReadSTU<STUHero>(kvp.Key);
    if (hero == null || hero.m_64DC571F == 0 || hero.m_gameplayEntity == 0) continue;
    var heroIdx = teResourceGUID.Index(kvp.Key);

    STUEntityDefinition? entDef;
    try { entDef = ReadSTU<STUEntityDefinition>(hero.m_gameplayEntity); }
    catch { continue; }
    if (entDef?.m_componentMap == null) continue;

    // Hero-level entity sweep: STUHero has m_gameplayEntity, m_322C521A, m_26D71549, m_8125713E,
    // m_previewEmoteEntity, m_0F4BFD2C, m_D207D258 — all entity refs bound to this hero.
    // Capture them all even when they aren't spawned via graph nodes.
    CollectEntityAssetRefsDeep(hero, (eg, path) => {
        if (teResourceGUID.Type(eg) != 0x003) return;
        AddOrigin(teResourceGUID.Index(eg), heroIdx, 0, 0,
            $"STUHero{path}", "HeroFieldRef", null);
        heroLevelRefs++;
    }, "", 0, null);

    // EntityDefinition + component sweep: catches turrets whose ref lives in some obscure
    // component field like m_child, m_entity, m_spawnedEntity etc. (Symmetra/Torbjorn turrets,
    // Illari pylon, Lifeweaver platform all surface this way.)
    CollectEntityAssetRefsDeep(entDef, (eg, path) => {
        if (teResourceGUID.Type(eg) != 0x003) return;
        var egIdx = teResourceGUID.Index(eg);
        if (egIdx == teResourceGUID.Index(hero.m_gameplayEntity)) return;  // self-ref
        AddOrigin(egIdx, heroIdx, 0, 0,
            $"EntityDef{path}", "EntityDefFieldRef", null);
        componentLevelRefs++;
    }, "", 0, null);

    // Loadout sweep: walk every STULoadout this hero owns and harvest entity refs. A lot of
    // placed / summoned entities (Sym sentry, Widow mine, Lifeweaver platform) have their
    // entity_def embedded inside the ability's own ConfigVar slots.
    if (hero.m_heroLoadout != null) {
        foreach (var lr in hero.m_heroLoadout) {
            if (lr == 0) continue;
            STULoadout? lo;
            try { lo = ReadSTU<STULoadout>(lr); } catch { continue; }
            if (lo == null) continue;
            var lid = teResourceGUID.Index(lr);
            var lname = ReadString(lo.m_name);
            var lbutton = ReadString(ReadSTU<STU_C5243F93>(lo.m_logicalButton)?.m_name);
            CollectEntityAssetRefsDeep(lo, (eg, path) => {
                if (teResourceGUID.Type(eg) != 0x003) return;
                var egIdx = teResourceGUID.Index(eg);
                if (!entityOrigins.TryGetValue(egIdx, out var list)) {
                    list = new List<object>();
                    entityOrigins[egIdx] = list;
                }
                list.Add(new {
                    hero_id = $"0x{heroIdx:X}",
                    hero_name = heroNameMap.TryGetValue(heroIdx, out var hn) ? hn : null,
                    graph_index = (string?)null,
                    slot_index = (uint?)null,
                    node_id = 0u,
                    node_type = "LoadoutFieldRef",
                    source_field = $"STULoadout(0x{lid:X}:{lname}){path}",
                    loadout_id = $"0x{lid:X}",
                    loadout_name = lname,
                    loadout_button = lbutton,
                });
                componentLevelRefs++;
            }, "", 0, null);
        }
    }

    STUStatescriptComponent? ssC = null;
    foreach (var c in entDef.m_componentMap)
        if (c.Value is STUStatescriptComponent sc) { ssC = sc; break; }
    if (ssC?.m_B634821A == null) continue;
    traceHeroCount++;

    uint slotIndex = 0;
    foreach (var gwo in ssC.m_B634821A) {
        var graphGuid = (ulong)gwo.m_graph;
        if (graphGuid == 0) { slotIndex++; continue; }
        STUStatescriptGraph? graph;
        try { graph = ReadSTU<STUStatescriptGraph>(graphGuid); } catch { slotIndex++; continue; }
        if (graph?.m_nodes == null) { slotIndex++; continue; }
        traceGraphCount++;
        var gIdx = teResourceGUID.Index(graphGuid);

        foreach (var node in graph.m_nodes) {
            if (node == null) continue;
            // 1) STUStatescriptActionCreateEntity.m_entityDef
            if (node is STUStatescriptActionCreateEntity ace && ace.m_entityDef != null) {
                foreach (var g in ConfigVarStaticGuids(ace.m_entityDef)) {
                    if (teResourceGUID.Type(g) != 0x003) continue;
                    AddOrigin(teResourceGUID.Index(g), heroIdx, gIdx, node.m_uniqueID,
                        "ActionCreateEntity.m_entityDef", "STUStatescriptActionCreateEntity", slotIndex);
                    traceEntityRefs++;
                }
            }
            // 2) STUStatescriptStateCreateEntity.m_entityDef
            if (node is STUStatescriptStateCreateEntity sce && sce.m_entityDef != null) {
                foreach (var g in ConfigVarStaticGuids(sce.m_entityDef)) {
                    if (teResourceGUID.Type(g) != 0x003) continue;
                    AddOrigin(teResourceGUID.Index(g), heroIdx, gIdx, node.m_uniqueID,
                        "StateCreateEntity.m_entityDef", "STUStatescriptStateCreateEntity", slotIndex);
                    traceEntityRefs++;
                }
            }
            // 3) STUStatescriptStateWeaponVolley.m_projectileEntity (STUStatescriptWeaponProjectileEntity)
            if (node is STUStatescriptStateWeaponVolley wv && wv.m_projectileEntity != null) {
                var pe = wv.m_projectileEntity;
                // 3a. m_entityDef ConfigVar
                if (pe.m_entityDef != null) {
                    foreach (var g in ConfigVarStaticGuids(pe.m_entityDef)) {
                        if (teResourceGUID.Type(g) != 0x003) continue;
                        AddOrigin(teResourceGUID.Index(g), heroIdx, gIdx, node.m_uniqueID,
                            "WeaponVolley.m_projectileEntity.m_entityDef", "STUStatescriptStateWeaponVolley", slotIndex);
                        traceEntityRefs++;
                    }
                }
                // 3b. m_915AF62D static asset refs array
                if (pe.m_915AF62D != null) {
                    for (int ai = 0; ai < pe.m_915AF62D.Length; ai++) {
                        var aref = pe.m_915AF62D[ai];
                        ulong refGuid = (ulong)aref.GUID;
                        if (refGuid == 0 || teResourceGUID.Type(refGuid) != 0x003) continue;
                        AddOrigin(teResourceGUID.Index(refGuid), heroIdx, gIdx, node.m_uniqueID,
                            $"WeaponVolley.m_projectileEntity.m_915AF62D[{ai}]",
                            "STUStatescriptStateWeaponVolley", slotIndex);
                        traceEntityRefs++;
                    }
                }
            }
            // 4) Generic fallback: walk any STUConfigVar field and pick up static entity refs.
            //    Some abilities spawn entities via their own bespoke state class — this catches them.
            //    Guarded by depth/recursion to avoid pathological cases.
            {
                void Walk(object o, int depth) {
                    if (o == null || depth > 2) return;
                    foreach (var f in o.GetType().GetFields()) {
                        var v = f.GetValue(o);
                        if (v == null) continue;
                        if (v is STUConfigVar cv) {
                            foreach (var g in ConfigVarStaticGuids(cv)) {
                                if (teResourceGUID.Type(g) != 0x003) continue;
                                AddOrigin(teResourceGUID.Index(g), heroIdx, gIdx, node.m_uniqueID,
                                    $"{node.GetType().Name}.{f.Name}", node.GetType().Name, slotIndex);
                                traceEntityRefs++;
                            }
                        } else if (v is STUInstance si && !(v is STUConfigVar)) {
                            Walk(si, depth + 1);
                        } else if (v is ISerializable_STU ss) {
                            var gf = v.GetType().GetField("GUID");
                            if (gf?.GetValue(v) is teResourceGUID rg && (ulong)rg != 0 && teResourceGUID.Type((ulong)rg) == 0x003) {
                                AddOrigin(teResourceGUID.Index((ulong)rg), heroIdx, gIdx, node.m_uniqueID,
                                    $"{node.GetType().Name}.{f.Name}(static)", node.GetType().Name, slotIndex);
                                traceEntityRefs++;
                            }
                        }
                    }
                }
                // Only walk nodes we haven't already covered above (cheap guard).
                if (!(node is STUStatescriptActionCreateEntity) &&
                    !(node is STUStatescriptStateCreateEntity) &&
                    !(node is STUStatescriptStateWeaponVolley)) {
                    Walk(node, 0);
                }
            }
        }
        slotIndex++;
    }
}
Console.Error.WriteLine($"  scanned {traceHeroCount} heroes, {traceGraphCount} graphs, {traceEntityRefs} entity refs (graph-level)");
Console.Error.WriteLine($"  hero-level refs: {heroLevelRefs}, entity-def component refs: {componentLevelRefs}");
Console.Error.WriteLine($"  unique entities with an origin: {entityOrigins.Count}");

// Build a name candidate map: entity_id → best-guess name
// Strategy: pick the origin whose hero + graph slot has the most "useful" loadout candidate.
// Heuristic: prefer non-PassiveAbility, then by first non-null name.
var entityNameCandidates = new Dictionary<uint, string>();
foreach (var kv in entityOrigins) {
    uint entId = kv.Key;
    // group origins by hero, then attach the hero's loadout info for clue
    foreach (var o in kv.Value) {
        var je = JsonSerializer.SerializeToElement(o);
        var heroIdStr = je.GetProperty("hero_id").GetString() ?? "0x0";
        var heroName = je.GetProperty("hero_name").ValueKind == JsonValueKind.String ? je.GetProperty("hero_name").GetString() : null;
        if (string.IsNullOrEmpty(heroName)) continue;
        uint slotIdx = je.TryGetProperty("slot_index", out var si) && si.ValueKind == JsonValueKind.Number ? si.GetUInt32() : uint.MaxValue;
        // Only use the first origin per entity (stable order)
        if (entityNameCandidates.ContainsKey(entId)) continue;
        // Fallback "<HeroName>_slot{n}" — we don't yet know which loadout corresponds to each slot,
        // but the hero name alone is hugely useful (e.g. entity 0x2658 → "Symmetra_slot3" is enough
        // to identify it as a Symmetra-spawned entity; PY layer can refine later).
        var label = slotIdx < uint.MaxValue ? $"{heroName}_slot{slotIdx}" : $"{heroName}_entity";
        entityNameCandidates[entId] = label;
    }
}

// Export entity_origins.json — keyed by entity_id, each with a list of origin contexts.
var entityOriginList = entityOrigins
    .OrderBy(kv => kv.Key)
    .Select(kv => new {
        entity_id = $"0x{kv.Key:X}",
        entity_id_dec = kv.Key,
        origin_count = kv.Value.Count,
        name_candidate = entityNameCandidates.TryGetValue(kv.Key, out var n) ? n : null,
        origins = kv.Value,
    }).ToList();
File.WriteAllText(Path.Combine(outDir, "entity_origins.json"),
    JsonSerializer.Serialize(entityOriginList, jsonOpt));
Console.Error.WriteLine($"Wrote {entityOriginList.Count} entity origins to entity_origins.json");

// ============ 5.5. Ability / Graph Semantic Aggregation ============
// For each hero, each statescript graph, produce a structured summary:
//   * WeaponVolley ballistic data (speed/lifetime/pellets/rate/projectile_entity)
//   * ModifyHealth nodes (damage/heal amount as literal or "dynamic")
//   * DeflectProjectile nodes + filters
//   * HealthPool nodes (pool size, type)
//   * ChaseVar nodes (destination var)
//   * SetVar nodes (target var_id)
//   * CreateEntity nodes (spawned entity GUIDs)
//   * Union of writes_vars / reads_vars across all nodes
// This is the ONE file the SDK generator needs to understand what each ability actually does.
Console.Error.WriteLine("Aggregating ability semantics per graph...");

// Describe a ConfigVar for the SDK generator. The kind field is the essential discriminator.
// Literal values (float/int/bool) come from the dedicated STUConfigVar<T> subclasses.
// Dynamic references (STUConfigVarDynamic) carry a var_id — resolved at runtime.
// Static asset references live in subclass fields (teStructuredDataAssetRef<T>); we extract
// the first non-zero one via reflection.
object DescribeConfigVar(STUConfigVar? cv) {
    if (cv == null) return new { kind = "null" };
    if (cv is STUConfigVarFloat fv) return new { kind = "literal", type = "float", value = (object)fv.m_value };
    if (cv is STUConfigVarInt iv) return new { kind = "literal", type = "int", value = (object)iv.m_value };
    if (cv is STUConfigVarBool bv) return new { kind = "literal", type = "bool", value = (object)(bv.m_value != 0) };
    var cvTypeName = cv.GetType().Name;
    // Dynamic var (var_id handle)
    if (cvTypeName == "STUConfigVarDynamic" || cvTypeName == "STU_076E0DBA") {
        var idField = cv.GetType().GetField("m_identifier");
        uint dynId = 0;
        if (idField != null) { try { dynId = Convert.ToUInt32(idField.GetValue(cv)); } catch { } }
        if (dynId != 0) return new { kind = "dynamic", cv_type = cvTypeName, var_id = $"0x{dynId:X}" };
        if (cv.m_EE729DCB != 0 && teResourceGUID.Type(cv.m_EE729DCB) == 0x1C)
            return new { kind = "dynamic", cv_type = cvTypeName, var_id = $"0x{teResourceGUID.Index(cv.m_EE729DCB):X}" };
        return new { kind = "dynamic", cv_type = cvTypeName, var_id = (string?)null };
    }
    // Check base field
    if (cv.m_EE729DCB != 0) {
        var t = teResourceGUID.Type(cv.m_EE729DCB);
        if (t == 0x1C) return new { kind = "var_ref", cv_type = cvTypeName, var_id = $"0x{teResourceGUID.Index(cv.m_EE729DCB):X}" };
        return new { kind = "asset_ref", cv_type = cvTypeName, guid = $"0x{teResourceGUID.Index(cv.m_EE729DCB):X}.{t:X3}" };
    }
    // Reflect subclass fields — pick up teStructuredDataAssetRef<T> values.
    foreach (var f in cv.GetType().GetFields()) {
        var v = f.GetValue(cv);
        if (v == null) continue;
        if (v is ISerializable_STU) {
            var gf = v.GetType().GetField("GUID");
            if (gf?.GetValue(v) is teResourceGUID rg && (ulong)rg != 0) {
                ulong g = (ulong)rg;
                var t = teResourceGUID.Type(g);
                return new { kind = "asset_ref", cv_type = cvTypeName, field = f.Name,
                    guid = $"0x{teResourceGUID.Index(g):X}.{t:X3}" };
            }
        }
    }
    return new { kind = "empty", cv_type = cvTypeName };
}

// Recursively walk ANY object looking for STUConfigVarLoadout / STUConfigVarLogicalButton.
// These two ConfigVar subclasses are the smoking guns for graph↔loadout association:
//   STUConfigVarLoadout.m_loadout    → direct loadout GUID the graph talks about
//   STUConfigVarLogicalButton.m_logicalButton → STULogicalButton enum value (the "key" id)
// A graph may contain zero, one, or many such references. We collect them all and let the
// downstream SDK generator decide how to resolve slot_index → concrete loadout_id.
void CollectLoadoutButtonRefs(object? root, HashSet<uint> outLoadoutIds, HashSet<string> outButtonEnums, int depth = 0) {
    if (root == null || depth > 6) return;
    var t = root.GetType();
    // Match by type name (avoids requiring a compile-time reference to rarely-used STUs).
    if (t.Name == "STUConfigVarLoadout") {
        var f = t.GetField("m_loadout");
        if (f?.GetValue(root) is ISerializable_STU sr) {
            var gf = sr.GetType().GetField("GUID");
            if (gf?.GetValue(sr) is teResourceGUID rg && (ulong)rg != 0)
                outLoadoutIds.Add(teResourceGUID.Index((ulong)rg));
        }
    } else if (t.Name == "STUConfigVarLogicalButton") {
        var f = t.GetField("m_logicalButton");
        var v = f?.GetValue(root);
        if (v != null) outButtonEnums.Add(v.ToString() ?? "?");
    }
    // Recurse into sub-objects + arrays, but ONLY into STU-flavoured fields to avoid blow-ups.
    foreach (var f in t.GetFields()) {
        var v = f.GetValue(root);
        if (v == null) continue;
        if (v is STUInstance si) CollectLoadoutButtonRefs(si, outLoadoutIds, outButtonEnums, depth + 1);
        else if (v is Array arr) {
            foreach (var item in arr) {
                if (item is STUInstance si2) CollectLoadoutButtonRefs(si2, outLoadoutIds, outButtonEnums, depth + 1);
            }
        }
    }
}

// Build loadout.m_logicalButton → STULogicalButton enum value map, if possible.
// STULoadout stores button as STU_C5243F93 asset GUID. We try to resolve each loadout
// to a button enum by also looking at the loadout's config vars for STUConfigVarLogicalButton.
// The simpler approach: for each hero loadout, scan the loadout's own config var fields to
// find a STUConfigVarLogicalButton; its m_logicalButton enum is this loadout's button id.
// Result: loadout_id → button_enum_string.
var loadoutButtonEnum = new Dictionary<uint, string>();
foreach (var kvp in tank.m_assets) {
    if (teResourceGUID.Type(kvp.Key) != 0x9E) continue;
    var lo = ReadSTU<STULoadout>(kvp.Key);
    if (lo == null) continue;
    var lset = new HashSet<uint>();
    var bset = new HashSet<string>();
    CollectLoadoutButtonRefs(lo, lset, bset);
    if (bset.Count > 0) {
        loadoutButtonEnum[teResourceGUID.Index(kvp.Key)] = bset.First();
    }
}
Console.Error.WriteLine($"  -- Loadout→button enum map: {loadoutButtonEnum.Count}/793 loadouts resolved --");

var abilityHeroList = new List<object>();
int abilityHeroCount = 0, abilityGraphCount = 0;
int abWV = 0, abMH = 0, abDefl = 0, abHP = 0, abChase = 0, abSetVar = 0, abCreate = 0;

foreach (var kvp in tank.m_assets) {
    if (teResourceGUID.Type(kvp.Key) != 0x75) continue;
    var hero = ReadSTU<STUHero>(kvp.Key);
    if (hero == null || hero.m_64DC571F == 0 || hero.m_gameplayEntity == 0) continue;
    var heroIdx = teResourceGUID.Index(kvp.Key);
    var heroName = ReadString(hero.m_0EDCE350);

    STUEntityDefinition? entDef;
    try { entDef = ReadSTU<STUEntityDefinition>(hero.m_gameplayEntity); }
    catch { continue; }
    if (entDef?.m_componentMap == null) continue;
    STUStatescriptComponent? ssC = null;
    foreach (var c in entDef.m_componentMap)
        if (c.Value is STUStatescriptComponent sc) { ssC = sc; break; }
    if (ssC?.m_B634821A == null) continue;
    abilityHeroCount++;

    // Per-hero loadout enumeration (for cross-ref)
    var loadoutsOut = new List<object>();
    if (heroLoadoutsMap.TryGetValue(heroIdx, out var heroLos)) {
        foreach (var (lid, lname, lbutton, lcat) in heroLos) {
            loadoutsOut.Add(new {
                loadout_id = $"0x{lid:X}",
                name = lname,
                button = lbutton,
                category = lcat,
            });
        }
    }

    var graphsOut = new List<object>();
    uint slotIdx = 0;
    foreach (var gwo in ssC.m_B634821A) {
        var graphGuid = (ulong)gwo.m_graph;
        if (graphGuid == 0) { slotIdx++; continue; }
        STUStatescriptGraph? graph;
        try { graph = ReadSTU<STUStatescriptGraph>(graphGuid); } catch { slotIdx++; continue; }
        if (graph?.m_nodes == null) { slotIdx++; continue; }
        abilityGraphCount++;
        var gIdx = teResourceGUID.Index(graphGuid);

        var weaponVolleys = new List<object>();
        var modifyHealths = new List<object>();
        var deflects = new List<object>();
        var healthPools = new List<object>();
        var chaseVars = new List<object>();
        var setVars = new List<object>();
        var createEntities = new List<object>();
        var logicalButtonRefs = new List<object>();
        var unionWrites = new HashSet<uint>();
        var unionReads = new HashSet<uint>();
        var nodeTypeHist = new Dictionary<string, int>();

        foreach (var node in graph.m_nodes) {
            if (node == null) continue;
            var nt = node.GetType().Name;
            nodeTypeHist[nt] = nodeTypeHist.GetValueOrDefault(nt) + 1;

            // union var reads/writes
            if (node.m_BF5B22B7 != null)
                foreach (var sv in node.m_BF5B22B7)
                    if (sv?.m_0D09D2D9 != 0) unionWrites.Add(teResourceGUID.Index(sv.m_0D09D2D9));
            if (node.m_8BF03679 != null)
                foreach (var sv in node.m_8BF03679)
                    if (sv?.m_0D09D2D9 != 0) unionReads.Add(teResourceGUID.Index(sv.m_0D09D2D9));

            // Per-type extraction
            if (node is STUStatescriptStateWeaponVolley wv) {
                abWV++;
                string? projName = null;
                string? projGuidStr = null;
                if (wv.m_projectileEntity?.m_entityDef != null) {
                    // ConfigVarStaticGuids reflects subclass fields — catches STU_8556841E.m_entityDef etc.
                    foreach (var pg in ConfigVarStaticGuids(wv.m_projectileEntity.m_entityDef)) {
                        if (teResourceGUID.Type(pg) != 0x003) continue;
                        projGuidStr = $"0x{teResourceGUID.Index(pg):X}";
                        if (entityNameCandidates.TryGetValue(teResourceGUID.Index(pg), out var c)) projName = c;
                        break;
                    }
                }
                weaponVolleys.Add(new {
                    node_id = node.m_uniqueID,
                    speed = DescribeConfigVar(wv.m_projectileSpeed),
                    lifetime = DescribeConfigVar(wv.m_projectileLifetime),
                    pellets = DescribeConfigVar(wv.m_numProjectilesPerShot),
                    fire_rate = DescribeConfigVar(wv.m_numShotsPerSecond),
                    aim_id = DescribeConfigVar(wv.m_aimID),
                    projectile_entity_id = projGuidStr,
                    projectile_entity_name = projName,
                });
            }
            else if (node is STUStatescriptStateHealthPool hp) {
                abHP++;
                healthPools.Add(new {
                    node_id = node.m_uniqueID,
                    amount = DescribeConfigVar(hp.m_amount),
                });
            }
            else if (node is STUStatescriptStateDeflectProjectiles dp) {
                abDefl++;
                deflects.Add(new {
                    node_id = node.m_uniqueID,
                    box_count = dp.m_boxes?.Length ?? 0,
                });
            }
            else if (node is STUStatescriptStateChaseVar cvar) {
                abChase++;
                chaseVars.Add(new {
                    node_id = node.m_uniqueID,
                    destination = DescribeConfigVar(cvar.m_destination),
                });
            }
            else if (node is STUStatescriptActionSetVar sv2) {
                abSetVar++;
                setVars.Add(new {
                    node_id = node.m_uniqueID,
                    index = DescribeConfigVar(sv2.m_index),
                    key = DescribeConfigVar(sv2.m_key),
                    value = DescribeConfigVar(sv2.m_value),
                    // out_Var (STU_076E0DBA) is a dynamic var ref pointing at the target
                    out_var = sv2.m_out_Var != null ? DescribeConfigVar(sv2.m_out_Var) : null,
                });
            }
            else if (node is STUStatescriptActionCreateEntity ace) {
                abCreate++;
                string? epid = null;
                foreach (var g in ConfigVarStaticGuids(ace.m_entityDef)) {
                    if (teResourceGUID.Type(g) != 0x003) continue;
                    epid = $"0x{teResourceGUID.Index(g):X}";
                    break;
                }
                createEntities.Add(new {
                    node_id = node.m_uniqueID,
                    kind = "Action",
                    entity_def = DescribeConfigVar(ace.m_entityDef),
                    entity_id = epid,
                });
            }
            else if (node is STUStatescriptStateCreateEntity sce) {
                abCreate++;
                string? epid = null;
                foreach (var g in ConfigVarStaticGuids(sce.m_entityDef)) {
                    if (teResourceGUID.Type(g) != 0x003) continue;
                    epid = $"0x{teResourceGUID.Index(g):X}";
                    break;
                }
                createEntities.Add(new {
                    node_id = node.m_uniqueID,
                    kind = "State",
                    entity_def = DescribeConfigVar(sce.m_entityDef),
                    entity_id = epid,
                });
            }
            else if (node is STUStatescriptStateLogicalButton lb) {
                logicalButtonRefs.Add(new {
                    node_id = node.m_uniqueID,
                    button = DescribeConfigVar(lb.m_logicalButton),
                });
            }

            // ModifyHealth is embedded inside some node types — scan reflectively.
            foreach (var f in nt == nameof(STUStatescriptModifyHealth) ? new[] { (System.Reflection.FieldInfo?)null } : node.GetType().GetFields()) {
                if (f == null) continue;
                var v = f.GetValue(node);
                if (v is STUStatescriptModifyHealth mh) {
                    abMH++;
                    modifyHealths.Add(new {
                        node_id = node.m_uniqueID,
                        owner_field = f.Name,
                        amount = DescribeConfigVar(mh.m_amount),
                        knockback_rings = mh.m_knockbackRings?.Length ?? 0,
                        tag_count = mh.m_148F3152?.Length ?? 0,
                    });
                }
            }
        }

        // After walking all nodes, sweep the entire graph once for loadout / button refs.
        // This is the primary slot→loadout linker: if a graph's nodes mention STUConfigVarLoadout,
        // that loadout GUID is our best guess for "this graph's owning ability". If it mentions
        // STUConfigVarLogicalButton, we intersect the button enum with the hero's loadouts
        // (which we've pre-resolved into loadoutButtonEnum).
        var refLoadoutIds = new HashSet<uint>();
        var refButtonEnums = new HashSet<string>();
        foreach (var node in graph.m_nodes) {
            if (node != null) CollectLoadoutButtonRefs(node, refLoadoutIds, refButtonEnums);
        }

        // Pick the best loadout match: prefer one that's in this hero's own heroLoadout list.
        uint? associatedLoadoutId = null;
        string? associatedLoadoutName = null;
        string? associationMethod = null;
        if (heroLoadoutsMap.TryGetValue(heroIdx, out var heroLosForAssoc)) {
            var heroLoSet = heroLosForAssoc.Select(x => x.lid).ToHashSet();
            // Method 1: direct STUConfigVarLoadout hit intersecting hero's own loadouts
            foreach (var refId in refLoadoutIds) {
                if (heroLoSet.Contains(refId)) {
                    associatedLoadoutId = refId;
                    associatedLoadoutName = heroLosForAssoc.First(x => x.lid == refId).name;
                    associationMethod = "ConfigVarLoadout_direct";
                    break;
                }
            }
            // Method 2: if a button enum was observed in the graph, find hero loadouts whose
            // resolved button enum matches — great for LMB/RMB/Shift graphs without direct
            // loadout refs.
            if (associatedLoadoutId == null && refButtonEnums.Count > 0) {
                foreach (var (lid, lname, _, _) in heroLosForAssoc) {
                    if (!loadoutButtonEnum.TryGetValue(lid, out var benum)) continue;
                    if (refButtonEnums.Contains(benum)) {
                        associatedLoadoutId = lid;
                        associatedLoadoutName = lname;
                        associationMethod = "LogicalButton_enum";
                        break;
                    }
                }
            }
        }

        graphsOut.Add(new {
            slot_index = slotIdx,
            graph_index = $"0x{gIdx:X}",
            graph_guid = $"0x{graphGuid:X16}",
            total_nodes = graph.m_nodes.Length,
            node_type_histogram = nodeTypeHist.OrderByDescending(x => x.Value).Take(10).Select(x => new { type = x.Key, count = x.Value }).ToList(),
            // Graph ↔ loadout association (best effort, may be null)
            associated_loadout_id = associatedLoadoutId.HasValue ? $"0x{associatedLoadoutId.Value:X}" : null,
            associated_loadout_name = associatedLoadoutName,
            association_method = associationMethod,
            // All candidate references for downstream overrides
            referenced_loadout_ids = refLoadoutIds.OrderBy(x => x).Select(x => $"0x{x:X}").ToList(),
            referenced_button_enums = refButtonEnums.OrderBy(x => x).ToList(),
            logical_button_refs = logicalButtonRefs,
            weapon_volleys = weaponVolleys,
            modify_health = modifyHealths,
            deflect_projectiles = deflects,
            health_pools = healthPools,
            chase_vars = chaseVars,
            set_vars = setVars,
            create_entities = createEntities,
            writes_vars = unionWrites.OrderBy(x => x).Select(x => $"0x{x:X}").ToList(),
            reads_vars = unionReads.OrderBy(x => x).Select(x => $"0x{x:X}").ToList(),
        });
        slotIdx++;
    }

    abilityHeroList.Add(new {
        hero_id = $"0x{heroIdx:X}",
        hero_name = heroName,
        loadout_count = loadoutsOut.Count,
        graph_count = graphsOut.Count,
        loadouts = loadoutsOut,
        graphs = graphsOut,
    });
}
Console.Error.WriteLine($"  aggregated {abilityHeroCount} heroes, {abilityGraphCount} graphs");
Console.Error.WriteLine($"  nodes captured: WeaponVolley={abWV} ModifyHealth={abMH} Deflect={abDefl} HealthPool={abHP} ChaseVar={abChase} SetVar={abSetVar} CreateEntity={abCreate}");
File.WriteAllText(Path.Combine(outDir, "abilities.json"),
    JsonSerializer.Serialize(abilityHeroList, jsonOpt));
Console.Error.WriteLine($"Wrote {abilityHeroList.Count} heroes to abilities.json");

// ============ 6. ALL Entities — full STU asset enumeration ============
// Scans every asset in the CASC archive and tries to parse as STUEntityDefinition.
// Successful parses → dumped with name (extracted from STULocaleString hash if present)
// Use case: cheat needs to identify ALL entities (turrets, projectiles, summons, AI bots),
// not just hero pawns. Forum-known IDs (Sym Turret 0x2658, Bap Drone 0x29A9, etc.)
// will appear here even though they are not STUHero entries.
Console.Error.WriteLine("Dumping ALL entities (STUEntityDefinition full scan)...");
var allEntities = new List<object>();
var entityTypeStats = new Dictionary<ushort, (int total, int parsed)>();

// STUEntityDefinition GUID type code = 0x003 (verified in OWLib DataTool/FindLogic/Combo.cs:483 `case 0x3:`)
const ushort kEntityTypeCode = 0x003;
HashSet<ushort> tryTypes = new() { kEntityTypeCode };

// Pre-count for progress reporting
int totalEntityCandidates = tank.m_assets.Count(kvp => teResourceGUID.Type(kvp.Key) == kEntityTypeCode);
Console.Error.WriteLine($"  type 0x003 (STUEntityDefinition) total = {totalEntityCandidates}");

// Load KnownFields.csv tables for hash → name mapping
// (Both Data/ and DataPreHashChange/ — Data/ wins on conflict.)
var knownHashes = new Dictionary<uint, string>();
foreach (var path in new[] { "deps/OWLib/TankLibHelper/DataPreHashChange/KnownFields.csv",
                              "deps/OWLib/TankLibHelper/Data/KnownFields.csv" }) {
    if (!File.Exists(path)) continue;
    foreach (var line in File.ReadLines(path)) {
        var parts = line.Split(',', 2);
        if (parts.Length != 2) continue;
        if (uint.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.HexNumber, null, out var h))
            knownHashes[h] = parts[1].Trim();
    }
}
// User-supplied extras
knownHashes[0x99238F34] = "m_isBot";
knownHashes[0x738D89E1] = "m_isBullet";
Console.Error.WriteLine($"  loaded {knownHashes.Count} known field/type hash names");

// Bool entity-classification fields (hash → tag label)
var boolTagFields = new Dictionary<uint, string> {
    { 0x99238F34, "isBot" },
    { 0x738D89E1, "isBullet" },
    { 0x007CC6B1, "isTurret" },
    { 0xC2854590, "isHostile" },
    { 0xAD409EB1, "isFrozen" },
    { 0x95B166DF, "isResurrection" },
    { 0xE9E2290E, "isRevealed" },
    { 0x82804596, "isActive" },
    { 0x9C08D5A4, "isArea" },
    { 0xA198200E, "isUsable" },
};

// Helper: try to extract a display name from STUEntityDefinition's components.
string? ExtractEntityName(STUEntityDefinition entDef) {
    if (entDef.m_componentMap == null) return null;
    foreach (var c in entDef.m_componentMap.Values) {
        if (c == null) continue;
        // Recursively look for any teString / STULocaleString-like field
        var t = c.GetType();
        foreach (var fld in t.GetFields()) {
            try {
                var v = fld.GetValue(c);
                if (v is teString ts) {
                    var sv = ts.Value?.TrimEnd('\0')?.Trim();
                    if (!string.IsNullOrEmpty(sv)) return sv;
                }
                if (v is ulong gid && gid != 0 && (fld.Name.Contains("name", StringComparison.OrdinalIgnoreCase) || fld.Name.Contains("Name"))) {
                    var s = ReadString(gid);
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            } catch { /* ignore reflection errors */ }
        }
    }
    return null;
}

int scannedSoFar = 0;
foreach (var kvp in tank.m_assets) {
    var typeCode = teResourceGUID.Type(kvp.Key);
    if (!tryTypes.Contains(typeCode)) continue;
    scannedSoFar++;
    if (scannedSoFar % 500 == 0) {
        Console.Error.WriteLine($"  ...progress {scannedSoFar}/{totalEntityCandidates}, kept={allEntities.Count}");
    }
    if (!entityTypeStats.ContainsKey(typeCode)) entityTypeStats[typeCode] = (0, 0);
    var (total, parsed) = entityTypeStats[typeCode];
    entityTypeStats[typeCode] = (total + 1, parsed);

    STUEntityDefinition? entDef = null;
    try {
        using var s = tank.OpenFile(kvp.Key);
        if (s == null) continue;
        entDef = new teStructuredData(s).GetInstance<STUEntityDefinition>();
    } catch { continue; }
    if (entDef == null) continue;
    entityTypeStats[typeCode] = (total + 1, parsed + 1);

    var entIdx = teResourceGUID.Index(kvp.Key);
    var compTypes = new HashSet<string>();
    if (entDef.m_componentMap != null) {
        foreach (var c in entDef.m_componentMap) {
            compTypes.Add(c.Value?.GetType().Name ?? "null");
        }
    }

    // FILTER: only keep "interesting" entities (has logic / health / weapon / projectile)
    bool interesting = compTypes.Contains("STUStatescriptComponent")
                    || compTypes.Contains("STUHealthComponent")
                    || compTypes.Contains("STUWeaponComponent")
                    || compTypes.Contains("STUProjectileVisualComponent");
    if (!interesting) continue;

    // Extract entity tags from m_is* fields (only set when bool=true)
    // Walk every component's fields, look up hash in boolTagFields
    var entityTags = new HashSet<string>();
    if (entDef.m_componentMap != null) {
        foreach (var (compHash, comp) in entDef.m_componentMap) {
            if (comp == null) continue;
            // Check the component class itself for STU [STUField] hashes
            // (Use reflection on declared fields; if field name matches hash → check value)
            foreach (var fld in comp.GetType().GetFields()) {
                // m_isBot stored as field name might be hash form (m_99238F34) or named
                // Try by field name match first (cheap)
                string? matchedTag = null;
                foreach (var (h, tag2) in boolTagFields) {
                    if (fld.Name == $"m_{h:X8}" || fld.Name == "m_" + boolTagFields[h]) {
                        matchedTag = tag2;
                        break;
                    }
                }
                if (matchedTag == null) continue;
                try {
                    var v = fld.GetValue(comp);
                    if (v is bool bv && bv) entityTags.Add(matchedTag);
                } catch { }
            }
        }
    }

    // Rough entity-purpose tag from component composition (fallback / coarse classification)
    string compTag = "Other";
    if (compTypes.Contains("STUProjectileVisualComponent")) compTag = "Projectile";
    else if (compTypes.Contains("STUStatescriptComponent") && compTypes.Contains("STUHealthComponent")) compTag = "Pawn";
    else if (compTypes.Contains("STUStatescriptComponent")) compTag = "Scripted";
    else if (compTypes.Contains("STUHealthComponent")) compTag = "Destructible";
    else if (compTypes.Contains("STUWeaponComponent")) compTag = "Weapon";

    var name = ExtractEntityName(entDef);
    // Fold in entity_origins tracing: if a hero's graph created / fires this entity,
    // we now know the owner. This replaces the Python-side forum_known hardcoded map.
    string? originOwnerHero = null;
    string? originName = null;
    int originRefCount = 0;
    if (entityOrigins.TryGetValue(entIdx, out var origList)) {
        originRefCount = origList.Count;
        // Pull a representative hero name from the first origin record.
        foreach (var o in origList) {
            var je = JsonSerializer.SerializeToElement(o);
            if (je.TryGetProperty("hero_name", out var hn) && hn.ValueKind == JsonValueKind.String) {
                originOwnerHero = hn.GetString();
                break;
            }
        }
        if (entityNameCandidates.TryGetValue(entIdx, out var cand)) originName = cand;
    }

    allEntities.Add(new {
        entity_id = $"0x{entIdx:X}",
        entity_id_dec = entIdx,
        guid = $"0x{kvp.Key:X16}",
        type_code = $"0x{typeCode:X}",
        tag = compTag,                                    // coarse
        bool_tags = entityTags.ToList(),                  // precise (from m_isBot etc.)
        name_inferred = name,
        origin_owner_hero = originOwnerHero,              // reverse-traced via CreateEntity/WeaponVolley
        origin_name_candidate = originName,               // best-effort label from origin
        origin_ref_count = originRefCount,
        component_count = compTypes.Count,
        component_types = compTypes.ToList(),
    });
}
Console.Error.WriteLine($"  scan complete: {scannedSoFar} type-0x003 assets, {allEntities.Count} kept after filter");

Console.Error.WriteLine($"  Scanned types — parse success rate:");
foreach (var kv in entityTypeStats.OrderBy(x => x.Key)) {
    Console.Error.WriteLine($"    type 0x{kv.Key:X3}  total={kv.Value.total}  parsed_as_entity={kv.Value.parsed}");
}
File.WriteAllText(Path.Combine(outDir, "all_entities.json"),
    JsonSerializer.Serialize(allEntities, jsonOpt));
Console.Error.WriteLine($"Wrote {allEntities.Count} entities to all_entities.json");

Console.Error.WriteLine("Done!");
