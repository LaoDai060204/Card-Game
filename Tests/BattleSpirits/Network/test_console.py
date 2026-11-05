#!/usr/bin/env python3
"""Launch the production console host and join its room with two real TCP clients."""
import argparse
from pathlib import Path
import json
import os
import queue
import re
import shutil
import signal
import socket
import subprocess
import threading
import time

ROOT = Path(__file__).resolve().parents[3]
PROJECT = ROOT / "Tools/BattleSpiritsServer/BattleSpirits.Server.csproj"
DLL = ROOT / "Tools/BattleSpiritsServer/bin/Release/net10.0/BattleSpirits.Server.dll"


def check(condition, text):
    if not condition:
        raise AssertionError(text)


class Peer:
    def __init__(self, port):
        self.socket = socket.create_connection(("127.0.0.1", port), timeout=5)
        self.socket.settimeout(5)
        self.reader = self.socket.makefile("r", encoding="utf-8")

    def send(self, value):
        self.socket.sendall((json.dumps(value, ensure_ascii=False) + "\n").encode("utf-8"))

    def until(self, predicate):
        for _ in range(30):
            line = self.reader.readline()
            check(bool(line), "The console host closed a client unexpectedly")
            value = json.loads(line)
            check(value["type"] != "error", "Server rejected the smoke-test request: " + value.get("text", ""))
            if predicate(value):
                return value
        raise AssertionError("Expected console protocol response was not received")

    def close(self):
        self.reader.close()
        self.socket.close()


def exercise_host(dotnet, source, rules_format, format_options):
    process_options = {"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == "nt" else {}
    process = subprocess.Popen([dotnet, str(DLL), "--catalog", "Data/BattleSpirits/cards.json", "--port", "0", *format_options],
                               cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                               bufsize=1, **process_options)
    output = queue.Queue()
    transcript = []

    def read_output():
        for line in process.stdout:
            output.put(line.rstrip())

    worker = threading.Thread(target=read_output, daemon=True)
    worker.start()
    peers = []
    try:
        deadline = time.monotonic() + 12
        port = None
        fingerprint = None
        ready = False
        while time.monotonic() < deadline and not ready:
            try:
                line = output.get(timeout=0.1)
            except queue.Empty:
                check(process.poll() is None, "Console host exited: " + "\n".join(transcript))
                continue
            transcript.append(line)
            if line.startswith("Catalog hash: "):
                fingerprint = line.split(": ", 1)[1]
            matched = re.search(r"Ready on TCP port (\d+)", line)
            if matched:
                port, ready = int(matched[1]), True
        check(ready and fingerprint, "Console host did not become ready: " + "\n".join(transcript))
        check("Ruleset: Japanese Battle Spirits " + rules_format in transcript,
              "Console selected the wrong format: " + "\n".join(transcript))
        check("Protocol: 3" in transcript, "Console did not announce protocol 3")
        eligible = [card for card in source if playable_in_format(card, rules_format)]
        supported = [card["id"] for card in eligible if card.get("type", "Spirit") == "Spirit"]
        check(len(supported) >= 14, "Smoke test needs enough distinct supported Spirit names")
        deck = (supported * 3)[:40]
        for index in range(2):
            peer = Peer(port)
            peers.append(peer)
            peer.send({"type": "hello", "version": 3, "rulesFormat": rules_format,
                       "catalogHash": fingerprint, "name": "Console " + str(index)})
            welcome = peer.until(lambda value: value["type"] == "welcome")
            check(welcome["playerIndex"] == index and welcome["rulesFormat"] == rules_format,
                  "Console assigned the wrong seat or negotiated format")
            peer.send({"type": "deck", "deck": deck})
            peer.send({"type": "ready", "ready": True})
        for peer in peers:
            peer.until(lambda value: value["type"] == "lobby" and all(player["ready"] for player in value["players"]))
        peers[0].send({"type": "start"})
        for index, peer in enumerate(peers):
            state = peer.until(lambda value: value["type"] == "state")
            view = state["view"]
            check(state["version"] == 3 and state["rulesFormat"] == rules_format and view["rulesFormat"] == rules_format,
                  "Console snapshot changed the negotiated format or protocol")
            check(view["viewer"] == index and view["phase"] == "Opening" and len(view["ownHand"]) == 4,
                  "Console opening state is wrong")
            check(all("hand" not in player and "deck" not in player for player in view["players"]),
                  "Console public view leaked a private list")
        peers[0].send({"type": "action", "action": "concede"})
        for peer in peers:
            state = peer.until(lambda value: value["type"] == "state" and value["view"]["phase"] == "GameOver")
            check(state["view"]["winner"] == 1 and state["rulesFormat"] == rules_format
                  and state["view"]["rulesFormat"] == rules_format,
                  "Console clients disagreed about winner or format")
        process.send_signal(signal.CTRL_BREAK_EVENT if os.name == "nt" else signal.SIGINT)
        check(process.wait(timeout=8) == 0, "Console did not stop successfully")
        worker.join(timeout=1)
        while not output.empty():
            transcript.append(output.get_nowait())
        check("Server stopped." in transcript, "Console graceful shutdown message is missing")
        return {"format": rules_format, "playableCards": len(eligible), "deckSpiritNames": len(supported),
                "actualTcpClients": 2, "consoleGracefulShutdown": True}
    finally:
        for peer in peers:
            peer.close()
        if process.poll() is None:
            process.kill()
            process.wait(timeout=5)


def playable_in_format(card, rules_format):
    if not card.get("implemented") or not card.get("released", True) or card.get("maxCopies", 3) <= 0:
        return False
    observed = card.get("format", "Eternal")
    if rules_format == "Standard":
        return (observed in ("Standard", "Both") and card.get("standardLegal")
                and card.get("standardLegalityVerified") and card.get("standardBlock") == "A")
    return observed in ("Eternal", "Both") and card.get("eternalLegal", True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"))
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Install .NET SDK 10 or pass --dotnet /path/to/dotnet")
    build = subprocess.run([args.dotnet, "build", str(PROJECT), "-c", "Release", "--nologo"],
                           cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=120)
    check(build.returncode == 0, build.stdout)
    cases = [(["--help"], 0, "--format Standard"),
             (["--port", "-1"], 2, "Invalid option"),
             (["--catalog", "missing-smoke-catalog.json"], 1, "Unable to start"),
             (["--format", "Saga"], 2, "Invalid option"),
             (["--format", "0"], 2, "Invalid option"),
             (["--format"], 2, "Invalid option")]
    for options, expected, marker in cases:
        result = subprocess.run([args.dotnet, str(DLL), *options], cwd=ROOT, text=True,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=10)
        check(result.returncode == expected and marker in result.stdout, result.stdout)
    source = json.loads((ROOT / "Data/BattleSpirits/cards.json").read_text(encoding="utf-8"))["cards"]
    # Absence of --format deliberately tests the new Standard default. Explicit
    # Eternal verifies that existing clients can still use their reviewed pool.
    hosts = [exercise_host(args.dotnet, source, "Standard", []),
             exercise_host(args.dotnet, source, "Eternal", ["--format", "Eternal"])]
    print(json.dumps({"status": "PASS", "scenarios": len(cases) + 2 * len(hosts),
                      "argumentScenarios": len(cases), "catalogCards": len(source),
                      "protocolVersion": 3, "hosts": hosts,
                      "platform": os.name, "unityPlayTested": False, "windowsLanTested": False}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
