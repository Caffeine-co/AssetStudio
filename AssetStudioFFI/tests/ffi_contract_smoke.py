#!/usr/bin/env python3
import argparse
import ctypes
import json
import struct
import sys
import traceback


class NativeObjectListRequest(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("offset", ctypes.c_int),
        ("limit", ctypes.c_int),
        ("asset_types_csv_utf8", ctypes.POINTER(ctypes.c_ubyte)),
        ("asset_types_csv_utf8_len", ctypes.c_int),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
    ]


class NativeObjectListIntoRequestV3(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("offset", ctypes.c_int),
        ("limit", ctypes.c_int),
        ("asset_types_csv_utf8", ctypes.POINTER(ctypes.c_ubyte)),
        ("asset_types_csv_utf8_len", ctypes.c_int),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
        ("buffer", ctypes.c_void_p),
        ("buffer_len", ctypes.c_longlong),
    ]


class NativeObjectLookupRequest(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("lookup_kind", ctypes.c_int),
        ("path_id", ctypes.c_longlong),
        ("query_utf8", ctypes.POINTER(ctypes.c_ubyte)),
        ("query_utf8_len", ctypes.c_int),
        ("asset_types_csv_utf8", ctypes.POINTER(ctypes.c_ubyte)),
        ("asset_types_csv_utf8_len", ctypes.c_int),
        ("offset", ctypes.c_int),
        ("limit", ctypes.c_int),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
    ]


class NativeObjectLookupIntoRequestV2(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("lookup_kind", ctypes.c_int),
        ("path_id", ctypes.c_longlong),
        ("query_utf8", ctypes.POINTER(ctypes.c_ubyte)),
        ("query_utf8_len", ctypes.c_int),
        ("asset_types_csv_utf8", ctypes.POINTER(ctypes.c_ubyte)),
        ("asset_types_csv_utf8_len", ctypes.c_int),
        ("offset", ctypes.c_int),
        ("limit", ctypes.c_int),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
        ("buffer", ctypes.c_void_p),
        ("buffer_len", ctypes.c_longlong),
    ]


class NativeContextOpenRequest(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("input_path_utf8", ctypes.POINTER(ctypes.c_ubyte)),
        ("input_path_utf8_len", ctypes.c_int),
        ("unity_version_utf8", ctypes.POINTER(ctypes.c_ubyte)),
        ("unity_version_utf8_len", ctypes.c_int),
        ("asset_types_csv_utf8", ctypes.POINTER(ctypes.c_ubyte)),
        ("asset_types_csv_utf8_len", ctypes.c_int),
        ("output_dir_utf8", ctypes.POINTER(ctypes.c_ubyte)),
        ("output_dir_utf8_len", ctypes.c_int),
        ("load_all_assets", ctypes.c_int),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
    ]


class NativeContextOpenResponse(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("abi_version", ctypes.c_int),
        ("schema_version", ctypes.c_int),
        ("context_abi_version", ctypes.c_int),
        ("status", ctypes.c_int),
        ("error_code", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("assets_file_count", ctypes.c_int),
        ("exportable_asset_count", ctypes.c_int),
        ("object_index_count", ctypes.c_int),
        ("has_more_assets", ctypes.c_int),
        ("unity_version_utf8", ctypes.POINTER(ctypes.c_ubyte)),
        ("unity_version_utf8_len", ctypes.c_int),
        ("buffer", ctypes.c_void_p),
        ("buffer_len", ctypes.c_longlong),
        ("duration_ms", ctypes.c_longlong),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
    ]


class NativeContextCloseRequest(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
    ]


class NativeContextCloseResponse(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("abi_version", ctypes.c_int),
        ("schema_version", ctypes.c_int),
        ("context_abi_version", ctypes.c_int),
        ("status", ctypes.c_int),
        ("error_code", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("duration_ms", ctypes.c_longlong),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
    ]


class NativeLimitsResponse(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("abi_version", ctypes.c_int),
        ("schema_version", ctypes.c_int),
        ("limits_abi_version", ctypes.c_int),
        ("status", ctypes.c_int),
        ("error_code", ctypes.c_int),
        ("max_native_utf8_bytes", ctypes.c_int),
        ("max_object_read_batch_count", ctypes.c_int),
        ("max_object_table_page_limit", ctypes.c_int),
        ("max_object_read_batch_payload_bytes", ctypes.c_longlong),
        ("max_cached_object_read_batch_payload_bytes", ctypes.c_longlong),
        ("max_active_contexts", ctypes.c_int),
        ("max_concurrent_operations", ctypes.c_int),
        ("supports_multiple_contexts", ctypes.c_int),
        ("supports_concurrent_operations", ctypes.c_int),
        ("legacy_static_engine", ctypes.c_int),
        ("native_console_capture", ctypes.c_int),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
    ]


class NativeCapabilitiesResponse(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("abi_version", ctypes.c_int),
        ("schema_version", ctypes.c_int),
        ("status", ctypes.c_int),
        ("error_code", ctypes.c_int),
        ("core_api_version_major", ctypes.c_int),
        ("core_api_version_minor", ctypes.c_int),
        ("context_abi_version", ctypes.c_int),
        ("object_table_abi_version", ctypes.c_int),
        ("object_table_into_abi_version", ctypes.c_int),
        ("object_lookup_abi_version", ctypes.c_int),
        ("object_lookup_into_abi_version", ctypes.c_int),
        ("object_read_abi_version", ctypes.c_int),
        ("object_read_batch_abi_version", ctypes.c_int),
        ("object_read_batch_handle_abi_version", ctypes.c_int),
        ("object_read_batch_into_abi_version", ctypes.c_int),
        ("object_read_batch_by_index_abi_version", ctypes.c_int),
        ("object_read_batch_direct_into_abi_version", ctypes.c_int),
        ("object_read_batch_direct_retry_abi_version", ctypes.c_int),
        ("supports_typed_object_table", ctypes.c_int),
        ("supports_caller_provided_object_table_buffers", ctypes.c_int),
        ("supports_typed_object_lookup", ctypes.c_int),
        ("supports_caller_provided_object_lookup_buffers", ctypes.c_int),
        ("supports_typed_object_read", ctypes.c_int),
        ("supports_typed_object_read_batch", ctypes.c_int),
        ("supports_result_handle", ctypes.c_int),
        ("supports_direct_object_read_retry", ctypes.c_int),
        ("supports_typed_context", ctypes.c_int),
        ("supports_native_dependency_resolver", ctypes.c_int),
        ("supports_abi_layout", ctypes.c_int),
        ("supports_multiple_contexts", ctypes.c_int),
        ("supports_concurrent_operations", ctypes.c_int),
        ("supports_context_lifetime_guards", ctypes.c_int),
        ("native_console_capture", ctypes.c_int),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
    ]


class NativeAbiLayoutResponse(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("abi_version", ctypes.c_int),
        ("schema_version", ctypes.c_int),
        ("status", ctypes.c_int),
        ("error_code", ctypes.c_int),
        ("layout_version", ctypes.c_int),
        ("context_open_request", ctypes.c_int),
        ("context_open_response", ctypes.c_int),
        ("context_close_request", ctypes.c_int),
        ("context_close_response", ctypes.c_int),
        ("limits_response", ctypes.c_int),
        ("capabilities_response", ctypes.c_int),
        ("object_list_request", ctypes.c_int),
        ("object_list_into_request_v3", ctypes.c_int),
        ("object_table", ctypes.c_int),
        ("asset_object", ctypes.c_int),
        ("object_read_item_request", ctypes.c_int),
        ("object_read_batch_into_request_v4", ctypes.c_int),
        ("object_read_item_response_v4", ctypes.c_int),
        ("object_read_batch_retry_response_v7", ctypes.c_int),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
    ]


class NativeAssetObject(ctypes.Structure):
    _fields_ = [
        ("index", ctypes.c_int),
        ("type_id", ctypes.c_int),
        ("path_id", ctypes.c_longlong),
        ("size", ctypes.c_longlong),
        ("estimated_payload_capacity", ctypes.c_longlong),
        ("raw_payload_capacity", ctypes.c_longlong),
        ("image_payload_capacity", ctypes.c_longlong),
        ("text_payload_capacity", ctypes.c_longlong),
        ("payload_capacity_flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
        ("name_offset", ctypes.c_int),
        ("name_len", ctypes.c_int),
        ("container_offset", ctypes.c_int),
        ("container_len", ctypes.c_int),
        ("type_offset", ctypes.c_int),
        ("type_len", ctypes.c_int),
        ("unique_id_offset", ctypes.c_int),
        ("unique_id_len", ctypes.c_int),
        ("source_file_offset", ctypes.c_int),
        ("source_file_len", ctypes.c_int),
    ]


class NativeObjectTable(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("abi_version", ctypes.c_int),
        ("schema_version", ctypes.c_int),
        ("object_table_abi_version", ctypes.c_int),
        ("status", ctypes.c_int),
        ("error_code", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("offset", ctypes.c_int),
        ("limit", ctypes.c_int),
        ("next_offset", ctypes.c_int),
        ("has_more", ctypes.c_int),
        ("total_count", ctypes.c_int),
        ("returned_count", ctypes.c_int),
        ("objects", ctypes.POINTER(NativeAssetObject)),
        ("string_data", ctypes.POINTER(ctypes.c_ubyte)),
        ("string_data_len", ctypes.c_int),
        ("buffer", ctypes.c_void_p),
        ("buffer_len", ctypes.c_longlong),
        ("duration_ms", ctypes.c_longlong),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
    ]


class NativeObjectReadItemRequest(ctypes.Structure):
    _fields_ = [
        ("path_id", ctypes.c_longlong),
        ("kind_utf8", ctypes.POINTER(ctypes.c_ubyte)),
        ("kind_utf8_len", ctypes.c_int),
        ("image_format_utf8", ctypes.POINTER(ctypes.c_ubyte)),
        ("image_format_utf8_len", ctypes.c_int),
    ]


class NativeObjectReadItemByIndexRequestV5(ctypes.Structure):
    _fields_ = [
        ("object_index", ctypes.c_int),
        ("kind_utf8", ctypes.POINTER(ctypes.c_ubyte)),
        ("kind_utf8_len", ctypes.c_int),
        ("image_format_utf8", ctypes.POINTER(ctypes.c_ubyte)),
        ("image_format_utf8_len", ctypes.c_int),
    ]


class NativeObjectReadBatchRequest(ctypes.Structure):
    _fields_ = [
        ("context_id", ctypes.c_longlong),
        ("items", ctypes.POINTER(NativeObjectReadItemRequest)),
        ("count", ctypes.c_int),
        ("flags", ctypes.c_int),
    ]


class NativeObjectReadBatchRequestV4(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("items", ctypes.POINTER(NativeObjectReadItemRequest)),
        ("count", ctypes.c_int),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
    ]


class NativeObjectReadBatchIntoRequestV4(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("items", ctypes.POINTER(NativeObjectReadItemRequest)),
        ("count", ctypes.c_int),
        ("flags", ctypes.c_int),
        ("items_buffer", ctypes.c_void_p),
        ("items_buffer_len", ctypes.c_longlong),
        ("payload", ctypes.c_void_p),
        ("payload_len", ctypes.c_longlong),
        ("reserved", ctypes.c_int),
    ]


class NativeObjectReadBatchByIndexRequestV5(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("items", ctypes.POINTER(NativeObjectReadItemByIndexRequestV5)),
        ("count", ctypes.c_int),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
    ]


class NativeObjectReadBatchByIndexIntoRequestV5(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("items", ctypes.POINTER(NativeObjectReadItemByIndexRequestV5)),
        ("count", ctypes.c_int),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
        ("items_buffer", ctypes.c_void_p),
        ("items_buffer_len", ctypes.c_longlong),
        ("payload", ctypes.c_void_p),
        ("payload_len", ctypes.c_longlong),
    ]


class NativeObjectReadItemResponse(ctypes.Structure):
    _fields_ = [
        ("index", ctypes.c_int),
        ("status", ctypes.c_int),
        ("error_code", ctypes.c_int),
        ("path_id", ctypes.c_longlong),
        ("type_id", ctypes.c_int),
        ("size", ctypes.c_longlong),
        ("payload_offset", ctypes.c_longlong),
        ("payload_len", ctypes.c_longlong),
        ("payload_kind_offset", ctypes.c_int),
        ("payload_kind_len", ctypes.c_int),
        ("suggested_extension_offset", ctypes.c_int),
        ("suggested_extension_len", ctypes.c_int),
    ]


class NativeObjectReadBatchResponse(ctypes.Structure):
    _fields_ = [
        ("abi_version", ctypes.c_int),
        ("schema_version", ctypes.c_int),
        ("object_read_batch_abi_version", ctypes.c_int),
        ("status", ctypes.c_int),
        ("error_code", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("requested_count", ctypes.c_int),
        ("returned_count", ctypes.c_int),
        ("failed_count", ctypes.c_int),
        ("items", ctypes.POINTER(NativeObjectReadItemResponse)),
        ("string_data", ctypes.POINTER(ctypes.c_ubyte)),
        ("string_data_len", ctypes.c_int),
        ("items_buffer", ctypes.c_void_p),
        ("items_buffer_len", ctypes.c_longlong),
        ("payload", ctypes.c_void_p),
        ("payload_len", ctypes.c_longlong),
        ("duration_ms", ctypes.c_longlong),
    ]


class NativeObjectReadItemResponseV4(ctypes.Structure):
    _fields_ = [
        ("index", ctypes.c_int),
        ("status", ctypes.c_int),
        ("error_code", ctypes.c_int),
        ("path_id", ctypes.c_longlong),
        ("type_id", ctypes.c_int),
        ("size", ctypes.c_longlong),
        ("payload_offset", ctypes.c_longlong),
        ("payload_len", ctypes.c_longlong),
        ("payload_kind_offset", ctypes.c_int),
        ("payload_kind_len", ctypes.c_int),
        ("suggested_extension_offset", ctypes.c_int),
        ("suggested_extension_len", ctypes.c_int),
        ("error_message_offset", ctypes.c_int),
        ("error_message_len", ctypes.c_int),
    ]


class NativeObjectReadBatchResponseV3(ctypes.Structure):
    _fields_ = [
        ("abi_version", ctypes.c_int),
        ("schema_version", ctypes.c_int),
        ("object_read_batch_abi_version", ctypes.c_int),
        ("status", ctypes.c_int),
        ("error_code", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("requested_count", ctypes.c_int),
        ("returned_count", ctypes.c_int),
        ("failed_count", ctypes.c_int),
        ("items", ctypes.POINTER(NativeObjectReadItemResponse)),
        ("string_data", ctypes.POINTER(ctypes.c_ubyte)),
        ("string_data_len", ctypes.c_int),
        ("items_buffer", ctypes.c_void_p),
        ("items_buffer_len", ctypes.c_longlong),
        ("payload", ctypes.c_void_p),
        ("payload_len", ctypes.c_longlong),
        ("duration_ms", ctypes.c_longlong),
        ("object_read_batch_handle_abi_version", ctypes.c_int),
        ("result_handle", ctypes.c_longlong),
    ]


class NativeObjectReadBatchSizeResponseV4(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("abi_version", ctypes.c_int),
        ("schema_version", ctypes.c_int),
        ("object_read_batch_abi_version", ctypes.c_int),
        ("object_read_batch_into_abi_version", ctypes.c_int),
        ("status", ctypes.c_int),
        ("error_code", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("requested_count", ctypes.c_int),
        ("returned_count", ctypes.c_int),
        ("failed_count", ctypes.c_int),
        ("required_items_buffer_len", ctypes.c_longlong),
        ("required_string_data_len", ctypes.c_int),
        ("required_payload_len", ctypes.c_longlong),
        ("items_buffer_len", ctypes.c_longlong),
        ("string_data_len", ctypes.c_int),
        ("payload_len", ctypes.c_longlong),
        ("duration_ms", ctypes.c_longlong),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
    ]


class NativeObjectReadBatchIntoResponseV4(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("abi_version", ctypes.c_int),
        ("schema_version", ctypes.c_int),
        ("object_read_batch_abi_version", ctypes.c_int),
        ("object_read_batch_into_abi_version", ctypes.c_int),
        ("status", ctypes.c_int),
        ("error_code", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("requested_count", ctypes.c_int),
        ("returned_count", ctypes.c_int),
        ("failed_count", ctypes.c_int),
        ("items", ctypes.POINTER(NativeObjectReadItemResponseV4)),
        ("string_data", ctypes.POINTER(ctypes.c_ubyte)),
        ("string_data_len", ctypes.c_int),
        ("items_buffer", ctypes.c_void_p),
        ("items_buffer_len", ctypes.c_longlong),
        ("payload", ctypes.c_void_p),
        ("payload_len", ctypes.c_longlong),
        ("required_items_buffer_len", ctypes.c_longlong),
        ("required_string_data_len", ctypes.c_int),
        ("required_payload_len", ctypes.c_longlong),
        ("duration_ms", ctypes.c_longlong),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
    ]


class NativeObjectReadBatchRetryResponseV7(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int),
        ("abi_version", ctypes.c_int),
        ("schema_version", ctypes.c_int),
        ("object_read_batch_abi_version", ctypes.c_int),
        ("object_read_batch_into_abi_version", ctypes.c_int),
        ("object_read_batch_direct_retry_abi_version", ctypes.c_int),
        ("status", ctypes.c_int),
        ("error_code", ctypes.c_int),
        ("context_id", ctypes.c_longlong),
        ("requested_count", ctypes.c_int),
        ("returned_count", ctypes.c_int),
        ("failed_count", ctypes.c_int),
        ("items", ctypes.POINTER(NativeObjectReadItemResponseV4)),
        ("string_data", ctypes.POINTER(ctypes.c_ubyte)),
        ("string_data_len", ctypes.c_int),
        ("items_buffer", ctypes.c_void_p),
        ("items_buffer_len", ctypes.c_longlong),
        ("payload", ctypes.c_void_p),
        ("payload_len", ctypes.c_longlong),
        ("required_items_buffer_len", ctypes.c_longlong),
        ("required_string_data_len", ctypes.c_int),
        ("required_payload_len", ctypes.c_longlong),
        ("duration_ms", ctypes.c_longlong),
        ("result_handle", ctypes.c_longlong),
        ("ownership_flags", ctypes.c_int),
        ("flags", ctypes.c_int),
        ("reserved", ctypes.c_int),
    ]


class HarukiAssetStudioNative:
    def __init__(self, library_path):
        self.lib = ctypes.CDLL(library_path)
        self.lib.haruki_assetstudio_capabilities_v2.argtypes = [ctypes.POINTER(NativeCapabilitiesResponse)]
        self.lib.haruki_assetstudio_capabilities_v2.restype = ctypes.c_int
        self.lib.haruki_assetstudio_abi_layout_v2.argtypes = [ctypes.POINTER(NativeAbiLayoutResponse)]
        self.lib.haruki_assetstudio_abi_layout_v2.restype = ctypes.c_int
        self.lib.haruki_assetstudio_limits_v1.argtypes = [ctypes.POINTER(NativeLimitsResponse)]
        self.lib.haruki_assetstudio_limits_v1.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_open_v2.argtypes = [
            ctypes.POINTER(NativeContextOpenRequest),
            ctypes.POINTER(NativeContextOpenResponse),
        ]
        self.lib.haruki_assetstudio_context_open_v2.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_list_objects_v2.argtypes = [
            ctypes.POINTER(NativeObjectListRequest),
            ctypes.POINTER(NativeObjectTable),
        ]
        self.lib.haruki_assetstudio_context_list_objects_v2.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_list_objects_size_v3.argtypes = [
            ctypes.POINTER(NativeObjectListRequest),
            ctypes.POINTER(NativeObjectTable),
        ]
        self.lib.haruki_assetstudio_context_list_objects_size_v3.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_list_objects_into_v3.argtypes = [
            ctypes.POINTER(NativeObjectListIntoRequestV3),
            ctypes.POINTER(NativeObjectTable),
        ]
        self.lib.haruki_assetstudio_context_list_objects_into_v3.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_lookup_objects_v1.argtypes = [
            ctypes.POINTER(NativeObjectLookupRequest),
            ctypes.POINTER(NativeObjectTable),
        ]
        self.lib.haruki_assetstudio_context_lookup_objects_v1.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_lookup_objects_size_v2.argtypes = [
            ctypes.POINTER(NativeObjectLookupRequest),
            ctypes.POINTER(NativeObjectTable),
        ]
        self.lib.haruki_assetstudio_context_lookup_objects_size_v2.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_lookup_objects_into_v2.argtypes = [
            ctypes.POINTER(NativeObjectLookupIntoRequestV2),
            ctypes.POINTER(NativeObjectTable),
        ]
        self.lib.haruki_assetstudio_context_lookup_objects_into_v2.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_read_objects_v2.argtypes = [
            ctypes.POINTER(NativeObjectReadBatchRequest),
            ctypes.POINTER(NativeObjectReadBatchResponse),
        ]
        self.lib.haruki_assetstudio_context_read_objects_v2.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_read_objects_v3.argtypes = [
            ctypes.POINTER(NativeObjectReadBatchRequest),
            ctypes.POINTER(NativeObjectReadBatchResponseV3),
        ]
        self.lib.haruki_assetstudio_context_read_objects_v3.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_read_objects_size_v4.argtypes = [
            ctypes.POINTER(NativeObjectReadBatchRequestV4),
            ctypes.POINTER(NativeObjectReadBatchSizeResponseV4),
        ]
        self.lib.haruki_assetstudio_context_read_objects_size_v4.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_read_objects_into_v4.argtypes = [
            ctypes.POINTER(NativeObjectReadBatchIntoRequestV4),
            ctypes.POINTER(NativeObjectReadBatchIntoResponseV4),
        ]
        self.lib.haruki_assetstudio_context_read_objects_into_v4.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_read_objects_by_index_size_v5.argtypes = [
            ctypes.POINTER(NativeObjectReadBatchByIndexRequestV5),
            ctypes.POINTER(NativeObjectReadBatchSizeResponseV4),
        ]
        self.lib.haruki_assetstudio_context_read_objects_by_index_size_v5.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_read_objects_by_index_into_v5.argtypes = [
            ctypes.POINTER(NativeObjectReadBatchByIndexIntoRequestV5),
            ctypes.POINTER(NativeObjectReadBatchIntoResponseV4),
        ]
        self.lib.haruki_assetstudio_context_read_objects_by_index_into_v5.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_read_objects_direct_into_v6.argtypes = [
            ctypes.POINTER(NativeObjectReadBatchIntoRequestV4),
            ctypes.POINTER(NativeObjectReadBatchIntoResponseV4),
        ]
        self.lib.haruki_assetstudio_context_read_objects_direct_into_v6.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_read_objects_by_index_direct_into_v6.argtypes = [
            ctypes.POINTER(NativeObjectReadBatchByIndexIntoRequestV5),
            ctypes.POINTER(NativeObjectReadBatchIntoResponseV4),
        ]
        self.lib.haruki_assetstudio_context_read_objects_by_index_direct_into_v6.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_read_objects_direct_retry_v7.argtypes = [
            ctypes.POINTER(NativeObjectReadBatchIntoRequestV4),
            ctypes.POINTER(NativeObjectReadBatchRetryResponseV7),
        ]
        self.lib.haruki_assetstudio_context_read_objects_direct_retry_v7.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_read_objects_by_index_direct_retry_v7.argtypes = [
            ctypes.POINTER(NativeObjectReadBatchByIndexIntoRequestV5),
            ctypes.POINTER(NativeObjectReadBatchRetryResponseV7),
        ]
        self.lib.haruki_assetstudio_context_read_objects_by_index_direct_retry_v7.restype = ctypes.c_int
        self.lib.haruki_assetstudio_result_free.argtypes = [ctypes.c_longlong]
        self.lib.haruki_assetstudio_result_free.restype = ctypes.c_int
        self.lib.haruki_assetstudio_context_close_v2.argtypes = [
            ctypes.POINTER(NativeContextCloseRequest),
            ctypes.POINTER(NativeContextCloseResponse),
        ]
        self.lib.haruki_assetstudio_context_close_v2.restype = ctypes.c_int
        self.lib.haruki_assetstudio_free_string.argtypes = [ctypes.c_void_p]
        self.lib.haruki_assetstudio_free_string.restype = None
        self.lib.haruki_assetstudio_free_buffer.argtypes = [ctypes.c_void_p]
        self.lib.haruki_assetstudio_free_buffer.restype = None

    def _take_json(self, ptr):
        if not ptr:
            return {}
        text = ctypes.string_at(ptr).decode("utf-8")
        self.lib.haruki_assetstudio_free_string(ptr)
        return json.loads(text)

    def call_no_request(self, function):
        response = ctypes.c_void_p()
        rc = function(ctypes.byref(response))
        return rc, self._take_json(response.value)

    def open_v2(self, input_path, unity_version):
        buffers = []

        def native_bytes(value):
            raw = value.encode("utf-8")
            buffer = (ctypes.c_ubyte * len(raw)).from_buffer_copy(raw)
            buffers.append(buffer)
            return buffer, len(raw)

        input_path_buffer, input_path_len = native_bytes(input_path)
        unity_version_buffer, unity_version_len = native_bytes(unity_version)
        request = NativeContextOpenRequest(
            struct_size=ctypes.sizeof(NativeContextOpenRequest),
            input_path_utf8=input_path_buffer,
            input_path_utf8_len=input_path_len,
            unity_version_utf8=unity_version_buffer,
            unity_version_utf8_len=unity_version_len,
            asset_types_csv_utf8=None,
            asset_types_csv_utf8_len=0,
            output_dir_utf8=None,
            output_dir_utf8_len=0,
            load_all_assets=0,
            flags=0,
            reserved=0,
        )
        response = NativeContextOpenResponse()
        rc = self.lib.haruki_assetstudio_context_open_v2(ctypes.byref(request), ctypes.byref(response))
        unity_version_text = ""
        if response.unity_version_utf8 and response.unity_version_utf8_len > 0:
            unity_version_text = ctypes.string_at(response.unity_version_utf8, response.unity_version_utf8_len).decode("utf-8")
        if response.buffer:
            self.lib.haruki_assetstudio_free_buffer(response.buffer)
        return rc, response, unity_version_text

    def close_v2(self, context_id):
        request = NativeContextCloseRequest(
            struct_size=ctypes.sizeof(NativeContextCloseRequest),
            context_id=context_id,
            flags=0,
            reserved=0,
        )
        response = NativeContextCloseResponse()
        rc = self.lib.haruki_assetstudio_context_close_v2(ctypes.byref(request), ctypes.byref(response))
        return rc, response

    def list_objects_v2(self, context_id, offset=0, limit=8, asset_types_csv=None):
        asset_type_bytes = None
        asset_type_ptr = None
        asset_type_len = 0
        if asset_types_csv:
            asset_type_bytes = asset_types_csv.encode("utf-8")
            asset_type_len = len(asset_type_bytes)
            asset_type_ptr = (ctypes.c_ubyte * asset_type_len).from_buffer_copy(asset_type_bytes)
        request = NativeObjectListRequest(
            struct_size=ctypes.sizeof(NativeObjectListRequest),
            context_id=context_id,
            offset=offset,
            limit=limit,
            asset_types_csv_utf8=asset_type_ptr,
            asset_types_csv_utf8_len=asset_type_len,
            flags=0,
            reserved=0,
        )
        response = NativeObjectTable()
        rc = self.lib.haruki_assetstudio_context_list_objects_v2(ctypes.byref(request), ctypes.byref(response))
        objects = self._take_object_table_objects(response)
        return rc, response, objects

    def list_objects_v3(self, context_id, offset=0, limit=8, asset_types_csv=None, shrink_buffer_by=0):
        asset_type_bytes = None
        asset_type_ptr = None
        asset_type_len = 0
        if asset_types_csv:
            asset_type_bytes = asset_types_csv.encode("utf-8")
            asset_type_len = len(asset_type_bytes)
            asset_type_ptr = (ctypes.c_ubyte * asset_type_len).from_buffer_copy(asset_type_bytes)
        size_request = NativeObjectListRequest(
            struct_size=ctypes.sizeof(NativeObjectListRequest),
            context_id=context_id,
            offset=offset,
            limit=limit,
            asset_types_csv_utf8=asset_type_ptr,
            asset_types_csv_utf8_len=asset_type_len,
            flags=0,
            reserved=0,
        )
        size_response = NativeObjectTable()
        size_rc = self.lib.haruki_assetstudio_context_list_objects_size_v3(
            ctypes.byref(size_request),
            ctypes.byref(size_response),
        )
        buffer_len = max(0, size_response.buffer_len - shrink_buffer_by)
        buffer = (ctypes.c_ubyte * buffer_len)() if buffer_len > 0 else None
        into_request = NativeObjectListIntoRequestV3(
            struct_size=ctypes.sizeof(NativeObjectListIntoRequestV3),
            context_id=context_id,
            offset=offset,
            limit=limit,
            asset_types_csv_utf8=asset_type_ptr,
            asset_types_csv_utf8_len=asset_type_len,
            flags=0,
            reserved=0,
            buffer=ctypes.cast(buffer, ctypes.c_void_p) if buffer is not None else None,
            buffer_len=buffer_len,
        )
        into_response = NativeObjectTable()
        into_rc = self.lib.haruki_assetstudio_context_list_objects_into_v3(
            ctypes.byref(into_request),
            ctypes.byref(into_response),
        )
        objects = self._take_object_table_objects(into_response, free_buffer=False) if into_response.status == 0 else []
        return size_rc, size_response, into_rc, into_response, objects, buffer

    def lookup_objects_v1(self, context_id, lookup_kind, path_id=0, query=None, offset=0, limit=8, asset_types_csv=None, flags=0):
        query_bytes = None
        query_ptr = None
        query_len = 0
        if query:
            query_bytes = query.encode("utf-8")
            query_len = len(query_bytes)
            query_ptr = (ctypes.c_ubyte * query_len).from_buffer_copy(query_bytes)
        asset_type_bytes = None
        asset_type_ptr = None
        asset_type_len = 0
        if asset_types_csv:
            asset_type_bytes = asset_types_csv.encode("utf-8")
            asset_type_len = len(asset_type_bytes)
            asset_type_ptr = (ctypes.c_ubyte * asset_type_len).from_buffer_copy(asset_type_bytes)
        request = NativeObjectLookupRequest(
            struct_size=ctypes.sizeof(NativeObjectLookupRequest),
            context_id=context_id,
            lookup_kind=lookup_kind,
            path_id=path_id,
            query_utf8=query_ptr,
            query_utf8_len=query_len,
            asset_types_csv_utf8=asset_type_ptr,
            asset_types_csv_utf8_len=asset_type_len,
            offset=offset,
            limit=limit,
            flags=flags,
            reserved=0,
        )
        response = NativeObjectTable()
        rc = self.lib.haruki_assetstudio_context_lookup_objects_v1(ctypes.byref(request), ctypes.byref(response))
        objects = self._take_object_table_objects(response)
        return rc, response, objects

    def lookup_objects_v2(self, context_id, lookup_kind, path_id=0, query=None, offset=0, limit=8, asset_types_csv=None, flags=0, shrink_buffer_by=0):
        query_bytes = None
        query_ptr = None
        query_len = 0
        if query:
            query_bytes = query.encode("utf-8")
            query_len = len(query_bytes)
            query_ptr = (ctypes.c_ubyte * query_len).from_buffer_copy(query_bytes)
        asset_type_bytes = None
        asset_type_ptr = None
        asset_type_len = 0
        if asset_types_csv:
            asset_type_bytes = asset_types_csv.encode("utf-8")
            asset_type_len = len(asset_type_bytes)
            asset_type_ptr = (ctypes.c_ubyte * asset_type_len).from_buffer_copy(asset_type_bytes)
        size_request = NativeObjectLookupRequest(
            struct_size=ctypes.sizeof(NativeObjectLookupRequest),
            context_id=context_id,
            lookup_kind=lookup_kind,
            path_id=path_id,
            query_utf8=query_ptr,
            query_utf8_len=query_len,
            asset_types_csv_utf8=asset_type_ptr,
            asset_types_csv_utf8_len=asset_type_len,
            offset=offset,
            limit=limit,
            flags=flags,
            reserved=0,
        )
        size_response = NativeObjectTable()
        size_rc = self.lib.haruki_assetstudio_context_lookup_objects_size_v2(
            ctypes.byref(size_request),
            ctypes.byref(size_response),
        )
        buffer_len = max(0, size_response.buffer_len - shrink_buffer_by)
        buffer = (ctypes.c_ubyte * buffer_len)() if buffer_len > 0 else None
        into_request = NativeObjectLookupIntoRequestV2(
            struct_size=ctypes.sizeof(NativeObjectLookupIntoRequestV2),
            context_id=context_id,
            lookup_kind=lookup_kind,
            path_id=path_id,
            query_utf8=query_ptr,
            query_utf8_len=query_len,
            asset_types_csv_utf8=asset_type_ptr,
            asset_types_csv_utf8_len=asset_type_len,
            offset=offset,
            limit=limit,
            flags=flags,
            reserved=0,
            buffer=ctypes.cast(buffer, ctypes.c_void_p) if buffer is not None else None,
            buffer_len=buffer_len,
        )
        into_response = NativeObjectTable()
        into_rc = self.lib.haruki_assetstudio_context_lookup_objects_into_v2(
            ctypes.byref(into_request),
            ctypes.byref(into_response),
        )
        objects = self._take_object_table_objects(into_response, free_buffer=False) if into_response.status == 0 else []
        return size_rc, size_response, into_rc, into_response, objects, buffer

    def _take_object_table_objects(self, response, free_buffer=True):
        objects = []
        try:
            string_data = b""
            if response.string_data and response.string_data_len > 0:
                string_data = ctypes.string_at(response.string_data, response.string_data_len)
            for index in range(max(0, response.returned_count)):
                item = response.objects[index]
                objects.append({
                    "index": item.index,
                    "type_id": item.type_id,
                    "path_id": item.path_id,
                    "size": item.size,
                    "estimated_payload_capacity": item.estimated_payload_capacity,
                    "raw_payload_capacity": item.raw_payload_capacity,
                    "image_payload_capacity": item.image_payload_capacity,
                    "text_payload_capacity": item.text_payload_capacity,
                    "payload_capacity_flags": item.payload_capacity_flags,
                    "name": native_string(string_data, item.name_offset, item.name_len),
                    "container": native_string(string_data, item.container_offset, item.container_len),
                    "type": native_string(string_data, item.type_offset, item.type_len),
                    "unique_id": native_string(string_data, item.unique_id_offset, item.unique_id_len),
                    "source_file": native_string(string_data, item.source_file_offset, item.source_file_len),
                })
            return objects
        finally:
            if free_buffer and response.buffer:
                self.lib.haruki_assetstudio_free_buffer(response.buffer)

    def list_objects_v2_null_request(self):
        response = NativeObjectTable()
        rc = self.lib.haruki_assetstudio_context_list_objects_v2(None, ctypes.byref(response))
        return rc, response

    def read_objects_v3(self, context_id, objects):
        requests, buffers = self._build_read_requests(objects)
        request = NativeObjectReadBatchRequest(
            context_id=context_id,
            items=requests,
            count=len(objects),
            flags=0,
        )
        response = NativeObjectReadBatchResponseV3()
        rc = self.lib.haruki_assetstudio_context_read_objects_v3(ctypes.byref(request), ctypes.byref(response))
        return rc, response

    def read_objects_v2(self, context_id, objects):
        requests, buffers = self._build_read_requests(objects)
        request = NativeObjectReadBatchRequest(
            context_id=context_id,
            items=requests,
            count=len(objects),
            flags=0,
        )
        response = NativeObjectReadBatchResponse()
        rc = self.lib.haruki_assetstudio_context_read_objects_v2(ctypes.byref(request), ctypes.byref(response))
        return rc, response

    def read_objects_v4(self, context_id, objects, shrink_items_by=0, shrink_payload_by=0):
        requests, buffers = self._build_read_requests(objects)
        size_request = NativeObjectReadBatchRequestV4(
            struct_size=ctypes.sizeof(NativeObjectReadBatchRequestV4),
            context_id=context_id,
            items=requests,
            count=len(objects),
            flags=0,
        )
        size_response = NativeObjectReadBatchSizeResponseV4()
        size_rc = self.lib.haruki_assetstudio_context_read_objects_size_v4(
            ctypes.byref(size_request),
            ctypes.byref(size_response),
        )

        items_len = max(0, size_response.required_items_buffer_len - shrink_items_by)
        payload_len = max(0, size_response.required_payload_len - shrink_payload_by)
        items_buffer = (ctypes.c_ubyte * items_len)() if items_len > 0 else None
        payload_buffer = (ctypes.c_ubyte * payload_len)() if payload_len > 0 else None
        into_request = NativeObjectReadBatchIntoRequestV4(
            struct_size=ctypes.sizeof(NativeObjectReadBatchIntoRequestV4),
            context_id=context_id,
            items=requests,
            count=len(objects),
            flags=0,
            items_buffer=ctypes.cast(items_buffer, ctypes.c_void_p) if items_buffer is not None else None,
            items_buffer_len=items_len,
            payload=ctypes.cast(payload_buffer, ctypes.c_void_p) if payload_buffer is not None else None,
            payload_len=payload_len,
        )
        into_response = NativeObjectReadBatchIntoResponseV4()
        into_rc = self.lib.haruki_assetstudio_context_read_objects_into_v4(
            ctypes.byref(into_request),
            ctypes.byref(into_response),
        )
        return size_rc, size_response, into_rc, into_response, items_buffer, payload_buffer

    def read_objects_by_index_v5(self, context_id, objects):
        requests, buffers = self._build_read_by_index_requests(objects)
        size_request = NativeObjectReadBatchByIndexRequestV5(
            struct_size=ctypes.sizeof(NativeObjectReadBatchByIndexRequestV5),
            context_id=context_id,
            items=requests,
            count=len(objects),
            flags=0,
            reserved=0,
        )
        size_response = NativeObjectReadBatchSizeResponseV4()
        size_rc = self.lib.haruki_assetstudio_context_read_objects_by_index_size_v5(
            ctypes.byref(size_request),
            ctypes.byref(size_response),
        )
        items_len = max(0, size_response.required_items_buffer_len)
        payload_len = max(0, size_response.required_payload_len)
        items_buffer = (ctypes.c_ubyte * items_len)() if items_len > 0 else None
        payload_buffer = (ctypes.c_ubyte * payload_len)() if payload_len > 0 else None
        into_request = NativeObjectReadBatchByIndexIntoRequestV5(
            struct_size=ctypes.sizeof(NativeObjectReadBatchByIndexIntoRequestV5),
            context_id=context_id,
            items=requests,
            count=len(objects),
            flags=0,
            reserved=0,
            items_buffer=ctypes.cast(items_buffer, ctypes.c_void_p) if items_buffer is not None else None,
            items_buffer_len=items_len,
            payload=ctypes.cast(payload_buffer, ctypes.c_void_p) if payload_buffer is not None else None,
            payload_len=payload_len,
        )
        into_response = NativeObjectReadBatchIntoResponseV4()
        into_rc = self.lib.haruki_assetstudio_context_read_objects_by_index_into_v5(
            ctypes.byref(into_request),
            ctypes.byref(into_response),
        )
        return size_rc, size_response, into_rc, into_response, items_buffer, payload_buffer

    def read_objects_direct_v6(self, context_id, objects, items_len, payload_len):
        requests, buffers = self._build_read_requests(objects)
        items_buffer = (ctypes.c_ubyte * items_len)() if items_len > 0 else None
        payload_buffer = (ctypes.c_ubyte * payload_len)() if payload_len > 0 else None
        request = NativeObjectReadBatchIntoRequestV4(
            struct_size=ctypes.sizeof(NativeObjectReadBatchIntoRequestV4),
            context_id=context_id,
            items=requests,
            count=len(objects),
            flags=0,
            items_buffer=ctypes.cast(items_buffer, ctypes.c_void_p) if items_buffer is not None else None,
            items_buffer_len=items_len,
            payload=ctypes.cast(payload_buffer, ctypes.c_void_p) if payload_buffer is not None else None,
            payload_len=payload_len,
        )
        response = NativeObjectReadBatchIntoResponseV4()
        rc = self.lib.haruki_assetstudio_context_read_objects_direct_into_v6(
            ctypes.byref(request),
            ctypes.byref(response),
        )
        return rc, response, items_buffer, payload_buffer

    def read_objects_by_index_direct_v6(self, context_id, objects, items_len, payload_len):
        requests, buffers = self._build_read_by_index_requests(objects)
        items_buffer = (ctypes.c_ubyte * items_len)() if items_len > 0 else None
        payload_buffer = (ctypes.c_ubyte * payload_len)() if payload_len > 0 else None
        request = NativeObjectReadBatchByIndexIntoRequestV5(
            struct_size=ctypes.sizeof(NativeObjectReadBatchByIndexIntoRequestV5),
            context_id=context_id,
            items=requests,
            count=len(objects),
            flags=0,
            reserved=0,
            items_buffer=ctypes.cast(items_buffer, ctypes.c_void_p) if items_buffer is not None else None,
            items_buffer_len=items_len,
            payload=ctypes.cast(payload_buffer, ctypes.c_void_p) if payload_buffer is not None else None,
            payload_len=payload_len,
        )
        response = NativeObjectReadBatchIntoResponseV4()
        rc = self.lib.haruki_assetstudio_context_read_objects_by_index_direct_into_v6(
            ctypes.byref(request),
            ctypes.byref(response),
        )
        return rc, response, items_buffer, payload_buffer

    def read_objects_direct_retry_v7(self, context_id, objects, items_len, payload_len):
        requests, buffers = self._build_read_requests(objects)
        items_buffer = (ctypes.c_ubyte * items_len)() if items_len > 0 else None
        payload_buffer = (ctypes.c_ubyte * payload_len)() if payload_len > 0 else None
        request = NativeObjectReadBatchIntoRequestV4(
            struct_size=ctypes.sizeof(NativeObjectReadBatchIntoRequestV4),
            context_id=context_id,
            items=requests,
            count=len(objects),
            flags=0,
            items_buffer=ctypes.cast(items_buffer, ctypes.c_void_p) if items_buffer is not None else None,
            items_buffer_len=items_len,
            payload=ctypes.cast(payload_buffer, ctypes.c_void_p) if payload_buffer is not None else None,
            payload_len=payload_len,
        )
        response = NativeObjectReadBatchRetryResponseV7()
        rc = self.lib.haruki_assetstudio_context_read_objects_direct_retry_v7(
            ctypes.byref(request),
            ctypes.byref(response),
        )
        return rc, response, items_buffer, payload_buffer

    def read_objects_by_index_direct_retry_v7(self, context_id, objects, items_len, payload_len):
        requests, buffers = self._build_read_by_index_requests(objects)
        items_buffer = (ctypes.c_ubyte * items_len)() if items_len > 0 else None
        payload_buffer = (ctypes.c_ubyte * payload_len)() if payload_len > 0 else None
        request = NativeObjectReadBatchByIndexIntoRequestV5(
            struct_size=ctypes.sizeof(NativeObjectReadBatchByIndexIntoRequestV5),
            context_id=context_id,
            items=requests,
            count=len(objects),
            flags=0,
            reserved=0,
            items_buffer=ctypes.cast(items_buffer, ctypes.c_void_p) if items_buffer is not None else None,
            items_buffer_len=items_len,
            payload=ctypes.cast(payload_buffer, ctypes.c_void_p) if payload_buffer is not None else None,
            payload_len=payload_len,
        )
        response = NativeObjectReadBatchRetryResponseV7()
        rc = self.lib.haruki_assetstudio_context_read_objects_by_index_direct_retry_v7(
            ctypes.byref(request),
            ctypes.byref(response),
        )
        return rc, response, items_buffer, payload_buffer

    def read_objects_v4_too_small_then_retry(self, context_id, objects):
        requests, buffers = self._build_read_requests(objects)
        size_request = NativeObjectReadBatchRequestV4(
            struct_size=ctypes.sizeof(NativeObjectReadBatchRequestV4),
            context_id=context_id,
            items=requests,
            count=len(objects),
            flags=0,
        )
        size_response = NativeObjectReadBatchSizeResponseV4()
        size_rc = self.lib.haruki_assetstudio_context_read_objects_size_v4(
            ctypes.byref(size_request),
            ctypes.byref(size_response),
        )

        small_items_len = size_response.required_items_buffer_len
        small_payload_len = max(0, size_response.required_payload_len - 1)
        small_items_buffer = (ctypes.c_ubyte * small_items_len)() if small_items_len > 0 else None
        small_payload_buffer = (ctypes.c_ubyte * small_payload_len)() if small_payload_len > 0 else None
        small_request = NativeObjectReadBatchIntoRequestV4(
            struct_size=ctypes.sizeof(NativeObjectReadBatchIntoRequestV4),
            context_id=context_id,
            items=requests,
            count=len(objects),
            flags=0,
            items_buffer=ctypes.cast(small_items_buffer, ctypes.c_void_p) if small_items_buffer is not None else None,
            items_buffer_len=small_items_len,
            payload=ctypes.cast(small_payload_buffer, ctypes.c_void_p) if small_payload_buffer is not None else None,
            payload_len=small_payload_len,
        )
        small_response = NativeObjectReadBatchIntoResponseV4()
        small_rc = self.lib.haruki_assetstudio_context_read_objects_into_v4(
            ctypes.byref(small_request),
            ctypes.byref(small_response),
        )

        retry_items_len = size_response.required_items_buffer_len
        retry_payload_len = size_response.required_payload_len
        retry_items_buffer = (ctypes.c_ubyte * retry_items_len)() if retry_items_len > 0 else None
        retry_payload_buffer = (ctypes.c_ubyte * retry_payload_len)() if retry_payload_len > 0 else None
        retry_request = NativeObjectReadBatchIntoRequestV4(
            struct_size=ctypes.sizeof(NativeObjectReadBatchIntoRequestV4),
            context_id=context_id,
            items=requests,
            count=len(objects),
            flags=0,
            items_buffer=ctypes.cast(retry_items_buffer, ctypes.c_void_p) if retry_items_buffer is not None else None,
            items_buffer_len=retry_items_len,
            payload=ctypes.cast(retry_payload_buffer, ctypes.c_void_p) if retry_payload_buffer is not None else None,
            payload_len=retry_payload_len,
        )
        retry_response = NativeObjectReadBatchIntoResponseV4()
        retry_rc = self.lib.haruki_assetstudio_context_read_objects_into_v4(
            ctypes.byref(retry_request),
            ctypes.byref(retry_response),
        )
        return (
            size_rc,
            size_response,
            small_rc,
            small_response,
            retry_rc,
            retry_response,
            retry_items_buffer,
            retry_payload_buffer,
        )

    def _build_read_requests(self, objects):
        buffers = []

        def native_bytes(value):
            raw = value.encode("utf-8")
            buffer = (ctypes.c_ubyte * len(raw)).from_buffer_copy(raw)
            buffers.append(buffer)
            return buffer, len(raw)

        requests = (NativeObjectReadItemRequest * len(objects))()
        for index, item in enumerate(objects):
            kind, kind_len = native_bytes(item.get("kind", "auto"))
            image_format, image_format_len = native_bytes(item.get("image_format", "raw_rgba"))
            requests[index] = NativeObjectReadItemRequest(
                path_id=item["path_id"],
                kind_utf8=kind,
                kind_utf8_len=kind_len,
                image_format_utf8=image_format,
                image_format_utf8_len=image_format_len,
            )
        return requests, buffers

    def _build_read_by_index_requests(self, objects):
        buffers = []

        def native_bytes(value):
            raw = value.encode("utf-8")
            buffer = (ctypes.c_ubyte * len(raw)).from_buffer_copy(raw)
            buffers.append(buffer)
            return buffer, len(raw)

        requests = (NativeObjectReadItemByIndexRequestV5 * len(objects))()
        for index, item in enumerate(objects):
            kind, kind_len = native_bytes(item.get("kind", "auto"))
            image_format, image_format_len = native_bytes(item.get("image_format", "raw_rgba"))
            requests[index] = NativeObjectReadItemByIndexRequestV5(
                object_index=item["object_index"],
                kind_utf8=kind,
                kind_utf8_len=kind_len,
                image_format_utf8=image_format,
                image_format_utf8_len=image_format_len,
            )
        return requests, buffers


def native_string(string_data, offset, length):
    if length <= 0:
        return ""
    return string_data[offset:offset + length].decode("utf-8")


def assert_eq(actual, expected, label):
    if actual != expected:
        raise AssertionError(f"{label}: expected {expected!r}, got {actual!r}")


def assert_true(value, label):
    if not value:
        raise AssertionError(f"{label}: expected truthy value, got {value!r}")


def assert_error(response, error_code, label):
    assert_eq(response.get("success"), False, f"{label}.success")
    assert_eq(response.get("error_code"), error_code, f"{label}.error_code")
    assert_eq(response.get("abi_version"), 1, f"{label}.abi_version")
    assert_true(response.get("schema_version", 0) >= 2, f"{label}.schema_version")


def assert_abi_layout(layout):
    expected = {
        "context_open_request": ctypes.sizeof(NativeContextOpenRequest),
        "context_open_response": ctypes.sizeof(NativeContextOpenResponse),
        "context_close_request": ctypes.sizeof(NativeContextCloseRequest),
        "context_close_response": ctypes.sizeof(NativeContextCloseResponse),
        "limits_response": ctypes.sizeof(NativeLimitsResponse),
        "capabilities_response": ctypes.sizeof(NativeCapabilitiesResponse),
        "object_list_request": ctypes.sizeof(NativeObjectListRequest),
        "object_list_into_request_v3": ctypes.sizeof(NativeObjectListIntoRequestV3),
        "object_table": ctypes.sizeof(NativeObjectTable),
        "asset_object": ctypes.sizeof(NativeAssetObject),
        "object_read_item_request": ctypes.sizeof(NativeObjectReadItemRequest),
        "object_read_batch_into_request_v4": ctypes.sizeof(NativeObjectReadBatchIntoRequestV4),
        "object_read_item_response_v4": ctypes.sizeof(NativeObjectReadItemResponseV4),
        "object_read_batch_retry_response_v7": ctypes.sizeof(NativeObjectReadBatchRetryResponseV7),
    }
    for name, size in expected.items():
        assert_eq(getattr(layout, name), size, f"abi_layout.struct_size.{name}")


def parse_hapb_v2(payload):
    if len(payload) < 20:
        raise AssertionError(f"payload bundle too short: {len(payload)}")
    magic, version, header_len, entry_count, payload_data_bytes = struct.unpack_from("<IHHiq", payload, 0)
    assert_eq(magic, 0x42504148, "payload.magic")
    assert_eq(version, 2, "payload.version")
    assert_eq(header_len, 20, "payload.header_len")
    offset = header_len
    entries = []
    total_payload = 0
    for _ in range(entry_count):
        name_len, data_len = struct.unpack_from("<iq", payload, offset)
        offset += 12
        name = payload[offset:offset + name_len].decode("utf-8")
        offset += name_len
        data = payload[offset:offset + data_len]
        offset += data_len
        total_payload += data_len
        entries.append((name, data))
    assert_eq(offset, len(payload), "payload.consumed_len")
    assert_eq(total_payload, payload_data_bytes, "payload.payload_data_bytes")
    return entries


def main():
    parser = argparse.ArgumentParser(description="Haruki AssetStudio Native FFI contract smoke test")
    parser.add_argument("library", help="Path to HarukiAssetStudioFFI shared library")
    parser.add_argument("input_path", help="Unity asset bundle/file/directory input path")
    parser.add_argument("--unity-version", default="2022.3.62f1")
    parser.add_argument("--list-limit", type=int, default=8)
    args = parser.parse_args()

    native = HarukiAssetStudioNative(args.library)
    opened_context = None
    opened_context2 = None

    try:
        caps = NativeCapabilitiesResponse()
        rc = native.lib.haruki_assetstudio_capabilities_v2(ctypes.byref(caps))
        assert_eq(rc, 0, "capabilities.rc")
        assert_eq(caps.struct_size, ctypes.sizeof(NativeCapabilitiesResponse), "capabilities.struct_size")
        assert_eq(caps.abi_version, 1, "capabilities.abi_version")
        assert_true(caps.schema_version >= 2, "capabilities.schema_version")
        assert_eq(caps.status, 0, "capabilities.status")
        assert_eq(caps.error_code, 0, "capabilities.error_code")
        assert_eq(caps.core_api_version_major, 1, "capabilities.core_api_version_major")
        assert_eq(caps.core_api_version_minor, 0, "capabilities.core_api_version_minor")
        assert_eq(caps.context_abi_version, 1, "capabilities.context_abi_version")
        assert_eq(caps.object_table_abi_version, 3, "capabilities.object_table_abi_version")
        assert_eq(caps.object_table_into_abi_version, 3, "capabilities.object_table_into_abi_version")
        assert_eq(caps.object_lookup_abi_version, 1, "capabilities.object_lookup_abi_version")
        assert_eq(caps.object_lookup_into_abi_version, 1, "capabilities.object_lookup_into_abi_version")
        assert_eq(caps.object_read_batch_into_abi_version, 1, "capabilities.object_read_batch_into_abi_version")
        assert_eq(caps.object_read_batch_by_index_abi_version, 1, "capabilities.object_read_batch_by_index_abi_version")
        assert_eq(caps.object_read_batch_direct_into_abi_version, 2, "capabilities.object_read_batch_direct_into_abi_version")
        assert_eq(caps.object_read_batch_direct_retry_abi_version, 1, "capabilities.object_read_batch_direct_retry_abi_version")
        assert_eq(caps.supports_typed_object_table, 1, "capabilities.supports_typed_object_table")
        assert_eq(caps.supports_caller_provided_object_table_buffers, 1, "capabilities.supports_caller_provided_object_table_buffers")
        assert_eq(caps.supports_typed_object_lookup, 1, "capabilities.supports_typed_object_lookup")
        assert_eq(caps.supports_caller_provided_object_lookup_buffers, 1, "capabilities.supports_caller_provided_object_lookup_buffers")
        assert_eq(caps.supports_typed_object_read_batch, 1, "capabilities.supports_typed_object_read_batch")
        assert_eq(caps.supports_result_handle, 1, "capabilities.supports_result_handle")
        assert_eq(caps.supports_direct_object_read_retry, 1, "capabilities.supports_direct_object_read_retry")
        assert_eq(caps.supports_native_dependency_resolver, 1, "capabilities.supports_native_dependency_resolver")
        assert_eq(caps.supports_abi_layout, 1, "capabilities.supports_abi_layout")
        assert_eq(caps.supports_multiple_contexts, 1, "capabilities.supports_multiple_contexts")
        assert_eq(caps.supports_concurrent_operations, 1, "capabilities.supports_concurrent_operations")
        assert_eq(caps.supports_context_lifetime_guards, 1, "capabilities.supports_context_lifetime_guards")
        assert_eq(caps.native_console_capture, 0, "capabilities.native_console_capture")

        limits = NativeLimitsResponse()
        rc = native.lib.haruki_assetstudio_limits_v1(ctypes.byref(limits))
        assert_eq(rc, 0, "limits_v1.rc")
        assert_eq(limits.struct_size, ctypes.sizeof(NativeLimitsResponse), "limits_v1.struct_size")
        assert_eq(limits.abi_version, 1, "limits_v1.abi_version")
        assert_true(limits.schema_version >= 2, "limits_v1.schema_version")
        assert_eq(limits.limits_abi_version, 1, "limits_v1.limits_abi_version")
        assert_eq(limits.status, 0, "limits_v1.status")
        assert_eq(limits.error_code, 0, "limits_v1.error_code")
        assert_true(limits.max_native_utf8_bytes >= 1024, "limits_v1.max_native_utf8_bytes")
        assert_true(limits.max_object_read_batch_count >= 1, "limits_v1.max_object_read_batch_count")
        assert_true(limits.max_object_table_page_limit >= 1, "limits_v1.max_object_table_page_limit")
        assert_true(limits.max_object_read_batch_payload_bytes >= 1, "limits_v1.max_object_read_batch_payload_bytes")
        assert_true(limits.max_cached_object_read_batch_payload_bytes >= 1, "limits_v1.max_cached_object_read_batch_payload_bytes")
        assert_true(limits.max_active_contexts >= 2, "limits_v1.max_active_contexts")
        assert_true(limits.max_concurrent_operations >= 1, "limits_v1.max_concurrent_operations")
        assert_eq(limits.supports_multiple_contexts, 1, "limits_v1.supports_multiple_contexts")
        assert_eq(limits.supports_concurrent_operations, 1, "limits_v1.supports_concurrent_operations")
        assert_eq(limits.legacy_static_engine, 0, "limits_v1.legacy_static_engine")
        assert_eq(limits.native_console_capture, 0, "limits_v1.native_console_capture")

        abi_layout = NativeAbiLayoutResponse()
        rc = native.lib.haruki_assetstudio_abi_layout_v2(ctypes.byref(abi_layout))
        assert_eq(rc, 0, "abi_layout.rc")
        assert_eq(abi_layout.struct_size, ctypes.sizeof(NativeAbiLayoutResponse), "abi_layout.struct_size")
        assert_eq(abi_layout.abi_version, 1, "abi_layout.abi_version")
        assert_true(abi_layout.schema_version >= 2, "abi_layout.schema_version")
        assert_eq(abi_layout.status, 0, "abi_layout.status")
        assert_eq(abi_layout.error_code, 0, "abi_layout.error_code")
        assert_eq(abi_layout.layout_version, 2, "abi_layout.version")
        assert_abi_layout(abi_layout)

        rc, missing_close = native.close_v2(987654321)
        assert_eq(rc, 4, "close_missing.rc")
        assert_eq(missing_close.status, 4, "close_missing.status")
        assert_eq(missing_close.error_code, 4, "close_missing.error_code")

        rc, null_typed_table = native.list_objects_v2_null_request()
        assert_eq(rc, 1, "list_v2_null_request.rc")
        assert_eq(null_typed_table.status, 1, "list_v2_null_request.status")
        assert_eq(null_typed_table.error_code, 1, "list_v2_null_request.error_code")
        assert_eq(null_typed_table.abi_version, 1, "list_v2_null_request.abi_version")
        assert_true(null_typed_table.schema_version >= 2, "list_v2_null_request.schema_version")
        assert_eq(null_typed_table.object_table_abi_version, 3, "list_v2_null_request.object_table_abi_version")

        too_many_read_request = NativeObjectReadBatchRequestV4(
            struct_size=ctypes.sizeof(NativeObjectReadBatchRequestV4),
            context_id=0,
            items=None,
            count=limits.max_object_read_batch_count + 1,
            flags=0,
            reserved=0,
        )
        too_many_read_response = NativeObjectReadBatchSizeResponseV4()
        rc = native.lib.haruki_assetstudio_context_read_objects_size_v4(
            ctypes.byref(too_many_read_request),
            ctypes.byref(too_many_read_response),
        )
        assert_eq(rc, 2, "read_objects_size_v4_too_many.rc")
        assert_eq(too_many_read_response.status, 2, "read_objects_size_v4_too_many.status")
        assert_eq(too_many_read_response.error_code, 2, "read_objects_size_v4_too_many.error_code")

        rc, opened_v2, opened_unity_version = native.open_v2(args.input_path, args.unity_version)
        assert_eq(rc, 0, "open_v2.rc")
        assert_eq(opened_v2.status, 0, "open_v2.status")
        assert_eq(opened_v2.error_code, 0, "open_v2.error_code")
        assert_eq(opened_v2.struct_size, ctypes.sizeof(NativeContextOpenResponse), "open_v2.struct_size")
        assert_eq(opened_v2.abi_version, 1, "open_v2.abi_version")
        assert_true(opened_v2.schema_version >= 2, "open_v2.schema_version")
        assert_eq(opened_v2.context_abi_version, 1, "open_v2.context_abi_version")
        opened_context = opened_v2.context_id
        assert_true(opened_context, "open.context_id")
        assert_eq(opened_unity_version, args.unity_version, "open_v2.unity_version")
        assert_true(opened_v2.object_index_count > 0, "open_v2.object_index_count")
        assert_true(opened_v2.exportable_asset_count > 0, "open_v2.exportable_asset_count")
        assert_eq(opened_v2.has_more_assets, 1, "open_v2.has_more_assets")

        rc, second_open_v2, _ = native.open_v2(args.input_path, args.unity_version)
        assert_eq(rc, 0, "second_open_v2.rc")
        assert_eq(second_open_v2.status, 0, "second_open_v2.status")
        assert_eq(second_open_v2.error_code, 0, "second_open_v2.error_code")
        opened_context2 = second_open_v2.context_id
        assert_true(opened_context2 and opened_context2 != opened_context, "second_open_v2.context_id")
        assert_eq(second_open_v2.object_index_count, opened_v2.object_index_count, "second_open_v2.object_index_count")

        rc, second_typed_table, second_typed_assets = native.list_objects_v2(opened_context2, offset=0, limit=args.list_limit)
        assert_eq(rc, 0, "second_list_v2.rc")
        assert_eq(second_typed_table.status, 0, "second_list_v2.status")
        assert_eq(second_typed_table.error_code, 0, "second_list_v2.error_code")
        assert_true(len(second_typed_assets) > 0, "second_list_v2.assets")

        rc, second_closed = native.close_v2(opened_context2)
        opened_context2 = None
        assert_eq(rc, 0, "second_close_v2.rc")
        assert_eq(second_closed.status, 0, "second_close_v2.status")

        too_large_limit = limits.max_object_table_page_limit + 1
        rc, too_large_list_table, _ = native.list_objects_v2(opened_context, offset=0, limit=too_large_limit)
        assert_eq(rc, 2, "list_v2_too_large_limit.rc")
        assert_eq(too_large_list_table.status, 2, "list_v2_too_large_limit.status")
        assert_eq(too_large_list_table.error_code, 2, "list_v2_too_large_limit.error_code")

        rc, typed_table, typed_assets = native.list_objects_v2(opened_context, offset=0, limit=args.list_limit)
        assert_eq(rc, 0, "list_v2.rc")
        assert_eq(typed_table.status, 0, "list_v2.status")
        assert_eq(typed_table.error_code, 0, "list_v2.error_code")
        assert_eq(typed_table.struct_size, ctypes.sizeof(NativeObjectTable), "list_v2.struct_size")
        assert_eq(typed_table.abi_version, 1, "list_v2.abi_version")
        assert_true(typed_table.schema_version >= 2, "list_v2.schema_version")
        assert_eq(typed_table.object_table_abi_version, 3, "list_v2.object_table_abi_version")
        assert_true(typed_table.total_count >= typed_table.returned_count, "list_v2.total_count")
        assert_eq(typed_table.returned_count, len(typed_assets), "list_v2.returned_count")
        assert_true(len(typed_assets) > 0, "list_v2.assets_len")
        assert_true(typed_assets[0]["estimated_payload_capacity"] >= 0, "list_v2.first.estimated_payload_capacity")

        size_rc, size_table_v3, into_rc, typed_table_v3, typed_assets_v3, _ = native.list_objects_v3(
            opened_context,
            offset=0,
            limit=args.list_limit,
        )
        assert_eq(size_rc, 0, "list_size_v3.rc")
        assert_eq(size_table_v3.status, 0, "list_size_v3.status")
        assert_eq(size_table_v3.error_code, 0, "list_size_v3.error_code")
        assert_eq(size_table_v3.buffer_len, typed_table.buffer_len, "list_size_v3.buffer_len")
        assert_eq(size_table_v3.string_data_len, typed_table.string_data_len, "list_size_v3.string_data_len")
        assert_eq(size_table_v3.returned_count, typed_table.returned_count, "list_size_v3.returned_count")
        assert_eq(into_rc, 0, "list_into_v3.rc")
        assert_eq(typed_table_v3.status, 0, "list_into_v3.status")
        assert_eq(typed_table_v3.error_code, 0, "list_into_v3.error_code")
        assert_eq(typed_table_v3.buffer_len, size_table_v3.buffer_len, "list_into_v3.buffer_len")
        assert_eq(typed_table_v3.string_data_len, size_table_v3.string_data_len, "list_into_v3.string_data_len")
        assert_eq(len(typed_assets_v3), len(typed_assets), "list_into_v3.assets_len")
        assert_eq(typed_assets_v3[0]["path_id"], typed_assets[0]["path_id"], "list_into_v3.first.path_id")
        assert_eq(typed_assets_v3[0]["type"], typed_assets[0]["type"], "list_into_v3.first.type")

        if size_table_v3.buffer_len > 0:
            _, _, too_small_rc, too_small_table_v3, _, _ = native.list_objects_v3(
                opened_context,
                offset=0,
                limit=args.list_limit,
                shrink_buffer_by=1,
            )
            assert_eq(too_small_rc, 8, "list_into_v3_too_small.rc")
            assert_eq(too_small_table_v3.status, 8, "list_into_v3_too_small.status")
            assert_eq(too_small_table_v3.error_code, 8, "list_into_v3_too_small.error_code")
            assert_eq(too_small_table_v3.buffer_len, size_table_v3.buffer_len, "list_into_v3_too_small.required_buffer_len")

        rc, lookup_table, lookup_assets = native.lookup_objects_v1(
            opened_context,
            lookup_kind=1,
            path_id=typed_assets[0]["path_id"],
            limit=4,
        )
        assert_eq(rc, 0, "lookup_v1_path.rc")
        assert_eq(lookup_table.status, 0, "lookup_v1_path.status")
        assert_eq(lookup_table.error_code, 0, "lookup_v1_path.error_code")
        assert_eq(lookup_table.struct_size, ctypes.sizeof(NativeObjectTable), "lookup_v1_path.struct_size")
        assert_eq(lookup_table.object_table_abi_version, 3, "lookup_v1_path.object_table_abi_version")
        assert_eq(lookup_table.total_count, 1, "lookup_v1_path.total_count")
        assert_eq(len(lookup_assets), 1, "lookup_v1_path.assets_len")
        assert_eq(lookup_assets[0]["path_id"], typed_assets[0]["path_id"], "lookup_v1_path.path_id")

        rc, too_large_lookup_table, _ = native.lookup_objects_v1(
            opened_context,
            lookup_kind=1,
            path_id=typed_assets[0]["path_id"],
            limit=too_large_limit,
        )
        assert_eq(rc, 2, "lookup_v1_too_large_limit.rc")
        assert_eq(too_large_lookup_table.status, 2, "lookup_v1_too_large_limit.status")
        assert_eq(too_large_lookup_table.error_code, 2, "lookup_v1_too_large_limit.error_code")

        lookup_size_rc, lookup_size_v2, lookup_into_rc, lookup_table_v2, lookup_assets_v2, _ = native.lookup_objects_v2(
            opened_context,
            lookup_kind=1,
            path_id=typed_assets[0]["path_id"],
            limit=4,
        )
        assert_eq(lookup_size_rc, 0, "lookup_size_v2_path.rc")
        assert_eq(lookup_size_v2.status, 0, "lookup_size_v2_path.status")
        assert_eq(lookup_size_v2.error_code, 0, "lookup_size_v2_path.error_code")
        assert_eq(lookup_size_v2.buffer_len, lookup_table.buffer_len, "lookup_size_v2_path.buffer_len")
        assert_eq(lookup_size_v2.string_data_len, lookup_table.string_data_len, "lookup_size_v2_path.string_data_len")
        assert_eq(lookup_into_rc, 0, "lookup_into_v2_path.rc")
        assert_eq(lookup_table_v2.status, 0, "lookup_into_v2_path.status")
        assert_eq(lookup_table_v2.error_code, 0, "lookup_into_v2_path.error_code")
        assert_eq(lookup_table_v2.total_count, lookup_table.total_count, "lookup_into_v2_path.total_count")
        assert_eq(len(lookup_assets_v2), len(lookup_assets), "lookup_into_v2_path.assets_len")
        assert_eq(lookup_assets_v2[0]["path_id"], lookup_assets[0]["path_id"], "lookup_into_v2_path.path_id")

        if lookup_size_v2.buffer_len > 0:
            _, _, lookup_small_rc, lookup_small_v2, _, _ = native.lookup_objects_v2(
                opened_context,
                lookup_kind=1,
                path_id=typed_assets[0]["path_id"],
                limit=4,
                shrink_buffer_by=1,
            )
            assert_eq(lookup_small_rc, 8, "lookup_into_v2_too_small.rc")
            assert_eq(lookup_small_v2.status, 8, "lookup_into_v2_too_small.status")
            assert_eq(lookup_small_v2.error_code, 8, "lookup_into_v2_too_small.error_code")
            assert_eq(lookup_small_v2.buffer_len, lookup_size_v2.buffer_len, "lookup_into_v2_too_small.required_buffer_len")

        rc, missing_lookup_table, missing_lookup_assets = native.lookup_objects_v1(
            opened_context,
            lookup_kind=1,
            path_id=-9223372036854775808,
            limit=4,
        )
        assert_eq(rc, 0, "lookup_v1_missing_path.rc")
        assert_eq(missing_lookup_table.status, 0, "lookup_v1_missing_path.status")
        assert_eq(missing_lookup_table.total_count, 0, "lookup_v1_missing_path.total_count")
        assert_eq(len(missing_lookup_assets), 0, "lookup_v1_missing_path.assets_len")

        rc, invalid_lookup_table, _ = native.lookup_objects_v1(
            opened_context,
            lookup_kind=99,
            query="Texture2D",
            limit=1,
        )
        assert_eq(rc, 2, "lookup_v1_invalid_kind.rc")
        assert_eq(invalid_lookup_table.status, 2, "lookup_v1_invalid_kind.status")
        assert_eq(invalid_lookup_table.error_code, 2, "lookup_v1_invalid_kind.error_code")

        rc, type_lookup_table, type_lookup_assets = native.lookup_objects_v1(
            opened_context,
            lookup_kind=4,
            query=typed_assets[0]["type"],
            limit=1,
        )
        assert_eq(rc, 0, "lookup_v1_type.rc")
        assert_eq(type_lookup_table.status, 0, "lookup_v1_type.status")
        assert_true(type_lookup_table.total_count >= 1, "lookup_v1_type.total_count")
        assert_eq(len(type_lookup_assets), 1, "lookup_v1_type.assets_len")
        assert_eq(type_lookup_assets[0]["type"], typed_assets[0]["type"], "lookup_v1_type.type")

        type_fragment = typed_assets[0]["type"][: max(1, min(4, len(typed_assets[0]["type"])))]
        rc, contains_lookup_table, contains_lookup_assets = native.lookup_objects_v1(
            opened_context,
            lookup_kind=4,
            query=type_fragment.lower(),
            limit=1,
            flags=1,
        )
        assert_eq(rc, 0, "lookup_v1_type_contains.rc")
        assert_eq(contains_lookup_table.status, 0, "lookup_v1_type_contains.status")
        assert_true(contains_lookup_table.total_count >= 1, "lookup_v1_type_contains.total_count")
        assert_eq(len(contains_lookup_assets), 1, "lookup_v1_type_contains.assets_len")

        read_asset = None
        read_filter = None
        for candidate_type in ("TextAsset", "MonoBehaviour", "Shader", "Font"):
            rc, typed_filtered_table, typed_filtered_assets = native.list_objects_v2(
                opened_context,
                offset=0,
                limit=1,
                asset_types_csv=candidate_type,
            )
            assert_eq(rc, 0, f"list_v2_filter.{candidate_type}.rc")
            assert_eq(typed_filtered_table.status, 0, f"list_v2_filter.{candidate_type}.status")
            if typed_filtered_assets:
                assert_eq(len(typed_filtered_assets), 1, f"list_v2_filter.{candidate_type}.assets_len")
                assert_eq(typed_filtered_assets[0]["type"], candidate_type, f"list_v2_filter.{candidate_type}.type")
                read_asset = typed_filtered_assets[0]
                read_filter = candidate_type
                break

        if read_asset is None:
            read_asset = typed_assets[0]
            read_filter = read_asset.get("type")

        first_path_id = read_asset.get("path_id")
        assert_true(first_path_id is not None, "read_asset.path_id")
        default_read_kind = "raw" if read_asset.get("type") == "Texture2D" else "auto"

        rc, read_v2 = native.read_objects_v2(
            opened_context,
            [{"path_id": first_path_id, "kind": default_read_kind, "image_format": "raw_rgba"}],
        )
        assert_eq(rc, 0, "read_objects_v2.rc")
        assert_eq(read_v2.status, 0, "read_objects_v2.status")
        assert_eq(read_v2.error_code, 0, "read_objects_v2.error_code")
        assert_eq(read_v2.abi_version, 1, "read_objects_v2.abi_version")
        assert_eq(read_v2.object_read_batch_abi_version, 1, "read_objects_v2.batch_abi_version")
        assert_eq(read_v2.returned_count, 1, "read_objects_v2.returned_count")
        assert_eq(read_v2.failed_count, 0, "read_objects_v2.failed_count")
        assert_true(read_v2.payload_len > 0, "read_objects_v2.payload_len")
        assert_eq(read_v2.items[0].status, 0, "read_objects_v2.item.status")
        assert_eq(read_v2.items[0].payload_offset, 0, "read_objects_v2.item.payload_offset")
        assert_eq(read_v2.items[0].payload_len, read_v2.payload_len, "read_objects_v2.item.payload_len")
        read_v2_payload = ctypes.string_at(read_v2.payload, read_v2.payload_len)
        if read_v2.items_buffer:
            native.lib.haruki_assetstudio_free_buffer(read_v2.items_buffer)
        if read_v2.payload:
            native.lib.haruki_assetstudio_free_buffer(read_v2.payload)

        rc, read_v3 = native.read_objects_v3(
            opened_context,
            [{"path_id": first_path_id, "kind": default_read_kind, "image_format": "raw_rgba"}],
        )
        assert_eq(rc, 0, "read_objects_v3.rc")
        assert_eq(read_v3.status, 0, "read_objects_v3.status")
        assert_eq(read_v3.error_code, 0, "read_objects_v3.error_code")
        assert_eq(read_v3.abi_version, 1, "read_objects_v3.abi_version")
        assert_eq(read_v3.object_read_batch_abi_version, 1, "read_objects_v3.batch_abi_version")
        assert_eq(read_v3.object_read_batch_handle_abi_version, 1, "read_objects_v3.handle_abi_version")
        assert_eq(read_v3.returned_count, 1, "read_objects_v3.returned_count")
        assert_eq(read_v3.failed_count, 0, "read_objects_v3.failed_count")
        assert_true(read_v3.payload_len > 0, "read_objects_v3.payload_len")
        assert_eq(read_v3.items[0].status, 0, "read_objects_v3.item.status")
        assert_eq(read_v3.items[0].payload_offset, 0, "read_objects_v3.item.payload_offset")
        assert_eq(read_v3.items[0].payload_len, read_v3.payload_len, "read_objects_v3.item.payload_len")
        assert_true(read_v3.result_handle > 0, "read_objects_v3.result_handle")
        read_v3_payload = ctypes.string_at(read_v3.payload, read_v3.payload_len)
        assert_eq(read_v2_payload, read_v3_payload, "read_objects_v2.payload")
        assert_eq(native.lib.haruki_assetstudio_result_free(read_v3.result_handle), 0, "read_objects_v3.free")
        assert_eq(native.lib.haruki_assetstudio_result_free(read_v3.result_handle), 4, "read_objects_v3.double_free")

        size_rc, size_v4, into_rc, read_v4, items_buffer_v4, payload_buffer_v4 = native.read_objects_v4(
            opened_context,
            [{"path_id": first_path_id, "kind": default_read_kind, "image_format": "raw_rgba"}],
        )
        assert_eq(size_rc, 0, "read_objects_size_v4.rc")
        assert_eq(size_v4.struct_size, ctypes.sizeof(NativeObjectReadBatchSizeResponseV4), "read_objects_size_v4.struct_size")
        assert_eq(size_v4.status, 0, "read_objects_size_v4.status")
        assert_eq(size_v4.object_read_batch_into_abi_version, 1, "read_objects_size_v4.into_abi_version")
        assert_true(size_v4.required_items_buffer_len >= ctypes.sizeof(NativeObjectReadItemResponseV4), "read_objects_size_v4.items_buffer_len")
        assert_eq(size_v4.required_payload_len, len(read_v3_payload), "read_objects_size_v4.payload_len")
        assert_eq(into_rc, 0, "read_objects_into_v4.rc")
        assert_eq(read_v4.struct_size, ctypes.sizeof(NativeObjectReadBatchIntoResponseV4), "read_objects_into_v4.struct_size")
        assert_eq(read_v4.status, 0, "read_objects_into_v4.status")
        assert_eq(read_v4.error_code, 0, "read_objects_into_v4.error_code")
        assert_eq(read_v4.returned_count, 1, "read_objects_into_v4.returned_count")
        assert_eq(read_v4.failed_count, 0, "read_objects_into_v4.failed_count")
        assert_eq(read_v4.required_items_buffer_len, size_v4.required_items_buffer_len, "read_objects_into_v4.required_items_buffer_len")
        assert_eq(read_v4.required_payload_len, size_v4.required_payload_len, "read_objects_into_v4.required_payload_len")
        assert_eq(read_v4.items[0].payload_offset, 0, "read_objects_into_v4.item.payload_offset")
        assert_eq(read_v4.items[0].payload_len, read_v4.payload_len, "read_objects_into_v4.item.payload_len")
        assert_eq(read_v4.items[0].error_message_len, 0, "read_objects_into_v4.item.error_message_len")
        read_v4_payload = ctypes.string_at(read_v4.payload, read_v4.payload_len)
        assert_eq(read_v4_payload, read_v3_payload, "read_objects_into_v4.payload")

        size_index_rc, size_index_v5, into_index_rc, read_index_v5, _, _ = native.read_objects_by_index_v5(
            opened_context,
            [{"object_index": read_asset.get("index"), "kind": default_read_kind, "image_format": "raw_rgba"}],
        )
        assert_eq(size_index_rc, 0, "read_objects_by_index_size_v5.rc")
        assert_eq(size_index_v5.status, 0, "read_objects_by_index_size_v5.status")
        assert_eq(size_index_v5.required_payload_len, len(read_v3_payload), "read_objects_by_index_size_v5.payload_len")
        assert_eq(into_index_rc, 0, "read_objects_by_index_into_v5.rc")
        assert_eq(read_index_v5.status, 0, "read_objects_by_index_into_v5.status")
        assert_eq(read_index_v5.returned_count, 1, "read_objects_by_index_into_v5.returned_count")
        assert_eq(read_index_v5.failed_count, 0, "read_objects_by_index_into_v5.failed_count")
        assert_eq(read_index_v5.items[0].path_id, first_path_id, "read_objects_by_index_into_v5.item.path_id")
        read_index_payload = ctypes.string_at(read_index_v5.payload, read_index_v5.payload_len)
        assert_eq(read_index_payload, read_v4_payload, "read_objects_by_index_into_v5.payload")

        direct_v6_rc, direct_v6, _, _ = native.read_objects_direct_v6(
            opened_context,
            [{"path_id": first_path_id, "kind": default_read_kind, "image_format": "raw_rgba"}],
            size_v4.required_items_buffer_len,
            size_v4.required_payload_len,
        )
        assert_eq(direct_v6_rc, 0, "read_objects_direct_into_v6.rc")
        assert_eq(direct_v6.status, 0, "read_objects_direct_into_v6.status")
        assert_eq(direct_v6.returned_count, 1, "read_objects_direct_into_v6.returned_count")
        assert_eq(direct_v6.failed_count, 0, "read_objects_direct_into_v6.failed_count")
        direct_v6_payload = ctypes.string_at(direct_v6.payload, direct_v6.payload_len)
        assert_eq(direct_v6_payload, read_v4_payload, "read_objects_direct_into_v6.payload")

        direct_index_v6_rc, direct_index_v6, _, _ = native.read_objects_by_index_direct_v6(
            opened_context,
            [{"object_index": read_asset.get("index"), "kind": default_read_kind, "image_format": "raw_rgba"}],
            size_index_v5.required_items_buffer_len,
            size_index_v5.required_payload_len,
        )
        assert_eq(direct_index_v6_rc, 0, "read_objects_by_index_direct_into_v6.rc")
        assert_eq(direct_index_v6.status, 0, "read_objects_by_index_direct_into_v6.status")
        assert_eq(direct_index_v6.returned_count, 1, "read_objects_by_index_direct_into_v6.returned_count")
        assert_eq(direct_index_v6.failed_count, 0, "read_objects_by_index_direct_into_v6.failed_count")
        direct_index_v6_payload = ctypes.string_at(direct_index_v6.payload, direct_index_v6.payload_len)
        assert_eq(direct_index_v6_payload, read_v4_payload, "read_objects_by_index_direct_into_v6.payload")

        estimated_payload_capacity = int(read_asset.get("estimated_payload_capacity") or 0)
        raw_payload_capacity = int(read_asset.get("raw_payload_capacity") or 0)
        image_payload_capacity = int(read_asset.get("image_payload_capacity") or 0)
        text_payload_capacity = int(read_asset.get("text_payload_capacity") or 0)
        assert_true(estimated_payload_capacity >= 0, "read_asset.estimated_payload_capacity")
        if default_read_kind in ("auto", "raw") and raw_payload_capacity > 0:
            assert_true(raw_payload_capacity >= 0, "read_asset.raw_payload_capacity")
        if default_read_kind in ("auto", "image") and image_payload_capacity > 0:
            assert_true(image_payload_capacity >= len(read_v4_payload), "read_asset.image_payload_capacity")
        if default_read_kind in ("auto", "text_bytes", "text") and text_payload_capacity > 0:
            assert_true(text_payload_capacity >= len(read_v4_payload), "read_asset.text_payload_capacity")

        no_size_direct_index_v6_rc, no_size_direct_index_v6, _, _ = native.read_objects_by_index_direct_v6(
            opened_context,
            [{"object_index": read_asset.get("index"), "kind": default_read_kind, "image_format": "raw_rgba"}],
            size_index_v5.required_items_buffer_len,
            size_index_v5.required_payload_len,
        )
        assert_eq(no_size_direct_index_v6_rc, 0, "read_objects_by_index_direct_into_v6_no_size.rc")
        assert_eq(no_size_direct_index_v6.status, 0, "read_objects_by_index_direct_into_v6_no_size.status")
        assert_eq(no_size_direct_index_v6.returned_count, 1, "read_objects_by_index_direct_into_v6_no_size.returned_count")
        assert_eq(no_size_direct_index_v6.failed_count, 0, "read_objects_by_index_direct_into_v6_no_size.failed_count")
        no_size_direct_index_v6_payload = ctypes.string_at(no_size_direct_index_v6.payload, no_size_direct_index_v6.payload_len)
        assert_eq(no_size_direct_index_v6_payload, read_v4_payload, "read_objects_by_index_direct_into_v6_no_size.payload")

        retry_direct_rc, retry_direct_v7, _, _ = native.read_objects_by_index_direct_retry_v7(
            opened_context,
            [{"object_index": read_asset.get("index"), "kind": default_read_kind, "image_format": "raw_rgba"}],
            size_index_v5.required_items_buffer_len,
            size_index_v5.required_payload_len,
        )
        assert_eq(retry_direct_rc, 0, "read_objects_by_index_direct_retry_v7.rc")
        assert_eq(retry_direct_v7.status, 0, "read_objects_by_index_direct_retry_v7.status")
        assert_eq(retry_direct_v7.result_handle, 0, "read_objects_by_index_direct_retry_v7.result_handle")
        retry_direct_payload = ctypes.string_at(retry_direct_v7.payload, retry_direct_v7.payload_len)
        assert_eq(retry_direct_payload, read_v4_payload, "read_objects_by_index_direct_retry_v7.payload")

        retry_alloc_rc, retry_alloc_v7, _, _ = native.read_objects_by_index_direct_retry_v7(
            opened_context,
            [{"object_index": read_asset.get("index"), "kind": default_read_kind, "image_format": "raw_rgba"}],
            1,
            1,
        )
        assert_eq(retry_alloc_rc, 0, "read_objects_by_index_direct_retry_v7_alloc.rc")
        assert_eq(retry_alloc_v7.status, 0, "read_objects_by_index_direct_retry_v7_alloc.status")
        assert_true(retry_alloc_v7.result_handle != 0, "read_objects_by_index_direct_retry_v7_alloc.result_handle")
        assert_true((retry_alloc_v7.ownership_flags & 1) != 0, "read_objects_by_index_direct_retry_v7_alloc.ownership_items")
        assert_true((retry_alloc_v7.ownership_flags & 2) != 0, "read_objects_by_index_direct_retry_v7_alloc.ownership_payload")
        retry_alloc_payload = ctypes.string_at(retry_alloc_v7.payload, retry_alloc_v7.payload_len)
        assert_eq(retry_alloc_payload, read_v4_payload, "read_objects_by_index_direct_retry_v7_alloc.payload")
        assert_eq(native.lib.haruki_assetstudio_result_free(retry_alloc_v7.result_handle), 0, "read_objects_by_index_direct_retry_v7_alloc.free")

        _, too_small_size, too_small_rc, too_small_v4, _, _ = native.read_objects_v4(
            opened_context,
            [{"path_id": first_path_id, "kind": default_read_kind, "image_format": "raw_rgba"}],
            shrink_payload_by=1,
        )
        assert_eq(too_small_size.status, 0, "read_objects_into_v4_too_small.size_status")
        assert_eq(too_small_rc, 8, "read_objects_into_v4_too_small.rc")
        assert_eq(too_small_v4.status, 8, "read_objects_into_v4_too_small.status")
        assert_eq(too_small_v4.error_code, 8, "read_objects_into_v4_too_small.error_code")
        assert_eq(too_small_v4.required_payload_len, too_small_size.required_payload_len, "read_objects_into_v4_too_small.required_payload_len")

        retry_size_rc, retry_size_v4, retry_small_rc, retry_small_v4, retry_rc, retry_v4, _, retry_payload_buffer = native.read_objects_v4_too_small_then_retry(
            opened_context,
            [{"path_id": first_path_id, "kind": default_read_kind, "image_format": "raw_rgba"}],
        )
        assert_eq(retry_size_rc, 0, "read_objects_into_v4_too_small_retry.size_rc")
        assert_eq(retry_size_v4.status, 0, "read_objects_into_v4_too_small_retry.size_status")
        assert_eq(retry_small_rc, 8, "read_objects_into_v4_too_small_retry.small_rc")
        assert_eq(retry_small_v4.status, 8, "read_objects_into_v4_too_small_retry.small_status")
        assert_eq(retry_rc, 0, "read_objects_into_v4_too_small_retry.retry_rc")
        assert_eq(retry_v4.status, 0, "read_objects_into_v4_too_small_retry.retry_status")
        retry_payload = ctypes.string_at(retry_v4.payload, retry_v4.payload_len)
        assert_eq(retry_payload, read_v3_payload, "read_objects_into_v4_too_small_retry.payload")

        missing_id = -9223372036854775808
        missing_size_rc, missing_size_v4, missing_into_rc, missing_v4, _, _ = native.read_objects_v4(
            opened_context,
            [{"path_id": missing_id, "kind": "auto", "image_format": "raw_rgba"}],
        )
        assert_eq(missing_size_rc, 6, "read_objects_size_v4_missing.rc")
        assert_eq(missing_size_v4.status, 6, "read_objects_size_v4_missing.status")
        assert_eq(missing_size_v4.error_code, 6, "read_objects_size_v4_missing.error_code")
        assert_eq(missing_size_v4.failed_count, 1, "read_objects_size_v4_missing.failed_count")
        assert_true(missing_size_v4.required_string_data_len > 0, "read_objects_size_v4_missing.required_string_data_len")
        assert_eq(missing_into_rc, 6, "read_objects_into_v4_missing.rc")
        assert_eq(missing_v4.status, 6, "read_objects_into_v4_missing.status")
        assert_eq(missing_v4.error_code, 6, "read_objects_into_v4_missing.error_code")
        assert_eq(missing_v4.failed_count, 1, "read_objects_into_v4_missing.failed_count")
        assert_eq(missing_v4.items[0].status, 6, "read_objects_into_v4_missing.item.status")
        assert_eq(missing_v4.items[0].error_code, 6, "read_objects_into_v4_missing.item.error_code")
        missing_strings = ctypes.string_at(missing_v4.string_data, missing_v4.string_data_len)
        missing_message = native_string(missing_strings, missing_v4.items[0].error_message_offset, missing_v4.items[0].error_message_len)
        assert_true("was not found" in missing_message, "read_objects_into_v4_missing.error_message")

        partial_size_rc, partial_size_v4, partial_into_rc, partial_v4, _, _ = native.read_objects_v4(
            opened_context,
            [
                {"path_id": first_path_id, "kind": default_read_kind, "image_format": "raw_rgba"},
                {"path_id": missing_id, "kind": "auto", "image_format": "raw_rgba"},
            ],
        )
        assert_eq(partial_size_rc, 0, "read_objects_size_v4_partial.rc")
        assert_eq(partial_size_v4.status, 0, "read_objects_size_v4_partial.status")
        assert_eq(partial_size_v4.error_code, 9, "read_objects_size_v4_partial.error_code")
        assert_eq(partial_size_v4.returned_count, 2, "read_objects_size_v4_partial.returned_count")
        assert_eq(partial_size_v4.failed_count, 1, "read_objects_size_v4_partial.failed_count")
        assert_eq(partial_into_rc, 0, "read_objects_into_v4_partial.rc")
        assert_eq(partial_v4.status, 0, "read_objects_into_v4_partial.status")
        assert_eq(partial_v4.error_code, 9, "read_objects_into_v4_partial.error_code")
        assert_eq(partial_v4.returned_count, 2, "read_objects_into_v4_partial.returned_count")
        assert_eq(partial_v4.failed_count, 1, "read_objects_into_v4_partial.failed_count")
        assert_eq(partial_v4.items[0].status, 0, "read_objects_into_v4_partial.first.status")
        assert_eq(partial_v4.items[1].status, 6, "read_objects_into_v4_partial.second.status")

        recovery_size_rc, _, recovery_into_rc, recovery_v4, _, _ = native.read_objects_v4(
            opened_context,
            [{"path_id": first_path_id, "kind": default_read_kind, "image_format": "raw_rgba"}],
        )
        assert_eq(recovery_size_rc, 0, "read_objects_into_v4_recovery.size_rc")
        assert_eq(recovery_into_rc, 0, "read_objects_into_v4_recovery.into_rc")
        assert_eq(recovery_v4.status, 0, "read_objects_into_v4_recovery.status")

        unsupported_size_rc, unsupported_size_v4, unsupported_into_rc, unsupported_v4, _, _ = native.read_objects_v4(
            opened_context,
            [{"path_id": first_path_id, "kind": "definitely_not_supported", "image_format": "raw_rgba"}],
        )
        assert_eq(unsupported_size_rc, 7, "read_objects_size_v4_unsupported.rc")
        assert_eq(unsupported_size_v4.status, 7, "read_objects_size_v4_unsupported.status")
        assert_eq(unsupported_size_v4.error_code, 7, "read_objects_size_v4_unsupported.error_code")
        assert_eq(unsupported_size_v4.failed_count, 1, "read_objects_size_v4_unsupported.failed_count")
        assert_eq(unsupported_into_rc, 7, "read_objects_into_v4_unsupported.rc")
        assert_eq(unsupported_v4.status, 7, "read_objects_into_v4_unsupported.status")
        assert_eq(unsupported_v4.error_code, 7, "read_objects_into_v4_unsupported.error_code")
        assert_eq(unsupported_v4.items[0].status, 7, "read_objects_into_v4_unsupported.item.status")
        assert_eq(unsupported_v4.items[0].error_code, 7, "read_objects_into_v4_unsupported.item.error_code")
        unsupported_strings = ctypes.string_at(unsupported_v4.string_data, unsupported_v4.string_data_len)
        unsupported_message = native_string(unsupported_strings, unsupported_v4.items[0].error_message_offset, unsupported_v4.items[0].error_message_len)
        assert_true(unsupported_message, "read_objects_into_v4_unsupported.error_message")

        rc, texture_table, texture_assets = native.list_objects_v2(
            opened_context,
            offset=0,
            limit=64,
            asset_types_csv="Texture2D",
        )
        assert_eq(rc, 0, "list_v2_texture.rc")
        assert_eq(texture_table.status, 0, "list_v2_texture.status")
        texture_png = None
        for texture_asset in texture_assets:
            rc, candidate_png = native.read_objects_v3(
                opened_context,
                [{"path_id": texture_asset["path_id"], "kind": "image", "image_format": "raw_rgba"}],
            )
            assert_eq(rc, 0, "read_objects_v3_texture_png.rc")
            assert_eq(candidate_png.status, 0, "read_objects_v3_texture_png.status")
            assert_eq(candidate_png.error_code, 0, "read_objects_v3_texture_png.error_code")
            assert_eq(candidate_png.returned_count, 1, "read_objects_v3_texture_png.returned_count")
            assert_eq(candidate_png.failed_count, 0, "read_objects_v3_texture_png.failed_count")
            if candidate_png.payload_len > 8:
                texture_png = candidate_png
                break
            if candidate_png.result_handle:
                assert_eq(native.lib.haruki_assetstudio_result_free(candidate_png.result_handle), 0, "read_objects_v3_texture_png.empty_free")
        if texture_assets:
            assert_true(texture_png is not None, "read_objects_v3_texture_png.non_empty_candidate")
            png_header = ctypes.string_at(texture_png.payload, 8)
            assert_eq(png_header, b"\x89PNG\r\n\x1a\n", "read_objects_v3_texture_png.header")
            assert_true(texture_png.result_handle > 0, "read_objects_v3_texture_png.result_handle")
            assert_eq(native.lib.haruki_assetstudio_result_free(texture_png.result_handle), 0, "read_objects_v3_texture_png.free")

        rc, close_owned_read_v3 = native.read_objects_v3(
            opened_context,
            [{"path_id": first_path_id, "kind": default_read_kind, "image_format": "raw_rgba"}],
        )
        assert_eq(rc, 0, "read_objects_v3_close_owned.rc")
        assert_true(close_owned_read_v3.result_handle > 0, "read_objects_v3_close_owned.result_handle")

        rc, missing_read, missing_items, missing_payload = native.read_objects_direct_retry_v7(
            opened_context,
            [{"path_id": -9223372036854775808, "kind": "auto", "image_format": "raw_rgba"}],
            0,
            0,
        )
        assert_eq(rc, 6, "read_missing.rc")
        assert_eq(missing_read.status, 6, "read_missing.status")
        assert_eq(missing_read.error_code, 6, "read_missing.error_code")
        assert_eq(missing_payload, None, "read_missing.payload")

        rc, unsupported_response, unsupported_items, unsupported_payload = native.read_objects_direct_retry_v7(
            opened_context,
            [{"path_id": first_path_id, "kind": "definitely_not_supported", "image_format": "raw_rgba"}],
            0,
            0,
        )
        assert_eq(rc, 7, "read_unsupported_batch.rc")
        assert_eq(unsupported_response.status, 7, "read_unsupported_batch.status")
        assert_eq(unsupported_response.error_code, 7, "read_unsupported_batch.error_code")
        assert_eq(unsupported_response.failed_count, 1, "read_unsupported_batch.failed_count")
        assert_eq(unsupported_payload, None, "read_unsupported_batch.payload")

        closed_context = opened_context
        rc, closed = native.close_v2(closed_context)
        opened_context = None
        assert_eq(rc, 0, "close_v2.rc")
        assert_eq(closed.status, 0, "close_v2.status")
        assert_eq(closed.error_code, 0, "close_v2.error_code")
        assert_eq(closed.struct_size, ctypes.sizeof(NativeContextCloseResponse), "close_v2.struct_size")
        assert_eq(
            native.lib.haruki_assetstudio_result_free(close_owned_read_v3.result_handle),
            4,
            "read_objects_v3_close_owned.free_after_close",
        )

        rc, close_again = native.close_v2(closed_context)
        assert_eq(rc, 4, "close_again_v2.rc")
        assert_eq(close_again.status, 4, "close_again_v2.status")
        assert_eq(close_again.error_code, 4, "close_again_v2.error_code")

        print(json.dumps({
            "ok": True,
            "core_api_version": f"{caps.core_api_version_major}.{caps.core_api_version_minor}",
            "legacy_static_engine": bool(limits.legacy_static_engine),
            "native_console_capture": bool(caps.native_console_capture),
            "object_index_count": opened_v2.object_index_count,
            "listed": len(typed_assets),
            "read_entries": read_v4.returned_count,
            "read_filter": read_filter,
        }, separators=(",", ":")))
    finally:
        if opened_context2:
            try:
                native.close_v2(opened_context2)
            except Exception:
                pass
        if opened_context:
            try:
                native.close_v2(opened_context)
            except Exception:
                pass


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"ffi contract smoke failed: {exc}", file=sys.stderr)
        traceback.print_exc()
        sys.exit(1)
