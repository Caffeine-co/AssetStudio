using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

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
    var openRequest = new Dictionary<string, object?>
    {
        ["input_path"] = options.InputPath,
        ["include_assets"] = true,
        ["output_dir"] = Path.Combine(Path.GetTempPath(), "assetstudio-ffi-bench-" + Guid.NewGuid().ToString("N")),
    };
    if (options.AssetTypes.Count > 0)
    {
        openRequest["asset_types"] = options.AssetTypes;
    }
    if (!string.IsNullOrWhiteSpace(options.UnityVersion))
    {
        openRequest["unity_version"] = options.UnityVersion;
    }

    var open = ffi.CallJson("context_open", ffi.ContextOpen, openRequest);
    using var openDoc = JsonDocument.Parse(open.ResponseJson);
    EnsureSuccess(openDoc.RootElement);
    var contextId = openDoc.RootElement.GetProperty("context_id").GetInt64();
    var assets = ReadAssets(openDoc.RootElement);

    Sample? list = null;
    Sample? readBatch = null;
    Sample closeSample;
    try
    {
        list = ffi.CallJson("context_list_objects", ffi.ContextListObjects, new Dictionary<string, object?>
        {
            ["context_id"] = contextId,
            ["offset"] = 0,
            ["limit"] = options.ReadCount > 0 ? options.ReadCount : Math.Min(assets.Count, 100),
        }).Sample;

        if (options.ReadCount > 0 && assets.Count > 0)
        {
            var selected = assets.Take(options.ReadCount)
                .Select(asset => new Dictionary<string, object?>
                {
                    ["path_id"] = asset.PathId,
                    ["kind"] = options.Kind,
                    ["image_format"] = options.ImageFormat,
                })
                .ToArray();

            var batchRequest = new Dictionary<string, object?>
            {
                ["context_id"] = contextId,
                ["objects"] = selected,
            };
            var batch = ffi.CallReadObjects(batchRequest);
            using var batchDoc = JsonDocument.Parse(batch.ResponseJson);
            EnsureSuccess(batchDoc.RootElement);
            readBatch = batch.Sample with { PayloadBytes = batch.PayloadLength };
        }
    }
    finally
    {
        var close = ffi.CallJson("context_close", ffi.ContextClose, new Dictionary<string, object?>
        {
            ["context_id"] = contextId,
        });

        open.Sample.AssetCount = assets.Count;
        open.Sample.ObjectIndexCount = TryGetInt(openDoc.RootElement, "object_index_count");
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

static List<AssetRef> ReadAssets(JsonElement root)
{
    var assets = new List<AssetRef>();
    if (!root.TryGetProperty("assets", out var assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
    {
        return assets;
    }

    foreach (var asset in assetsElement.EnumerateArray())
    {
        if (asset.TryGetProperty("path_id", out var pathId))
        {
            assets.Add(new AssetRef(pathId.GetInt64()));
        }
    }

    return assets;
}

static void EnsureSuccess(JsonElement root)
{
    if (root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True)
    {
        return;
    }

    var error = root.TryGetProperty("error", out var errorElement)
        ? errorElement.GetString()
        : root.GetRawText();
    throw new InvalidOperationException(error);
}

static int TryGetInt(JsonElement root, string propertyName)
{
    return root.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result)
        ? result
        : 0;
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
    private readonly FreeDelegate freeString;
    private readonly FreeDelegate freeBuffer;

    public NativeAssetStudio(string libraryPath)
    {
        var handle = NativeLibrary.Load(libraryPath);
        ContextOpen = Load<NativeJsonDelegate>(handle, "haruki_assetstudio_context_open");
        ContextListObjects = Load<NativeJsonDelegate>(handle, "haruki_assetstudio_context_list_objects");
        ContextClose = Load<NativeJsonDelegate>(handle, "haruki_assetstudio_context_close");
        ContextReadObjects = Load<NativeReadObjectsDelegate>(handle, "haruki_assetstudio_context_read_objects");
        freeString = Load<FreeDelegate>(handle, "haruki_assetstudio_free_string");
        freeBuffer = Load<FreeDelegate>(handle, "haruki_assetstudio_free_buffer");
    }

    public NativeJsonDelegate ContextOpen { get; }

    public NativeJsonDelegate ContextListObjects { get; }

    public NativeJsonDelegate ContextClose { get; }

    public NativeReadObjectsDelegate ContextReadObjects { get; }

    public NativeJsonResult CallJson(string name, NativeJsonDelegate callback, object request)
    {
        var requestPtr = StringToNativeUtf8(JsonSerializer.Serialize(request));
        IntPtr responsePtr = IntPtr.Zero;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var code = callback(requestPtr, out responsePtr);
            stopwatch.Stop();
            var responseJson = PtrToString(responsePtr);
            if (code != 0)
            {
                throw new InvalidOperationException($"{name} returned {code}: {responseJson}");
            }
            return new NativeJsonResult(responseJson, new Sample(name, stopwatch.Elapsed.TotalMilliseconds));
        }
        finally
        {
            Marshal.FreeCoTaskMem(requestPtr);
            if (responsePtr != IntPtr.Zero)
            {
                freeString(responsePtr);
            }
        }
    }

    public NativeReadResult CallReadObjects(object request)
    {
        var requestPtr = StringToNativeUtf8(JsonSerializer.Serialize(request));
        IntPtr responsePtr = IntPtr.Zero;
        IntPtr payloadPtr = IntPtr.Zero;
        long payloadLength = 0;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var code = ContextReadObjects(requestPtr, out responsePtr, out payloadPtr, out payloadLength);
            stopwatch.Stop();
            var responseJson = PtrToString(responsePtr);
            if (code != 0)
            {
                throw new InvalidOperationException($"context_read_objects returned {code}: {responseJson}");
            }
            return new NativeReadResult(responseJson, new Sample("context_read_objects", stopwatch.Elapsed.TotalMilliseconds), payloadLength);
        }
        finally
        {
            Marshal.FreeCoTaskMem(requestPtr);
            if (responsePtr != IntPtr.Zero)
            {
                freeString(responsePtr);
            }
            if (payloadPtr != IntPtr.Zero)
            {
                freeBuffer(payloadPtr);
            }
        }
    }

    private static T Load<T>(IntPtr handle, string name)
        where T : Delegate
    {
        return Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(handle, name));
    }

    private static IntPtr StringToNativeUtf8(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var pointer = Marshal.AllocCoTaskMem(bytes.Length + 1);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        Marshal.WriteByte(pointer, bytes.Length, 0);
        return pointer;
    }

    private static string PtrToString(IntPtr pointer)
    {
        return pointer == IntPtr.Zero
            ? string.Empty
            : Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NativeJsonDelegate(IntPtr requestJson, out IntPtr responseJson);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NativeReadObjectsDelegate(IntPtr requestJson, out IntPtr responseJson, out IntPtr payloadPtr, out long payloadLen);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeDelegate(IntPtr value);
}

internal sealed record NativeJsonResult(string ResponseJson, Sample Sample);

internal sealed record NativeReadResult(string ResponseJson, Sample Sample, long PayloadLength);

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
    public string ImageFormat { get; private init; } = "bmp";
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
            ImageFormat = values.TryGetValue("--image-format", out var imageFormat) ? imageFormat : "bmp",
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
            "AssetStudioNative",
            "bin",
            "Release",
            "net9.0",
            RuntimeInformation.RuntimeIdentifier,
            "publish",
            "HarukiAssetStudioNative" + extension));
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
