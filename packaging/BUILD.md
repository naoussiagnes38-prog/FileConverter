# Offline Windows release

Build the FileConverter solution in Release and restore its NuGet packages.
Use the x64 CPython 3.12.10 embeddable archive from the official Python release.
Stage the Python packages from `translation-win-x64.lock.txt` in a clean directory.

```sh
uv pip install --only-binary :all: --no-deps --python-version 3.12 \
  --python-platform x86_64-pc-windows-msvc --target staging/site-packages \
  -r packaging/translation-win-x64.lock.txt
```

Put both Argos 1.9 archives and the English and French MiniSBD ONNX files
in `staging/models`. Put the Microsoft x64 Visual C++ redistributable
under `staging/sources/VC_redist.x64.exe`, along with the corresponding
third-party source archives for this distribution.

```sh
python3 packaging/build_offline_release.py \
  --app FileConverter/bin/Release \
  --python-zip staging/python-3.12.10-embed-amd64.zip \
  --site-packages staging/site-packages \
  --models staging/models --sources staging/sources \
  --output staging/FileConverter-Offline-Windows-x64.zip
```

Before sharing a release, extract it on Windows x64 with the network
disconnected. Run Offline Translation Test.cmd and then use FileConverter
to translate a document in each direction with no other LibreTranslate
server running. Verify that closing the app stops the server it started.

- [CPython 3.12.10 downloads](https://www.python.org/downloads/release/python-31210/)
- [Argos model index](https://github.com/argosopentech/argospm-index)
- [MiniSBD model release](https://github.com/LibreTranslate/MiniSBD/releases/tag/v0.0.1)
- [Visual C++ runtime downloads](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)
