#!/usr/bin/env python3
"""
PATIENT ZERO — Unity MCP driver.
Talks to the Unity Editor's MCP relay (~/.unity/relay/) over stdio JSON-RPC.

Usage:
  python3 tools/unity_mcp.py doctor                      # check everything is ready
  python3 tools/unity_mcp.py list                        # list Unity MCP tools
  python3 tools/unity_mcp.py call <tool_name> '<json>'   # call a tool
"""
import json, os, subprocess, sys, glob, select, time

PROTOCOL_VERSION = "2024-11-05"
CLIENT_INFO = {"name": "patient-zero-driver", "version": "1.0.0"}


def find_relay():
    """Locate the Unity MCP relay binary."""
    roots = glob.glob(os.path.expanduser("~/.unity/relay/**"), recursive=True)
    cands = [p for p in roots
             if os.path.isfile(p) and os.access(p, os.X_OK) and not p.endswith((".json", ".log", ".txt"))]
    if not cands:
        return None
    return sorted(cands)[-1]


class MCP:
    def __init__(self, relay_path):
        self.proc = subprocess.Popen(
            [relay_path, "--mcp"],
            stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL, text=True, bufsize=1)
        self._id = 0

    def _send(self, method, params=None, is_notification=False):
        self._id += 1
        msg = {"jsonrpc": "2.0", "method": method}
        if not is_notification:
            msg["id"] = self._id
        if params is not None:
            msg["params"] = params
        self.proc.stdin.write(json.dumps(msg) + "\n")
        self.proc.stdin.flush()
        return None if is_notification else self._id

    def _read(self, want_id, timeout=30):
        end = time.time() + timeout
        while time.time() < end:
            r, _, _ = select.select([self.proc.stdout], [], [], max(0.1, end - time.time()))
            if not r:
                continue
            line = self.proc.stdout.readline()
            if not line:
                break
            line = line.strip()
            if not line:
                continue
            try:
                msg = json.loads(line)
            except json.JSONDecodeError:
                continue
            if msg.get("id") == want_id:
                return msg
        return None

    def initialize(self):
        rid = self._send("initialize", {
            "protocolVersion": PROTOCOL_VERSION,
            "capabilities": {},
            "clientInfo": CLIENT_INFO,
        })
        resp = self._read(rid)
        if resp:
            self._send("notifications/initialized", is_notification=True)
        return resp

    def list_tools(self):
        rid = self._send("tools/list", {})
        return self._read(rid, timeout=45)

    def call_tool(self, name, arguments):
        rid = self._send("tools/call", {"name": name, "arguments": arguments})
        return self._read(rid, timeout=120)

    def close(self):
        try:
            self.proc.terminate()
        except Exception:
            pass


def unity_running():
    r = subprocess.run(["pgrep", "-f", "Unity.*-projectPath"], capture_output=True)
    return r.returncode == 0


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    cmd = sys.argv[1]

    relay = find_relay()
    if cmd == "doctor":
        print(f"relay binary : {relay or 'NOT FOUND (start Unity once with AI features enabled)'}")
        print(f"unity editor : {'RUNNING' if unity_running() else 'not running'}")
        if relay:
            m = MCP(relay)
            resp = m.initialize()
            print(f"handshake    : {'OK — ' + json.dumps(resp.get('result', {}).get('serverInfo', {})) if resp else 'FAILED'}")
            m.close()
        return

    if not relay:
        print("ERROR: relay binary not found at ~/.unity/relay/ — see docs/UNITY_MCP.md")
        sys.exit(2)

    m = MCP(relay)
    if not m.initialize():
        print("ERROR: MCP handshake failed — is Unity Editor running with the MCP bridge?")
        sys.exit(2)

    if cmd == "list":
        resp = m.list_tools()
        tools = (resp or {}).get("result", {}).get("tools", [])
        for t in tools:
            print(f"- {t.get('name')}: {(t.get('description') or '')[:90]}")
        print(f"({len(tools)} tools)")
    elif cmd == "call":
        if len(sys.argv) < 4:
            print("usage: unity_mcp.py call <tool_name> '<json_arguments>'")
            sys.exit(1)
        resp = m.call_tool(sys.argv[2], json.loads(sys.argv[3]))
        print(json.dumps(resp.get("result", resp) if resp else {"error": "timeout"}, indent=2)[:6000])
    m.close()


if __name__ == "__main__":
    main()
