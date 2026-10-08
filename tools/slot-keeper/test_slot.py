#!/usr/bin/env python3
"""Unit tests for the editor slot keeper. Stdlib unittest, a fake Machine, no Unity, no live state.

    python -m unittest discover -s tools/slot-keeper -p "test_slot.py" -v

Each test gets its own state folder under the repo's Temp/slot-tests/ (git ignores /Temp/). The tests
never delete it (R9 holds for the tests too); clear Temp/ by hand when you like.
"""

import json
import os
import subprocess
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import slot  # noqa: E402

REPO = os.path.dirname(os.path.dirname(HERE))
TEST_ROOT = os.path.join(REPO, "Temp", "slot-tests")

GIB, GB = slot.GIB, slot.GB
UNITY = "C:\\Program Files\\Unity\\Hub\\Editor\\6000.5.0f1\\Editor\\Unity.exe"
BRIDGE_EXE = "C:\\Tools\\Unity\\bin\\unity.exe"
HUB_EXE = "C:\\Program Files\\Unity Hub\\resources\\app.asar.unpacked\\lib\\win32\\unity.exe"
SAVE_BYTES = b'\xef\xbb\xbf{"SchemaVersion": 3, "WorldSeed": 12345, "Money": 20}'
CHANGED_BYTES = b'{"SchemaVersion": 3, "WorldSeed": 4242, "Money": 0}'
FAKE_DAY = 24 * 3600  # a keeper loop that outlives this on the fake clock has hung: fail, never hang


class Crash(Exception):
    """Stands in for a watchdog process dying mid-run."""


class FakeHandle:
    def __init__(self, fake, pid, created):
        self.fake, self.pid, self.created_ms = fake, pid, created

    def poll(self):
        return self.fake.exit_codes.get(self.pid)


class FakeMachine:
    """Every probe and action of slot.RealMachine, scripted. Time moves only through sleep()."""

    def __init__(self):
        self.t = self.t0 = 1791500000.0
        self.limit = int(63.79 * GIB)
        self.headroom = int(40 * GIB)
        self.disk = int(185.8 * GB)
        self.procs = {}
        self.exit_codes = {}
        self.kills = []
        self.spawned = []
        self.started = []
        self.hooks = []
        self.on_spawn = None
        self.on_editor_start = None
        self.editor_child = True
        self.orphan_child = False
        self.editor_runs = None
        self.editor_exit_code = 0
        self.porcelain = " M Assets/_Project/Code/Thing.cs\n?? artifacts/\n"
        self.next_pid = 4000
        self.me = [self.add("python.exe", "C:\\Py\\python.exe", "python slot.py status").pid]

    def add(self, name, exe, cmd, ppid=1):
        pid = self.next_pid
        self.next_pid += 4
        p = slot.Proc(pid, ppid, name, exe, cmd, int(self.t * 1000))
        self.procs[pid] = p
        return p

    def end(self, pid, code=0):
        self.procs.pop(pid, None)
        self.exit_codes[pid] = code

    def descendants(self, pid):
        out, changed = [pid], True
        while changed:
            changed = False
            for p in list(self.procs.values()):
                if p.ppid in out and p.pid not in out:
                    out.append(p.pid)
                    changed = True
        return out

    # --- the Machine interface ---
    def now(self):
        return self.t

    def sleep(self, seconds):
        self.t += seconds
        if self.t > self.t0 + FAKE_DAY:
            raise AssertionError("the fake clock ran a whole day: a keeper loop never ended")
        for hook in list(self.hooks):
            hook(self)

    def memory(self):
        return self.limit, self.headroom

    def disk_free(self, path):
        return self.disk

    def processes(self):
        return list(self.procs.values())

    def self_identity(self):
        pid = self.me[-1]
        p = self.procs.get(pid)
        return pid, (p.created if p else 0)

    def alive(self, pid, created_ms):
        p = self.procs.get(pid)
        if p is None:
            return False
        return created_ms is None or p.created == created_ms

    def kill_tree(self, pid):
        self.kills.append(" ".join(slot.taskkill_argv(pid)))
        if pid not in self.procs:
            return 128, "ERROR: The process %d not found." % pid
        for d in self.descendants(pid):
            self.end(d, 1)
        return 0, "SUCCESS"

    def spawn_detached(self, argv, cwd, log_path):
        p = self.add("python.exe", "C:\\Py\\python.exe", subprocess.list2cmdline(argv))
        self.spawned.append(argv)
        if self.on_spawn:
            self.on_spawn(argv, p.pid)
        return p.pid

    def start_editor(self, argv, env, out_path, cwd):
        self.started.append((argv, env, cwd))
        ed = self.add(os.path.basename(argv[0]), argv[0], subprocess.list2cmdline(argv), ppid=self.me[-1])
        child = None
        if self.editor_child:
            child = self.add("Unity.exe", argv[0], '"%s" -name AssetImportWorker0 -parentPid %d -projectPath "%s"'
                             % (argv[0], ed.pid, cwd), ppid=ed.pid)
        if self.editor_runs is not None:
            end_at = self.t + self.editor_runs

            def exits(f, pid=ed.pid, child=child):
                if f.t >= end_at and pid in f.procs:
                    f.end(pid, f.editor_exit_code)
                    if child is not None and not f.orphan_child:
                        f.end(child.pid, 0)

            self.hooks.append(exits)
        if self.on_editor_start:
            self.on_editor_start(self, ed)
        return FakeHandle(self, ed.pid, ed.created)

    def open_handle(self, pid, created_ms):
        return FakeHandle(self, pid, created_ms) if self.alive(pid, created_ms) else None

    def git_porcelain(self, box):
        return 0, self.porcelain


class Base(unittest.TestCase):
    def setUp(self):
        os.makedirs(TEST_ROOT, exist_ok=True)
        self.root = tempfile.mkdtemp(prefix=self._testMethodName[5:45] + "-", dir=TEST_ROOT)
        self.state = os.path.join(self.root, "state")
        os.makedirs(self.state)
        self.owner = os.path.join(self.root, "Owner Project")
        os.makedirs(os.path.join(self.owner, "sub"))
        self.boxes = {}
        for name in ("box-a", "box-b", "box-c"):
            self.boxes[name] = os.path.join(self.root, "boxes", name)
            os.makedirs(self.boxes[name])
        self.box_a = self.boxes["box-a"]
        self.save = os.path.join(self.root, "LocalLow", "savegame.json")
        os.makedirs(os.path.dirname(self.save))
        with open(self.save, "wb") as f:
            f.write(SAVE_BYTES)
        self.cfg = slot.make_config({
            "unity_exe": UNITY, "owner_project": self.owner, "save_path": self.save, "state_dir": self.state,
            "grace_minutes": 1, "idle_minutes": 5,
        })
        self.config_path = os.path.join(self.root, "slot.json")
        self.fake = FakeMachine()
        self.fake.on_spawn = self.inline_watchdog
        self.k = slot.Keeper(self.cfg, self.fake, self.config_path)

    # The watchdog runs in-process on the same fake, as the detached process would on the real machine.
    def inline_watchdog(self, argv, pid):
        lease = argv[argv.index("--lease") + 1]
        n = int(argv[argv.index("--launch") + 1])
        self.fake.me.append(pid)
        try:
            slot.Keeper(self.cfg, self.fake, self.config_path).run_watchdog(lease, n, "--adopt" in argv)
        finally:
            self.fake.me.pop()
            self.fake.end(pid, 0)

    def crashing_watchdog(self, then=None):
        """The first watchdog dies at its first sleep; later ones (an adopter) run normally."""
        def crash_hook(f):
            f.hooks.remove(crash_hook)
            raise Crash()

        def on_spawn(argv, pid):
            if self.fake.spawned.__len__() == 1:
                self.fake.hooks.insert(0, crash_hook)
                try:
                    self.inline_watchdog(argv, pid)
                except Crash:
                    pass
                if then:
                    then()
            else:
                self.inline_watchdog(argv, pid)
        self.fake.on_spawn = on_spawn

    def add(self, label, box=None, minutes=30, by="seat"):
        r = self.k.cmd_queue_add(label, box or self.box_a, minutes, by)
        self.assertEqual(r.code, slot.EXIT_OK, r.line)

    def grant(self, label="lane-a", box=None, minutes=30):
        self.add(label, box, minutes)
        r = self.k.cmd_take(label)
        self.assertEqual(r.code, slot.EXIT_OK, r.line)
        self.assertTrue(r.line.startswith("GRANTED "), r.line)
        return r.data["lease"]

    def launch(self, lease, args=None, max_minutes=60):
        if args is None:
            args = ["-batchmode", "-projectPath", self.box_a, "-runTests", "-testPlatform", "EditMode"]
        return self.k.cmd_launch(lease, max_minutes, args)

    def lease(self):
        return self.k.store.read("lease.json", {"lease": None})["lease"]

    def ret(self, lease_id):
        return self.k.store.read(os.path.join("returns", lease_id + ".json"), None)

    def events(self):
        path = os.path.join(self.state, "log.jsonl")
        if not os.path.exists(path):
            return []
        with open(path, encoding="utf-8") as f:
            return [json.loads(line) for line in f if line.strip()]

    def save_bytes(self):
        with open(self.save, "rb") as f:
            return f.read()

    def foreign_editor(self, project):
        return self.fake.add("Unity.exe", UNITY, '"%s" -projectPath "%s"' % (UNITY, project))


class GrantingTests(Base):
    def test_the_slot_goes_to_a_waiting_lane_in_queue_order(self):
        holder = self.grant("lane-c", self.boxes["box-c"])
        self.add("lane-a")
        self.add("lane-b", self.boxes["box-b"])
        self.assertEqual(self.k.cmd_take("lane-a").code, slot.EXIT_QUEUED)
        self.assertEqual(self.k.cmd_take("lane-b").code, slot.EXIT_QUEUED)
        self.assertEqual(self.k.cmd_give(holder, False).code, slot.EXIT_OK)
        r = self.k.cmd_take("lane-b")
        self.assertEqual(r.code, slot.EXIT_QUEUED, r.line)
        self.assertIn("QUEUED (position 2 of 2): lane-a is above you and waiting", r.line)
        r = self.k.cmd_take("lane-a")
        self.assertTrue(r.line.startswith("GRANTED "), r.line)
        self.assertEqual(self.lease()["label"], "lane-a")

    def test_an_entry_above_that_is_not_waiting_does_not_block_the_entries_below(self):
        holder = self.grant("lane-c", self.boxes["box-c"])
        self.add("lane-a")
        self.add("lane-b", self.boxes["box-b"])
        self.k.cmd_take("lane-a")          # lane-a waited once ...
        self.fake.sleep(self.cfg["wait_heartbeat_seconds"] + 1)  # ... and then stopped calling
        self.k.cmd_give(holder, False)
        r = self.k.cmd_take("lane-b")
        self.assertTrue(r.line.startswith("GRANTED "), r.line)
        self.assertEqual(self.lease()["label"], "lane-b")
        self.assertEqual([e["label"] for e in self.k._queue()["entries"]], ["lane-a"])

    def test_paused_grants_nothing(self):
        self.add("lane-a")
        self.assertEqual(self.k.cmd_pause("owner is playing").code, slot.EXIT_OK)
        r = self.k.cmd_take("lane-a")
        self.assertEqual(r.code, slot.EXIT_QUEUED)
        self.assertIn("paused: owner is playing", r.line)
        self.assertIsNone(self.lease())
        self.k.cmd_resume()
        self.assertTrue(self.k.cmd_take("lane-a").line.startswith("GRANTED "))

    def test_a_label_not_in_the_queue_is_refused(self):
        r = self.k.cmd_take("ghost")
        self.assertEqual(r.code, slot.EXIT_REFUSED)
        self.assertIn("ghost is not in the queue", r.line)
        self.assertIsNone(self.lease())


class LaunchCheckTests(Base):
    def test_a_foreign_editor_blocks_the_grant(self):
        foreign = self.foreign_editor(self.owner)
        self.add("lane-a")
        r = self.k.cmd_take("lane-a")
        self.assertEqual(r.code, slot.EXIT_QUEUED)
        self.assertIn("editor PID %d is running (R1)" % foreign.pid, r.line)
        self.assertIsNone(self.lease())

    def test_a_foreign_editor_blocks_every_launch(self):
        lease = self.grant()
        foreign = self.foreign_editor(self.boxes["box-b"])
        r = self.launch(lease)
        self.assertEqual(r.code, slot.EXIT_REFUSED)
        self.assertIn("editor PID %d is running (R1)" % foreign.pid, r.line)
        self.assertEqual(self.fake.spawned, [])
        self.assertEqual(self.fake.kills, [])

    def test_the_owners_project_is_refused_exactly_and_as_a_sub_path_in_any_case_or_slash(self):
        variants = [self.owner, self.owner.upper(), self.owner.replace("\\", "/"), self.owner + "\\",
                    os.path.join(self.owner, "sub"), os.path.join(self.owner, "sub").upper().replace("\\", "/")]
        for i, box in enumerate(variants):
            r = self.k.cmd_queue_add("lane-%d" % i, box, 30, "seat")
            self.assertEqual(r.code, slot.EXIT_REFUSED, box)
            self.assertIn("owner's project", r.line)
        # A hand-edited queue entry reaches the grant; the launch still refuses it.
        sub = os.path.join(self.owner, "sub")
        self.k.store.write("queue.json", {"entries": [{"label": "sneak", "box": sub, "minutes": 30, "by": "owner",
                                                        "added": self.fake.t, "seen": None}]})
        lease = self.k.cmd_take("sneak").data["lease"]
        r = self.launch(lease, ["-batchmode", "-projectPath", sub.upper().replace("\\", "/")])
        self.assertEqual(r.code, slot.EXIT_REFUSED, r.line)
        self.assertIn("owner's project", r.line)
        self.assertEqual(self.fake.spawned, [])

    def test_nographics_is_refused(self):
        lease = self.grant()
        r = self.launch(lease, ["-batchmode", "-nographics", "-projectPath", self.box_a])
        self.assertEqual(r.code, slot.EXIT_REFUSED)
        self.assertIn("-nographics is refused", r.line)
        self.assertEqual(self.fake.spawned, [])

    def test_force_d3d11_is_added_when_missing_and_not_doubled(self):
        self.fake.editor_runs = 10
        lease = self.grant()
        self.assertEqual(self.launch(lease).code, slot.EXIT_OK)
        argv = self.fake.started[-1][0]
        self.assertEqual(argv[0], UNITY)
        self.assertEqual(argv.count("-force-d3d11"), 1)
        self.assertEqual(self.fake.started[-1][2], self.box_a)  # the editor starts in the box
        self.assertTrue(self.fake.started[-1][1].get("ALLUSERSPROFILE"))
        self.assertEqual(self.launch(lease, ["-projectPath", self.box_a, "-force-d3d11"]).code, slot.EXIT_OK)
        self.assertEqual(self.fake.started[-1][0].count("-force-d3d11"), 1)
        r = self.launch(lease, ["-projectPath", self.box_a, "-force-d3d12"])
        self.assertEqual(r.code, slot.EXIT_REFUSED)

    def test_headroom_at_15_9_gib_refuses_a_launch(self):
        lease = self.grant()
        self.fake.headroom = int(15.9 * GIB)
        r = self.launch(lease)
        self.assertEqual(r.code, slot.EXIT_REFUSED)
        self.assertIn("headroom 15.9 GiB is under 16", r.line)
        self.assertEqual(self.fake.spawned, [])
        self.fake.headroom = 16 * GIB
        self.fake.editor_runs = 10
        self.assertEqual(self.launch(lease).code, slot.EXIT_OK)

    def test_disk_at_14_gb_refuses_a_launch(self):
        lease = self.grant()
        self.fake.disk = 14 * GB
        r = self.launch(lease)
        self.assertEqual(r.code, slot.EXIT_REFUSED)
        self.assertIn("14.0 GB free, under 15", r.line)
        self.assertEqual(self.fake.spawned, [])

    def test_a_project_path_that_is_not_the_box_is_refused(self):
        lease = self.grant()
        r = self.launch(lease, ["-batchmode", "-projectPath", self.boxes["box-b"]])
        self.assertEqual(r.code, slot.EXIT_REFUSED)
        self.assertIn("is not the lease's box", r.line)

    def test_a_second_launch_while_one_runs_is_refused(self):
        self.fake.on_spawn = None  # the watchdog is slow to come up: the launch is still running
        lease = self.grant()
        self.assertEqual(self.launch(lease, max_minutes=0).code, slot.EXIT_QUEUED)
        r = self.launch(lease)
        self.assertEqual(r.code, slot.EXIT_REFUSED)
        self.assertIn("launch #1 is running", r.line)


class WatchdogTests(Base):
    def test_headroom_at_3_9_gib_closes_the_editor_by_pid_tree(self):
        start = self.fake.t

        def squeeze(f):
            if f.t >= start + 20:
                f.headroom = int(3.9 * GIB)
        self.fake.hooks.append(squeeze)
        lease = self.grant()
        r = self.launch(lease)
        self.assertTrue(r.line.startswith("STOPPED: HEADROOM"), r.line)
        L = self.lease()["launches"][0]
        self.assertEqual(self.fake.kills, ["taskkill /PID %d /T /F" % L["editor"]["pid"]])
        self.assertEqual(L["how"], "stopped: headroom")
        self.assertAlmostEqual(L["min_headroom_gib"], 3.9, places=3)
        self.assertEqual(L["kills"][0]["why"], "stopped: headroom")
        self.assertEqual(L["children"][0]["name"], "Unity.exe")

    def test_budget_plus_grace_closes_the_editor_the_same_way(self):
        lease = self.grant(minutes=1)
        r = self.launch(lease)
        self.assertTrue(r.line.startswith("STOPPED: BUDGET"), r.line)
        ret = self.ret(lease)
        self.assertIsNotNone(ret, "the lease ended with its budget")
        self.assertEqual(ret["end_reason"], "budget")
        v = ret["launches"][0]
        self.assertEqual(self.fake.kills, ["taskkill /PID %d /T /F" % v["editor_pid"]])
        self.assertEqual(v["how"], "stopped: budget")
        self.assertGreaterEqual(v["minutes"], 2.0)  # 1 min budget + 1 min grace
        self.assertTrue(ret["all_launched_pids_gone"])
        self.assertIsNone(self.lease())

    def test_an_idle_lease_expires_on_a_waiting_lanes_wait(self):
        idle = self.grant("lane-a")
        self.add("lane-b", self.boxes["box-b"])
        self.fake.sleep(self.cfg["idle_minutes"] * 60 + 1)
        r = self.k.cmd_wait("lane-b", 1)
        self.assertTrue(r.line.startswith("GRANTED "), r.line)
        self.assertEqual(self.ret(idle)["end_reason"], "idle")
        self.assertEqual(self.lease()["label"], "lane-b")

    def test_keep_resets_the_idle_clock(self):
        lease = self.grant("lane-a")
        self.fake.sleep(self.cfg["idle_minutes"] * 60 - 10)
        self.assertEqual(self.k.cmd_keep(lease).code, slot.EXIT_OK)
        self.fake.sleep(30)
        self.k.reconcile()
        self.assertEqual(self.lease()["id"], lease)


class SaveTests(Base):
    def test_a_changed_save_is_restored_and_checked(self):
        def writes_the_save(f, ed):
            with open(self.save, "wb") as out:
                out.write(CHANGED_BYTES)
        self.fake.on_editor_start = writes_the_save
        self.fake.editor_runs = 10
        lease = self.grant()
        r = self.launch(lease)
        self.assertIn("save restored", r.line)
        self.assertEqual(self.save_bytes(), SAVE_BYTES)
        L = self.lease()["launches"][0]
        self.assertTrue(L["save_restored"])
        self.assertEqual(L["save_before"]["world_seed"], 12345)
        self.assertEqual(L["save_after"]["world_seed"], 4242)
        self.assertEqual(L["save_after_restore"]["sha256"], L["save_before"]["sha256"])
        with open(L["save_changed_copy"], "rb") as f:
            self.assertEqual(f.read(), CHANGED_BYTES)  # the changed save is kept, not lost
        self.assertIsNone(self.k._paused())

    def test_a_changed_save_with_a_foreign_editor_seen_is_not_restored_and_the_queue_pauses(self):
        start = self.fake.t
        seen = {}

        def owner_plays(f):
            if f.t >= start + 5 and "pid" not in seen:
                seen["pid"] = self.foreign_editor(self.owner).pid
                with open(self.save, "wb") as out:
                    out.write(CHANGED_BYTES)
        self.fake.hooks.append(owner_plays)
        self.fake.editor_runs = 15
        lease = self.grant()
        r = self.launch(lease)
        self.assertIn("NOT restored", r.line)
        self.assertEqual(self.save_bytes(), CHANGED_BYTES)
        L = self.lease()["launches"][0]
        self.assertFalse(L["save_restored"])
        self.assertEqual(L["foreign_seen"], [seen["pid"]])
        paused = self.k._paused()
        self.assertIn("save changed with a foreign editor running", paused["reason"])
        self.assertEqual(self.fake.kills, [])  # the foreign editor is never touched
        self.assertIn(seen["pid"], self.fake.procs)


class LockAndRecoveryTests(Base):
    def test_a_stale_lock_is_broken_and_recorded(self):
        with open(os.path.join(self.state, "lock"), "w", encoding="utf-8") as f:
            json.dump({"pid": 99999, "created_ms": 1, "at": "2026-10-08T17:00:00Z", "what": "crashed"}, f)
        self.assertEqual(self.k.cmd_queue_list().code, slot.EXIT_OK)
        broken = [e for e in self.events() if e["event"] == "lock broken"]
        self.assertEqual(len(broken), 1)
        self.assertEqual(broken[0]["holder"]["pid"], 99999)
        self.assertFalse(os.path.exists(os.path.join(self.state, "lock")))

    def test_a_live_lock_is_not_broken(self):
        holder = self.fake.add("python.exe", "C:\\Py\\python.exe", "python slot.py wait")
        text = json.dumps({"pid": holder.pid, "created_ms": holder.created, "at": "x", "what": "wait"})
        with open(os.path.join(self.state, "lock"), "w", encoding="utf-8") as f:
            f.write(text)
        with self.assertRaises(slot.LockBusy):
            self.k.cmd_queue_list()
        with open(os.path.join(self.state, "lock"), encoding="utf-8") as f:
            self.assertEqual(f.read(), text)

    def test_a_dead_watchdog_leaves_watchdog_lost(self):
        def both_die():
            for pid in list(self.fake.procs):
                if self.fake.procs[pid].name == "Unity.exe":
                    self.fake.end(pid, 1)
        self.crashing_watchdog(then=both_die)
        lease = self.grant()
        r = self.launch(lease)
        self.assertTrue(r.line.startswith("WATCHDOG LOST"), r.line)
        L = self.lease()["launches"][0]
        self.assertEqual(L["how"], "watchdog lost")
        self.assertTrue(L["unobserved"])
        self.assertTrue(any(e["event"] == "watchdog error" for e in self.events()))
        self.assertEqual(self.fake.kills, [])

    def test_an_editor_alive_with_no_watchdog_is_adopted(self):
        self.fake.editor_runs = 60
        self.crashing_watchdog()
        lease = self.grant()
        r = self.launch(lease)
        self.assertTrue(r.line.startswith("EXITED 0"), r.line)
        L = self.lease()["launches"][0]
        self.assertEqual(len(L["watchdogs"]), 2)
        self.assertTrue(L["unobserved"])
        self.assertEqual(self.fake.kills, [])

    def test_an_editor_alive_with_no_watchdog_past_its_budget_is_closed(self):
        def late():
            self.fake.t += 3 * 60
        self.crashing_watchdog(then=late)
        lease = self.grant(minutes=1)
        r = self.launch(lease)
        self.assertTrue(r.line.startswith("STOPPED: BUDGET"), r.line)
        ret = self.ret(lease)
        self.assertEqual(self.fake.kills, ["taskkill /PID %d /T /F" % ret["launches"][0]["editor_pid"]])
        self.assertEqual(ret["end_reason"], "budget")


class SafetyAndRecordTests(Base):
    def test_nothing_but_our_own_tree_ever_reaches_the_kill_list(self):
        bridges = [self.fake.add("unity.exe", BRIDGE_EXE, '"%s" mcp' % BRIDGE_EXE).pid for _ in range(6)]
        hub = self.fake.add("Unity.exe", HUB_EXE, '"%s" --headless' % HUB_EXE).pid
        unknown = self.fake.add("Unity.exe", "D:\\Elsewhere\\Unity.exe", "Unity.exe -batchmode").pid
        start = self.fake.t
        foreign = {}

        def foreigners(f):
            if f.t >= start + 10 and not foreign:
                foreign["owner"] = self.foreign_editor(self.owner).pid
                foreign["box"] = self.foreign_editor(self.box_a).pid  # someone opened our box by hand
        self.fake.hooks.append(foreigners)
        self.fake.editor_runs = 20
        self.fake.orphan_child = True  # the import worker outlives its editor
        lease = self.grant()
        self.launch(lease)
        self.fake.sleep(self.cfg["sample_seconds"])
        L = self.lease()["launches"][0]
        ours = {L["editor"]["pid"]} | {c["pid"] for c in L["children"]}
        give = self.k.cmd_give(lease, False)
        self.assertEqual(give.code, slot.EXIT_OK, give.line)
        self.assertEqual(self.k.cmd_stop().line, "NOTHING TO STOP: the slot is free")
        killed = {int(k.split()[2]) for k in self.fake.kills}
        self.assertTrue(killed, "the orphaned worker was closed")
        self.assertTrue(killed <= ours, (killed, ours))
        for pid in bridges + [hub, unknown, foreign["owner"], foreign["box"]]:
            self.assertNotIn(pid, killed)
            self.assertIn(pid, self.fake.procs)
        for k in self.fake.kills:
            self.assertRegex(k, r"^taskkill /PID \d+ /T /F$")
        ret = self.ret(lease)
        self.assertIn(foreign["box"], [p["pid"] for p in ret["launches"][0]["foreign_naming_box"]])
        self.assertIn(foreign["box"], [p["pid"] for p in ret["end_sweep_foreign"]])

    def test_an_orphaned_worker_naming_the_box_is_closed_after_the_editor_exits(self):
        self.fake.editor_runs = 10
        self.fake.orphan_child = True
        lease = self.grant()
        r = self.launch(lease)
        self.assertTrue(r.line.startswith("EXITED 0"), r.line)
        L = self.lease()["launches"][0]
        worker = L["children"][0]["pid"]
        self.assertEqual(self.fake.kills, ["taskkill /PID %d /T /F" % worker])
        self.assertEqual([c["pid"] for c in L["leftovers_closed"]], [worker])
        self.assertNotIn(worker, self.fake.procs)

    def test_give_is_refused_while_a_launch_runs(self):
        self.fake.on_spawn = None
        lease = self.grant()
        self.assertEqual(self.launch(lease, max_minutes=0).code, slot.EXIT_QUEUED)
        r = self.k.cmd_give(lease, False)
        self.assertEqual(r.code, slot.EXIT_REFUSED)
        self.assertIn("launch #1 is running", r.line)
        self.assertEqual(self.lease()["id"], lease)
        r = self.k.cmd_give(lease, True)
        self.assertEqual(r.code, slot.EXIT_OK, r.line)
        self.assertEqual(self.ret(lease)["end_reason"], "give --stop")

    def test_stop_ends_a_held_lease_and_writes_the_return(self):
        lease = self.grant()
        r = self.k.cmd_stop()
        self.assertTrue(r.line.startswith("STOPPED %s" % lease), r.line)
        self.assertEqual(self.ret(lease)["end_reason"], "stopped by owner")
        self.assertIsNone(self.lease())

    def test_the_return_record_carries_every_field(self):
        self.fake.editor_runs = 10
        lease = self.grant()
        self.assertEqual(self.launch(lease).code, slot.EXIT_OK)
        r = self.k.cmd_give(lease, False)
        self.assertTrue(r.line.startswith("RETURN %s" % lease), r.line)
        ret = self.ret(lease)
        for key in ("lease", "label", "box", "added_by", "granted", "end", "end_reason", "launches",
                    "launched_pids", "launched_pids_alive", "all_launched_pids_gone", "disk_free_gb", "summary"):
            self.assertIn(key, ret)
        self.assertEqual((ret["label"], ret["added_by"], ret["end_reason"]), ("lane-a", "seat", "give"))
        v = ret["launches"][0]
        for key in ("args", "editor_pid", "child_pids", "start", "end", "exit_code", "how", "min_headroom_gib",
                    "min_headroom_at", "save_before", "save_after", "save_restored", "foreign_editor_seen",
                    "porcelain"):
            self.assertIn(key, v)
            self.assertIsNotNone(v[key], key)
        for side in ("save_before", "save_after"):
            self.assertEqual(set(v[side]) >= {"sha256_prefix", "size", "world_seed"}, True, side)
        self.assertEqual(v["save_before"]["world_seed"], 12345)
        self.assertEqual(v["exit_code"], 0)
        self.assertEqual(v["how"], "exited")
        self.assertEqual(len(v["child_pids"]), 1)
        self.assertEqual(v["porcelain"]["lines"], [" M Assets/_Project/Code/Thing.cs", "?? artifacts/"])
        self.assertTrue(ret["all_launched_pids_gone"])
        self.assertEqual(ret["launched_pids_alive"], [])
        self.assertEqual(ret["summary"], r.line)

    def test_the_classifier_tells_editors_workers_bridges_and_the_hub_apart(self):
        rule = self.cfg["editor_rule"]
        rows = {
            "editor": slot.Proc(1, 0, "Unity.exe", UNITY, '"%s" -projectpath C:\\hh-x -batchmode' % UNITY, 0),
            "worker": slot.Proc(2, 1, "Unity.exe", UNITY, '"%s" -batchMode -name AssetImportWorker3 -projectPath '
                                                        'C:\\hh-x -parentPid 1' % UNITY, 0),
            "bridge": slot.Proc(3, 0, "unity.exe", BRIDGE_EXE, '"%s" mcp ' % BRIDGE_EXE, 0),
            "hub": slot.Proc(4, 0, "Unity.exe", HUB_EXE, '"%s" -projectPath x' % HUB_EXE, 0),
            "unknown": slot.Proc(5, 0, "Unity.exe", "D:\\Other\\Unity.exe", "Unity.exe -projectPath x", 0),
        }
        for want, proc in rows.items():
            self.assertEqual(slot.classify(proc, rule), want)
        self.assertIsNone(slot.classify(slot.Proc(6, 0, "python.exe", "", "python", 0), rule))

    def test_names_path_matches_the_box_and_not_a_longer_sibling(self):
        self.assertTrue(slot.names_path('Unity.exe -projectPath "C:\\hh-gauntlet\\box"', "C:/hh-gauntlet/box"))
        self.assertTrue(slot.names_path("Unity.exe -projectpath c:/HH-GAUNTLET/box/ -x", "C:\\hh-gauntlet\\box"))
        self.assertFalse(slot.names_path("Unity.exe -projectPath C:\\hh-gauntlet\\box2", "C:\\hh-gauntlet\\box"))
        self.assertFalse(slot.names_path("Unity.exe -projectPath D:\\C\\hh-gauntlet\\box", "C:\\hh-gauntlet\\box"))


if __name__ == "__main__":
    unittest.main()
