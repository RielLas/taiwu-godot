"""
Taiwu Path of the Gu - External Playtest & Vision Bot Harness
Provides programmatic screen capture and DirectInput simulation to test the Godot game.
"""

import sys
import os
import time
import argparse
import ctypes

def attach_to_default_desktop():
    """Attaches current thread to user interactive desktop if in sandbox/service session."""
    user32 = ctypes.windll.user32
    h_desk = user32.OpenDesktopW("default", 0, False, 0x01FF) # DESKTOP_ALL
    if h_desk:
        user32.SetThreadDesktop(h_desk)

# Attach desktop before importing windowing modules
attach_to_default_desktop()

import pygetwindow as gw
import mss
import pydirectinput
from PIL import Image

pydirectinput.FAILSAFE = True
pydirectinput.PAUSE = 0.05

def get_godot_window(title_hint="Taiwu"):
    attach_to_default_desktop()
    windows = [w for w in gw.getAllWindows() if w.title and w.visible]
    
    # Priority 1: Running game instance (not the Godot Editor)
    game_windows = [
        w for w in windows 
        if title_hint.lower() in w.title.lower() and "godot engine" not in w.title.lower()
    ]
    if game_windows:
        return game_windows[0]
    
    # Priority 2: Any Taiwu window
    taiwu_windows = [w for w in windows if title_hint.lower() in w.title.lower()]
    if taiwu_windows:
        return taiwu_windows[0]

    # Priority 3: Fallback to Godot
    fallback = [w for w in windows if "godot" in w.title.lower()]
    return fallback[0] if fallback else None

def activate_window(win):
    try:
        if win.isMinimized:
            win.restore()
        win.activate()
        time.sleep(0.3)
    except Exception as e:
        print(f"[Warn] Window activation: {e}")

def capture_screen(output_path="captures/current_screen.png", activate=True):
    attach_to_default_desktop()
    win = get_godot_window()
    if not win:
        print("ERROR: Godot game window not found!")
        return False
    
    if activate:
        activate_window(win)

    rect = {
        "left": max(0, win.left),
        "top": max(0, win.top),
        "width": win.width,
        "height": win.height
    }

    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    with mss.mss() as sct:
        sct_img = sct.grab(rect)
        img = Image.frombytes("RGB", sct_img.size, sct_img.bgra, "raw", "BGRX")
        img.save(output_path)
    
    print(f"CAPTURED: {os.path.abspath(output_path)} ({win.width}x{win.height}) Window: '{win.title}'")
    return True

def click_normalized(norm_x, norm_y, button="left", double=False):
    attach_to_default_desktop()
    win = get_godot_window()
    if not win:
        print("ERROR: Godot game window not found!")
        return False
    
    activate_window(win)
    abs_x = int(win.left + norm_x * win.width)
    abs_y = int(win.top + norm_y * win.height)

    pydirectinput.moveTo(abs_x, abs_y)
    time.sleep(0.1)
    if double:
        pydirectinput.doubleClick(abs_x, abs_y, button=button)
    else:
        pydirectinput.click(abs_x, abs_y, button=button)
    
    print(f"CLICKED: ({abs_x}, {abs_y}) [norm: {norm_x:.3f}, {norm_y:.3f}] button={button} in '{win.title}'")
    return True

def drag_normalized(x1, y1, x2, y2, duration=0.5):
    attach_to_default_desktop()
    win = get_godot_window()
    if not win:
        print("ERROR: Godot game window not found!")
        return False
    
    activate_window(win)
    start_x = int(win.left + x1 * win.width)
    start_y = int(win.top + y1 * win.height)
    end_x = int(win.left + x2 * win.width)
    end_y = int(win.top + y2 * win.height)

    pydirectinput.moveTo(start_x, start_y)
    time.sleep(0.05)
    pydirectinput.mouseDown()
    time.sleep(duration / 2)
    pydirectinput.moveTo(end_x, end_y)
    time.sleep(duration / 2)
    pydirectinput.mouseUp()
    print(f"DRAGGED: ({start_x}, {start_y}) -> ({end_x}, {end_y})")
    return True

def press_key(key, duration=0.1):
    attach_to_default_desktop()
    win = get_godot_window()
    if not win:
        print("ERROR: Godot game window not found!")
        return False
    
    activate_window(win)
    pydirectinput.keyDown(key)
    time.sleep(duration)
    pydirectinput.keyUp(key)
    print(f"PRESSED KEY: '{key}' in '{win.title}'")
    return True

def list_windows():
    attach_to_default_desktop()
    wins = gw.getAllWindows()
    print("Open Windows:")
    for w in wins:
        if w.title and w.visible:
            print(f" - [{w.title}] ({w.width}x{w.height}) at ({w.left},{w.top})")

def main():
    parser = argparse.ArgumentParser(description="Taiwu Godot Playtest Vision Harness")
    subparsers = parser.add_subparsers(dest="command")

    # capture
    cap_parser = subparsers.add_parser("capture")
    cap_parser.add_argument("--out", "-o", default="captures/current_screen.png", help="Output path")

    # click
    click_parser = subparsers.add_parser("click")
    click_parser.add_argument("x", type=float, help="Normalized X (0.0 to 1.0)")
    click_parser.add_argument("y", type=float, help="Normalized Y (0.0 to 1.0)")
    click_parser.add_argument("--button", "-b", default="left", choices=["left", "right", "middle"])
    click_parser.add_argument("--double", "-d", action="store_true")

    # drag
    drag_parser = subparsers.add_parser("drag")
    drag_parser.add_argument("x1", type=float)
    drag_parser.add_argument("y1", type=float)
    drag_parser.add_argument("x2", type=float)
    drag_parser.add_argument("y2", type=float)

    # press
    press_parser = subparsers.add_parser("press")
    press_parser.add_argument("key", type=str, help="Key to press (e.g. b, c, f, tab, esc, enter)")

    # windows
    subparsers.add_parser("list")

    args = parser.parse_args()

    if args.command == "capture":
        capture_screen(args.out)
    elif args.command == "click":
        click_normalized(args.x, args.y, button=args.button, double=args.double)
    elif args.command == "drag":
        drag_normalized(args.x1, args.y1, args.x2, args.y2)
    elif args.command == "press":
        press_key(args.key)
    elif args.command == "list":
        list_windows()
    else:
        parser.print_help()

if __name__ == "__main__":
    main()
