#!/usr/bin/env bash
# stamp-base.sh — download a MAIN run's test results as the gauntlet's base and write its provenance.
#
#   usage: tools/gauntlet/stamp-base.sh <run-id> <dir>
#
# Writes <dir>/editmode-results.xml, <dir>/playmode-results.xml and <dir>/STAMP (sha, run, createdAt,
# totals). pr-gauntlet.sh reads STAMP for --base-sha and for the run-predates-stamp check. Only stamp
# a run that is on MAIN, completed, success, and that you have authenticated by class against the
# previous stamp — the stamp is the seat's word that main is green at that sha.
set -u
set -o pipefail
RUNID="${1:-}"; DIR="${2:-}"
[ -n "$RUNID" ] && [ -n "$DIR" ] || { echo "usage: $0 <run-id> <dir>" >&2; exit 2; }
command -v gh >/dev/null || { echo "gh not on PATH" >&2; exit 2; }
PY=$(command -v python3 || command -v python) || { echo "python needed" >&2; exit 2; }
py() { "$PY" "$@" | tr -d '\r'; }   # Python on Windows prints \r\n even into a pipe

gh run view "$RUNID" --json headSha,headBranch,status,conclusion,createdAt,event > "/tmp/stamp-$RUNID.json" || exit 1
read -r SHA BR ST CONC CREATED EV < <(py -c 'import json,sys; d=json.load(open(sys.argv[1])); print(d["headSha"], d["headBranch"], d["status"], d["conclusion"], d["createdAt"], d["event"])' "/tmp/stamp-$RUNID.json")
if [ "$BR" != "main" ] || [ "$EV" != "push" ]; then echo "refusing: run $RUNID is $EV on '$BR', not a push to main" >&2; exit 1; fi
if [ "$ST" != "completed" ] || [ "$CONC" != "success" ]; then echo "refusing: run $RUNID is $ST / $CONC" >&2; exit 1; fi

mkdir -p "$DIR"
rm -f "$DIR"/*-results.xml "$DIR"/*.cases "$DIR"/*.classes "$DIR"/*.names
gh run download "$RUNID" -n test-results -D "$DIR" || exit 1
for m in editmode playmode; do
  [ -f "$DIR/$m-results.xml" ] || { echo "no $m-results.xml in the artifact" >&2; exit 1; }
  grep -oE '<test-case [^>]*>' "$DIR/$m-results.xml" > "$DIR/$m.cases"
  grep -oE 'classname="[^"]+"' "$DIR/$m.cases" | sed 's/classname=//;s/"//g' | sort | uniq -c | awk '{print $2" "$1}' > "$DIR/$m.classes"
  sed -E 's/.*fullname="([^"]+)".*classname="([^"]+)".*/\2\t\1/' "$DIR/$m.cases" | sort -u > "$DIR/$m.names"
done
ET=$(wc -l < "$DIR/editmode.cases" | tr -d ' '); EF=$(grep -c 'result="Failed"' "$DIR/editmode.cases"); ES=$(grep -c 'result="Skipped"' "$DIR/editmode.cases")
PT=$(wc -l < "$DIR/playmode.cases" | tr -d ' '); PF=$(grep -c 'result="Failed"' "$DIR/playmode.cases"); PS=$(grep -c 'result="Skipped"' "$DIR/playmode.cases")
if [ "$EF" != 0 ] || [ "$PF" != 0 ]; then echo "refusing: $EF EditMode / $PF PlayMode FAILED in run $RUNID — a red main is not a base" >&2; exit 1; fi
{
  echo "sha $SHA"
  echo "run $RUNID"
  echo "createdAt $CREATED"
  echo "editmode $ET total $ES skipped $EF failed"
  echo "playmode $PT total $PS skipped $PF failed"
  echo "stampedAt $(date -u +%Y-%m-%dT%H:%M:%SZ)"
} > "$DIR/STAMP"
cat "$DIR/STAMP"
