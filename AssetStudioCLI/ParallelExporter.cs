using AssetStudio;
using AssetStudioCLI.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace AssetStudioCLI
{
    internal static class ParallelExportDiagnostics
    {
        private static readonly ConcurrentDictionary<string, long> TimingTicks = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, long> Counters = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);

        public static void Reset()
        {
            TimingTicks.Clear();
            Counters.Clear();
            ImageSharpNativeAotGuard.TimingSink = AddTicks;
        }

        public static IReadOnlyDictionary<string, long> SnapshotTimingMs()
        {
            var snapshot = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var timing in TimingTicks)
            {
                snapshot[timing.Key] = (long)Math.Round(timing.Value * 1000.0 / Stopwatch.Frequency);
            }
            return snapshot;
        }

        public static IReadOnlyDictionary<string, long> SnapshotMetrics()
        {
            var snapshot = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var counter in Counters)
            {
                snapshot[counter.Key] = counter.Value;
            }
            return snapshot;
        }

        public static void Count(string name)
        {
            Counters.AddOrUpdate(name, 1, (_, value) => value + 1);
        }

        public static T Measure<T>(string name, Func<T> action)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                return action();
            }
            finally
            {
                AddTicks(name, Stopwatch.GetTimestamp() - started);
            }
        }

        public static void Measure(string name, Action action)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                action();
            }
            finally
            {
                AddTicks(name, Stopwatch.GetTimestamp() - started);
            }
        }

        private static void AddTicks(string name, long elapsedTicks)
        {
            TimingTicks.AddOrUpdate(name, elapsedTicks, (_, value) => value + elapsedTicks);
        }
    }

    internal static class ParallelExporter
    {
        private static readonly ConcurrentDictionary<string, bool> ExportPathDict = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public static void ResetDiagnostics()
        {
            ParallelExportDiagnostics.Reset();
        }

        public static IReadOnlyDictionary<string, long> SnapshotTimingMs()
        {
            return ParallelExportDiagnostics.SnapshotTimingMs();
        }

        public static IReadOnlyDictionary<string, long> SnapshotMetrics()
        {
            return ParallelExportDiagnostics.SnapshotMetrics();
        }

        public static bool ExportTexture2D(AssetItem item, string exportPath, out string debugLog)
        {
            debugLog = "";
            ParallelExportDiagnostics.Count("parallel.texture2d.count");
            var m_Texture2D = (Texture2D)item.Asset;
            if (CLIOptions.convertTexture)
            {
                var type = CLIOptions.o_imageFormat.Value;
                string exportFullPath = string.Empty;
                var canExport = ParallelExportDiagnostics.Measure(
                    "parallel.texture2d.try_export_file",
                    () => TryExportFile(exportPath, item, "." + type.ToString().ToLower(), out exportFullPath));
                if (!canExport)
                    return false;

                if (CLIOptions.o_logLevel.Value <= LoggerEvent.Debug)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Converting {item.TypeString} \"{m_Texture2D.m_Name}\" to {type}..");
                    sb.AppendLine($"Width: {m_Texture2D.m_Width}");
                    sb.AppendLine($"Height: {m_Texture2D.m_Height}");
                    sb.AppendLine($"Format: {m_Texture2D.m_TextureFormat}");
                    switch (m_Texture2D.m_TextureSettings.m_FilterMode)
                    {
                        case 0: sb.AppendLine("Filter Mode: Point "); break;
                        case 1: sb.AppendLine("Filter Mode: Bilinear "); break;
                        case 2: sb.AppendLine("Filter Mode: Trilinear "); break;
                    }
                    sb.AppendLine($"Anisotropic level: {m_Texture2D.m_TextureSettings.m_Aniso}");
                    sb.AppendLine($"Mip map bias: {m_Texture2D.m_TextureSettings.m_MipBias}");
                    switch (m_Texture2D.m_TextureSettings.m_WrapMode)
                    {
                        case 0: sb.AppendLine($"Wrap mode: Repeat"); break;
                        case 1: sb.AppendLine($"Wrap mode: Clamp"); break;
                    }
                    debugLog += sb.ToString();
                }

                var log = debugLog;
                var exported = ImageSharpNativeAotGuard.Run(() =>
                {
                    var image = ParallelExportDiagnostics.Measure(
                        "parallel.texture2d.convert_to_image",
                        () => m_Texture2D.ConvertToImage(flip: true));
                    if (image == null)
                    {
                        Logger.Error($"{log}Export error. Failed to convert texture \"{m_Texture2D.m_Name}\" into image");
                        return false;
                    }
                    using (image)
                    {
                        using (var file = File.OpenWrite(exportFullPath))
                        {
                            ParallelExportDiagnostics.Measure(
                                "parallel.texture2d.write_image",
                                () => image.WriteToStream(file, type));
                        }
                        log += $"{item.TypeString} \"{item.Text}\" exported to \"{exportFullPath}\"";
                        return true;
                    }
                });
                debugLog = log;
                return exported;
            }

            string rawExportFullPath = string.Empty;
            var canExportRaw = ParallelExportDiagnostics.Measure(
                "parallel.texture2d.raw_try_export_file",
                () => TryExportFile(exportPath, item, ".tex", out rawExportFullPath));
            if (!canExportRaw)
                return false;
            var rawData = ParallelExportDiagnostics.Measure(
                "parallel.texture2d.raw_get_data",
                () => m_Texture2D.image_data.GetData());
            ParallelExportDiagnostics.Measure(
                "parallel.texture2d.raw_write",
                () => File.WriteAllBytes(rawExportFullPath, rawData));
            debugLog += $"{item.TypeString} \"{item.Text}\" exported to \"{rawExportFullPath}\"";
            return true;
        }

        public static bool ExportSprite(AssetItem item, string exportPath, out string debugLog)
        {
            debugLog = "";
            ParallelExportDiagnostics.Count("parallel.sprite.count");
            var type = CLIOptions.o_imageFormat.Value;
            var alphaMask = SpriteMaskMode.On;
            string exportFullPath = string.Empty;
            var canExport = ParallelExportDiagnostics.Measure(
                "parallel.sprite.try_export_file",
                () => TryExportFile(exportPath, item, "." + type.ToString().ToLower(), out exportFullPath));
            if (!canExport)
                return false;

            var spriteLog = debugLog;
            var spriteExported = ImageSharpNativeAotGuard.Run(() =>
            {
                var image = ParallelExportDiagnostics.Measure(
                    "parallel.sprite.get_image",
                    () => ((Sprite)item.Asset).GetImage(alphaMask));
                if (image != null)
                {
                    using (image)
                    {
                        using (var file = File.OpenWrite(exportFullPath))
                        {
                            ParallelExportDiagnostics.Measure(
                                "parallel.sprite.write_image",
                                () => image.WriteToStream(file, type));
                        }
                        spriteLog += $"{item.TypeString} \"{item.Text}\" exported to \"{exportFullPath}\"";
                        return true;
                    }
                }
                return false;
            });
            debugLog = spriteLog;
            return spriteExported;
        }

        public static bool ExportAudioClip(AssetItem item, string exportPath, out string debugLog)
        {
            debugLog = string.Empty;
            ParallelExportDiagnostics.Count("parallel.audio.count");
            var m_AudioClip = (AudioClip)item.Asset;
            var m_AudioData = BigArrayPool<byte>.Shared.Rent(m_AudioClip.m_AudioData.Size);
            try
            {
                string exportFullPath = string.Empty;
                var dataLen = ParallelExportDiagnostics.Measure(
                    "parallel.audio.get_data",
                    () => m_AudioClip.m_AudioData.GetData(m_AudioData));
                if (dataLen <= 0)
                {
                    Logger.Error($"Export error. \"{item.Text}\": AudioData was not found");
                    return false;
                }
                var converter = new AudioClipConverter(m_AudioClip);
                if (CLIOptions.o_audioFormat.Value != AudioFormat.None && (converter.IsSupport || converter.IsLegacy))
                {
                    var canExport = ParallelExportDiagnostics.Measure(
                        "parallel.audio.try_export_file",
                        () => TryExportFile(exportPath, item, ".wav", out exportFullPath));
                    if (!canExport)
                        return false;

                    if (CLIOptions.o_logLevel.Value <= LoggerEvent.Debug)
                    {
                        debugLog += $"Converting {item.TypeString} \"{m_AudioClip.m_Name}\" to wav..\n";
                        debugLog += GenerateAudioClipInfo(m_AudioClip);
                    }

                    var audioLog = debugLog;
                    var buffer = ParallelExportDiagnostics.Measure(
                        "parallel.audio.convert_wav",
                        () => converter.IsLegacy
                            ? converter.RawAudioClipToWav(ref audioLog)
                            : converter.ConvertToWav(m_AudioData, ref audioLog));
                    debugLog = audioLog;
                    if (buffer == null)
                    {
                        Logger.Error($"{debugLog}Export error. \"{item.Text}\": Failed to convert fmod audio to Wav");
                        return false;
                    }
                    ParallelExportDiagnostics.Measure(
                        "parallel.audio.write",
                        () => File.WriteAllBytes(exportFullPath, buffer));
                }
                else
                {
                    var canExport = ParallelExportDiagnostics.Measure(
                        "parallel.audio.try_export_file",
                        () => TryExportFile(exportPath, item, converter.GetExtensionName(), out exportFullPath));
                    if (!canExport)
                        return false;

                    if (CLIOptions.o_logLevel.Value <= LoggerEvent.Debug)
                    {
                        debugLog += $"Exporting non-fmod {item.TypeString} \"{m_AudioClip.m_Name}\"..\n";
                        debugLog += GenerateAudioClipInfo(m_AudioClip);
                    }
                    using (var file = File.OpenWrite(exportFullPath))
                    {
                        ParallelExportDiagnostics.Measure(
                            "parallel.audio.write",
                            () => file.Write(m_AudioData, 0, m_AudioClip.m_AudioData.Size));
                    }
                }
                debugLog += $"{item.TypeString} \"{item.Text}\" exported to \"{exportFullPath}\"";
                return true;
            }
            finally
            {
                BigArrayPool<byte>.Shared.Return(m_AudioData, clearArray: true);
            }
        }

        private static string GenerateAudioClipInfo(AudioClip m_AudioClip)
        {
            var sb = new StringBuilder();
            if (m_AudioClip.version >= (2, 6))
            {
                sb.AppendLine(m_AudioClip.version < 5
                    ? $"AudioClip type: {m_AudioClip.m_Type}"
                    : $"AudioClip compression format: {m_AudioClip.m_CompressionFormat}");
                if (m_AudioClip.version >= 5)
                {
                    sb.AppendLine($"AudioClip channel count: {m_AudioClip.m_Channels}");
                    sb.AppendLine($"AudioClip sample rate: {m_AudioClip.m_Frequency}");
                    sb.AppendLine($"AudioClip bit depth: {m_AudioClip.m_BitsPerSample}");
                }
            }
            else
            {
                var isRawWav = m_AudioClip.m_Format != 0x05;
                sb.AppendLine($"Is raw wav data: {isRawWav}");
                if (isRawWav)
                    sb.AppendLine($"AudioClip channel count: {m_AudioClip.m_Channels}");
                sb.AppendLine($"AudioClip sample rate: {m_AudioClip.m_Frequency}");
            }
            return sb.ToString();
        }

        private static bool TryExportFile(string dir, AssetItem item, string extension, out string fullPath)
        {
            var fileName = FixFileName(item.Text);
            var filenameFormat = CLIOptions.o_filenameFormat.Value;
            var canOverwrite = CLIOptions.f_overwriteExisting.Value;
            switch (filenameFormat)
            {
                case FilenameFormat.AssetName_PathID:
                    fileName = $"{fileName} @{item.m_PathID}";
                    break;
                case FilenameFormat.PathID:
                    fileName = item.m_PathID.ToString();
                    break;
            }
            fullPath = Path.Combine(dir, fileName + extension);
            if (ExportPathDict.TryAdd(fullPath, true))
            {
                if (CanWrite(fullPath, dir, canOverwrite))
                {
                    return true;
                }
            }
            else if (filenameFormat == FilenameFormat.AssetName)
            {
                fullPath = Path.Combine(dir, fileName + item.UniqueID + extension);
                if (CanWrite(fullPath, dir, canOverwrite))
                {
                    return true;
                }
            }
            Logger.Error($"Export error. File \"{fullPath.Color(ColorConsole.BrightRed)}\" already exist");
            return false;
        }

        private static bool CanWrite(string fullPath, string dir, bool canOverwrite)
        {
            if (!canOverwrite && File.Exists(fullPath))
                return false;
            Directory.CreateDirectory(dir);
            return true;
        }

        public static bool ParallelExportConvertFile(AssetItem item, string exportPath, out string debugLog)
        {
            switch (item.Type)
            {
                case ClassIDType.Texture2D:
                case ClassIDType.Texture2DArrayImage:
                    return ExportTexture2D(item, exportPath, out debugLog);
                case ClassIDType.Sprite:
                    return ExportSprite(item, exportPath, out debugLog);
                case ClassIDType.AudioClip:
                    return ExportAudioClip(item, exportPath, out debugLog);
                default:
                    throw new NotImplementedException();
            }
        }

        private static string FixFileName(string str)
        {
            return str.Length >= 260
                ? Path.GetRandomFileName()
                : Path.GetInvalidFileNameChars().Aggregate(str, (current, c) => current.Replace(c, '_'));
        }

        public static void ClearHash()
        {
            ExportPathDict.Clear();
        }
    }
}
