"""Run FileConverter's bundled translation service and verify its local models."""

import os
import sys
from pathlib import Path

runtime = Path(__file__).resolve().parent
data = Path(os.environ["LOCALAPPDATA"]) / "FileConverter" / "Translation"
data.mkdir(parents=True, exist_ok=True)
os.chdir(data)
os.environ["XDG_DATA_HOME"] = str(data / "data")
os.environ["XDG_CONFIG_HOME"] = str(data / "config")
os.environ["XDG_CACHE_HOME"] = str(data / "cache")
os.environ["ARGOS_PACKAGES_DIR"] = str(runtime / "models")
os.environ["ARGOS_CHUNK_TYPE"] = "MINISBD"
os.environ["ARGOS_MODEL_PROVIDER"] = "OPENNMT"
os.environ["ARGOS_DEVICE_TYPE"] = "cpu"
os.environ["ARGOS_DEBUG"] = "0"
os.environ.pop("LT_UPDATE_MODELS", None)
os.environ.pop("LT_FORCE_UPDATE_MODELS", None)

from argostranslate.package import get_installed_packages

required = {("en", "fr"), ("fr", "en")}
available = {
    (package.from_code, package.to_code)
    for package in get_installed_packages()
    if (package.package_path / "minisbd" / (package.from_code + ".onnx")).is_file()
}
if not required <= available:
    sys.exit("The bundled English and French translation or sentence models are missing.")

if sys.argv[1:] == ["--self-test"]:
    from argostranslate.translate import translate

    for source, target, text in (
        ("en", "fr", "Hello, how are you today?"),
        ("fr", "en", "Bonjour, comment allez-vous ?"),
    ):
        translated = translate(text, source, target)
        if not translated.strip() or translated.strip().casefold() == text.casefold():
            sys.exit(f"Translation self-test failed for {source} to {target}.")
        print(f"{source} -> {target}: {translated}")
else:
    from libretranslate.main import main

    main()
