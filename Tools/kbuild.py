#!/usr/bin/env python3
"""
kbuild — System-wide build coordinator for kiloverse projects.

Ensures only one build runs at a time across all sessions (bridge, Claude, other LLMs).
Uses flock for mutual exclusion and a JSON status file for visibility.

Usage:
    kbuild status                     # Show current build state
    kbuild cancel                     # Cancel running build
    kbuild run <command...>           # Run a build (waits in queue if busy)
    kbuild run --timeout 600 <cmd>    # Run with custom timeout (default 900s)
    kbuild run --tag "K1L0 iOS" <cmd> # Tag for display
    kbuild run --who "claude" <cmd>   # Who requested it
    kbuild history                    # Recent build history
    kbuild clean                      # Kill zombie build processes

Status file: /tmp/kbuild_status.json (always readable)
Lock file:   /tmp/kbuild.lock (flock-based)
History:     /tmp/kbuild_history.json (last 20 builds)
"""

import argparse
import fcntl
import json
import os
import signal
import subprocess
import sys
import time
from datetime import datetime, timezone

LOCK_FILE = "/tmp/kbuild.lock"
STATUS_FILE = "/tmp/kbuild_status.json"
HISTORY_FILE = "/tmp/kbuild_history.json"
LOG_FILE = "/tmp/kbuild_current.log"
DEFAULT_TIMEOUT = 900  # 15 minutes
STUCK_TIMEOUT = 120    # No output for 2 minutes = stuck


def utc_now():
    return datetime.now(timezone.utc).isoformat()


def elapsed_str(started_at):
    try:
        start = datetime.fromisoformat(started_at)
        delta = datetime.now(timezone.utc) - start
        secs = int(delta.total_seconds())
        if secs < 60:
            return f"{secs}s"
        return f"{secs // 60}m {secs % 60}s"
    except:
        return "?"


def write_status(state, **kwargs):
    data = {"state": state, "updated_at": utc_now()}
    data.update(kwargs)
    try:
        with open(STATUS_FILE, "w") as f:
            json.dump(data, f, indent=2)
    except:
        pass


def read_status():
    try:
        with open(STATUS_FILE) as f:
            return json.load(f)
    except:
        return {"state": "idle"}


def append_history(entry):
    try:
        history = []
        if os.path.exists(HISTORY_FILE):
            with open(HISTORY_FILE) as f:
                history = json.load(f)
        history.append(entry)
        history = history[-20:]  # Keep last 20
        with open(HISTORY_FILE, "w") as f:
            json.dump(history, f, indent=2)
    except:
        pass


def read_history():
    try:
        with open(HISTORY_FILE) as f:
            return json.load(f)
    except:
        return []


def kill_build_processes():
    """Kill common build process zombies."""
    patterns = [
        "xcodebuild",
        "Unity.*MacOS/Unity.*-batchmode",
        "Unity.ILPP",
        "bee_backend",
        "UnityPackageManager.*-ipc",
        "VBCSCompiler",
    ]
    killed = []
    for pat in patterns:
        result = subprocess.run(
            ["pgrep", "-f", pat], capture_output=True, text=True
        )
        pids = result.stdout.strip().split("\n")
        for pid in pids:
            pid = pid.strip()
            if pid and pid != str(os.getpid()):
                try:
                    os.kill(int(pid), 9)
                    killed.append(f"{pat}:{pid}")
                except:
                    pass
    return killed


def cmd_status(args):
    status = read_status()
    state = status.get("state", "idle")

    if state == "idle":
        print("BUILD: idle — no build running")
        return

    if state == "building":
        tag = status.get("tag", "unknown")
        who = status.get("who", "unknown")
        started = status.get("started_at", "?")
        pid = status.get("pid", "?")
        elapsed = elapsed_str(started)
        last_line = status.get("last_output", "").strip()
        progress = status.get("progress", "")

        print(f"BUILD: running — {tag}")
        print(f"  requested by: {who}")
        print(f"  elapsed: {elapsed}")
        print(f"  pid: {pid}")
        if progress:
            print(f"  progress: {progress}")
        if last_line:
            print(f"  last output: {last_line[:120]}")
        return

    if state == "queued":
        print(f"BUILD: queued — waiting for lock")
        print(f"  tag: {status.get('tag', '?')}")
        print(f"  who: {status.get('who', '?')}")
        return

    print(f"BUILD: {state}")
    for k, v in status.items():
        if k != "state":
            print(f"  {k}: {v}")


def cmd_cancel(args):
    status = read_status()
    pid = status.get("pid")
    if not pid:
        print("No build running (no pid in status)")
        write_status("idle")
        return

    try:
        os.kill(int(pid), signal.SIGTERM)
        time.sleep(1)
        try:
            os.kill(int(pid), signal.SIGKILL)
        except ProcessLookupError:
            pass
        print(f"Cancelled build (pid {pid})")
    except ProcessLookupError:
        print(f"Build process {pid} already dead")
    except Exception as e:
        print(f"Failed to cancel: {e}")

    write_status("idle", cancelled=True, cancelled_at=utc_now())


def cmd_clean(args):
    killed = kill_build_processes()
    if killed:
        print(f"Killed {len(killed)} zombie processes:")
        for k in killed:
            print(f"  {k}")
    else:
        print("No zombie build processes found")
    write_status("idle", cleaned_at=utc_now())


def cmd_history(args):
    history = read_history()
    if not history:
        print("No build history")
        return

    for entry in reversed(history[-10:]):
        status = entry.get("result", "?")
        tag = entry.get("tag", "?")
        duration = entry.get("duration", "?")
        who = entry.get("who", "?")
        when = entry.get("started_at", "?")
        icon = "✓" if status == "success" else "✗" if status == "failed" else "○"
        print(f"  {icon} {tag} — {duration} — by {who} — {when}")


def cmd_run(args):
    if not args.command:
        print("Error: no command specified")
        sys.exit(1)

    command = " ".join(args.command)
    tag = args.tag or command[:60]
    who = args.who or os.environ.get("USER", "unknown")
    timeout = args.timeout or DEFAULT_TIMEOUT

    # Check if a build is already running and detect stuck
    status = read_status()
    if status.get("state") == "building":
        existing_pid = status.get("pid")
        if existing_pid:
            try:
                os.kill(int(existing_pid), 0)  # Check if alive
                # Process exists — check if stuck
                updated = status.get("last_output_at", status.get("started_at", ""))
                if updated:
                    try:
                        last = datetime.fromisoformat(updated)
                        age = (datetime.now(timezone.utc) - last).total_seconds()
                        if age > STUCK_TIMEOUT:
                            print(f"Existing build appears stuck ({int(age)}s no output). Killing it.")
                            os.kill(int(existing_pid), 9)
                            time.sleep(1)
                        else:
                            print(f"Build already running ({status.get('tag', '?')}, {elapsed_str(status.get('started_at', ''))}). Waiting for lock...")
                    except:
                        pass
            except ProcessLookupError:
                # Process is dead but status not cleaned up
                print("Previous build process is dead. Cleaning up.")
                write_status("idle")

    write_status("queued", tag=tag, who=who, command=command, queued_at=utc_now())

    # Acquire lock (blocks until available)
    lock_fd = open(LOCK_FILE, "w")
    try:
        fcntl.flock(lock_fd, fcntl.LOCK_EX)
    except KeyboardInterrupt:
        print("\nCancelled while waiting for lock")
        write_status("idle")
        lock_fd.close()
        sys.exit(1)

    started_at = utc_now()
    start_time = time.time()

    # Kill zombies before starting
    kill_build_processes()

    # Clear log
    with open(LOG_FILE, "w") as f:
        f.write(f"[kbuild] {tag}\n[kbuild] Started: {started_at}\n[kbuild] Command: {command}\n\n")

    proc = subprocess.Popen(
        ["/bin/bash", "-c", command],
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        bufsize=1,
        env={**os.environ, "PATH": "/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:" + os.environ.get("PATH", "")},
    )

    write_status(
        "building",
        tag=tag,
        who=who,
        command=command,
        pid=proc.pid,
        started_at=started_at,
        last_output="starting...",
        last_output_at=started_at,
        progress="",
    )

    print(f"[kbuild] Building: {tag} (pid {proc.pid})")

    last_output_time = time.time()
    last_line = ""
    progress = ""
    exit_code = None

    try:
        while True:
            # Check timeout
            elapsed = time.time() - start_time
            if elapsed > timeout:
                print(f"\n[kbuild] TIMEOUT after {int(elapsed)}s — killing build")
                proc.kill()
                exit_code = -1
                break

            # Check stuck (no output)
            if time.time() - last_output_time > STUCK_TIMEOUT:
                print(f"\n[kbuild] STUCK — no output for {STUCK_TIMEOUT}s — killing build")
                proc.kill()
                exit_code = -2
                break

            # Read output
            line = proc.stdout.readline()
            if not line:
                exit_code = proc.wait()
                break

            line = line.rstrip("\n")
            last_line = line
            last_output_time = time.time()

            # Parse progress from xcodebuild / Unity
            if "BUILD SUCCEEDED" in line or "BUILD FAILED" in line:
                progress = line
            elif "Build Finished" in line:
                progress = line
            elif line.startswith("CompileC ") or line.startswith("Ld "):
                progress = line[:80]
            elif "error:" in line.lower():
                progress = f"ERROR: {line[:80]}"
            elif "[BUSY" in line or "IL2CPP" in line:
                progress = line[:80]

            # Update status periodically (every 2s)
            if time.time() - start_time > 2:
                write_status(
                    "building",
                    tag=tag,
                    who=who,
                    pid=proc.pid,
                    started_at=started_at,
                    elapsed=f"{int(time.time() - start_time)}s",
                    last_output=last_line[:200],
                    last_output_at=utc_now(),
                    progress=progress,
                )

            # Write to log
            with open(LOG_FILE, "a") as f:
                f.write(line + "\n")

            # Print to stdout
            print(line)

    except KeyboardInterrupt:
        print("\n[kbuild] Cancelled by user")
        proc.kill()
        exit_code = -3

    duration = time.time() - start_time
    duration_str = f"{int(duration)}s" if duration < 60 else f"{int(duration // 60)}m {int(duration % 60)}s"

    result = "success" if exit_code == 0 else "failed" if exit_code and exit_code > 0 else "timeout" if exit_code == -1 else "stuck" if exit_code == -2 else "cancelled"

    write_status(
        "idle",
        last_build=tag,
        last_result=result,
        last_duration=duration_str,
        last_who=who,
        completed_at=utc_now(),
    )

    append_history({
        "tag": tag,
        "who": who,
        "result": result,
        "exit_code": exit_code,
        "duration": duration_str,
        "started_at": started_at,
        "completed_at": utc_now(),
    })

    # Release lock
    fcntl.flock(lock_fd, fcntl.LOCK_UN)
    lock_fd.close()

    if result == "success":
        print(f"\n[kbuild] ✓ {tag} — {duration_str}")
    else:
        print(f"\n[kbuild] ✗ {tag} — {result} after {duration_str}")

    sys.exit(0 if exit_code == 0 else 1)


def main():
    parser = argparse.ArgumentParser(description="System-wide build coordinator")
    sub = parser.add_subparsers(dest="action")

    sub.add_parser("status", help="Show current build state")
    sub.add_parser("cancel", help="Cancel running build")
    sub.add_parser("clean", help="Kill zombie build processes")
    sub.add_parser("history", help="Show recent build history")

    run_parser = sub.add_parser("run", help="Run a build command")
    run_parser.add_argument("--tag", help="Display name for this build")
    run_parser.add_argument("--who", help="Who requested it")
    run_parser.add_argument("--timeout", type=int, help=f"Timeout in seconds (default {DEFAULT_TIMEOUT})")
    run_parser.add_argument("command", nargs=argparse.REMAINDER, help="Build command to run")

    args = parser.parse_args()

    if args.action == "status":
        cmd_status(args)
    elif args.action == "cancel":
        cmd_cancel(args)
    elif args.action == "clean":
        cmd_clean(args)
    elif args.action == "history":
        cmd_history(args)
    elif args.action == "run":
        cmd_run(args)
    else:
        parser.print_help()


if __name__ == "__main__":
    main()
