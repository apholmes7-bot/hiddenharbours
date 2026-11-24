#!/usr/bin/env python3
"""Read-only companion to SurfSequenceTests' SHA-256 guards. Standard library only."""

import argparse
import hashlib
import json
from pathlib import Path
import re
import sys


TEST_PATH = Path("Assets/Tests/EditMode/SurfSequenceTests.cs")
MATERIALS = Path("Assets/_Project/Art/Materials")
STRING = r'"(?:\\.|[^"\\])*"'
CS_TOKEN = re.compile(r'@"(?:""|[^"])*"|' + STRING + r'|//[^\n]*|/\*[\s\S]*?\*/')
HEX = r"[0-9a-f]{64}"


def read(path):
    # File.ReadAllText(..., Encoding.UTF8) consumes the BOM. Do not use universal
    # newlines: the C# Read helper replaces CRLF only, leaving lone CR untouched.
    return path.read_bytes().decode("utf-8-sig").replace("\r\n", "\n")


def sha256(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def uncomment(text):
    # Preserve quoted // and /* (including the LegacyDepth replacement).
    return CS_TOKEN.sub(lambda m: " " if m[0].startswith(("//", "/*")) else m[0], text)


def one(pattern, text, description):
    matches = list(re.finditer(pattern, text))
    if len(matches) != 1:
        raise ValueError("expected exactly one " + description)
    return matches[0]


def strings(expression):
    return [json.loads(value) for value in re.findall(STRING, expression)]


def string_array(source, declaration):
    expression = one(declaration + r"\s*\{([\s\S]*?)\}", source, declaration)[1]
    # These initializers contain only ordinary string literals and commas.
    if re.sub(STRING + r"|[\s,]", "", expression):
        raise ValueError("unsupported string array: " + declaration)
    return strings(expression)


def read_pins(source):
    source = uncomment(source)
    constants = {}
    for match in re.finditer(r"const string (\w+)\s*=\s*(" + STRING +
                             r"(?:\s*\+\s*" + STRING + r")*)\s*;", source):
        constants[match[1]] = "".join(strings(match[2]))
    for key in ("WaterPath", "BaseWaterHash", "LegacyDepth", "Dial"):
        if key not in constants:
            raise ValueError("missing constant " + key)
    materials = string_array(source, r"string\[\] originalHashes\s*=")
    direct = []
    for match in re.finditer(r"Assert\.That\(Hash\(Read\((" + STRING +
                             r"|\w+)\)\),\s*Is\.EqualTo\(\"(" + HEX + r")\"\)\)", source):
        path = json.loads(match[1]) if match[1].startswith('"') else constants[match[1]]
        direct.append((path, match[2]))
    pins = [constants["BaseWaterHash"], *materials, *(pin for _, pin in direct)]
    literals = re.findall('"(' + HEX + ')"', source)
    if len(materials) != 9 or len(direct) != 3 or sorted(pins) != sorted(literals):
        raise ValueError("pin layout changed; review SurfSequenceTests before updating this script")
    parts = string_array(source, r"foreach \(string part in new\[\]")
    declarations = string_array(source, r"foreach \(string line in new\[\]")
    transport_keys = string_array(source, r"foreach \(string key in new\[\]")
    low_sun_keys = string_array(source, r"foreach \(string lowSunKey in new\[\]")
    return constants, materials, direct, parts, declarations, transport_keys, low_sun_keys


def require_line(text, line):
    if len(re.findall("^" + re.escape(line) + "$", text, re.MULTILINE)) != 1:
        raise ValueError("missing or repeated required line: " + line.strip())


def water_control(text, constants, parts, declarations):
    for part in parts:
        pattern = (r"^ *// B1 FRINGE " + re.escape(part) + r" BEGIN\n[\s\S]*?"
                   r"^ *// B1 FRINGE " + re.escape(part) + r" END\n")
        if not re.search(pattern, text, re.MULTILINE):
            raise ValueError("missing B1 block " + part)
        replacement = constants["LegacyDepth"] if part == "DEPTH" else ""
        text = re.sub(pattern, lambda _: replacement, text, flags=re.MULTILINE)
    for line in declarations:
        require_line(text, line)
        text = text.replace(line + "\n", "")
    return text


def material_control(text, name, dial, transport_keys, low_sun_keys):
    dial_line = "    - " + dial + ": 0"
    require_line(text, dial_line)
    for key in transport_keys:
        line = "    - " + key + ": 0"
        require_line(text, line)
        text = text.replace(line + "\n", "")
    require_line(text, "    - _SwellReadRelative: 1")
    text = text.replace("    - _SwellReadRelative: 1\n", "")
    if name == "Water_StormGrey.mat":
        require_line(text, "    - _PaletteValueFloor: 0.16")
        text = text.replace("    - _PaletteValueFloor: 0.16\n",
                            "    - _PaletteValueFloor: 0.07\n")
    if name == "Water.mat":
        for key, current, original in (
                ("_RippleStrength", "0.25", "0.45"),
                ("_SunGlitterStrength", "0.25", "0.6")):
            line = "    - " + key + ": " + current
            require_line(text, line)
            text = text.replace(line + "\n", "    - " + key + ": " + original + "\n")
        for key, value in (("_ObjectReflectFadeChop", "0.75"), ("_ObjectReflectLowLight", "4")):
            line = "    - " + key + ": " + value
            require_line(text, line)
            text = text.replace(line + "\n", "")
        for key in low_sun_keys:
            pattern = r"^    - " + re.escape(key) + r": [^\n]*\n"
            one("(?m)" + pattern, text, key + " material row")
            text = re.sub(pattern, "", text, flags=re.MULTILINE)
    return text.replace(dial_line + "\n", "")


def check(root):
    (constants, material_pins, direct, parts, declarations,
     transport_keys, low_sun_keys) = read_pins(read(root / TEST_PATH))
    # Ordinal ordering agrees on Windows and Linux for these ASCII file names.
    presets = sorted((root / MATERIALS / "WaterPresets").glob("*.mat"), key=lambda p: p.name)
    material_files = [root / MATERIALS / "Water.mat", *presets]
    if len(material_files) != len(material_pins):
        raise ValueError("expected nine materials (Water.mat followed by eight sorted presets)")
    jobs = [("BaseWaterHash", constants["BaseWaterHash"], root / constants["WaterPath"],
             lambda text: water_control(text, constants, parts, declarations))]
    jobs.extend(("AdvectPath" if path == constants.get("AdvectPath") else Path(path).name,
                 pin, root / path, lambda text: text) for path, pin in direct)
    jobs.extend((path.name, pin, path,
                 lambda text, name=path.name: material_control(
                     text, name, constants["Dial"], transport_keys, low_sun_keys))
                for path, pin in zip(material_files, material_pins))
    failures = 0
    for label, pinned, path, transform in jobs:
        try:
            actual = sha256(transform(read(path)))
            error = ""
        except (OSError, UnicodeError, ValueError) as exc:
            actual, error = "ERROR", str(exc)
        status = "OK" if actual == pinned else "MISMATCH"
        failures += status != "OK"
        print("{} {} pinned={} recomputed={}".format(status, label, pinned, actual))
        if error:
            print("  " + error)
    print("{} pins: {} OK, {} MISMATCH".format(len(jobs), len(jobs) - failures, failures))
    return 1 if failures else 0


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parent.parent,
                        help="checkout to read (default: checkout containing this script)")
    args = parser.parse_args(argv)
    try:
        return check(args.root.resolve())
    except (OSError, UnicodeError, ValueError, KeyError) as exc:
        print("ERROR: " + str(exc), file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
