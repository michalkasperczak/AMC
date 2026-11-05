"""Offline checks for TIDAL Max parsing and the private DASH proxy."""
import base64
import io
import sys
import unittest
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from amc_wx_lite.tidal_dash_audio import DashProxy, DashResolutionError, resolve_dash


SECRET = "signed-secret"
HOST = "https://sp-ad-fa.audio.tidal.com"


def payload(*, media_host=HOST, quality="HI_RES_LOSSLESS", protection=""):
    manifest = f'''<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" type="static"
      mediaPresentationDuration="PT12S" minBufferTime="PT2S" profiles="urn:mpeg:dash:profile:isoff-on-demand:2011">
      <Period><AdaptationSet mimeType="audio/mp4">{protection}
      <Representation id="1" codecs="flac" audioSamplingRate="192000" bandwidth="5000000">
      <SegmentTemplate timescale="192000" startNumber="1"
        initialization="{HOST}/init.mp4?sig={SECRET}"
        media="{media_host}/seg-$Number$.mp4?sig={SECRET}">
      <SegmentTimeline><S d="768000" r="2"/></SegmentTimeline>
      </SegmentTemplate></Representation></AdaptationSet></Period></MPD>'''
    return {"assetPresentation": "FULL", "audioQuality": quality,
            "bitDepth": 24, "sampleRate": 192000,
            "manifestMimeType": "application/dash+xml",
            "manifest": base64.b64encode(manifest.encode()).decode()}


class Response(io.BytesIO):
    status = 200
    headers = {"Content-Length": "4"}


class Opener:
    def __init__(self):
        self.paths = []

    def open(self, request, timeout):
        self.paths.append(request.full_url.split("?", 1)[0].rsplit("/", 1)[-1])
        return Response(b"data")


class DashTests(unittest.TestCase):
    def test_manifest_rewrites_signed_addresses_and_limits_segments(self):
        plan = resolve_dash(payload(), duration_seconds=12)
        self.assertEqual(plan.bitrate_kbps, 5000)
        self.assertNotIn(SECRET, repr(plan))
        self.assertEqual(plan.segment_url(1).split("/seg-", 1)[1].split("?", 1)[0], "1.mp4")
        self.assertIsNone(plan.segment_url(4))
        opener = Opener()
        with DashProxy(plan, opener=opener) as proxy:
            with urllib.request.urlopen(proxy.local_url) as response:
                manifest = response.read()
            self.assertNotIn(SECRET.encode(), manifest)
            self.assertNotIn(HOST.encode(), manifest)
            root = ET.fromstring(manifest)
            self.assertEqual(root.tag.rsplit("}", 1)[-1], "MPD")
            for suffix in ("init", "seg-1", "seg-3"):
                with urllib.request.urlopen(proxy.local_url[:-4] + "/" + suffix) as response:
                    self.assertEqual(response.read(), b"data")
            with self.assertRaises(urllib.error.HTTPError) as error:
                urllib.request.urlopen(proxy.local_url[:-4] + "/seg-4")
            self.assertEqual(error.exception.code, 404)
            error.exception.close()
        self.assertEqual(opener.paths, ["init.mp4", "seg-1.mp4", "seg-3.mp4"])

    def test_declines_foreign_or_encrypted_manifest_and_bad_metadata(self):
        for info in (payload(media_host="https://evil.example"),
                     payload(protection="<ContentProtection/>",),
                     payload(quality="LOSSLESS")):
            with self.assertRaises(DashResolutionError):
                resolve_dash(info, duration_seconds=12)
        info = payload()
        info["sampleRate"] = "192000"
        with self.assertRaises(DashResolutionError):
            resolve_dash(info, duration_seconds=12)


if __name__ == "__main__":
    unittest.main()
