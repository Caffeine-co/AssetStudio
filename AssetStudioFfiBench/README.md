# AssetStudio FFI Benchmark

Small benchmark harness for comparing the NativeAOT FFI path with an optional CLI process run on the same input.

Build or publish the native library first:

```bash
dotnet publish ../AssetStudioFFI/AssetStudioFFI.csproj -c Release -r osx-arm64 -v:minimal
```

Run FFI-only open/list timing:

```bash
dotnet run --project . -c Release -- \
  --input /path/to/unity/assets/or/bundle \
  --unity-version 2022.3.62f1 \
  --iterations 5 \
  --warmup 1
```

Run FFI with batch object reads:

```bash
dotnet run --project . -c Release -- \
  --input /path/to/unity/assets/or/bundle \
  --unity-version 2022.3.62f1 \
  --read-count 20 \
  --kind auto \
  --image-format bmp
```

Run FFI and CLI process comparison:

```bash
dotnet run --project . -c Release -- \
  --input /path/to/unity/assets/or/bundle \
  --cli ../AssetStudioCLI/bin/Release/net9.0/osx-arm64/AssetStudioModCLI.dll \
  --unity-version 2022.3.62f1 \
  --iterations 5 \
  --warmup 1
```

Use `--native-lib` when measuring a library outside the default native publish path.
Use `--deobfuscate-input` for Project Sekai-style bundle headers that start with `20 00 00 00` or `10 00 00 00`.
