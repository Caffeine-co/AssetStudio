#nullable enable

using AssetStudio;
using AssetStudioCLI.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetStudioCLI
{
    public static class AssetStudioCliRunner
    {
        private static Dictionary<long, AssetItem>? activePathIdIndex;
        private static Dictionary<long, int>? activePathIdPositionIndex;
        private static List<AssetItem>? activeObjectList;
        private static readonly byte[] PayloadBundleMagic = Encoding.ASCII.GetBytes("HARUKI_ASSET_PAYLOAD_BUNDLE_V1");

        public static int ActiveObjectIndexCount => activePathIdIndex?.Count ?? 0;

        public static AssetStudioRunResult Run(string[] args, IReadOnlyCollection<long>? exactPathIds = null)
        {
            var phases = new Dictionary<string, long>();
            Measure(phases, "parse_args", () =>
            {
                CLIOptions.Reset();
                CLIOptions.ParseArgs(args);
            });
            if (!CLIOptions.isParsed)
            {
                throw new InvalidOperationException("AssetStudio CLI arguments could not be parsed.");
            }
            return RunParsed(catchExceptions: false, exactPathIds, phases);
        }

        public static AssetStudioInspectResult Inspect(AssetStudioInspectOptions options)
        {
            var phases = new Dictionary<string, long>();
            Measure(phases, "parse_args", () =>
            {
                CLIOptions.Reset();
                CLIOptions.ParseArgs(options.ToCliArgs());
            });
            if (!CLIOptions.isParsed)
            {
                throw new InvalidOperationException("AssetStudio inspect arguments could not be parsed.");
            }

            var cliLogger = new CLILogger();
            Logger.Default = cliLogger;
            Measure(phases, "prepare_run", Studio.PrepareForRun);
            Measure(phases, "show_options", CLIOptions.ShowCurrentOptions);

            try
            {
                if (!Measure(phases, "load_assets", Studio.LoadAssets))
                {
                    return new AssetStudioInspectResult
                    {
                        AssetsFileCount = 0,
                        ExportableAssetCount = 0,
                        Assets = Array.Empty<AssetStudioAssetInfo>(),
                        PhaseMs = phases,
                    };
                }

                Measure(phases, "parse_assets", Studio.ParseAssets);
                if (CLIOptions.filterBy != FilterBy.None)
                {
                    Measure(phases, "filter", Studio.Filter);
                }

                return Measure(phases, "create_inspect_result", () => CreateInspectResult(phases));
            }
            finally
            {
                Measure(phases, "clear", Studio.Clear);
                cliLogger.LogToFile(LoggerEvent.Verbose, "---Inspect ended---");
            }
        }

        public static AssetStudioLoadedSession BeginSession(AssetStudioInspectOptions options)
        {
            var phases = new Dictionary<string, long>();
            Measure(phases, "parse_args", () =>
            {
                CLIOptions.Reset();
                CLIOptions.ParseArgs(options.ToCliArgs());
            });
            if (!CLIOptions.isParsed)
            {
                throw new InvalidOperationException("AssetStudio context arguments could not be parsed.");
            }

            var cliLogger = new CLILogger();
            Logger.Default = cliLogger;
            Measure(phases, "prepare_run", Studio.PrepareForRun);
            Measure(phases, "show_options", CLIOptions.ShowCurrentOptions);

            try
            {
                if (!Measure(phases, "load_assets", Studio.LoadAssets))
                {
                    return new AssetStudioLoadedSession
                    {
                        Loaded = false,
                        InspectResult = new AssetStudioInspectResult
                        {
                            AssetsFileCount = 0,
                            ExportableAssetCount = 0,
                            Assets = Array.Empty<AssetStudioAssetInfo>(),
                            PhaseMs = phases,
                        },
                    };
                }

                Measure(phases, "parse_assets", Studio.ParseAssets);
                if (CLIOptions.filterBy != FilterBy.None)
                {
                    Measure(phases, "filter", Studio.Filter);
                }
                Measure(phases, "build_object_index", BuildActiveObjectIndex);

                return new AssetStudioLoadedSession
                {
                    Loaded = true,
                    InspectResult = Measure(phases, "create_inspect_result", () => CreateInspectResult(phases)),
                };
            }
            catch
            {
                Studio.Clear();
                cliLogger.LogToFile(LoggerEvent.Verbose, "---Context open failed---");
                throw;
            }
        }

        public static AssetStudioRunResult ExportSession(string[] args, IReadOnlyCollection<long>? exactPathIds = null)
        {
            var phases = new Dictionary<string, long>();
            var metrics = new Dictionary<string, long>();
            Measure(phases, "parse_args", () =>
            {
                CLIOptions.Reset();
                CLIOptions.ParseArgs(args);
            });
            if (!CLIOptions.isParsed)
            {
                throw new InvalidOperationException("AssetStudio context export arguments could not be parsed.");
            }

            Measure(phases, "show_options", CLIOptions.ShowCurrentOptions);
            Measure(phases, "exact_path_filter", () => ApplyExactPathIdFilter(exactPathIds));
            if (CLIOptions.o_exportAssetList.Value != ExportListType.None)
            {
                Measure(phases, "export_asset_list", Studio.ExportAssetList);
            }
            ExportCurrentMode(phases, metrics);
            return new AssetStudioRunResult { PhaseMs = phases, Metrics = metrics };
        }

        public static AssetStudioObjectReadResult ReadObject(AssetStudioObjectReadOptions options)
        {
            var phases = new Dictionary<string, long>();
            var item = Measure(phases, "find_object", () => FindActiveObject(options.PathId));
            if (item == null)
            {
                throw new InvalidOperationException($"asset path_id {options.PathId} was not found in the active context");
            }

            var payloadStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var payload = ReadObjectPayload(item, options);
            var payloadElapsedMs = payloadStopwatch.ElapsedMilliseconds;
            phases["read_payload"] = payloadElapsedMs;
            phases["read_payload.asset_type." + PhaseName(item.TypeString)] = payloadElapsedMs;
            phases["read_payload.payload_kind." + PhaseName(payload.PayloadKind)] = payloadElapsedMs;
            foreach (var phase in payload.PhaseMs)
            {
                AddPhase(phases, "read_payload.detail." + phase.Key, phase.Value);
            }
            return new AssetStudioObjectReadResult
            {
                Asset = ToAssetInfo(item),
                Payload = payload.Payload,
                PayloadKind = payload.PayloadKind,
                SuggestedExtension = payload.SuggestedExtension,
                PhaseMs = phases,
            };
        }

        public static void EndSession()
        {
            ClearActiveObjectIndex();
            Studio.Clear();
            Logger.Default = new DummyLogger();
            Progress.Reset();
            Progress.Reset(index: 1);
        }

        public static void ResetProcessLocalState()
        {
            ClearActiveObjectIndex();
            Studio.Clear();
            CLIOptions.Reset();
            Logger.Default = new DummyLogger();
            Progress.Reset();
            Progress.Reset(index: 1);
        }

        internal static AssetStudioRunResult RunParsed(
            bool catchExceptions,
            IReadOnlyCollection<long>? exactPathIds = null,
            Dictionary<string, long>? phases = null,
            Dictionary<string, long>? metrics = null)
        {
            phases ??= new Dictionary<string, long>();
            metrics ??= new Dictionary<string, long>();
            var cliLogger = new CLILogger();
            Logger.Default = cliLogger;
            Measure(phases, "prepare_run", Studio.PrepareForRun);
            Measure(phases, "show_options", CLIOptions.ShowCurrentOptions);

            try
            {
                if (CLIOptions.o_workMode.Value == WorkMode.Extract)
                {
                    Measure(phases, "extract_bundles", Studio.ExtractBundles);
                }
                else if (Measure(phases, "load_assets", Studio.LoadAssets))
                {
                    Measure(phases, "parse_assets", Studio.ParseAssets);
                    if (CLIOptions.filterBy != FilterBy.None)
                    {
                        Measure(phases, "filter", Studio.Filter);
                    }
                    Measure(phases, "exact_path_filter", () => ApplyExactPathIdFilter(exactPathIds));
                    if (CLIOptions.o_exportAssetList.Value != ExportListType.None)
                    {
                        Measure(phases, "export_asset_list", Studio.ExportAssetList);
                    }
                    ExportCurrentMode(phases, metrics);
                }
            }
            catch (Exception ex) when (catchExceptions)
            {
                Logger.Error(ex.ToString());
            }
            finally
            {
                Measure(phases, "clear", Studio.Clear);
                cliLogger.LogToFile(LoggerEvent.Verbose, "---Program ended---");
            }
            return new AssetStudioRunResult { PhaseMs = phases, Metrics = metrics };
        }

        private static AssetStudioInspectResult CreateInspectResult(Dictionary<string, long> phases)
        {
            var assets = (activeObjectList ?? Studio.parsedAssetsList)
                .Select((asset, index) => ToAssetInfo(asset, index))
                .ToArray();

            return new AssetStudioInspectResult
            {
                AssetsFileCount = Studio.assetsManager.AssetsFileList.Count,
                ExportableAssetCount = assets.Length,
                UnityVersion = Studio.assetsManager.AssetsFileList.FirstOrDefault()?.version.ToString(),
                Assets = assets,
                PhaseMs = phases,
            };
        }

        private static void BuildActiveObjectIndex()
        {
            var byPathId = new Dictionary<long, AssetItem>();
            var byPathIdPosition = new Dictionary<long, int>();
            var objects = BuildContextObjectList();
            for (var i = 0; i < objects.Count; i++)
            {
                var asset = objects[i];
                if (byPathId.ContainsKey(asset.m_PathID))
                {
                    continue;
                }

                byPathId.Add(asset.m_PathID, asset);
                byPathIdPosition.Add(asset.m_PathID, i);
            }

            activeObjectList = objects;
            activePathIdIndex = byPathId;
            activePathIdPositionIndex = byPathIdPosition;
        }

        private static void ClearActiveObjectIndex()
        {
            activeObjectList = null;
            activePathIdIndex = null;
            activePathIdPositionIndex = null;
        }

        private static List<AssetItem> BuildContextObjectList()
        {
            var objects = new List<AssetItem>(Studio.parsedAssetsList.Count);
            var nextSyntheticPathId = -1L;
            foreach (var asset in Studio.parsedAssetsList)
            {
                objects.Add(asset);
                if (asset.Asset is Texture2DArray textureArray)
                {
                    var textures = textureArray.TextureList.Count > 0
                        ? textureArray.TextureList
                        : Enumerable.Range(0, Math.Max(textureArray.m_Depth, 0))
                            .Select(layer => new Texture2D(textureArray, layer))
                            .ToList();
                    foreach (var texture in textures)
                    {
                        var fakeItem = new AssetItem(texture)
                        {
                            Text = texture.m_Name,
                            Container = asset.Container,
                            m_PathID = nextSyntheticPathId--,
                        };
                        objects.Add(fakeItem);
                    }
                }
            }
            return objects;
        }

        private static AssetItem? FindActiveObject(long pathId)
        {
            if (activePathIdIndex != null && activePathIdIndex.TryGetValue(pathId, out var indexed))
            {
                return indexed;
            }

            return (activeObjectList ?? Studio.parsedAssetsList).FirstOrDefault(asset => asset.m_PathID == pathId);
        }

        private static int ActiveObjectIndexOf(AssetItem asset)
        {
            if (activePathIdPositionIndex != null &&
                activePathIdPositionIndex.TryGetValue(asset.m_PathID, out var index))
            {
                return index;
            }

            return (activeObjectList ?? Studio.parsedAssetsList).IndexOf(asset);
        }

        private static string PhaseName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            var builder = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
            }
            return builder.ToString();
        }

        private static AssetStudioAssetInfo ToAssetInfo(AssetItem asset, int? index = null)
        {
            return new AssetStudioAssetInfo
            {
                Index = index ?? ActiveObjectIndexOf(asset),
                Name = asset.Text,
                Container = asset.Container,
                Type = asset.TypeString,
                TypeId = (int)asset.Type,
                PathId = asset.m_PathID,
                UniqueId = asset.UniqueID,
                Size = asset.FullSize,
                SourceFile = asset.SourceFile?.originalPath ?? asset.SourceFile?.fullName,
            };
        }

        private static AssetStudioObjectPayload ReadObjectPayload(AssetItem item, AssetStudioObjectReadOptions options)
        {
            var requestedKind = string.IsNullOrWhiteSpace(options.Kind)
                ? "auto"
                : options.Kind.Trim().ToLowerInvariant();
            return item.Asset switch
            {
                Texture2D texture when requestedKind is "auto" or "image" => ReadTexturePayload(texture, options),
                Texture2DArray textureArray when requestedKind is "auto" or "image" or "image_archive" => ReadTextureArrayPayload(textureArray, options),
                Sprite sprite when requestedKind is "auto" or "image" => ReadSpritePayload(sprite, options),
                AudioClip audioClip when requestedKind is "auto" or "audio" or "raw" => ReadAudioClipPayload(audioClip),
                VideoClip videoClip when requestedKind is "auto" or "video" or "raw" => ReadVideoClipPayload(videoClip),
                MovieTexture movieTexture when requestedKind is "auto" or "video" or "raw" => new AssetStudioObjectPayload
                {
                    Payload = movieTexture.m_MovieData ?? Array.Empty<byte>(),
                    PayloadKind = "movie_ogv",
                    SuggestedExtension = ".ogv",
                },
                Font font when requestedKind is "auto" or "font" or "raw" => ReadFontPayload(font),
                Shader shader when requestedKind is "auto" or "shader" or "text" => ReadShaderPayload(shader),
                TextAsset textAsset when requestedKind is "auto" or "text_bytes" => ReadTextAssetPayload(textAsset),
                MonoBehaviour monoBehaviour when requestedKind is "auto" or "typetree_json" => ReadMonoBehaviourPayload(monoBehaviour),
                Mesh mesh when requestedKind is "auto" or "mesh" or "obj" => ReadMeshPayload(mesh),
                Animator _ when requestedKind is "auto" or "animator" or "fbx" => ReadAnimatorPayload(item),
                AssetStudio.Object asset when requestedKind is "auto" or "typetree_json" or "raw" => ReadGenericObjectPayload(asset, requestedKind),
                _ => new AssetStudioObjectPayload
                {
                    Payload = Array.Empty<byte>(),
                    PayloadKind = "unsupported",
                    SuggestedExtension = ".bin",
                },
            };
        }

        private static AssetStudioObjectPayload ReadGenericObjectPayload(AssetStudio.Object asset, string requestedKind)
        {
            var phases = new Dictionary<string, long>();
            if (requestedKind != "raw" && asset.serializedType?.m_Type != null)
            {
                try
                {
                    return new AssetStudioObjectPayload
                    {
                        Payload = Measure(phases, "typetree_json", () => ReadTypeTreeJsonPayload(asset)),
                        PayloadKind = "typetree_json",
                        SuggestedExtension = ".json",
                        PhaseMs = phases,
                    };
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to read {asset.type} path_id {asset.m_PathID} as typetree json, falling back to raw bytes: {ex.Message}");
                }
            }

            return new AssetStudioObjectPayload
            {
                Payload = Measure(phases, "raw_bytes", asset.GetRawData),
                PayloadKind = "raw",
                SuggestedExtension = ".dat",
                PhaseMs = phases,
            };
        }

        private static byte[] ReadTypeTreeJsonPayload(AssetStudio.Object asset)
        {
            var typeTree = asset.serializedType?.m_Type
                ?? throw new InvalidOperationException($"asset path_id {asset.m_PathID} has no typetree");
            asset.reader.Reset();
            using var output = new MemoryStream();
            using var writer = new Utf8JsonWriter(output, new JsonWriterOptions
            {
                Indented = false,
                SkipValidation = false,
            });
            writer.WriteStartObject();
            var nodes = typeTree.m_Nodes;
            for (var i = 1; i < nodes.Count; i++)
            {
                writer.WritePropertyName(JsonFieldName(nodes[i].m_Name, i));
                WriteTypeTreeJsonValue(nodes, asset.reader, ref i, writer);
            }
            writer.WriteEndObject();
            writer.Flush();
            return output.ToArray();
        }

        private static void WriteTypeTreeJsonValue(List<TypeTreeNode> nodes, BinaryReader reader, ref int i, Utf8JsonWriter writer)
        {
            var node = nodes[i];
            var align = (node.m_MetaFlag & 0x4000) != 0;
            switch (node.m_Type)
            {
                case "SInt8":
                    writer.WriteNumberValue(reader.ReadSByte());
                    break;
                case "UInt8":
                    writer.WriteNumberValue(reader.ReadByte());
                    break;
                case "char":
                    writer.WriteStringValue(BitConverter.ToChar(reader.ReadBytes(2), 0).ToString());
                    break;
                case "short":
                case "SInt16":
                    writer.WriteNumberValue(reader.ReadInt16());
                    break;
                case "UInt16":
                case "unsigned short":
                    writer.WriteNumberValue(reader.ReadUInt16());
                    break;
                case "int":
                case "SInt32":
                    writer.WriteNumberValue(reader.ReadInt32());
                    break;
                case "UInt32":
                case "unsigned int":
                case "Type*":
                    writer.WriteNumberValue(reader.ReadUInt32());
                    break;
                case "long long":
                case "SInt64":
                    writer.WriteNumberValue(reader.ReadInt64());
                    break;
                case "UInt64":
                case "unsigned long long":
                case "FileSize":
                    writer.WriteNumberValue(reader.ReadUInt64());
                    break;
                case "float":
                    WriteFiniteNumberOrString(writer, reader.ReadSingle());
                    break;
                case "double":
                    WriteFiniteNumberOrString(writer, reader.ReadDouble());
                    break;
                case "bool":
                    writer.WriteBooleanValue(reader.ReadBoolean());
                    break;
                case "string" when i + 1 < nodes.Count && nodes[i + 1].m_Type == "Array":
                    writer.WriteStringValue(reader.ReadAlignedString());
                    i += GetTypeTreeNodes(nodes, i).Count - 1;
                    break;
                case "map":
                    if ((nodes[i + 1].m_MetaFlag & 0x4000) != 0)
                    {
                        align = true;
                    }
                    var map = GetTypeTreeNodes(nodes, i);
                    i += map.Count - 1;
                    var first = GetTypeTreeNodes(map, 4);
                    var next = 4 + first.Count;
                    var second = GetTypeTreeNodes(map, next);
                    var mapSize = Math.Max(reader.ReadInt32(), 0);
                    writer.WriteStartArray();
                    for (var j = 0; j < mapSize; j++)
                    {
                        var keyIndex = 0;
                        var valueIndex = 0;
                        writer.WriteStartObject();
                        writer.WritePropertyName("key");
                        WriteTypeTreeJsonValue(first, reader, ref keyIndex, writer);
                        writer.WritePropertyName("value");
                        WriteTypeTreeJsonValue(second, reader, ref valueIndex, writer);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                    break;
                case "TypelessData":
                    var typelessSize = Math.Max(reader.ReadInt32(), 0);
                    var offset = typelessSize > 0 ? reader.BaseStream.Position : 0;
                    writer.WriteStartObject();
                    writer.WriteNumber("Offset", offset);
                    writer.WriteNumber("Size", typelessSize);
                    writer.WriteEndObject();
                    reader.BaseStream.Position += typelessSize;
                    i += 2;
                    break;
                default:
                    if (i < nodes.Count - 1 && nodes[i + 1].m_Type == "Array")
                    {
                        if ((nodes[i + 1].m_MetaFlag & 0x4000) != 0)
                        {
                            align = true;
                        }
                        var vector = GetTypeTreeNodes(nodes, i);
                        i += vector.Count - 1;
                        var arraySize = Math.Max(reader.ReadInt32(), 0);
                        writer.WriteStartArray();
                        for (var j = 0; j < arraySize; j++)
                        {
                            var itemIndex = 3;
                            WriteTypeTreeJsonValue(vector, reader, ref itemIndex, writer);
                        }
                        writer.WriteEndArray();
                    }
                    else
                    {
                        var @class = GetTypeTreeNodes(nodes, i);
                        i += @class.Count - 1;
                        writer.WriteStartObject();
                        for (var j = 1; j < @class.Count; j++)
                        {
                            writer.WritePropertyName(JsonFieldName(@class[j].m_Name, j));
                            WriteTypeTreeJsonValue(@class, reader, ref j, writer);
                        }
                        writer.WriteEndObject();
                    }
                    break;
            }
            if (align)
            {
                reader.AlignStream();
            }
        }

        private static void WriteFiniteNumberOrString(Utf8JsonWriter writer, float value)
        {
            if (float.IsFinite(value))
            {
                writer.WriteNumberValue(value);
            }
            else
            {
                writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void WriteFiniteNumberOrString(Utf8JsonWriter writer, double value)
        {
            if (double.IsFinite(value))
            {
                writer.WriteNumberValue(value);
            }
            else
            {
                writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static string JsonFieldName(string? name, int index)
        {
            return string.IsNullOrEmpty(name) ? $"field_{index}" : name;
        }

        private static List<TypeTreeNode> GetTypeTreeNodes(List<TypeTreeNode> nodes, int index)
        {
            var selected = new List<TypeTreeNode> { nodes[index] };
            var level = nodes[index].m_Level;
            for (var i = index + 1; i < nodes.Count; i++)
            {
                if (nodes[i].m_Level <= level)
                {
                    return selected;
                }
                selected.Add(nodes[i]);
            }
            return selected;
        }

        private static AssetStudioObjectPayload ReadAnimatorPayload(AssetItem item)
        {
            var phases = new Dictionary<string, long>();
            var tempDir = Path.Combine(Path.GetTempPath(), "haruki-assetstudio-animator-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                Measure(phases, "animator.export_fbx", () => Exporter.ExportAnimator(item, tempDir));
                return new AssetStudioObjectPayload
                {
                    Payload = Measure(phases, "animator.pack_directory", () => ReadDirectoryPayloadBundle(tempDir)),
                    PayloadKind = "animator_bundle_fbx",
                    SuggestedExtension = "",
                    PhaseMs = phases,
                };
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch
                {
                    // Best-effort cleanup only; preserve the real object read error if export failed.
                }
            }
        }

        private static AssetStudioObjectPayload ReadTextureArrayPayload(Texture2DArray textureArray, AssetStudioObjectReadOptions options)
        {
            var phases = new Dictionary<string, long>();
            var imageFormat = Measure(phases, "image.parse_format", () => ParseImageFormat(options.ImageFormat));
            var bytes = ImageSharpNativeAotGuard.Run(() =>
            {
                var entries = new List<(string Name, byte[] Payload)>();
                var textures = Measure(phases, "texture_array.prepare_layers", () => textureArray.TextureList.Count > 0
                    ? textureArray.TextureList
                    : Enumerable.Range(0, Math.Max(textureArray.m_Depth, 0))
                        .Select(layer => new Texture2D(textureArray, layer))
                        .ToList());
                for (var layer = 0; layer < textures.Count; layer++)
                {
                    using var image = Measure(phases, "texture_array.convert_to_image", () => textures[layer].ConvertToImage(flip: true));
                    if (image == null)
                    {
                        continue;
                    }
                    using var entryStream = new MemoryStream();
                    Measure(phases, "texture_array.encode_image", () => image.WriteToStream(entryStream, imageFormat));
                    entries.Add((
                        $"layer_{layer:D4}.{imageFormat.ToString().ToLowerInvariant()}",
                        entryStream.ToArray()));
                }
                return Measure(phases, "texture_array.pack_layers", () => WritePayloadBundle(entries));
            });
            return new AssetStudioObjectPayload
            {
                Payload = bytes,
                PayloadKind = $"image_array_bundle_{imageFormat.ToString().ToLowerInvariant()}",
                SuggestedExtension = "",
                PhaseMs = phases,
            };
        }

        private static AssetStudioObjectPayload ReadTexturePayload(Texture2D texture, AssetStudioObjectReadOptions options)
        {
            var phases = new Dictionary<string, long>();
            var imageFormat = Measure(phases, "image.parse_format", () => ParseImageFormat(options.ImageFormat));
            var bytes = ImageSharpNativeAotGuard.Run(() =>
            {
                using var image = Measure(phases, "texture.convert_to_image", () => texture.ConvertToImage(flip: true));
                if (image == null)
                {
                    return Array.Empty<byte>();
                }
                using var output = new MemoryStream();
                Measure(phases, "texture.encode_image", () => image.WriteToStream(output, imageFormat));
                return output.ToArray();
            });
            return new AssetStudioObjectPayload
            {
                Payload = bytes,
                PayloadKind = $"image_{imageFormat.ToString().ToLowerInvariant()}",
                SuggestedExtension = "." + imageFormat.ToString().ToLowerInvariant(),
                PhaseMs = phases,
            };
        }

        private static AssetStudioObjectPayload ReadSpritePayload(Sprite sprite, AssetStudioObjectReadOptions options)
        {
            var phases = new Dictionary<string, long>();
            var imageFormat = Measure(phases, "image.parse_format", () => ParseImageFormat(options.ImageFormat));
            var bytes = ImageSharpNativeAotGuard.Run(() =>
            {
                using var image = Measure(phases, "sprite.get_image", () => sprite.GetImage(SpriteMaskMode.On));
                if (image == null)
                {
                    return Array.Empty<byte>();
                }
                using var output = new MemoryStream();
                Measure(phases, "sprite.encode_image", () => image.WriteToStream(output, imageFormat));
                return output.ToArray();
            });
            return new AssetStudioObjectPayload
            {
                Payload = bytes,
                PayloadKind = $"image_{imageFormat.ToString().ToLowerInvariant()}",
                SuggestedExtension = "." + imageFormat.ToString().ToLowerInvariant(),
                PhaseMs = phases,
            };
        }

        private static AssetStudioObjectPayload ReadAudioClipPayload(AudioClip audioClip)
        {
            var phases = new Dictionary<string, long>();
            return new AssetStudioObjectPayload
            {
                Payload = Measure(phases, "audio.get_data", audioClip.m_AudioData.GetData),
                PayloadKind = "audio_raw",
                SuggestedExtension = AudioClipExtension(audioClip),
                PhaseMs = phases,
            };
        }

        private static AssetStudioObjectPayload ReadVideoClipPayload(VideoClip videoClip)
        {
            var phases = new Dictionary<string, long>();
            var extension = Path.GetExtension(videoClip.m_OriginalPath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".video";
            }
            return new AssetStudioObjectPayload
            {
                Payload = Measure(phases, "video.get_data", videoClip.m_VideoData.GetData),
                PayloadKind = "video_raw",
                SuggestedExtension = extension,
                PhaseMs = phases,
            };
        }

        private static AssetStudioObjectPayload ReadFontPayload(Font font)
        {
            var phases = new Dictionary<string, long>();
            var payload = font.m_FontData ?? Array.Empty<byte>();
            var extension = payload.Length >= 4
                && payload[0] == 79
                && payload[1] == 84
                && payload[2] == 84
                && payload[3] == 79
                    ? ".otf"
                    : ".ttf";
            return new AssetStudioObjectPayload
            {
                Payload = payload,
                PayloadKind = "font",
                SuggestedExtension = extension,
                PhaseMs = phases,
            };
        }

        private static AssetStudioObjectPayload ReadShaderPayload(Shader shader)
        {
            var phases = new Dictionary<string, long>();
            return new AssetStudioObjectPayload
            {
                Payload = Measure(phases, "shader.convert_to_utf8", () => Encoding.UTF8.GetBytes(shader.Convert() ?? string.Empty)),
                PayloadKind = "shader_text",
                SuggestedExtension = ".shader",
                PhaseMs = phases,
            };
        }

        private static AssetStudioObjectPayload ReadTextAssetPayload(TextAsset textAsset)
        {
            var phases = new Dictionary<string, long>();
            return new AssetStudioObjectPayload
            {
                Payload = Measure(phases, "text_asset.bytes", () => textAsset.m_Script),
                PayloadKind = "text_bytes",
                SuggestedExtension = ".bytes",
                PhaseMs = phases,
            };
        }

        private static AssetStudioObjectPayload ReadMonoBehaviourPayload(MonoBehaviour monoBehaviour)
        {
            return ReadGenericObjectPayload(monoBehaviour, "typetree_json");
        }

        private static AssetStudioObjectPayload ReadMeshPayload(Mesh mesh)
        {
            var phases = new Dictionary<string, long>();
            Measure(phases, "mesh.process_data", mesh.ProcessData);
            if (mesh.m_VertexCount <= 0 || mesh.m_Vertices == null || mesh.m_Vertices.Length == 0)
            {
                return new AssetStudioObjectPayload
                {
                    Payload = Array.Empty<byte>(),
                    PayloadKind = "mesh_obj",
                    SuggestedExtension = ".obj",
                    PhaseMs = phases,
                };
            }

            var sb = Measure(phases, "mesh.write_obj", () =>
            {
                var builder = new StringBuilder();
                builder.AppendLine("g " + mesh.m_Name);

                var componentCount = mesh.m_Vertices.Length == mesh.m_VertexCount * 4 ? 4 : 3;
                for (var vertex = 0; vertex < mesh.m_VertexCount; vertex++)
                {
                    builder.Append(CultureInfo.InvariantCulture, $"v {-mesh.m_Vertices[vertex * componentCount]} {mesh.m_Vertices[vertex * componentCount + 1]} {mesh.m_Vertices[vertex * componentCount + 2]}\r\n");
                }

                if (mesh.m_UV0?.Length > 0)
                {
                    componentCount = 4;
                    if (mesh.m_UV0.Length == mesh.m_VertexCount * 2)
                    {
                        componentCount = 2;
                    }
                    else if (mesh.m_UV0.Length == mesh.m_VertexCount * 3)
                    {
                        componentCount = 3;
                    }

                    for (var vertex = 0; vertex < mesh.m_VertexCount; vertex++)
                    {
                        builder.Append(CultureInfo.InvariantCulture, $"vt {mesh.m_UV0[vertex * componentCount]} {mesh.m_UV0[vertex * componentCount + 1]}\r\n");
                    }
                }

                if (mesh.m_Normals?.Length > 0)
                {
                    componentCount = mesh.m_Normals.Length == mesh.m_VertexCount * 4 ? 4 : 3;
                    for (var vertex = 0; vertex < mesh.m_VertexCount; vertex++)
                    {
                        builder.Append(CultureInfo.InvariantCulture, $"vn {-mesh.m_Normals[vertex * componentCount]} {mesh.m_Normals[vertex * componentCount + 1]} {mesh.m_Normals[vertex * componentCount + 2]}\r\n");
                    }
                }

                var sum = 0;
                for (var subMeshIndex = 0; subMeshIndex < mesh.m_SubMeshes.Count; subMeshIndex++)
                {
                    builder.AppendLine($"g {mesh.m_Name}_{subMeshIndex}");
                    var indexCount = (int)mesh.m_SubMeshes[subMeshIndex].indexCount;
                    var end = sum + indexCount / 3;
                    for (var face = sum; face < end; face++)
                    {
                        builder.Append(CultureInfo.InvariantCulture, $"f {mesh.m_Indices[face * 3 + 2] + 1}/{mesh.m_Indices[face * 3 + 2] + 1}/{mesh.m_Indices[face * 3 + 2] + 1} {mesh.m_Indices[face * 3 + 1] + 1}/{mesh.m_Indices[face * 3 + 1] + 1}/{mesh.m_Indices[face * 3 + 1] + 1} {mesh.m_Indices[face * 3] + 1}/{mesh.m_Indices[face * 3] + 1}/{mesh.m_Indices[face * 3] + 1}\r\n");
                    }
                    sum = end;
                }

                return builder;
            });

            return new AssetStudioObjectPayload
            {
                Payload = Measure(phases, "mesh.encode_utf8", () => Encoding.UTF8.GetBytes(sb.Replace("NaN", "0").ToString())),
                PayloadKind = "mesh_obj",
                SuggestedExtension = ".obj",
                PhaseMs = phases,
            };
        }

        private static string AudioClipExtension(AudioClip audioClip)
        {
            if (audioClip.version < 5)
            {
                return audioClip.m_Type switch
                {
                    FMODSoundType.AAC => ".m4a",
                    FMODSoundType.AIFF => ".aif",
                    FMODSoundType.IT => ".it",
                    FMODSoundType.MOD => ".mod",
                    FMODSoundType.MPEG => ".mp3",
                    FMODSoundType.OGGVORBIS => ".ogg",
                    FMODSoundType.S3M => ".s3m",
                    FMODSoundType.WAV => ".wav",
                    FMODSoundType.XM => ".xm",
                    FMODSoundType.XMA => ".wav",
                    FMODSoundType.VAG => ".vag",
                    FMODSoundType.AUDIOQUEUE => ".fsb",
                    _ => ".AudioClip",
                };
            }

            return audioClip.m_CompressionFormat switch
            {
                AudioCompressionFormat.AAC => ".m4a",
                _ => ".fsb",
            };
        }

        private static byte[] ReadDirectoryPayloadBundle(string directory)
        {
            var entries = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => (
                    Name: Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/'),
                    Payload: File.ReadAllBytes(path)))
                .ToArray();
            return WritePayloadBundle(entries);
        }

        private static byte[] WritePayloadBundle(IReadOnlyCollection<(string Name, byte[] Payload)> entries)
        {
            using var output = new MemoryStream(EstimatePayloadBundleCapacity(entries));
            using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
            writer.Write(PayloadBundleMagic);
            writer.Write(entries.Count);
            foreach (var (name, payload) in entries)
            {
                var nameBytes = Encoding.UTF8.GetBytes(name);
                writer.Write(nameBytes.Length);
                writer.Write((long)payload.Length);
                writer.Write(nameBytes);
                writer.Write(payload);
            }
            writer.Flush();
            return output.ToArray();
        }

        private static int EstimatePayloadBundleCapacity(IReadOnlyCollection<(string Name, byte[] Payload)> entries)
        {
            long capacity = PayloadBundleMagic.Length + sizeof(int);
            foreach (var (name, payload) in entries)
            {
                capacity += sizeof(int) + sizeof(long) + Encoding.UTF8.GetByteCount(name) + payload.Length;
                if (capacity > int.MaxValue)
                {
                    return 0;
                }
            }
            return (int)capacity;
        }

        private static ImageFormat ParseImageFormat(string? imageFormat)
        {
            return imageFormat?.Trim().ToLowerInvariant() switch
            {
                "png" => ImageFormat.Png,
                "bmp" or null or "" => ImageFormat.Bmp,
                _ => throw new ArgumentException($"unsupported image_format `{imageFormat}`"),
            };
        }

        private static void ExportCurrentMode(Dictionary<string, long> phases, Dictionary<string, long> metrics)
        {
            switch (CLIOptions.o_workMode.Value)
            {
                case WorkMode.Info:
                    Measure(phases, "show_exportable_assets_info", Studio.ShowExportableAssetsInfo);
                    break;
                case WorkMode.Live2D:
                    Measure(phases, "export_live2d", Studio.ExportLive2D);
                    break;
                case WorkMode.SplitObjects:
                    Measure(phases, "export_split_objects", Studio.ExportSplitObjects);
                    break;
                case WorkMode.Animator:
                    Measure(phases, "export_animator", Studio.ExportAnimator);
                    break;
                default:
                    ParallelExporter.ResetDiagnostics();
                    Measure(phases, "export_assets", Studio.ExportAssets);
                    foreach (var phase in ParallelExporter.SnapshotTimingMs())
                    {
                        AddPhase(phases, phase.Key, phase.Value);
                    }
                    foreach (var metric in ParallelExporter.SnapshotMetrics())
                    {
                        AddPhase(metrics, metric.Key, metric.Value);
                    }
                    break;
            }
        }

        private static void Measure(Dictionary<string, long> phases, string name, Action action)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            action();
            AddPhase(phases, name, stopwatch.ElapsedMilliseconds);
        }

        private static T Measure<T>(Dictionary<string, long> phases, string name, Func<T> action)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = action();
            AddPhase(phases, name, stopwatch.ElapsedMilliseconds);
            return result;
        }

        private static void AddPhase(Dictionary<string, long> phases, string name, long elapsedMs)
        {
            phases[name] = phases.TryGetValue(name, out var current)
                ? current + elapsedMs
                : elapsedMs;
        }

        private static void ApplyExactPathIdFilter(IReadOnlyCollection<long>? exactPathIds)
        {
            if (exactPathIds == null || exactPathIds.Count == 0)
            {
                return;
            }

            var pathIdSet = exactPathIds.ToHashSet();
            Studio.parsedAssetsList = Studio.parsedAssetsList
                .Where(asset => pathIdSet.Contains(asset.m_PathID))
                .ToList();
        }
    }

    public sealed class AssetStudioLoadedSession
    {
        public bool Loaded { get; set; }
        public AssetStudioInspectResult InspectResult { get; set; } = new AssetStudioInspectResult();
    }

    public sealed class AssetStudioRunResult
    {
        public IReadOnlyDictionary<string, long> PhaseMs { get; set; } = new Dictionary<string, long>();
        public IReadOnlyDictionary<string, long> Metrics { get; set; } = new Dictionary<string, long>();
    }

    public sealed class AssetStudioObjectReadOptions
    {
        public long PathId { get; set; }
        public string Kind { get; set; } = "auto";
        public string ImageFormat { get; set; } = "bmp";
    }

    public sealed class AssetStudioObjectReadResult
    {
        public AssetStudioAssetInfo Asset { get; set; } = new AssetStudioAssetInfo();
        public byte[] Payload { get; set; } = Array.Empty<byte>();
        public string PayloadKind { get; set; } = "";
        public string SuggestedExtension { get; set; } = "";
        public IReadOnlyDictionary<string, long> PhaseMs { get; set; } = new Dictionary<string, long>();
    }

    internal sealed class AssetStudioObjectPayload
    {
        public byte[] Payload { get; set; } = Array.Empty<byte>();
        public string PayloadKind { get; set; } = "";
        public string SuggestedExtension { get; set; } = "";
        public IReadOnlyDictionary<string, long> PhaseMs { get; set; } = new Dictionary<string, long>();
    }

    public sealed class AssetStudioInspectOptions
    {
        public string InputPath { get; set; } = "";
        public IReadOnlyCollection<string>? AssetTypes { get; set; }
        public string? UnityVersion { get; set; }
        public bool FilterExcludeMode { get; set; }
        public bool FilterWithRegex { get; set; }
        public string? FilterByName { get; set; }
        public string? FilterByContainer { get; set; }
        public IReadOnlyCollection<long>? FilterByPathIds { get; set; }
        public bool LoadAllAssets { get; set; }
        public string? OutputDir { get; set; }

        public string[] ToCliArgs()
        {
            if (string.IsNullOrWhiteSpace(InputPath))
            {
                throw new ArgumentException("input_path is required");
            }

            var args = new List<string>
            {
                InputPath,
                "-m",
                "info",
                "-o",
                string.IsNullOrWhiteSpace(OutputDir)
                    ? Path.Combine(Path.GetTempPath(), "assetstudio-inspect-" + Guid.NewGuid().ToString("N"))
                    : OutputDir,
            };

            if (AssetTypes != null && AssetTypes.Count > 0)
            {
                args.Add("-t");
                args.Add(string.Join(",", AssetTypes));
            }
            if (!string.IsNullOrWhiteSpace(UnityVersion))
            {
                args.Add("--unity-version");
                args.Add(UnityVersion);
            }
            if (FilterExcludeMode)
            {
                args.Add("--filter-exclude-mode");
            }
            if (FilterWithRegex)
            {
                args.Add("--filter-with-regex");
            }
            if (!string.IsNullOrWhiteSpace(FilterByName))
            {
                args.Add("--filter-by-name");
                args.Add(FilterByName);
            }
            if (!string.IsNullOrWhiteSpace(FilterByContainer))
            {
                args.Add("--filter-by-container");
                args.Add(FilterByContainer);
            }
            if (FilterByPathIds != null && FilterByPathIds.Count > 0)
            {
                args.Add("--filter-by-pathid");
                args.Add(string.Join(",", FilterByPathIds));
            }
            if (LoadAllAssets)
            {
                args.Add("--load-all");
            }

            return args.ToArray();
        }
    }

    public sealed class AssetStudioInspectResult
    {
        public int AssetsFileCount { get; set; }
        public int ExportableAssetCount { get; set; }
        public string? UnityVersion { get; set; }
        public IReadOnlyCollection<AssetStudioAssetInfo> Assets { get; set; } = Array.Empty<AssetStudioAssetInfo>();
        public IReadOnlyDictionary<string, long> PhaseMs { get; set; } = new Dictionary<string, long>();
    }

    public sealed class AssetStudioAssetInfo
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("container")]
        public string? Container { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("type_id")]
        public int TypeId { get; set; }

        [JsonPropertyName("path_id")]
        public long PathId { get; set; }

        [JsonPropertyName("unique_id")]
        public string? UniqueId { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("source_file")]
        public string? SourceFile { get; set; }
    }
}
