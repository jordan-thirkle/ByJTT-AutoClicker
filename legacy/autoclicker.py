import ctypes
import json
import os
import random
import sys
import threading
import time
import tkinter as tk
import webbrowser
from ctypes import wintypes
from pathlib import Path
from tkinter import messagebox, ttk

APP_NAME = "ByJTT AutoClicker"
APP_VERSION = "3.0.0"
APP_AUTHOR = "ByJTT"
APP_DESCRIPTION = "A simple, accessible Windows auto-clicker."
REPO_URL = "https://github.com/jordan-thirkle/ByJTT-AutoClicker"

APP_BG = "#f3f6fb"
CARD_BG = "#ffffff"
BORDER = "#cfd8e3"
TEXT_PRIMARY = "#111827"
TEXT_MUTED = "#4b5563"
ACCENT = "#0b5fff"
SUCCESS = "#0f7b3e"
SUCCESS_BG = "#dcfce7"
DANGER = "#b42318"
DANGER_BG = "#fee4e2"
WARNING = "#9a3412"
WARNING_BG = "#ffedd5"

MOUSEEVENTF_LEFTDOWN = 0x0002
MOUSEEVENTF_LEFTUP = 0x0004
MOUSEEVENTF_RIGHTDOWN = 0x0008
MOUSEEVENTF_RIGHTUP = 0x0010
MOUSEEVENTF_MIDDLEDOWN = 0x0020
MOUSEEVENTF_MIDDLEUP = 0x0040

BUTTON_FLAGS = {
    "Left": (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP),
    "Right": (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
    "Middle": (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
}

HOTKEY_OPTIONS = {f"F{i}": 0x6F + i for i in range(1, 13)}

ULONG_PTR = ctypes.c_ulonglong if ctypes.sizeof(ctypes.c_void_p) == 8 else ctypes.c_ulong


def get_app_data_dir() -> Path:
    base = os.environ.get("APPDATA") or str(Path.home() / "AppData" / "Roaming")
    path = Path(base) / "ByJTT" / "AutoClicker"
    path.mkdir(parents=True, exist_ok=True)
    return path


def get_settings_path() -> Path:
    return get_app_data_dir() / "settings.json"


def get_legacy_settings_path() -> Path:
    return Path(__file__).resolve().parent / "settings.json"


class MOUSEINPUT(ctypes.Structure):
    _fields_ = (
        ("dx", wintypes.LONG),
        ("dy", wintypes.LONG),
        ("mouseData", wintypes.DWORD),
        ("dwFlags", wintypes.DWORD),
        ("time", wintypes.DWORD),
        ("dwExtraInfo", ULONG_PTR),
    )


class INPUT(ctypes.Structure):
    class _INPUT(ctypes.Union):
        _fields_ = (("mi", MOUSEINPUT),)

    _anonymous_ = ("_input",)
    _fields_ = (("type", wintypes.DWORD), ("_input", _INPUT))


INPUT_MOUSE = 0


def enable_dpi_awareness() -> None:
    try:
        ctypes.windll.shcore.SetProcessDpiAwareness(2)
        return
    except Exception:
        pass
    try:
        ctypes.windll.shcore.SetProcessDpiAwareness(1)
        return
    except Exception:
        pass
    try:
        ctypes.windll.user32.SetProcessDPIAware()
    except Exception:
        pass


enable_dpi_awareness()
user32 = ctypes.windll.user32
try:
    user32.SendInput.argtypes = (wintypes.UINT, ctypes.POINTER(INPUT), ctypes.c_int)
    user32.SendInput.restype = wintypes.UINT
except Exception:
    pass


class POINT(ctypes.Structure):
    _fields_ = [("x", ctypes.c_long), ("y", ctypes.c_long)]


class ToolTip:
    def __init__(self, widget: tk.Widget, text: str) -> None:
        self.widget = widget
        self.text = text
        self.tip_window: tk.Toplevel | None = None
        widget.bind("<Enter>", self.show, add="+")
        widget.bind("<Leave>", self.hide, add="+")
        widget.bind("<ButtonPress>", self.hide, add="+")
        widget.bind("<FocusOut>", self.hide, add="+")

    def show(self, _event=None) -> None:
        if self.tip_window or not self.text:
            return
        try:
            x = self.widget.winfo_rootx() + 12
            y = self.widget.winfo_rooty() + self.widget.winfo_height() + 8
        except Exception:
            return
        tw = tk.Toplevel(self.widget)
        tw.wm_overrideredirect(True)
        try:
            tw.wm_attributes("-topmost", True)
        except Exception:
            pass
        tw.wm_geometry(f"+{x}+{y}")
        label = tk.Label(
            tw,
            text=self.text,
            justify="left",
            background="#111827",
            foreground="#f9fafb",
            relief="solid",
            borderwidth=1,
            font=("Segoe UI", 9),
            padx=8,
            pady=5,
            wraplength=360,
        )
        label.pack()
        self.tip_window = tw

    def hide(self, _event=None) -> None:
        if self.tip_window:
            try:
                self.tip_window.destroy()
            except Exception:
                pass
            self.tip_window = None


DEFAULT_SETTINGS = {
    "interval_ms": "100",
    "jitter_ms": "0",
    "button": "Left",
    "click_type": "Single",
    "repeat_mode": "until_stopped",
    "count": "100",
    "delay_sec": "0",
    "position_mode": "cursor",
    "x": "0",
    "y": "0",
    "hotkey": "F6",
    "always_on_top": False,
    "failsafe": True,
    "geometry": "",
}


class ByJTTAutoClicker:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title(f"{APP_NAME} - {APP_DESCRIPTION}")
        self.root.minsize(720, 620)
        self.root.configure(bg=APP_BG)

        self.running = False
        self.countdown_active = False
        self.pick_active = False
        self.clicks_done = 0
        self.target_clicks = 0
        self.session_started_at: float | None = None
        self._last_elapsed = 0.0
        self._stop_event = threading.Event()
        self._hotkey_running = True
        self._hotkey_vk = HOTKEY_OPTIONS["F6"]
        self._active_settings: dict | None = None
        self._settings_lock = threading.Lock()
        self._last_start_signature: tuple | None = None
        self._start_confirmed = False

        self.interval_var = tk.StringVar(value=DEFAULT_SETTINGS["interval_ms"])
        self.jitter_var = tk.StringVar(value=DEFAULT_SETTINGS["jitter_ms"])
        self.button_var = tk.StringVar(value=DEFAULT_SETTINGS["button"])
        self.click_type_var = tk.StringVar(value=DEFAULT_SETTINGS["click_type"])
        self.repeat_var = tk.StringVar(value=DEFAULT_SETTINGS["repeat_mode"])
        self.count_var = tk.StringVar(value=DEFAULT_SETTINGS["count"])
        self.delay_var = tk.StringVar(value=DEFAULT_SETTINGS["delay_sec"])
        self.position_mode_var = tk.StringVar(value=DEFAULT_SETTINGS["position_mode"])
        self.x_var = tk.StringVar(value=DEFAULT_SETTINGS["x"])
        self.y_var = tk.StringVar(value=DEFAULT_SETTINGS["y"])
        self.hotkey_var = tk.StringVar(value=DEFAULT_SETTINGS["hotkey"])
        self.always_on_top_var = tk.BooleanVar(value=DEFAULT_SETTINGS["always_on_top"])
        self.failsafe_var = tk.BooleanVar(value=DEFAULT_SETTINGS["failsafe"])

        self._configure_style()
        self._build_menu()
        self._build_ui()
        self._bind_shortcuts()
        self._load_settings()
        self._apply_hotkey_change(initial=True)
        self._apply_always_on_top()
        self._update_repeat_state()
        self._update_position_state()
        self._update_rate_info()
        self._set_status("stopped", "Ready. Configure your settings, then press Start or use the global hotkey.")

        self.root.protocol("WM_DELETE_WINDOW", self.on_close)
        self.interval_var.trace_add("write", lambda *_: self._update_rate_info())
        self.jitter_var.trace_add("write", lambda *_: self._update_rate_info())

        threading.Thread(target=self._hotkey_listener, daemon=True).start()
        self._tick_ui()

    def _configure_style(self) -> None:
        style = ttk.Style(self.root)
        try:
            style.theme_use("clam")
        except Exception:
            pass
        style.configure("App.TFrame", background=APP_BG)
        style.configure("Card.TLabelframe", background=CARD_BG, bordercolor=BORDER, relief="solid", borderwidth=1)
        style.configure("Card.TLabelframe.Label", background=CARD_BG, foreground=TEXT_PRIMARY, font=("Segoe UI Semibold", 10))
        style.configure("Card.TFrame", background=CARD_BG)
        style.configure("TLabel", background=CARD_BG, foreground=TEXT_PRIMARY, font=("Segoe UI", 9))
        style.configure("App.TLabel", background=APP_BG, foreground=TEXT_PRIMARY, font=("Segoe UI", 9))
        style.configure("Muted.TLabel", background=CARD_BG, foreground=TEXT_MUTED, font=("Segoe UI", 9))
        style.configure("MutedApp.TLabel", background=APP_BG, foreground=TEXT_MUTED, font=("Segoe UI", 9))
        style.configure("Title.TLabel", background=APP_BG, foreground=TEXT_PRIMARY, font=("Segoe UI Semibold", 20))
        style.configure("Subtitle.TLabel", background=APP_BG, foreground=TEXT_MUTED, font=("Segoe UI", 9))
        style.configure("Value.TLabel", background=CARD_BG, foreground=TEXT_PRIMARY, font=("Segoe UI Semibold", 10))
        style.configure("TButton", font=("Segoe UI", 9), padding=(10, 6))
        style.configure("Primary.TButton", font=("Segoe UI Semibold", 10), padding=(12, 9))
        style.configure("Secondary.TButton", font=("Segoe UI Semibold", 10), padding=(12, 9))
        style.configure("TRadiobutton", background=CARD_BG, foreground=TEXT_PRIMARY, font=("Segoe UI", 9))
        style.configure("TCheckbutton", background=CARD_BG, foreground=TEXT_PRIMARY, font=("Segoe UI", 9))
        style.configure("TEntry", padding=4)
        style.configure("TCombobox", padding=4)
        style.configure("Accent.Horizontal.TProgressbar", troughcolor="#e5e7eb", background=ACCENT, bordercolor="#e5e7eb")

    def _build_menu(self) -> None:
        menubar = tk.Menu(self.root)

        file_menu = tk.Menu(menubar, tearoff=False)
        file_menu.add_command(label="Save settings", accelerator="Ctrl+S", command=lambda: self._save_settings(silent=False, notify=True))
        file_menu.add_command(label="Reset defaults", command=self.reset_defaults)
        file_menu.add_separator()
        file_menu.add_command(label="Exit", accelerator="Alt+F4", command=self.on_close)
        menubar.add_cascade(label="File", menu=file_menu)

        help_menu = tk.Menu(menubar, tearoff=False)
        help_menu.add_command(label="Keyboard shortcuts", accelerator="F1", command=self.show_shortcuts)
        help_menu.add_command(label="Responsible use", command=self.show_responsible_use)
        help_menu.add_separator()
        help_menu.add_command(label="About", command=self.show_about)
        menubar.add_cascade(label="Help", menu=help_menu)

        self.root.config(menu=menubar)

    def _build_ui(self) -> None:
        outer = ttk.Frame(self.root, style="App.TFrame")
        outer.grid(row=0, column=0, sticky="nsew")
        self.root.rowconfigure(0, weight=1)
        self.root.columnconfigure(0, weight=1)
        outer.rowconfigure(1, weight=1)
        outer.columnconfigure(0, weight=1)

        self._build_header(outer)

        canvas_wrap = ttk.Frame(outer, style="App.TFrame")
        canvas_wrap.grid(row=1, column=0, sticky="nsew", padx=14, pady=(0, 10))
        canvas_wrap.rowconfigure(0, weight=1)
        canvas_wrap.columnconfigure(0, weight=1)

        self.canvas = tk.Canvas(canvas_wrap, bg=APP_BG, highlightthickness=0, bd=0)
        self.canvas.grid(row=0, column=0, sticky="nsew")
        vsb = ttk.Scrollbar(canvas_wrap, orient="vertical", command=self.canvas.yview)
        vsb.grid(row=0, column=1, sticky="ns")
        self.canvas.configure(yscrollcommand=vsb.set)

        self.content = ttk.Frame(self.canvas, style="App.TFrame")
        self.content_window = self.canvas.create_window((0, 0), window=self.content, anchor="nw")
        self.content.bind("<Configure>", lambda _e: self.canvas.configure(scrollregion=self.canvas.bbox("all")))
        self.canvas.bind("<Configure>", lambda e: self.canvas.itemconfigure(self.content_window, width=e.width))
        self.content.columnconfigure(0, weight=1)

        self._build_cards(self.content)
        self._build_status(self.content)
        self._build_actions(self.content)
        self._build_footer(self.content)

        self.root.bind_all("<MouseWheel>", self._on_mousewheel, add="+")

    def _on_mousewheel(self, event) -> None:
        try:
            if self.canvas.winfo_exists():
                self.canvas.yview_scroll(int(-1 * (event.delta / 120)), "units")
        except Exception:
            pass

    def _build_header(self, parent: ttk.Frame) -> None:
        header = ttk.Frame(parent, style="App.TFrame", padding=(14, 14, 14, 8))
        header.grid(row=0, column=0, sticky="ew")
        header.columnconfigure(0, weight=1)

        title_block = ttk.Frame(header, style="App.TFrame")
        title_block.grid(row=0, column=0, sticky="w")
        ttk.Label(title_block, text=APP_NAME, style="Title.TLabel").grid(row=0, column=0, sticky="w")
        ttk.Label(
            title_block,
            text="Configurable, keyboard-friendly auto-clicking for Windows.",
            style="Subtitle.TLabel",
        ).grid(row=1, column=0, sticky="w", pady=(2, 0))

        status_block = ttk.Frame(header, style="App.TFrame")
        status_block.grid(row=0, column=1, sticky="e", padx=(12, 0))
        self.status_badge = tk.Label(
            status_block,
            text="STOPPED",
            bg=DANGER_BG,
            fg=DANGER,
            font=("Segoe UI Semibold", 10),
            padx=14,
            pady=7,
        )
        self.status_badge.grid(row=0, column=0, sticky="e")
        ttk.Label(status_block, text=f"Version {APP_VERSION}", style="MutedApp.TLabel").grid(row=1, column=0, sticky="e", pady=(4, 0))

    def _build_cards(self, parent: ttk.Frame) -> None:
        body = ttk.Frame(parent, style="App.TFrame")
        body.grid(row=0, column=0, sticky="ew")
        body.columnconfigure(0, weight=1)
        body.columnconfigure(1, weight=1)

        click_card = ttk.LabelFrame(body, text="Click settings", padding=12, style="Card.TLabelframe")
        click_card.grid(row=0, column=0, sticky="nsew", padx=(0, 6), pady=(0, 8))
        click_card.columnconfigure(2, weight=1)

        ttk.Label(click_card, text="Interval (milliseconds):").grid(row=0, column=0, sticky="w", pady=4)
        self.interval_entry = ttk.Entry(click_card, textvariable=self.interval_var, width=12)
        self.interval_entry.grid(row=0, column=1, sticky="w", pady=4)
        ToolTip(self.interval_entry, "Base time between clicks in milliseconds. 100 ms is about 10 clicks per second.")
        self.rate_label = ttk.Label(click_card, text="~10.0 CPS", style="Muted.TLabel")
        self.rate_label.grid(row=0, column=2, sticky="w", padx=(8, 0))

        ttk.Label(click_card, text="Random jitter (± ms):").grid(row=1, column=0, sticky="w", pady=4)
        self.jitter_entry = ttk.Entry(click_card, textvariable=self.jitter_var, width=12)
        self.jitter_entry.grid(row=1, column=1, sticky="w", pady=4)
        ToolTip(self.jitter_entry, "Optional random variation added to each interval, from 0 up to this value.")

        ttk.Label(click_card, text="Mouse button:").grid(row=2, column=0, sticky="w", pady=4)
        self.button_combo = ttk.Combobox(
            click_card, textvariable=self.button_var, values=["Left", "Right", "Middle"], state="readonly", width=12
        )
        self.button_combo.grid(row=2, column=1, sticky="w", pady=4)

        ttk.Label(click_card, text="Click type:").grid(row=3, column=0, sticky="w", pady=4)
        self.click_type_combo = ttk.Combobox(
            click_card, textvariable=self.click_type_var, values=["Single", "Double"], state="readonly", width=12
        )
        self.click_type_combo.grid(row=3, column=1, sticky="w", pady=4)

        repeat_card = ttk.LabelFrame(body, text="Repeat and timing", padding=12, style="Card.TLabelframe")
        repeat_card.grid(row=0, column=1, sticky="nsew", padx=(6, 0), pady=(0, 8))
        repeat_card.columnconfigure(2, weight=1)

        ttk.Radiobutton(
            repeat_card,
            text="Repeat until stopped",
            variable=self.repeat_var,
            value="until_stopped",
            command=self._update_repeat_state,
        ).grid(row=0, column=0, columnspan=3, sticky="w", pady=2)
        ttk.Radiobutton(
            repeat_card,
            text="Repeat a fixed number of times",
            variable=self.repeat_var,
            value="count",
            command=self._update_repeat_state,
        ).grid(row=1, column=0, columnspan=3, sticky="w", pady=2)

        ttk.Label(repeat_card, text="Number of clicks:").grid(row=2, column=0, sticky="w", pady=4)
        self.count_entry = ttk.Entry(repeat_card, textvariable=self.count_var, width=12)
        self.count_entry.grid(row=2, column=1, sticky="w", pady=4)

        ttk.Label(repeat_card, text="Start delay (seconds):").grid(row=3, column=0, sticky="w", pady=4)
        self.delay_entry = ttk.Entry(repeat_card, textvariable=self.delay_var, width=12)
        self.delay_entry.grid(row=3, column=1, sticky="w", pady=4)
        ToolTip(self.delay_entry, "Countdown before clicking starts (0 to 60 seconds).")

        ttk.Label(
            repeat_card,
            text="Tip: Use the global hotkey or Stop button to end continuous clicking at any time.",
            style="Muted.TLabel",
            wraplength=280,
            justify="left",
        ).grid(row=4, column=0, columnspan=3, sticky="w", pady=(8, 0))

        loc_card = ttk.LabelFrame(body, text="Click location", padding=12, style="Card.TLabelframe")
        loc_card.grid(row=1, column=0, sticky="nsew", padx=(0, 6), pady=(0, 8))
        loc_card.columnconfigure(4, weight=1)

        ttk.Radiobutton(
            loc_card,
            text="Follow current mouse cursor",
            variable=self.position_mode_var,
            value="cursor",
            command=self._update_position_state,
        ).grid(row=0, column=0, columnspan=5, sticky="w", pady=2)
        ttk.Radiobutton(
            loc_card,
            text="Use a fixed screen position",
            variable=self.position_mode_var,
            value="fixed",
            command=self._update_position_state,
        ).grid(row=1, column=0, columnspan=5, sticky="w", pady=2)

        ttk.Label(loc_card, text="X:").grid(row=2, column=0, sticky="w", pady=4)
        self.x_entry = ttk.Entry(loc_card, textvariable=self.x_var, width=10)
        self.x_entry.grid(row=2, column=1, sticky="w", padx=(2, 10), pady=4)
        ttk.Label(loc_card, text="Y:").grid(row=2, column=2, sticky="w", pady=4)
        self.y_entry = ttk.Entry(loc_card, textvariable=self.y_var, width=10)
        self.y_entry.grid(row=2, column=3, sticky="w", padx=(2, 0), pady=4)

        loc_actions = ttk.Frame(loc_card, style="Card.TFrame")
        loc_actions.grid(row=3, column=0, columnspan=5, sticky="w", pady=(6, 2))
        self.pick_button = ttk.Button(loc_actions, text="Pick location (3s)", command=self.start_pick_location)
        self.pick_button.grid(row=0, column=0, sticky="w")
        self.use_current_button = ttk.Button(loc_actions, text="Use current cursor", command=self.use_current_position)
        self.use_current_button.grid(row=0, column=1, sticky="w", padx=(8, 0))
        ToolTip(self.pick_button, "Counts down 3 seconds, then captures the mouse cursor position.")
        ToolTip(self.use_current_button, "Copies the current mouse cursor position into X and Y immediately.")
        self.cursor_live_label = ttk.Label(loc_card, text="Cursor position: -, -", style="Muted.TLabel")
        self.cursor_live_label.grid(row=4, column=0, columnspan=5, sticky="w", pady=(6, 0))

        opts_card = ttk.LabelFrame(body, text="Controls and safety", padding=12, style="Card.TLabelframe")
        opts_card.grid(row=1, column=1, sticky="nsew", padx=(6, 0), pady=(0, 8))
        opts_card.columnconfigure(1, weight=1)

        ttk.Label(opts_card, text="Global hotkey:").grid(row=0, column=0, sticky="w", pady=4)
        self.hotkey_combo = ttk.Combobox(
            opts_card, textvariable=self.hotkey_var, values=list(HOTKEY_OPTIONS.keys()), state="readonly", width=10
        )
        self.hotkey_combo.grid(row=0, column=1, sticky="w", pady=4)
        self.hotkey_combo.bind("<<ComboboxSelected>>", lambda _e: self._apply_hotkey_change())
        ToolTip(self.hotkey_combo, "Press this key anywhere to start or stop clicking.")

        ttk.Checkbutton(
            opts_card,
            text="Keep window always on top",
            variable=self.always_on_top_var,
            command=self._apply_always_on_top,
        ).grid(row=1, column=0, columnspan=2, sticky="w", pady=(8, 2))
        ttk.Checkbutton(
            opts_card,
            text="Enable failsafe: move mouse to top-left corner (0,0) to stop",
            variable=self.failsafe_var,
        ).grid(row=2, column=0, columnspan=2, sticky="w", pady=2)

        ttk.Label(
            opts_card,
            text="Esc stops when this window is focused. Use only where automated clicking is permitted.",
            style="Muted.TLabel",
            wraplength=280,
            justify="left",
        ).grid(row=3, column=0, columnspan=2, sticky="w", pady=(10, 0))

    def _build_status(self, parent: ttk.Frame) -> None:
        status_card = ttk.LabelFrame(parent, text="Status", padding=12, style="Card.TLabelframe")
        status_card.grid(row=1, column=0, sticky="ew", pady=(0, 8))
        for c in range(4):
            status_card.columnconfigure(c, weight=1)

        self._add_stat(status_card, 0, 0, "State", "Stopped", "state_value")
        self._add_stat(status_card, 0, 1, "Hotkey", "F6", "hotkey_value")
        self._add_stat(status_card, 0, 2, "Clicks completed", "0", "clicks_value")
        self._add_stat(status_card, 0, 3, "Elapsed time", "0.0s", "elapsed_value")

        self.progress = ttk.Progressbar(status_card, mode="determinate", style="Accent.Horizontal.TProgressbar")
        self.progress.grid(row=1, column=0, columnspan=4, sticky="ew", pady=(10, 6))
        self.progress.configure(value=0, maximum=100)

        self.detail_label = ttk.Label(status_card, text="Ready.", style="Muted.TLabel", wraplength=660, justify="left")
        self.detail_label.grid(row=2, column=0, columnspan=4, sticky="w")

    def _add_stat(self, parent: ttk.Frame, row: int, col: int, title: str, value: str, attr: str) -> None:
        cell = ttk.Frame(parent, style="Card.TFrame")
        cell.grid(row=row, column=col, sticky="w", padx=(0, 12), pady=2)
        ttk.Label(cell, text=title, style="Muted.TLabel").grid(row=0, column=0, sticky="w")
        lbl = ttk.Label(cell, text=value, style="Value.TLabel")
        lbl.grid(row=1, column=0, sticky="w")
        setattr(self, attr, lbl)

    def _build_actions(self, parent: ttk.Frame) -> None:
        actions = ttk.Frame(parent, style="App.TFrame")
        actions.grid(row=2, column=0, sticky="ew", pady=(0, 8))
        for c in range(4):
            actions.columnconfigure(c, weight=1)

        self.start_button = ttk.Button(actions, text="Start (F6)", style="Primary.TButton", command=self.start_clicking)
        self.start_button.grid(row=0, column=0, sticky="ew", padx=(0, 5))
        self.stop_button = ttk.Button(actions, text="Stop (Esc)", style="Secondary.TButton", command=self.stop_clicking)
        self.stop_button.grid(row=0, column=1, sticky="ew", padx=5)
        self.test_button = ttk.Button(actions, text="Click once (test)", command=self.test_click)
        self.test_button.grid(row=0, column=2, sticky="ew", padx=5)
        self.reset_button = ttk.Button(actions, text="Reset defaults", command=self.reset_defaults)
        self.reset_button.grid(row=0, column=3, sticky="ew", padx=(5, 0))
        ToolTip(self.test_button, "Performs one click now using the current settings.")
        ToolTip(self.reset_button, "Restores default settings. Save or close the app to persist them.")

    def _build_footer(self, parent: ttk.Frame) -> None:
        ttk.Label(
            parent,
            text=(
                "Use responsibly and only where automated clicking is permitted. "
                "This app stores settings locally on your device and does not collect or transmit personal data."
            ),
            style="MutedApp.TLabel",
            wraplength=700,
            justify="left",
        ).grid(row=3, column=0, sticky="w")

    def _bind_shortcuts(self) -> None:
        self.root.bind("<Escape>", lambda _e: self.stop_clicking())
        self.root.bind("<Control-s>", lambda _e: self._save_settings(silent=False, notify=True))
        self.root.bind("<Control-S>", lambda _e: self._save_settings(silent=False, notify=True))
        self.root.bind("<Alt-s>", lambda _e: self.start_clicking())
        self.root.bind("<Alt-S>", lambda _e: self.start_clicking())
        self.root.bind("<Alt-t>", lambda _e: self.stop_clicking())
        self.root.bind("<Alt-T>", lambda _e: self.stop_clicking())
        self.root.bind("<F1>", lambda _e: self.show_shortcuts())

    def _set_detail(self, text: str) -> None:
        try:
            self.detail_label.configure(text=text)
        except Exception:
            pass

    def _set_status(self, mode: str, detail: str | None = None) -> None:
        if mode == "running":
            self.status_badge.configure(text="RUNNING", bg=SUCCESS_BG, fg=SUCCESS)
            self.state_value.configure(text="Running", foreground=SUCCESS)
            self.start_button.configure(state="disabled")
            self.stop_button.configure(state="normal")
            self.test_button.configure(state="disabled")
            self.reset_button.configure(state="disabled")
            detail = detail or "Clicking is active. Use the hotkey, Stop, Esc, or the failsafe corner to stop."
        elif mode == "countdown":
            self.status_badge.configure(text="STARTING", bg=WARNING_BG, fg=WARNING)
            self.state_value.configure(text="Starting", foreground=WARNING)
            self.start_button.configure(state="disabled")
            self.stop_button.configure(state="normal")
            self.test_button.configure(state="disabled")
            self.reset_button.configure(state="disabled")
            detail = detail or "Starting soon..."
        else:
            self.status_badge.configure(text="STOPPED", bg=DANGER_BG, fg=DANGER)
            self.state_value.configure(text="Stopped", foreground=DANGER)
            self.start_button.configure(state="normal")
            self.stop_button.configure(state="disabled")
            self.test_button.configure(state="normal")
            self.reset_button.configure(state="normal")
            detail = detail or "Ready."
        self._set_detail(detail)
        self._refresh_stats_labels()

    def _refresh_stats_labels(self) -> None:
        self.clicks_value.configure(text=str(self.clicks_done))
        if self.session_started_at and (self.running or self.countdown_active):
            elapsed = max(0.0, time.time() - self.session_started_at)
        else:
            elapsed = self._last_elapsed
        self.elapsed_value.configure(text=f"{elapsed:.1f}s")
        if self.target_clicks > 0 and (self.running or self.countdown_active or self.clicks_done > 0):
            self.progress.configure(maximum=self.target_clicks, value=min(self.clicks_done, self.target_clicks))
        else:
            self.progress.configure(maximum=100, value=0)

    def _tick_ui(self) -> None:
        if not self._hotkey_running:
            return
        try:
            self._refresh_stats_labels()
            self._update_cursor_label()
        except Exception:
            pass
        try:
            self.root.after(150, self._tick_ui)
        except Exception:
            pass

    def _get_cursor_pos(self):
        pt = POINT()
        try:
            if user32.GetCursorPos(ctypes.byref(pt)):
                return int(pt.x), int(pt.y)
        except Exception:
            return None
        return None

    def _update_cursor_label(self) -> None:
        pos = self._get_cursor_pos()
        if pos:
            self.cursor_live_label.configure(text=f"Cursor position: {pos[0]}, {pos[1]}")
        else:
            self.cursor_live_label.configure(text="Cursor position: unavailable")

    def _update_rate_info(self) -> None:
        try:
            interval = float(self.interval_var.get().strip())
            jitter = float(self.jitter_var.get().strip() or "0")
            if interval <= 0 or jitter < 0:
                raise ValueError
            if jitter > 0:
                lo = max(1.0, interval - jitter)
                hi = interval + jitter
                self.rate_label.configure(text=f"~{1000.0 / hi:.1f}–{1000.0 / lo:.1f} CPS")
            else:
                self.rate_label.configure(text=f"~{1000.0 / interval:.1f} CPS")
        except Exception:
            self.rate_label.configure(text="CPS: —")

    def _update_repeat_state(self) -> None:
        self.count_entry.configure(state="normal" if self.repeat_var.get() == "count" else "disabled")

    def _update_position_state(self) -> None:
        fixed = self.position_mode_var.get() == "fixed"
        state = "normal" if fixed else "disabled"
        self.x_entry.configure(state=state)
        self.y_entry.configure(state=state)
        can_pick = fixed and not self.running and not self.countdown_active and not self.pick_active
        self.pick_button.configure(state="normal" if can_pick else "disabled")
        self.use_current_button.configure(state="normal" if can_pick else "disabled")

    def _apply_always_on_top(self) -> None:
        try:
            self.root.attributes("-topmost", bool(self.always_on_top_var.get()))
        except Exception:
            pass

    def _apply_hotkey_change(self, initial: bool = False) -> None:
        key = self.hotkey_var.get()
        if key not in HOTKEY_OPTIONS:
            key = "F6"
            self.hotkey_var.set(key)
        self._hotkey_vk = HOTKEY_OPTIONS[key]
        self.hotkey_value.configure(text=key)
        self.start_button.configure(text=f"Start ({key})")
        self.stop_button.configure(text=f"Stop ({key} / Esc)")
        if not initial and not self.running and not self.countdown_active:
            self._set_detail(f"Global hotkey set to {key}.")

    def _parse_int(self, value: str, field: str, min_value=None, max_value=None):
        try:
            n = int(str(value).strip())
        except Exception:
            messagebox.showerror("Invalid value", f"Enter a valid whole number for {field}.", parent=self.root)
            return None
        if min_value is not None and n < min_value:
            messagebox.showerror("Invalid value", f"{field} must be at least {min_value}.", parent=self.root)
            return None
        if max_value is not None and n > max_value:
            messagebox.showerror("Invalid value", f"{field} must be at most {max_value}.", parent=self.root)
            return None
        return n

    def _validate_settings(self):
        try:
            interval_ms = float(self.interval_var.get().strip())
            if interval_ms <= 0 or interval_ms > 3_600_000:
                raise ValueError
        except ValueError:
            messagebox.showerror("Invalid interval", "Enter an interval greater than 0 ms and at most 3,600,000 ms.", parent=self.root)
            return None
        try:
            jitter_ms = float(self.jitter_var.get().strip() or "0")
            if jitter_ms < 0 or jitter_ms > 3_600_000:
                raise ValueError
        except ValueError:
            messagebox.showerror("Invalid jitter", "Enter a jitter value of 0 ms or greater.", parent=self.root)
            return None
        try:
            delay_sec = float(self.delay_var.get().strip() or "0")
            if delay_sec < 0 or delay_sec > 60:
                raise ValueError
        except ValueError:
            messagebox.showerror("Invalid delay", "Enter a start delay between 0 and 60 seconds.", parent=self.root)
            return None

        repeat_mode = self.repeat_var.get()
        if repeat_mode not in ("until_stopped", "count"):
            messagebox.showerror("Invalid repeat mode", "Choose a valid repeat mode.", parent=self.root)
            return None
        max_clicks = 0
        if repeat_mode == "count":
            parsed = self._parse_int(self.count_var.get(), "number of clicks", min_value=1, max_value=100_000_000)
            if parsed is None:
                return None
            max_clicks = parsed

        position_mode = self.position_mode_var.get()
        if position_mode not in ("cursor", "fixed"):
            messagebox.showerror("Invalid position mode", "Choose a valid click location mode.", parent=self.root)
            return None
        fixed_x = fixed_y = 0
        if position_mode == "fixed":
            px = self._parse_int(self.x_var.get(), "X coordinate", min_value=-100_000, max_value=100_000)
            py = self._parse_int(self.y_var.get(), "Y coordinate", min_value=-100_000, max_value=100_000)
            if px is None or py is None:
                return None
            fixed_x, fixed_y = px, py

        button_name = self.button_var.get()
        if button_name not in BUTTON_FLAGS:
            messagebox.showerror("Invalid button", "Select a valid mouse button.", parent=self.root)
            return None
        click_type = self.click_type_var.get()
        if click_type not in ("Single", "Double"):
            messagebox.showerror("Invalid click type", "Select Single or Double.", parent=self.root)
            return None
        hotkey = self.hotkey_var.get()
        if hotkey not in HOTKEY_OPTIONS:
            hotkey = "F6"
            self.hotkey_var.set(hotkey)

        return {
            "interval_ms": interval_ms,
            "jitter_ms": jitter_ms,
            "delay_sec": delay_sec,
            "repeat_mode": repeat_mode,
            "max_clicks": max_clicks,
            "position_mode": position_mode,
            "fixed_x": fixed_x,
            "fixed_y": fixed_y,
            "button_name": button_name,
            "click_type": click_type,
            "hotkey": hotkey,
            "failsafe": bool(self.failsafe_var.get()),
        }

    def _collect_settings(self) -> dict:
        data = {
            "interval_ms": self.interval_var.get(),
            "jitter_ms": self.jitter_var.get(),
            "button": self.button_var.get(),
            "click_type": self.click_type_var.get(),
            "repeat_mode": self.repeat_var.get(),
            "count": self.count_var.get(),
            "delay_sec": self.delay_var.get(),
            "position_mode": self.position_mode_var.get(),
            "x": self.x_var.get(),
            "y": self.y_var.get(),
            "hotkey": self.hotkey_var.get(),
            "always_on_top": bool(self.always_on_top_var.get()),
            "failsafe": bool(self.failsafe_var.get()),
        }
        try:
            data["geometry"] = self.root.geometry()
        except Exception:
            data["geometry"] = ""
        return data

    def _apply_settings(self, data: dict) -> None:
        def set_str(key, var, allowed=None):
            if key not in data or data[key] is None:
                return
            val = str(data[key])
            if allowed is not None and val not in allowed:
                return
            var.set(val)

        set_str("interval_ms", self.interval_var)
        set_str("jitter_ms", self.jitter_var)
        set_str("button", self.button_var, set(BUTTON_FLAGS.keys()))
        set_str("click_type", self.click_type_var, {"Single", "Double"})
        set_str("repeat_mode", self.repeat_var, {"until_stopped", "count"})
        set_str("count", self.count_var)
        set_str("delay_sec", self.delay_var)
        set_str("position_mode", self.position_mode_var, {"cursor", "fixed"})
        set_str("x", self.x_var)
        set_str("y", self.y_var)
        set_str("hotkey", self.hotkey_var, set(HOTKEY_OPTIONS.keys()))
        if "always_on_top" in data:
            self.always_on_top_var.set(bool(data.get("always_on_top")))
        if "failsafe" in data:
            self.failsafe_var.set(bool(data.get("failsafe")))
        geom = str(data.get("geometry") or "").strip()
        if geom:
            try:
                self.root.geometry(geom)
            except Exception:
                pass

    def _load_settings(self) -> None:
        for path in (get_settings_path(), get_legacy_settings_path()):
            try:
                if path.is_file():
                    with path.open("r", encoding="utf-8") as f:
                        data = json.load(f)
                    if isinstance(data, dict):
                        self._apply_settings(data)
                    return
            except Exception:
                continue

    def _save_settings(self, silent: bool = True, notify: bool = False) -> None:
        path = get_settings_path()
        data = self._collect_settings()
        try:
            with self._settings_lock:
                tmp = path.with_suffix(".tmp")
                with tmp.open("w", encoding="utf-8") as f:
                    json.dump(data, f, indent=2)
                os.replace(tmp, path)
            if notify and not silent:
                self._set_detail(f"Settings saved to {path}.")
        except Exception:
            if not silent:
                messagebox.showwarning("Save settings", "Could not save settings.", parent=self.root)

    def reset_defaults(self) -> None:
        if self.running or self.countdown_active:
            return
        self.interval_var.set(DEFAULT_SETTINGS["interval_ms"])
        self.jitter_var.set(DEFAULT_SETTINGS["jitter_ms"])
        self.button_var.set(DEFAULT_SETTINGS["button"])
        self.click_type_var.set(DEFAULT_SETTINGS["click_type"])
        self.repeat_var.set(DEFAULT_SETTINGS["repeat_mode"])
        self.count_var.set(DEFAULT_SETTINGS["count"])
        self.delay_var.set(DEFAULT_SETTINGS["delay_sec"])
        self.position_mode_var.set(DEFAULT_SETTINGS["position_mode"])
        self.x_var.set(DEFAULT_SETTINGS["x"])
        self.y_var.set(DEFAULT_SETTINGS["y"])
        self.hotkey_var.set(DEFAULT_SETTINGS["hotkey"])
        self.always_on_top_var.set(bool(DEFAULT_SETTINGS["always_on_top"]))
        self.failsafe_var.set(bool(DEFAULT_SETTINGS["failsafe"]))
        self.clicks_done = 0
        self.target_clicks = 0
        self._last_elapsed = 0.0
        self._apply_hotkey_change()
        self._apply_always_on_top()
        self._update_repeat_state()
        self._update_position_state()
        self._update_rate_info()
        self._set_status("stopped", "Default settings restored.")

    def start_clicking(self) -> None:
        if self.running or self.countdown_active:
            return
        settings = self._validate_settings()
        if settings is None:
            return

        signature = (
            settings["button_name"],
            settings["click_type"],
            settings["position_mode"],
            settings["fixed_x"],
            settings["fixed_y"],
            settings["interval_ms"],
            settings["jitter_ms"],
            settings["repeat_mode"],
            settings["max_clicks"],
        )
        if signature != self._last_start_signature or not self._start_confirmed:
            msg = self._describe_settings(settings)
            if not messagebox.askyesno("Start auto-clicking", msg + "\n\nStart now?", parent=self.root):
                self._set_detail("Start cancelled.")
                return
            self._start_confirmed = True
            self._last_start_signature = signature

        self._save_settings(silent=True)
        self._active_settings = settings
        self._stop_event.clear()
        self.clicks_done = 0
        self._last_elapsed = 0.0
        self.target_clicks = settings["max_clicks"] if settings["repeat_mode"] == "count" else 0
        self.session_started_at = time.time()
        self._apply_hotkey_change(initial=True)
        self._update_position_state()
        self._refresh_stats_labels()

        if settings["delay_sec"] > 0:
            self.countdown_active = True
            self._set_status("countdown")
            threading.Thread(target=self._countdown_then_run, args=(float(settings["delay_sec"]),), daemon=True).start()
        else:
            self._begin_click_thread()

    def _describe_settings(self, settings: dict) -> str:
        where = "current cursor position" if settings["position_mode"] == "cursor" else f"fixed position ({settings['fixed_x']}, {settings['fixed_y']})"
        repeat = "until stopped" if settings["repeat_mode"] == "until_stopped" else f"{settings['max_clicks']} time(s)"
        jitter = f" ±{settings['jitter_ms']} ms jitter" if settings["jitter_ms"] > 0 else ""
        delay = f"\nStart delay: {settings['delay_sec']} s" if settings["delay_sec"] > 0 else ""
        failsafe = "On" if settings["failsafe"] else "Off"
        return (
            f"Action: {settings['click_type']} {settings['button_name'].lower()} click\n"
            f"Location: {where}\n"
            f"Interval: {settings['interval_ms']} ms{jitter}\n"
            f"Repeat: {repeat}{delay}\n"
            f"Hotkey: {settings['hotkey']} (toggle start/stop)\n"
            f"Failsafe (top-left corner): {failsafe}"
        )

    def _countdown_then_run(self, delay_sec: float) -> None:
        remaining = delay_sec
        while remaining > 0 and not self._stop_event.is_set() and self.countdown_active:
            display = f"{remaining:.1f}" if remaining < 10 else f"{remaining:.0f}"
            self.root.after(0, lambda s=display: self._set_detail(f"Starting in {s}s... position your target."))
            step = 0.1 if remaining < 2 else 0.2
            time.sleep(min(step, remaining))
            remaining -= step
        if self._stop_event.is_set() or not self.countdown_active:
            self.countdown_active = False
            self.session_started_at = None
            self._last_elapsed = 0.0
            self.root.after(0, lambda: self._set_status("stopped", "Start cancelled."))
            self.root.after(0, self._update_position_state)
            return
        self.countdown_active = False
        self.root.after(0, self._begin_click_thread)

    def _begin_click_thread(self) -> None:
        if self._stop_event.is_set():
            self._set_status("stopped", "Start cancelled.")
            self._update_position_state()
            return
        self.running = True
        self.session_started_at = time.time()
        self._set_status("running")
        self._update_position_state()
        threading.Thread(target=self._click_loop, daemon=True).start()

    def stop_clicking(self, reason: str | None = None) -> None:
        if not self.running and not self.countdown_active:
            self._set_status("stopped", reason or "Ready.")
            self._update_position_state()
            return
        self._stop_event.set()
        was_countdown = self.countdown_active
        self.countdown_active = False
        self.running = False
        if self.session_started_at:
            self._last_elapsed = max(0.0, time.time() - self.session_started_at)
        self.session_started_at = None
        if reason is None:
            reason = "Start cancelled." if was_countdown else f"Stopped. {self.clicks_done} click(s) completed."
        self._set_status("stopped", reason)
        self._update_position_state()

    def toggle_clicking(self) -> None:
        if self.running or self.countdown_active:
            self.stop_clicking()
        else:
            self.start_clicking()

    def _send_mouse_button(self, down_flag: int, up_flag: int) -> bool:
        try:
            inputs = (INPUT * 2)()
            inputs[0].type = INPUT_MOUSE
            inputs[0].mi = MOUSEINPUT(0, 0, 0, down_flag, 0, 0)
            inputs[1].type = INPUT_MOUSE
            inputs[1].mi = MOUSEINPUT(0, 0, 0, up_flag, 0, 0)
            if user32.SendInput(2, inputs, ctypes.sizeof(INPUT)) == 2:
                return True
        except Exception:
            pass
        try:
            user32.mouse_event(down_flag, 0, 0, 0, 0)
            user32.mouse_event(up_flag, 0, 0, 0, 0)
            return True
        except Exception:
            return False

    def _perform_click_once(self, settings: dict) -> bool:
        if settings["position_mode"] == "fixed":
            try:
                user32.SetCursorPos(int(settings["fixed_x"]), int(settings["fixed_y"]))
            except Exception:
                return False
        down, up = BUTTON_FLAGS[settings["button_name"]]
        if not self._send_mouse_button(down, up):
            return False
        if settings["click_type"] == "Double":
            time.sleep(0.045)
            if not self._send_mouse_button(down, up):
                return False
        return True

    def _failsafe_triggered(self) -> bool:
        return self._get_cursor_pos() == (0, 0)

    def _next_interval(self, base_ms: float, jitter_ms: float) -> float:
        if jitter_ms <= 0:
            return max(0.001, base_ms / 1000.0)
        low = max(1.0, base_ms - jitter_ms)
        high = base_ms + jitter_ms
        return max(0.001, random.uniform(low, high) / 1000.0)

    def _click_loop(self) -> None:
        settings = self._active_settings or {}
        base_ms = float(settings.get("interval_ms", 100))
        jitter_ms = float(settings.get("jitter_ms", 0))
        repeat_mode = settings.get("repeat_mode", "until_stopped")
        max_clicks = int(settings.get("max_clicks", 0))
        failsafe = bool(settings.get("failsafe", True))

        while not self._stop_event.is_set():
            if failsafe and self._failsafe_triggered():
                self.root.after(0, lambda: self.stop_clicking("Failsafe triggered: mouse moved to the top-left corner (0,0)."))
                return
            if not self._perform_click_once(settings):
                self.root.after(0, self._on_click_error)
                return

            self.clicks_done += 1
            count = self.clicks_done
            detail = (
                f"Clicking... {max(0, max_clicks - count)} remaining."
                if repeat_mode == "count"
                else "Clicking... use the hotkey, Stop, or Esc to end."
            )
            self.root.after(0, lambda c=count, d=detail: self._on_click_tick(c, d))

            if repeat_mode == "count" and count >= max_clicks:
                self.root.after(0, self._on_completed)
                return

            interval_sec = self._next_interval(base_ms, jitter_ms)
            end_time = time.perf_counter() + interval_sec
            while not self._stop_event.is_set():
                if failsafe and self._failsafe_triggered():
                    self.root.after(0, lambda: self.stop_clicking("Failsafe triggered: mouse moved to the top-left corner (0,0)."))
                    return
                remaining_t = end_time - time.perf_counter()
                if remaining_t <= 0:
                    break
                time.sleep(min(0.02, remaining_t))

    def _on_click_tick(self, count: int, detail: str) -> None:
        if self.running:
            self.clicks_value.configure(text=str(count))
            self._set_detail(detail)

    def _on_completed(self) -> None:
        if self.session_started_at:
            self._last_elapsed = max(0.0, time.time() - self.session_started_at)
        self.running = False
        self.countdown_active = False
        self.session_started_at = None
        self._stop_event.set()
        self._set_status("stopped", f"Completed {self.clicks_done} click(s).")
        self._refresh_stats_labels()
        self._update_position_state()

    def _on_click_error(self) -> None:
        if self.session_started_at:
            self._last_elapsed = max(0.0, time.time() - self.session_started_at)
        self.running = False
        self.countdown_active = False
        self.session_started_at = None
        self._stop_event.set()
        self._set_status("stopped", "Stopped due to a click error.")
        self._refresh_stats_labels()
        self._update_position_state()
        try:
            messagebox.showerror("Auto-click error", "An error occurred while sending mouse clicks.", parent=self.root)
        except Exception:
            pass

    def test_click(self) -> None:
        if self.running or self.countdown_active or self.pick_active:
            return
        settings = self._validate_settings()
        if settings is None:
            return
        try:
            ok = self._perform_click_once(settings)
        except Exception:
            ok = False
        if ok:
            self._set_detail("Performed 1 test click with the current settings.")
        else:
            self._set_detail("Test click failed. Check your settings and try again.")
            messagebox.showerror("Test click failed", "Could not perform the test click.", parent=self.root)

    def use_current_position(self) -> None:
        if self.running or self.countdown_active or self.pick_active:
            return
        pos = self._get_cursor_pos()
        if pos is None:
            self._set_detail("Could not read the current cursor position.")
            return
        self.position_mode_var.set("fixed")
        self.x_var.set(str(pos[0]))
        self.y_var.set(str(pos[1]))
        self._update_position_state()
        self._set_detail(f"Fixed position set to current cursor: ({pos[0]}, {pos[1]}).")

    def start_pick_location(self) -> None:
        if self.running or self.countdown_active or self.pick_active:
            return
        self.position_mode_var.set("fixed")
        self._update_position_state()
        self.pick_active = True
        self._update_position_state()
        threading.Thread(target=self._pick_location_worker, daemon=True).start()

    def _pick_location_worker(self) -> None:
        for remaining in (3, 2, 1):
            self.root.after(0, lambda r=remaining: self._set_detail(f"Move mouse to target... capturing in {r}s"))
            time.sleep(1)
        pos = self._get_cursor_pos()

        def finish() -> None:
            self.pick_active = False
            if pos is not None:
                self.x_var.set(str(pos[0]))
                self.y_var.set(str(pos[1]))
                self._set_detail(f"Captured fixed position: ({pos[0]}, {pos[1]}).")
            else:
                self._set_detail("Could not capture the mouse position.")
            self._update_position_state()

        try:
            self.root.after(0, finish)
        except Exception:
            self.pick_active = False

    def _hotkey_listener(self) -> None:
        was_down = False
        while self._hotkey_running:
            try:
                is_down = bool(user32.GetAsyncKeyState(self._hotkey_vk) & 0x8000)
            except Exception:
                is_down = False
            if is_down and not was_down:
                try:
                    self.root.after(0, self.toggle_clicking)
                except Exception:
                    pass
            was_down = is_down
            time.sleep(0.03)

    def show_shortcuts(self) -> None:
        text = (
            "Keyboard shortcuts\n\n"
            "Global hotkey (default F6): Start/stop clicking\n"
            "Esc (when window is focused): Stop clicking\n"
            "Ctrl+S: Save settings\n"
            "Alt+S: Start\n"
            "Alt+T: Stop\n"
            "F1: Show this help\n"
            "Alt+F4: Close the app\n"
            "Tab / Shift+Tab: Move between controls\n"
            "Space / Enter: Activate focused controls\n\n"
            "Mouse actions: Use 'Pick location (3s)' or 'Use current cursor' to set a fixed click point."
        )
        messagebox.showinfo("Keyboard shortcuts", text, parent=self.root)

    def show_responsible_use(self) -> None:
        text = (
            "Responsible use\n\n"
            "ByJTT AutoClicker is intended for legitimate automation tasks and accessibility-related convenience where automated clicking is allowed.\n\n"
            "Do not use this app to violate software terms of service, game rules, workplace policies, or applicable laws. You are responsible for how you use it.\n\n"
            "Safety tips: keep the failsafe enabled, test with 'Click once', and be ready to stop with the hotkey, Esc, or by moving the mouse to the top-left corner (0,0)."
        )
        messagebox.showinfo("Responsible use", text, parent=self.root)

    def show_about(self) -> None:
        top = tk.Toplevel(self.root)
        top.title(f"About {APP_NAME}")
        top.transient(self.root)
        top.resizable(False, False)
        top.configure(bg=CARD_BG)

        frame = ttk.Frame(top, padding=16, style="Card.TFrame")
        frame.grid(row=0, column=0, sticky="nsew")

        ttk.Label(frame, text=APP_NAME, font=("Segoe UI Semibold", 14), background=CARD_BG, foreground=TEXT_PRIMARY).grid(row=0, column=0, sticky="w")
        ttk.Label(frame, text=f"Version {APP_VERSION}", style="Muted.TLabel").grid(row=1, column=0, sticky="w", pady=(2, 8))
        info = (
            f"{APP_DESCRIPTION}\n\n"
            "This Windows desktop utility is designed for clear controls, keyboard access, and local-only settings storage.\n"
            "No telemetry, network services, or accounts are used by the app.\n\n"
            f"Author: {APP_AUTHOR}\n"
            "License: MIT"
        )
        ttk.Label(frame, text=info, wraplength=420, justify="left", style="TLabel").grid(row=2, column=0, sticky="w")

        link = tk.Label(frame, text=REPO_URL, fg=ACCENT, bg=CARD_BG, cursor="hand2", font=("Segoe UI", 9, "underline"))
        link.grid(row=3, column=0, sticky="w", pady=(8, 12))
        link.bind("<Button-1>", lambda _e: webbrowser.open(REPO_URL))

        btns = ttk.Frame(frame, style="Card.TFrame")
        btns.grid(row=4, column=0, sticky="e")
        ttk.Button(btns, text="Close", command=top.destroy).grid(row=0, column=0)

        try:
            top.grab_set()
            top.focus_set()
        except Exception:
            pass

    def on_close(self) -> None:
        self._save_settings(silent=True)
        self._hotkey_running = False
        self._stop_event.set()
        self.running = False
        self.countdown_active = False
        try:
            self.root.destroy()
        except Exception:
            pass


def main() -> None:
    root = tk.Tk()
    try:
        root.tk.call("tk", "scaling", 1.2)
    except Exception:
        pass
    ByJTTAutoClicker(root)
    root.mainloop()


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        try:
            messagebox.showerror(APP_NAME, f"An unexpected error occurred:\n{exc}")
        except Exception:
            pass
        raise
