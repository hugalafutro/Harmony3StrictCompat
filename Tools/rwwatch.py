"""Live RimWorld log watch for the Monitor tool: prints one line per interesting log line.

One process, no child processes: it polls the two log files by size every 2 s and reads only the new bytes.
(The earlier bash version started about eight processes per poll, and stacked watchers were never stopped.)
Survives the game replacing Player.log on relaunch (the file shrinks, so reading restarts at 0).
Only one watcher runs at a time: a new one takes over and the old one exits within one poll.

    python rwwatch.py          watch from the current end of the logs
    python rwwatch.py start    read the current logs from the beginning first
"""
import os
import re
import sys
import time

LOGS = os.path.join(os.environ["USERPROFILE"], "AppData", "LocalLow", "Ludeon Studios",
                    "RimWorld by Ludeon Studios", "Logs")
POLL_SECONDS = 2
MAX_LINES_PER_POLL = 40  # a log flood becomes one summary line, not thousands of notifications

# (tag, file, match, skip). Known and accepted lines are skipped (see memory rimworld-log-watch).
WATCHED = [
    ("RW", "Player.log",
     re.compile(r" (ERROR|FATAL) |Exception| WARN.*\[Harmony3StrictCompat\]|\[WOWGAG|멈춤 [0-9.]+초|\[OgreStack\]"
                r"|Truncating|\[AdaptiveMealBill\]|Could not load reference"),
     # Stack lines, the HD keep-API pair, and [GUIPerf] summaries that do not report a freeze.
     re.compile(r"^\s+at |per-pawn loadout keep API|keep API bridged|emptied duplicate-comp patch|Modify Stack Sizes Complete|\[WOWGAG 공지\]"
                r"|\[GUIPerf\](?!.*멈춤 [0-9.]+초)")),
    ("HD", "HaulersDream-debug.log", re.compile(r"WARN|ERR"), re.compile(r"per-pawn loadout keep API")),
]


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    from_start = len(sys.argv) > 1 and sys.argv[1] == "start"

    # Single instance: the newest watcher owns the token file; an older one sees a foreign token and exits.
    token_path = os.path.join(os.environ.get("TEMP", LOGS), "rwwatch.token")
    token = f"{os.getpid()}-{time.time()}"
    with open(token_path, "w") as f:
        f.write(token)

    offsets = {}  # file -> bytes already read
    partial = {}  # file -> bytes of an unfinished last line
    while True:
        try:
            with open(token_path) as f:
                if f.read() != token:
                    return
        except OSError:
            pass
        for tag, name, match, skip in WATCHED:
            path = os.path.join(LOGS, name)
            try:
                size = os.path.getsize(path)
            except OSError:
                continue
            if path not in offsets:
                offsets[path] = 0 if from_start else size
                partial[path] = b""
            if size < offsets[path]:
                print(f"[{tag}] log restarted (game relaunch, or Hauler's Dream rotating its log at 8 MB)")
                offsets[path], partial[path] = 0, b""
            if size > offsets[path]:
                try:
                    with open(path, "rb") as f:
                        f.seek(offsets[path])
                        data = partial[path] + f.read(size - offsets[path])
                except OSError:
                    continue
                offsets[path] = size
                lines = data.split(b"\n")
                partial[path] = lines.pop()  # empty when the data ended with a newline
                hits = []
                for raw in lines:
                    line = raw.decode("utf-8", "replace").rstrip("\r")
                    if match.search(line) and not skip.search(line):
                        hits.append(line)
                for line in hits[:MAX_LINES_PER_POLL]:
                    print(f"[{tag}] {line[:300]}")
                if len(hits) > MAX_LINES_PER_POLL:
                    print(f"[{tag}] ... {len(hits) - MAX_LINES_PER_POLL} more matching lines in this poll (flood)")
        sys.stdout.flush()
        time.sleep(POLL_SECONDS)


if __name__ == "__main__":
    main()
