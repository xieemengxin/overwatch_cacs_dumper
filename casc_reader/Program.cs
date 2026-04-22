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

        ssDataList.Add(new {
            hero_id = $"0x{heroIdx:X}",
            hero_name = heroName,
            entity_components = componentDump,
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
    JsonSerializer.Serialize(ssDataList, jsonOpt));
Console.Error.WriteLine($"Wrote {ssDataList.Count} hero statescript data to statescript_data.json");

Console.Error.WriteLine("Done!");
