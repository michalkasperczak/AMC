"""Local Windows named pipe. No NVDA imports; testable outside the reader."""
import ctypes
from ctypes import wintypes
import json
import os
import time

COMMANDS = frozenset((
    "status", "playPause", "previous", "next", "volumeUp", "volumeDown", "mute",
    "seekBack", "seekForward", "elapsed", "remaining", "total", "sessionPrevious", "sessionNext",
))


class BridgeError(Exception):
    pass


class Overlapped(ctypes.Structure):
    _fields_ = [("Internal", ctypes.c_size_t), ("InternalHigh", ctypes.c_size_t),
                ("Offset", wintypes.DWORD), ("OffsetHigh", wintypes.DWORD), ("hEvent", wintypes.HANDLE)]


def parse_reply(data):
    try:
        reply = json.loads(data.decode("utf-8"))
        if (not isinstance(reply, dict) or type(reply.get("version")) is not int
                or reply["version"] != 1 or type(reply.get("ok")) is not bool
                or not isinstance(reply.get("message"), str) or len(reply["message"]) > 2000):
            raise ValueError()
        return reply
    except (ValueError, UnicodeError, TypeError):
        raise BridgeError("Nieprawidłowa odpowiedź AMC. Sprawdź wersję programu.") from None


def exchange(command, pipe_name=None):
    if command not in COMMANDS:
        raise BridgeError("Nieobsługiwane polecenie AMC.")
    k = ctypes.WinDLL("kernel32", use_last_error=True)
    k.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                             ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
    k.CreateFileW.restype = wintypes.HANDLE
    k.CreateEventW.argtypes = [ctypes.c_void_p, wintypes.BOOL, wintypes.BOOL, wintypes.LPCWSTR]
    k.CreateEventW.restype = wintypes.HANDLE
    k.CloseHandle.argtypes = [wintypes.HANDLE]
    k.ProcessIdToSessionId.argtypes = [wintypes.DWORD, ctypes.POINTER(wintypes.DWORD)]
    k.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
    k.WaitForSingleObject.restype = wintypes.DWORD
    k.GetOverlappedResult.argtypes = [wintypes.HANDLE, ctypes.POINTER(Overlapped),
                                    ctypes.POINTER(wintypes.DWORD), wintypes.BOOL]
    k.CancelIoEx.argtypes = [wintypes.HANDLE, ctypes.POINTER(Overlapped)]
    for name in ("ReadFile", "WriteFile"):
        getattr(k, name).argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
                                     ctypes.POINTER(wintypes.DWORD), ctypes.POINTER(Overlapped)]
    if pipe_name is None:
        session = wintypes.DWORD()
        if not k.ProcessIdToSessionId(os.getpid(), ctypes.byref(session)):
            raise BridgeError("Nie można ustalić sesji Windows.")
        pipe_name = "AMC.NVDA.v1.%d" % session.value
    # SQOS IDENTIFICATION prevents a pipe server from impersonating NVDA.
    connect_deadline = time.monotonic() + .4
    while True:
        handle = k.CreateFileW("\\\\.\\pipe\\" + pipe_name, 0xC0000000, 0, None, 3,
                               0x40000000 | 0x00100000 | 0x00010000, None)
        if handle != ctypes.c_void_p(-1).value:
            break
        # Retry only opening the pipe, before sending any command bytes.
        if ctypes.get_last_error() not in (2, 231) or time.monotonic() >= connect_deadline:
            raise BridgeError("Brak połączenia z AMC. Uruchom wersję alpha 339 lub nowszą; jeśli działa, spróbuj za chwilę.")
        time.sleep(.015)
    deadline = time.monotonic() + 2.5

    def transfer(name, buffer, size):
        event = k.CreateEventW(None, True, False, None)
        if not event:
            raise BridgeError("Nie można utworzyć połączenia z AMC.")
        op = Overlapped()
        op.hEvent = event
        count = wintypes.DWORD()
        try:
            if not getattr(k, name)(handle, buffer, size, ctypes.byref(count), ctypes.byref(op)):
                if ctypes.get_last_error() != 997:  # ERROR_IO_PENDING
                    raise BridgeError("Połączenie z AMC zostało przerwane. Sprawdź stan odtwarzania.")
                wait_ms = max(0, int((deadline - time.monotonic()) * 1000))
                if k.WaitForSingleObject(event, wait_ms) != 0:
                    k.CancelIoEx(handle, ctypes.byref(op))
                    # Drain cancellation before freeing OVERLAPPED / buffer memory.
                    k.GetOverlappedResult(handle, ctypes.byref(op), ctypes.byref(count), True)
                    raise BridgeError("AMC nie odpowiedział. Nie ponawiam polecenia; sprawdź stan odtwarzania.")
                if not k.GetOverlappedResult(handle, ctypes.byref(op), ctypes.byref(count), False):
                    raise BridgeError("Połączenie z AMC zostało przerwane. Sprawdź stan odtwarzania.")
            return count.value
        finally:
            k.CloseHandle(event)

    try:
        data = (json.dumps({"version": 1, "command": command}) + "\n").encode("utf-8")
        out = ctypes.create_string_buffer(data)
        if transfer("WriteFile", out, len(data)) != len(data):
            raise BridgeError("Niepełne polecenie AMC. Nie ponawiam go automatycznie.")
        result = bytearray()
        while len(result) < 16384:
            incoming = ctypes.create_string_buffer(min(4096, 16384 - len(result)))
            count = transfer("ReadFile", incoming, len(incoming))
            if not count:
                break
            result.extend(incoming.raw[:count])
            if b"\n" in result:
                return parse_reply(result.split(b"\n", 1)[0])
        raise BridgeError("Niepełna odpowiedź AMC. Sprawdź stan odtwarzania.")
    finally:
        k.CloseHandle(handle)
