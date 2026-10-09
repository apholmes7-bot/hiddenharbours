#!/usr/bin/env python3
"""The editor slot keeper: hands out the machine's one Unity editor slot in queue order.

Stdlib only. Windows only: it reads commit headroom through kernel32 and processes through CIM.
Every path lives in the live config (``slot.json``), never in this file: the repo is public.

    python slot.py status [--json]
    python slot.py queue add --label L --box PATH --minutes N --by seat|owner
    python slot.py queue remove L  |  queue move L POS  |  queue list
    python slot.py pause [--reason TEXT]  |  resume  |  stop
    python slot.py take --label L
    python slot.py wait --label L --max-minutes M
    python slot.py launch --lease ID [--max-minutes 9] -- <unity args>
    python slot.py wait-run --lease ID [--max-minutes 9]
    python slot.py keep --lease ID
    python slot.py give --lease ID [--stop]

Each command prints ONE summary line first; ``--json`` adds the full state as JSON after it.
Exit codes: 0 done / GRANTED, 1 an error, 2 REFUSED, 3 QUEUED / STILL QUEUED / still RUNNING.

The rules it enforces (R1-R9) and the state it keeps are in README.md beside this file.
"""

import argparse
import contextlib
import datetime
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
import traceback

HERE = os.path.dirname(os.path.abspath(__file__))

GIB = 1024 ** 3
GB = 1000 ** 3

EXIT_OK, EXIT_ERROR, EXIT_REFUSED, EXIT_QUEUED = 0, 1, 2, 3

REQUIRED_KEYS = ("unity_exe", "owner_project", "save_path", "state_dir")

# The slot's policy numbers. The live slot.json may override any of them; the owner tunes there.
DEFAULTS = {
    "unity_exe_args": [],
    "disk_path": "C:\\",
    "allusersprofile": "C:\\ProgramData",
    "editor_rule": None,
    "launch_headroom_gib": 16,
    "stop_headroom_gib": 4,
    "min_disk_gb": 15,
    "sample_seconds": 5,
    "grace_minutes": 15,
    "idle_minutes": 30,
    "wait_heartbeat_seconds": 120,
    "max_lease_minutes": 240,
    "wait_check_seconds": 15,
}

# How a unity.exe is told apart (all comparisons lower-case; an empty string matches anything).
# A trial points "image" and the cmdline keys at a stand-in instead of the real editor.
DEFAULT_RULE = {
    "image": "unity.exe",
    "exe_path_contains": "\\hub\\editor\\",
    "exe_path_endswith": "\\editor\\unity.exe",
    "cmdline_contains": "-projectpath",
    "worker_cmdline_contains": "assetimportworker",
    "bridge_cmdline_endswith": " mcp",
    "hub_exe_path_contains": "\\unity hub\\resources\\",
}

# Graphics flags that would fight the -force-d3d11 the keeper adds (R3).
OTHER_GFX_FLAGS = ("-force-d3d12", "-force-vulkan", "-force-glcore", "-force-gles", "-force-opengl",
                   "-force-metal")

# How the keeper waits on itself. These are mechanics, not slot policy (policy is the config above).
LOCK_WAIT_SECONDS = 30.0
LOCK_POLL_SECONDS = 0.1
LOCK_EMPTY_STALE_SECONDS = 10.0
SPAWN_GRACE_SECONDS = 60.0
TREE_EXIT_WAIT_SECONDS = 60.0
TREE_EXIT_POLL_SECONDS = 2.0
KILL_SETTLE_SECONDS = 10.0
EDITOR_EXIT_WAIT_SECONDS = 30.0
STOP_WAIT_SECONDS = 180.0
RUN_POLL_SECONDS = 3.0
CREATED_TOLERANCE_MS = 1000
PORCELAIN_MAX_LINES = 200
RETURNS_SHOWN = 3
# How long `launch` and `wait-run` wait before answering RUNNING: under a lane's 10-minute tool call
# (the owner's ruling, 10-08). The watchdog keeps the editor; the lane calls `wait-run` again.
CALL_WAIT_MINUTES = 9.0
REPLACE_RETRIES = 20

LABEL_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,47}$")

# Windows process-creation flags.
DETACHED_PROCESS = 0x00000008
CREATE_NEW_PROCESS_GROUP = 0x00000200
CREATE_BREAKAWAY_FROM_JOB = 0x01000000
CREATE_NO_WINDOW = 0x08000000


class SlotError(Exception):
    """A failure the CLI reports as ERROR (exit 1)."""


class LockBusy(SlotError):
    pass


class ProbeError(SlotError):
    pass


class Result:
    def __init__(self, code, line, details=None, data=None):
        self.code, self.line, self.details, self.data = code, line, details or [], data


class Proc:
    """One row of the process list. ``created`` is epoch milliseconds (0 when unknown)."""

    __slots__ = ("pid", "ppid", "name", "exe", "cmd", "created")

    def __init__(self, pid, ppid, name, exe, cmd, created):
        self.pid, self.ppid, self.name, self.exe, self.cmd, self.created = (
            int(pid), int(ppid or 0), name or "", exe or "", cmd or "", int(created or 0))

    def brief(self):
        return {"pid": self.pid, "ppid": self.ppid, "name": self.name, "cmd": self.cmd[:400]}


# --------------------------------------------------------------------------------------------
# Config
# --------------------------------------------------------------------------------------------

def make_config(raw):
    if not isinstance(raw, dict):
        raise SlotError("the config must be a JSON object")
    allowed = set(REQUIRED_KEYS) | set(DEFAULTS)
    unknown = sorted(k for k in raw if k not in allowed and not k.startswith("_"))
    if unknown:
        raise SlotError("unknown config keys: %s" % ", ".join(unknown))
    missing = [k for k in REQUIRED_KEYS if not raw.get(k)]
    if missing:
        raise SlotError("the config lacks %s" % ", ".join(missing))
    placeholders = [k for k in REQUIRED_KEYS if str(raw[k]).startswith("<")]
    if placeholders:
        raise SlotError("fill the placeholder paths: %s" % ", ".join(placeholders))
    cfg = dict(DEFAULTS)
    cfg.update({k: v for k, v in raw.items() if not k.startswith("_")})
    rule = dict(DEFAULT_RULE)
    given = raw.get("editor_rule") or {}
    bad = sorted(k for k in given if k not in DEFAULT_RULE)
    if bad:
        raise SlotError("unknown editor_rule keys: %s" % ", ".join(bad))
    rule.update(given)
    cfg["editor_rule"] = {k: str(v).lower() for k, v in rule.items()}
    if not isinstance(cfg["unity_exe_args"], list):
        raise SlotError("unity_exe_args must be a list")
    for key in ("launch_headroom_gib", "stop_headroom_gib", "min_disk_gb", "sample_seconds", "grace_minutes",
                "idle_minutes", "wait_heartbeat_seconds", "max_lease_minutes", "wait_check_seconds"):
        if not isinstance(cfg[key], (int, float)) or cfg[key] < 0:
            raise SlotError("%s must be a number >= 0" % key)
    return cfg


def load_config(path):
    try:
        with open(path, encoding="utf-8-sig") as f:
            raw = json.load(f)
    except FileNotFoundError:
        raise SlotError("no config at %s (pass --config, or set HH_SLOT_CONFIG)" % path)
    except ValueError as e:
        raise SlotError("the config at %s is not JSON: %s" % (path, e))
    return make_config(raw)


# --------------------------------------------------------------------------------------------
# Small pure helpers
# --------------------------------------------------------------------------------------------

def iso(ts):
    if ts is None:
        return None
    return datetime.datetime.fromtimestamp(ts, datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def hm(ts):
    if ts is None:
        return "?"
    return datetime.datetime.fromtimestamp(ts, datetime.timezone.utc).strftime("%H:%M:%SZ")


def path_key(p):
    """A path for comparison: absolute, backslashes, lower case, no trailing slash (R2)."""
    s = os.path.normpath(os.path.abspath(str(p))).replace("/", "\\").lower()
    if s.startswith("\\\\?\\"):
        s = s[4:]
    return s.rstrip("\\") if len(s) > 3 else s


def real_key(p):
    """path_key after resolving junctions and links, so a link into the owner's project is caught."""
    try:
        return path_key(os.path.realpath(str(p)))
    except (OSError, ValueError):
        return path_key(p)


def same_or_under(path, root):
    a, b = path_key(path), path_key(root)
    return a == b or a.startswith(b.rstrip("\\") + "\\")


def owner_conflict(box, owner):
    return same_or_under(box, owner) or same_or_under(real_key(box), real_key(owner))


def names_path(cmdline, path):
    """True when the command line names ``path`` itself (not merely a longer sibling path)."""
    cmd = (cmdline or "").replace("/", "\\").lower()
    target = path_key(path)
    i = cmd.find(target)
    while i >= 0:
        before = cmd[i - 1:i] if i else ""
        after = cmd[i + len(target):i + len(target) + 1]
        if before in ("", " ", '"', "'", "=") and after in ("", " ", '"', "'", "\\"):
            return True
        i = cmd.find(target, i + 1)
    return False


def classify(proc, rule):
    """editor / worker / bridge / hub / unknown for a process of the rule's image, else None."""
    if proc.name.lower() != rule["image"]:
        return None
    cmd = proc.cmd.strip().lower()
    exe = proc.exe.lower()
    if rule["bridge_cmdline_endswith"] and cmd.endswith(rule["bridge_cmdline_endswith"]):
        return "bridge"
    if rule["hub_exe_path_contains"] and rule["hub_exe_path_contains"] in exe:
        return "hub"
    if rule["worker_cmdline_contains"] and rule["worker_cmdline_contains"] in cmd:
        return "worker"
    if (rule["exe_path_contains"] in exe and exe.endswith(rule["exe_path_endswith"])
            and rule["cmdline_contains"] in cmd):
        return "editor"
    return "unknown"


def classify_all(procs, rule):
    out = {"editor": [], "worker": [], "bridge": [], "hub": [], "unknown": []}
    for p in procs:
        c = classify(p, rule)
        if c:
            out[c].append(p)
    return out


def taskkill_argv(pid):
    """The one way the keeper closes anything: the tree, by PID (R7)."""
    return ["taskkill", "/PID", str(int(pid)), "/T", "/F"]


def prepare_args(args, box):
    """R3. Returns (final args, None) or (None, why refused)."""
    out = list(args)
    low = [a.lower() for a in out]
    if "-nographics" in low:
        return None, "-nographics is refused (R3): runs on the slot keep their GPU"
    for a in low:
        for flag in OTHER_GFX_FLAGS:
            if a.startswith(flag):
                return None, "%s is refused (R3): the slot runs on -force-d3d11" % a
    plain = [i for i, a in enumerate(low) if a == "-projectpath"]
    joined = [i for i, a in enumerate(low) if a.startswith("-projectpath=")]
    if len(plain) + len(joined) > 1:
        return None, "more than one -projectPath (R3)"
    given = None
    if plain:
        i = plain[0]
        if i + 1 >= len(out):
            return None, "-projectPath has no value (R3)"
        given = out[i + 1]
    elif joined:
        given = out[joined[0]].split("=", 1)[1]
    if given is not None and path_key(given) != path_key(box):
        return None, "-projectPath %s is not the lease's box %s (R3)" % (given, box)
    if given is None:
        out = ["-projectPath", box] + out
    if "-force-d3d11" not in low:
        out.append("-force-d3d11")
    return out, None


def find_key(obj, key):
    stack = [obj]
    while stack:
        o = stack.pop()
        if isinstance(o, dict):
            if key in o:
                return o[key]
            stack.extend(o.values())
        elif isinstance(o, list):
            stack.extend(o)
    return None


def describe_save(data):
    sha = hashlib.sha256(data).hexdigest().upper()
    try:
        seed = find_key(json.loads(data.decode("utf-8-sig")), "WorldSeed")
    except (ValueError, UnicodeDecodeError):
        seed = None
    return {"sha256": sha, "sha256_prefix": sha[:12], "size": len(data), "world_seed": seed}


def read_save(path):
    try:
        with open(path, "rb") as f:
            data = f.read()
    except FileNotFoundError:
        return {"absent": True}, None
    return describe_save(data), data


def atomic_write_bytes(path, data):
    """Write to a temp file beside ``path``, then os.replace it into place."""
    tmp = "%s.tmp.%d" % (path, os.getpid())
    with open(tmp, "wb") as f:
        f.write(data)
        f.flush()
        os.fsync(f.fileno())
    for attempt in range(REPLACE_RETRIES):
        try:
            os.replace(tmp, path)
            return
        except PermissionError:
            if attempt == REPLACE_RETRIES - 1:
                raise
            time.sleep(0.05)


def ident(pair):
    return {"pid": pair[0], "created_ms": pair[1]}


def short_save(s):
    if not s:
        return None
    if s.get("absent"):
        return "absent"
    return "%s %d B seed %s" % (s["sha256_prefix"], s["size"], s.get("world_seed"))


# --------------------------------------------------------------------------------------------
# The state folder
# --------------------------------------------------------------------------------------------

class Store:
    """The keeper's live home: queue.json, lease.json, paused, log.jsonl, returns/, saves/, runs/, lock."""

    def __init__(self, state_dir, machine):
        self.dir = state_dir
        self.m = machine

    def p(self, *parts):
        return os.path.join(self.dir, *parts)

    def exists(self):
        return os.path.isdir(self.dir)

    def ensure(self):
        if not self.exists():
            raise SlotError("state_dir %s does not exist; the keeper never creates its live home" % self.dir)
        for sub in ("returns", "saves", "runs"):
            os.makedirs(self.p(sub), exist_ok=True)

    def read(self, name, default):
        try:
            with open(self.p(name), encoding="utf-8") as f:
                return json.load(f)
        except FileNotFoundError:
            return default
        except ValueError as e:
            raise SlotError("state file %s is not JSON: %s" % (self.p(name), e))

    def write(self, name, obj):
        data = (json.dumps(obj, indent=1, sort_keys=True) + "\n").encode("utf-8")
        os.makedirs(os.path.dirname(self.p(name)), exist_ok=True)
        atomic_write_bytes(self.p(name), data)

    def log(self, event, **fields):
        rec = {"at": iso(self.m.now()), "event": event}
        rec.update(fields)
        with open(self.p("log.jsonl"), "a", encoding="utf-8", newline="\n") as f:
            f.write(json.dumps(rec, sort_keys=True) + "\n")

    @contextlib.contextmanager
    def lock(self, what):
        """ONE lock for every state change: a file made by exclusive create, holding a PID and a time."""
        path = self.p("lock")
        pid, created = self.m.self_identity()
        token = os.urandom(8).hex()
        mine = json.dumps({"pid": pid, "created_ms": created, "at": iso(self.m.now()), "what": what,
                           "token": token})
        deadline = self.m.now() + LOCK_WAIT_SECONDS
        while True:
            try:
                fd = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
            except FileExistsError:
                if self._break_if_stale(path):
                    continue
                if self.m.now() >= deadline:
                    raise LockBusy("the state lock %s stayed busy for %ds: %s" % (
                        path, LOCK_WAIT_SECONDS, self._peek(path)))
                self.m.sleep(LOCK_POLL_SECONDS)
                continue
            with os.fdopen(fd, "w", encoding="utf-8") as f:
                f.write(mine)
            break
        try:
            yield
        finally:
            try:
                with open(path, encoding="utf-8") as f:
                    still_mine = token in f.read()
                if still_mine:
                    os.remove(path)
            except FileNotFoundError:
                pass

    def _peek(self, path):
        try:
            with open(path, encoding="utf-8") as f:
                return f.read().strip()
        except OSError:
            return "?"

    def _break_if_stale(self, path):
        """A lock whose holder PID is dead is broken and recorded. Returns True to retry at once."""
        try:
            with open(path, encoding="utf-8") as f:
                text = f.read()
            mtime = os.stat(path).st_mtime
        except FileNotFoundError:
            return True
        except PermissionError:
            return False
        try:
            holder = json.loads(text)
        except ValueError:
            holder = None
        if not isinstance(holder, dict):
            if time.time() - mtime < LOCK_EMPTY_STALE_SECONDS:
                return False
            why = "an unreadable lock older than %ds" % LOCK_EMPTY_STALE_SECONDS
        else:
            if self.m.alive(holder.get("pid"), holder.get("created_ms")) is not False:
                return False
            why = "its holder PID %s is dead" % holder.get("pid")
        aside = "%s.broken.%d.%s" % (path, os.getpid(), os.urandom(4).hex())
        try:
            os.rename(path, aside)
        except OSError:
            return True
        try:
            with open(aside, encoding="utf-8") as f:
                taken = f.read()
        except OSError:
            taken = None
        if taken != text:
            # Someone broke it first and made a fresh lock, which we just moved: put it back.
            try:
                os.rename(aside, path)
            except OSError:
                self.log("lock contention", note="a fresh lock was moved aside and could not go back",
                         aside=aside)
            return False
        self.log("lock broken", why=why, holder=holder if holder is not None else text)
        os.remove(aside)  # R9: the keeper's own temporary file, inside its state folder
        return True


# --------------------------------------------------------------------------------------------
# The real machine (probes and actions). Tests replace it with a fake that has the same methods.
# --------------------------------------------------------------------------------------------

PS_PROCESSES = (
    "[Console]::OutputEncoding = [Text.Encoding]::UTF8; "
    "$rows = @(Get-CimInstance Win32_Process | ForEach-Object { [pscustomobject]@{ "
    "pid = [int]$_.ProcessId; ppid = [int]$_.ParentProcessId; name = [string]$_.Name; "
    "exe = [string]$_.ExecutablePath; cmd = [string]$_.CommandLine; "
    "created = $(if ($_.CreationDate) { ([DateTimeOffset]$_.CreationDate).ToUnixTimeMilliseconds() } else { 0 }) "
    "} }); ConvertTo-Json -InputObject $rows -Compress -Depth 2"
)

PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
SYNCHRONIZE = 0x00100000
STILL_ACTIVE = 259
ERROR_INVALID_PARAMETER = 87


def popen_detached(cmd, **kw):
    """Start a process outside the caller's console and process group, breaking away from a job if allowed."""
    flags = DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP
    try:
        return subprocess.Popen(cmd, creationflags=flags | CREATE_BREAKAWAY_FROM_JOB,
                                stdin=subprocess.DEVNULL, **kw)
    except OSError:
        return subprocess.Popen(cmd, creationflags=flags, stdin=subprocess.DEVNULL, **kw)


class _WinHandle:
    def __init__(self, machine, pid, created_ms, popen=None, handle=None):
        self.m, self.pid, self.created_ms, self.popen, self.handle = machine, pid, created_ms, popen, handle

    def poll(self):
        if self.popen is not None:
            return self.popen.poll()
        ok, code = self.m._exit_code(self.handle)
        if not ok or code == STILL_ACTIVE:
            return None
        return code


class RealMachine:
    def __init__(self):
        import ctypes
        from ctypes import wintypes

        class MEMORYSTATUSEX(ctypes.Structure):
            _fields_ = [("dwLength", wintypes.DWORD), ("dwMemoryLoad", wintypes.DWORD),
                        ("ullTotalPhys", ctypes.c_ulonglong), ("ullAvailPhys", ctypes.c_ulonglong),
                        ("ullTotalPageFile", ctypes.c_ulonglong), ("ullAvailPageFile", ctypes.c_ulonglong),
                        ("ullTotalVirtual", ctypes.c_ulonglong), ("ullAvailVirtual", ctypes.c_ulonglong),
                        ("ullAvailExtendedVirtual", ctypes.c_ulonglong)]

        k = ctypes.WinDLL("kernel32", use_last_error=True)
        k.OpenProcess.restype = wintypes.HANDLE
        k.OpenProcess.argtypes = (wintypes.DWORD, wintypes.BOOL, wintypes.DWORD)
        k.GetExitCodeProcess.restype = wintypes.BOOL
        k.GetExitCodeProcess.argtypes = (wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD))
        k.GetProcessTimes.restype = wintypes.BOOL
        k.GetProcessTimes.argtypes = (wintypes.HANDLE,) + (ctypes.POINTER(wintypes.FILETIME),) * 4
        k.CloseHandle.argtypes = (wintypes.HANDLE,)
        k.GlobalMemoryStatusEx.restype = wintypes.BOOL
        k.GlobalMemoryStatusEx.argtypes = (ctypes.POINTER(MEMORYSTATUSEX),)
        self._ct, self._wt, self._k, self._mse = ctypes, wintypes, k, MEMORYSTATUSEX

    # --- time ---
    def now(self):
        return time.time()

    def sleep(self, seconds):
        time.sleep(max(0.0, seconds))

    # --- memory and disk ---
    def memory(self):
        """(commit limit, commit available) in bytes: ullTotalPageFile and ullAvailPageFile."""
        st = self._mse()
        st.dwLength = self._ct.sizeof(st)
        if not self._k.GlobalMemoryStatusEx(self._ct.byref(st)):
            raise ProbeError("GlobalMemoryStatusEx failed (%d)" % self._ct.get_last_error())
        return int(st.ullTotalPageFile), int(st.ullAvailPageFile)

    def disk_free(self, path):
        return shutil.disk_usage(path).free

    # --- processes ---
    def processes(self):
        try:
            r = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", PS_PROCESSES],
                               capture_output=True, stdin=subprocess.DEVNULL, timeout=120,
                               creationflags=CREATE_NO_WINDOW)
        except (OSError, subprocess.TimeoutExpired) as e:
            raise ProbeError("the process probe failed: %s" % e)
        if r.returncode != 0:
            raise ProbeError("the process probe failed: %s" % r.stderr.decode("utf-8", "replace")[:300])
        text = r.stdout.decode("utf-8-sig", "replace").strip()
        try:
            rows = json.loads(text) if text else []
        except ValueError as e:
            raise ProbeError("the process probe printed no JSON: %s" % e)
        if isinstance(rows, dict):
            rows = [rows]
        return [Proc(x.get("pid"), x.get("ppid"), x.get("name"), x.get("exe"), x.get("cmd"), x.get("created"))
                for x in rows]

    def _open(self, pid, access):
        h = self._k.OpenProcess(access, False, int(pid))
        if not h:
            return None, self._ct.get_last_error()
        return h, 0

    def _created(self, h):
        ft = [self._wt.FILETIME() for _ in range(4)]
        if not self._k.GetProcessTimes(h, *[self._ct.byref(x) for x in ft]):
            return None
        v = (ft[0].dwHighDateTime << 32) | ft[0].dwLowDateTime
        return (v - 116444736000000000) // 10000

    def _exit_code(self, h):
        code = self._wt.DWORD()
        if not self._k.GetExitCodeProcess(h, self._ct.byref(code)):
            return False, None
        return True, code.value

    def created_ms(self, pid):
        h, _ = self._open(pid, PROCESS_QUERY_LIMITED_INFORMATION)
        if h is None:
            return None
        try:
            return self._created(h)
        finally:
            self._k.CloseHandle(h)

    def alive(self, pid, created_ms):
        """True / False, or None when Windows will not say (access denied)."""
        if not pid:
            return False
        h, err = self._open(pid, PROCESS_QUERY_LIMITED_INFORMATION)
        if h is None:
            return False if err == ERROR_INVALID_PARAMETER else None
        try:
            ok, code = self._exit_code(h)
            if not ok:
                return None
            if code != STILL_ACTIVE:
                return False
            if created_ms is not None:
                c = self._created(h)
                if c is None:
                    return None
                if abs(c - int(created_ms)) > 1:
                    return False
            return True
        finally:
            self._k.CloseHandle(h)

    def self_identity(self):
        pid = os.getpid()
        return pid, self.created_ms(pid)

    def kill_tree(self, pid):
        try:
            r = subprocess.run(taskkill_argv(pid), capture_output=True, stdin=subprocess.DEVNULL, timeout=60,
                               creationflags=CREATE_NO_WINDOW)
        except (OSError, subprocess.TimeoutExpired) as e:
            return -1, str(e)
        out = (r.stdout + r.stderr).decode("utf-8", "replace").strip()
        return r.returncode, out[:600]

    def spawn_detached(self, argv, cwd, log_path):
        """Start argv detached through a short-lived spawner, so the started process has a dead parent
        and a tree kill of the caller never reaches it. Returns the started PID."""
        hop = [sys.executable, os.path.abspath(__file__), "_spawn", "--cwd", cwd, "--log", log_path, "--"] + argv
        p = popen_detached(hop, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
        out, _ = p.communicate(timeout=60)
        text = out.decode("ascii", "replace").strip()
        if p.returncode != 0 or not text.isdigit():
            raise SlotError("the detached spawn failed: %r" % text)
        return int(text)

    def start_editor(self, argv, env, out_path, cwd):
        with open(out_path, "ab") as f:
            p = subprocess.Popen(argv, cwd=cwd, env=env, stdin=subprocess.DEVNULL, stdout=f,
                                 stderr=subprocess.STDOUT,
                                 creationflags=CREATE_NO_WINDOW | CREATE_NEW_PROCESS_GROUP)
        return _WinHandle(self, p.pid, self.created_ms(p.pid), popen=p)

    def open_handle(self, pid, created_ms):
        h, _ = self._open(pid, SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION)
        if h is None:
            return None
        ok, code = self._exit_code(h)
        c = self._created(h)
        if not ok or code != STILL_ACTIVE or c is None or abs(c - int(created_ms)) > 1:
            self._k.CloseHandle(h)
            return None
        return _WinHandle(self, pid, c, handle=h)

    def git_porcelain(self, box):
        env = dict(os.environ)
        env["GIT_NO_LAZY_FETCH"] = "1"
        env["GIT_OPTIONAL_LOCKS"] = "0"
        try:
            r = subprocess.run(["git", "-C", box, "status", "--porcelain"], capture_output=True, env=env,
                               stdin=subprocess.DEVNULL, timeout=300, creationflags=CREATE_NO_WINDOW)
        except (OSError, subprocess.TimeoutExpired) as e:
            return -1, str(e)
        text = r.stdout.decode("utf-8", "replace")
        if r.returncode != 0:
            text += r.stderr.decode("utf-8", "replace")
        return r.returncode, text


# --------------------------------------------------------------------------------------------
# The keeper
# --------------------------------------------------------------------------------------------

def stop_how(stop):
    return "stopped: give" if stop and stop.get("by") == "lane" else "stopped: owner"


def launch_view(L):
    """A launch as the return record carries it."""
    ed = L.get("editor") or {}
    minutes = None
    if L.get("start") is not None and L.get("end") is not None:
        minutes = round((L["end"] - L["start"]) / 60.0, 2)
    return {
        "n": L["n"],
        "args": L["args"],
        "editor_pid": ed.get("pid"),
        "child_pids": [c["pid"] for c in L.get("children", [])],
        "children": L.get("children", []),
        "watchdog_pids": [w["pid"] for w in L.get("watchdogs", [])],
        "start": iso(L.get("start")),
        "end": iso(L.get("end")),
        "minutes": minutes,
        "exit_code": L.get("exit_code"),
        "how": L.get("how"),
        "min_headroom_gib": None if L.get("min_headroom_gib") is None else round(L["min_headroom_gib"], 3),
        "min_headroom_at": iso(L.get("min_headroom_at")),
        "save_before": L.get("save_before"),
        "save_after": L.get("save_after"),
        "save_restored": bool(L.get("save_restored")),
        "save_note": L.get("save_note"),
        "foreign_editor_seen": bool(L.get("foreign_seen")),
        "foreign_editor_pids": L.get("foreign_seen", []),
        "unwatched_gap": bool(L.get("unobserved")),
        "porcelain": L.get("porcelain"),
        "leftovers_closed": L.get("leftovers_closed", []),
        "foreign_naming_box": L.get("foreign_naming_box", []),
        "kills": L.get("kills", []),
        "state": L.get("state"),
    }


def save_phrase(v):
    if v.get("save_restored"):
        return "save restored (%s)" % short_save(v.get("save_before"))
    return v.get("save_note") or "save not checked"


def outcome_line(v, lease_id):
    how = v.get("how") or "?"
    head = "EXITED %s" % v.get("exit_code") if how == "exited" else "%s (exit %s)" % (how.upper(), v.get("exit_code"))
    parts = [head, "%s min" % ("?" if v.get("minutes") is None else "%.1f" % v["minutes"])]
    if v.get("min_headroom_gib") is not None:
        parts.append("lowest headroom %.1f GiB at %s" % (v["min_headroom_gib"], (v.get("min_headroom_at") or "?")[11:]))
    parts.append(save_phrase(v))
    parts.append("launch %s #%d" % (lease_id, v["n"]))
    return " | ".join(parts)


def return_summary(ret):
    bits = []
    for v in ret["launches"]:
        head = "exited %s" % v["exit_code"] if v["how"] == "exited" else "%s (exit %s)" % (v["how"], v["exit_code"])
        low = "" if v["min_headroom_gib"] is None else ", low %.1f GiB" % v["min_headroom_gib"]
        mins = "?" if v["minutes"] is None else "%.1f" % v["minutes"]
        bits.append("#%d %s, %s min%s, %s" % (v["n"], head, mins, low, save_phrase(v)))
    launches = "; ".join(bits) if bits else "no launches"
    return "RETURN %s | %s on %s, added by %s | %s to %s, %s | %s | launched PIDs gone: %s | %.1f GB free" % (
        ret["lease"], ret["label"], ret["box"], ret["added_by"], (ret["granted"] or "?")[11:],
        (ret["end"] or "?")[11:], ret["end_reason"], launches,
        "yes" if ret["all_launched_pids_gone"] else "NO %s" % ret["launched_pids_alive"], ret["disk_free_gb"])


class Keeper:
    def __init__(self, cfg, machine, config_path=None):
        self.cfg = cfg
        self.m = machine
        self.rule = cfg["editor_rule"]
        self.store = Store(cfg["state_dir"], machine)
        self.config_path = config_path

    # ---------------- state access (callers hold the lock) ----------------
    def _lease(self):
        return self.store.read("lease.json", {"lease": None}).get("lease")

    def _write_lease(self, lease):
        self.store.write("lease.json", {"lease": lease})

    def _queue(self):
        q = self.store.read("queue.json", {"entries": []})
        q.setdefault("entries", [])
        return q

    def _paused(self):
        path = self.store.p("paused")
        if not os.path.exists(path):
            return None
        try:
            with open(path, encoding="utf-8") as f:
                v = json.load(f)
            return v if isinstance(v, dict) else {"reason": str(v)}
        except (ValueError, OSError):
            return {"reason": "the paused flag file is present"}

    def _set_paused(self, reason, by):
        self.store.write("paused", {"reason": reason, "by": by, "at": iso(self.m.now())})
        self.store.log("pause", reason=reason, by=by)

    # ---------------- readings ----------------
    def readings(self):
        limit, avail = self.m.memory()
        return {"commit_limit_gib": limit / GIB, "headroom_gib": avail / GIB,
                "disk_free_gb": self.m.disk_free(self.cfg["disk_path"]) / GB}

    def snapshot(self):
        procs = self.m.processes()
        return procs, classify_all(procs, self.rule)

    def box_problems(self, box):
        if not box or not os.path.isdir(box):
            return "box %s is not an existing folder (R2)" % box
        if owner_conflict(box, self.cfg["owner_project"]):
            return "box %s is the owner's project or under it (R2)" % box
        return None

    # ---------------- whose processes ----------------
    def _our_tree(self, L, procs):
        """Our editor and its descendants alive in this snapshot. R7: only these are ever ours."""
        by_pid = {p.pid: p for p in procs}
        anchors = {}
        ed = L.get("editor")
        if ed:
            anchors[ed["pid"]] = ed.get("cim_created") or ed["created_ms"] or 0
        for c in L.get("children", []):
            anchors[c["pid"]] = c["created"]
        ours = {}
        for pid, created in anchors.items():
            p = by_pid.get(pid)
            if p and abs(p.created - created) <= CREATED_TOLERANCE_MS:
                ours[pid] = p
        reused = {pid: by_pid[pid].created for pid in anchors if pid in by_pid and pid not in ours}
        changed = True
        while changed:
            changed = False
            for p in procs:
                if p.pid in ours or p.ppid not in anchors:
                    continue
                if p.created + CREATED_TOLERANCE_MS < anchors[p.ppid]:
                    continue  # older than its "parent": that parent PID belonged to someone else then
                if p.ppid in reused and p.created >= reused[p.ppid]:
                    continue  # the parent PID was reused; this is the new owner's child
                if p.pid in anchors and abs(p.created - anchors[p.pid]) > CREATED_TOLERANCE_MS:
                    continue
                anchors[p.pid] = p.created
                ours[p.pid] = p
                changed = True
        return ours

    def _sweep(self, box, launches, procs=None):
        """R6: close every editor or import worker naming the box that is ours; record any that is not."""
        if procs is None:
            procs = self.m.processes()
        cls = classify_all(procs, self.rule)
        ours = {}
        for L in launches:
            ours.update(self._our_tree(L, procs))
        closed, foreign = [], []
        for p in cls["editor"] + cls["worker"]:
            if not names_path(p.cmd, box):
                continue
            if p.pid in ours:
                rc, out = self.m.kill_tree(p.pid)
                closed.append({"pid": p.pid, "name": p.name, "cmd": p.cmd[:300], "taskkill_rc": rc,
                               "taskkill_out": out, "at": iso(self.m.now())})
                self.store.log("leftover closed", pid=p.pid, cmd=p.cmd[:300], rc=rc)
            else:
                foreign.append(p.brief())
        if closed:
            deadline = self.m.now() + KILL_SETTLE_SECONDS
            gone = {c["pid"] for c in closed}
            while self.m.now() < deadline:
                left = [p.pid for p in self.m.processes() if p.pid in gone]
                if not left:
                    break
                self.m.sleep(1.0)
        return closed, foreign

    # ---------------- reconcile: recovery and lease ends ----------------
    def _lease_due(self, lease, now):
        if lease.get("stop"):
            return lease["stop"]["reason"]
        if now >= lease["grace_end"]:
            return "budget"
        if now - lease["last_activity"] >= self.cfg["idle_minutes"] * 60:
            return "idle"
        return None

    def reconcile(self):
        """Every call first squares the state with the live process list."""
        notes = []
        for _ in range(4):
            with self.store.lock("reconcile"):
                lease = self._lease()
                if not lease:
                    return notes
                now = self.m.now()
                L = lease.get("launch")
                if not L:
                    due = self._lease_due(lease, now)
                    if due:
                        ret = self._end_lease(lease, due)
                        notes.append(ret["summary"])
                    return notes
                action = self._launch_health(lease, L, now)
                if action is None:
                    return notes
                lease_id, n = lease["id"], L["n"]
            kind = action[0]
            if kind == "adopt":
                pid = self._spawn_watchdog(lease_id, n, adopt=True)
                notes.append("launch %s #%d: its watchdog was dead and its editor alive; adopted by PID %s"
                             % (lease_id, n, pid))
                return notes
            if kind == "close":
                ed, how = action[1], action[2]
                if self.m.alive(ed["pid"], ed["created_ms"]) is True:
                    rc, out = self.m.kill_tree(ed["pid"])
                    self._record_kill(lease_id, n, ed["pid"], how, rc, out)
                notes.append("launch %s #%d: watchdog dead past its end; editor closed" % (lease_id, n))
                self.finish_launch(lease_id, n, how, None, unobserved=True)
                continue
            if kind == "finish":
                notes.append("launch %s #%d closed: %s" % (lease_id, n, action[1]))
                self.finish_launch(lease_id, n, action[1], action[2], unobserved=True)
                continue
        return notes

    def _launch_health(self, lease, L, now):
        """Under the lock: None when a live watchdog (or finisher) has the launch, else what to do."""
        me = ident(self.m.self_identity())
        if L["state"] == "finishing":
            fin = L.get("finisher") or {}
            if self.m.alive(fin.get("pid"), fin.get("created_ms")) is not False:
                return None
            L["finisher"] = me
            self._write_lease(lease)
            self.store.log("finisher lost", lease=lease["id"], n=L["n"], finisher=fin)
            return ("finish", L.get("how") or "watchdog lost", L.get("exit_code"))
        wd = L.get("watchdog")
        if wd is None:
            if now - L["spawned_at"] < SPAWN_GRACE_SECONDS:
                return None
        elif self.m.alive(wd["pid"], wd["created_ms"]) is not False:
            return None
        ed = L.get("editor")
        if ed and self.m.alive(ed["pid"], ed["created_ms"]) is True:
            L["unobserved"] = True
            if now >= lease["grace_end"] or lease.get("stop"):
                how = stop_how(lease["stop"]) if lease.get("stop") else "stopped: budget"
                L["state"], L["finisher"], L["how"] = "finishing", me, how
                self._write_lease(lease)
                self.store.log("watchdog lost", lease=lease["id"], n=L["n"], action="close", editor=ed["pid"])
                return ("close", ed, how)
            L["state"], L["watchdog"], L["spawned_at"], L["cli"] = "adopting", None, now, me
            self._write_lease(lease)
            self.store.log("watchdog lost", lease=lease["id"], n=L["n"], action="adopt", editor=ed["pid"])
            return ("adopt",)
        how = L.get("how") or "watchdog lost"
        L["state"], L["finisher"], L["how"], L["unobserved"] = "finishing", me, how, True
        if L.get("end") is None:
            L["end"] = now
        self._write_lease(lease)
        self.store.log("watchdog lost", lease=lease["id"], n=L["n"], action="close record")
        return ("finish", how, L.get("exit_code"))

    def _record_kill(self, lease_id, n, pid, why, rc, out):
        with self.store.lock("record kill"):
            lease = self._lease()
            L = self._running(lease, lease_id, n)
            if L is not None:
                L.setdefault("kills", []).append({"pid": pid, "why": why, "taskkill_rc": rc, "taskkill_out": out,
                                                  "at": iso(self.m.now())})
                self._write_lease(lease)
            self.store.log("editor closed", lease=lease_id, n=n, pid=pid, why=why, rc=rc)

    @staticmethod
    def _running(lease, lease_id, n):
        if not lease or lease["id"] != lease_id:
            return None
        L = lease.get("launch")
        if not L or L["n"] != n:
            return None
        return L

    # ---------------- ending a lease ----------------
    def _end_lease(self, lease, reason):
        """Under the lock: R6 sweep, confirm every launched PID gone, write the return, free the slot."""
        now = self.m.now()
        closed, foreign, alive, note = [], [], [], None
        try:
            closed, foreign = self._sweep(lease["box"], lease["launches"])
            alive = self._launched_alive(lease, self.m.processes())
        except ProbeError as e:
            note = str(e)
            alive = ["unknown: %s" % e]
        disk = self.m.disk_free(self.cfg["disk_path"]) / GB
        ret = {
            "lease": lease["id"], "label": lease["label"], "box": lease["box"], "added_by": lease["by"],
            "minutes": lease["minutes"], "granted": iso(lease["granted"]), "end": iso(now),
            "end_reason": reason, "launches": [launch_view(L) for L in lease["launches"]],
            "launched_pids": sorted({p for L in lease["launches"] for p in self._launched_pids(L)}),
            "launched_pids_alive": alive, "all_launched_pids_gone": not alive,
            "end_sweep_closed": closed, "end_sweep_foreign": foreign, "probe_note": note,
            "disk_free_gb": round(disk, 1),
        }
        ret["summary"] = return_summary(ret)
        self.store.write(os.path.join("returns", "%s.json" % lease["id"]), ret)
        self._write_lease(None)
        self.store.log("lease end", lease=lease["id"], label=lease["label"], reason=reason, summary=ret["summary"])
        return ret

    @staticmethod
    def _launched_pids(L):
        pids = [c["pid"] for c in L.get("children", [])]
        if L.get("editor"):
            pids.insert(0, L["editor"]["pid"])
        return pids

    def _launched_alive(self, lease, procs):
        alive = []
        for L in lease["launches"]:
            ed = L.get("editor")
            if ed and self.m.alive(ed["pid"], ed["created_ms"]) is not False:
                alive.append(ed["pid"])
            for pid in self._our_tree(L, procs):
                if pid not in alive:
                    alive.append(pid)
        return alive

    # ---------------- finishing a launch (watchdog, or recovery) ----------------
    def finish_launch(self, lease_id, n, how, exit_code, end=None, unobserved=False):
        with self.store.lock("finish"):
            lease = self._lease()
            L = self._running(lease, lease_id, n)
            if L is None:
                return None
            L["state"], L["finisher"], L["how"] = "finishing", ident(self.m.self_identity()), how
            if end is not None:
                L["end"] = end
            elif L.get("end") is None:
                L["end"] = self.m.now()
            if exit_code is not None:
                L["exit_code"] = exit_code
            L["unobserved"] = bool(L.get("unobserved") or unobserved)
            self._write_lease(lease)
            snap = json.loads(json.dumps(L))
            box, launches = lease["box"], lease["launches"] + [snap]
        tree_left = self._wait_tree_gone(snap)
        try:
            closed, foreign = self._sweep(box, launches)
        except ProbeError as e:
            closed, foreign = [], [{"probe_error": str(e)}]
        save = self._check_save(snap)
        rc, text = self.m.git_porcelain(box)
        lines = text.splitlines()
        porcelain = {"rc": rc, "count": len(lines), "lines": lines[:PORCELAIN_MAX_LINES]}
        with self.store.lock("finish commit"):
            lease = self._lease()
            L = self._running(lease, lease_id, n)
            if L is None:
                return None
            pause = save.pop("pause", None)
            L.update(save)
            L["tree_left_after_wait"] = tree_left
            L["leftovers_closed"] = L.get("leftovers_closed", []) + closed
            L["foreign_naming_box"] = foreign
            L["porcelain"] = porcelain
            L["state"] = "done"
            L.pop("finisher", None)
            lease["launches"].append(L)
            lease["launch"] = None
            lease["last_activity"] = self.m.now()
            self._write_lease(lease)
            self.store.log("launch end", lease=lease_id, n=n, how=L["how"], exit_code=L.get("exit_code"),
                           save=save_phrase(L), leftovers=[c["pid"] for c in closed])
            if pause:
                self._set_paused("%s (lease %s launch #%d)" % (pause, lease_id, n), "keeper")
            due = self._lease_due(lease, self.m.now())
            if due and due != "idle":
                self._end_lease(lease, due)
            return L

    def _wait_tree_gone(self, L):
        """Wait for the editor's whole tree to exit. Returns the PIDs still alive at the end of the wait."""
        deadline = self.m.now() + TREE_EXIT_WAIT_SECONDS
        while True:
            try:
                tree = self._our_tree(L, self.m.processes())
            except ProbeError:
                return None
            if not tree:
                return []
            if self.m.now() >= deadline:
                return sorted(tree)
            self.m.sleep(TREE_EXIT_POLL_SECONDS)

    def _check_save(self, L):
        """R5, after the tree has exited: unchanged, restored and checked, or left alone and the queue paused."""
        save_path = self.cfg["save_path"]
        before = L.get("save_before") or {"absent": True}
        after, data = read_save(save_path)
        out = {"save_after": after, "save_restored": False}
        if before.get("absent"):
            if after.get("absent"):
                out["save_note"] = "save absent before and after"
            else:
                out["save_note"] = "save appeared during the launch; left in place"
                out["pause"] = "a save appeared during a launch"
            return out
        if not after.get("absent") and after["sha256"] == before["sha256"]:
            out["save_note"] = "save unchanged"
            return out
        if data is not None:
            kept = self.store.p("saves", "%s.after.json" % L["id"])
            atomic_write_bytes(kept, data)
            out["save_changed_copy"] = kept
        if L.get("foreign_seen"):
            out["save_note"] = "save changed with a foreign editor running; NOT restored, queue paused"
            out["pause"] = "save changed with a foreign editor running"
            return out
        if L.get("unobserved"):
            out["save_note"] = "save changed while the launch went unwatched; NOT restored, queue paused"
            out["pause"] = "save changed while a launch went unwatched (watchdog lost)"
            return out
        with open(L["save_copy"], "rb") as f:
            copy = f.read()
        if hashlib.sha256(copy).hexdigest().upper() != before["sha256"]:
            out["save_note"] = "save changed and the copy no longer checks; NOT restored, queue paused"
            out["pause"] = "the save copy failed its sha256"
            return out
        tmp = self.store.p("saves", "%s.restore.tmp" % L["id"])
        with open(tmp, "wb") as f:
            f.write(copy)
            f.flush()
            os.fsync(f.fileno())
        try:
            os.replace(tmp, save_path)
        except OSError:
            with open(save_path, "wb") as f:
                f.write(copy)
        check, _ = read_save(save_path)
        out["save_after_restore"] = check
        if check.get("sha256") == before["sha256"]:
            out["save_restored"] = True
            out["save_note"] = "save restored and checked"
            self.store.log("save restored", launch=L["id"], sha256=check["sha256_prefix"])
        else:
            out["save_note"] = "save restore did NOT check; queue paused"
            out["pause"] = "a save restore failed its sha256"
        return out

    # ---------------- the watchdog (a detached process) ----------------
    def _spawn_watchdog(self, lease_id, n, adopt=False):
        if not self.config_path:
            raise SlotError("no config path to hand the watchdog")
        argv = [sys.executable, os.path.abspath(__file__), "--config", self.config_path, "_watchdog",
                "--lease", lease_id, "--launch", str(n)] + (["--adopt"] if adopt else [])
        log_path = self.store.p("runs", "%s-%d.watchdog.log" % (lease_id, n))
        return self.m.spawn_detached(argv, self.store.dir, log_path)

    def editor_env(self):
        env = dict(os.environ)
        if not env.get("ALLUSERSPROFILE"):
            env["ALLUSERSPROFILE"] = self.cfg["allusersprofile"]
        return env

    def run_watchdog(self, lease_id, n, adopt=False):
        try:
            self._run_watchdog(lease_id, n, adopt)
        except Exception:
            try:
                self.store.log("watchdog error", lease=lease_id, n=n, trace=traceback.format_exc()[-2000:])
            finally:
                raise

    def _run_watchdog(self, lease_id, n, adopt):
        me = ident(self.m.self_identity())
        with self.store.lock("watchdog start"):
            lease = self._lease()
            L = self._running(lease, lease_id, n)
            if L is None:
                self.store.log("watchdog orphaned", lease=lease_id, n=n)
                return
            old = L.get("watchdog")
            if old and old["pid"] != me["pid"] and self.m.alive(old["pid"], old["created_ms"]) is not False:
                self.store.log("watchdog duplicate", lease=lease_id, n=n, running=old["pid"])
                return
            L["watchdog"] = me
            L.setdefault("watchdogs", []).append(me)
            stopped = None if adopt else lease.get("stop")
            if not adopt and not stopped:
                L["state"] = "starting"
            self._write_lease(lease)
            box, args = lease["box"], list(L["args"])
        if stopped:
            self.finish_launch(lease_id, n, stop_how(stopped), None)
            return
        if adopt:
            handle = self.m.open_handle(L["editor"]["pid"], L["editor"]["created_ms"])
            if handle is None:
                self.finish_launch(lease_id, n, L.get("how") or "watchdog lost", None, unobserved=True)
                return
        else:
            argv = [self.cfg["unity_exe"]] + list(self.cfg["unity_exe_args"]) + args
            out_path = self.store.p("runs", "%s-%d.out" % (lease_id, n))
            try:
                handle = self.m.start_editor(argv, self.editor_env(), out_path, box)
            except OSError as e:
                self.finish_launch(lease_id, n, "failed to start: %s" % e, None)
                return
        with self.store.lock("watchdog running"):
            lease = self._lease()
            L = self._running(lease, lease_id, n)
            if L is None:
                return
            if not adopt:
                L["editor"] = {"pid": handle.pid, "created_ms": handle.created_ms, "cim_created": None}
                L["start"] = self.m.now()
                self.store.log("editor started", lease=lease_id, n=n, pid=handle.pid, args=args)
            L["state"] = "running"
            self._write_lease(lease)
        how, code = self._watch(lease_id, n, handle)
        self.finish_launch(lease_id, n, how, code, end=self.m.now())

    def _watch(self, lease_id, n, handle):
        """R4: sample every sample_seconds; close the tree on low headroom, on budget plus grace, or on stop."""
        while True:
            now = self.m.now()
            _, avail = self.m.memory()
            try:
                procs = self.m.processes()
                cls = classify_all(procs, self.rule)
            except ProbeError as e:
                procs = cls = None
                self.store.log("probe error", lease=lease_id, n=n, error=str(e))
            why = self._sample(lease_id, n, procs, cls, avail, now)
            code = handle.poll()
            if code is not None:
                return self._how_ended(lease_id, n) or "exited", code
            if why:
                rc, out = self.m.kill_tree(handle.pid)  # our handle pins the PID: it is still our editor
                self._record_kill(lease_id, n, handle.pid, why, rc, out)
                deadline = self.m.now() + EDITOR_EXIT_WAIT_SECONDS
                code = handle.poll()
                while code is None and self.m.now() < deadline:
                    self.m.sleep(1.0)
                    code = handle.poll()
                return why, code
            self.m.sleep(self.cfg["sample_seconds"])

    def _how_ended(self, lease_id, n):
        with self.store.lock("watchdog exit"):
            L = self._running(self._lease(), lease_id, n)
            return L.get("how") if L else None

    def _sample(self, lease_id, n, procs, cls, avail, now):
        with self.store.lock("watchdog sample"):
            lease = self._lease()
            L = self._running(lease, lease_id, n)
            if L is None:
                return "stopped: lease lost"
            ed = L["editor"]
            if procs is not None:
                tree = self._our_tree(L, procs)
                if ed.get("cim_created") is None and ed["pid"] in tree:
                    ed["cim_created"] = tree[ed["pid"]].created
                known = {c["pid"] for c in L["children"]}
                for pid, p in sorted(tree.items()):
                    if pid != ed["pid"] and pid not in known:
                        L["children"].append({"pid": pid, "name": p.name, "created": p.created,
                                              "first_seen": iso(now)})
                for p in cls["editor"]:
                    if p.pid not in tree and p.pid not in L["foreign_seen"]:
                        L["foreign_seen"].append(p.pid)
                        self.store.log("foreign editor seen", lease=lease_id, n=n, pid=p.pid, cmd=p.cmd[:300])
            h = avail / GIB
            if L.get("min_headroom_gib") is None or h < L["min_headroom_gib"]:
                L["min_headroom_gib"], L["min_headroom_at"] = h, now
            L["samples"] = L.get("samples", 0) + 1
            why = None
            if h < self.cfg["stop_headroom_gib"]:
                why = "stopped: headroom"
            elif now >= lease["grace_end"]:
                why = "stopped: budget"
            elif lease.get("stop"):
                why = L.get("how") or stop_how(lease["stop"])
            if why:
                L["how"] = why
            self._write_lease(lease)
            return why

    # ---------------- commands ----------------
    def cmd_queue_add(self, label, box, minutes, by):
        self.store.ensure()
        if not LABEL_RE.match(label or ""):
            return Result(EXIT_REFUSED, "REFUSED: a label is 1-48 of A-Z a-z 0-9 . _ - (got %r)" % label)
        problem = self.box_problems(box)
        if problem:
            return Result(EXIT_REFUSED, "REFUSED: " + problem)
        if not minutes or minutes <= 0 or minutes > self.cfg["max_lease_minutes"]:
            return Result(EXIT_REFUSED, "REFUSED: minutes must be above 0 and at most %s" % self.cfg["max_lease_minutes"])
        self.reconcile()
        with self.store.lock("queue add"):
            q = self._queue()
            lease = self._lease()
            if any(e["label"] == label for e in q["entries"]):
                return Result(EXIT_REFUSED, "REFUSED: %s is already in the queue" % label)
            if lease and lease["label"] == label:
                return Result(EXIT_REFUSED, "REFUSED: %s holds the slot now (lease %s)" % (label, lease["id"]))
            q["entries"].append({"label": label, "box": os.path.abspath(box), "minutes": minutes, "by": by,
                                 "added": self.m.now(), "seen": None})
            self.store.write("queue.json", q)
            self.store.log("queue add", label=label, box=os.path.abspath(box), minutes=minutes, by=by)
            return Result(EXIT_OK, "QUEUE ADDED %s at position %d of %d (%g min, by %s)" % (
                label, len(q["entries"]), len(q["entries"]), minutes, by))

    def cmd_queue_remove(self, label):
        self.store.ensure()
        with self.store.lock("queue remove"):
            q = self._queue()
            keep = [e for e in q["entries"] if e["label"] != label]
            if len(keep) == len(q["entries"]):
                return Result(EXIT_REFUSED, "REFUSED: %s is not in the queue" % label)
            q["entries"] = keep
            self.store.write("queue.json", q)
            self.store.log("queue remove", label=label)
            return Result(EXIT_OK, "QUEUE REMOVED %s (%d left)" % (label, len(keep)))

    def cmd_queue_move(self, label, pos):
        self.store.ensure()
        with self.store.lock("queue move"):
            q = self._queue()
            entry = next((e for e in q["entries"] if e["label"] == label), None)
            if entry is None:
                return Result(EXIT_REFUSED, "REFUSED: %s is not in the queue" % label)
            q["entries"].remove(entry)
            pos = max(1, min(int(pos), len(q["entries"]) + 1))
            q["entries"].insert(pos - 1, entry)
            self.store.write("queue.json", q)
            self.store.log("queue move", label=label, position=pos)
            return Result(EXIT_OK, "QUEUE MOVED %s to position %d of %d" % (label, pos, len(q["entries"])))

    def cmd_queue_list(self):
        self.store.ensure()
        with self.store.lock("queue list"):
            q = self._queue()
        now = self.m.now()
        return Result(EXIT_OK, "QUEUE %d entries" % len(q["entries"]), self._queue_lines(q, now))

    def _queue_lines(self, q, now):
        out = []
        for i, e in enumerate(q["entries"], 1):
            waiting = e.get("seen") is not None and now - e["seen"] <= self.cfg["wait_heartbeat_seconds"]
            seen = "waiting (seen %ds ago)" % (now - e["seen"]) if waiting else "not waiting"
            out.append("  %d. %s  %s  %g min  by %s  %s" % (i, e["label"], e["box"], e["minutes"], e["by"], seen))
        return out

    def cmd_pause(self, reason):
        self.store.ensure()
        with self.store.lock("pause"):
            self._set_paused(reason or "paused by the owner", "owner")
        return Result(EXIT_OK, "PAUSED: nothing new is granted; a running lease goes on to its end")

    def cmd_resume(self):
        self.store.ensure()
        with self.store.lock("resume"):
            was = self._paused()
            if was is None:
                return Result(EXIT_OK, "RESUMED: it was not paused")
            os.remove(self.store.p("paused"))  # R9: the keeper's own flag file, inside its state folder
            self.store.log("resume", was=was)
        return Result(EXIT_OK, "RESUMED (was paused: %s)" % was.get("reason"))

    def _try_grant(self, label, cls):
        now = self.m.now()
        q = self._queue()
        lease = self._lease()
        if lease and lease["label"] == label:
            return Result(EXIT_OK, "GRANTED %s (%g min; budget ends %s; already yours)" % (
                lease["id"], lease["minutes"], hm(lease["budget_end"])), data={"lease": lease["id"]})
        pos, entry = next(((i, e) for i, e in enumerate(q["entries"], 1) if e["label"] == label), (None, None))
        if entry is None:
            return Result(EXIT_REFUSED, "REFUSED: %s is not in the queue%s" % (label, self._ended_hint(label)))
        entry["seen"] = now
        self.store.write("queue.json", q)
        paused = self._paused()
        why = None
        if paused:
            why = "paused: %s" % paused.get("reason")
        elif lease:
            why = "held by %s (lease %s), budget ends %s" % (lease["label"], lease["id"], hm(lease["budget_end"]))
        elif cls["editor"]:
            why = "editor PID %s is running (R1)" % ", ".join(str(p.pid) for p in cls["editor"])
        else:
            hb = self.cfg["wait_heartbeat_seconds"]
            first = next(e for e in q["entries"] if e.get("seen") is not None and now - e["seen"] <= hb)
            if first["label"] != label:
                why = "%s is above you and waiting" % first["label"]
            else:
                grace = self.cfg["grace_minutes"]
                lease_id = "%s-%s-%s" % (time.strftime("%Y%m%dT%H%M%SZ", time.gmtime(now)), label,
                                         os.urandom(2).hex())
                minutes = float(entry["minutes"])
                lease = {"id": lease_id, "label": label, "box": entry["box"], "by": entry["by"],
                         "minutes": minutes, "entry_added": entry["added"], "granted": now,
                         "budget_end": now + minutes * 60, "grace_end": now + (minutes + grace) * 60,
                         "last_keep": None, "last_activity": now, "launches": [], "launch": None, "stop": None}
                q["entries"].remove(entry)
                self.store.write("queue.json", q)
                self._write_lease(lease)
                self.store.log("grant", lease=lease_id, label=label, box=entry["box"], minutes=minutes,
                               by=entry["by"])
                return Result(EXIT_OK, "GRANTED %s (%g min; budget ends %s, grace to %s)" % (
                    lease_id, minutes, hm(lease["budget_end"]), hm(lease["grace_end"])), data={"lease": lease_id})
        return Result(EXIT_QUEUED, "QUEUED (position %d of %d): %s" % (pos, len(q["entries"]), why),
                      data={"position": pos, "why": why})

    def _ended_hint(self, label):
        best = None
        for ret in self._recent_returns(50):
            if ret.get("label") == label and (best is None or ret["end"] > best["end"]):
                best = ret
        if best is None:
            return ""
        return " (its lease %s ended: %s at %s)" % (best["lease"], best["end_reason"], best["end"][11:])

    def cmd_take(self, label):
        self.store.ensure()
        self.reconcile()
        _, cls = self.snapshot()
        with self.store.lock("take"):
            return self._try_grant(label, cls)

    def cmd_wait(self, label, max_minutes):
        deadline = self.m.now() + max_minutes * 60
        while True:
            r = self.cmd_take(label)
            if r.code != EXIT_QUEUED:
                return r
            left = deadline - self.m.now()
            if left <= 0:
                with self.store.lock("wait end"):
                    lease = self._lease()
                holder = "holder %s, budget ends %s" % (lease["label"], hm(lease["budget_end"])) if lease else "no holder"
                return Result(EXIT_QUEUED, "STILL QUEUED (position %d): %s; %s" % (
                    r.data["position"], r.data["why"], holder), data=r.data)
            self.m.sleep(min(self.cfg["wait_check_seconds"], left))

    def cmd_keep(self, lease_id):
        self.store.ensure()
        self.reconcile()
        with self.store.lock("keep"):
            lease = self._lease()
            if not lease or lease["id"] != lease_id:
                return Result(EXIT_REFUSED, "REFUSED: no live lease %s%s" % (lease_id, self._lease_hint(lease_id)))
            now = self.m.now()
            lease["last_keep"] = now
            lease["last_activity"] = max(lease["last_activity"], now)
            self._write_lease(lease)
            self.store.log("keep", lease=lease_id)
            return Result(EXIT_OK, "KEPT %s: idle clock reset; budget ends %s (%.0f min left)" % (
                lease_id, hm(lease["budget_end"]), max(0, lease["budget_end"] - now) / 60))

    def _lease_hint(self, lease_id):
        try:
            ret = self.store.read(os.path.join("returns", "%s.json" % lease_id), None)
        except SlotError:
            ret = None
        return " (it ended: %s at %s)" % (ret["end_reason"], ret["end"][11:]) if ret else ""

    def cmd_launch(self, lease_id, max_minutes, args):
        self.store.ensure()
        self.reconcile()
        procs, cls = self.snapshot()
        rd = self.readings()
        with self.store.lock("launch"):
            lease = self._lease()
            now = self.m.now()
            if not lease or lease["id"] != lease_id:
                return Result(EXIT_REFUSED, "REFUSED: no live lease %s%s" % (lease_id, self._lease_hint(lease_id)))
            if lease.get("launch"):
                L = lease["launch"]
                return Result(EXIT_REFUSED, "REFUSED: launch #%d is running (editor PID %s); one at a time (R3)" % (
                    L["n"], (L.get("editor") or {}).get("pid")))
            if lease.get("stop"):
                return Result(EXIT_REFUSED, "REFUSED: the lease is stopping (%s)" % lease["stop"]["reason"])
            if now >= lease["budget_end"]:
                return Result(EXIT_REFUSED, "REFUSED: the lease's budget ended at %s; grace only finishes a run "
                                            "already going" % hm(lease["budget_end"]))
            final, why = prepare_args(args, lease["box"])
            if why:
                return Result(EXIT_REFUSED, "REFUSED: " + why)
            problems = []
            if cls["editor"]:
                problems.append("editor PID %s is running (R1)" % ", ".join(str(p.pid) for p in cls["editor"]))
            if rd["headroom_gib"] < self.cfg["launch_headroom_gib"]:
                problems.append("headroom %.1f GiB is under %s (R2)" % (rd["headroom_gib"],
                                                                        self.cfg["launch_headroom_gib"]))
            if rd["disk_free_gb"] < self.cfg["min_disk_gb"]:
                problems.append("%s has %.1f GB free, under %s (R2)" % (self.cfg["disk_path"], rd["disk_free_gb"],
                                                                         self.cfg["min_disk_gb"]))
            problem = self.box_problems(lease["box"])
            if problem:
                problems.append(problem)
            if problems:
                self.store.log("launch refused", lease=lease_id, why=problems)
                return Result(EXIT_REFUSED, "REFUSED: " + "; ".join(problems))
            n = len(lease["launches"]) + 1
            launch_id = "%s-%d" % (lease_id, n)
            before, data = read_save(self.cfg["save_path"])
            copy_path = None
            if data is not None:
                copy_path = self.store.p("saves", "%s.json" % launch_id)
                atomic_write_bytes(copy_path, data)
                with open(copy_path, "rb") as f:
                    if hashlib.sha256(f.read()).hexdigest().upper() != before["sha256"]:
                        return Result(EXIT_REFUSED, "REFUSED: the save copy did not check by sha256 (R5)")
            lease["launch"] = {
                "n": n, "id": launch_id, "requested_args": list(args), "args": final, "state": "spawning",
                "cli": ident(self.m.self_identity()), "spawned_at": now, "watchdog": None, "watchdogs": [],
                "editor": None, "children": [], "start": None, "end": None, "exit_code": None, "how": None,
                "min_headroom_gib": None, "min_headroom_at": None, "save_before": before, "save_copy": copy_path,
                "foreign_seen": [], "kills": [], "leftovers_closed": [], "unobserved": False,
                "headroom_at_launch_gib": round(rd["headroom_gib"], 3), "disk_at_launch_gb": round(rd["disk_free_gb"], 1),
            }
            lease["last_activity"] = now
            self._write_lease(lease)
            self.store.log("launch", lease=lease_id, n=n, args=final, save=short_save(before))
        try:
            pid = self._spawn_watchdog(lease_id, n)
        except (OSError, SlotError) as e:
            self.store.log("watchdog spawn failed", lease=lease_id, n=n, error=str(e))
            return Result(EXIT_ERROR, "ERROR: the watchdog did not start (%s); `slot status` closes the record" % e)
        self.store.log("watchdog spawned", lease=lease_id, n=n, pid=pid)
        return self._await_launch(lease_id, n, max_minutes)

    def _find_launch(self, lease_id, n):
        with self.store.lock("find launch"):
            lease = self._lease()
            if lease and lease["id"] == lease_id:
                L = lease.get("launch")
                if L and L["n"] == n:
                    return launch_view(L), L
                for L in lease["launches"]:
                    if L["n"] == n:
                        return launch_view(L), L
                return None, None
        ret = self.store.read(os.path.join("returns", "%s.json" % lease_id), None)
        if ret:
            for v in ret["launches"]:
                if v["n"] == n:
                    return v, None
        return None, None

    def _await_launch(self, lease_id, n, max_minutes):
        deadline = self.m.now() + max_minutes * 60
        while True:
            self.reconcile()
            v, raw = self._find_launch(lease_id, n)
            if v is None:
                return Result(EXIT_ERROR, "ERROR: launch %s #%d is not on record" % (lease_id, n))
            if v.get("state") == "done":
                return Result(EXIT_OK, outcome_line(v, lease_id), data=v)
            if self.m.now() >= deadline:
                ed = (raw or {}).get("editor") or {}
                start = (raw or {}).get("start")
                mins = (self.m.now() - start) / 60 if start else 0.0
                low = v.get("min_headroom_gib")
                return Result(EXIT_QUEUED, "RUNNING launch %s #%d (editor PID %s, %.1f min, lowest headroom %s); "
                                           "the watchdog has it: `slot wait-run --lease %s`" % (
                                               lease_id, n, ed.get("pid"), mins,
                                               "?" if low is None else "%.1f GiB" % low, lease_id), data=v)
            self.m.sleep(min(RUN_POLL_SECONDS, max(0.0, deadline - self.m.now())))

    def cmd_wait_run(self, lease_id, max_minutes):
        self.store.ensure()
        self.reconcile()
        n = None
        with self.store.lock("wait-run"):
            lease = self._lease()
            if lease and lease["id"] == lease_id:
                if lease.get("launch"):
                    n = lease["launch"]["n"]
                elif lease["launches"]:
                    n = lease["launches"][-1]["n"]
        if n is None:
            ret = self.store.read(os.path.join("returns", "%s.json" % lease_id), None)
            if ret and ret["launches"]:
                return Result(EXIT_OK, outcome_line(ret["launches"][-1], lease_id), data=ret["launches"][-1])
            return Result(EXIT_REFUSED, "REFUSED: lease %s has no launch on record" % lease_id)
        return self._await_launch(lease_id, n, max_minutes)

    def cmd_give(self, lease_id, stop):
        self.store.ensure()
        self.reconcile()
        with self.store.lock("give"):
            lease = self._lease()
            if not lease or lease["id"] != lease_id:
                return Result(EXIT_REFUSED, "REFUSED: no live lease %s%s" % (lease_id, self._lease_hint(lease_id)))
            L = lease.get("launch")
            if L and not stop:
                return Result(EXIT_REFUSED, "REFUSED: launch #%d is running (editor PID %s); `slot wait-run` first, "
                                            "or `slot give --stop`" % (L["n"], (L.get("editor") or {}).get("pid")))
            if not L:
                ret = self._end_lease(lease, "give")
                return Result(EXIT_OK, ret["summary"], data=ret)
        return self._stop(lease_id, "give --stop", "lane")

    def cmd_stop(self):
        self.store.ensure()
        self.reconcile()
        return self._stop(None, "stopped by owner", "owner")

    def _stop(self, lease_id, reason, by):
        with self.store.lock("stop"):
            lease = self._lease()
            if not lease or (lease_id and lease["id"] != lease_id):
                return Result(EXIT_OK, "NOTHING TO STOP: the slot is free")
            lease_id = lease["id"]
            lease["stop"] = {"reason": reason, "by": by, "at": self.m.now()}
            L = lease.get("launch")
            target = None
            if L:
                L["how"] = stop_how(lease["stop"])
                ed = L.get("editor")
                if ed and self.m.alive(ed["pid"], ed["created_ms"]) is True:
                    target = ed
            self._write_lease(lease)
            self.store.log("stop", lease=lease_id, reason=reason, by=by)
            if target:
                rc, out = self.m.kill_tree(target["pid"])
                L.setdefault("kills", []).append({"pid": target["pid"], "why": reason, "taskkill_rc": rc,
                                                  "taskkill_out": out, "at": iso(self.m.now())})
                self._write_lease(lease)
                self.store.log("editor closed", lease=lease_id, n=L["n"], pid=target["pid"], why=reason, rc=rc)
        deadline = self.m.now() + STOP_WAIT_SECONDS
        while True:
            self.reconcile()
            with self.store.lock("stop wait"):
                cur = self._lease()
            if not cur or cur["id"] != lease_id:
                break
            if self.m.now() >= deadline:
                return Result(EXIT_ERROR, "ERROR: stop is pending on lease %s; its watchdog has not finished "
                                          "after %ds" % (lease_id, STOP_WAIT_SECONDS))
            self.m.sleep(RUN_POLL_SECONDS)
        ret = self.store.read(os.path.join("returns", "%s.json" % lease_id), None) or {}
        return Result(EXIT_OK, "STOPPED %s. %s" % (lease_id, ret.get("summary", "")), data=ret)

    # ---------------- status ----------------
    def _recent_returns(self, count):
        folder = self.store.p("returns")
        if not os.path.isdir(folder):
            return []
        rets = []
        for name in os.listdir(folder):
            if name.endswith(".json"):
                try:
                    with open(os.path.join(folder, name), encoding="utf-8") as f:
                        rets.append(json.load(f))
                except (OSError, ValueError):
                    continue
        rets.sort(key=lambda r: r.get("end") or "")
        return rets[-count:]

    def full_state(self, procs=None, cls=None, rd=None):
        live = self.store.exists()
        lease = queue = paused = None
        if live:
            with self.store.lock("state"):
                lease, queue, paused = self._lease(), self._queue(), self._paused()
        if procs is None:
            procs, cls = self.snapshot()
        if rd is None:
            rd = self.readings()
        return {
            "state_dir": self.store.dir, "live": live, "lease": lease, "queue": queue, "paused": paused,
            "readings": rd,
            "processes": {k: [p.brief() for p in v] for k, v in cls.items()},
            "returns": [r.get("summary") for r in self._recent_returns(RETURNS_SHOWN)] if live else [],
            "numbers": {k: self.cfg[k] for k in DEFAULTS if k not in ("editor_rule", "unity_exe_args")},
        }

    def cmd_status(self):
        notes = []
        if self.store.exists():
            notes = self.reconcile()
        procs, cls = self.snapshot()
        rd = self.readings()
        st = self.full_state(procs, cls, rd)
        now = self.m.now()
        parts = []
        lease = st["lease"]
        if lease:
            used = (now - lease["granted"]) / 60
            parts.append("SLOT HELD by %s (lease %s), %.0f of %g min used, %.0f left" % (
                lease["label"], lease["id"], used, lease["minutes"], max(0, lease["budget_end"] - now) / 60))
            L = lease.get("launch")
            if L:
                ed = L.get("editor") or {}
                run = (now - L["start"]) / 60 if L.get("start") else 0.0
                parts.append("launch #%d %s, editor PID %s, %s, %.0f min" % (L["n"], L["state"], ed.get("pid"),
                                                                           lease["box"], run))
            else:
                parts.append("no launch running")
        elif st["live"]:
            parts.append("SLOT FREE")
        else:
            parts.append("NO LIVE STATE (%s does not exist)" % self.store.dir)
        if st["paused"]:
            parts.append("PAUSED (%s)" % st["paused"].get("reason"))
        else:
            parts.append("not paused")
        entries = (st["queue"] or {}).get("entries", [])
        hb = self.cfg["wait_heartbeat_seconds"]
        waiting = sum(1 for e in entries if e.get("seen") is not None and now - e["seen"] <= hb)
        parts.append("queue %d (%d waiting)" % (len(entries), waiting))
        parts.append("headroom %.1f of %.1f GiB" % (rd["headroom_gib"], rd["commit_limit_gib"]))
        parts.append("%s %.1f GB free" % (self.cfg["disk_path"], rd["disk_free_gb"]))
        parts.append("%s: editors %d, workers %d, bridges %d, hub %d, unknown %d" % (
            self.rule["image"], len(cls["editor"]), len(cls["worker"]), len(cls["bridge"]), len(cls["hub"]),
            len(cls["unknown"])))
        details = self._queue_lines({"entries": entries}, now)
        for k in ("editor", "worker", "unknown"):
            for p in cls[k]:
                details.append("  %s PID %d: %s" % (k, p.pid, p.cmd[:200]))
        details += ["  " + s for s in st["returns"] if s]
        details += ["  note: " + s for s in notes]
        st["notes"] = notes
        return Result(EXIT_OK, " | ".join(parts), details, st)


# --------------------------------------------------------------------------------------------
# CLI
# --------------------------------------------------------------------------------------------

def spawn_main(argv):
    """The `_spawn` hop: start argv detached, print its PID, exit (so its parent is dead)."""
    p = argparse.ArgumentParser(prog="slot.py _spawn")
    p.add_argument("--cwd", required=True)
    p.add_argument("--log", required=True)
    if "--" not in argv:
        return EXIT_ERROR
    i = argv.index("--")
    a = p.parse_args(argv[:i])
    with open(a.log, "ab") as f:
        child = popen_detached(argv[i + 1:], cwd=a.cwd, stdout=f, stderr=f)
    print(child.pid)
    return EXIT_OK


def build_parser():
    common = argparse.ArgumentParser(add_help=False)
    common.add_argument("--config", default=argparse.SUPPRESS, help="the live slot.json (default: beside slot.py)")
    common.add_argument("--json", action="store_true", default=argparse.SUPPRESS, help="print the full state as JSON")
    p = argparse.ArgumentParser(prog="slot.py", description="The editor slot keeper.", parents=[common])
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("status", parents=[common], help="the holder, the run, the queue, headroom, disk, returns")
    q = sub.add_parser("queue", parents=[common], help="add / remove / move / list queue entries")
    qs = q.add_subparsers(dest="qcmd", required=True)
    qa = qs.add_parser("add", parents=[common])
    qa.add_argument("--label", required=True)
    qa.add_argument("--box", required=True)
    qa.add_argument("--minutes", required=True, type=float)
    qa.add_argument("--by", required=True, choices=("seat", "owner"))
    qr = qs.add_parser("remove", parents=[common])
    qr.add_argument("label")
    qm = qs.add_parser("move", parents=[common])
    qm.add_argument("label")
    qm.add_argument("pos", type=int)
    qs.add_parser("list", parents=[common])
    pa = sub.add_parser("pause", parents=[common], help="the owner's switch: grant nothing new")
    pa.add_argument("--reason", default=None)
    sub.add_parser("resume", parents=[common])
    sub.add_parser("stop", parents=[common], help="the owner's emergency stop")
    t = sub.add_parser("take", parents=[common], help="GRANTED, QUEUED or REFUSED, at once")
    t.add_argument("--label", required=True)
    w = sub.add_parser("wait", parents=[common], help="block up to M minutes for the grant")
    w.add_argument("--label", required=True)
    w.add_argument("--max-minutes", type=float, required=True)
    la = sub.add_parser("launch", parents=[common], help="start Unity under the watchdog: launch --lease ID -- args")
    la.add_argument("--lease", required=True)
    la.add_argument("--max-minutes", type=float, default=CALL_WAIT_MINUTES)
    wr = sub.add_parser("wait-run", parents=[common], help="wait again for a launch the watchdog still has")
    wr.add_argument("--lease", required=True)
    wr.add_argument("--max-minutes", type=float, default=CALL_WAIT_MINUTES)
    k = sub.add_parser("keep", parents=[common], help="heartbeat between launches")
    k.add_argument("--lease", required=True)
    g = sub.add_parser("give", parents=[common], help="end the lease")
    g.add_argument("--lease", required=True)
    g.add_argument("--stop", action="store_true")
    wd = sub.add_parser("_watchdog", parents=[common])
    wd.add_argument("--lease", required=True)
    wd.add_argument("--launch", type=int, required=True)
    wd.add_argument("--adopt", action="store_true")
    return p


def dispatch(keeper, a, unity_args):
    c = a.cmd
    if c == "status":
        return keeper.cmd_status()
    if c == "queue":
        if a.qcmd == "add":
            return keeper.cmd_queue_add(a.label, a.box, a.minutes, a.by)
        if a.qcmd == "remove":
            return keeper.cmd_queue_remove(a.label)
        if a.qcmd == "move":
            return keeper.cmd_queue_move(a.label, a.pos)
        return keeper.cmd_queue_list()
    if c == "pause":
        return keeper.cmd_pause(a.reason)
    if c == "resume":
        return keeper.cmd_resume()
    if c == "stop":
        return keeper.cmd_stop()
    if c == "take":
        return keeper.cmd_take(a.label)
    if c == "wait":
        return keeper.cmd_wait(a.label, a.max_minutes)
    if c == "launch":
        return keeper.cmd_launch(a.lease, a.max_minutes, unity_args)
    if c == "wait-run":
        return keeper.cmd_wait_run(a.lease, a.max_minutes)
    if c == "keep":
        return keeper.cmd_keep(a.lease)
    if c == "give":
        return keeper.cmd_give(a.lease, a.stop)
    raise SlotError("unknown command %s" % c)


def main(argv=None, machine=None, out=None):
    argv = list(sys.argv[1:] if argv is None else argv)
    out = out or sys.stdout
    if argv[:1] == ["_spawn"]:
        return spawn_main(argv[1:])
    unity_args = []
    if "--" in argv:
        i = argv.index("--")
        argv, unity_args = argv[:i], argv[i + 1:]
    a = build_parser().parse_args(argv)
    config_path = getattr(a, "config", None) or os.environ.get("HH_SLOT_CONFIG") or os.path.join(HERE, "slot.json")
    config_path = os.path.abspath(config_path)
    want_json = getattr(a, "json", False)
    try:
        cfg = load_config(config_path)
        keeper = Keeper(cfg, machine or RealMachine(), config_path)
        if a.cmd == "_watchdog":
            keeper.run_watchdog(a.lease, a.launch, a.adopt)
            return EXIT_OK
        result = dispatch(keeper, a, unity_args)
        print(result.line, file=out)
        if want_json:
            data = {"result": result.data}
            if keeper.store.exists() or a.cmd == "status":
                data["state"] = result.data if a.cmd == "status" else keeper.full_state()
            print(json.dumps(data, indent=1, sort_keys=True, default=str), file=out)
        else:
            for line in result.details:
                print(line, file=out)
        return result.code
    except SlotError as e:
        print("ERROR: %s" % e, file=out)
        return EXIT_ERROR


if __name__ == "__main__":
    try:
        sys.stdout.reconfigure(errors="replace")
    except AttributeError:
        pass
    sys.exit(main())
