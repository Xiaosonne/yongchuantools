#!/usr/bin/env python3
"""Send raw TCP packets to YongChuanTools TCP server.

Usage:
    python send_tcp.py <hex-string>
    python send_tcp.py <hex-string> <ip> <port>

Examples:
    python send_tcp.py "40401ECA0101292D1017031A26CD0E00000000..."
    python send_tcp.py "40401ECA..." 127.0.0.1 44370
"""
import sys
import socket
import binascii
import os

def send_tcp(hex_str, ip='127.0.0.1', port=44370):
    """Send hex data as raw TCP packet."""
    hex_clean = hex_str.replace(" ", "").replace("-", "").replace("\r", "").replace("\n", "")

    if len(hex_clean) % 2 != 0:
        print(f"Error: hex string length must be even, got {len(hex_clean)}")
        return False

    try:
        data = binascii.unhexlify(hex_clean)
    except Exception as e:
        print(f"Error decoding hex: {e}")
        return False

    print(f"Connecting to {ip}:{port}...")
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(5)
        sock.connect((ip, port))
        print(f"Connected! Sending {len(data)} bytes...")
        sock.send(data)
        print(f"Sent {len(data)} bytes.")
        print(f"Hex: {hex_clean[:80]}{'...' if len(hex_clean) > 80 else ''}")
        sock.close()
        return True
    except Exception as e:
        print(f"Error: {e}")
        return False

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    hex_str = sys.argv[1]
    ip = sys.argv[2] if len(sys.argv) > 2 else '127.0.0.1'
    port = int(sys.argv[3]) if len(sys.argv) > 3 else 44370

    send_tcp(hex_str, ip, port)
