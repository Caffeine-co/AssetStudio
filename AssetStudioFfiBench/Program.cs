using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

var options = BenchOptions.Parse(args);
if (options == null)
{
    BenchOptions.PrintUsage();
    return 2;
}

using var preparedInput = PreparedInput.Create(options.InputPath, options.DeobfuscateInput);
options.InputPath = preparedInput.Path;

Console.WriteLine("AssetStudio FFI benchmark");
Console.WriteLine($"input:      {options.InputPath}");
if (preparedInput.Mode != "none")
{
    Console.WriteLine($"input mode: {preparedInput.Mode}");
}
Console.WriteLine($"native lib: {options.NativeLibraryPath}");
if (!string.IsNullOrWhiteSpace(options.CliPath))
{
    Console.WriteLine($"cli:        {options.CliPath}");
}
Console.WriteLine($"iterations: warmup={options.WarmupIterations} measured={options.Iterations}");
Console.WriteLine($"read:       count={options.ReadCount} kind={options.Kind} image_format={options.ImageFormat}");
if (!string.IsNullOrWhiteSpace(options.UnityVersion))
{
    Console.WriteLine($"unity:      {options.UnityVersion}");
}
Console.WriteLine();

var ffi = new NativeAssetStudio(options.NativeLibraryPath);
var ffiRuns = new List<FfiRunResult>();
var cliRuns = new List<Sample>();

for (var i = 0; i < options.WarmupIterations; i++)
{
    Console.WriteLine($"warmup {i + 1}/{options.WarmupIterations}");
    _ = RunFfi(ffi, options);
    if (!string.IsNullOrWhiteSpace(options.CliPath))
    {
        _ = RunCli(options);
    }
}

for (var i = 0; i < options.Iterations; i++)
{
    Console.WriteLine($"run {i + 1}/{options.Iterations}");
    ffiRuns.Add(RunFfi(ffi, options));
    if (!string.IsNullOrWhiteSpace(options.CliPath))
    {
        cliRuns.Add(RunCli(options));
    }
}

Console.WriteLine();
PrintFfiSummary(ffiRuns);
if (cliRuns.Count > 0)
{
    Console.WriteLine();
    PrintSampleSummary("cli_process", cliRuns);
}

return 0;

static FfiRunResult RunFfi(NativeAssetStudio ffi, BenchOptions options)
{
    var outputDir = Path.Combine(Path.GetTempPath(), "assetstudio-ffi-bench-" + Guid.NewGuid().ToString("N"));
    var assetTypesCsv = options.AssetTypes.Count > 0 ? string.Join(",", options.AssetTypes) : null;
    var open = ffi.Open(options.InputPath, options.UnityVersion, assetTypesCsv, outputDir);
    var contextId = open.ContextId;

    Sample? list = null;
    Sample? readBatch = null;
    Sample closeSample;
    IReadOnlyList<AssetRef> assets = Array.Empty<AssetRef>();
    try
    {
        var defaultListLimit = open.ExportableAssetCount > 0 ? Math.Min(open.ExportableAssetCount, 100) : 100;
        var listResult = ffi.ListObjects(contextId, 0, options.ReadCount > 0 ? options.ReadCount : defaultListLimit, assetTypesCsv);
        assets = listResult.Assets;
        list = listResult.Sample;

        if (options.ReadCount > 0 && assets.Count > 0)
        {
            readBatch = ffi.ReadObjects(contextId, assets.Take(options.ReadCount).Select(x => x.PathId).ToArray(), options.Kind, options.ImageFormat);
        }
    }
    finally
    {
        var close = ffi.Close(contextId);
        open.Sample.AssetCount = open.ExportableAssetCount;
        open.Sample.ObjectIndexCount = open.ObjectIndexCount;
        close.Sample.AssetCount = assets.Count;
        closeSample = close.Sample;
    }

    return new FfiRunResult(open.Sample, list, readBatch, closeSample);
}

static Sample RunCli(BenchOptions options)
{
    var tempOutput = Path.Combine(Path.GetTempPath(), "assetstudio-cli-bench-" + Guid.NewGuid().ToString("N"));
    var arguments = new List<string>
    {
        options.InputPath,
        "-m",
        "info",
        "-o",
        tempOutput,
    };
    if (options.AssetTypes.Count > 0)
    {
        arguments.Add("-t");
        arguments.Add(string.Join(",", options.AssetTypes));
    }
    if (!string.IsNullOrWhiteSpace(options.UnityVersion))
    {
        arguments.Add("--unity-version");
        arguments.Add(options.UnityVersion);
    }

    var startInfo = CreateCliStartInfo(options.CliPath!, arguments);
    var stopwatch = Stopwatch.StartNew();
    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("failed to start CLI process");
    var stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    stopwatch.Stop();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"CLI exited with {process.ExitCode}\nstdout:\n{stdout}\nstderr:\n{stderr}");
    }

    return new Sample("cli_process", stopwatch.Elapsed.TotalMilliseconds);
}

static ProcessStartInfo CreateCliStartInfo(string cliPath, IReadOnlyList<string> arguments)
{
    var startInfo = new ProcessStartInfo
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };

    if (Path.GetExtension(cliPath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
    {
        startInfo.FileName = "dotnet";
        startInfo.ArgumentList.Add(cliPath);
    }
    else
    {
        startInfo.FileName = cliPath;
    }

    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    return startInfo;
}

static void PrintFfiSummary(IReadOnlyList<FfiRunResult> runs)
{
    PrintSampleSummary("ffi_context_open", runs.Select(x => x.Open).ToList());
    PrintSampleSummary("ffi_context_list_objects", runs.Select(x => x.List).Where(x => x != null).Cast<Sample>().ToList());
    PrintSampleSummary("ffi_context_read_objects", runs.Select(x => x.ReadBatch).Where(x => x != null).Cast<Sample>().ToList());
    PrintSampleSummary("ffi_context_close", runs.Select(x => x.Close).ToList());
    PrintSampleSummary("ffi_total", runs.Select(x => new Sample("ffi_total", x.TotalMs)).ToList());
}

static void PrintSampleSummary(string name, IReadOnlyList<Sample> samples)
{
    if (samples.Count == 0)
    {
        return;
    }

    var values = samples.Select(x => x.ElapsedMs).OrderBy(x => x).ToArray();
    var payloadBytes = samples.Sum(x => x.PayloadBytes);
    var assetCount = samples.Max(x => x.AssetCount);
    var objectIndexCount = samples.Max(x => x.ObjectIndexCount);
    Console.WriteLine(
        string.Create(
            CultureInfo.InvariantCulture,
            $"{name,-24} median={Percentile(values, 50),8:F2} ms min={values[0],8:F2} ms max={values[^1],8:F2} ms assets={assetCount} objects={objectIndexCount} payload_bytes={payloadBytes}"));
}

static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
{
    if (sortedValues.Count == 0)
    {
        return 0;
    }

    var rank = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Count);
    return sortedValues[Math.Clamp(rank - 1, 0, sortedValues.Count - 1)];
}

internal sealed class NativeAssetStudio
{
    private readonly FreeDelegate freeBuffer;
    private readonly ResultFreeDelegate resultFree;
    private readonly ContextOpenDelegate contextOpen;
    private readonly ContextListObjectsDelegate contextListObjects;
    private readonly ContextCloseDelegate contextClose;
    private readonly ContextReadObjectsDirectRetryDelegate contextReadObjectsDirectRetry;

    public NativeAssetStudio(string libraryPath)
    {
        var handle = NativeLibrary.Load(libraryPath);
        contextOpen = Load<ContextOpenDelegate>(handle, "haruki_assetstudio_context_open_v1");
        contextListObjects = Load<ContextListObjectsDelegate>(handle, "haruki_assetstudio_context_list_objects_v1");
        contextClose = Load<ContextCloseDelegate>(handle, "haruki_assetstudio_context_close_v1");
        contextReadObjectsDirectRetry = Load<ContextReadObjectsDirectRetryDelegate>(handle, "haruki_assetstudio_context_read_objects_direct_retry_v1");
        freeBuffer = Load<FreeDelegate>(handle, "haruki_assetstudio_free_buffer");
        resultFree = Load<ResultFreeDelegate>(handle, "haruki_assetstudio_result_free");
    }

    public NativeOpenResult Open(string inputPath, string? unityVersion, string? assetTypesCsv, string outputDir)
    {
        var inputPathUtf8 = NativeUtf8.From(inputPath);
        var unityVersionUtf8 = NativeUtf8.From(unityVersion);
        var assetTypesUtf8 = NativeUtf8.From(assetTypesCsv);
        var outputDirUtf8 = NativeUtf8.From(outputDir);
        var request = new NativeContextOpenRequest
        {
            StructSize = Marshal.SizeOf<NativeContextOpenRequest>(),
            InputPathUtf8 = inputPathUtf8.Pointer,
            InputPathUtf8Len = inputPathUtf8.Length,
            UnityVersionUtf8 = unityVersionUtf8.Pointer,
            UnityVersionUtf8Len = unityVersionUtf8.Length,
            AssetTypesCsvUtf8 = assetTypesUtf8.Pointer,
            AssetTypesCsvUtf8Len = assetTypesUtf8.Length,
            OutputDirUtf8 = outputDirUtf8.Pointer,
            OutputDirUtf8Len = outputDirUtf8.Length,
            LoadAllAssets = 0,
        };
        var response = new NativeContextOpenResponse();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var code = contextOpen(ref request, ref response);
            stopwatch.Stop();
            if (code != 0 || response.Status != 0)
            {
                throw new InvalidOperationException($"context_open_v1 returned rc={code} status={response.Status} error={response.ErrorCode}");
            }
            return new NativeOpenResult(
                response.ContextId,
                response.ExportableAssetCount,
                response.ObjectIndexCount,
                new Sample("context_open_v1", stopwatch.Elapsed.TotalMilliseconds));
        }
        finally
        {
            inputPathUtf8.Dispose();
            unityVersionUtf8.Dispose();
            assetTypesUtf8.Dispose();
            outputDirUtf8.Dispose();
            if (response.Buffer != IntPtr.Zero)
            {
                freeBuffer(response.Buffer);
            }
        }
    }

    public NativeListResult ListObjects(long contextId, int offset, int limit, string? assetTypesCsv)
    {
        var assetTypesUtf8 = NativeUtf8.From(assetTypesCsv);
        var request = new NativeObjectListRequest
        {
            StructSize = Marshal.SizeOf<NativeObjectListRequest>(),
            ContextId = contextId,
            Offset = offset,
            Limit = limit,
            AssetTypesCsvUtf8 = assetTypesUtf8.Pointer,
            AssetTypesCsvUtf8Len = assetTypesUtf8.Length,
        };
        var response = new NativeObjectTable();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var code = contextListObjects(ref request, ref response);
            stopwatch.Stop();
            if (code != 0 || response.Status != 0)
            {
                throw new InvalidOperationException($"context_list_objects_v1 returned rc={code} status={response.Status} error={response.ErrorCode}");
            }
            var assets = new List<AssetRef>(Math.Max(0, response.ReturnedCount));
            for (var i = 0; i < response.ReturnedCount; i++)
            {
                var asset = Marshal.PtrToStructure<NativeAssetObject>(IntPtr.Add(response.Objects, i * Marshal.SizeOf<NativeAssetObject>()));
                assets.Add(new AssetRef(asset.PathId));
            }
            return new NativeListResult(assets, new Sample("context_list_objects_v1", stopwatch.Elapsed.TotalMilliseconds)
            {
                AssetCount = response.TotalCount,
            });
        }
        finally
        {
            assetTypesUtf8.Dispose();
            if (response.Buffer != IntPtr.Zero)
            {
                freeBuffer(response.Buffer);
            }
        }
    }

    public Sample ReadObjects(long contextId, IReadOnlyList<long> pathIds, string kind, string imageFormat)
    {
        var kindUtf8 = NativeUtf8.From(kind);
        var imageFormatUtf8 = NativeUtf8.From(imageFormat);
        var itemSize = Marshal.SizeOf<NativeObjectReadItemRequest>();
        var itemsPtr = Marshal.AllocCoTaskMem(itemSize * pathIds.Count);
        var response = new NativeObjectReadBatchRetryResponseV1();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            for (var i = 0; i < pathIds.Count; i++)
            {
                var item = new NativeObjectReadItemRequest
                {
                    PathId = pathIds[i],
                    KindUtf8 = kindUtf8.Pointer,
                    KindUtf8Len = kindUtf8.Length,
                    ImageFormatUtf8 = imageFormatUtf8.Pointer,
                    ImageFormatUtf8Len = imageFormatUtf8.Length,
                };
                Marshal.StructureToPtr(item, IntPtr.Add(itemsPtr, i * itemSize), false);
            }

            var request = new NativeObjectReadBatchIntoRequestV1
            {
                StructSize = Marshal.SizeOf<NativeObjectReadBatchIntoRequestV1>(),
                ContextId = contextId,
                Items = itemsPtr,
                Count = pathIds.Count,
            };
            var code = contextReadObjectsDirectRetry(ref request, ref response);
            stopwatch.Stop();
            if (code != 0 || (response.Status != 0 && response.Status != 9))
            {
                throw new InvalidOperationException($"context_read_objects_direct_retry_v1 returned rc={code} status={response.Status} error={response.ErrorCode}");
            }
            return new Sample("context_read_objects_direct_retry_v1", stopwatch.Elapsed.TotalMilliseconds)
            {
                PayloadBytes = response.PayloadLen,
            };
        }
        finally
        {
            if (response.ResultHandle != 0)
            {
                resultFree(response.ResultHandle);
            }
            Marshal.FreeCoTaskMem(itemsPtr);
            kindUtf8.Dispose();
            imageFormatUtf8.Dispose();
        }
    }

    public NativeCloseResult Close(long contextId)
    {
        var request = new NativeContextCloseRequest
        {
            StructSize = Marshal.SizeOf<NativeContextCloseRequest>(),
            ContextId = contextId,
        };
        var response = new NativeContextCloseResponse();
        var stopwatch = Stopwatch.StartNew();
        var code = contextClose(ref request, ref response);
        stopwatch.Stop();
        if (code != 0 || response.Status != 0)
        {
            throw new InvalidOperationException($"context_close_v1 returned rc={code} status={response.Status} error={response.ErrorCode}");
        }
        return new NativeCloseResult(new Sample("context_close_v1", stopwatch.Elapsed.TotalMilliseconds));
    }

    private static T Load<T>(IntPtr handle, string name)
        where T : Delegate
    {
        return Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(handle, name));
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ContextOpenDelegate(ref NativeContextOpenRequest request, ref NativeContextOpenResponse response);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ContextListObjectsDelegate(ref NativeObjectListRequest request, ref NativeObjectTable response);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ContextCloseDelegate(ref NativeContextCloseRequest request, ref NativeContextCloseResponse response);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ContextReadObjectsDirectRetryDelegate(ref NativeObjectReadBatchIntoRequestV1 request, ref NativeObjectReadBatchRetryResponseV1 response);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeDelegate(IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ResultFreeDelegate(long resultHandle);
}

internal sealed record NativeOpenResult(long ContextId, int ExportableAssetCount, int ObjectIndexCount, Sample Sample);
internal sealed record NativeListResult(IReadOnlyList<AssetRef> Assets, Sample Sample);
internal sealed record NativeCloseResult(Sample Sample);

internal sealed record FfiRunResult(Sample Open, Sample? List, Sample? ReadBatch, Sample Close)
{
    public double TotalMs => Open.ElapsedMs + (List?.ElapsedMs ?? 0) + (ReadBatch?.ElapsedMs ?? 0) + Close.ElapsedMs;
}

internal sealed record AssetRef(long PathId);

internal sealed record Sample(string Name, double ElapsedMs)
{
    public long PayloadBytes { get; init; }
    public int AssetCount { get; set; }
    public int ObjectIndexCount { get; set; }
}

internal readonly struct NativeUtf8 : IDisposable
{
    private NativeUtf8(IntPtr pointer, int length)
    {
        Pointer = pointer;
        Length = length;
    }

    public IntPtr Pointer { get; }
    public int Length { get; }

    public static NativeUtf8 From(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return new NativeUtf8(IntPtr.Zero, 0);
        }
        var bytes = Encoding.UTF8.GetBytes(value);
        var pointer = Marshal.AllocCoTaskMem(bytes.Length);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        return new NativeUtf8(pointer, bytes.Length);
    }

    public void Dispose()
    {
        if (Pointer != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(Pointer);
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeContextOpenRequest
{
    public int StructSize;
    public IntPtr InputPathUtf8;
    public int InputPathUtf8Len;
    public IntPtr UnityVersionUtf8;
    public int UnityVersionUtf8Len;
    public IntPtr AssetTypesCsvUtf8;
    public int AssetTypesCsvUtf8Len;
    public IntPtr OutputDirUtf8;
    public int OutputDirUtf8Len;
    public int LoadAllAssets;
    public int Flags;
    public int Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeContextOpenResponse
{
    public int StructSize;
    public int AbiVersion;
    public int SchemaVersion;
    public int ContextAbiVersion;
    public int Status;
    public int ErrorCode;
    public long ContextId;
    public int AssetsFileCount;
    public int ExportableAssetCount;
    public int ObjectIndexCount;
    public int HasMoreAssets;
    public IntPtr UnityVersionUtf8;
    public int UnityVersionUtf8Len;
    public IntPtr Buffer;
    public long BufferLen;
    public long DurationMs;
    public int Flags;
    public int Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeContextCloseRequest
{
    public int StructSize;
    public long ContextId;
    public int Flags;
    public int Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeContextCloseResponse
{
    public int StructSize;
    public int AbiVersion;
    public int SchemaVersion;
    public int ContextAbiVersion;
    public int Status;
    public int ErrorCode;
    public long ContextId;
    public long DurationMs;
    public int Flags;
    public int Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeObjectListRequest
{
    public int StructSize;
    public long ContextId;
    public int Offset;
    public int Limit;
    public IntPtr AssetTypesCsvUtf8;
    public int AssetTypesCsvUtf8Len;
    public int Flags;
    public int Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeObjectTable
{
    public int StructSize;
    public int AbiVersion;
    public int SchemaVersion;
    public int ObjectTableAbiVersion;
    public int Status;
    public int ErrorCode;
    public long ContextId;
    public int Offset;
    public int Limit;
    public int NextOffset;
    public int HasMore;
    public int TotalCount;
    public int ReturnedCount;
    public IntPtr Objects;
    public IntPtr StringData;
    public int StringDataLen;
    public IntPtr Buffer;
    public long BufferLen;
    public long DurationMs;
    public int Flags;
    public int Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeAssetObject
{
    public int Index;
    public int TypeId;
    public long PathId;
    public long Size;
    public long EstimatedPayloadCapacity;
    public long RawPayloadCapacity;
    public long ImagePayloadCapacity;
    public long TextPayloadCapacity;
    public int PayloadCapacityFlags;
    public int Reserved;
    public int NameOffset;
    public int NameLen;
    public int ContainerOffset;
    public int ContainerLen;
    public int TypeOffset;
    public int TypeLen;
    public int UniqueIdOffset;
    public int UniqueIdLen;
    public int SourceFileOffset;
    public int SourceFileLen;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeObjectReadItemRequest
{
    public long PathId;
    public IntPtr KindUtf8;
    public int KindUtf8Len;
    public IntPtr ImageFormatUtf8;
    public int ImageFormatUtf8Len;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeObjectReadBatchIntoRequestV1
{
    public int StructSize;
    public long ContextId;
    public IntPtr Items;
    public int Count;
    public int Flags;
    public IntPtr ItemsBuffer;
    public long ItemsBufferLen;
    public IntPtr Payload;
    public long PayloadLen;
    public int Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeObjectReadBatchRetryResponseV1
{
    public int StructSize;
    public int AbiVersion;
    public int SchemaVersion;
    public int ObjectReadBatchAbiVersion;
    public int Status;
    public int ErrorCode;
    public long ContextId;
    public int RequestedCount;
    public int ReturnedCount;
    public int FailedCount;
    public IntPtr Items;
    public IntPtr StringData;
    public int StringDataLen;
    public IntPtr ItemsBuffer;
    public long ItemsBufferLen;
    public IntPtr Payload;
    public long PayloadLen;
    public long RequiredItemsBufferLen;
    public int RequiredStringDataLen;
    public long RequiredPayloadLen;
    public long DurationMs;
    public long ResultHandle;
    public int OwnershipFlags;
    public int Flags;
    public int Reserved;
}

internal sealed class BenchOptions
{
    public string InputPath { get; set; } = "";
    public string NativeLibraryPath { get; private init; } = "";
    public string? CliPath { get; private init; }
    public string? UnityVersion { get; private init; }
    public int Iterations { get; private init; } = 5;
    public int WarmupIterations { get; private init; } = 1;
    public int ReadCount { get; private init; }
    public string Kind { get; private init; } = "auto";
    public string ImageFormat { get; private init; } = "raw_rgba";
    public List<string> AssetTypes { get; private init; } = new();
    public bool DeobfuscateInput { get; private init; }

    public static BenchOptions? Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!key.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                values[key] = "true";
                continue;
            }

            values[key] = args[++i];
        }

        if (!values.TryGetValue("--input", out var inputPath) || string.IsNullOrWhiteSpace(inputPath))
        {
            return null;
        }

        var nativeLibraryPath = values.TryGetValue("--native-lib", out var nativeLib)
            ? nativeLib
            : DefaultNativeLibraryPath();
        if (!File.Exists(nativeLibraryPath))
        {
            throw new FileNotFoundException($"native library was not found: {nativeLibraryPath}", nativeLibraryPath);
        }

        return new BenchOptions
        {
            InputPath = inputPath,
            NativeLibraryPath = nativeLibraryPath,
            CliPath = values.TryGetValue("--cli", out var cli) ? cli : null,
            UnityVersion = values.TryGetValue("--unity-version", out var unityVersion) ? unityVersion : null,
            Iterations = ReadInt(values, "--iterations", 5),
            WarmupIterations = ReadInt(values, "--warmup", 1),
            ReadCount = ReadInt(values, "--read-count", 0),
            Kind = values.TryGetValue("--kind", out var kind) ? kind : "auto",
            ImageFormat = values.TryGetValue("--image-format", out var imageFormat) ? imageFormat : "raw_rgba",
            AssetTypes = values.TryGetValue("--asset-types", out var assetTypes)
                ? assetTypes.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : new List<string>(),
            DeobfuscateInput = values.ContainsKey("--deobfuscate-input"),
        };
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project AssetStudioFfiBench -- --input <asset-file-or-folder> [--native-lib <dylib/so/dll>] [--cli <cli-dll-or-exe>] [--unity-version 2022.3.62f1] [--deobfuscate-input] [--iterations 5] [--warmup 1] [--read-count 20]");
    }

    private static int ReadInt(IReadOnlyDictionary<string, string> values, string key, int fallback)
    {
        return values.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Max(0, parsed)
            : fallback;
    }

    private static string DefaultNativeLibraryPath()
    {
        var extension = OperatingSystem.IsWindows() ? ".dll" : OperatingSystem.IsMacOS() ? ".dylib" : ".so";
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "AssetStudioFFI",
            "bin",
            "Release",
            "net9.0",
            RuntimeInformation.RuntimeIdentifier,
            "publish",
            "HarukiAssetStudioFFI" + extension));
    }
}

internal sealed class PreparedInput : IDisposable
{
    private readonly string? tempPath;

    private PreparedInput(string path, string mode, string? tempPath)
    {
        Path = path;
        Mode = mode;
        this.tempPath = tempPath;
    }

    public string Path { get; }

    public string Mode { get; }

    public static PreparedInput Create(string inputPath, bool deobfuscate)
    {
        if (!deobfuscate)
        {
            return new PreparedInput(inputPath, "none", null);
        }

        var data = File.ReadAllBytes(inputPath);
        var (mode, output) = Deobfuscate(data);
        var tempPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "assetstudio-ffi-bench-input-" + Guid.NewGuid().ToString("N") + System.IO.Path.GetExtension(inputPath));
        File.WriteAllBytes(tempPath, output);
        return new PreparedInput(tempPath, mode, tempPath);
    }

    public void Dispose()
    {
        if (tempPath == null)
        {
            return;
        }

        try
        {
            File.Delete(tempPath);
        }
        catch
        {
            // Best-effort cleanup for benchmark temp input.
        }
    }

    private static (string Mode, byte[] Output) Deobfuscate(byte[] data)
    {
        var simple = new byte[] { 0x20, 0x00, 0x00, 0x00 };
        var xorHeader = new byte[] { 0x10, 0x00, 0x00, 0x00 };
        if (data.AsSpan().StartsWith(simple))
        {
            return ("simple-header", data[4..]);
        }

        if (!data.AsSpan().StartsWith(xorHeader))
        {
            return ("unchanged", data);
        }

        var body = data[4..];
        if (body.Length < 128)
        {
            return ("xor-header-short", body);
        }

        var output = new byte[body.Length];
        var pattern = new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0x00, 0x00, 0x00 };
        for (var i = 0; i < 128; i++)
        {
            output[i] = (byte)(body[i] ^ pattern[i % pattern.Length]);
        }
        Buffer.BlockCopy(body, 128, output, 128, body.Length - 128);
        return ("xor-header", output);
    }
}
