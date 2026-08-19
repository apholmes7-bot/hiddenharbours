"""What vintage of the world a package is a picture of.

Nothing here is keyed to ``HEAD``. A package pinned to whatever commit happened to be checked
out could never be self-consistent — committing it moves ``HEAD`` and invalidates it — so every
field is a function of the **inputs**: the commit the scene was last banked at, the scene file's
own hash, and the newest builder commit that has landed since. Those move when the answer moves
and at no other time, which is what lets ``--check`` mean something.

The committed ``.unity`` is a **banked build**, not hand-authored: the builders rebuild from an
empty scene, so a scene file is only ever as current as the last time somebody ran the builder
and committed the result. That gap is the single most decision-relevant fact about an export,
so it is measured and stamped rather than left for the reader to discover by noticing a missing
road.
"""

import hashlib
import os
import subprocess

# The builder families whose output lands in a region scene. A commit touching one of these
# after the scene was banked is a change the committed scene does not show.
BUILDER_GLOBS = {
    "NineMileCreek": ["Assets/_Project/Code/App/Editor/NineMileCreek*"],
    "StPeters": ["Assets/_Project/Code/App/Editor/StPeters*"],
}


def _git(repo_root, *args):
    try:
        result = subprocess.run(
            ("git",) + args, cwd=repo_root, capture_output=True, text=True, check=False,
        )
    except OSError:
        return None
    if result.returncode != 0:
        return None
    return result.stdout.strip()


def collect(repo, region_name, scene_rel, height_map):
    root = repo.root
    scene_commit = _git(root, "log", "-1", "--format=%H", "--", scene_rel)
    scene_date = _git(root, "log", "-1", "--format=%ad", "--date=short", "--", scene_rel)
    scene_subject = _git(root, "log", "-1", "--format=%s", "--", scene_rel)
    dirty = _git(root, "status", "--porcelain", "--", scene_rel)

    # A shallow clone (the usual shape in a CI or agent container) can only see back to its
    # graft point, so "last built at X" may really mean "at or before X" and the drift count is
    # a floor rather than a total. Saying which it is costs one git call.
    shallow = _git(root, "rev-parse", "--is-shallow-repository") == "true"

    drift = None
    if scene_commit:
        globs = BUILDER_GLOBS.get(region_name, [])
        commits = _git(root, "rev-list", "--count", f"{scene_commit}..HEAD", "--", *globs)
        subjects = _git(root, "log", "--format=%h %s", f"{scene_commit}..HEAD", "--", *globs)
        # Measured to the newest BUILDER commit, not to HEAD. The number only changes when the
        # answer changes, so a package does not go stale every time an unrelated commit lands —
        # and the exporter's own commits cannot invalidate its own output.
        newest = _git(root, "log", "-1", "--format=%H", f"{scene_commit}..HEAD", "--", *globs)
        drift = {
            "builderCommitsSinceScene": int(commits) if commits and commits.isdigit() else None,
            "measuredTo": newest or scene_commit,
            "exact": not shallow,
            "builderCommits": subjects.split("\n") if subjects else [],
        }
        if shallow:
            drift["x-note"] = ("shallow clone: this is a LOWER BOUND. Commits before the graft "
                               "point are invisible, so the scene may have been banked earlier "
                               "than sceneLastBuiltCommit says. Run `git fetch --unshallow`.")

    return {
        "historyIsComplete": not shallow,
        "sceneFile": scene_rel,
        "sceneFileSha256": _file_sha(os.path.join(root, scene_rel)),
        "sceneLastBuiltCommit": scene_commit,
        "sceneLastBuiltDate": scene_date,
        "sceneLastBuiltSubject": scene_subject,
        "sceneWorkingTreeDirty": bool(dirty),
        "builderDrift": drift,
        "heightMap": height_map,
        "readFrom": {
            "regionFrame": "Assets/_Project/Data/Regions/*.asset (source of truth)",
            "spriteCellAndPivot": "*.png.meta import settings (source of truth)",
            "rigIdentity": "docs/art/rigs/** bytes, LF-normalised sha256 (source of truth)",
            "placements": "the committed .unity — a DERIVED copy of builder C#, which cannot be "
                          "run outside Unity. Pinned by sceneLastBuiltCommit and builderDrift.",
        },
    }


def _file_sha(path):
    try:
        with open(path, "rb") as fh:
            return hashlib.sha256(fh.read()).hexdigest()
    except OSError:
        return None
