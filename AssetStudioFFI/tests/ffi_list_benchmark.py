#!/usr/bin/env python3
import argparse
import ctypes
import json
import statistics
import time

from ffi_contract_smoke import HarukiAssetStudioNative, NativeObjectListRequest, NativeObjectTable


def percentile(values, fraction):
    if not values:
        return 0.0
    ordered = sorted(values)
    index = min(len(ordered) - 1, max(0, round((len(ordered) - 1) * fraction)))
    return ordered[index]


def summarize(samples):
    return {
        "min_ms": round(min(samples), 3),
        "mean_ms": round(statistics.mean(samples), 3),
        "p50_ms": round(percentile(samples, 0.50), 3),
        "p95_ms": round(percentile(samples, 0.95), 3),
        "max_ms": round(max(samples), 3),
    }


def list_all_typed(native, context_id, page_size, asset_types_csv):
    offset = 0
    count = 0
    checksum = 0
    native_ms = 0
    while True:
        rc, table, assets = native.list_objects_v1(
            context_id,
            offset=offset,
            limit=page_size,
            asset_types_csv=asset_types_csv,
        )
        if rc != 0 or table.status != 0:
            raise RuntimeError(f"typed list failed rc={rc} status={table.status} error={table.error_code}")
        for asset in assets:
            checksum ^= int(asset["path_id"])
        count += len(assets)
        native_ms += table.duration_ms
        if not table.has_more:
            return count, checksum, native_ms
        offset = table.next_offset


def list_all_typed_raw(native, context_id, page_size, asset_types_csv):
    offset = 0
    count = 0
    checksum = 0
    native_ms = 0
    asset_type_bytes = asset_types_csv.encode("utf-8") if asset_types_csv else None
    asset_type_ptr = None
    asset_type_len = 0
    if asset_type_bytes:
        asset_type_len = len(asset_type_bytes)
        asset_type_ptr = (ctypes.c_ubyte * asset_type_len).from_buffer_copy(asset_type_bytes)
    while True:
        request = NativeObjectListRequest(
            struct_size=ctypes.sizeof(NativeObjectListRequest),
            context_id=context_id,
            offset=offset,
            limit=page_size,
            asset_types_csv_utf8=asset_type_ptr,
            asset_types_csv_utf8_len=asset_type_len,
        )
        table = NativeObjectTable()
        rc = native.lib.haruki_assetstudio_context_list_objects_v1(ctypes.byref(request), ctypes.byref(table))
        try:
            if rc != 0 or table.status != 0:
                raise RuntimeError(f"typed raw list failed rc={rc} status={table.status} error={table.error_code}")
            for index in range(max(0, table.returned_count)):
                checksum ^= int(table.objects[index].path_id)
            count += table.returned_count
            native_ms += table.duration_ms
            if not table.has_more:
                return count, checksum, native_ms
            offset = table.next_offset
        finally:
            if table.buffer:
                native.lib.haruki_assetstudio_free_buffer(table.buffer)


def main():
    parser = argparse.ArgumentParser(description="Compare typed object table list FFI paths")
    parser.add_argument("library", help="Path to HarukiAssetStudioFFI shared library")
    parser.add_argument("input_path", help="Unity asset bundle/file/directory input path")
    parser.add_argument("--unity-version", default="2022.3.62f1")
    parser.add_argument("--page-size", type=int, default=4096)
    parser.add_argument("--rounds", type=int, default=5)
    parser.add_argument("--asset-types", default="", help="Comma-separated type filter, for example TextAsset,MonoBehaviour")
    args = parser.parse_args()

    native = HarukiAssetStudioNative(args.library)
    asset_types = [value.strip() for value in args.asset_types.split(",") if value.strip()]
    asset_types_csv = ",".join(asset_types) if asset_types else None
    opened_context = None

    try:
        rc, opened, _ = native.open_v1(args.input_path, args.unity_version)
        if rc != 0 or opened.status != 0:
            raise RuntimeError(f"open_v1 failed rc={rc} status={opened.status} error={opened.error_code}")
        opened_context = opened.context_id

        typed_raw_samples = []
        typed_samples = []
        typed_raw_native_ms = []
        typed_native_ms = []
        expected_count = None
        expected_checksum = None

        for _ in range(args.rounds):
            start = time.perf_counter()
            typed_raw_count, typed_raw_checksum, typed_raw_ms = list_all_typed_raw(
                native,
                opened_context,
                args.page_size,
                asset_types_csv,
            )
            typed_raw_samples.append((time.perf_counter() - start) * 1000)
            typed_raw_native_ms.append(typed_raw_ms)

            start = time.perf_counter()
            typed_count, typed_checksum, typed_ms = list_all_typed(native, opened_context, args.page_size, asset_types_csv)
            typed_samples.append((time.perf_counter() - start) * 1000)
            typed_native_ms.append(typed_ms)

            if expected_count is None:
                expected_count = typed_raw_count
                expected_checksum = typed_raw_checksum
            if typed_count != expected_count or typed_checksum != expected_checksum:
                raise AssertionError(
                    f"typed mismatch count/checksum: expected=({expected_count},{expected_checksum}) "
                    f"typed=({typed_count},{typed_checksum})"
                )
            if typed_raw_count != expected_count or typed_raw_checksum != expected_checksum:
                raise AssertionError(
                    f"typed raw mismatch count/checksum: expected=({expected_count},{expected_checksum}) "
                    f"typed_raw=({typed_raw_count},{typed_raw_checksum})"
                )

        result = {
            "ok": True,
            "object_count": expected_count,
            "page_size": args.page_size,
            "rounds": args.rounds,
            "asset_types": asset_types,
            "typed_raw_wall": summarize(typed_raw_samples),
            "typed_wall": summarize(typed_samples),
            "typed_raw_native_duration_ms": summarize(typed_raw_native_ms),
            "typed_native_duration_ms": summarize(typed_native_ms),
            "typed_vs_raw_wall_ratio": round(statistics.mean(typed_samples) / statistics.mean(typed_raw_samples), 3),
        }
        print(json.dumps(result, separators=(",", ":")))
    finally:
        if opened_context:
            native.close_v1(opened_context)


if __name__ == "__main__":
    main()
