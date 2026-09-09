#!/usr/bin/env bash
# pr-gauntlet.sh — the coordinator's PR intake gauntlet for Hidden Harbours.
#
# Every PR must survive the same ordered battery before `gh pr merge N --squash`. Each check prints
# PASS / WARN / FAIL with its evidence; the script exits 1 on any FAIL. A WARN is a thing the seat
# must read, not a thing the script decides. The battery encodes the merge laws in memory
# (hidden-harbours-pr-workflow and its siblings); when a law changes, change the check here.
#
#   usage: tools/gauntlet/pr-gauntlet.sh <pr-number> --base <dir> [options]
#
#   --base <dir>        directory holding the STAMPED main run's editmode-results.xml and
#                       playmode-results.xml (gh run download <run> -n test-results -D <dir>)
#   --base-sha <sha>    the main commit that base was measured on (for the drift warning)
#   --work <dir>        scratch dir (default: a mktemp dir); the PR's artifact lands in <work>/run
#   --ack-removed       accept removed test names inside touched classes (after reading them)
#   --merged-ok         allow a MERGED/CLOSED PR (dry runs and post-mortems)
#   --skip-download     reuse <work>/run from an earlier invocation
#
# Needs: gh (authenticated), grep, awk, sed, sort, comm, join, python3 (JSON only). No Unity, no jq.

set -u
set -o pipefail

PR=""; BASE=""; BASE_SHA=""; WORK=""; ACK_REMOVED=0; MERGED_OK=0; SKIP_DL=0
while [ $# -gt 0 ]; do
  case "$1" in
    --base) BASE="$2"; shift 2 ;;
    --base-sha) BASE_SHA="$2"; shift 2 ;;
    --work) WORK="$2"; shift 2 ;;
    --ack-removed) ACK_REMOVED=1; shift ;;
    --merged-ok) MERGED_OK=1; shift ;;
    --skip-download) SKIP_DL=1; shift ;;
    -h|--help) sed -n '2,20p' "$0"; exit 0 ;;
    *) if [ -z "$PR" ]; then PR="$1"; shift; else echo "unknown arg: $1" >&2; exit 2; fi ;;
  esac
done
[ -n "$PR" ] || { echo "usage: $0 <pr-number> --base <dir> [--base-sha <sha>] [--work <dir>] [--ack-removed] [--merged-ok]" >&2; exit 2; }
[ -n "$BASE" ] && [ -f "$BASE/editmode-results.xml" ] && [ -f "$BASE/playmode-results.xml" ] \
  || { echo "--base must hold editmode-results.xml and playmode-results.xml" >&2; exit 2; }
command -v gh >/dev/null || { echo "gh not on PATH" >&2; exit 2; }
PY=$(command -v python3 || command -v python) || { echo "python needed for JSON" >&2; exit 2; }
py() { "$PY" "$@" | tr -d '\r'; }   # Python on Windows prints \r\n even into a pipe; a captured "main\r" is not "main"

[ -n "$WORK" ] || WORK=$(mktemp -d)
mkdir -p "$WORK"
# a base written by stamp-base.sh carries its own provenance
BASE_TIME=""
if [ -f "$BASE/STAMP" ]; then
  [ -n "$BASE_SHA" ] || BASE_SHA=$(awk '$1=="sha"{print $2}' "$BASE/STAMP")
  BASE_TIME=$(awk '$1=="createdAt"{print $2}' "$BASE/STAMP")
fi
RUN="$WORK/run"
REPO=$(gh repo view --json nameWithOwner --jq .nameWithOwner 2>/dev/null || echo "apholmes7-bot/hiddenharbours")
RECORD="$WORK/record-$PR.md"
FAILS=0; WARNS=0
LINES=()

say()  { echo "$1"; LINES+=("$1"); }
pass() { say "PASS  $1"; }
warn() { say "WARN  $1"; WARNS=$((WARNS+1)); }
fail() { say "FAIL  $1"; FAILS=$((FAILS+1)); }
note() { say "      $1"; }
hdr()  { say ""; say "## $1"; }

jget() { py -c 'import json,sys; d=json.load(open(sys.argv[1]));
for k in sys.argv[2].split("."):
    d = d[int(k)] if isinstance(d,list) else d.get(k)
    if d is None: break
print("" if d is None else (json.dumps(d) if isinstance(d,(list,dict)) else d))' "$1" "$2"; }

echo "# PR gauntlet — #$PR — $(date -u +%Y-%m-%dT%H:%MZ) — repo $REPO"
echo "  base: $BASE ${BASE_SHA:+(main $BASE_SHA)}"
echo "  work: $WORK"

# ─────────────────────────────────────────────────────────────────────────────── G1 PR facts
hdr "G1 · PR facts (open, on main, mergeable)"
gh pr view "$PR" --json number,title,state,isDraft,baseRefName,headRefName,headRefOid,mergeable,mergeStateStatus,mergeCommit,files,author \
  > "$WORK/pr.json" || { fail "gh pr view $PR failed"; echo; exit 1; }
TITLE=$(jget "$WORK/pr.json" title)
STATE=$(jget "$WORK/pr.json" state)
DRAFT=$(jget "$WORK/pr.json" isDraft)
BASEREF=$(jget "$WORK/pr.json" baseRefName)
HEADREF=$(jget "$WORK/pr.json" headRefName)
HEAD=$(jget "$WORK/pr.json" headRefOid)
MERGEABLE=$(jget "$WORK/pr.json" mergeable)
MSTATE=$(jget "$WORK/pr.json" mergeStateStatus)
AUTHOR=$(jget "$WORK/pr.json" author.login)
"$PY" -c 'import json,sys; [print(f["path"]) for f in json.load(open(sys.argv[1]))["files"]]' "$WORK/pr.json" > "$WORK/files.txt"
NFILES=$(wc -l < "$WORK/files.txt" | tr -d ' ')
note "#$PR \"$TITLE\" by $AUTHOR — $HEADREF @ ${HEAD:0:8} → $BASEREF; $NFILES files"

if [ "$STATE" = "OPEN" ]; then pass "state OPEN"
elif [ "$MERGED_OK" = 1 ]; then warn "state $STATE (allowed by --merged-ok; merge commit $(jget "$WORK/pr.json" mergeCommit.oid | cut -c1-8))"
else fail "state $STATE — nothing to merge"; fi

if [ "$BASEREF" = "main" ]; then pass "base branch is main"
else fail "base branch is '$BASEREF' — a STACKED PR: merge the parent PLAIN (never --delete-branch), then 'gh pr edit $PR --base main' and have the lane rebase --onto origin/main; no pull_request event fires on the base change, only on the push"; fi

case "$MERGEABLE" in
  MERGEABLE) pass "mergeable ($MSTATE)";;
  CONFLICTING) fail "CONFLICTING with main — a conflicting PR fires NO pull_request event, so a missing run is this, not CI; the lane rebases";;
  *) warn "mergeable=$MERGEABLE ($MSTATE) — GitHub has not computed it; re-run in a minute before trusting a missing run";;
esac
[ "$DRAFT" = "True" ] || [ "$DRAFT" = "true" ] && warn "DRAFT — a cloud lane's PR or one still owed a plate; do not merge a draft"

# ─────────────────────────────────────────────────────────────────────────────── G2 CI on the exact head
hdr "G2 · CI ran on the exact head sha, both jobs green"
gh run list --branch "$HEADREF" --event pull_request --limit 30 --json databaseId,headSha,status,conclusion,createdAt,event \
  > "$WORK/runs.json" || echo "[]" > "$WORK/runs.json"
RUNID=$(py -c 'import json,sys
runs=[r for r in json.load(open(sys.argv[1])) if r["headSha"]==sys.argv[2]]
runs.sort(key=lambda r:r["createdAt"], reverse=True)
print(runs[0]["databaseId"] if runs else "")' "$WORK/runs.json" "$HEAD")
if [ -z "$RUNID" ]; then
  fail "ZERO pull_request runs on head ${HEAD:0:8} — check mergeable (G1) first; a conflicting PR fires no event and an empty commit cannot help; a stacked PR gets no run until retargeted"
  RUNSTATUS=""; RUNCONC=""
else
  RUNSTATUS=$(py -c 'import json,sys; print([r for r in json.load(open(sys.argv[1])) if str(r["databaseId"])==sys.argv[2]][0]["status"])' "$WORK/runs.json" "$RUNID")
  RUNCONC=$(py -c 'import json,sys; print([r for r in json.load(open(sys.argv[1])) if str(r["databaseId"])==sys.argv[2]][0]["conclusion"])' "$WORK/runs.json" "$RUNID")
  RUNTIME=$(py -c 'import json,sys; print([r for r in json.load(open(sys.argv[1])) if str(r["databaseId"])==sys.argv[2]][0]["createdAt"])' "$WORK/runs.json" "$RUNID")
  note "run $RUNID on ${HEAD:0:8}: $RUNSTATUS / $RUNCONC, created $RUNTIME  (https://github.com/$REPO/actions/runs/$RUNID)"
  if [ "$RUNSTATUS" != "completed" ]; then fail "run $RUNID is $RUNSTATUS — wait; never merge on a pending run"
  elif [ "$RUNCONC" = "success" ]; then pass "run $RUNID completed success"
  else fail "run $RUNID concluded $RUNCONC"; fi
  # older runs on other heads are history, not evidence
  NOLDER=$(py -c 'import json,sys; print(len([r for r in json.load(open(sys.argv[1])) if r["headSha"]!=sys.argv[2]]))' "$WORK/runs.json" "$HEAD")
  [ "$NOLDER" -gt 0 ] && note "$NOLDER earlier run(s) on other heads of this branch — history, not evidence"
fi
# per-job view: the scene-export job is the exporter's --check; the tests job is Unity
gh pr checks "$PR" --json name,state,bucket,link > "$WORK/checks.json" 2>/dev/null || echo "[]" > "$WORK/checks.json"
"$PY" - "$WORK/checks.json" <<'PY'
import json, sys
for c in json.load(open(sys.argv[1])):
    print("      check: %s: %s (%s)" % (c["name"], c["state"], c["bucket"]))
PY
NBAD=$(py -c 'import json,sys; print(len([c for c in json.load(open(sys.argv[1])) if c["bucket"] not in ("pass",)]))' "$WORK/checks.json")
NCHK=$(py -c 'import json,sys; print(len(json.load(open(sys.argv[1]))))' "$WORK/checks.json")
if [ "$NCHK" = 0 ]; then warn "gh pr checks lists no checks (merged PR, or none reported yet)"
elif [ "$NBAD" = 0 ]; then pass "all $NCHK checks pass (scene-export --check and Unity tests)"
else fail "$NBAD of $NCHK checks not passing — 'gh pr view $PR' CLEAN is not 'its tests pass'"; fi

# ─────────────────────────────────────────────────────────────────────────────── G3 the run's own results
hdr "G3 · the run's test results: zero failed, skips unchanged"
if [ -n "$RUNID" ]; then
  if [ "$SKIP_DL" = 1 ] && [ -f "$RUN/editmode-results.xml" ]; then note "reusing $RUN"
  else rm -rf "$RUN"; mkdir -p "$RUN"; gh run download "$RUNID" -n test-results -D "$RUN" >/dev/null 2>&1 || fail "could not download test-results from run $RUNID"; fi
fi
cases() { # <dir> <mode> → writes <dir>/<mode>.cases .classes .names
  local d="$1" m="$2"
  [ -f "$d/$m-results.xml" ] || { : > "$d/$m.cases"; : > "$d/$m.classes"; : > "$d/$m.names"; return; }
  [ -s "$d/$m.cases" ] || grep -oE '<test-case [^>]*>' "$d/$m-results.xml" > "$d/$m.cases"
  [ -s "$d/$m.classes" ] || grep -oE 'classname="[^"]+"' "$d/$m.cases" | sed 's/classname=//;s/"//g' | sort | uniq -c | awk '{print $2" "$1}' > "$d/$m.classes"
  [ -s "$d/$m.names" ] || sed -E 's/.*fullname="([^"]+)".*classname="([^"]+)".*/\2\t\1/' "$d/$m.cases" | sort -u > "$d/$m.names"
}
summ() { # <dir> <mode> → "total passed skipped failed"
  local c="$1/$2.cases"; [ -f "$c" ] || { echo "0 0 0 0"; return; }
  echo "$(wc -l < "$c" | tr -d ' ') $(grep -c 'result="Passed"' "$c") $(grep -c 'result="Skipped"' "$c") $(grep -c 'result="Failed"' "$c")"
}
for m in editmode playmode; do cases "$BASE" $m; cases "$RUN" $m; done
declare -A T
for m in editmode playmode; do
  read -r bt bp bs bf <<< "$(summ "$BASE" $m)"; read -r rt rp rs rf <<< "$(summ "$RUN" $m)"
  T[$m]="$rt/$rp/$rs/$rf"
  note "$m: base $bt (passed $bp, skipped $bs, failed $bf) → run $rt (passed $rp, skipped $rs, failed $rf)"
  if [ "$rt" = 0 ]; then fail "$m: run has ZERO test cases — the artifact is missing or the job died; total=0 is a FALSE GREEN"
  elif [ "$rf" != 0 ]; then fail "$m: $rf FAILED"; grep 'result="Failed"' "$RUN/$m.cases" | grep -oE 'fullname="[^"]+"' | head -20 | sed 's/fullname=//;s/"//g;s/^/        /' | while read -r l; do note "$l"; done
  else pass "$m: 0 failed"; fi
  [ "$rs" != "$bs" ] && warn "$m: skipped count moved $bs → $rs — a new skip can hide a test; find it in the name-set diff"
done

# ─────────────────────────────────────────────────────────────────────────────── G4 by-class delta vs base
hdr "G4 · per-class delta against the stamped base — only the PR's OWN classes may move"
# own classes = classes declared in the PR's changed test files, read at the head sha (never the local checkout)
: > "$WORK/own.txt"
grep -E '^Assets/Tests/.*\.cs$' "$WORK/files.txt" | while read -r f; do
  gh api "repos/$REPO/contents/$f?ref=$HEAD" -H "Accept: application/vnd.github.raw+json" 2>/dev/null \
    | grep -oE '(class|struct)\s+[A-Za-z_][A-Za-z0-9_]*' | awk '{print $2}' >> "$WORK/own.txt" || true
done
sort -u -o "$WORK/own.txt" "$WORK/own.txt"
NOWN=$(wc -l < "$WORK/own.txt" | tr -d ' ')
note "own test classes declared in the PR's test files ($NOWN): $(tr '\n' ' ' < "$WORK/own.txt")"
DRIFT=0; STALE_RUN=0
MAINSHA=$(gh api "repos/$REPO/commits/main" --jq .sha 2>/dev/null || echo "")
if [ -n "$BASE_SHA" ] && [ -n "$MAINSHA" ] && [ "${MAINSHA:0:8}" != "${BASE_SHA:0:8}" ]; then
  DRIFT=1; warn "main is ${MAINSHA:0:8} but base was measured on ${BASE_SHA:0:8} — the PR ran on the MERGE ref, so classes from PRs merged since the stamp show as moved (POSITIVE, not own); re-stamp before trusting a non-own delta"
fi
if [ -n "$BASE_TIME" ] && [ -n "${RUNTIME:-}" ] && [[ "$RUNTIME" < "$BASE_TIME" ]]; then
  STALE_RUN=1; warn "this run ($RUNTIME) PREDATES the stamp ($BASE_TIME) — tests merged into main after the run was cut show as NEGATIVE, not own; that is age, not damage. To merge on a fresh picture the lane rebases (a push re-fires CI)"
fi
: > "$WORK/explained.classes"
for m in editmode playmode; do
  join -a1 -a2 -e0 -o '0,1.2,2.2' "$BASE/$m.classes" "$RUN/$m.classes" | awk '$2!=$3' > "$WORK/$m.delta"
  NET=$(awk '{s+=$3-$2} END{print s+0}' "$WORK/$m.delta")
  NCH=$(wc -l < "$WORK/$m.delta" | tr -d ' ')
  note "$m: $NCH class(es) moved, net $NET"
  NOTOWN=0; EXPLAINED=0
  while read -r cls b r; do
    simple="${cls##*.}"
    if grep -qx "$simple" "$WORK/own.txt"; then tag="own"
    elif [ "$STALE_RUN" = 1 ] && [ "$r" -lt "$b" ]; then tag="not own — later merge (run predates stamp)"; EXPLAINED=$((EXPLAINED+1)); echo "$cls" >> "$WORK/explained.classes"
    elif [ "$DRIFT" = 1 ] && [ "$r" -gt "$b" ]; then tag="not own — main drift (merged since stamp)"; EXPLAINED=$((EXPLAINED+1)); echo "$cls" >> "$WORK/explained.classes"
    else tag="NOT OWN"; NOTOWN=$((NOTOWN+1)); fi
    note "  $cls: $b → $r  [$tag]"
  done < "$WORK/$m.delta"
  if [ "$NOTOWN" = 0 ] && [ "$EXPLAINED" = 0 ]; then pass "$m: every moved class is the PR's own"
  elif [ "$NOTOWN" = 0 ]; then warn "$m: $EXPLAINED non-own class(es) explained by stamp/run age — read them; if any is NOT a later merge, this is a FAIL"
  else fail "$m: $NOTOWN class(es) moved outside the PR's own test files with no age to explain it — a test the PR did not write changed count; find out why before merging"; fi
done

# ─────────────────────────────────────────────────────────────────────────────── G5 name sets inside touched classes
hdr "G5 · name-set diff inside every moved class — a renamed test reads as a deletion"
REMOVED=0
for m in editmode playmode; do
  awk '{print $1}' "$WORK/$m.delta" | sort -u > "$WORK/$m.touched"
  [ -s "$WORK/$m.touched" ] || continue
  awk -F'\t' -v T="$WORK/$m.touched" 'BEGIN{while((getline l<T)>0) t[l]=1} ($1 in t){print $2}' "$RUN/$m.names" | sort -u > "$WORK/$m.run.names"
  awk -F'\t' -v T="$WORK/$m.touched" 'BEGIN{while((getline l<T)>0) t[l]=1} ($1 in t){print $2}' "$BASE/$m.names" | sort -u > "$WORK/$m.base.names"
  comm -23 "$WORK/$m.base.names" "$WORK/$m.run.names" > "$WORK/$m.removed.all"
  comm -13 "$WORK/$m.base.names" "$WORK/$m.run.names" > "$WORK/$m.added"
  # removed names in a class explained by age are that class's whole story, not a deletion
  if [ -s "$WORK/explained.classes" ]; then
    awk -v E="$WORK/explained.classes" 'BEGIN{while((getline l<E)>0) e[l]=1} {c=$0; sub(/\.[^.]*(\(.*)?$/,"",c); if(!(c in e)) print}' "$WORK/$m.removed.all" > "$WORK/$m.removed"
    NX=$(( $(wc -l < "$WORK/$m.removed.all") - $(wc -l < "$WORK/$m.removed") ))
    [ "$NX" -gt 0 ] && note "$m: $NX removed name(s) belong to age-explained classes and are not counted"
  else cp "$WORK/$m.removed.all" "$WORK/$m.removed"; fi
  NA=$(wc -l < "$WORK/$m.added" | tr -d ' '); NR=$(wc -l < "$WORK/$m.removed" | tr -d ' ')
  note "$m: +$NA added, -$NR removed test names inside the moved classes"
  head -40 "$WORK/$m.added" | sed 's/^/        + /' | while read -r l; do note "$l"; done
  head -40 "$WORK/$m.removed" | sed 's/^/        - /' | while read -r l; do note "$l"; done
  REMOVED=$((REMOVED+NR))
done
if [ "$REMOVED" = 0 ]; then pass "no test name disappeared"
elif [ "$ACK_REMOVED" = 1 ]; then warn "$REMOVED test name(s) removed — ACKNOWLEDGED by --ack-removed (renames or deliberate deletions read in the diff)"
else fail "$REMOVED test name(s) removed inside the moved classes — read each in the diff (rename? deletion? a guard silenced?), then re-run with --ack-removed"; fi

# ─────────────────────────────────────────────────────────────────────────────── G6 diff hygiene
hdr "G6 · diff hygiene: markers, forbidden paths, Unity-rewritten assets, LFS"
gh pr diff "$PR" > "$WORK/pr.diff" 2>/dev/null || : > "$WORK/pr.diff"
if grep -nE '^\+(<<<<<<< |=======$|>>>>>>> )' "$WORK/pr.diff" | head -5 | grep -q .; then
  fail "CONFLICT MARKERS in added lines — 'resolver && git add && git rebase --continue' on one line commits them; the lane re-resolves"
else pass "no conflict markers"; fi
if grep -qE '^(Library|Temp|Logs|Builds?|obj)/|\.csproj$|\.sln$' "$WORK/files.txt"; then
  fail "forbidden paths in the PR: $(grep -E '^(Library|Temp|Logs|Builds?|obj)/|\.csproj$|\.sln$' "$WORK/files.txt" | tr '\n' ' ')"
else pass "no Library/Temp/Builds/csproj/sln"; fi
if grep -qE '^Assets/_Project/Data/Boats/.*\.asset$' "$WORK/files.txt"; then
  warn "boat Def assets touched: $(grep -E '^Assets/_Project/Data/Boats/.*\.asset$' "$WORK/files.txt" | tr '\n' ' ')— a Unity run REWRITES these; confirm the change is authored, not 'git add -A' residue"
fi
if grep -qE '^ProjectSettings/' "$WORK/files.txt"; then warn "ProjectSettings touched: $(grep -E '^ProjectSettings/' "$WORK/files.txt" | tr '\n' ' ')— an editor version or build-settings change needs lead-architect eyes (ADR 0005)"; fi
BIN=$(grep -iE '\.(png|jpg|jpeg|psd|wav|ogg|mp3|fbx|tga|aseprite|ttf|otf)$' "$WORK/files.txt" || true)
if [ -n "$BIN" ]; then
  NOLFS=""
  while read -r f; do
    [ -n "$f" ] || continue
    attr=$(git check-attr filter -- "$f" 2>/dev/null | awk '{print $NF}')
    [ "$attr" = "lfs" ] || NOLFS="$NOLFS $f"
  done <<< "$BIN"
  if [ -n "$NOLFS" ]; then fail "binary files NOT under LFS per .gitattributes:$NOLFS"; else pass "every binary in the PR is LFS-tracked"; fi
fi
if grep -qE '\.meta$' "$WORK/files.txt"; then
  # every new asset needs its meta and vice versa
  "$PY" - "$WORK/files.txt" <<'PY' > "$WORK/meta.txt"
import sys
files=[l.strip() for l in open(sys.argv[1]) if l.strip()]
s=set(files)
for f in files:
    if f.endswith(".meta"):
        base=f[:-5]
        if base.startswith("Assets/") and base not in s and not base.endswith("/"):
            print("meta without asset: "+f)
    elif f.startswith("Assets/") and (f+".meta") not in s and "/" in f:
        pass
PY
  if [ -s "$WORK/meta.txt" ]; then warn "$(head -3 "$WORK/meta.txt" | tr '\n' ';') — a meta whose asset is not in the PR (moved? deleted? folder meta is fine)"; fi
fi

# ─────────────────────────────────────────────────────────────────────────────── G7 scene / builder touch
hdr "G7 · a PR that touches a SCENE or BUILDER re-runs the exporter"
SCENE=$(grep -E '^Assets/_Project/Scenes/.*\.unity$|(^|/)[A-Za-z]*Builder[A-Za-z]*\.cs$|^Assets/_Project/Prefabs/.*\.prefab$' "$WORK/files.txt" || true)
PKG=$(grep -E '^tools/scene-export/packages/' "$WORK/files.txt" || true)
if [ -z "$SCENE" ]; then pass "no scene or builder file touched"
else
  note "scene/builder files: $(echo "$SCENE" | tr '\n' ' ')"
  if [ -n "$PKG" ]; then pass "export packages regenerated in the same PR ($(echo "$PKG" | wc -l | tr -d ' ') package files)"
  else warn "scene/builder touched but tools/scene-export/packages/ NOT regenerated — the CI scene-export job's --check is the arbiter (G2); if it is green the change was invisible to the exporter (sprite-less object?) — ask the lane"; fi
  grep -qE '^Assets/_Project/Scenes/StPeters\.unity$' "$WORK/files.txt" && warn "StPeters.unity touched — it cannot be REBUILT (≈590 hunks stale); a hand patch rebases by regeneration; a rebuilt scene regenerates every id"
fi

# ─────────────────────────────────────────────────────────────────────────────── G8 fixture-law lint on added test lines
hdr "G8 · fixture-law lint on ADDED lines (WARNs for the source read; one FAIL: GetInstanceID)"
W0=$WARNS; F0=$FAILS
awk '/^\+\+\+ b\//{f=substr($0,7)} /^\+[^+]/{print f"\t"substr($0,2)}' "$WORK/pr.diff" > "$WORK/added.tsv"
grep -E '^Assets/Tests/' "$WORK/added.tsv" > "$WORK/added.tests.tsv" || : > "$WORK/added.tests.tsv"
grep -vE '^Assets/Tests/' "$WORK/added.tsv" | grep -E '^Assets/_Project/Code/' > "$WORK/added.code.tsv" || : > "$WORK/added.code.tsv"
lint() { # <file> <regex> <label> <level>
  local hits; hits=$(grep -E "$2" "$1" | head -6 || true)
  [ -n "$hits" ] || return 0
  local n; n=$(grep -cE "$2" "$1")
  if [ "$4" = fail ]; then fail "$3 ($n line(s))"; else warn "$3 ($n line(s))"; fi
  echo "$hits" | awk -F'\t' '{printf "        %s: %s\n", $1, substr($2,1,110)}' | while read -r l; do note "$l"; done
}
lint "$WORK/added.tests.tsv" 'GetInstanceID' "GetInstanceID — obsolete-as-ERROR on Unity 6.5" fail
lint "$WORK/added.code.tsv"  'GetInstanceID' "GetInstanceID in production code — obsolete-as-ERROR on Unity 6.5" fail
lint "$WORK/added.tests.tsv" '\.Clear<' "Clear<T>() in a test UNSUBSCRIBES production's listener — the fixture consumes registration, never performs it" warn
lint "$WORK/added.tests.tsv" '\.Register(<|\()' "Register in a test — a fixture that PRE-REGISTERS what production should register tests nothing" warn
lint "$WORK/added.tests.tsv" 'LoadSceneMode\.Single' "LoadSceneMode.Single in a test — Single-loading a REGION leaks the persistent core past teardown" warn
lint "$WORK/added.tests.tsv" 'StartNewGame\(' "StartNewGame in a test — the fixture sits on the TITLE PAGE and reads no input (ShellFlow.Reset())" warn
RC=$(git grep -hoE 'RequireComponent\(typeof\([A-Za-z0-9_.]+\)' -- 'Assets/_Project/Code' 2>/dev/null | sed 's/.*typeof(//;s/)//;s/.*\.//' | sort -u | grep -vE '^(Renderer|SpriteRenderer|Rigidbody2D|CircleCollider2D|CapsuleCollider2D)$' | paste -sd'|' -)
[ -n "$RC" ] && lint "$WORK/added.tests.tsv" "AddComponent<($RC)>" "AddComponent of a [RequireComponent]-added type in a test — a DUPLICATE makes every negative assertion vacuous; RESOLVE it (GetComponent) instead" warn
lint "$WORK/added.tests.tsv" '(GameTimeSeconds|SetClock|clock)[^;]*=[^;=]*[0-9]{3,}' "a hard-coded clock in a test — inside a SEEDED window this tests the seed; evaluate the same hash" warn
lint "$WORK/added.tests.tsv" 'savegame|SaveGame\.Write|SaveSystem\.Save' "a test that touches the save — the player's savegame is SHARED by every worktree" warn
lint "$WORK/added.code.tsv"  'new (System\.)?Random\(\)|UnityEngine\.Random\.(value|Range|insideUnit)' "unseeded randomness in production code — rule 5: sim is deterministic from (worldSeed, gameTime)" warn
lint "$WORK/added.code.tsv"  '\b[0-9]+\.[0-9]+f\b' "float literals added in production code — rule 6: is each a tunable that belongs in a Def/GameConfig?" warn
lint "$WORK/added.code.tsv"  'using HiddenHarbours\.(Boats|Fishing|Economy|World|UI|Audio)\b' "a cross-module using — rule 4: modules talk through Core interfaces + EventBus; check it is not a feature→feature reach" warn
[ -s "$WORK/added.tests.tsv" ] || note "no test lines added"
[ "$WARNS" = "$W0" ] && [ "$FAILS" = "$F0" ] && pass "no fixture-law pattern in the added lines"

# ─────────────────────────────────────────────────────────────────────────────── record
hdr "Verdict"
if [ "$FAILS" = 0 ]; then say "GAUNTLET PASSED — $WARNS warning(s) to read. Merge: gh pr merge $PR --squash   (PLAIN — never --delete-branch)"
else say "GAUNTLET FAILED — $FAILS failure(s), $WARNS warning(s). Do not merge."; fi

{
  echo "### Gauntlet record — #$PR ${HEAD:0:8} — $(date -u +%Y-%m-%dT%H:%MZ)"
  echo "- run ${RUNID:-none} · EditMode ${T[editmode]} · PlayMode ${T[playmode]} (total/passed/skipped/failed)"
  echo "- base: ${BASE_SHA:-unstated} · own classes: $(tr '\n' ' ' < "$WORK/own.txt")"
  for m in editmode playmode; do
    [ -s "$WORK/$m.delta" ] && { echo "- $m delta:"; awk '{print "  - "$1": "$2" → "$3}' "$WORK/$m.delta"; }
    [ -s "$WORK/$m.removed" ] && { echo "- $m removed names:"; sed 's/^/  - /' "$WORK/$m.removed"; }
  done
  echo "- verdict: $([ "$FAILS" = 0 ] && echo PASS || echo FAIL) ($FAILS fail / $WARNS warn)"
  echo
  echo '```'
  printf '%s\n' "${LINES[@]}"
  echo '```'
} > "$RECORD"
echo
echo "record → $RECORD  (paste the top block as the merge comment)"
[ "$FAILS" = 0 ]
