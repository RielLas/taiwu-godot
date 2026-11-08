import ctypes

user32 = ctypes.windll.user32

def enum_desktop(desk_name):
    print(f"=== Desktop: {desk_name} ===")
    if desk_name:
        h_desk = user32.OpenDesktopW(desk_name, 0, False, 0x01FF)
        if not h_desk:
            print("Failed to open desktop", desk_name)
            return
        user32.SetThreadDesktop(h_desk)
    
    WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)
    def callback(hwnd, extra):
        length = user32.GetWindowTextLengthW(hwnd)
        buf = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buf, length + 1)
        title = buf.value
        visible = bool(user32.IsWindowVisible(hwnd))
        if title.strip():
            rect = (ctypes.c_long * 4)()
            user32.GetWindowRect(hwnd, rect)
            print(f"  [{hwnd}] visible={visible} title='{title}' rect=({rect[0]},{rect[1]},{rect[2]},{rect[3]})")
        return True
    user32.EnumWindows(WNDENUMPROC(callback), 0)

enum_desktop(None) # current thread desktop
enum_desktop("default")
