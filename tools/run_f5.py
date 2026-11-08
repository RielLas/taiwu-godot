import ctypes
import time
import pydirectinput

user32 = ctypes.windll.user32
h_desk = user32.OpenDesktopW("default", 0, False, 0x01FF)
if h_desk:
    user32.SetThreadDesktop(h_desk)

SW_RESTORE = 9
SW_SHOW = 5

hwnd = 1573988 # Godot editor
user32.ShowWindow(hwnd, SW_RESTORE)
user32.SetForegroundWindow(hwnd)
time.sleep(1)

# Press F5 to run project in Godot
pydirectinput.press("f5")
print("Pressed F5 in Godot Editor")
