#!/usr/bin/env bash
# Live RimWorld log watch for the Monitor tool. The work is in rwwatch.py (one process, no child processes);
# this launcher only keeps the old command name. "rwwatch.sh start" reads the current logs from line 1.
exec python -u "$(dirname "$0")/rwwatch.py" "$@"
