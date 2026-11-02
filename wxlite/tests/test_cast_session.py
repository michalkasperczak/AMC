from __future__ import annotations
import socket
import os
import sqlite3
import struct
import sys
import tempfile
import unittest
import urllib.error
import urllib.request
from contextlib import closing
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from amc_wx_lite.cast_media import CastFileShare, cast_source
from amc_wx_lite.cast_protocol import CastDevice, CastError, _cast_message, _parse_cast_message, _dns_name
from amc_wx_lite.cast_session import CastSessionRouter, UNHANDLED
from amc_wx_lite.cast_session import cast_action_available
from amc_wx_lite.cast_session import cast_session_available
from amc_wx_lite.shortcuts import Action, Chord, resolve, command_presentation, describe, describe_action
from amc_wx_lite.navigation import SessionId
from amc_wx_lite import command_palette, menu_model
from amc_wx_lite import i18n
from amc_wx_lite.host_client import LiteHostClient, HostError


class FakeConnection:
    fail_load = False
    def __init__(self, device):
        self.device = device
        self.commands = []
        self.closed = False
        self.state = "PLAYING"
    def connect(self): pass
    def load(self, url, mime, title, **kwargs):
        if self.fail_load:
            raise CastError("Odrzucono materiał")
        self.commands.append(("LOAD", url))
        self.state = "PLAYING" if kwargs.get("autoplay", True) else "PAUSED"
    def status(self):
        return {"playerState": self.state, "currentTime": 17, "media": {"duration": 100}}
    def receiver_status(self): return {"volume": {"level":0.2,"muted":False}}
    def transport(self, command, **kwargs):
        self.commands.append((command, kwargs))
        self.state = {"PLAY": "PLAYING", "PAUSE": "PAUSED", "STOP": "IDLE"}.get(command,self.state)
    def set_volume(self, level, **kwargs): self.commands.append(("VOLUME",level))
    def close(self): self.closed = True


class CastTests(unittest.TestCase):
    def test_capability_rejects_protected_current_source_but_allows_return(self):
        status={'source':'https://youtube.com/watch?v=abc','playing':True}
        self.assertFalse(cast_session_available('podcasts',status))
        self.assertTrue(cast_session_available('podcasts',status,active=True))
        self.assertFalse(cast_session_available('spotify'))
        self.assertTrue(cast_session_available('radio'))

    def test_selection_resolves_stable_id_again_before_connecting(self):
        router,_,discoveries=self.router()
        router.devices()
        router._discover=lambda:(CastDevice('stable-id','WiiM Sound','192.168.1.99'),)
        router.select('radio','stable-id')
        self.assertEqual(router.routes['radio'].device.host,'192.168.1.99')
        router.close()

    def test_refresh_keeps_unavailable_selected_device_readable(self):
        router,_,_=self.router()
        router.select('radio','stable-id')
        router._discover=lambda:()
        device=router.devices(refresh=True)[0]
        self.assertFalse(device.available)
        self.assertIn('WiiM Sound',device.label)
        self.assertNotIn('stable-id',device.label)
        with self.assertRaises(CastError): router.select('radio','stable-id')
        self.assertTrue(router.active('radio'))
        router.close()

    def test_paused_return_is_silent_and_preserves_local_volume_and_rate(self):
        router,calls,_=self.router()
        native=router.native
        def configured(op,args):
            result=native(op,args)
            return {**result,'volume':35,'rate':1.5} if op=='transport.status' else result
        router.native=configured
        router.select('radio','stable-id')
        router.call('transport.pauseResume',{'sessionId':'radio'})
        router.select('radio',None)
        args=next(args for op,args in calls if op=='radio.play')
        self.assertEqual(args['volume'],0)
        self.assertEqual(args['rate'],1.5)
        self.assertEqual(calls[-1],('transport.setVolume',{'sessionId':'radio','volume':35}))

    @unittest.skipUnless(os.environ.get('AMC_CAST_TEST_HOST'), 'Wymaga jawnego AMC_CAST_TEST_HOST')
    def test_host_records_cast_history_in_the_correct_session(self):
        with tempfile.TemporaryDirectory(prefix='amc-cast-history-') as temporary:
            profile=Path(temporary)
            client=LiteHostClient(os.environ['AMC_CAST_TEST_HOST'],
                library_db=profile/'library.db',podcasts_db=profile/'podcasts.db',
                state_json=profile/'state.json',release_profile='test')
            try:
                client.start(); client.hello()
                result=client.call('cast.notePlayback',{'sessionId':'files','id':'cast-file-one'})
                self.assertIsNone(result.get('historyError'))
                result=client.call('cast.notePlayback',{'sessionId':'radio','id':'cast-station-one'})
                self.assertIsNone(result.get('historyError'))
                with self.assertRaises(HostError):
                    client.call('cast.notePlayback',{'sessionId':'spotify','id':'must-not-record'})
                with closing(sqlite3.connect(profile/'library.db')) as database:
                    rows=database.execute('SELECT session_id,item_id FROM playback_history ORDER BY session_id').fetchall()
                self.assertEqual(rows,[('local','cast-file-one'),('radio','cast-station-one')])
            finally: client.close()

    def test_command_name_shortcut_help_and_palette_parity(self):
        shared=command_presentation(Action.CAST_SESSION)
        self.assertEqual(shared.shortcut,'Shift+C')
        self.assertEqual(describe_action(Action.CAST_SESSION),shared.description)
        self.assertIn(('Shift+C',shared.description),describe())
        for session in (SessionId.FILES,SessionId.RADIO,SessionId.PODCASTS):
            entries=command_palette.entries_for_session(session)
            entry=next(e for e in entries if e.action is Action.CAST_SESSION)
            self.assertEqual((entry.name,entry.shortcut),(shared.name,shared.shortcut))
            menu=next(item for m in menu_model.menus_for_session(session) for item in m.items
                      if item.action is Action.CAST_SESSION)
            self.assertEqual(menu.shortcut,shared.shortcut)
        for session in (SessionId.SPOTIFY,SessionId.TIDAL,SessionId.WIIM,SessionId.SONOS):
            self.assertFalse(any(e.action is Action.CAST_SESSION for e in command_palette.entries_for_session(session)))

    def test_shift_c_and_original_audio_shortcut_in_both_views(self):
        for player in (False,True):
            self.assertIs(resolve(Chord('C',shift=True),player_view=player,radio_session=True),Action.CAST_SESSION)
            self.assertIs(resolve(Chord('A',shift=True),player_view=player,radio_session=True),Action.SELECT_AUDIO_OUTPUT)
            for context in ('wiim_session','sonos_session','spotify_session','tidal_session','frontier_smart_session'):
                self.assertIsNone(resolve(Chord('C',shift=True),player_view=player,radio_session=False,**{context:True}))

    def test_cast_capabilities_block_native_only_commands(self):
        for action in (Action.RATE_UP,Action.ADD_BOOKMARK,Action.QUEUE_NEXT,Action.VIEW_CHAPTERS,
                       Action.CLIP_REMOVE,Action.CLIP_APPEND,Action.CLIP_EXPORT):
            self.assertTrue(cast_action_available(action,False))
            self.assertFalse(cast_action_available(action,True,'files'))
        self.assertTrue(cast_action_available(Action.SEEK_FORWARD_10,True,'files'))
        self.assertFalse(cast_action_available(Action.SEEK_FORWARD_10,True,'radio'))
        self.assertTrue(cast_action_available(Action.PLAY_PAUSE,True,'radio'))

    def test_playback_routes_only_the_selected_session(self):
        router,calls,_=self.router()
        events=[]
        router.event=lambda name,data:events.append((name,data))
        router.select('radio','stable-id')
        calls.clear()
        result=router.call('radio.play',{'url':'https://radio.example/other','id':'other','title':'Nowa stacja'})
        self.assertTrue(result['cast'])
        self.assertEqual([op for op,_ in calls],['cast.notePlayback'])
        self.assertEqual(events[-1][0],'playback.started')
        self.assertEqual(events[-1][1]['sessionId'],'radio')
        self.assertIs(router.call('files.play',{'path':'anything'}),UNHANDLED)
        router.close()

    def test_wire_roundtrip_and_invalid_frame(self):
        frame = _cast_message("sender", "receiver", "namespace", {"type": "LOAD", "title": "Głośnik"})
        self.assertEqual(struct.unpack(">I",frame[:4])[0], len(frame)-4)
        ns, sender, data = _parse_cast_message(frame[4:])
        self.assertEqual((ns,sender,data["title"]),("namespace","sender","Głośnik"))
        with self.assertRaises(ValueError): _parse_cast_message(b"\x32\x05x")

    def test_cast_labels_and_errors_are_intentional_in_both_languages(self):
        previous=i18n.active_language()
        try:
            for language in ('pl','en'):
                i18n.configure(language)
                for key in i18n._CATALOGS[language]:
                    if key.startswith('cast.error.'):
                        text=str(CastError(key,name='WiiM Sound'))
                        self.assertNotIn('cast.error.',text)
                        self.assertNotIn('{name}',text)
                device=CastDevice('technical-id','WiiM Sound','192.168.1.2',available=False)
                self.assertIn('WiiM Sound',str(device))
                self.assertNotIn('technical-id',str(device))
                self.assertNotIn('CastDevice',str(device))
        finally: i18n.configure(previous)

    def test_dns_pointer_cycle_is_bounded(self):
        with self.assertRaises(ValueError): _dns_name(b"\xc0\x00",0)

    def test_source_blocks_protected_platform_and_loopback(self):
        for source in ("https://www.youtube.com/watch?v=abc", "https://tidal.com/a.mp3",
                       "http://127.0.0.1/song.mp3", "https://name:pass@example.com/song.mp3"):
            with self.assertRaises(CastError): cast_source(source)
        self.assertEqual(cast_source("https://radio.example/live")[1], "audio/mpeg")

    def test_share_ranges_and_no_directory_access(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp)/"song.wav"
            path.write_bytes(b"0123456789")
            share = CastFileShare(path,"127.0.0.1","audio/wav")
            try:
                request = urllib.request.Request(share.url,headers={"Range":"bytes=2-5"})
                with urllib.request.urlopen(request) as reply:
                    self.assertEqual(reply.status,206)
                    self.assertEqual(reply.read(),b"2345")
                    self.assertEqual(reply.headers["Content-Range"],"bytes 2-5/10")
                for address in (share.url+"/../song.wav", share.url.rsplit("/",1)[0]+"/"):
                    with self.assertRaises(urllib.error.HTTPError) as error: urllib.request.urlopen(address)
                    self.assertEqual(error.exception.code,404)
                    error.exception.close()
            finally: share.close()

    def router(self, connection=FakeConnection):
        calls=[]
        def native(op,args):
            calls.append((op,args))
            return {"playing":True,"paused":False,"source":"https://radio.example/live",
                    "title":"Radio", "id":"radio-1", "positionSeconds":0}
        device=CastDevice("stable-id","WiiM Sound","192.168.1.2")
        discoveries=[]
        def discover(): discoveries.append(1); return (device,)
        return CastSessionRouter(native,discover=discover,connect=connection),calls,discoveries

    def test_selection_cache_and_successful_handoff(self):
        router,calls,discoveries=self.router()
        router.devices(); router.devices()
        self.assertEqual(len(discoveries),1)
        router.select("radio","stable-id")
        self.assertEqual([x[0] for x in calls],["transport.status","transport.pauseResume","transport.stop"])
        self.assertTrue(router.active("radio"))
        self.assertEqual(router.call("transport.status",{"sessionId":"radio"})["title"],"Radio")
        self.assertIs(router.call("transport.status",{"sessionId":"files"}),UNHANDLED)
        router.close()

    def test_failed_load_resumes_native_and_does_not_select(self):
        class Failing(FakeConnection): fail_load=True
        router,calls,_=self.router(Failing)
        with self.assertRaises(CastError): router.select("radio","stable-id")
        self.assertFalse(router.active("radio"))
        self.assertEqual([x[0] for x in calls], ["transport.status","transport.pauseResume","transport.pauseResume"])

    def test_return_local_uses_remote_position_and_releases_connection(self):
        router,calls,_=self.router()
        router.select("radio","stable-id")
        connection=router.routes["radio"].connection
        router.select("radio",None)
        self.assertFalse(router.active("radio"))
        play=next(args for op,args in calls if op=="radio.play")
        self.assertEqual(play["url"],"https://radio.example/live")
        self.assertTrue(connection.closed)

    def test_one_physical_device_cannot_own_two_sessions(self):
        router,_,_=self.router()
        router.select("radio","stable-id")
        with self.assertRaises(CastError): router.select("files","stable-id")
        router.close()

    def test_native_podcast_start_stops_cast_files_instead_of_mirroring(self):
        router,calls,_=self.router()
        router.select('files','stable-id')
        connection=router.routes['files'].connection
        calls.clear()
        router.call('media.play',{'source':'https://example.test/episode.mp3','id':'episode'})
        self.assertEqual(calls[0][0],'media.play')
        self.assertTrue(router.routes['files'].stopped)
        self.assertIn('STOP',[op for op,_ in connection.commands])
        router.close()

    def test_stopped_cast_restarts_the_material_on_play(self):
        router,_,_=self.router()
        router.select('radio','stable-id')
        router.call('transport.stop',{'sessionId':'radio'})
        result=router.call('transport.pauseResume',{'sessionId':'radio'})
        self.assertTrue(result['playing'])
        router.close()


if __name__ == "__main__": unittest.main()
