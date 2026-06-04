use libloading::Library;
use serde::Deserialize;
use std::collections::HashMap;
use std::ffi::{CStr, CString};
use std::mem::size_of;
use std::os::raw::{c_char, c_int, c_longlong, c_uchar};
use std::path::Path;
use std::ptr;
use std::sync::Arc;

pub type Result<T> = std::result::Result<T, Error>;

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("native library error: {0}")]
    Library(#[from] libloading::Error),
    #[error("json error: {0}")]
    Json(#[from] serde_json::Error),
    #[error("nul byte in string: {0}")]
    Nul(#[from] std::ffi::NulError),
    #[error("ABI layout mismatch for {name}: native={native}, rust={rust}")]
    LayoutMismatch {
        name: &'static str,
        native: usize,
        rust: usize,
    },
    #[error("native status {status}: {detail}")]
    Native { status: i32, detail: String },
}

type JsonFn = unsafe extern "C" fn(*mut *mut c_char) -> i32;
type FreeStringFn = unsafe extern "C" fn(*mut c_char);
type FreeBufferFn = unsafe extern "C" fn(*mut c_uchar);
type ResultFreeFn = unsafe extern "C" fn(c_longlong) -> i32;
type OpenFn = unsafe extern "C" fn(*const ContextOpenRequest, *mut ContextOpenResponse) -> i32;
type CloseFn = unsafe extern "C" fn(*const ContextCloseRequest, *mut ContextCloseResponse) -> i32;
type ListSizeFn = unsafe extern "C" fn(*const ObjectListRequest, *mut ObjectTable) -> i32;
type ListIntoFn = unsafe extern "C" fn(*const ObjectListIntoRequest, *mut ObjectTable) -> i32;
type LookupSizeFn = unsafe extern "C" fn(*const ObjectLookupRequest, *mut ObjectTable) -> i32;
type LookupIntoFn = unsafe extern "C" fn(*const ObjectLookupIntoRequest, *mut ObjectTable) -> i32;
type ReadByPathRetryFn =
    unsafe extern "C" fn(*const ObjectReadBatchIntoRequest, *mut ObjectReadBatchRetryResponse) -> i32;
type ReadByIndexRetryFn =
    unsafe extern "C" fn(*const ObjectReadBatchByIndexIntoRequest, *mut ObjectReadBatchRetryResponse) -> i32;

pub struct AssetStudioLibrary {
    inner: Arc<Native>,
}

struct Native {
    _library: Library,
    capabilities: JsonFn,
    abi_layout: JsonFn,
    free_string: FreeStringFn,
    free_buffer: FreeBufferFn,
    result_free: ResultFreeFn,
    open: OpenFn,
    close: CloseFn,
    list_size: ListSizeFn,
    list_into: ListIntoFn,
    lookup_size: LookupSizeFn,
    lookup_into: LookupIntoFn,
    read_by_path_retry: ReadByPathRetryFn,
    read_by_index_retry: ReadByIndexRetryFn,
}

impl AssetStudioLibrary {
    pub fn load(path: impl AsRef<Path>) -> Result<Self> {
        unsafe {
            let library = Library::new(path.as_ref())?;
            let capabilities = *library.get::<JsonFn>(b"haruki_assetstudio_capabilities")?;
            let abi_layout = *library.get::<JsonFn>(b"haruki_assetstudio_abi_layout")?;
            let free_string = *library.get::<FreeStringFn>(b"haruki_assetstudio_free_string")?;
            let free_buffer = *library.get::<FreeBufferFn>(b"haruki_assetstudio_free_buffer")?;
            let result_free = *library.get::<ResultFreeFn>(b"haruki_assetstudio_result_free")?;
            let open = *library.get::<OpenFn>(b"haruki_assetstudio_context_open_v2")?;
            let close = *library.get::<CloseFn>(b"haruki_assetstudio_context_close_v2")?;
            let list_size = *library.get::<ListSizeFn>(b"haruki_assetstudio_context_list_objects_size_v3")?;
            let list_into = *library.get::<ListIntoFn>(b"haruki_assetstudio_context_list_objects_into_v3")?;
            let lookup_size = *library.get::<LookupSizeFn>(b"haruki_assetstudio_context_lookup_objects_size_v2")?;
            let lookup_into = *library.get::<LookupIntoFn>(b"haruki_assetstudio_context_lookup_objects_into_v2")?;
            let read_by_path_retry =
                *library.get::<ReadByPathRetryFn>(b"haruki_assetstudio_context_read_objects_direct_retry_v7")?;
            let read_by_index_retry = *library
                .get::<ReadByIndexRetryFn>(b"haruki_assetstudio_context_read_objects_by_index_direct_retry_v7")?;
            let native = Arc::new(Native {
                _library: library,
                capabilities,
                abi_layout,
                free_string,
                free_buffer,
                result_free,
                open,
                close,
                list_size,
                list_into,
                lookup_size,
                lookup_into,
                read_by_path_retry,
                read_by_index_retry,
            });
            native.verify_layout()?;
            Ok(Self { inner: native })
        }
    }

    pub fn open(
        &self,
        input_path: &str,
        unity_version: Option<&str>,
        asset_types: &[&str],
        load_all_assets: bool,
    ) -> Result<Context> {
        let input_path = CString::new(input_path)?;
        let unity_version = optional_cstring(unity_version)?;
        let asset_types_csv = CString::new(asset_types.join(","))?;
        let request = ContextOpenRequest {
            struct_size: size_of::<ContextOpenRequest>() as c_int,
            input_path_utf8: input_path.as_ptr() as *const c_uchar,
            input_path_utf8_len: input_path.as_bytes().len() as c_int,
            unity_version_utf8: unity_version
                .as_ref()
                .map_or(ptr::null(), |value| value.as_ptr() as *const c_uchar),
            unity_version_utf8_len: unity_version.as_ref().map_or(0, |value| value.as_bytes().len() as c_int),
            asset_types_csv_utf8: asset_types_csv.as_ptr() as *const c_uchar,
            asset_types_csv_utf8_len: asset_types_csv.as_bytes().len() as c_int,
            output_dir_utf8: ptr::null(),
            output_dir_utf8_len: 0,
            load_all_assets: load_all_assets as c_int,
            flags: 0,
            reserved: 0,
        };
        let mut response = ContextOpenResponse::default();
        let status = unsafe { (self.inner.open)(&request, &mut response) };
        if status != 0 || response.status != 0 {
            return Err(native_error(status, response.status, response.error_code));
        }
        if !response.buffer.is_null() {
            unsafe { (self.inner.free_buffer)(response.buffer) };
        }
        Ok(Context {
            native: Arc::clone(&self.inner),
            context_id: response.context_id,
        })
    }

    pub fn capabilities(&self) -> Result<Capabilities> {
        self.inner.capabilities()
    }
}

impl Native {
    fn capabilities(&self) -> Result<Capabilities> {
        let json = unsafe { call_json(self.capabilities, self.free_string)? };
        Ok(serde_json::from_str(&json)?)
    }

    fn verify_layout(&self) -> Result<()> {
        let json = unsafe { call_json(self.abi_layout, self.free_string)? };
        let layout: AbiLayout = serde_json::from_str(&json)?;
        check_size::<ContextOpenRequest>(&layout, "haruki_assetstudio_context_open_request")?;
        check_size::<ContextOpenResponse>(&layout, "haruki_assetstudio_context_open_response")?;
        check_size::<ObjectTable>(&layout, "haruki_assetstudio_object_table")?;
        check_size::<AssetObject>(&layout, "haruki_assetstudio_asset_object")?;
        check_size::<ObjectLookupRequest>(&layout, "haruki_assetstudio_object_lookup_request")?;
        check_size::<ObjectLookupIntoRequest>(&layout, "haruki_assetstudio_object_lookup_into_request_v2")?;
        check_size::<ObjectReadBatchIntoRequest>(&layout, "haruki_assetstudio_object_read_batch_into_request_v4")?;
        check_size::<ObjectReadBatchByIndexIntoRequest>(&layout, "haruki_assetstudio_object_read_batch_by_index_into_request_v5")?;
        check_size::<ObjectReadBatchRetryResponse>(&layout, "haruki_assetstudio_object_read_batch_retry_response_v7")?;
        Ok(())
    }
}

pub struct Context {
    native: Arc<Native>,
    context_id: i64,
}

impl Context {
    pub fn id(&self) -> i64 {
        self.context_id
    }

    pub fn list_objects(&self, offset: i32, limit: i32, asset_types: &[&str]) -> Result<Vec<AssetInfo>> {
        let asset_types_csv = CString::new(asset_types.join(","))?;
        let request = ObjectListRequest {
            struct_size: size_of::<ObjectListRequest>() as c_int,
            context_id: self.context_id,
            offset,
            limit,
            asset_types_csv_utf8: asset_types_csv.as_ptr() as *const c_uchar,
            asset_types_csv_utf8_len: asset_types_csv.as_bytes().len() as c_int,
            flags: 0,
            reserved: 0,
        };
        let mut size_response = ObjectTable::default();
        let status = unsafe { (self.native.list_size)(&request, &mut size_response) };
        if status != 0 || size_response.status != 0 {
            return Err(native_error(status, size_response.status, size_response.error_code));
        }
        let mut buffer = vec![0u8; size_response.buffer_len as usize];
        let into_request = ObjectListIntoRequest {
            struct_size: size_of::<ObjectListIntoRequest>() as c_int,
            context_id: self.context_id,
            offset,
            limit,
            asset_types_csv_utf8: asset_types_csv.as_ptr() as *const c_uchar,
            asset_types_csv_utf8_len: asset_types_csv.as_bytes().len() as c_int,
            flags: 0,
            reserved: 0,
            buffer: buffer.as_mut_ptr(),
            buffer_len: buffer.len() as c_longlong,
        };
        let mut response = ObjectTable::default();
        let status = unsafe { (self.native.list_into)(&into_request, &mut response) };
        if status != 0 || response.status != 0 {
            return Err(native_error(status, response.status, response.error_code));
        }
        Ok(read_asset_infos(&response))
    }

    pub fn lookup_objects(&self, request: ObjectLookupRequestOptions<'_>) -> Result<Vec<AssetInfo>> {
        let query = optional_cstring(request.query)?;
        let asset_types_csv = CString::new(request.asset_types.join(","))?;
        let lookup_request = ObjectLookupRequest {
            struct_size: size_of::<ObjectLookupRequest>() as c_int,
            context_id: self.context_id,
            lookup_kind: request.kind as c_int,
            path_id: request.path_id.unwrap_or(0),
            query_utf8: query
                .as_ref()
                .map_or(ptr::null(), |value| value.as_ptr() as *const c_uchar),
            query_utf8_len: query.as_ref().map_or(0, |value| value.as_bytes().len() as c_int),
            asset_types_csv_utf8: asset_types_csv.as_ptr() as *const c_uchar,
            asset_types_csv_utf8_len: asset_types_csv.as_bytes().len() as c_int,
            offset: request.offset,
            limit: request.limit,
            flags: if request.contains { 1 } else { 0 },
            reserved: 0,
        };
        let mut size_response = ObjectTable::default();
        let status = unsafe { (self.native.lookup_size)(&lookup_request, &mut size_response) };
        if status != 0 || size_response.status != 0 {
            return Err(native_error(status, size_response.status, size_response.error_code));
        }
        let mut buffer = vec![0u8; size_response.buffer_len as usize];
        let into_request = ObjectLookupIntoRequest {
            struct_size: size_of::<ObjectLookupIntoRequest>() as c_int,
            context_id: lookup_request.context_id,
            lookup_kind: lookup_request.lookup_kind,
            path_id: lookup_request.path_id,
            query_utf8: lookup_request.query_utf8,
            query_utf8_len: lookup_request.query_utf8_len,
            asset_types_csv_utf8: lookup_request.asset_types_csv_utf8,
            asset_types_csv_utf8_len: lookup_request.asset_types_csv_utf8_len,
            offset: lookup_request.offset,
            limit: lookup_request.limit,
            flags: lookup_request.flags,
            reserved: 0,
            buffer: buffer.as_mut_ptr(),
            buffer_len: buffer.len() as c_longlong,
        };
        let mut response = ObjectTable::default();
        let status = unsafe { (self.native.lookup_into)(&into_request, &mut response) };
        if status != 0 || response.status != 0 {
            return Err(native_error(status, response.status, response.error_code));
        }
        Ok(read_asset_infos(&response))
    }

    pub fn read_by_path_id_retry(&self, requests: &[ObjectReadByPathIdRequest<'_>]) -> Result<ObjectReadResult> {
        let mut kinds = Vec::with_capacity(requests.len());
        let mut formats = Vec::with_capacity(requests.len());
        let mut items = Vec::with_capacity(requests.len());
        for request in requests {
            let kind = CString::new(request.kind)?;
            let format = CString::new(request.image_format)?;
            items.push(ObjectReadItemRequest {
                path_id: request.path_id,
                kind_utf8: kind.as_ptr() as *const c_uchar,
                kind_utf8_len: kind.as_bytes().len() as c_int,
                image_format_utf8: format.as_ptr() as *const c_uchar,
                image_format_utf8_len: format.as_bytes().len() as c_int,
            });
            kinds.push(kind);
            formats.push(format);
        }
        let ffi_request = ObjectReadBatchIntoRequest {
            struct_size: size_of::<ObjectReadBatchIntoRequest>() as c_int,
            context_id: self.context_id,
            items: items.as_ptr(),
            count: items.len() as c_int,
            flags: 0,
            items_buffer: ptr::null_mut(),
            items_buffer_len: 0,
            payload: ptr::null_mut(),
            payload_len: 0,
            reserved: 0,
        };
        let mut response = ObjectReadBatchRetryResponse::default();
        let status = unsafe { (self.native.read_by_path_retry)(&ffi_request, &mut response) };
        self.finish_read_retry(status, response)
    }

    pub fn read_by_index_retry(&self, requests: &[ObjectReadByIndexRequest<'_>]) -> Result<ObjectReadResult> {
        let mut kinds = Vec::with_capacity(requests.len());
        let mut formats = Vec::with_capacity(requests.len());
        let mut items = Vec::with_capacity(requests.len());
        for request in requests {
            let kind = CString::new(request.kind)?;
            let format = CString::new(request.image_format)?;
            items.push(ObjectReadItemByIndexRequest {
                object_index: request.object_index,
                kind_utf8: kind.as_ptr() as *const c_uchar,
                kind_utf8_len: kind.as_bytes().len() as c_int,
                image_format_utf8: format.as_ptr() as *const c_uchar,
                image_format_utf8_len: format.as_bytes().len() as c_int,
            });
            kinds.push(kind);
            formats.push(format);
        }
        let ffi_request = ObjectReadBatchByIndexIntoRequest {
            struct_size: size_of::<ObjectReadBatchByIndexIntoRequest>() as c_int,
            context_id: self.context_id,
            items: items.as_ptr(),
            count: items.len() as c_int,
            flags: 0,
            reserved: 0,
            items_buffer: ptr::null_mut(),
            items_buffer_len: 0,
            payload: ptr::null_mut(),
            payload_len: 0,
        };
        let mut response = ObjectReadBatchRetryResponse::default();
        let status = unsafe { (self.native.read_by_index_retry)(&ffi_request, &mut response) };
        self.finish_read_retry(status, response)
    }

    fn finish_read_retry(&self, status: i32, response: ObjectReadBatchRetryResponse) -> Result<ObjectReadResult> {
        if status != 0 && status != 9 {
            if response.result_handle != 0 {
                unsafe { (self.native.result_free)(response.result_handle) };
            }
            return Err(native_error(status, response.status, response.error_code));
        }
        let read_items = if response.items.is_null() || response.returned_count <= 0 {
            Vec::new()
        } else {
            let native_items = unsafe { std::slice::from_raw_parts(response.items, response.returned_count as usize) };
            native_items
                .iter()
                .map(|item| ObjectReadItem {
                    index: item.index,
                    status: item.status,
                    error_code: item.error_code,
                    path_id: item.path_id,
                    type_id: item.type_id,
                    size: item.size,
                    payload_offset: item.payload_offset,
                    payload_len: item.payload_len,
                    payload_kind: read_string(response.string_data, item.payload_kind_offset, item.payload_kind_len),
                    suggested_extension: read_string(
                        response.string_data,
                        item.suggested_extension_offset,
                        item.suggested_extension_len,
                    ),
                    error_message: read_string(response.string_data, item.error_message_offset, item.error_message_len),
                })
                .collect()
        };
        let payload = if response.payload.is_null() || response.payload_len <= 0 {
            Vec::new()
        } else {
            unsafe { std::slice::from_raw_parts(response.payload, response.payload_len as usize).to_vec() }
        };
        let handle = response.result_handle;
        if handle != 0 {
            unsafe { (self.native.result_free)(handle) };
        }
        Ok(ObjectReadResult {
            status: response.status,
            error_code: response.error_code,
            requested_count: response.requested_count,
            returned_count: response.returned_count,
            failed_count: response.failed_count,
            required_items_buffer_len: response.required_items_buffer_len,
            required_payload_len: response.required_payload_len,
            ownership_flags: response.ownership_flags,
            items: read_items,
            payload,
        })
    }
}

impl Drop for Context {
    fn drop(&mut self) {
        let request = ContextCloseRequest {
            struct_size: size_of::<ContextCloseRequest>() as c_int,
            context_id: self.context_id,
            flags: 0,
            reserved: 0,
        };
        let mut response = ContextCloseResponse::default();
        unsafe {
            (self.native.close)(&request, &mut response);
        }
    }
}

#[derive(Debug, Clone)]
pub struct AssetInfo {
    pub index: i32,
    pub path_id: i64,
    pub type_id: i32,
    pub size: i64,
    pub name: String,
    pub type_name: String,
}

#[derive(Debug, Clone)]
pub struct ObjectReadByIndexRequest<'a> {
    pub object_index: i32,
    pub kind: &'a str,
    pub image_format: &'a str,
}

#[derive(Debug, Clone)]
pub struct ObjectReadByPathIdRequest<'a> {
    pub path_id: i64,
    pub kind: &'a str,
    pub image_format: &'a str,
}

#[derive(Debug, Clone, Copy)]
#[repr(i32)]
pub enum ObjectLookupKind {
    PathId = 1,
    Name = 2,
    Container = 3,
    Type = 4,
}

#[derive(Debug, Clone)]
pub struct ObjectLookupRequestOptions<'a> {
    pub kind: ObjectLookupKind,
    pub path_id: Option<i64>,
    pub query: Option<&'a str>,
    pub offset: i32,
    pub limit: i32,
    pub contains: bool,
    pub asset_types: Vec<&'a str>,
}

impl<'a> ObjectLookupRequestOptions<'a> {
    pub fn path_id(path_id: i64) -> Self {
        Self {
            kind: ObjectLookupKind::PathId,
            path_id: Some(path_id),
            query: None,
            offset: 0,
            limit: 1,
            contains: false,
            asset_types: Vec::new(),
        }
    }

    pub fn name(query: &'a str) -> Self {
        Self {
            kind: ObjectLookupKind::Name,
            path_id: None,
            query: Some(query),
            offset: 0,
            limit: 0,
            contains: false,
            asset_types: Vec::new(),
        }
    }
}

#[derive(Debug, Clone)]
pub struct ObjectReadResult {
    pub status: i32,
    pub error_code: i32,
    pub requested_count: i32,
    pub returned_count: i32,
    pub failed_count: i32,
    pub required_items_buffer_len: i64,
    pub required_payload_len: i64,
    pub ownership_flags: i32,
    pub items: Vec<ObjectReadItem>,
    pub payload: Vec<u8>,
}

#[derive(Debug, Clone)]
pub struct ObjectReadItem {
    pub index: i32,
    pub status: i32,
    pub error_code: i32,
    pub path_id: i64,
    pub type_id: i32,
    pub size: i64,
    pub payload_offset: i64,
    pub payload_len: i64,
    pub payload_kind: String,
    pub suggested_extension: String,
    pub error_message: String,
}

impl ObjectReadResult {
    pub fn payload_for(&self, item: &ObjectReadItem) -> Option<&[u8]> {
        if item.payload_offset < 0 || item.payload_len < 0 {
            return None;
        }
        let start = item.payload_offset as usize;
        let len = item.payload_len as usize;
        let end = start.checked_add(len)?;
        self.payload.get(start..end)
    }
}

#[derive(Debug, Clone, Deserialize)]
pub struct Capabilities {
    #[serde(default)]
    pub ffi_mode: String,
    #[serde(default)]
    pub supports_native_streaming_payload: bool,
    #[serde(default)]
    pub native_streaming_payload_kinds: Vec<String>,
    #[serde(default)]
    pub direct_buffer_write_payload_kinds: Vec<String>,
    #[serde(default)]
    pub source_streaming_payload_kinds: Vec<String>,
    #[serde(default)]
    pub resident_buffer_payload_kinds: Vec<String>,
    #[serde(default)]
    pub generated_streaming_payload_kinds: Vec<String>,
    #[serde(default)]
    pub temp_file_intermediate_payload_kinds: Vec<String>,
    #[serde(default)]
    pub managed_intermediate_payload_kinds: Vec<String>,
    #[serde(default)]
    pub supports_concurrent_operations: bool,
    #[serde(default)]
    pub max_concurrent_operations: i32,
    #[serde(default)]
    pub legacy_static_engine: bool,
}

#[derive(Deserialize)]
struct AbiLayout {
    struct_sizes: HashMap<String, usize>,
}

fn check_size<T>(layout: &AbiLayout, name: &'static str) -> Result<()> {
    let rust = size_of::<T>();
    let native = layout.struct_sizes.get(name).copied().unwrap_or(0);
    if native != rust {
        return Err(Error::LayoutMismatch { name, native, rust });
    }
    Ok(())
}

unsafe fn call_json(function: JsonFn, free: FreeStringFn) -> Result<String> {
    let mut pointer: *mut c_char = ptr::null_mut();
    let status = function(&mut pointer);
    if status != 0 {
        return Err(Error::Native {
            status,
            detail: "json function failed".to_string(),
        });
    }
    let value = CStr::from_ptr(pointer).to_string_lossy().into_owned();
    free(pointer);
    Ok(value)
}

fn optional_cstring(value: Option<&str>) -> Result<Option<CString>> {
    value.map(CString::new).transpose().map_err(Error::from)
}

fn native_error(return_status: i32, response_status: i32, error_code: i32) -> Error {
    Error::Native {
        status: if return_status != 0 { return_status } else { response_status },
        detail: format!("response_status={response_status} error_code={error_code}"),
    }
}

fn read_string(base: *const c_uchar, offset: c_int, len: c_int) -> String {
    if base.is_null() || offset < 0 || len <= 0 {
        return String::new();
    }
    unsafe {
        let bytes = std::slice::from_raw_parts(base.add(offset as usize), len as usize);
        String::from_utf8_lossy(bytes).into_owned()
    }
}

fn read_asset_infos(response: &ObjectTable) -> Vec<AssetInfo> {
    if response.objects.is_null() || response.returned_count <= 0 {
        return Vec::new();
    }
    let objects = unsafe { std::slice::from_raw_parts(response.objects, response.returned_count as usize) };
    objects
        .iter()
        .map(|object| AssetInfo {
            index: object.index,
            path_id: object.path_id,
            type_id: object.type_id,
            size: object.size,
            name: read_string(response.string_data, object.name_offset, object.name_len),
            type_name: read_string(response.string_data, object.type_offset, object.type_len),
        })
        .collect()
}

#[repr(C)]
struct ContextOpenRequest {
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
#[derive(Default)]
struct ContextOpenResponse {
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
struct ContextCloseRequest {
    struct_size: c_int,
    context_id: c_longlong,
    flags: c_int,
    reserved: c_int,
}

#[repr(C)]
#[derive(Default)]
struct ContextCloseResponse {
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
struct ObjectListRequest {
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
struct ObjectListIntoRequest {
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
struct ObjectLookupRequest {
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
struct ObjectLookupIntoRequest {
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
struct AssetObject {
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
#[derive(Default)]
struct ObjectTable {
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
    objects: *mut AssetObject,
    string_data: *mut c_uchar,
    string_data_len: c_int,
    buffer: *mut c_uchar,
    buffer_len: c_longlong,
    duration_ms: c_longlong,
    flags: c_int,
    reserved: c_int,
}

#[repr(C)]
struct ObjectReadItemRequest {
    path_id: c_longlong,
    kind_utf8: *const c_uchar,
    kind_utf8_len: c_int,
    image_format_utf8: *const c_uchar,
    image_format_utf8_len: c_int,
}

#[repr(C)]
struct ObjectReadBatchIntoRequest {
    struct_size: c_int,
    context_id: c_longlong,
    items: *const ObjectReadItemRequest,
    count: c_int,
    flags: c_int,
    items_buffer: *mut c_uchar,
    items_buffer_len: c_longlong,
    payload: *mut c_uchar,
    payload_len: c_longlong,
    reserved: c_int,
}

#[repr(C)]
struct ObjectReadItemByIndexRequest {
    object_index: c_int,
    kind_utf8: *const c_uchar,
    kind_utf8_len: c_int,
    image_format_utf8: *const c_uchar,
    image_format_utf8_len: c_int,
}

#[repr(C)]
struct ObjectReadBatchByIndexIntoRequest {
    struct_size: c_int,
    context_id: c_longlong,
    items: *const ObjectReadItemByIndexRequest,
    count: c_int,
    flags: c_int,
    reserved: c_int,
    items_buffer: *mut c_uchar,
    items_buffer_len: c_longlong,
    payload: *mut c_uchar,
    payload_len: c_longlong,
}

#[repr(C)]
#[derive(Default)]
struct ObjectReadBatchRetryResponse {
    struct_size: c_int,
    abi_version: c_int,
    schema_version: c_int,
    object_read_batch_abi_version: c_int,
    object_read_batch_into_abi_version: c_int,
    object_read_batch_direct_retry_abi_version: c_int,
    status: c_int,
    error_code: c_int,
    context_id: c_longlong,
    requested_count: c_int,
    returned_count: c_int,
    failed_count: c_int,
    items: *mut ObjectReadItemResponse,
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
    result_handle: c_longlong,
    ownership_flags: c_int,
    flags: c_int,
    reserved: c_int,
}

#[repr(C)]
struct ObjectReadItemResponse {
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn payload_for_returns_item_slice() {
        let result = ObjectReadResult {
            status: 0,
            error_code: 0,
            requested_count: 1,
            returned_count: 1,
            failed_count: 0,
            required_items_buffer_len: 0,
            required_payload_len: 6,
            ownership_flags: 0,
            items: Vec::new(),
            payload: vec![1, 2, 3, 4, 5, 6],
        };
        let item = ObjectReadItem {
            index: 0,
            status: 0,
            error_code: 0,
            path_id: 10,
            type_id: 49,
            size: 6,
            payload_offset: 2,
            payload_len: 3,
            payload_kind: "raw".to_string(),
            suggested_extension: ".dat".to_string(),
            error_message: String::new(),
        };
        assert_eq!(result.payload_for(&item), Some(&[3, 4, 5][..]));
    }

    #[test]
    fn payload_for_rejects_out_of_range_slice() {
        let result = ObjectReadResult {
            status: 0,
            error_code: 0,
            requested_count: 1,
            returned_count: 1,
            failed_count: 0,
            required_items_buffer_len: 0,
            required_payload_len: 2,
            ownership_flags: 0,
            items: Vec::new(),
            payload: vec![1, 2],
        };
        let item = ObjectReadItem {
            index: 0,
            status: 0,
            error_code: 0,
            path_id: 10,
            type_id: 49,
            size: 2,
            payload_offset: 1,
            payload_len: 3,
            payload_kind: "raw".to_string(),
            suggested_extension: ".dat".to_string(),
            error_message: String::new(),
        };
        assert!(result.payload_for(&item).is_none());
    }

    #[test]
    fn capabilities_defaults_missing_streaming_tiers() {
        let capabilities: Capabilities = serde_json::from_str(
            r#"{
                "ffi_mode": "core",
                "supports_native_streaming_payload": true,
                "native_streaming_payload_kinds": ["raw"],
                "legacy_static_engine": false
            }"#,
        )
        .unwrap();
        assert_eq!(capabilities.native_streaming_payload_kinds, ["raw"]);
        assert!(capabilities.generated_streaming_payload_kinds.is_empty());
        assert!(!capabilities.legacy_static_engine);
    }
}
