using AssetStudioCLI;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Texture2DDecoder;

namespace AssetStudioNative;

public static unsafe class NativeExports
{
    private static readonly SemaphoreSlim OperationGate = new(1, 1);
    private static readonly NativeDiagnostics Diagnostics = NativeDiagnostics.CreateFromEnvironment();
    private static readonly string WorkerId = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
    private const uint PayloadBundleMagic = 0x42504148; // HAPB
    private const ushort PayloadBundleVersion = 2;
    private const ushort PayloadBundleHeaderLength = 20;
    private static long NextContextId;
    private static long NextReadObjectsCallSeq;
    private static ActiveNativeContext? ActiveContext;

    static NativeExports()
    {
        SixLabors.ImageSharp.Configuration.Default.MaxDegreeOfParallelism = 1;
        NativeLibrary.SetDllImportResolver(typeof(TextureDecoder).Assembly, ResolveAssetStudioNativeLibrary);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Diagnostics.Event("process", "unhandled_exception", args.ExceptionObject?.ToString());
        TaskScheduler.UnobservedTaskException += (_, args) =>
            Diagnostics.Event("process", "unobserved_task_exception", args.Exception.ToString());
        Diagnostics.Event(
            "process",
            "native_exports_initialized",
            $"image_guard={AssetStudio.ImageSharpNativeAotGuard.Enabled}");
    }

    [UnmanagedCallersOnly(EntryPoint = "haruki_assetstudio_version", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int Version(byte** responseJson)
    {
        if (responseJson == null)
        {
            return 1;
        }

        try
        {
            var response = new VersionResponse
            {
                Success = true,
                AdapterVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                AssetStudioCliVersion = typeof(AssetStudioCliRunner).Assembly.GetName().Version?.ToString(),
            };
            *responseJson = AllocateJson(response);
            return 0;
        }
        catch (Exception ex)
        {
            *responseJson = AllocateJson(VersionResponse.Fail(ex.ToString()));
            return 100;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "haruki_assetstudio_inspect", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int Inspect(byte* requestJson, byte** responseJson)
    {
        if (responseJson == null)
        {
            return 1;
        }
        *responseJson = null;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (requestJson == null)
            {
                *responseJson = AllocateJson(InspectResponse.Fail("request_json is null", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            var json = Marshal.PtrToStringUTF8((IntPtr)requestJson);
            if (string.IsNullOrWhiteSpace(json))
            {
                *responseJson = AllocateJson(InspectResponse.Fail("request_json is empty", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            var request = JsonSerializer.Deserialize(json, NativeJsonContext.Default.InspectRequest);
            if (request == null)
            {
                *responseJson = AllocateJson(InspectResponse.Fail("request_json could not be parsed", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            var operationId = Diagnostics.Begin("inspect", request.InputPath);
            Diagnostics.Event(operationId, "waiting_for_operation_gate");
            AssetStudioInspectResult result;
            OperationGate.Wait();
            try
            {
                Diagnostics.Event(operationId, "acquired_operation_gate");
                using var console = ConsoleCapture.Start(Diagnostics.CaptureConsole);
                result = AssetStudioCliRunner.Inspect(request.ToInspectOptions());
                Diagnostics.Console(operationId, console.StandardOutput, console.StandardError);
            }
            catch (Exception ex)
            {
                Diagnostics.Exception(operationId, ex);
                throw;
            }
            finally
            {
                ResetProcessLocalState();
                OperationGate.Release();
                Diagnostics.Event(operationId, "released_operation_gate");
            }

            *responseJson = AllocateJson(new InspectResponse
            {
                Success = true,
                AssetsFileCount = result.AssetsFileCount,
                ExportableAssetCount = result.ExportableAssetCount,
                UnityVersion = result.UnityVersion,
                Assets = result.Assets,
                Warnings = Diagnostics.ResponseWarnings(operationId),
                PhaseMs = result.PhaseMs,
                DurationMs = stopwatch.ElapsedMilliseconds,
            });
            Diagnostics.End(operationId, "inspect", stopwatch.ElapsedMilliseconds, $"assets={result.Assets.Count}");
            return 0;
        }
        catch (Exception ex)
        {
            Diagnostics.Exception("inspect", ex);
            *responseJson = AllocateJson(InspectResponse.Fail(ex.ToString(), stopwatch.ElapsedMilliseconds));
            return 100;
        }
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.Texture2D))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.Texture2DArray))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.GLTextureSettings))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.StreamingInfo))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.ResourceReader))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicFields, typeof(AssetStudio.TextureFormat))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicFields, typeof(AssetStudio.GraphicsFormat))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicFields, typeof(AssetStudio.ClassIDType))]
    [UnmanagedCallersOnly(EntryPoint = "haruki_assetstudio_context_open", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int ContextOpen(byte* requestJson, byte** responseJson)
    {
        if (responseJson == null)
        {
            return 1;
        }
        *responseJson = null;

        var stopwatch = Stopwatch.StartNew();
        var gateAcquired = false;
        try
        {
            if (requestJson == null)
            {
                *responseJson = AllocateJson(ContextOpenResponse.Fail("request_json is null", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            var json = Marshal.PtrToStringUTF8((IntPtr)requestJson);
            if (string.IsNullOrWhiteSpace(json))
            {
                *responseJson = AllocateJson(ContextOpenResponse.Fail("request_json is empty", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            var request = JsonSerializer.Deserialize(json, NativeJsonContext.Default.InspectRequest);
            if (request == null)
            {
                *responseJson = AllocateJson(ContextOpenResponse.Fail("request_json could not be parsed", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            var operationId = Diagnostics.Begin("context_open", request.InputPath);
            Diagnostics.Event(operationId, "waiting_for_operation_gate");
            OperationGate.Wait();
            gateAcquired = true;

            if (ActiveContext != null)
            {
                throw new InvalidOperationException($"native context {ActiveContext.ContextId} is already active");
            }

            AssetStudioLoadedSession session;
            try
            {
                Diagnostics.Event(operationId, "acquired_operation_gate");
                using var console = ConsoleCapture.Start(Diagnostics.CaptureConsole);
                session = AssetStudioCliRunner.BeginSession(request.ToInspectOptions());
                Diagnostics.Console(operationId, console.StandardOutput, console.StandardError);
            }
            catch (Exception ex)
            {
                Diagnostics.Exception(operationId, ex);
                throw;
            }

            var contextId = Interlocked.Increment(ref NextContextId);
            var result = session.InspectResult;
            var objectIndexCount = AssetStudioCliRunner.ActiveObjectIndexCount;
            var responseAssets = FilterAssetsForResponse(result.Assets, request.AssetTypes);
            ActiveContext = new ActiveNativeContext(contextId, operationId, request.InputPath, stopwatch, responseAssets);

            *responseJson = AllocateJson(new ContextOpenResponse
            {
                Success = true,
                ContextId = contextId,
                AssetsFileCount = result.AssetsFileCount,
                ExportableAssetCount = responseAssets.Count,
                UnityVersion = result.UnityVersion,
                Assets = request.IncludeAssets ? responseAssets : Array.Empty<AssetStudioAssetInfo>(),
                Warnings = Diagnostics.ResponseWarnings(operationId),
                PhaseMs = result.PhaseMs,
                WorkerId = WorkerId,
                ObjectIndexCount = objectIndexCount,
                ReturnedAssetCount = request.IncludeAssets ? responseAssets.Count : 0,
                HasMoreAssets = !request.IncludeAssets && responseAssets.Count > 0,
                DurationMs = stopwatch.ElapsedMilliseconds,
            });
            Diagnostics.End(operationId, "context_open", stopwatch.ElapsedMilliseconds, $"assets={responseAssets.Count}/{result.Assets.Count} object_index={objectIndexCount}");
            return 0;
        }
        catch (Exception ex)
        {
            Diagnostics.Exception("context_open", ex);
            if (gateAcquired)
            {
                ActiveContext = null;
                ResetProcessLocalState();
                OperationGate.Release();
            }
            *responseJson = AllocateJson(ContextOpenResponse.Fail(ex.ToString(), stopwatch.ElapsedMilliseconds));
            return 100;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "haruki_assetstudio_context_close", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int ContextClose(byte* requestJson, byte** responseJson)
    {
        if (responseJson == null)
        {
            return 1;
        }
        *responseJson = null;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (requestJson == null)
            {
                *responseJson = AllocateJson(ContextCloseResponse.Fail("request_json is null", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            var json = Marshal.PtrToStringUTF8((IntPtr)requestJson);
            if (string.IsNullOrWhiteSpace(json))
            {
                *responseJson = AllocateJson(ContextCloseResponse.Fail("request_json is empty", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            var request = JsonSerializer.Deserialize(json, NativeJsonContext.Default.ContextCloseRequest);
            if (request == null)
            {
                *responseJson = AllocateJson(ContextCloseResponse.Fail("request_json could not be parsed", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            if (ActiveContext == null || ActiveContext.ContextId != request.ContextId)
            {
                *responseJson = AllocateJson(ContextCloseResponse.Fail($"native context {request.ContextId} is not active", stopwatch.ElapsedMilliseconds));
                return 4;
            }

            var operationId = ActiveContext.OperationId;
            try
            {
                AssetStudioCliRunner.EndSession();
            }
            finally
            {
                ActiveContext = null;
                OperationGate.Release();
            }
            Diagnostics.Event(operationId, "context_closed", $"duration_ms={stopwatch.ElapsedMilliseconds}");
            *responseJson = AllocateJson(new ContextCloseResponse
            {
                Success = true,
                Warnings = Diagnostics.ResponseWarnings(operationId),
                DurationMs = stopwatch.ElapsedMilliseconds,
            });
            return 0;
        }
        catch (Exception ex)
        {
            Diagnostics.Exception("context_close", ex);
            *responseJson = AllocateJson(ContextCloseResponse.Fail(ex.ToString(), stopwatch.ElapsedMilliseconds));
            return 100;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "haruki_assetstudio_context_list_objects", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int ContextListObjects(byte* requestJson, byte** responseJson)
    {
        if (responseJson == null)
        {
            return 1;
        }
        *responseJson = null;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (requestJson == null)
            {
                *responseJson = AllocateJson(ContextListObjectsResponse.Fail("request_json is null", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            var json = Marshal.PtrToStringUTF8((IntPtr)requestJson);
            if (string.IsNullOrWhiteSpace(json))
            {
                *responseJson = AllocateJson(ContextListObjectsResponse.Fail("request_json is empty", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            var request = JsonSerializer.Deserialize(json, NativeJsonContext.Default.ContextListObjectsRequest);
            if (request == null)
            {
                *responseJson = AllocateJson(ContextListObjectsResponse.Fail("request_json could not be parsed", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            if (ActiveContext == null || ActiveContext.ContextId != request.ContextId)
            {
                *responseJson = AllocateJson(ContextListObjectsResponse.Fail($"native context {request.ContextId} is not active", stopwatch.ElapsedMilliseconds));
                return 4;
            }

            var offset = Math.Max(0, request.Offset);
            var limit = request.Limit <= 0 ? ActiveContext.Assets.Count : request.Limit;
            var page = ActiveContext.Assets.Skip(offset).Take(limit).ToArray();
            var nextOffset = offset + page.Length;
            var hasMore = nextOffset < ActiveContext.Assets.Count;
            Diagnostics.Event(
                ActiveContext.OperationId,
                "context_list_objects",
                $"offset={offset} limit={limit} returned={page.Length}/{ActiveContext.Assets.Count}");

            *responseJson = AllocateJson(new ContextListObjectsResponse
            {
                Success = true,
                ContextId = request.ContextId,
                Offset = offset,
                Limit = limit,
                NextOffset = hasMore ? nextOffset : null,
                TotalCount = ActiveContext.Assets.Count,
                Assets = page,
                Warnings = Diagnostics.ResponseWarnings(ActiveContext.OperationId),
                DurationMs = stopwatch.ElapsedMilliseconds,
            });
            return 0;
        }
        catch (Exception ex)
        {
            Diagnostics.Exception("context_list_objects", ex);
            *responseJson = AllocateJson(ContextListObjectsResponse.Fail(ex.ToString(), stopwatch.ElapsedMilliseconds));
            return 100;
        }
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.Texture2D))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.Texture2DArray))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.Sprite))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.TextAsset))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.MonoBehaviour))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.AudioClip))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.VideoClip))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.MovieTexture))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.Font))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.Shader))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.Mesh))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.Animator))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(AssetStudio.AnimationClip))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicFields, typeof(AssetStudio.FMODSoundType))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicFields, typeof(AssetStudio.AudioCompressionFormat))]
    [UnmanagedCallersOnly(EntryPoint = "haruki_assetstudio_context_read_object", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int ContextReadObject(byte* requestJson, byte** responseJson, byte** payloadPtr, long* payloadLen)
    {
        if (responseJson == null || payloadPtr == null || payloadLen == null)
        {
            return 1;
        }
        *responseJson = null;
        *payloadPtr = null;
        *payloadLen = 0;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (requestJson == null)
            {
                *responseJson = AllocateJson(ObjectReadResponse.Fail("request_json is null", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            var json = Marshal.PtrToStringUTF8((IntPtr)requestJson);
            if (string.IsNullOrWhiteSpace(json))
            {
                *responseJson = AllocateJson(ObjectReadResponse.Fail("request_json is empty", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            var request = JsonSerializer.Deserialize(json, NativeJsonContext.Default.ContextReadObjectRequest);
            if (request == null)
            {
                *responseJson = AllocateJson(ObjectReadResponse.Fail("request_json could not be parsed", stopwatch.ElapsedMilliseconds));
                return 2;
            }
            if (ActiveContext == null || ActiveContext.ContextId != request.ContextId)
            {
                *responseJson = AllocateJson(ObjectReadResponse.Fail($"native context {request.ContextId} is not active", stopwatch.ElapsedMilliseconds));
                return 4;
            }

            var operationId = ActiveContext.OperationId;
            AssetStudioObjectReadResult result;
            try
            {
                Diagnostics.Event(operationId, "context_read_object", $"path_id={request.PathId} kind={request.Kind}");
                using var console = ConsoleCapture.Start(Diagnostics.CaptureConsole);
                result = AssetStudioCliRunner.ReadObject(request.ToReadOptions());
                Diagnostics.Console(operationId, console.StandardOutput, console.StandardError);
            }
            catch (Exception ex)
            {
                Diagnostics.Exception(operationId, ex);
                *responseJson = AllocateJson(ObjectReadResponse.Fail(ex.ToString(), stopwatch.ElapsedMilliseconds));
                return 100;
            }

            if (result.Payload.Length > 0)
            {
                var buffer = (byte*)NativeMemory.Alloc((nuint)result.Payload.Length);
                Marshal.Copy(result.Payload, 0, (IntPtr)buffer, result.Payload.Length);
                *payloadPtr = buffer;
                *payloadLen = result.Payload.Length;
            }

            *responseJson = AllocateJson(new ObjectReadResponse
            {
                Success = true,
                Asset = result.Asset,
                PayloadKind = result.PayloadKind,
                PayloadLen = result.Payload.Length,
                SuggestedExtension = result.SuggestedExtension,
                Warnings = Diagnostics.ResponseWarnings(operationId),
                PhaseMs = result.PhaseMs,
                DurationMs = stopwatch.ElapsedMilliseconds,
            });
            return 0;
        }
        catch (Exception ex)
        {
            Diagnostics.Exception("context_read_object", ex);
            *responseJson = AllocateJson(ObjectReadResponse.Fail(ex.ToString(), stopwatch.ElapsedMilliseconds));
            return 100;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "haruki_assetstudio_context_read_objects", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int ContextReadObjects(byte* requestJson, byte** responseJson, byte** payloadPtr, long* payloadLen)
    {
        if (responseJson == null || payloadPtr == null || payloadLen == null)
        {
            return 1;
        }
        *responseJson = null;
        *payloadPtr = null;
        *payloadLen = 0;

        var stopwatch = Stopwatch.StartNew();
        var phaseMs = new Dictionary<string, long>(StringComparer.Ordinal);
        long callSeq = 0;
        try
        {
            if (requestJson == null)
            {
                *responseJson = AllocateJson(ObjectReadBatchResponse.Fail("request_json is null", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            var json = Marshal.PtrToStringUTF8((IntPtr)requestJson);
            if (string.IsNullOrWhiteSpace(json))
            {
                *responseJson = AllocateJson(ObjectReadBatchResponse.Fail("request_json is empty", stopwatch.ElapsedMilliseconds));
                return 2;
            }

            var phaseStopwatch = Stopwatch.StartNew();
            var request = JsonSerializer.Deserialize(json, NativeJsonContext.Default.ContextReadObjectsRequest);
            RecordElapsed(phaseMs, "decode_request", phaseStopwatch);
            if (request == null)
            {
                *responseJson = AllocateJson(ObjectReadBatchResponse.Fail("request_json could not be parsed", stopwatch.ElapsedMilliseconds));
                return 2;
            }
            if (ActiveContext == null || ActiveContext.ContextId != request.ContextId)
            {
                *responseJson = AllocateJson(ObjectReadBatchResponse.Fail($"native context {request.ContextId} is not active", stopwatch.ElapsedMilliseconds));
                return 4;
            }

            var operationId = ActiveContext.OperationId;
            callSeq = Interlocked.Increment(ref NextReadObjectsCallSeq);
            var reads = new List<ObjectReadResponse>(request.Objects.Count);
            var payloadEntries = new List<(string Name, byte[] Payload)>();
            var phaseSamples = new Dictionary<string, List<long>>(StringComparer.Ordinal);
            long readPayloadMs = 0;
            Diagnostics.Event(operationId, "context_read_objects", $"count={request.Objects.Count}");
            phaseStopwatch.Restart();
            using (var console = ConsoleCapture.Start(Diagnostics.CaptureConsole))
            {
                foreach (var item in request.Objects)
                {
                    try
                    {
                        var result = AssetStudioCliRunner.ReadObject(item.ToReadOptions());
                        if (result.PhaseMs.TryGetValue("read_payload", out var itemReadPayloadMs))
                        {
                            readPayloadMs += itemReadPayloadMs;
                        }
                        RecordPhaseSamples(phaseSamples, result.PhaseMs);
                        if (result.Payload.Length > 0)
                        {
                            payloadEntries.Add((item.PathId.ToString(CultureInfo.InvariantCulture), result.Payload));
                        }
                        reads.Add(new ObjectReadResponse
                        {
                            Success = true,
                            Asset = result.Asset,
                            PayloadKind = result.PayloadKind,
                            PayloadLen = result.Payload.Length,
                            SuggestedExtension = result.SuggestedExtension,
                            Warnings = Array.Empty<string>(),
                            PhaseMs = result.PhaseMs,
                            DurationMs = 0,
                        });
                    }
                    catch (Exception ex)
                    {
                        Diagnostics.Exception(operationId, ex);
                        reads.Add(ObjectReadResponse.Fail(ex.ToString()));
                    }
                }
                Diagnostics.Console(operationId, console.StandardOutput, console.StandardError);
            }
            RecordElapsed(phaseMs, "read_objects", phaseStopwatch);

            phaseStopwatch.Restart();
            var payload = WritePayloadBundleToNative(payloadEntries);
            RecordElapsed(phaseMs, "write_payload_bundle", phaseStopwatch);
            if (payload.Pointer != null && payload.Length > 0)
            {
                *payloadPtr = payload.Pointer;
                *payloadLen = payload.Length;
                phaseMs["marshal_payload"] = 0;
            }

            *responseJson = AllocateJson(new ObjectReadBatchResponse
            {
                Success = true,
                Reads = reads,
                Warnings = Diagnostics.ResponseWarnings(operationId),
                PhaseMs = phaseMs,
                AssetTypeCounts = CountBy(reads, read => read.Asset?.Type),
                PayloadKindCounts = CountBy(reads, read => read.PayloadKind),
                PayloadBytesByKind = SumPayloadBytesByKind(reads),
                PayloadLen = payload.Length,
                ObjectCount = request.Objects.Count,
                PayloadBundleVersion = payload.Version,
                PayloadBundleEntryCount = payload.EntryCount,
                PayloadBundleBytes = payload.Length,
                PayloadDataBytes = payload.DataBytes,
                FailedCount = reads.Count(read => !read.Success),
                ReadPayloadMs = readPayloadMs,
                WorkerId = WorkerId,
                CallSeq = callSeq,
                ObjectIndexCount = AssetStudioCliRunner.ActiveObjectIndexCount,
                PhaseStats = BuildPhaseStats(phaseSamples),
                DurationMs = stopwatch.ElapsedMilliseconds,
            });
            return 0;
        }
        catch (Exception ex)
        {
            Diagnostics.Exception("context_read_objects", ex);
            *responseJson = AllocateJson(ObjectReadBatchResponse.Fail(
                ex.ToString(),
                stopwatch.ElapsedMilliseconds,
                WorkerId,
                callSeq,
                AssetStudioCliRunner.ActiveObjectIndexCount,
                phaseMs));
            return 100;
        }
    }

    private static void RecordElapsed(Dictionary<string, long> phaseMs, string phase, Stopwatch stopwatch)
    {
        phaseMs[phase] = stopwatch.ElapsedMilliseconds;
    }

    private static Dictionary<string, int> CountBy(
        IEnumerable<ObjectReadResponse> reads,
        Func<ObjectReadResponse, string?> keySelector)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var read in reads)
        {
            if (!read.Success)
            {
                continue;
            }

            var key = keySelector(read);
            if (string.IsNullOrWhiteSpace(key))
            {
                key = "unknown";
            }

            result[key] = result.TryGetValue(key, out var count) ? count + 1 : 1;
        }
        return result;
    }

    private static Dictionary<string, long> SumPayloadBytesByKind(IEnumerable<ObjectReadResponse> reads)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var read in reads)
        {
            if (!read.Success)
            {
                continue;
            }

            var key = string.IsNullOrWhiteSpace(read.PayloadKind) ? "unknown" : read.PayloadKind;
            result[key] = result.TryGetValue(key, out var bytes) ? bytes + read.PayloadLen : read.PayloadLen;
        }
        return result;
    }

    private static IReadOnlyCollection<AssetStudioAssetInfo> FilterAssetsForResponse(
        IReadOnlyCollection<AssetStudioAssetInfo> assets,
        IReadOnlyCollection<string>? requestedTypes)
    {
        if (requestedTypes == null || requestedTypes.Count == 0)
        {
            return assets;
        }

        var normalizedTypes = requestedTypes
            .Select(NormalizeAssetTypeName)
            .Where(type => type.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        if (normalizedTypes.Count == 0 || normalizedTypes.Contains("all") || normalizedTypes.Contains("*"))
        {
            return assets;
        }

        return assets
            .Where(asset => asset.Type != null && RequestedTypesMatchAsset(normalizedTypes, asset.Type))
            .ToArray();
    }

    private static bool RequestedTypesMatchAsset(IReadOnlySet<string> normalizedTypes, string assetType)
    {
        var normalizedAssetType = NormalizeAssetTypeName(assetType);
        if (normalizedTypes.Contains(normalizedAssetType))
        {
            return normalizedAssetType != "texture2darray";
        }

        return normalizedAssetType switch
        {
            "texture2darrayimage" => normalizedTypes.Contains("texture2darray"),
            _ => false,
        };
    }

    private static string NormalizeAssetTypeName(string type)
    {
        return type.Trim().Replace("_", "", StringComparison.Ordinal).ToLowerInvariant() switch
        {
            "tex2d" => "texture2d",
            "tex2darray" => "texture2darray",
            "texture2darrayimage" => "texture2darrayimage",
            "monobehavior" => "monobehaviour",
            "monobehaviour" => "monobehaviour",
            "textasset" => "textasset",
            "audio" => "audioclip",
            "audioclip" => "audioclip",
            "video" => "videoclip",
            "videoclip" => "videoclip",
            "movietexture" => "movietexture",
            "sprite" => "sprite",
            "font" => "font",
            "shader" => "shader",
            "mesh" => "mesh",
            "animator" => "animator",
            var normalized => normalized,
        };
    }

    private static void RecordPhaseSamples(Dictionary<string, List<long>> phaseSamples, IReadOnlyDictionary<string, long> phaseMs)
    {
        foreach (var (phase, elapsedMs) in phaseMs)
        {
            if (!phaseSamples.TryGetValue(phase, out var samples))
            {
                samples = new List<long>();
                phaseSamples[phase] = samples;
            }
            samples.Add(elapsedMs);
        }
    }

    private static Dictionary<string, NativePhaseStats> BuildPhaseStats(Dictionary<string, List<long>> phaseSamples)
    {
        var result = new Dictionary<string, NativePhaseStats>(StringComparer.Ordinal);
        foreach (var (phase, samples) in phaseSamples)
        {
            if (samples.Count == 0)
            {
                continue;
            }
            samples.Sort();
            result[phase] = new NativePhaseStats
            {
                P50Ms = PercentileNearestRank(samples, 50),
                P95Ms = PercentileNearestRank(samples, 95),
            };
        }
        return result;
    }

    private static long PercentileNearestRank(List<long> sortedSamples, int percentile)
    {
        if (sortedSamples.Count == 0)
        {
            return 0;
        }
        var rank = (int)Math.Ceiling(percentile / 100.0 * sortedSamples.Count);
        var index = Math.Clamp(rank - 1, 0, sortedSamples.Count - 1);
        return sortedSamples[index];
    }

    [UnmanagedCallersOnly(EntryPoint = "haruki_assetstudio_free_string", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void FreeString(byte* value)
    {
        if (value != null)
        {
            NativeMemory.Free(value);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "haruki_assetstudio_free_buffer", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void FreeBuffer(byte* value)
    {
        if (value != null)
        {
            NativeMemory.Free(value);
        }
    }

    private static byte* AllocateJson<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, typeof(T), NativeJsonContext.Default);
        var bytes = Encoding.UTF8.GetBytes(json);
        var buffer = (byte*)NativeMemory.Alloc((nuint)bytes.Length + 1);
        fixed (byte* source = bytes)
        {
            Buffer.MemoryCopy(source, buffer, bytes.Length + 1, bytes.Length);
        }
        buffer[bytes.Length] = 0;
        return buffer;
    }

    private static NativePayloadBundle WritePayloadBundleToNative(IReadOnlyCollection<(string Name, byte[] Payload)> entries)
    {
        if (entries.Count == 0)
        {
            return default;
        }

        var capacity = EstimatePayloadBundleCapacity(entries);
        if (capacity <= 0)
        {
            throw new InvalidOperationException("payload bundle is too large to address as one native buffer");
        }

        var buffer = (byte*)NativeMemory.Alloc((nuint)capacity);
        try
        {
            var span = new Span<byte>(buffer, capacity);
            var offset = 0;
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset, sizeof(uint)), PayloadBundleMagic);
            offset += sizeof(uint);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset, sizeof(ushort)), PayloadBundleVersion);
            offset += sizeof(ushort);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset, sizeof(ushort)), PayloadBundleHeaderLength);
            offset += sizeof(ushort);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, sizeof(int)), entries.Count);
            offset += sizeof(int);
            var payloadDataBytes = SumPayloadDataBytes(entries);
            BinaryPrimitives.WriteInt64LittleEndian(span.Slice(offset, sizeof(long)), payloadDataBytes);
            offset += sizeof(long);

            foreach (var (name, payload) in entries)
            {
                var nameByteCount = Encoding.UTF8.GetByteCount(name);
                BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, sizeof(int)), nameByteCount);
                offset += sizeof(int);
                BinaryPrimitives.WriteInt64LittleEndian(span.Slice(offset, sizeof(long)), payload.Length);
                offset += sizeof(long);
                var written = Encoding.UTF8.GetBytes(name, span.Slice(offset, nameByteCount));
                offset += written;
                payload.CopyTo(span.Slice(offset, payload.Length));
                offset += payload.Length;
            }

            return new NativePayloadBundle(
                buffer,
                capacity,
                PayloadBundleVersion,
                entries.Count,
                payloadDataBytes);
        }
        catch
        {
            NativeMemory.Free(buffer);
            throw;
        }
    }

    private static int EstimatePayloadBundleCapacity(IReadOnlyCollection<(string Name, byte[] Payload)> entries)
    {
        long capacity = PayloadBundleHeaderLength;
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

    private static long SumPayloadDataBytes(IReadOnlyCollection<(string Name, byte[] Payload)> entries)
    {
        long bytes = 0;
        foreach (var (_, payload) in entries)
        {
            bytes += payload.Length;
        }
        return bytes;
    }

    private readonly struct NativePayloadBundle
    {
        public NativePayloadBundle(byte* pointer, long length, int version, int entryCount, long dataBytes)
        {
            Pointer = pointer;
            Length = length;
            Version = version;
            EntryCount = entryCount;
            DataBytes = dataBytes;
        }

        public byte* Pointer { get; }
        public long Length { get; }
        public int Version { get; }
        public int EntryCount { get; }
        public long DataBytes { get; }
    }

    private static void ResetProcessLocalState()
    {
        AssetStudioCliRunner.ResetProcessLocalState();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static string ShellQuote(string value)
    {
        return value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
    }

    private static IntPtr ResolveAssetStudioNativeLibrary(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, "Texture2DDecoderNative", StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        foreach (var candidate in NativeDependencyCandidates())
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> NativeDependencyCandidates()
    {
        var fileName = NativeDependencyFileName();
        foreach (var directory in NativeDependencyDirectories())
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                yield return Path.Combine(directory, fileName);
            }
        }
    }

    private static IEnumerable<string> NativeDependencyDirectories()
    {
        var configuredPath = Environment.GetEnvironmentVariable("HARUKI_ASSET_STUDIO_NATIVE_LIBRARY_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            yield return Path.GetDirectoryName(configuredPath)!;
        }

        yield return AppContext.BaseDirectory;
        yield return Environment.CurrentDirectory;
    }

    private static string NativeDependencyFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Texture2DDecoderNative.dll";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "libTexture2DDecoderNative.dylib";
        }
        return "libTexture2DDecoderNative.so";
    }
}

internal sealed class ConsoleCapture : IDisposable
{
    private readonly TextWriter previousOut;
    private readonly TextWriter previousError;
    private readonly ThreadSafeStringWriter? standardOutput;
    private readonly ThreadSafeStringWriter? standardError;

    private ConsoleCapture(TextWriter previousOut, TextWriter previousError, ThreadSafeStringWriter? standardOutput, ThreadSafeStringWriter? standardError)
    {
        this.previousOut = previousOut;
        this.previousError = previousError;
        this.standardOutput = standardOutput;
        this.standardError = standardError;
    }

    public string StandardOutput => standardOutput?.ToString() ?? string.Empty;

    public string StandardError => standardError?.ToString() ?? string.Empty;

    public static ConsoleCapture Start(bool captureText)
    {
        var previousOut = Console.Out;
        var previousError = Console.Error;
        ThreadSafeStringWriter? standardOutput = null;
        ThreadSafeStringWriter? standardError = null;
        if (captureText)
        {
            standardOutput = new ThreadSafeStringWriter(CultureInfo.InvariantCulture);
            standardError = new ThreadSafeStringWriter(CultureInfo.InvariantCulture);
            Console.SetOut(standardOutput);
            Console.SetError(standardError);
        }
        else
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
        }
        return new ConsoleCapture(previousOut, previousError, standardOutput, standardError);
    }

    public void Dispose()
    {
        Console.SetOut(previousOut);
        Console.SetError(previousError);
        standardOutput?.Dispose();
        standardError?.Dispose();
    }
}

internal sealed class ThreadSafeStringWriter : TextWriter
{
    private readonly object sync = new();
    private readonly StringBuilder buffer = new();
    private readonly IFormatProvider formatProvider;

    public ThreadSafeStringWriter(IFormatProvider formatProvider)
    {
        this.formatProvider = formatProvider;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override IFormatProvider FormatProvider => formatProvider;

    public override void Write(char value)
    {
        lock (sync)
        {
            buffer.Append(value);
        }
    }

    public override void Write(char[] buffer, int index, int count)
    {
        lock (sync)
        {
            this.buffer.Append(buffer, index, count);
        }
    }

    public override void Write(string? value)
    {
        if (value == null)
        {
            return;
        }

        lock (sync)
        {
            buffer.Append(value);
        }
    }

    public override string ToString()
    {
        lock (sync)
        {
            return buffer.ToString();
        }
    }
}

internal sealed class NativeDiagnostics
{
    private readonly object sync = new();
    private readonly bool enabled;
    private readonly string? logPath;

    private NativeDiagnostics(bool enabled, string? logPath)
    {
        this.enabled = enabled;
        this.logPath = logPath;
    }

    public bool CaptureConsole => enabled;

    public static NativeDiagnostics CreateFromEnvironment()
    {
        var enabled = IsEnabled(Environment.GetEnvironmentVariable("HARUKI_ASSET_STUDIO_NATIVE_TRACE"))
            || IsEnabled(Environment.GetEnvironmentVariable("HARUKI_ASSET_STUDIO_NATIVE_DIAGNOSTICS"));
        if (!enabled)
        {
            return new NativeDiagnostics(false, null);
        }

        var logDir = Environment.GetEnvironmentVariable("HARUKI_ASSET_STUDIO_NATIVE_LOG_DIR");
        if (string.IsNullOrWhiteSpace(logDir))
        {
            logDir = Path.Combine(Path.GetTempPath(), "haruki-assetstudio-native");
        }
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, $"native-{Environment.ProcessId}.log");
        return new NativeDiagnostics(true, logPath);
    }

    public string Begin(string operation, string? inputPath)
    {
        var operationId = $"{Environment.ProcessId}:{Environment.CurrentManagedThreadId}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        Event(operationId, "begin", $"{operation} input={inputPath}");
        return operationId;
    }

    public void End(string operationId, string operation, long durationMs, string detail)
    {
        Event(operationId, "end", $"{operation} duration_ms={durationMs} {detail}");
    }

    public void Event(string operationId, string eventName, string? detail = null)
    {
        if (!enabled || logPath == null)
        {
            return;
        }

        var line = $"{DateTimeOffset.UtcNow:O} pid={Environment.ProcessId} tid={Environment.CurrentManagedThreadId} op={operationId} event={eventName}";
        if (!string.IsNullOrWhiteSpace(detail))
        {
            line += $" {detail}";
        }
        Write(line);
    }

    public void Console(string operationId, string stdout, string stderr)
    {
        if (!enabled)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            Event(operationId, "captured_stdout", stdout.Trim());
        }
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            Event(operationId, "captured_stderr", stderr.Trim());
        }
    }

    public void Exception(string operationId, Exception exception)
    {
        Event(operationId, "exception", exception.ToString());
    }

    public IReadOnlyCollection<string> ResponseWarnings(string operationId, params string[] extraWarnings)
    {
        var hasDiagnostics = enabled && logPath != null;
        if (!hasDiagnostics && extraWarnings.Length == 0)
        {
            return Array.Empty<string>();
        }

        var warnings = new List<string>(extraWarnings.Length + (hasDiagnostics ? 1 : 0));
        if (hasDiagnostics)
        {
            warnings.Add($"native diagnostics op={operationId} log={logPath}");
        }
        warnings.AddRange(extraWarnings);
        return warnings;
    }

    private void Write(string line)
    {
        lock (sync)
        {
            File.AppendAllText(logPath!, line + Environment.NewLine);
        }
    }

    private static bool IsEnabled(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("debug", StringComparison.OrdinalIgnoreCase)
            || value.Equals("trace", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class ActiveNativeContext
{
    public ActiveNativeContext(
        long contextId,
        string operationId,
        string inputPath,
        Stopwatch stopwatch,
        IReadOnlyCollection<AssetStudioAssetInfo> assets)
    {
        ContextId = contextId;
        OperationId = operationId;
        InputPath = inputPath;
        Stopwatch = stopwatch;
        Assets = assets;
    }

    public long ContextId { get; }
    public string OperationId { get; }
    public string InputPath { get; }
    public Stopwatch Stopwatch { get; }
    public IReadOnlyCollection<AssetStudioAssetInfo> Assets { get; }
}

internal sealed class ContextCloseRequest
{
    [JsonPropertyName("context_id")]
    public long ContextId { get; set; }
}

internal sealed class ContextListObjectsRequest
{
    [JsonPropertyName("context_id")]
    public long ContextId { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 1024;
}

internal sealed class ContextReadObjectRequest
{
    [JsonPropertyName("context_id")]
    public long ContextId { get; set; }

    [JsonPropertyName("path_id")]
    public long PathId { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "auto";

    [JsonPropertyName("image_format")]
    public string ImageFormat { get; set; } = "bmp";

    public AssetStudioObjectReadOptions ToReadOptions()
    {
        return new AssetStudioObjectReadOptions
        {
            PathId = PathId,
            Kind = Kind,
            ImageFormat = ImageFormat,
        };
    }
}

internal sealed class ContextReadObjectsRequest
{
    [JsonPropertyName("context_id")]
    public long ContextId { get; set; }

    [JsonPropertyName("objects")]
    public List<ContextReadObjectItemRequest> Objects { get; set; } = new();
}

internal sealed class ContextReadObjectItemRequest
{
    [JsonPropertyName("path_id")]
    public long PathId { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "auto";

    [JsonPropertyName("image_format")]
    public string ImageFormat { get; set; } = "bmp";

    public AssetStudioObjectReadOptions ToReadOptions()
    {
        return new AssetStudioObjectReadOptions
        {
            PathId = PathId,
            Kind = Kind,
            ImageFormat = ImageFormat,
        };
    }
}

internal sealed class InspectRequest
{
    [JsonPropertyName("input_path")]
    public string InputPath { get; set; } = "";

    [JsonPropertyName("asset_types")]
    public List<string>? AssetTypes { get; set; }

    [JsonPropertyName("unity_version")]
    public string? UnityVersion { get; set; }

    [JsonPropertyName("filter_exclude_mode")]
    public bool FilterExcludeMode { get; set; }

    [JsonPropertyName("filter_with_regex")]
    public bool FilterWithRegex { get; set; }

    [JsonPropertyName("filter_by_name")]
    public string? FilterByName { get; set; }

    [JsonPropertyName("filter_by_container")]
    public string? FilterByContainer { get; set; }

    [JsonPropertyName("filter_by_path_ids")]
    public List<long>? FilterByPathIds { get; set; }

    [JsonPropertyName("load_all_assets")]
    public bool LoadAllAssets { get; set; }

    [JsonPropertyName("include_assets")]
    public bool IncludeAssets { get; set; } = true;

    [JsonPropertyName("output_dir")]
    public string? OutputDir { get; set; }

    public AssetStudioInspectOptions ToInspectOptions()
    {
        return new AssetStudioInspectOptions
        {
            InputPath = InputPath,
            AssetTypes = AssetTypes,
            UnityVersion = UnityVersion,
            FilterExcludeMode = FilterExcludeMode,
            FilterWithRegex = FilterWithRegex,
            FilterByName = FilterByName,
            FilterByContainer = FilterByContainer,
            FilterByPathIds = FilterByPathIds,
            LoadAllAssets = LoadAllAssets,
            OutputDir = OutputDir,
        };
    }
}

internal sealed class InspectResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("assets_file_count")]
    public int AssetsFileCount { get; set; }

    [JsonPropertyName("exportable_asset_count")]
    public int ExportableAssetCount { get; set; }

    [JsonPropertyName("unity_version")]
    public string? UnityVersion { get; set; }

    [JsonPropertyName("assets")]
    public IReadOnlyCollection<AssetStudioAssetInfo> Assets { get; set; } = Array.Empty<AssetStudioAssetInfo>();

    [JsonPropertyName("warnings")]
    public IReadOnlyCollection<string> Warnings { get; set; } = Array.Empty<string>();

    [JsonPropertyName("phase_ms")]
    public IReadOnlyDictionary<string, long> PhaseMs { get; set; } = new Dictionary<string, long>();

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }

    public static InspectResponse Fail(string error, long durationMs = 0) => new()
    {
        Success = false,
        Assets = Array.Empty<AssetStudioAssetInfo>(),
        Warnings = Array.Empty<string>(),
        PhaseMs = new Dictionary<string, long>(),
        Error = error,
        DurationMs = durationMs,
    };
}

internal sealed class ContextOpenResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("context_id")]
    public long ContextId { get; set; }

    [JsonPropertyName("assets_file_count")]
    public int AssetsFileCount { get; set; }

    [JsonPropertyName("exportable_asset_count")]
    public int ExportableAssetCount { get; set; }

    [JsonPropertyName("unity_version")]
    public string? UnityVersion { get; set; }

    [JsonPropertyName("assets")]
    public IReadOnlyCollection<AssetStudioAssetInfo> Assets { get; set; } = Array.Empty<AssetStudioAssetInfo>();

    [JsonPropertyName("warnings")]
    public IReadOnlyCollection<string> Warnings { get; set; } = Array.Empty<string>();

    [JsonPropertyName("phase_ms")]
    public IReadOnlyDictionary<string, long> PhaseMs { get; set; } = new Dictionary<string, long>();

    [JsonPropertyName("worker_id")]
    public string? WorkerId { get; set; }

    [JsonPropertyName("object_index_count")]
    public int ObjectIndexCount { get; set; }

    [JsonPropertyName("returned_asset_count")]
    public int ReturnedAssetCount { get; set; }

    [JsonPropertyName("has_more_assets")]
    public bool HasMoreAssets { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }

    public static ContextOpenResponse Fail(string error, long durationMs = 0) => new()
    {
        Success = false,
        Assets = Array.Empty<AssetStudioAssetInfo>(),
        Warnings = Array.Empty<string>(),
        PhaseMs = new Dictionary<string, long>(),
        Error = error,
        DurationMs = durationMs,
    };
}

internal sealed class ContextListObjectsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("context_id")]
    public long ContextId { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("next_offset")]
    public int? NextOffset { get; set; }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("assets")]
    public IReadOnlyCollection<AssetStudioAssetInfo> Assets { get; set; } = Array.Empty<AssetStudioAssetInfo>();

    [JsonPropertyName("warnings")]
    public IReadOnlyCollection<string> Warnings { get; set; } = Array.Empty<string>();

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }

    public static ContextListObjectsResponse Fail(string error, long durationMs = 0) => new()
    {
        Success = false,
        Assets = Array.Empty<AssetStudioAssetInfo>(),
        Warnings = Array.Empty<string>(),
        Error = error,
        DurationMs = durationMs,
    };
}

internal sealed class ContextCloseResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("warnings")]
    public IReadOnlyCollection<string> Warnings { get; set; } = Array.Empty<string>();

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }

    public static ContextCloseResponse Fail(string error, long durationMs = 0) => new()
    {
        Success = false,
        Warnings = Array.Empty<string>(),
        Error = error,
        DurationMs = durationMs,
    };
}

internal sealed class ObjectReadResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("asset")]
    public AssetStudioAssetInfo? Asset { get; set; }

    [JsonPropertyName("payload_kind")]
    public string? PayloadKind { get; set; }

    [JsonPropertyName("payload_len")]
    public long PayloadLen { get; set; }

    [JsonPropertyName("suggested_extension")]
    public string? SuggestedExtension { get; set; }

    [JsonPropertyName("warnings")]
    public IReadOnlyCollection<string> Warnings { get; set; } = Array.Empty<string>();

    [JsonPropertyName("phase_ms")]
    public IReadOnlyDictionary<string, long> PhaseMs { get; set; } = new Dictionary<string, long>();

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }

    public static ObjectReadResponse Fail(string error, long durationMs = 0) => new()
    {
        Success = false,
        Warnings = Array.Empty<string>(),
        PhaseMs = new Dictionary<string, long>(),
        Error = error,
        DurationMs = durationMs,
    };
}

internal sealed class ObjectReadBatchResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("reads")]
    public IReadOnlyCollection<ObjectReadResponse> Reads { get; set; } = Array.Empty<ObjectReadResponse>();

    [JsonPropertyName("warnings")]
    public IReadOnlyCollection<string> Warnings { get; set; } = Array.Empty<string>();

    [JsonPropertyName("phase_ms")]
    public IReadOnlyDictionary<string, long> PhaseMs { get; set; } = new Dictionary<string, long>();

    [JsonPropertyName("asset_type_counts")]
    public IReadOnlyDictionary<string, int> AssetTypeCounts { get; set; } = new Dictionary<string, int>();

    [JsonPropertyName("payload_kind_counts")]
    public IReadOnlyDictionary<string, int> PayloadKindCounts { get; set; } = new Dictionary<string, int>();

    [JsonPropertyName("payload_bytes_by_kind")]
    public IReadOnlyDictionary<string, long> PayloadBytesByKind { get; set; } = new Dictionary<string, long>();

    [JsonPropertyName("payload_len")]
    public long PayloadLen { get; set; }

    [JsonPropertyName("object_count")]
    public int ObjectCount { get; set; }

    [JsonPropertyName("payload_bundle_version")]
    public int PayloadBundleVersion { get; set; }

    [JsonPropertyName("payload_bundle_entry_count")]
    public int PayloadBundleEntryCount { get; set; }

    [JsonPropertyName("payload_bundle_bytes")]
    public long PayloadBundleBytes { get; set; }

    [JsonPropertyName("payload_data_bytes")]
    public long PayloadDataBytes { get; set; }

    [JsonPropertyName("failed_count")]
    public int FailedCount { get; set; }

    [JsonPropertyName("read_payload_ms")]
    public long ReadPayloadMs { get; set; }

    [JsonPropertyName("worker_id")]
    public string? WorkerId { get; set; }

    [JsonPropertyName("call_seq")]
    public long CallSeq { get; set; }

    [JsonPropertyName("object_index_count")]
    public int ObjectIndexCount { get; set; }

    [JsonPropertyName("phase_stats")]
    public IReadOnlyDictionary<string, NativePhaseStats> PhaseStats { get; set; } = new Dictionary<string, NativePhaseStats>();

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }

    public static ObjectReadBatchResponse Fail(
        string error,
        long durationMs = 0,
        string? workerId = null,
        long callSeq = 0,
        int objectIndexCount = 0,
        IReadOnlyDictionary<string, long>? phaseMs = null) => new()
    {
        Success = false,
        Reads = Array.Empty<ObjectReadResponse>(),
        Warnings = Array.Empty<string>(),
        PhaseMs = phaseMs ?? new Dictionary<string, long>(),
        AssetTypeCounts = new Dictionary<string, int>(),
        PayloadKindCounts = new Dictionary<string, int>(),
        PayloadBytesByKind = new Dictionary<string, long>(),
        WorkerId = workerId,
        CallSeq = callSeq,
        ObjectIndexCount = objectIndexCount,
        Error = error,
        DurationMs = durationMs,
    };
}

internal sealed class NativePhaseStats
{
    [JsonPropertyName("p50_ms")]
    public long P50Ms { get; set; }

    [JsonPropertyName("p95_ms")]
    public long P95Ms { get; set; }
}

internal sealed class VersionResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("adapter_version")]
    public string? AdapterVersion { get; set; }

    [JsonPropertyName("assetstudio_cli_version")]
    public string? AssetStudioCliVersion { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    public static VersionResponse Fail(string error) => new()
    {
        Success = false,
        Error = error,
    };
}

    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(ContextCloseRequest))]
    [JsonSerializable(typeof(ContextListObjectsRequest))]
    [JsonSerializable(typeof(ContextReadObjectRequest))]
    [JsonSerializable(typeof(ContextReadObjectsRequest))]
    [JsonSerializable(typeof(ContextReadObjectItemRequest))]
    [JsonSerializable(typeof(ContextOpenResponse))]
    [JsonSerializable(typeof(ContextCloseResponse))]
    [JsonSerializable(typeof(ContextListObjectsResponse))]
    [JsonSerializable(typeof(ObjectReadResponse))]
[JsonSerializable(typeof(ObjectReadBatchResponse))]
[JsonSerializable(typeof(NativePhaseStats))]
[JsonSerializable(typeof(InspectRequest))]
[JsonSerializable(typeof(InspectResponse))]
[JsonSerializable(typeof(VersionResponse))]
[JsonSerializable(typeof(Dictionary<string, long>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(Dictionary<string, NativePhaseStats>))]
internal partial class NativeJsonContext : JsonSerializerContext
{
}
