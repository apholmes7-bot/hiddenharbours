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

import datetime
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
        # encoding is explicit: `text=True` alone decodes in the platform locale, and a commit
        # subject full of em-dashes comes back mojibake'd through cp1252 on Windows — which then
        # lands in the package and makes the bytes machine-dependent.
        result = subprocess.run(
            ("git",) + args, cwd=repo_root, capture_output=True, check=False,
            text=True, encoding="utf-8", errors="replace",
        )
    except OSError:
        return None
    if result.returncode != 0:
        return None
    return result.stdout.strip()


_SHALLOW_NOTE = (
    "shallow clone: this checkout cannot see past its graft point, so WHEN THIS SCENE WAS BANKED "
    "IS NOT STATED HERE. `git log -1 -- <scene>` answers with the newest commit the clone can "
    "see that touched the path, and at depth 1 that is HEAD for every path — on a CI "
    "pull_request run, the synthetic refs/pull/N/merge commit. sceneLastBuiltCommit, its date, "
    "its subject and the whole builderDrift are therefore null rather than a plausible falsehood, "
    "and `generatedAt` is HEAD's committer date rather than the newest input commit's. Run "
    "`git fetch --unshallow` (or checkout with fetch-depth: 0) and re-export for the real answer.")


def collect(repo, region_name, scene_rel, height_map):
    root = repo.root
    scene_commit = _git(root, "log", "-1", "--format=%H", "--", scene_rel)
    scene_date = _git(root, "log", "-1", "--format=%ad", "--date=short", "--", scene_rel)
    scene_date_iso = _git(root, "log", "-1", "--format=%cI", "--", scene_rel)
    scene_subject = _git(root, "log", "-1", "--format=%s", "--", scene_rel)
    dirty = _git(root, "status", "--porcelain", "--", scene_rel)

    # A shallow clone (the usual shape in a CI or agent container) can only see back to its
    # graft point, so "last built at X" may really mean "at or before X" and the drift count is
    # a floor rather than a total. Saying which it is costs one git call.
    shallow = _git(root, "rev-parse", "--is-shallow-repository") == "true"

    # ⚠ AND AT DEPTH 1 IT IS NOT A FLOOR, IT IS A FICTION. `git log -1 -- <scene>` answers with
    # the newest commit the clone can SEE that touched the path — and a depth-1 clone can see
    # exactly one commit, so it answers HEAD for every path in the repo, whether or not HEAD ever
    # touched it. On a CI pull_request run HEAD is `refs/pull/N/merge`, a synthetic commit nobody
    # has locally, so the package would pin a sha that does not exist anywhere and that changes
    # every run. Measured: a real `git clone --depth 1` of this repo answers HEAD for both scenes;
    # with full history it answers 31f0d08a and ad852bd2, which are the true banks.
    #
    # So a shallow clone states NOTHING about when the scene was banked rather than stating
    # something false. All three fields go together — a null commit beside a live date and
    # subject would just move the fiction one field over — and the drift, which is measured FROM
    # that commit, goes with them. `historyIsComplete` already says which state you are holding.
    if shallow:
        scene_commit = scene_date = scene_subject = None

    drift = None
    if shallow:
        drift = {
            "builderCommitsSinceScene": None,
            "measuredTo": None,
            "measuredToDate": None,
            "exact": False,
            "builderCommits": [],
            "x-note": _SHALLOW_NOTE,
        }
    elif scene_commit:
        globs = BUILDER_GLOBS.get(region_name, [])
        commits = _git(root, "rev-list", "--count", f"{scene_commit}..HEAD", "--", *globs)
        subjects = _git(root, "log", "--format=%h %s", f"{scene_commit}..HEAD", "--", *globs)
        # Measured to the newest BUILDER commit, not to HEAD. The number only changes when the
        # answer changes, so a package does not go stale every time an unrelated commit lands —
        # and the exporter's own commits cannot invalidate its own output.
        newest = _git(root, "log", "-1", "--format=%H", f"{scene_commit}..HEAD", "--", *globs)
        newest_date = _git(root, "log", "-1", "--format=%cI", f"{scene_commit}..HEAD", "--", *globs)
        drift = {
            "builderCommitsSinceScene": int(commits) if commits and commits.isdigit() else None,
            "measuredTo": newest or scene_commit,
            "measuredToDate": newest_date or None,
            "exact": not shallow,
            "builderCommits": subjects.split("\n") if subjects else [],
        }
        if shallow:
            drift["x-note"] = ("shallow clone: this is a LOWER BOUND. Commits before the graft "
                               "point are invisible, so the scene may have been banked earlier "
                               "than sceneLastBuiltCommit says. Run `git fetch --unshallow`.")

    # The document's `generatedAt` is the committer date of the newest INPUT commit, never the
    # wall clock. A timestamp of the run would make the output non-reproducible and the --check
    # gate meaningless; a timestamp of the inputs says the one useful thing — what vintage of the
    # repo this is a picture of — and is identical on every re-run at that commit.
    # ⚠ On a shallow clone the newest input commit VISIBLE is HEAD, so this is HEAD's committer
    # date. That is still deterministic for a given checkout and still not a wall clock, but it
    # is not the vintage of the world it claims to be — x-shallowNote below says so, and the
    # field keeps a valid ISO-8601 value because the format requires one.
    generated_at = None
    if drift and drift.get("measuredTo"):
        generated_at = drift.get("measuredToDate") or scene_date_iso
    generated_at = _utc_z(generated_at or scene_date_iso)

    return {
        "generatedAt": generated_at,
        "historyIsComplete": not shallow,
        "sceneFile": scene_rel,
        "sceneFileSha256": _file_sha(os.path.join(root, scene_rel)),
        "sceneLastBuiltCommit": scene_commit,
        "sceneLastBuiltDate": scene_date,
        "sceneLastBuiltSubject": scene_subject,
        "sceneWorkingTreeDirty": bool(dirty),
        # Present only where it applies, so a full-clone package is byte-for-byte what it always
        # was and this change moves no committed bytes.
        **({"x-shallowNote": _SHALLOW_NOTE} if shallow else {}),
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


def _utc_z(iso):
    """Normalise a git ``%cI`` date to the UTC ``…Z`` form the reference package uses."""
    if not iso:
        return None
    try:
        return (
            datetime.datetime.fromisoformat(iso)
            .astimezone(datetime.timezone.utc)
            .strftime("%Y-%m-%dT%H:%M:%SZ")
        )
    except ValueError:
        return iso


def _file_sha(path):
    try:
        with open(path, "rb") as fh:
            return hashlib.sha256(fh.read()).hexdigest()
    except OSError:
        return None
