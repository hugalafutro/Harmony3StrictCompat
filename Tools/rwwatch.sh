#!/usr/bin/env bash
# Live RimWorld log watch for the Monitor tool: prints one line per interesting log line.
# Polls by line count instead of tail -F, so it survives the game replacing Player.log on relaunch.
# Watches Player.log (errors, compat messages, WOWGAG freeze lines, OgreStack re-stack) and
# Hauler's Dream's own debug log (WARN/ERR).
LOGS="$USERPROFILE/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Logs"
PLAYER_RE=' (ERROR|FATAL) |Exception| WARN.*\[Harmony3StrictCompat\]|\[WOWGAG|멈춤 [0-9.]+초|\[OgreStack\]|Truncating|\[AdaptiveMealBill\]|Could not load reference'
# Known and accepted (see memory rimworld-log-watch): HD keep-API warning and its bridge confirmation.
PLAYER_SKIP='^[[:space:]]+at |per-pawn loadout keep API|keep API bridged|emptied duplicate-comp patch'
# "rwwatch.sh start" reads the current logs from line 1 (watch armed after the game already started).
[ "$1" = start ] && FROM_START=1
HD_RE='WARN|ERR'

declare -A seen
check() { # $1 tag, $2 file, $3 match regex, $4 skip regex
	local f="$2" n
	[ -f "$f" ] || return
	n=$(wc -l < "$f")
	if [ -z "${seen[$1]}" ]; then   # first pass: start at the end, or at line 1 with "start"
		if [ -n "$FROM_START" ]; then seen[$1]=0; else seen[$1]=$n; return; fi
	fi
	if [ "$n" -lt "${seen[$1]}" ]; then echo "[$1] log restarted (game relaunch, or Hauler's Dream rotating its log at 8 MB)"; seen[$1]=0; fi
	if [ "$n" -gt "${seen[$1]}" ]; then
		# WOWGAG's [GUIPerf] summaries count caught exceptions: keep a GUIPerf line only when it reports a freeze.
		sed -n "$((seen[$1] + 1)),${n}p" "$f" | grep -aE "$3" | { if [ -n "$4" ]; then grep -avE "$4"; else cat; fi; } \
			| grep -aPv '\[GUIPerf\](?!.*멈춤 [0-9.]+초)' \
			| cut -c1-300 | sed "s/^/[$1] /"
		seen[$1]=$n
	fi
}
while true; do
	check RW "$LOGS/Player.log" "$PLAYER_RE" "$PLAYER_SKIP"
	check HD "$LOGS/HaulersDream-debug.log" "$HD_RE" "per-pawn loadout keep API"
	sleep 2
done
