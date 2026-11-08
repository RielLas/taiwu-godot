import ctypes

user32 = ctypes.windll.user32
h_desk = user32.OpenDesktopW("default", 0, False, 0x01FF)
if h_desk:
    user32.SetThreadDesktop(h_desk)

WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)

def callback(hwnd, extra):
    pid = ctypes.c_ulong()
    user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
    if pid.value in [14192, 7612, 9172]:
        length = user32.GetWindowTextLengthW(hwnd)
        buf = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buf, length + 1)
        title = buf.value
        visible = bool(user32.IsWindowVisible(hwnd))
        rect = (ctypes.c_long * 4)()
        user32.GetWindowRect(hwnd, rect)
        print(f"HWND {hwnd} (PID {pid.value}): visible={visible} title='{title}' rect=({rect[0]},{rect[1]},{rect[2]},{rect[3]})")
    return True

user32.EnumWindows(WNDENUMPROC(callback), 0)
