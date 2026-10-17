# OpenNDOF Fusion 360 add-in — SpaceMouse viewport bridge
#
# EXPERIMENTAL. Fusion 360's built-in SpaceMouse support requires the
# proprietary 3DxWare driver and never touches the TDxInput COM API, so a COM
# server cannot serve it. This add-in receives the connected device's motion
# and buttons from tools/SpaceMouseBridge over localhost UDP and drives the
# active viewport camera directly.
#
# Adapted from requies/Fusion360-Linux_Spacemouse (MIT), which proved this
# approach for Fusion under Wine. Protocol:
#   motion :  "m <dx> <dy> <dz> <drx> <dry> <drz> 0\n"   (integer deltas,
#             raw 3Dconnexion units ~±350 full deflection)
#   button :  "b <pressed|0> <button>\n"
#
# Install in Fusion: Tools -> Add-Ins -> Add -> select this folder.

import adsk.core
import math
import socket
import threading
import traceback


EVENT_ID = 'org.openndof.spacemouse.motion'
HOST = '127.0.0.1'
PORT = 39030

# Tune if the device feels too fast, too slow, or reversed.
PAN_SCALE = 0.000035
ZOOM_SCALE = 0.000075
ROT_SCALE = 0.000030
ROLL_SCALE = 0.000030

PAN_X_SIGN = 1.0
PAN_Y_SIGN = 1.0
ZOOM_SIGN = 1.0
PITCH_SIGN = 1.0
YAW_SIGN = 1.0
ROLL_SIGN = 1.0

MAX_AXIS_SUM = 1800
EPSILON = 1.0e-9

_app = None
_handler = None
_custom_event = None
_thread = None
_sock = None
_stop_event = threading.Event()
_lock = threading.Lock()
_pending_motion = [0, 0, 0, 0, 0, 0]
_pending_buttons = []
_event_queued = False
_handlers = []


def _log(message):
    try:
        adsk.core.Application.log('[OpenNDOF-SpaceMouse] ' + str(message))
    except Exception:
        pass


def _clamp(value, limit):
    return max(-limit, min(limit, value))


def _v_add(a, b):
    return [a[0] + b[0], a[1] + b[1], a[2] + b[2]]


def _v_sub(a, b):
    return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]


def _v_scale(a, scalar):
    return [a[0] * scalar, a[1] * scalar, a[2] * scalar]


def _v_dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _v_cross(a, b):
    return [
        a[1] * b[2] - a[2] * b[1],
        a[2] * b[0] - a[0] * b[2],
        a[0] * b[1] - a[1] * b[0],
    ]


def _v_length(a):
    return math.sqrt(_v_dot(a, a))


def _v_normalize(a, fallback=None):
    length = _v_length(a)
    if length < EPSILON:
        return fallback[:] if fallback else [0.0, 0.0, 1.0]
    return [a[0] / length, a[1] / length, a[2] / length]


def _rotate(vector, axis, angle):
    axis = _v_normalize(axis)
    cosine = math.cos(angle)
    sine = math.sin(angle)
    term1 = _v_scale(vector, cosine)
    term2 = _v_scale(_v_cross(axis, vector), sine)
    term3 = _v_scale(axis, _v_dot(axis, vector) * (1.0 - cosine))
    return _v_add(_v_add(term1, term2), term3)


def _queue_fusion_event():
    global _event_queued

    should_fire = False
    with _lock:
        if not _event_queued:
            _event_queued = True
            should_fire = True

    if should_fire and _app:
        try:
            _app.fireCustomEvent(EVENT_ID, '')
        except Exception:
            _log(traceback.format_exc())


def _parse_packet(packet):
    text = packet.decode('ascii', errors='ignore').strip()
    if not text:
        return

    parts = text.split()
    global _pending_motion

    if parts[0] == 'm' and len(parts) >= 7:
        try:
            values = [int(parts[i]) for i in range(1, 7)]
        except ValueError:
            return

        with _lock:
            for i, value in enumerate(values):
                _pending_motion[i] = _clamp(_pending_motion[i] + value, MAX_AXIS_SUM)

        _queue_fusion_event()
    elif parts[0] == 'b' and len(parts) >= 3:
        try:
            pressed = int(parts[1])
            button = int(parts[2])
        except ValueError:
            return

        if pressed:
            with _lock:
                _pending_buttons.append(button)
            _queue_fusion_event()


def _udp_loop():
    global _sock

    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        sock.bind((HOST, PORT))
        sock.settimeout(0.25)
        _sock = sock
        _log('listening on {}:{}'.format(HOST, PORT))

        while not _stop_event.is_set():
            try:
                packet, _address = sock.recvfrom(256)
            except socket.timeout:
                continue
            except OSError:
                break

            _parse_packet(packet)
    except Exception:
        _log(traceback.format_exc())
    finally:
        try:
            if _sock:
                _sock.close()
        except Exception:
            pass
        _sock = None


def _drain_pending():
    global _pending_motion, _pending_buttons

    with _lock:
        motion = _pending_motion[:]
        buttons = _pending_buttons[:]
        _pending_motion = [0, 0, 0, 0, 0, 0]
        _pending_buttons = []

    return motion, buttons


def _finish_event_or_refire():
    global _event_queued

    should_fire = False
    with _lock:
        has_motion = any(_pending_motion)
        has_buttons = bool(_pending_buttons)
        if has_motion or has_buttons:
            should_fire = True
        else:
            _event_queued = False

    if should_fire and _app:
        try:
            _app.fireCustomEvent(EVENT_ID, '')
        except Exception:
            _log(traceback.format_exc())


def _apply_buttons(viewport, buttons):
    for button in buttons:
        if button == 0:
            viewport.fit()
        elif button == 1:
            viewport.goHome(False)


def _apply_motion(viewport, motion):
    x, y, z, rx, ry, rz = [float(_clamp(value, MAX_AXIS_SUM)) for value in motion]
    if not any([x, y, z, rx, ry, rz]):
        return

    camera = viewport.camera
    eye = list(camera.eye.asArray())
    target = list(camera.target.asArray())
    up = _v_normalize(list(camera.upVector.asArray()), [0.0, 1.0, 0.0])

    view = _v_sub(eye, target)
    distance = max(_v_length(view), 0.01)
    forward = _v_normalize(_v_scale(view, -1.0), [0.0, 0.0, -1.0])
    right = _v_normalize(_v_cross(forward, up), [1.0, 0.0, 0.0])
    up = _v_normalize(_v_cross(right, forward), [0.0, 1.0, 0.0])

    pan = _v_add(
        _v_scale(right, x * PAN_X_SIGN * PAN_SCALE * distance),
        _v_scale(up, y * PAN_Y_SIGN * PAN_SCALE * distance),
    )
    eye = _v_add(eye, pan)
    target = _v_add(target, pan)
    view = _v_sub(eye, target)

    if abs(z) > 0.0:
        zoom = math.exp(-z * ZOOM_SIGN * ZOOM_SCALE)
        # Orthographic cameras (cameraType == 0) zoom by changing viewExtents,
        # not eye distance. Other modes zoom by moving the camera position.
        if camera.cameraType == 0 and hasattr(camera, 'viewExtents'):
            camera.viewExtents = max(camera.viewExtents * zoom, 0.001)
        else:
            distance = max(_v_length(view) * zoom, 0.01)
            view = _v_scale(_v_normalize(view, [0.0, 0.0, 1.0]), distance)

    if abs(ry) > 0.0:
        view = _rotate(view, up, ry * YAW_SIGN * ROT_SCALE)
        right = _v_normalize(_v_cross(_v_normalize(_v_scale(view, -1.0)), up), right)

    if abs(rx) > 0.0:
        view = _rotate(view, right, rx * PITCH_SIGN * ROT_SCALE)
        up = _rotate(up, right, rx * PITCH_SIGN * ROT_SCALE)

    forward = _v_normalize(_v_scale(view, -1.0), forward)
    if abs(rz) > 0.0:
        up = _rotate(up, forward, rz * ROLL_SIGN * ROLL_SCALE)

    eye = _v_add(target, view)
    up = _v_normalize(up, [0.0, 1.0, 0.0])

    camera.eye = adsk.core.Point3D.create(eye[0], eye[1], eye[2])
    camera.target = adsk.core.Point3D.create(target[0], target[1], target[2])
    camera.upVector = adsk.core.Vector3D.create(up[0], up[1], up[2])
    camera.isSmoothTransition = False
    viewport.camera = camera
    viewport.refresh()


class SpaceMouseEventHandler(adsk.core.CustomEventHandler):
    def __init__(self):
        super().__init__()

    def notify(self, event_args):
        try:
            motion, buttons = _drain_pending()
            viewport = _app.activeViewport if _app else None
            if viewport:
                _apply_buttons(viewport, buttons)
                _apply_motion(viewport, motion)
        except Exception:
            _log(traceback.format_exc())
        finally:
            _finish_event_or_refire()


def run(context):
    global _app, _handler, _custom_event, _thread

    try:
        _app = adsk.core.Application.get()
        try:
            _app.unregisterCustomEvent(EVENT_ID)
        except Exception:
            pass
        _custom_event = _app.registerCustomEvent(EVENT_ID)
        if not _custom_event:
            _log('failed to register custom event')
            return
        _handler = SpaceMouseEventHandler()
        _custom_event.add(_handler)
        _handlers.append(_handler)

        _stop_event.clear()
        _thread = threading.Thread(target=_udp_loop, name='OpenNDOFSpaceMouseUdp', daemon=True)
        _thread.start()

        adsk.autoTerminate(False)
    except Exception:
        _log(traceback.format_exc())


def stop(context):
    global _handler, _custom_event, _thread, _sock

    try:
        _stop_event.set()
        if _sock:
            try:
                _sock.close()
            except Exception:
                pass
        if _thread:
            _thread.join(1.0)
            _thread = None
        if _custom_event and _handler:
            _custom_event.remove(_handler)
        if _app:
            _app.unregisterCustomEvent(EVENT_ID)
        _handlers.clear()
        _handler = None
        _custom_event = None
    except Exception:
        _log(traceback.format_exc())