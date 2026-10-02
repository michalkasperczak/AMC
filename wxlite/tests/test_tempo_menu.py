"""Real GUI command methods on tiny control doubles, not a Windows GUI test."""
from types import SimpleNamespace
from test_gui_logic import install_wx_stub
install_wx_stub()
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.state_store import Options


class Runner:
    def submit(self, name, work, done, failed):
        try:
            answer = work()
        except Exception as error:
            failed(error)
        else:
            done(answer)


class Item:
    def __init__(self):
        self.checked = False
        self.enabled = True
    def Check(self, value):
        self.checked = value
    def Enable(self, value):
        self.enabled = value


def frame_for(client):
    messages = []
    saves = []
    frame = SimpleNamespace(options=Options(), client=client, runner=Runner(),
        tempo_items={value: Item() for value in (0, 1, 2)},
        announcer=SimpleNamespace(say=messages.append),
        _save_state=lambda: saves.append(True))
    return frame, messages, saves


def test_algorithm_choice_calls_host_before_persisting():
    calls = []
    def configure(**payload):
        calls.append(payload)
        return {"tempoAlgorithm": payload["tempoAlgorithm"], "appliesOnNextPlayback": True}
    frame, messages, saves = frame_for(SimpleNamespace(configure_audio=configure))
    assert hasattr(LiteFrame, "_set_tempo_algorithm"), "Menu choice must execute a host command"
    LiteFrame._set_tempo_algorithm(frame, 2)
    assert calls[0]["tempoAlgorithm"] == 2
    assert frame.options.tempo_algorithm == 2
    assert frame.tempo_items[2].checked and saves == [True]
    assert any("ponown" in text for text in messages)


def test_failed_algorithm_choice_does_not_save_false_success():
    def configure(**payload):
        raise RuntimeError("library missing")
    frame, messages, saves = frame_for(SimpleNamespace(configure_audio=configure))
    assert hasattr(LiteFrame, "_set_tempo_algorithm")
    LiteFrame._set_tempo_algorithm(frame, 2)
    assert frame.options.tempo_algorithm == 1
    assert frame.tempo_items[1].checked and not saves
    assert any("library missing" in text for text in messages)
    assert all(item.enabled for item in frame.tempo_items.values())
