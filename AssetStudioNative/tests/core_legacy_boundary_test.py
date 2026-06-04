#!/usr/bin/env python3
import pathlib
import sys


REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]

ALLOWED_CORE_CLIOPTIONS_FILES = set()


def iter_source_files(root):
    for path in root.rglob("*"):
        if any(part in {"bin", "obj", "__pycache__"} for part in path.parts):
            continue
        if path.suffix in {".cs", ".csproj"}:
            yield path


def fail(message):
    print(message, file=sys.stderr)
    return 1


def main():
    violations = []

    core_root = REPO_ROOT / "AssetStudioCore"
    for path in iter_source_files(core_root):
        if path in ALLOWED_CORE_CLIOPTIONS_FILES:
            continue
        text = path.read_text(encoding="utf-8-sig")
        if "CLIOptions" in text:
            violations.append(f"{path.relative_to(REPO_ROOT)} directly references CLIOptions")

    native_root = REPO_ROOT / "AssetStudioNative"
    for path in iter_source_files(native_root):
        text = path.read_text(encoding="utf-8-sig")
        if "AssetStudioCLI" in text:
            violations.append(f"{path.relative_to(REPO_ROOT)} references AssetStudioCLI")

    if violations:
        return fail("Core/FFI legacy boundary violations:\n" + "\n".join(violations))

    print("ok: core legacy boundary is contained")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
