"""Build a Windows x64 portable ZIP from the staged release inputs."""

import argparse
import shutil
from pathlib import Path
from tempfile import TemporaryDirectory
from zipfile import ZIP_DEFLATED, ZipFile

MODEL_NAMES = {"en": "translate-en_fr-1_9", "fr": "translate-fr_en-1_9"}


def unpack(archive, directory):
    with ZipFile(archive) as source:
        corrupt = source.testzip()
        if corrupt is not None:
            raise ValueError(f"Invalid archive entry in {archive}: {corrupt}")
        source.extractall(directory)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("app", "python-zip", "site-packages", "models", "sources", "output"):
        parser.add_argument("--" + name, required=True, type=Path)
    args = parser.parse_args()

    with TemporaryDirectory(prefix="FileConverter-release-") as temporary:
        stage = Path(temporary) / "FileConverter"
        stage.mkdir()
        for entry in args.app.iterdir():
            if entry.is_file() and entry.suffix.lower() in (".exe", ".dll", ".config"):
                shutil.copy2(entry, stage)
        if not (stage / "FileConverter.exe").is_file():
            raise ValueError("Build FileConverter in Release before packaging.")

        runtime = stage / "TranslationRuntime"
        unpack(args.python_zip, runtime)
        if not (runtime / "python312.dll").is_file():
            raise ValueError("Use the Windows x64 embeddable Python 3.12 ZIP.")
        (runtime / "python312._pth").write_text(
            "python312.zip\n.\nsite-packages\nimport site\n", encoding="ascii"
        )
        shutil.copytree(
            args.site_packages, runtime / "site-packages",
            ignore=shutil.ignore_patterns("__pycache__", "*.pyc", ".lock", "*.whl", "bin"),
        )
        for name in ("libretranslate", "argostranslate", "minisbd", "ctranslate2", "onnxruntime"):
            if not (runtime / "site-packages" / name).is_dir():
                raise ValueError(f"Missing staged Python dependency: {name}")
        if not list((runtime / "site-packages" / "ctranslate2").glob("*win_amd64.pyd")):
            raise ValueError("Stage the Windows x64 Python 3.12 dependencies.")

        for language, name in MODEL_NAMES.items():
            unpack(args.models / (name + ".argosmodel"), runtime / "models")
            sentences = runtime / "models" / name / "minisbd"
            sentences.mkdir()
            shutil.copy2(args.models / (language + ".onnx"), sentences)

        scripts = Path(__file__).resolve().parent
        shutil.copy2(scripts / "run_libretranslate.py", runtime)
        shutil.copy2(scripts / "Offline Translation Test.cmd", stage)
        shutil.copy2(scripts / "START HERE.txt", stage)
        prerequisites = stage / "Prerequisites"
        prerequisites.mkdir()
        shutil.copy2(args.sources / "VC_redist.x64.exe", prerequisites)
        source_directory = stage / "ThirdPartySource"
        source_directory.mkdir()
        for archive in args.sources.glob("*.tar.gz"):
            shutil.copy2(archive, source_directory)

        args.output.parent.mkdir(parents=True, exist_ok=True)
        with ZipFile(args.output, "w", ZIP_DEFLATED, compresslevel=3) as release:
            for path in sorted(stage.rglob("*")):
                if path.is_file():
                    release.write(path, path.relative_to(stage.parent))
        print(args.output.resolve())


if __name__ == "__main__":
    main()
