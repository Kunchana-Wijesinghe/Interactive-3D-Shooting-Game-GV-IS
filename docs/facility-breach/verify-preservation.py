"""Read-only audit against the exact user workspace baseline, including existing local edits."""
import hashlib
import json
import pathlib
import re
import subprocess

root = pathlib.Path(__file__).resolve().parents[2]
baseline = json.loads((root / "docs/facility-breach/baseline.json").read_text())
protected = baseline["protected"]
actual = {str(p.relative_to(root)).replace("\\", "/") for p in (root / "Assets/Perera").rglob("*") if p.is_file()}
actual.add("Assets/Perera.meta")
assert actual == set(protected), "Protected file set changed"
for name, digest in protected.items():
    assert hashlib.sha256((root / name).read_bytes()).hexdigest() == digest, name
scene = (root / "Assets/Scenes/Prototype.unity").read_text()
blocks = {re.search(r"&(-?\d+)", block)[1]: block for block in re.split(r"(?=--- !u!)", scene) if block.startswith("--- !u!")}
for transform in baseline["transforms"]:
    assert hashlib.sha256(blocks[transform["id"]].encode()).hexdigest() == transform["sha256"], transform["name"]
original = subprocess.check_output(["git", "show", "add95ee:Assets/Scenes/Prototype.unity"], cwd=root, text=True)
for block in re.split(r"(?=--- !u!)", original):
    if not block.startswith("--- !u!"): continue
    identity = re.search(r"&(-?\d+)", block)[1]
    if "SceneRoots:" in block:
        assert blocks[identity].startswith(block), "Existing roots changed"
    else:
        assert blocks[identity] == block, "Existing scene component changed: " + identity
print(f"PASS: {len(protected)} protected files and {len(baseline['transforms'])} authored transforms unchanged")
