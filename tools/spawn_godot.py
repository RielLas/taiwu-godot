import ctypes
from ctypes import wintypes
import subprocess

def test_launch():
    godot_exe = r"C:\Users\rifas\AppData\Local\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
    proj = r"D:\Wonderland Discord\Projects\Taiwu Path of the Gu\Taiwu_Godot"
    
    # We can pass desktop parameter or run via subprocess with CREATE_NEW_CONSOLE
    proc = subprocess.Popen(
        [godot_exe, "--path", proj, "--verbose"],
        cwd=proj,
        creationflags=subprocess.CREATE_NEW_CONSOLE
    )
    print(f"Spawned Godot with PID {proc.pid}")

if __name__ == "__main__":
    test_launch()
