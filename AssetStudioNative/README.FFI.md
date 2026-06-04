# Haruki AssetStudio Native FFI

This NativeAOT library is intended to be consumed from C-compatible hosts such as Rust, C, C++, Swift, or Python FFI bindings. The exported function names are stable and remain compatible with the previous adapter, while response envelopes now expose ABI/schema metadata and machine-readable error codes.

## Architecture

The native adapter calls `AssetStudioCore` directly. The CLI is now a separate entry-point adapter that also calls the same core project, so FFI consumers no longer depend on the CLI assembly or CLI runner.

## Lifecycle

Use the context API for library integrations:

1. `haruki_assetstudio_capabilities(&json)` to check `abi_version`, `schema_version`, `ffi_mode`, `core_api_version`, engine limits, and supported payload features.
2. `haruki_assetstudio_abi_layout(&json)` to verify native struct sizes against the caller's compiled bindings.
3. `haruki_assetstudio_limits_v1(&response)` to get hard limits without parsing JSON.
4. `haruki_assetstudio_context_open_v2(&request, &response)` to load and index a bundle or asset directory without returning the full object list.
5. `haruki_assetstudio_context_list_objects_size_v3(...)` and `haruki_assetstudio_context_list_objects_into_v3(...)` to page through objects into a caller-owned object table buffer.
6. `haruki_assetstudio_context_read_objects_size_v4(...)` to get exact metadata/string/payload buffer sizes for a typed batch read.
7. `haruki_assetstudio_context_read_objects_into_v4(...)`, direct v6, or direct retry v7 to read payloads into caller-owned or safe native-owned buffers.
8. `haruki_assetstudio_context_close_v2(&request, &response)` to release the active context.
9. Free every returned JSON string with `haruki_assetstudio_free_string`. For list v3 and read v4 typed calls, the caller owns the output buffers. For v3 typed batch reads, release `result_handle` once with `haruki_assetstudio_result_free`; for legacy JSON reads and list v2, release returned payload/table buffers with `haruki_assetstudio_free_buffer`.

Core logger, progress, runtime options, and ImageSharp timing sinks are execution-context local, so `capabilities.legacy_static_engine` is `false`. Multiple active contexts are supported up to `capabilities.max_active_contexts`, and open/list/lookup/read/close calls use per-context lifetime guards instead of one cross-context operation gate. `capabilities.supports_concurrent_operations` is `true`, with narrow internal locks retained only for dependencies that require them, such as the NativeAOT ImageSharp guard. Per-context lifetime guards are enabled (`supports_context_lifetime_guards=true`): calls retain the context while running, and `close` returns `context_busy` / `HARUKI_ASSETSTUDIO_CONTEXT_ERROR_CONTEXT_BUSY` if another call is still using that context. The Native layer does not redirect process-wide `Console.Out`/`Console.Error` during normal calls; `capabilities.native_console_capture` is `false`.

The typed ABI is defensive, but it is still a C ABI. Null pointers, negative lengths, oversized UTF-8 lengths, oversized object table page limits, oversized batch counts, invalid `struct_size`, missing contexts, unsupported object kinds, and insufficient caller buffers are converted to status/error codes. `capabilities.max_native_utf8_bytes`, `capabilities.max_object_table_page_limit`, `capabilities.max_object_read_batch_count`, and `haruki_assetstudio_limits_v1` expose the current hard limits. Dangling pointers, forged addresses, or pointers to memory shorter than the declared length are undefined behavior at the process boundary and cannot be reliably recovered by the callee. Rust bindings should keep request buffers alive for the entire call, pass exact byte lengths, initialize every struct with zeroed memory plus `struct_size`, and treat all returned pointers as borrowed unless the specific function documents caller ownership.

Recommended SDK flow:

```text
capabilities -> abi_layout -> limits_v1 -> open_v2 -> typed list size_v3 -> typed list into_v3 -> typed read by-index direct retry_v7 -> result_free if needed -> close_v2
```

For every typed v2/v3/v4 request, set `struct_size` to `sizeof(request_type)`, set `flags` to `0` unless a future capability documents a flag, and set all `reserved` fields to `0`. Typed responses fill their own `struct_size` so callers can validate the ABI layout they compiled against.

`capabilities.struct_sizes` and `haruki_assetstudio_abi_layout` report the native `sizeof(...)` for each public typed struct. SDK bindings should compare those values with their own struct sizes during startup and fail fast if a layout differs. `haruki_assetstudio_limits_v1` returns the same hard limits through a typed struct, so hot-path hosts do not need JSON parsing to discover page, UTF-8, batch, payload, cache, and legacy engine limits.

For high-throughput object enumeration, prefer `haruki_assetstudio_context_list_objects_size_v3` plus `haruki_assetstudio_context_list_objects_into_v3` when `capabilities.supports_caller_provided_object_table_buffers` is `true`. It writes the typed object table and UTF-8 string pool into caller-owned memory, avoiding large JSON responses and avoiding one native alloc/free per page. `haruki_assetstudio_context_list_objects_v2` remains the compatibility typed path when a caller wants the library to allocate the table buffer. If `limit <= 0`, typed table calls return at most `max_object_table_page_limit` objects; if `limit > max_object_table_page_limit`, they return `HARUKI_ASSETSTUDIO_INVALID_REQUEST`. When `supports_indexed_asset_type_filter` is true, `asset_types_csv_utf8` filtering is handled by a Core-side type index while preserving object order.

For targeted object discovery, prefer `haruki_assetstudio_context_lookup_objects_size_v2` plus `haruki_assetstudio_context_lookup_objects_into_v2` when `capabilities.supports_caller_provided_object_lookup_buffers` is `true`. It reuses the typed object table layout and can lookup by path id, name, container, or type before the caller pays to move a wider object list across the ABI boundary. `haruki_assetstudio_context_lookup_objects_v1` remains the compatibility typed path when a caller wants the library to allocate the table buffer. Exact path id/name/container/type lookup is indexed when `supports_indexed_exact_object_lookup` is true; set `HARUKI_ASSETSTUDIO_OBJECT_LOOKUP_CONTAINS` only when substring matching is needed.

For high-throughput object reads after a typed list/lookup, prefer `haruki_assetstudio_context_read_objects_by_index_direct_into_v6` when `capabilities.supports_direct_object_read_into` and `capabilities.supports_object_read_by_index` are both `true` and the caller already has reusable buffers. The object `index` returned by list/lookup is stable for the lifetime of the context, so this path avoids path-id dictionary lookup, keeps all read output in caller-owned buffers, and skips the per-context pending batch cache. If `capabilities.supports_payload_kind_capacity_hints` is true, each typed object row includes default `estimated_payload_capacity` plus kind-specific `raw_payload_capacity`, `image_payload_capacity`, and `text_payload_capacity`; SDK callers can allocate or grow their reusable payload buffer from the best matching hint and call direct v6 without a size prepass. If direct v6 returns `HARUKI_ASSETSTUDIO_BUFFER_TOO_SMALL`, grow to `response.required_payload_len` and retry.

For a safer SDK default, use `haruki_assetstudio_context_read_objects_by_index_direct_retry_v7` or path-id `haruki_assetstudio_context_read_objects_direct_retry_v7` when `capabilities.supports_direct_object_read_retry` is true. v7 accepts the same caller-owned buffers as v6. If they are sufficient, `result_handle` is `0` and returned pointers borrow caller memory. If either buffer is too small or null, Native allocates exact-size replacement buffers, sets `ownership_flags` and `result_handle`, and returns the normal object read status instead of `BUFFER_TOO_SMALL`; release that handle once with `haruki_assetstudio_result_free`. This avoids a manual Rust resize loop while keeping FFI exceptions contained as fixed status/error codes.

When buffer sizes are unknown and the caller wants strict caller-owned memory only, use the two-step size/into path instead: `haruki_assetstudio_context_read_objects_by_index_size_v5` plus `haruki_assetstudio_context_read_objects_by_index_into_v5`, or path-id `haruki_assetstudio_context_read_objects_size_v4` plus `haruki_assetstudio_context_read_objects_into_v4`. All these paths avoid JSON response construction and write per-item typed error messages into the same UTF-8 string pool. If some items fail and some succeed, `response.status` remains `0` while `response.error_code` is `HARUKI_ASSETSTUDIO_OBJECT_READ_ERROR_PARTIAL_FAILURE`; inspect each item status/error code before using its payload range. If all items fail with the same reason, the batch returns that concrete error code instead of a generic internal error. When `supports_cached_object_read_size_v4` is true, the two-step size path keeps the most recent matching size result inside the context so the following `into` call can avoid re-reading the same objects. The cache is capped by `max_cached_object_read_batch_payload_bytes`; set `HARUKI_ASSET_STUDIO_NATIVE_MAX_CACHED_READ_PAYLOAD_BYTES=0` to disable it or to another byte value to tune memory use.

`haruki_assetstudio_context_read_objects_v2` and `haruki_assetstudio_context_read_objects_v3` remain compatibility typed paths when a caller prefers library-owned buffers. They now use the same Core payload writer path as the caller-owned read APIs. v3 additionally returns one `result_handle` that owns the metadata buffer and payload buffer together.

When `capabilities.supports_native_streaming_payload` is `true`, direct v6/v7 reads can write Core output into caller/native memory through the writer path. Capability arrays describe how native each payload kind is:

- `native_streaming_payload_kinds` / `source_streaming_payload_kinds`: source-level streaming with no full managed payload buffer. Current kinds: `raw`, `audio_raw`, `video_raw`.
- `resident_buffer_payload_kinds`: direct write from arrays already resident on parsed objects. Current kinds: `movie_ogv`, `font`, `text_bytes`.
- `generated_streaming_payload_kinds`: generated output is streamed to the caller/native buffer instead of first becoming a full managed `byte[]`. Current kinds: `shader_text`, `typetree_json`, `mesh_obj`, `image_bmp`, `image_png`, `image_array_bundle_bmp`, `image_array_bundle_png`. `shader_text` is mixed: uncompressed shaders without subprogram blobs write the original script bytes directly after the header, while compressed/subprogram shaders still generate converted text. Texture array bundles use a counting pass before the write pass so entry lengths can be emitted without retaining each encoded layer as a managed array.
- `temp_file_intermediate_payload_kinds`: output is streamed from temporary files into the caller/native buffer. Current kind: `animator_bundle_fbx`.
- `managed_intermediate_payload_kinds`: still use full managed payload arrays before the final direct write. This is currently empty for the direct v6/v7 writer path.

FBX/animator export still uses a temporary directory internally, but the FFI bundle pack streams those files to the payload writer. `ImageSharpNativeAotGuard` may still serialize ImageSharp encoder calls behind a narrow dependency lock under NativeAOT.

When `capabilities.supports_estimated_native_batch_capacity` is `true`, v3 uses the indexed object sizes to reserve an initial native payload capacity before reading the batch. This is a performance hint that reduces reallocations; callers should still trust only `response.payload_len` for the valid byte range.

When `capabilities.object_table_abi_version >= 3`, `haruki_assetstudio_asset_object` includes:

- `estimated_payload_capacity`: recommended caller-owned payload buffer capacity for the default direct read path.
- `raw_payload_capacity`, `image_payload_capacity`, `text_payload_capacity`: recommended capacity for those payload families when non-zero.
- `payload_capacity_flags`: bit 0 means estimated, bit 1 means exact for the default auto/raw-style payload, bits 2/3/4 indicate raw/image/text hints are present.

Capacity hints are not a correctness contract. Direct read responses remain authoritative: use `payload_len` for valid bytes, and grow/retry on `HARUKI_ASSETSTUDIO_BUFFER_TOO_SMALL`.

Texture decoding depends on the platform native `Texture2DDecoderNative` library. NativeAOT publish copies the current RID dependency next to `HarukiAssetStudioNative` by default. External packagers should keep that file beside the FFI library or set `HARUKI_ASSET_STUDIO_NATIVE_LIBRARY_PATH` to a dependency file, dependency directory, or path-list. `capabilities.supports_native_dependency_resolver`, `texture2d_decoder_native_dependency`, and `texture2d_decoder_native_candidate_paths` expose the resolver contract for SDK diagnostics.

## Response Envelope

All JSON responses include these stable fields:

```json
{
  "success": true,
  "abi_version": 1,
  "schema_version": 2,
  "warnings": [],
  "duration_ms": 0
}
```

Failures include both the legacy `error` string and the stable fields:

```json
{
  "success": false,
  "abi_version": 1,
  "schema_version": 2,
  "error": "...",
  "error_code": "context_not_found",
  "error_message": "..."
}
```

Stable `error_code` values are:

- `null_pointer`
- `invalid_json`
- `invalid_request`
- `context_not_found`
- `context_limit`
- `context_busy`
- `asset_not_found`
- `unsupported_kind`
- `internal_error`

## Request Shapes

Open request:

```json
{
  "input_path": "/path/to/bundle-or-directory",
  "unity_version": "2022.3.62f1",
  "asset_types": ["Texture2D", "TextAsset"],
  "include_assets": false
}
```

List request:

```json
{
  "context_id": 1,
  "offset": 0,
  "limit": 1024,
  "asset_types": ["TextAsset", "MonoBehaviour"]
}
```

Typed list v3 request:

```c
haruki_assetstudio_object_list_request request = {
  .struct_size = sizeof(haruki_assetstudio_object_list_request),
  .context_id = context_id,
  .offset = 0,
  .limit = 4096,
  .asset_types_csv_utf8 = (const uint8_t *)"TextAsset,MonoBehaviour",
  .asset_types_csv_utf8_len = 23,
  .flags = 0,
  .reserved = 0
};
haruki_assetstudio_object_table size = {0};
int size_rc = haruki_assetstudio_context_list_objects_size_v3(&request, &size);
uint8_t *buffer = malloc((size_t)size.buffer_len);
haruki_assetstudio_object_list_into_request_v3 into = {
  .struct_size = sizeof(haruki_assetstudio_object_list_into_request_v3),
  .context_id = context_id,
  .offset = request.offset,
  .limit = request.limit,
  .asset_types_csv_utf8 = request.asset_types_csv_utf8,
  .asset_types_csv_utf8_len = request.asset_types_csv_utf8_len,
  .flags = 0,
  .reserved = 0,
  .buffer = buffer,
  .buffer_len = size.buffer_len
};
haruki_assetstudio_object_table table = {0};
int rc = haruki_assetstudio_context_list_objects_into_v3(&into, &table);
/* table.objects points into caller-owned buffer; string offsets are relative to table.string_data. */
free(buffer);
```

Typed lookup request:

```c
haruki_assetstudio_object_lookup_request lookup = {
  .struct_size = sizeof(haruki_assetstudio_object_lookup_request),
  .context_id = context_id,
  .lookup_kind = HARUKI_ASSETSTUDIO_OBJECT_LOOKUP_PATH_ID,
  .path_id = path_id,
  .query_utf8 = NULL,
  .query_utf8_len = 0,
  .asset_types_csv_utf8 = NULL,
  .asset_types_csv_utf8_len = 0,
  .offset = 0,
  .limit = 1,
  .flags = 0,
  .reserved = 0
};
haruki_assetstudio_object_table lookup_table = {0};
int lookup_rc = haruki_assetstudio_context_lookup_objects_v1(&lookup, &lookup_table);
haruki_assetstudio_free_buffer(lookup_table.buffer);
```

Typed list v3 memory rules:

- `size_v3` fills `buffer_len` with the required contiguous table bytes and `string_data_len` with the UTF-8 string pool bytes.
- `into_v3` writes both `table.objects` and `table.string_data` into the caller-owned `request.buffer`.
- Do not call `haruki_assetstudio_free_buffer` for list v3 output buffers.
- If the provided buffer is too small, `into_v3` returns `HARUKI_ASSETSTUDIO_BUFFER_TOO_SMALL`/`8` and leaves `objects`/`string_data` unset.
- `table.objects[i]` is valid while `table.buffer` is alive.
- Every string field is `(offset, len)` into `table.string_data`; offsets are byte offsets, and strings are UTF-8 without null terminators.
- Empty strings have `len == 0`; callers should ignore the offset in that case.
- `table.next_offset == -1` means the page is complete. Otherwise pass `next_offset` into the next request.
- On failure, `table.status` and `table.error_code` mirror the stable native return code, and ABI/schema fields are still filled when `table` itself is writable.

Typed list v2 memory rules:

- `table.buffer` owns both `table.objects` and `table.string_data`.
- Release `table.buffer` exactly once with `haruki_assetstudio_free_buffer`; do not free `objects` or `string_data` separately.

Batch read request:

```json
{
  "context_id": 1,
  "objects": [
    { "path_id": 123, "kind": "auto", "image_format": "bmp" }
  ]
}
```

Typed batch read v4 memory rules:

- Set `request.struct_size` to `sizeof(haruki_assetstudio_object_read_batch_request_v4)` for `size_v4`.
- Set every `reserved` field to `0`.
- Allocate `items_buffer` with at least `size_response.required_items_buffer_len` bytes and `payload` with at least `size_response.required_payload_len` bytes.
- Set `into_request.struct_size` to `sizeof(haruki_assetstudio_object_read_batch_into_request_v4)` for `into_v4`.
- `into_response.items` and `into_response.string_data` point inside the caller-owned `items_buffer`; `into_response.payload` points to the caller-owned payload buffer.
- Every v4 item string field is `(offset, len)` into `into_response.string_data`, including `error_message_offset/error_message_len` for failed items.
- If a provided buffer is too small, `into_v4` returns `HARUKI_ASSETSTUDIO_BUFFER_TOO_SMALL`/`8` and fills `required_items_buffer_len`, `required_string_data_len`, and `required_payload_len` without writing item or payload data.
- Do not call `haruki_assetstudio_result_free` or `haruki_assetstudio_free_buffer` for v4 output buffers.

Typed batch read v3 memory rules:

- `response.result_handle` owns `response.items_buffer` and `response.payload`.
- Release `response.result_handle` exactly once with `haruki_assetstudio_result_free`.
- Do not pass `response.items_buffer` or `response.payload` to `haruki_assetstudio_free_buffer` when `result_handle != 0`.
- `response.items` and `response.string_data` point inside `response.items_buffer`.
- A second `haruki_assetstudio_result_free` for the same handle returns `HARUKI_ASSETSTUDIO_CONTEXT_NOT_FOUND`/`4`.
- Closing a context automatically releases any still-owned result handles for that context; freeing such a handle after close also returns `4`.

## Batch Payload Bundle v2

`haruki_assetstudio_context_read_objects` returns metadata in response JSON and a single binary payload bundle in `payload_ptr/payload_len`. Payload extraction uses the same Core writer path as typed batch reads; Native then packs the contiguous payload ranges into this bundle without retaining per-object managed payload arrays.

All integer fields are little-endian:

| Field | Type | Notes |
| --- | --- | --- |
| magic | `u32` | `0x42504148`, ASCII `HAPB` |
| version | `u16` | currently `2` |
| header_len | `u16` | currently `20` |
| entry_count | `i32` | number of entries |
| payload_data_bytes | `i64` | sum of payload bytes only |

Then for each entry:

| Field | Type | Notes |
| --- | --- | --- |
| name_len | `i32` | UTF-8 byte count |
| payload_len | `i64` | payload byte count |
| name | bytes | currently path id as decimal text |
| payload | bytes | object payload |

## Rust SDK Crate

The Rust wrapper lives in `AssetStudioNative/rust/haruki-assetstudio`. It loads the native library with `libloading`, validates typed ABI struct sizes through `haruki_assetstudio_abi_layout`, exposes capabilities, manages context close through RAII, lists and looks up objects through caller-owned table buffers, and reads objects by either path id or stable list index through direct retry v7. `ObjectReadResult` owns copied payload bytes plus per-item metadata (`payload_kind`, `suggested_extension`, offsets, lengths, and error fields), and `payload_for(item)` returns the item slice with bounds checks.

```bash
cargo run --manifest-path AssetStudioNative/rust/haruki-assetstudio/Cargo.toml \
  --example smoke -- \
  /path/to/HarukiAssetStudioNative.dylib \
  /path/to/resources.assets
```

## Contract Smoke Test

After publishing the NativeAOT library, run the Python contract smoke test against a real Unity asset file:

```bash
python3 AssetStudioNative/tests/ffi_contract_smoke.py \
  /path/to/HarukiAssetStudioNative.dylib \
  /path/to/resources.assets \
  --unity-version 2022.3.62f1
```

The smoke test verifies capabilities, stable error codes, lightweight open, paged list, JSON batch payload bundle v2, typed batch read v3 result handles, context limit, close, and memory release paths.

For list-path performance comparisons:

```bash
python3 AssetStudioNative/tests/ffi_list_benchmark.py \
  /path/to/HarukiAssetStudioNative.dylib \
  /path/to/resources.assets \
  --unity-version 2022.3.62f1 \
  --page-size 4096 \
  --rounds 5
```
