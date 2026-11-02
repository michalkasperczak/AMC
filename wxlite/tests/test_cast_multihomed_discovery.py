from __future__ import annotations

import socket
import sys
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from amc_wx_lite import cast_protocol


class FakeSocket:
    def __init__(self):
        self.multicast_interface = None
        self.sent = []
        self.closed = False

    def setsockopt(self, level, option, value):
        assert level == socket.IPPROTO_IP and option == socket.IP_MULTICAST_IF
        self.multicast_interface = socket.inet_ntoa(value)

    def sendto(self, packet, destination):
        self.sent.append((packet, destination))

    def close(self):
        self.closed = True


class MultihomedDiscoveryTests(unittest.TestCase):
    def test_queries_wifi_when_default_route_may_use_vpn(self):
        created = []

        def make_socket(*_args):
            result = FakeSocket()
            created.append(result)
            return result

        records = [(socket.AF_INET, socket.SOCK_DGRAM, 0, "", (address, 0))
                   for address in ("100.100.221.1", "192.168.0.183")]
        with (patch.object(cast_protocol.socket, "gethostname", return_value="test-pc"),
              patch.object(cast_protocol.socket, "getaddrinfo", return_value=records),
              patch.object(cast_protocol.socket, "socket", side_effect=make_socket),
              patch.object(cast_protocol.select, "select", return_value=([], [], []))):
            self.assertEqual(cast_protocol.discover_cast_devices(timeout=0.1), ())

        self.assertEqual([item.multicast_interface for item in created],
                         [None, "100.100.221.1", "192.168.0.183"])
        self.assertTrue(all(len(item.sent) == 1 and item.closed for item in created))
        self.assertTrue(all(item.sent[0][1] == cast_protocol._MULTICAST
                            for item in created))


if __name__ == "__main__":
    unittest.main()
