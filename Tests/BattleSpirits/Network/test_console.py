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


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"))
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Install .NET SDK 10 or pass --dotnet /path/to/dotnet")
    build = subprocess.run([args.dotnet, "build", str(PROJECT), "-c", "Release", "--nologo"],
                           cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=120)
    check(build.returncode == 0, build.stdout)
    for options, expected, marker in [(["--help"], 0, "--catalog"),
                                      (["--port", "-1"], 2, "Invalid option"),
                                      (["--catalog", "missing-smoke-catalog.json"], 1, "Unable to start")]:
        result = subprocess.run([args.dotnet, str(DLL), *options], cwd=ROOT, text=True,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=10)
        check(result.returncode == expected and marker in result.stdout, result.stdout)
    process_options = {"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == "nt" else {}
    process = subprocess.Popen([args.dotnet, str(DLL), "--catalog", "Data/BattleSpirits/cards.json", "--port", "0"],
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
        source = json.loads((ROOT / "Data/BattleSpirits/cards.json").read_text(encoding="utf-8"))["cards"]
        supported = [card["id"] for card in source if card.get("implemented")]
        check(len(supported) >= 14, "Smoke test needs enough distinct supported Spirit names")
        deck = (supported * 3)[:40]
        for index in range(2):
            peer = Peer(port)
            peers.append(peer)
            peer.send({"type": "hello", "version": 1, "catalogHash": fingerprint, "name": "Console " + str(index)})
            check(peer.until(lambda value: value["type"] == "welcome")["playerIndex"] == index,
                  "Console assigned the wrong seat")
            peer.send({"type": "deck", "deck": deck})
            peer.send({"type": "ready", "ready": True})
        for peer in peers:
            peer.until(lambda value: value["type"] == "lobby" and all(player["ready"] for player in value["players"]))
        peers[0].send({"type": "start"})
        for index, peer in enumerate(peers):
            view = peer.until(lambda value: value["type"] == "state")["view"]
            check(view["viewer"] == index and view["phase"] == "Opening" and len(view["ownHand"]) == 4,
                  "Console opening state is wrong")
            check(all("hand" not in player and "deck" not in player for player in view["players"]),
                  "Console public view leaked a private list")
        peers[0].send({"type": "action", "action": "concede"})
        for peer in peers:
            view = peer.until(lambda value: value["type"] == "state" and value["view"]["phase"] == "GameOver")["view"]
            check(view["winner"] == 1, "Console clients disagreed about a conceded match")
        process.send_signal(signal.CTRL_BREAK_EVENT if os.name == "nt" else signal.SIGINT)
        check(process.wait(timeout=8) == 0, "Console did not stop successfully")
        worker.join(timeout=1)
        while not output.empty():
            transcript.append(output.get_nowait())
        check("Server stopped." in transcript, "Console graceful shutdown message is missing")
        print(json.dumps({"status": "PASS", "scenarios": 5, "catalogCards": len(source), "playableCards": len(supported),
                          "actualTcpClients": 2, "consoleGracefulShutdown": True,
                          "platform": os.name, "unityPlayTested": False, "windowsLanTested": False}, indent=2))
        return 0
    finally:
        for peer in peers:
            peer.close()
        if process.poll() is None:
            process.kill()
            process.wait(timeout=5)


if __name__ == "__main__":
    raise SystemExit(main())
