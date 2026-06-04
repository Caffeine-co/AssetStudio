// Minimal Rust smoke harness for HarukiAssetStudioNative.
//
// Cargo.toml dependencies:
//   libloading = "0.8"
//   serde_json = "1"
//
// Usage:
//   cargo run --release --example rust_smoke -- \
//     /path/to/HarukiAssetStudioNative.dylib /path/to/bundle 2022.3.62f1

use libloading::{Library, Symbol};
use serde_json::Value;
use std::ffi::CStr;
use std::os::raw::{c_char, c_int, c_longlong, c_uchar, c_void};

type NoRequestJsonFn = unsafe extern "C" fn(*mut *mut c_char) -> i32;
type FreeStringFn = unsafe extern "C" fn(*mut c_char);
type FreeBufferFn = unsafe extern "C" fn(*mut c_uchar);
type ContextOpenV2Fn = unsafe extern "C" fn(
    *const HarukiAssetStudioContextOpenRequest,
    *mut HarukiAssetStudioContextOpenResponse,
) -> i32;
type ContextCloseV2Fn = unsafe extern "C" fn(
    *const HarukiAssetStudioContextCloseRequest,
    *mut HarukiAssetStudioContextCloseResponse,
) -> i32;
type ListObjectsV2Fn = unsafe extern "C" fn(
    *const HarukiAssetStudioObjectListRequest,
    *mut HarukiAssetStudioObjectTable,
) -> i32;
type ListObjectsSizeV3Fn = unsafe extern "C" fn(
    *const HarukiAssetStudioObjectListRequest,
    *mut HarukiAssetStudioObjectTable,
) -> i32;
type ListObjectsIntoV3Fn = unsafe extern "C" fn(
    *const HarukiAssetStudioObjectListIntoRequestV3,
    *mut HarukiAssetStudioObjectTable,
) -> i32;
type LookupObjectsV1Fn = unsafe extern "C" fn(
    *const HarukiAssetStudioObjectLookupRequest,
    *mut HarukiAssetStudioObjectTable,
) -> i32;
type LookupObjectsSizeV2Fn = unsafe extern "C" fn(
    *const HarukiAssetStudioObjectLookupRequest,
    *mut HarukiAssetStudioObjectTable,
) -> i32;
type LookupObjectsIntoV2Fn = unsafe extern "C" fn(
    *const HarukiAssetStudioObjectLookupIntoRequestV2,
    *mut HarukiAssetStudioObjectTable,
) -> i32;
type ReadObjectsV3Fn = unsafe extern "C" fn(
    *const HarukiAssetStudioObjectReadBatchRequest,
    *mut HarukiAssetStudioObjectReadBatchResponseV3,
) -> i32;
type ReadObjectsSizeV4Fn = unsafe extern "C" fn(
    *const HarukiAssetStudioObjectReadBatchRequestV4,
    *mut HarukiAssetStudioObjectReadBatchSizeResponseV4,
) -> i32;
type ReadObjectsIntoV4Fn = unsafe extern "C" fn(
    *const HarukiAssetStudioObjectReadBatchIntoRequestV4,
    *mut HarukiAssetStudioObjectReadBatchIntoResponseV4,
) -> i32;
type ResultFreeFn = unsafe extern "C" fn(c_longlong) -> i32;

#[repr(C)]
struct HarukiAssetStudioContextOpenRequest {
    struct_size: c_int,
    input_path_utf8: *const c_uchar,
    input_path_utf8_len: c_int,
    unity_version_utf8: *const c_uchar,
    unity_version_utf8_len: c_int,
    asset_types_csv_utf8: *const c_uchar,
    asset_types_csv_utf8_len: c_int,
    output_dir_utf8: *const c_uchar,
    output_dir_utf8_len: c_int,
    load_all_assets: c_int,
    flags: c_int,
    reserved: c_int,
}

#[repr(C)]
struct HarukiAssetStudioContextOpenResponse {
    struct_size: c_int,
    abi_version: c_int,
    schema_version: c_int,
    context_abi_version: c_int,
    status: c_int,
    error_code: c_int,
    context_id: c_longlong,
    assets_file_count: c_int,
    exportable_asset_count: c_int,
    object_index_count: c_int,
    has_more_assets: c_int,
    unity_version_utf8: *mut c_uchar,
    unity_version_utf8_len: c_int,
    buffer: *mut c_uchar,
    buffer_len: c_longlong,
    duration_ms: c_longlong,
    flags: c_int,
    reserved: c_int,
}

#[repr(C)]
struct HarukiAssetStudioContextCloseRequest {
    struct_size: c_int,
    context_id: c_longlong,
    flags: c_int,
    reserved: c_int,
}

#[repr(C)]
struct HarukiAssetStudioContextCloseResponse {
    struct_size: c_int,
    abi_version: c_int,
    schema_version: c_int,
    context_abi_version: c_int,
    status: c_int,
    error_code: c_int,
    context_id: c_longlong,
    duration_ms: c_longlong,
    flags: c_int,
    reserved: c_int,
}

#[repr(C)]
struct HarukiAssetStudioObjectListRequest {
    struct_size: c_int,
    context_id: c_longlong,
    offset: c_int,
    limit: c_int,
    asset_types_csv_utf8: *const c_uchar,
    asset_types_csv_utf8_len: c_int,
    flags: c_int,
    reserved: c_int,
}

#[repr(C)]
struct HarukiAssetStudioObjectListIntoRequestV3 {
    struct_size: c_int,
    context_id: c_longlong,
    offset: c_int,
    limit: c_int,
    asset_types_csv_utf8: *const c_uchar,
    asset_types_csv_utf8_len: c_int,
    flags: c_int,
    reserved: c_int,
    buffer: *mut c_uchar,
    buffer_len: c_longlong,
}

#[repr(C)]
struct HarukiAssetStudioObjectLookupRequest {
    struct_size: c_int,
    context_id: c_longlong,
    lookup_kind: c_int,
    path_id: c_longlong,
    query_utf8: *const c_uchar,
    query_utf8_len: c_int,
    asset_types_csv_utf8: *const c_uchar,
    asset_types_csv_utf8_len: c_int,
    offset: c_int,
    limit: c_int,
    flags: c_int,
    reserved: c_int,
}

#[repr(C)]
struct HarukiAssetStudioObjectLookupIntoRequestV2 {
    struct_size: c_int,
    context_id: c_longlong,
    lookup_kind: c_int,
    path_id: c_longlong,
    query_utf8: *const c_uchar,
    query_utf8_len: c_int,
    asset_types_csv_utf8: *const c_uchar,
    asset_types_csv_utf8_len: c_int,
    offset: c_int,
    limit: c_int,
    flags: c_int,
    reserved: c_int,
    buffer: *mut c_uchar,
    buffer_len: c_longlong,
}

#[repr(C)]
#[derive(Clone, Copy)]
struct HarukiAssetStudioAssetObject {
    index: c_int,
    type_id: c_int,
    path_id: c_longlong,
    size: c_longlong,
    estimated_payload_capacity: c_longlong,
    raw_payload_capacity: c_longlong,
    image_payload_capacity: c_longlong,
    text_payload_capacity: c_longlong,
    payload_capacity_flags: c_int,
    reserved: c_int,
    name_offset: c_int,
    name_len: c_int,
    container_offset: c_int,
    container_len: c_int,
    type_offset: c_int,
    type_len: c_int,
    unique_id_offset: c_int,
    unique_id_len: c_int,
    source_file_offset: c_int,
    source_file_len: c_int,
}

#[repr(C)]
struct HarukiAssetStudioObjectTable {
    struct_size: c_int,
    abi_version: c_int,
    schema_version: c_int,
    object_table_abi_version: c_int,
    status: c_int,
    error_code: c_int,
    context_id: c_longlong,
    offset: c_int,
    limit: c_int,
    next_offset: c_int,
    has_more: c_int,
    total_count: c_int,
    returned_count: c_int,
    objects: *mut HarukiAssetStudioAssetObject,
    string_data: *mut c_uchar,
    string_data_len: c_int,
    buffer: *mut c_uchar,
    buffer_len: c_longlong,
    duration_ms: c_longlong,
    flags: c_int,
    reserved: c_int,
}

#[repr(C)]
struct HarukiAssetStudioObjectReadItemRequest {
    path_id: c_longlong,
    kind_utf8: *const c_uchar,
    kind_utf8_len: c_int,
    image_format_utf8: *const c_uchar,
    image_format_utf8_len: c_int,
}

#[repr(C)]
struct HarukiAssetStudioObjectReadBatchRequest {
    context_id: c_longlong,
    items: *const HarukiAssetStudioObjectReadItemRequest,
    count: c_int,
    flags: c_int,
}

#[repr(C)]
struct HarukiAssetStudioObjectReadBatchRequestV4 {
    struct_size: c_int,
    context_id: c_longlong,
    items: *const HarukiAssetStudioObjectReadItemRequest,
    count: c_int,
    flags: c_int,
    reserved: c_int,
}

#[repr(C)]
struct HarukiAssetStudioObjectReadBatchIntoRequestV4 {
    struct_size: c_int,
    context_id: c_longlong,
    items: *const HarukiAssetStudioObjectReadItemRequest,
    count: c_int,
    flags: c_int,
    items_buffer: *mut c_uchar,
    items_buffer_len: c_longlong,
    payload: *mut c_uchar,
    payload_len: c_longlong,
    reserved: c_int,
}

#[repr(C)]
struct HarukiAssetStudioObjectReadItemResponse {
    index: c_int,
    status: c_int,
    error_code: c_int,
    path_id: c_longlong,
    type_id: c_int,
    size: c_longlong,
    payload_offset: c_longlong,
    payload_len: c_longlong,
    payload_kind_offset: c_int,
    payload_kind_len: c_int,
    suggested_extension_offset: c_int,
    suggested_extension_len: c_int,
}

#[repr(C)]
struct HarukiAssetStudioObjectReadItemResponseV4 {
    index: c_int,
    status: c_int,
    error_code: c_int,
    path_id: c_longlong,
    type_id: c_int,
    size: c_longlong,
    payload_offset: c_longlong,
    payload_len: c_longlong,
    payload_kind_offset: c_int,
    payload_kind_len: c_int,
    suggested_extension_offset: c_int,
    suggested_extension_len: c_int,
    error_message_offset: c_int,
    error_message_len: c_int,
}

#[repr(C)]
struct HarukiAssetStudioObjectReadBatchResponseV3 {
    abi_version: c_int,
    schema_version: c_int,
    object_read_batch_abi_version: c_int,
    status: c_int,
    error_code: c_int,
    context_id: c_longlong,
    requested_count: c_int,
    returned_count: c_int,
    failed_count: c_int,
    items: *mut HarukiAssetStudioObjectReadItemResponse,
    string_data: *mut c_uchar,
    string_data_len: c_int,
    items_buffer: *mut c_void,
    items_buffer_len: c_longlong,
    payload: *mut c_void,
    payload_len: c_longlong,
    duration_ms: c_longlong,
    object_read_batch_handle_abi_version: c_int,
    result_handle: c_longlong,
}

#[repr(C)]
struct HarukiAssetStudioObjectReadBatchSizeResponseV4 {
    struct_size: c_int,
    abi_version: c_int,
    schema_version: c_int,
    object_read_batch_abi_version: c_int,
    object_read_batch_into_abi_version: c_int,
    status: c_int,
    error_code: c_int,
    context_id: c_longlong,
    requested_count: c_int,
    returned_count: c_int,
    failed_count: c_int,
    required_items_buffer_len: c_longlong,
    required_string_data_len: c_int,
    required_payload_len: c_longlong,
    items_buffer_len: c_longlong,
    string_data_len: c_int,
    payload_len: c_longlong,
    duration_ms: c_longlong,
    flags: c_int,
    reserved: c_int,
}

#[repr(C)]
struct HarukiAssetStudioObjectReadBatchIntoResponseV4 {
    struct_size: c_int,
    abi_version: c_int,
    schema_version: c_int,
    object_read_batch_abi_version: c_int,
    object_read_batch_into_abi_version: c_int,
    status: c_int,
    error_code: c_int,
    context_id: c_longlong,
    requested_count: c_int,
    returned_count: c_int,
    failed_count: c_int,
    items: *mut HarukiAssetStudioObjectReadItemResponseV4,
    string_data: *mut c_uchar,
    string_data_len: c_int,
    items_buffer: *mut c_uchar,
    items_buffer_len: c_longlong,
    payload: *mut c_uchar,
    payload_len: c_longlong,
    required_items_buffer_len: c_longlong,
    required_string_data_len: c_int,
    required_payload_len: c_longlong,
    duration_ms: c_longlong,
    flags: c_int,
    reserved: c_int,
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let mut args = std::env::args().skip(1);
    let library_path = args.next().expect("native library path");
    let input_path = args.next().expect("asset input path");
    let unity_version = args.next().unwrap_or_else(|| "2022.3.62f1".to_string());

    unsafe {
        let library = Library::new(library_path)?;
        let capabilities: Symbol<NoRequestJsonFn> = library.get(b"haruki_assetstudio_capabilities")?;
        let open_v2: Symbol<ContextOpenV2Fn> =
            library.get(b"haruki_assetstudio_context_open_v2")?;
        let list_size_v3: Symbol<ListObjectsSizeV3Fn> =
            library.get(b"haruki_assetstudio_context_list_objects_size_v3")?;
        let list_into_v3: Symbol<ListObjectsIntoV3Fn> =
            library.get(b"haruki_assetstudio_context_list_objects_into_v3")?;
        let lookup_size_v2: Symbol<LookupObjectsSizeV2Fn> =
            library.get(b"haruki_assetstudio_context_lookup_objects_size_v2")?;
        let lookup_into_v2: Symbol<LookupObjectsIntoV2Fn> =
            library.get(b"haruki_assetstudio_context_lookup_objects_into_v2")?;
        let read_objects_v3: Symbol<ReadObjectsV3Fn> =
            library.get(b"haruki_assetstudio_context_read_objects_v3")?;
        let read_objects_size_v4: Symbol<ReadObjectsSizeV4Fn> =
            library.get(b"haruki_assetstudio_context_read_objects_size_v4")?;
        let read_objects_into_v4: Symbol<ReadObjectsIntoV4Fn> =
            library.get(b"haruki_assetstudio_context_read_objects_into_v4")?;
        let close_v2: Symbol<ContextCloseV2Fn> =
            library.get(b"haruki_assetstudio_context_close_v2")?;
        let free_string: Symbol<FreeStringFn> = library.get(b"haruki_assetstudio_free_string")?;
        let free_buffer: Symbol<FreeBufferFn> = library.get(b"haruki_assetstudio_free_buffer")?;
        let result_free: Symbol<ResultFreeFn> = library.get(b"haruki_assetstudio_result_free")?;

        let (_, caps) = call_json_no_request(*capabilities, *free_string)?;
        assert_eq!(caps["ffi_mode"], "core");
        assert_eq!(caps["legacy_static_engine"], false);
        assert_eq!(caps["native_console_capture"], false);
        assert!(caps["max_active_contexts"].as_i64().unwrap_or_default() >= 1);
        assert_eq!(caps["supports_caller_provided_object_table_buffers"], true);
        assert_eq!(caps["supports_caller_provided_read_buffers"], true);
        assert_eq!(caps["supports_payload_kind_capacity_hints"], true);
        assert_eq!(caps["supports_direct_object_read_retry"], true);
        assert_eq!(caps["supports_typed_item_error_messages"], true);
        assert_eq!(caps["supports_typed_object_lookup"], true);
        assert_eq!(caps["supports_caller_provided_object_lookup_buffers"], true);
        assert!(caps["max_native_utf8_bytes"].as_i64().unwrap_or_default() >= 1024);
        assert!(caps["max_object_read_batch_count"].as_i64().unwrap_or_default() >= 1);
        println!("capabilities: {}", caps);

        let open_response = call_context_open_v2(
            *open_v2,
            *free_buffer,
            input_path.as_bytes(),
            unity_version.as_bytes(),
        )?;
        let context_id = open_response.context_id;
        println!(
            "opened context {}, indexed {} objects",
            context_id, open_response.object_index_count
        );

        let typed_assets = call_list_objects_v3(*list_size_v3, *list_into_v3, context_id, 0, 8, None)?;
        println!("typed listed {} objects", typed_assets.len());
        if let Some(first) = typed_assets.first() {
            println!(
                "first typed object path_id={} type={}",
                first.path_id,
                native_string(&first.string_data, first.object.type_offset, first.object.type_len)?
            );
            let lookup_assets = call_lookup_objects_v2(
                *lookup_size_v2,
                *lookup_into_v2,
                context_id,
                1,
                first.path_id,
                None,
                0,
                4,
            )?;
            assert_eq!(lookup_assets.len(), 1);
            assert_eq!(lookup_assets[0].path_id, first.path_id);
            println!("lookup v1 matched path_id={}", first.path_id);
        }

        let typed_read_assets =
            call_list_objects_v3(*list_size_v3, *list_into_v3, context_id, 0, 1, Some("TextAsset"))?;
        let typed_read_asset = typed_read_assets.first().or_else(|| typed_assets.first());
        if let Some(first) = typed_read_asset {
            let response = call_read_objects_v4(
                *read_objects_size_v4,
                *read_objects_into_v4,
                context_id,
                first.path_id,
                "auto",
                "bmp",
            )?;
            println!(
                "typed v4 read {} objects, {} bytes, into abi {}",
                response.returned_count,
                response.payload_len,
                response.object_read_batch_into_abi_version
            );

            let v3_response = call_read_objects_v3(
                *read_objects_v3,
                *result_free,
                context_id,
                first.path_id,
                "auto",
                "bmp",
            )?;
            println!(
                "compat typed v3 read {} objects, {} bytes, handle abi {}",
                v3_response.returned_count,
                v3_response.payload_len,
                v3_response.object_read_batch_handle_abi_version
            );
        }

        call_context_close_v2(*close_v2, context_id)?;
        println!("closed context {}", context_id);
    }

    Ok(())
}

struct OwnedAssetObject {
    object: HarukiAssetStudioAssetObject,
    path_id: c_longlong,
    string_data: Vec<u8>,
}

unsafe fn call_json_no_request(
    function: NoRequestJsonFn,
    free_string: FreeStringFn,
) -> Result<(i32, Value), Box<dyn std::error::Error>> {
    let mut response = std::ptr::null_mut();
    let code = unsafe { function(&mut response) };
    let json = unsafe { take_json(response, free_string)? };
    Ok((code, serde_json::from_str(&json)?))
}

unsafe fn call_context_open_v2(
    function: ContextOpenV2Fn,
    free_buffer: FreeBufferFn,
    input_path: &[u8],
    unity_version: &[u8],
) -> Result<HarukiAssetStudioContextOpenResponse, Box<dyn std::error::Error>> {
    let request = HarukiAssetStudioContextOpenRequest {
        struct_size: std::mem::size_of::<HarukiAssetStudioContextOpenRequest>() as c_int,
        input_path_utf8: input_path.as_ptr(),
        input_path_utf8_len: input_path.len() as c_int,
        unity_version_utf8: unity_version.as_ptr(),
        unity_version_utf8_len: unity_version.len() as c_int,
        asset_types_csv_utf8: std::ptr::null(),
        asset_types_csv_utf8_len: 0,
        output_dir_utf8: std::ptr::null(),
        output_dir_utf8_len: 0,
        load_all_assets: 0,
        flags: 0,
        reserved: 0,
    };
    let mut response = unsafe { std::mem::zeroed::<HarukiAssetStudioContextOpenResponse>() };
    let code = unsafe { function(&request, &mut response) };
    if code != 0 || response.status != 0 {
        if !response.buffer.is_null() {
            unsafe { free_buffer(response.buffer) };
        }
        return Err(format!(
            "context_open_v2 failed: rc={} status={} error_code={}",
            code, response.status, response.error_code
        )
        .into());
    }
    if !response.buffer.is_null() {
        unsafe { free_buffer(response.buffer) };
        response.buffer = std::ptr::null_mut();
        response.unity_version_utf8 = std::ptr::null_mut();
        response.unity_version_utf8_len = 0;
    }
    Ok(response)
}

unsafe fn call_list_objects_v2(
    function: ListObjectsV2Fn,
    free_buffer: FreeBufferFn,
    context_id: c_longlong,
    offset: c_int,
    limit: c_int,
    asset_types_csv: Option<&str>,
) -> Result<Vec<OwnedAssetObject>, Box<dyn std::error::Error>> {
    let asset_types = asset_types_csv.map(|value| value.as_bytes().to_vec());
    let (asset_types_ptr, asset_types_len) = match asset_types.as_ref() {
        Some(bytes) => (bytes.as_ptr(), bytes.len() as c_int),
        None => (std::ptr::null(), 0),
    };
    let request = HarukiAssetStudioObjectListRequest {
        struct_size: std::mem::size_of::<HarukiAssetStudioObjectListRequest>() as c_int,
        context_id,
        offset,
        limit,
        asset_types_csv_utf8: asset_types_ptr,
        asset_types_csv_utf8_len: asset_types_len,
        flags: 0,
        reserved: 0,
    };
    let mut table = unsafe { std::mem::zeroed::<HarukiAssetStudioObjectTable>() };
    let code = unsafe { function(&request, &mut table) };
    if code != 0 || table.status != 0 {
        return Err(format!(
            "list_objects_v2 failed: rc={} status={} error_code={}",
            code, table.status, table.error_code
        )
        .into());
    }

    let string_data = if table.string_data.is_null() || table.string_data_len <= 0 {
        Vec::new()
    } else {
        unsafe { std::slice::from_raw_parts(table.string_data, table.string_data_len as usize) }.to_vec()
    };
    let objects = if table.objects.is_null() || table.returned_count <= 0 {
        Vec::new()
    } else {
        unsafe { std::slice::from_raw_parts(table.objects, table.returned_count as usize) }
            .iter()
            .map(|object| OwnedAssetObject {
                object: *object,
                path_id: object.path_id,
                string_data: string_data.clone(),
            })
            .collect()
    };
    if !table.buffer.is_null() {
        unsafe { free_buffer(table.buffer) };
    }
    Ok(objects)
}

unsafe fn call_list_objects_v3(
    size_function: ListObjectsSizeV3Fn,
    into_function: ListObjectsIntoV3Fn,
    context_id: c_longlong,
    offset: c_int,
    limit: c_int,
    asset_types_csv: Option<&str>,
) -> Result<Vec<OwnedAssetObject>, Box<dyn std::error::Error>> {
    let asset_types = asset_types_csv.map(|value| value.as_bytes().to_vec());
    let (asset_types_ptr, asset_types_len) = match asset_types.as_ref() {
        Some(bytes) => (bytes.as_ptr(), bytes.len() as c_int),
        None => (std::ptr::null(), 0),
    };
    let request = HarukiAssetStudioObjectListRequest {
        struct_size: std::mem::size_of::<HarukiAssetStudioObjectListRequest>() as c_int,
        context_id,
        offset,
        limit,
        asset_types_csv_utf8: asset_types_ptr,
        asset_types_csv_utf8_len: asset_types_len,
        flags: 0,
        reserved: 0,
    };
    let mut size_table = unsafe { std::mem::zeroed::<HarukiAssetStudioObjectTable>() };
    let size_code = unsafe { size_function(&request, &mut size_table) };
    if size_code != 0 || size_table.status != 0 {
        return Err(format!(
            "list_objects_size_v3 failed: rc={} status={} error_code={}",
            size_code, size_table.status, size_table.error_code
        )
        .into());
    }

    let mut buffer = vec![0u8; size_table.buffer_len.max(0) as usize];
    let into_request = HarukiAssetStudioObjectListIntoRequestV3 {
        struct_size: std::mem::size_of::<HarukiAssetStudioObjectListIntoRequestV3>() as c_int,
        context_id,
        offset,
        limit,
        asset_types_csv_utf8: asset_types_ptr,
        asset_types_csv_utf8_len: asset_types_len,
        flags: 0,
        reserved: 0,
        buffer: if buffer.is_empty() {
            std::ptr::null_mut()
        } else {
            buffer.as_mut_ptr()
        },
        buffer_len: buffer.len() as c_longlong,
    };
    let mut table = unsafe { std::mem::zeroed::<HarukiAssetStudioObjectTable>() };
    let into_code = unsafe { into_function(&into_request, &mut table) };
    if into_code != 0 || table.status != 0 {
        return Err(format!(
            "list_objects_into_v3 failed: rc={} status={} error_code={} required_buffer_len={}",
            into_code, table.status, table.error_code, table.buffer_len
        )
        .into());
    }

    let string_data = if table.string_data.is_null() || table.string_data_len <= 0 {
        Vec::new()
    } else {
        unsafe { std::slice::from_raw_parts(table.string_data, table.string_data_len as usize) }.to_vec()
    };
    let objects = if table.objects.is_null() || table.returned_count <= 0 {
        Vec::new()
    } else {
        unsafe { std::slice::from_raw_parts(table.objects, table.returned_count as usize) }
            .iter()
            .map(|object| OwnedAssetObject {
                object: *object,
                path_id: object.path_id,
                string_data: string_data.clone(),
            })
            .collect()
    };
    Ok(objects)
}

unsafe fn call_lookup_objects_v1(
    function: LookupObjectsV1Fn,
    free_buffer: FreeBufferFn,
    context_id: c_longlong,
    lookup_kind: c_int,
    path_id: c_longlong,
    query: Option<&str>,
    offset: c_int,
    limit: c_int,
) -> Result<Vec<OwnedAssetObject>, Box<dyn std::error::Error>> {
    let query_bytes = query.map(|value| value.as_bytes().to_vec());
    let (query_ptr, query_len) = match query_bytes.as_ref() {
        Some(bytes) => (bytes.as_ptr(), bytes.len() as c_int),
        None => (std::ptr::null(), 0),
    };
    let request = HarukiAssetStudioObjectLookupRequest {
        struct_size: std::mem::size_of::<HarukiAssetStudioObjectLookupRequest>() as c_int,
        context_id,
        lookup_kind,
        path_id,
        query_utf8: query_ptr,
        query_utf8_len: query_len,
        asset_types_csv_utf8: std::ptr::null(),
        asset_types_csv_utf8_len: 0,
        offset,
        limit,
        flags: 0,
        reserved: 0,
    };
    let mut table = unsafe { std::mem::zeroed::<HarukiAssetStudioObjectTable>() };
    let code = unsafe { function(&request, &mut table) };
    if code != 0 || table.status != 0 {
        return Err(format!(
            "lookup_objects_v1 failed: rc={} status={} error_code={}",
            code, table.status, table.error_code
        )
        .into());
    }

    let string_data = if table.string_data.is_null() || table.string_data_len <= 0 {
        Vec::new()
    } else {
        unsafe { std::slice::from_raw_parts(table.string_data, table.string_data_len as usize) }.to_vec()
    };
    let objects = if table.objects.is_null() || table.returned_count <= 0 {
        Vec::new()
    } else {
        unsafe { std::slice::from_raw_parts(table.objects, table.returned_count as usize) }
            .iter()
            .map(|object| OwnedAssetObject {
                object: *object,
                path_id: object.path_id,
                string_data: string_data.clone(),
            })
            .collect()
    };
    if !table.buffer.is_null() {
        unsafe { free_buffer(table.buffer) };
    }
    Ok(objects)
}

unsafe fn call_lookup_objects_v2(
    size_function: LookupObjectsSizeV2Fn,
    into_function: LookupObjectsIntoV2Fn,
    context_id: c_longlong,
    lookup_kind: c_int,
    path_id: c_longlong,
    query: Option<&str>,
    offset: c_int,
    limit: c_int,
) -> Result<Vec<OwnedAssetObject>, Box<dyn std::error::Error>> {
    let query_bytes = query.map(|value| value.as_bytes().to_vec());
    let (query_ptr, query_len) = match query_bytes.as_ref() {
        Some(bytes) => (bytes.as_ptr(), bytes.len() as c_int),
        None => (std::ptr::null(), 0),
    };
    let request = HarukiAssetStudioObjectLookupRequest {
        struct_size: std::mem::size_of::<HarukiAssetStudioObjectLookupRequest>() as c_int,
        context_id,
        lookup_kind,
        path_id,
        query_utf8: query_ptr,
        query_utf8_len: query_len,
        asset_types_csv_utf8: std::ptr::null(),
        asset_types_csv_utf8_len: 0,
        offset,
        limit,
        flags: 0,
        reserved: 0,
    };
    let mut size_table = unsafe { std::mem::zeroed::<HarukiAssetStudioObjectTable>() };
    let size_code = unsafe { size_function(&request, &mut size_table) };
    if size_code != 0 || size_table.status != 0 {
        return Err(format!(
            "lookup_objects_size_v2 failed: rc={} status={} error_code={}",
            size_code, size_table.status, size_table.error_code
        )
        .into());
    }

    let mut buffer = vec![0u8; size_table.buffer_len.max(0) as usize];
    let into_request = HarukiAssetStudioObjectLookupIntoRequestV2 {
        struct_size: std::mem::size_of::<HarukiAssetStudioObjectLookupIntoRequestV2>() as c_int,
        context_id,
        lookup_kind,
        path_id,
        query_utf8: query_ptr,
        query_utf8_len: query_len,
        asset_types_csv_utf8: std::ptr::null(),
        asset_types_csv_utf8_len: 0,
        offset,
        limit,
        flags: 0,
        reserved: 0,
        buffer: if buffer.is_empty() {
            std::ptr::null_mut()
        } else {
            buffer.as_mut_ptr()
        },
        buffer_len: buffer.len() as c_longlong,
    };
    let mut table = unsafe { std::mem::zeroed::<HarukiAssetStudioObjectTable>() };
    let into_code = unsafe { into_function(&into_request, &mut table) };
    if into_code != 0 || table.status != 0 {
        return Err(format!(
            "lookup_objects_into_v2 failed: rc={} status={} error_code={} required_buffer_len={}",
            into_code, table.status, table.error_code, table.buffer_len
        )
        .into());
    }

    let string_data = if table.string_data.is_null() || table.string_data_len <= 0 {
        Vec::new()
    } else {
        unsafe { std::slice::from_raw_parts(table.string_data, table.string_data_len as usize) }.to_vec()
    };
    let objects = if table.objects.is_null() || table.returned_count <= 0 {
        Vec::new()
    } else {
        unsafe { std::slice::from_raw_parts(table.objects, table.returned_count as usize) }
            .iter()
            .map(|object| OwnedAssetObject {
                object: *object,
                path_id: object.path_id,
                string_data: string_data.clone(),
            })
            .collect()
    };
    Ok(objects)
}

unsafe fn call_read_objects_v4(
    size_function: ReadObjectsSizeV4Fn,
    into_function: ReadObjectsIntoV4Fn,
    context_id: c_longlong,
    path_id: c_longlong,
    kind: &str,
    image_format: &str,
) -> Result<HarukiAssetStudioObjectReadBatchIntoResponseV4, Box<dyn std::error::Error>> {
    let kind = kind.as_bytes();
    let image_format = image_format.as_bytes();
    let item = HarukiAssetStudioObjectReadItemRequest {
        path_id,
        kind_utf8: kind.as_ptr(),
        kind_utf8_len: kind.len() as c_int,
        image_format_utf8: image_format.as_ptr(),
        image_format_utf8_len: image_format.len() as c_int,
    };
    let size_request = HarukiAssetStudioObjectReadBatchRequestV4 {
        struct_size: std::mem::size_of::<HarukiAssetStudioObjectReadBatchRequestV4>() as c_int,
        context_id,
        items: &item,
        count: 1,
        flags: 0,
        reserved: 0,
    };
    let mut size_response =
        unsafe { std::mem::zeroed::<HarukiAssetStudioObjectReadBatchSizeResponseV4>() };
    let size_code = unsafe { size_function(&size_request, &mut size_response) };
    if size_code != 0 || size_response.status != 0 {
        return Err(format!(
            "read_objects_size_v4 failed: rc={} status={} error_code={}",
            size_code, size_response.status, size_response.error_code
        )
        .into());
    }

    let mut items_buffer = vec![0u8; size_response.required_items_buffer_len as usize];
    let mut payload = vec![0u8; size_response.required_payload_len as usize];
    let into_request = HarukiAssetStudioObjectReadBatchIntoRequestV4 {
        struct_size: std::mem::size_of::<HarukiAssetStudioObjectReadBatchIntoRequestV4>() as c_int,
        context_id,
        items: &item,
        count: 1,
        flags: 0,
        items_buffer: items_buffer.as_mut_ptr(),
        items_buffer_len: items_buffer.len() as c_longlong,
        payload: payload.as_mut_ptr(),
        payload_len: payload.len() as c_longlong,
        reserved: 0,
    };
    let mut into_response =
        unsafe { std::mem::zeroed::<HarukiAssetStudioObjectReadBatchIntoResponseV4>() };
    let into_code = unsafe { into_function(&into_request, &mut into_response) };
    if into_code != 0 || into_response.status != 0 {
        return Err(format!(
            "read_objects_into_v4 failed: rc={} status={} error_code={}",
            into_code, into_response.status, into_response.error_code
        )
        .into());
    }
    if into_response.payload_len != payload.len() as c_longlong {
        return Err("read_objects_into_v4 payload length mismatch".into());
    }
    Ok(into_response)
}

unsafe fn call_read_objects_v3(
    function: ReadObjectsV3Fn,
    result_free: ResultFreeFn,
    context_id: c_longlong,
    path_id: c_longlong,
    kind: &str,
    image_format: &str,
) -> Result<HarukiAssetStudioObjectReadBatchResponseV3, Box<dyn std::error::Error>> {
    let kind = kind.as_bytes();
    let image_format = image_format.as_bytes();
    let item = HarukiAssetStudioObjectReadItemRequest {
        path_id,
        kind_utf8: kind.as_ptr(),
        kind_utf8_len: kind.len() as c_int,
        image_format_utf8: image_format.as_ptr(),
        image_format_utf8_len: image_format.len() as c_int,
    };
    let request = HarukiAssetStudioObjectReadBatchRequest {
        context_id,
        items: &item,
        count: 1,
        flags: 0,
    };
    let mut response = unsafe { std::mem::zeroed::<HarukiAssetStudioObjectReadBatchResponseV3>() };
    let code = unsafe { function(&request, &mut response) };
    if code != 0 || response.status != 0 {
        if response.result_handle > 0 {
            unsafe { result_free(response.result_handle) };
        }
        return Err(format!(
            "read_objects_v3 failed: rc={} status={} error_code={}",
            code, response.status, response.error_code
        )
        .into());
    }
    if response.result_handle <= 0 {
        return Err("read_objects_v3 did not return a result handle".into());
    }
    let free_code = unsafe { result_free(response.result_handle) };
    if free_code != 0 {
        return Err(format!("result_free failed: rc={}", free_code).into());
    }
    Ok(response)
}

unsafe fn call_context_close_v2(
    function: ContextCloseV2Fn,
    context_id: c_longlong,
) -> Result<(), Box<dyn std::error::Error>> {
    let request = HarukiAssetStudioContextCloseRequest {
        struct_size: std::mem::size_of::<HarukiAssetStudioContextCloseRequest>() as c_int,
        context_id,
        flags: 0,
        reserved: 0,
    };
    let mut response = unsafe { std::mem::zeroed::<HarukiAssetStudioContextCloseResponse>() };
    let code = unsafe { function(&request, &mut response) };
    if code != 0 || response.status != 0 {
        return Err(format!(
            "context_close_v2 failed: rc={} status={} error_code={}",
            code, response.status, response.error_code
        )
        .into());
    }
    Ok(())
}

unsafe fn take_json(
    response: *mut c_char,
    free_string: FreeStringFn,
) -> Result<String, Box<dyn std::error::Error>> {
    if response.is_null() {
        return Ok(String::new());
    }
    let json = unsafe { CStr::from_ptr(response) }.to_string_lossy().into_owned();
    unsafe { free_string(response) };
    Ok(json)
}

fn native_string(
    string_data: &[u8],
    offset: c_int,
    length: c_int,
) -> Result<&str, Box<dyn std::error::Error>> {
    if length <= 0 {
        return Ok("");
    }
    let start = offset as usize;
    let end = start + length as usize;
    Ok(std::str::from_utf8(&string_data[start..end])?)
}
