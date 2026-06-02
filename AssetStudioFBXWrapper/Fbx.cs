using AssetStudio.FbxInterop;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;

#if NETFRAMEWORK
using AssetStudio.PInvoke;
#endif

namespace AssetStudio
{
    public static partial class Fbx
    {
#if NETFRAMEWORK
        static Fbx()
        {
            DllLoader.PreloadDll(FbxDll.DllName);
        }
#endif

        public static Vector3 QuaternionToEuler(Quaternion q)
        {
            AsUtilQuaternionToEuler(q.X, q.Y, q.Z, q.W, out var x, out var y, out var z);
            return new Vector3(x, y, z);
        }

        public static Quaternion EulerToQuaternion(Vector3 v)
        {
            AsUtilEulerToQuaternion(v.X, v.Y, v.Z, out var x, out var y, out var z, out var w);
            return new Quaternion(x, y, z, w);
        }

        public static class Exporter
        {
            public static void Export(string path, IImported imported, Settings fbxSettings)
            {
                var file = new FileInfo(path);
                var dir = file.Directory;

                if (!dir.Exists)
                {
                    dir.Create();
                }

                var currentDir = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(dir.FullName);

                var name = Path.GetFileName(path);

                using (var exporter = new FbxExporter(name, imported, fbxSettings))
                {
                    exporter.ExportAll();
                }

                Directory.SetCurrentDirectory(currentDir);
            }
        }

        public sealed class Settings
        {
            public bool EulerFilter { get; set; }
            public float FilterPrecision { get; set; }
            public bool ExportAllNodes { get; set; }
            public bool ExportSkins { get; set; }
            public bool ExportAnimations { get; set; }
            public bool ExportBlendShape { get; set; }
            public bool CastToBone { get; set; }
            public float BoneSize { get; set; }
            public bool ExportAllUvsAsDiffuseMaps { get; set; }
            public float ScaleFactor { get; set; }
            public int FbxVersionIndex { get; set; }
            public int FbxFormat { get; set; }
            public Dictionary<int,int> UvBindings { get; set; }
            public bool IsAscii => FbxFormat == 1;

            public Settings()
            {
                Init();
            }

            public Settings(bool eulerFilter, float filterPrecision, bool exportAllNodes, bool exportSkins, bool exportAnimations, bool exportBlendShape, bool castToBone, float boneSize,
                bool exportAllUvsAsDiffuseMaps, float scaleFactor, int fbxVersionIndex, int fbxFormat, Dictionary<int, int> uvBindings)
            {
                EulerFilter = eulerFilter;
                FilterPrecision = filterPrecision;
                ExportAllNodes = exportAllNodes;
                ExportSkins = exportSkins;
                ExportAnimations = exportAnimations;
                ExportBlendShape = exportBlendShape;
                CastToBone = castToBone;
                BoneSize = (int)boneSize;
                ExportAllUvsAsDiffuseMaps = exportAllUvsAsDiffuseMaps;
                ScaleFactor = scaleFactor;
                FbxVersionIndex = fbxVersionIndex;
                FbxFormat = fbxFormat;
                UvBindings = uvBindings;
            }

            public void Init()
            {
                var uvDict = new Dictionary<int, int>();
                for (var i = 0; i < 8; i++)
                {
                    uvDict[i] = i + 1;
                }

                EulerFilter = true;
                FilterPrecision = 0.25f;
                ExportAllNodes = true;
                ExportSkins = true;
                ExportAnimations = true;
                ExportBlendShape = true;
                CastToBone = false;
                ExportAllUvsAsDiffuseMaps = false;
                BoneSize = 10;
                ScaleFactor = 1.0f;
                FbxFormat = 0;
                FbxVersionIndex = 3;
                UvBindings = uvDict;
            }

            public static Settings FromBase64(string base64String)
            {
                var settingsData = System.Convert.FromBase64String(base64String);
                var settings = new Settings();
                using var document = JsonDocument.Parse(settingsData);
                var root = document.RootElement;
                settings.EulerFilter = ReadBoolean(root, nameof(EulerFilter), settings.EulerFilter);
                settings.FilterPrecision = ReadSingle(root, nameof(FilterPrecision), settings.FilterPrecision);
                settings.ExportAllNodes = ReadBoolean(root, nameof(ExportAllNodes), settings.ExportAllNodes);
                settings.ExportSkins = ReadBoolean(root, nameof(ExportSkins), settings.ExportSkins);
                settings.ExportAnimations = ReadBoolean(root, nameof(ExportAnimations), settings.ExportAnimations);
                settings.ExportBlendShape = ReadBoolean(root, nameof(ExportBlendShape), settings.ExportBlendShape);
                settings.CastToBone = ReadBoolean(root, nameof(CastToBone), settings.CastToBone);
                settings.BoneSize = ReadSingle(root, nameof(BoneSize), settings.BoneSize);
                settings.ExportAllUvsAsDiffuseMaps = ReadBoolean(root, nameof(ExportAllUvsAsDiffuseMaps), settings.ExportAllUvsAsDiffuseMaps);
                settings.ScaleFactor = ReadSingle(root, nameof(ScaleFactor), settings.ScaleFactor);
                settings.FbxVersionIndex = ReadInt32(root, nameof(FbxVersionIndex), settings.FbxVersionIndex);
                settings.FbxFormat = ReadInt32(root, nameof(FbxFormat), settings.FbxFormat);
                if (root.TryGetProperty(nameof(UvBindings), out var uvBindings) && uvBindings.ValueKind == JsonValueKind.Object)
                {
                    settings.UvBindings.Clear();
                    foreach (var property in uvBindings.EnumerateObject())
                    {
                        if (int.TryParse(property.Name, out var key) && property.Value.TryGetInt32(out var value))
                        {
                            settings.UvBindings[key] = value;
                        }
                    }
                }
                return settings;
            }

            public string ToBase64()
            {
                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    writer.WriteBoolean(nameof(EulerFilter), EulerFilter);
                    writer.WriteNumber(nameof(FilterPrecision), FilterPrecision);
                    writer.WriteBoolean(nameof(ExportAllNodes), ExportAllNodes);
                    writer.WriteBoolean(nameof(ExportSkins), ExportSkins);
                    writer.WriteBoolean(nameof(ExportAnimations), ExportAnimations);
                    writer.WriteBoolean(nameof(ExportBlendShape), ExportBlendShape);
                    writer.WriteBoolean(nameof(CastToBone), CastToBone);
                    writer.WriteNumber(nameof(BoneSize), BoneSize);
                    writer.WriteBoolean(nameof(ExportAllUvsAsDiffuseMaps), ExportAllUvsAsDiffuseMaps);
                    writer.WriteNumber(nameof(ScaleFactor), ScaleFactor);
                    writer.WriteNumber(nameof(FbxVersionIndex), FbxVersionIndex);
                    writer.WriteNumber(nameof(FbxFormat), FbxFormat);
                    writer.WritePropertyName(nameof(UvBindings));
                    writer.WriteStartObject();
                    foreach (var binding in UvBindings)
                    {
                        writer.WriteNumber(binding.Key.ToString(), binding.Value);
                    }
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
                return System.Convert.ToBase64String(stream.ToArray());
            }

            private static bool ReadBoolean(JsonElement root, string propertyName, bool defaultValue)
            {
                return root.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? value.GetBoolean()
                    : defaultValue;
            }

            private static int ReadInt32(JsonElement root, string propertyName, int defaultValue)
            {
                return root.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed)
                    ? parsed
                    : defaultValue;
            }

            private static float ReadSingle(JsonElement root, string propertyName, float defaultValue)
            {
                return root.TryGetProperty(propertyName, out var value) && value.TryGetSingle(out var parsed)
                    ? parsed
                    : defaultValue;
            }
        }
    }
}
