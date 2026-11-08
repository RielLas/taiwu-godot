import ctypes
import mss
from PIL import Image

user32 = ctypes.windll.user32
h_desk = user32.OpenDesktopW("default", 0, False, 0x01FF)
if h_desk:
    user32.SetThreadDesktop(h_desk)

hwnd = 1573988 # Godot editor
rect = (ctypes.c_long * 4)()
user32.GetWindowRect(hwnd, rect)
width = rect[2] - rect[0]
height = rect[3] - rect[1]

with mss.mss() as sct:
    img = sct.grab({"left": max(0, rect[0]), "top": max(0, rect[1]), "width": width, "height": height})
    Image.frombytes("RGB", img.size, img.bgra, "raw", "BGRX").save("D:/Wonderland Discord/Projects/Taiwu Path of the Gu/Taiwu_Godot/tools/captures/godot_editor.png")
print("Saved editor screenshot")
