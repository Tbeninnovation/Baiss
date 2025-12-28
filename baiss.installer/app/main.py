# pip install Pillow

"""
    File: main.py
    Requirements:
        - Python 3.10+
        - python -m pip install Pillow
    Instructions:
        - Write Everything in in this file, not other files are allowed.
        - To run the installer, execute: python main.py
    File Structure:
        /assets
            /img
                step-1.png
                step-2.png
                step-3.png
                step-4.png
                step-5.png
        /main.py
    Description:
        This script creates a GUI-based setup wizard for installing the Baiss application.
        It includes multiple steps with navigation buttons, checkboxes, and input fields.
        The installer guides users through the installation process with a user-friendly interface.
"""

# message box in tkinter
import os
import sys
import threading
import tkinter as tk
import tkinter.font as tkFont
import ctypes
from typing import Any
from typing import List
from typing import Callable
from PIL import Image, ImageDraw, ImageTk
from tkinter import filedialog, messagebox
from tkinter.ttk import *
from baiss_installer import BaissPackageInstaller
from baiss_installer.utils import project_path

class BaissConfig:

    def __init__(self, package_installer: BaissPackageInstaller):
        self.__configs__: dict = {
            "InstallationDirectory": package_installer.default_installation_path,
            "LaunchAfterSetup"     : True,
            "CreateDesktopIcon"    : True,
            "CreateStartMenuIcon"  : True,
            "AcceptLicense"        : False,
            "BaissPythonCore"      : None,
            "BaissPythonVersion"   : None,
            "BaissPythonPath"      : None,
        }

    def get_launch_after_setup(self) -> bool:
        return self.__configs__["LaunchAfterSetup"]

    def set_launch_after_setup(self, enabled: bool):
        self.__configs__["LaunchAfterSetup"] = True if enabled else False

    def set_install_path(self, path: str):
        self.__configs__["InstallationDirectory"] = path

    def get_install_path(self) -> str:
        return self.__configs__["InstallationDirectory"]

    def set_desktop_shortcut(self, enabled: bool):
        self.__configs__["CreateDesktopIcon"] = True if enabled else False

    def get_desktop_shortcut(self) -> bool:
        return self.__configs__["CreateDesktopIcon"]

    def set_configure_start_menu(self, enabled: bool):
        self.__configs__["CreateStartMenuIcon"] = True if enabled else False

    def get_configure_start_menu(self) -> bool:
        return self.__configs__["CreateStartMenuIcon"]

# - - - - - - - - - - - - - - - - - - <start> components </start> - - - - - - - - - - - - - - - - - - #
class BaseComponent:

    default_font_name: str = "Segoe UI"
    default_font_size: int = 14

    # Typography scale - proper font hierarchy
    font_h1: int = 24
    font_h2: int = 20
    font_h3: int = 18
    font_body: int = 16
    font_small: int = 14
    font_tiny: int = 12

    def __init__(self,
        x            : int = None,
        y            : int = None,
        width        : int = None,
        height       : int = None,
        background   : str = None,
        border_radius: int = None,
    ):

        if border_radius is not None:
            bg = background
            r  = int(str(border_radius), 10)
            w  = int(str(width), 10)
            h  = int(str(height), 10)
            self._tk_arc1 = self._tkparent.create_arc(     (x, y, x + 2 * r, y + 2 * r), start=90, extent=90, fill=bg, outline=bg)
            self._tk_arc2 = self._tkparent.create_arc(     (x + w - 2 * r, y, x + w, y + 2 * r), start=0, extent=90, fill=bg, outline=bg)
            self._tk_arc3 = self._tkparent.create_arc(     (x, y + h - 2 * r, x + 2 * r, y + h), start=180, extent=90, fill=bg, outline=bg)
            self._tk_arc4 = self._tkparent.create_arc(     (x + w - 2 * r, y + h - 2 * r, x + w, y + h), start=270, extent=90, fill=bg, outline=bg)
            self._tk_rct1 = self._tkparent.create_rectangle(x + r, y, x + w - r, y + h, fill = bg, outline = bg)
            self._tk_rct2 = self._tkparent.create_rectangle(x, y + r, x + w, y + h - r, fill=bg, outline=bg)

    def destroy(self) -> int:
        """
            Destroy all attributes of the class instance, that starts with '_tk_'
        """
        count: int = 0
        for attr_name in dir(self):
            if attr_name.startswith('_tk_'):
                try:
                    self._tkparent.delete(getattr(self, attr_name))
                    count += 1
                except:
                    pass
                try:
                    getattr(self, attr_name).destroy()
                    count += 1
                except:
                    pass
        return count

class TextArea(BaseComponent):

    def __init__(self,
            x          : int = 0,
            y          : int = 0,
            context     = None,
            text        = "",
            width       = 686,
            background  = "#FFFFFF",
            foreground  = "#050D14",
            font_family = "Segoe UI",
            font_size   = BaseComponent.font_body,
        ):
            self._tk_text = tk.Message(
                context._base_canvas,
                text   = text,
                width  = width,
                bg     = background,
                fg     = foreground,
                font   = (font_family, font_size),
                anchor = "w"
            )
            self._tk_text.place(x=x, y=y)

class TextField(BaseComponent):
    def __init__(
            self,
            x           : int = 244,
            y           : int = 42,
            color       : str = "#000000",
            text        : str = "Text Field",
            context     : Any = None,
            font_size   : int = BaseComponent.font_body,
            font_name   : str = "Segoe UI",
            font_weight : str = "normal",   # "normal" or "bold"
            font_slant  : str = "roman",    # "roman" or "italic"
            underline   : bool = False,
            overstrike  : bool = False,
            anchor      : str = "nw"
        ):
            self._tkparent = context._base_canvas
            self._tk_font = tkFont.Font(
                family    = font_name,
                size      = font_size,
                weight    = font_weight,
                slant     = font_slant,
                underline = underline,
                overstrike= overstrike
            )
            self._tk_text = self._tkparent.create_text(
                x,
                y,
                text   = text,
                fill   = color,
                font   = self._tk_font,
                anchor = anchor
            )

    def set_value(self, new_text: str):
        """Update the displayed text in the TextField"""
        self._tkparent.itemconfig(self._tk_text, text=new_text)

class CanvasCheckButton(BaseComponent):
    def __init__(self,
            context,
            x: int,
            y: int,
            text: str,
            command=None,
            initial_checked: bool = True,
            font_size: int = BaseComponent.default_font_size - 2,
            foreground: str = '#FFFFFF',
            indicator_size: int = 22,
            indicator_outline: str = '#FFFFFF',
            indicator_fill: str = '#0B5FB0',
            indicator_unchecked_fill: str = '',
            check_color: str = '#FFFFFF',
            spacing: int = 12,
        ):
        self._context = context
        self._base_canvas = context._base_canvas
        self._command = command
        self._x = int(x)
        self._y = int(y)
        self._indicator_size = max(12, int(indicator_size))
        self._spacing = max(4, int(spacing))
        self._indicator_outline = indicator_outline or foreground
        self._indicator_fill = indicator_fill
        self._indicator_unchecked_fill = indicator_unchecked_fill
        self._foreground = foreground
        self._check_color = check_color or foreground
        self._font_size = max(8, int(font_size))
        self._text = text
        self._checked = bool(initial_checked)
        self._check_ids: list[int] = []
        self._box_id = self._base_canvas.create_rectangle(
            self._x,
            self._y,
            self._x + self._indicator_size,
            self._y + self._indicator_size,
            outline=self._indicator_outline,
            width=2,
            fill=self._indicator_fill if self._checked else self._indicator_unchecked_fill,
        )
        self._text_id = self._base_canvas.create_text(
            self._x + self._indicator_size + self._spacing,
            self._y + self._indicator_size / 2,
            text=self._text,
            fill=self._foreground,
            font=(BaseComponent.default_font_name, self._font_size),
            anchor='w',
        )
        for item in (self._box_id, self._text_id):
            self._base_canvas.tag_bind(item, '<Button-1>', self._handle_click)
            self._base_canvas.tag_bind(item, '<Enter>', self._set_hand_cursor)
            self._base_canvas.tag_bind(item, '<Leave>', self._reset_cursor)
        self._render_check()
    def _set_hand_cursor(self, _event=None):
        self._base_canvas.config(cursor='hand2')
    def _reset_cursor(self, _event=None):
        self._base_canvas.config(cursor='')
    def _handle_click(self, _event=None):
        self.toggle()
    def _clear_check(self):
        for item in self._check_ids:
            try:
                self._base_canvas.delete(item)
            except tk.TclError:
                pass
        self._check_ids = []
    def _render_check(self):
        self._clear_check()
        fill = self._indicator_fill if self._checked else self._indicator_unchecked_fill
        self._base_canvas.itemconfigure(self._box_id, fill=fill)
        if not self._checked:
            return
        size = self._indicator_size
        inset = max(3, size // 4)
        stroke = max(2, size // 6)
        first = self._base_canvas.create_line(
            self._x + inset,
            self._y + size // 2,
            self._x + size // 2,
            self._y + size - inset,
            fill=self._check_color,
            width=stroke,
        )
        second = self._base_canvas.create_line(
            self._x + size // 2,
            self._y + size - inset,
            self._x + size - inset,
            self._y + inset,
            fill=self._check_color,
            width=stroke,
        )
        self._check_ids.extend([first, second])
    def set_checked(self, value: bool):
        value = bool(value)
        if self._checked == value:
            return
        self._checked = value
        self._render_check()
        if self._command:
            self._command(self._checked)
    def toggle(self):
        self.set_checked(not self._checked)
    def is_checked(self) -> bool:
        return bool(self._checked)
    def destroy(self) -> int:
        self._reset_cursor()
        for item in [self._box_id, self._text_id]:
            if item is None:
                continue
            try:
                self._base_canvas.delete(item)
            except tk.TclError:
                pass
        self._box_id = None
        self._text_id = None
        self._clear_check()
        return super().destroy()

class CanvasLabel(BaseComponent):
    def __init__(self,
            context,
            x         : int,
            y         : int,
            text      : str,
            font_size : int = 20,
            weight    : str = "bold",
            foreground: str = "#1F2933",
            background: str | None = "#FFFFFF",
            anchor    : str = "nw",
            max_width : int | None = None,
            justify   : str = "left",
        ):
        self._context = context
        font = (BaseComponent.default_font_name, font_size, weight)
        if background is None:
            self._tk_label = None
            self._canvas_window = None
            self._text_id = context._base_canvas.create_text(x, y, text=text, fill=foreground, font=font, anchor=anchor)
        else:
            self._tk_label = tk.Label(
                context._base_canvas,
                text     = text,
                font     = font,
                fg       = foreground,
                bg       = background,
                justify  = justify,
            )
            if max_width is not None:
                self._tk_label.configure(wraplength = max_width)
            self._canvas_window = context._base_canvas.create_window(
                x,
                y,
                anchor = anchor,
                window = self._tk_label,
            )
            self._text_id = None
    def destroy(self) -> int:
        count = super().destroy()
        if self._tk_label is None:
            if getattr(self, "_text_id", None):
                self._context._base_canvas.delete(self._text_id)
        else:
            if getattr(self, "_canvas_window", None):
                self._context._base_canvas.delete(self._canvas_window)
                self._canvas_window = None
        return count

class OptionCard(BaseComponent):
    """Simplified radio row rendered as a frame on the base canvas."""
    _radio_cache: dict[tuple[str | None, str | None, int], ImageTk.PhotoImage] = {}
    _background_cache: dict[tuple[int, int, int, str], ImageTk.PhotoImage] = {}

    def __init__(
            self,
            context,
            x: int,
            y: int,
            title: str,
            description: str,
            icon_path: str,
            variable: tk.StringVar,
            value: str,
            on_change,
        ):
        self._context = context
        self._canvas = context._base_canvas
        self._variable = variable
        self._value = value
        self._on_change = on_change

        self._card_width = 850
        self._card_height = 95
        self._card_radius = 14
        self._card_padding = 8
        self._default_bg = "#eaeaea"
        self._selected_bg = "#eaeaea"

        self._background_image = self._get_background_image(
            width=self._card_width,
            height=self._card_height,
            radius=self._card_radius,
            fill=self._default_bg,
        )
        self._background_id = self._canvas.create_image(
            x,
            y,
            anchor="nw",
            image=self._background_image,
        )

        inner_width = self._card_width - (2 * self._card_padding)
        inner_height = self._card_height - (2 * self._card_padding)
        self._frame = tk.Frame(
            self._canvas,
            bg=self._default_bg,
            bd=0,
            highlightthickness=0,
            height=inner_height,
            width=inner_width,
        )
        self._window_id = self._canvas.create_window(
            x + self._card_padding,
            y + self._card_padding,
            anchor="nw",
            window=self._frame,
        )

        self._icon_image = None
        self._radio_images: dict[str, ImageTk.PhotoImage] = {}

        self._build(title=title, description=description, icon_path=icon_path)

        self._frame.grid_propagate(False)
        self._frame.grid_columnconfigure(2, weight=1)
        self._frame.grid_rowconfigure(0, weight=1)
        self._frame.grid_rowconfigure(1, weight=1)

        self._update_state()

    def _build(self, title: str, description: str, icon_path: str):
        icon_full_path = project_path(icon_path)
        icon_image = Image.open(icon_full_path).resize((36, 36), Image.LANCZOS)
        self._icon_image = ImageTk.PhotoImage(icon_image)

        self._radio_images = {
            "default": self._get_radio_image(border="#94A3B8", fill=None, size=26),
            "selected": self._get_radio_image(border="#1D4ED8", fill="#1D4ED8", size=26),
        }

        self._radio_label = tk.Label(
            self._frame,
            image=self._radio_images["default"],
            bg=self._frame["bg"],
            bd=0,
        )
        self._radio_label.grid(row=0, column=0, rowspan=2, padx=(12, 10), sticky="ns")

        self._icon_label = tk.Label(self._frame, image=self._icon_image, bg=self._frame["bg"])
        self._icon_label.grid(row=0, column=1, rowspan=2, padx=(0, 14), sticky="ns")

        text_padx = (16, 0)
        text_wrap = max(300, self._card_width - (self._card_padding * 2) - 180)

        self._title_label = tk.Label(
            self._frame,
            text=title,
            font=(BaseComponent.default_font_name, 15, "bold"),
            fg="#171717",
            bg=self._frame["bg"],
            anchor="w",
        )
        self._title_label.grid(row=0, column=2, sticky="sw", padx=text_padx)

        self._description_label = tk.Label(
            self._frame,
            text=description,
            font=(BaseComponent.default_font_name, 11),
            fg="#454545",
            bg=self._frame["bg"],
            anchor="w",
            wraplength=text_wrap,
            justify="left",
        )
        self._description_label.grid(row=1, column=2, sticky="nw", padx=text_padx)

        for widget in [self._frame, self._icon_label, self._title_label, self._description_label, self._radio_label]:
            widget.bind("<Button-1>", lambda _evt, value=self._value: self._on_click(value))

    @classmethod
    def _get_background_image(cls, width: int, height: int, radius: int, fill: str) -> ImageTk.PhotoImage:
        key = (width, height, radius, fill)
        if key in cls._background_cache:
            return cls._background_cache[key]
        img = Image.new("RGBA", (width, height), (255, 255, 255, 0))
        draw = ImageDraw.Draw(img)
        draw.rounded_rectangle(
            [0, 0, width - 1, height - 1],
            radius=radius,
            fill=fill,
        )
        photo = ImageTk.PhotoImage(img)
        cls._background_cache[key] = photo
        return photo

    @classmethod
    def _get_radio_image(cls, border: str | None, fill: str | None, size: int) -> ImageTk.PhotoImage:
        key = (border, fill, size)
        if key in cls._radio_cache:
            return cls._radio_cache[key]
        scale = 4
        big_size = size * scale
        img = Image.new("RGBA", (big_size, big_size), (255, 255, 255, 0))
        draw = ImageDraw.Draw(img)
        outer_margin = 3 * scale
        inner_margin = 8 * scale
        if border:
            draw.ellipse(
                [outer_margin, outer_margin, big_size - outer_margin, big_size - outer_margin],
                outline=border,
                width=2 * scale,
                fill=(255, 255, 255, 0),
            )
        if fill:
            draw.ellipse(
                [inner_margin, inner_margin, big_size - inner_margin, big_size - inner_margin],
                outline=fill,
                width=0,
                fill=fill,
            )
        img = img.resize((size, size), Image.LANCZOS)
        photo = ImageTk.PhotoImage(img)
        cls._radio_cache[key] = photo
        return photo

    def _apply_background(self, color: str):
        self._background_image = self._get_background_image(
            width=self._card_width,
            height=self._card_height,
            radius=self._card_radius,
            fill=color,
        )
        self._canvas.itemconfig(self._background_id, image=self._background_image)
        for widget in (
            getattr(self, "_frame", None),
            getattr(self, "_icon_label", None),
            getattr(self, "_title_label", None),
            getattr(self, "_description_label", None),
            getattr(self, "_radio_label", None),
        ):
            if widget is not None:
                widget.configure(bg=color)

    def _on_click(self, value: str):
        self._variable.set(value)
        self._handle_select()

    def _handle_select(self):
        if callable(self._on_change):
            self._on_change(self._variable.get())
        self._update_state()

    def _update_state(self):
        selected = self._variable.get() == self._value
        bg = self._selected_bg if selected else self._default_bg
        self._apply_background(bg)
        self._radio_label.configure(
            image=self._radio_images["selected" if selected else "default"],
        )

    def refresh(self):
        self._update_state()

    def destroy(self) -> int:
        if getattr(self, "_radio_label", None):
            self._radio_label.destroy()
            self._radio_label = None
        if getattr(self, "_frame", None):
            self._frame.destroy()
            self._frame = None
        if getattr(self, "_canvas", None):
            if getattr(self, "_window_id", None):
                self._canvas.delete(self._window_id)
                self._window_id = None
            if getattr(self, "_background_id", None):
                self._canvas.delete(self._background_id)
                self._background_id = None
        self._background_image = None
        self._icon_image = None
        return 1

class RoundedButton(BaseComponent):
    def __init__(
            self,
            x,
            y,
            background: str  = "#0B5FB0", # "#688AFF",
            foreground: str  = "#FFFFFF",
            text      : str  = "Next",
            width     : int  = 94,
            height    : int  = 44,
            radius    : int  = 6,
            tkparent  : Any  = None,
            context   : Any  = None,
            enabled   : bool = True,
            font_size : int  = BaseComponent.font_small,
            command   : Any  = None
        ):
        self.__states__ = {}

        if not tkparent:
            tkparent = context._base_canvas
        self._tkparent = tkparent
        w  = int(str(width))
        h  = int(str(height))
        r  = int(str(radius))
        self._bg = background
        self._fg = foreground
        bg = self._bg
        fg = self._fg
        self._command = command
        self._enabled = enabled

        super().__init__(x=x, y=y, width=w, height=h, background=bg, border_radius=r)

        self._tk_cnvs = tk.Canvas(self._tkparent, width= w - 2 - r // 2, height=h - 2 - r // 2, bg=bg, highlightthickness=0)
        self._tkparent.create_window(x + 1 + w//2, y + 1 + h//2, window=self._tk_cnvs)
        self._text_id = self._tk_cnvs.create_text(0 + (w - 2 - r)//2, 0 + (h - 2 - r)//2, text=text, fill=fg, font=(BaseComponent.default_font_name, font_size))
        self.set_command(command)

    def add_state(self, state_name: str, config: dict):
        self.__states__[state_name] = config
        return self

    def set_state(self, state_name: str):
        state_config = self.__states__[state_name]
        for key, val in state_config.items():
            if key in ["background"]:
                self.set_colors(background = val)
            elif key in ["foreground"]:
                self.set_colors(foreground = val)
            elif key in ["command"]:
                self._command = val
            else:
                raise ValueError(f"{key} ---> {val}")

    def del_state(self, state_name: str):
        pass

    def set_colors(self, background: str = None, foreground: str = None):
        if background:
            self._bg = background
        if foreground:
            self._fg = foreground
        self._tkparent.itemconfig(self._tk_arc1, fill=self._bg, outline=self._bg)
        self._tkparent.itemconfig(self._tk_arc2, fill=self._bg, outline=self._bg)
        self._tkparent.itemconfig(self._tk_arc3, fill=self._bg, outline=self._bg)
        self._tkparent.itemconfig(self._tk_arc4, fill=self._bg, outline=self._bg)
        self._tkparent.itemconfig(self._tk_rct1, fill=self._bg, outline=self._bg)
        self._tkparent.itemconfig(self._tk_rct2, fill=self._bg, outline=self._bg)
        self._tk_cnvs.config(bg=self._bg)
        self._tk_cnvs.itemconfig(self._text_id, fill=self._fg)

    def set_command(self, command: "Callable"):
        self._command = command
        self._tk_cnvs.bind("<Button-1>", lambda e: self._command() if self._command else None)
        self._tk_cnvs.bind("<Enter>"   , lambda e: self._tk_cnvs.config(cursor="hand2"))
        self._tk_cnvs.bind("<Leave>"   , lambda e: self._tk_cnvs.config(cursor=""))

    def enable(self):
        if not self._enabled:
            self._enabled = True
            self._cnvs.bind("<Button-1>", lambda e: self._command())
            self._cnvs.bind("<Enter>", lambda e: self._cnvs.config(cursor="hand2"))
            self._cnvs.bind("<Leave>", lambda e: self._cnvs.config(cursor=""))
            self._set_enabled_colors()

    def disable(self):
        if self._enabled:
            self._enabled = False
            self._cnvs.unbind("<Button-1>")
            self._cnvs.unbind("<Enter>")
            self._cnvs.unbind("<Leave>")
            self._cnvs.config(cursor="")
            self._set_disabled_colors()

class ProgressBar(BaseComponent):

    def __init__(
            self,
            x,
            y,
            width       : int   = 200,
            height      : int   = 20,
            radius      : int   = 6,
            background  : str   = "#E5E5E5",
            foreground  : str   = "#0B5FB0",
            border_color: str   = "#CCCCCC",
            border_size : int   = 1,
            tkparent    : Any   = None,
            context     : Any   = None,
            value       : float = 0.0   # between 0.0 and 1.0
        ):
        self.__states__ = {}
        if not tkparent:
            tkparent = context._base_canvas
        self._tkparent = tkparent

        self._w            = int(width)
        self._h            = int(height)
        self._r            = int(radius)
        self._bg           = background
        self._fg           = foreground
        self._border_color = border_color
        self._border_size  = border_size
        self._value        = max(0.0, min(1.0, value))  # clamp between 0 and 1

        # Outer frame (background with border)
        self._tk_frame = tk.Frame(
            self._tkparent,
            width=self._w,
            height=self._h,
            bg=self._border_color,
            highlightthickness=0,
            bd=0
        )
        self._tk_frame.place(x=x, y=y)

        # Inner canvas for drawing bar
        self._tk_canvas = tk.Canvas(
            self._tk_frame,
            width=self._w - 2 * self._border_size,
            height=self._h - 2 * self._border_size,
            bg=self._bg,
            highlightthickness=0,
            bd=0
        )
        self._tk_canvas.place(x=self._border_size, y=self._border_size)

        # Full background rectangle
        self._bg_rect = self._tk_canvas.create_rectangle(
            0, 0, self._w, self._h,
            fill=self._bg,
            outline=self._bg
        )

        # Foreground progress rectangle
        self._fg_rect = self._tk_canvas.create_rectangle(
            0, 0, int(self._value * self._w), self._h,
            fill=self._fg,
            outline=self._fg
        )

    def set_value(self, value: float):
        """Set progress between 0.0 and 1.0"""
        self._value = max(0.0, min(1.0, value))
        self._tk_canvas.coords(self._fg_rect, 0, 0, int(self._value * self._w), self._h)

    def get_value(self) -> float:
        """Return current progress value"""
        return self._value

    def run_callback(self, callback: Callable, on_finish: Callable = None):
        finished: bool = False
        def worker():
            nonlocal finished
            callback()
            self.set_value(1.0)
            finished = True
        t = threading.Thread(target=worker, daemon=True)
        t.start()
        def watcher():
            if finished:
                t.join()
            if not finished:
                self._tkparent.after(100, watcher)
            else:
                if on_finish:
                    on_finish()
        self._tkparent.after(100, watcher)

class InputField(BaseComponent):
    def __init__(
            self,
            x,
            y,
            background: str  = "#F7FAFC",
            foreground: str  = "#ACACAC",
            text      : str  = "Next",
            width     : int  = 94,
            height    : int  = 44,
            radius    : int  = 6,
            tkparent  : Any  = None,
            context   : Any  = None,
            enabled   : bool = True,
            font_size : int  = BaseComponent.font_small,
            command   : Any  = None,
            text_align: str  = "left",   # NEW
        ):
        self.__states__ = {}

        if not tkparent:
            tkparent = context._base_canvas
        self._tkparent = tkparent

        w = int(width)
        h = int(height)
        r = int(radius)

        self._bg = background
        self._fg = foreground
        self._command = command
        self._enabled = enabled
        self._text_align = text_align.lower()

        bg = self._bg
        fg = self._fg

        # Rounded rectangle parts
        self._tk_arc1 = self._tkparent.create_arc((x, y, x + 2 * r, y + 2 * r), start=90, extent=90, fill=bg, outline=bg)
        self._tk_arc2 = self._tkparent.create_arc((x + w - 2 * r, y, x + w, y + 2 * r), start=0, extent=90, fill=bg, outline=bg)
        self._tk_arc3 = self._tkparent.create_arc((x, y + h - 2 * r, x + 2 * r, y + h), start=180, extent=90, fill=bg, outline=bg)
        self._tk_arc4 = self._tkparent.create_arc((x + w - 2 * r, y + h - 2 * r, x + w, y + h), start=270, extent=90, fill=bg, outline=bg)
        self._tk_rct1 = self._tkparent.create_rectangle(x + r, y, x + w - r, y + h, fill=bg, outline=bg)
        self._tk_rct2 = self._tkparent.create_rectangle(x, y + r, x + w, y + h - r, fill=bg, outline=bg)

        # Inner canvas
        self._tk_cnvs = tk.Canvas(
            self._tkparent, width=w - 2 - r // 2, height=h - 2 - r // 2,
            bg=bg, highlightthickness=0, bd=0
        )
        self._tkparent.create_window(x + 1 + w // 2, y + 1 + h // 2, window=self._tk_cnvs)

        # Text anchor mapping
        anchor_map = {"left": "w", "center": "center", "right": "e"}
        anchor = anchor_map.get(self._text_align, "w")

        # Padding inside the field
        padx = 6

        # Text widget
        if self._text_align == "left":
            xpos = padx
        elif self._text_align == "right":
            xpos = (w - 2 - r) - padx
        else:  # center
            xpos = (w - 2 - r) // 2

        ypos = (h - 2 - r) // 2

        self._text_id = self._tk_cnvs.create_text(
            xpos, ypos,
            text=text,
            fill=fg,
            font=(BaseComponent.default_font_name, font_size),
            anchor=anchor
        )

    def set_value(self, new_text: str):
        """Update the displayed text in the InputField"""
        self._tk_cnvs.itemconfig(self._text_id, text=new_text)

class CheckBoxNewVersion(BaseComponent):
    def __init__(self,
            x               : int      = 396,
            y               : int      = 436,
            size            : int      = 18,
            text            : str      = "Launch BAISS now",
            background      : str      = "#FFFFFF",
            foreground      : str      = "#000000",
            on_check        : Callable = None,
            on_uncheck      : Callable = None,
            context         : Any      = None,
            checked         : bool     = False,
            disabled        : bool     = False,
            font_name       : str      = "Arial",
            font_size       : int      = BaseComponent.default_font_size,
            font_weight     : str      = "normal",   # "normal" or "bold"
            font_slant      : str      = "roman",    # "roman" or "italic"
            font_underline  : bool     = False,
            font_overstrike : bool     = False,
        ):
            super().__init__()
            self._on_check   = on_check
            self._on_uncheck = on_uncheck
            self._disabled   = disabled
            self._foreground = foreground
            self._tkparent   = context._base_canvas

            def create_rounded_rectangle(canvas, x1, y1, x2, y2, radius=25, **kwargs):
                """
                Draws a rounded rectangle on the canvas.

                (x1, y1) = top-left
                (x2, y2) = bottom-right
                radius   = corner radius
                kwargs   = same as Canvas.create_polygon/oval options (fill, outline, width, etc.)
                """
                points = [
                    x1+radius, y1,
                    x2-radius, y1,
                    x2, y1,
                    x2, y1+radius,
                    x2, y2-radius,
                    x2, y2,
                    x2-radius, y2,
                    x1+radius, y2,
                    x1, y2,
                    x1, y2-radius,
                    x1, y1+radius,
                    x1, y1
                ]
                return canvas.create_polygon(points, smooth=True, **kwargs)



            self._tk_text = TextField(
                x          = x + size + 14,
                y          = y,
                color      = "#FFFFFF",
                text       = text,
                context    = context,
                font_name  = font_name,
                font_size  = font_size,
                font_weight= font_weight,
                font_slant = font_slant,
                underline  = font_underline,
                overstrike = font_overstrike,
                anchor     = "w"
            )

            self._tkparent.create_line(
                x + size * 0.25, y + size * 0.55 - 10,
                x + size * 0.45, y + size * 0.75 - 10,
                x + size * 0.78, y + size * 0.28 - 10,
                width=1.5, fill="white", capstyle="round", joinstyle="round", tags="check"
            )

            create_rounded_rectangle(
                self._tkparent,
                50, 50, 300, 200,
                radius=15,
                # fill="#FFFFFF",
                outline="#FFFFFF",
                width=2
            )
            self.set_checked(checked)

    def set_checked(self, checked: bool):
        self._tk_variable.set(checked)
        if checked:
            if self._disabled:
                self._tk_canvas.itemconfigure("box", fill="#E2E2E2", outline="#C0C0C0")  # macOS blue
            else:
                self._tk_canvas.itemconfigure("box", fill="#007aff", outline="#007aff")  # macOS blue
            self._tk_canvas.itemconfigure("check", state="normal")
        else:
            self._tk_canvas.itemconfigure("box", fill="white", outline="#888")
            self._tk_canvas.itemconfigure("check", state="hidden")

    def update(self, *args):
        if self._disabled:
            return
        if self._tk_variable.get():
            self.set_checked(True)
            if self._on_check:
                self._on_check()
        else:
            self.set_checked(False)
            if self._on_uncheck:
                self._on_uncheck()

class CheckBox(BaseComponent):
    def __init__(self,
            x               : int,
            y               : int,
            size            : int      = 18,
            text            : str      = "Check Box",
            background      : str      = "#FFFFFF",
            foreground      : str      = "#000000",
            on_check        : Callable = None,
            on_uncheck      : Callable = None,
            context         : Any      = None,
            checked         : bool     = False,
            disabled        : bool     = False,
            font_name       : str      = "Arial",
            font_size       : int      = BaseComponent.font_small,
            font_weight     : str      = "normal",   # "normal" or "bold"
            font_slant      : str      = "roman",    # "roman" or "italic"
            font_underline  : bool     = False,
            font_overstrike : bool     = False,
        ):
            super().__init__()
            self._on_check   = on_check
            self._on_uncheck = on_uncheck
            self._disabled   = disabled
            self._foreground = foreground
            self._tkparent   = context._base_canvas

            self._tk_variable = tk.BooleanVar(value=False)
            self._tk_frame = tk.Frame(self._tkparent, bg=background)
            self._tk_canvas = tk.Canvas(self._tk_frame, width=size, height=size,
                    highlightthickness=0, bd=0, bg=background)
            self._tk_canvas.pack(side="left")
            self._tk_label = tk.Label(self._tk_frame, text=text,
                                font = (BaseComponent.default_font_name, font_size),
                                bg=background,
                                fg=foreground,
                                anchor="w")
            self._tk_label.pack(side="left", padx=(6, 0))
            self._tk_canvas.create_rectangle(
                1, 1, size-1, size-1,
                outline="#888", width=1.2, fill="white", tags="box"
            )
            self._tk_canvas.create_line(
                size*0.25, size*0.55, size*0.45, size*0.75, size*0.78, size*0.28,
                width=1.5, fill="white", capstyle="round", joinstyle="round", tags="check"
            )
            for w in (self._tk_canvas, self._tk_label):
                w.bind("<Button-1>", lambda *args: self._tk_variable.set(not self._tk_variable.get()))
            try:
                self._tk_variable.trace_add("write", self.update)
            except:
                self._tk_variable.trace("w", self.update)
            self.update()
            self._tk_frame.place(x=x, y=y)
            self.set_checked(checked)

    def set_checked(self, checked: bool):
        self._tk_variable.set(checked)
        if checked:
            if self._disabled:
                self._tk_canvas.itemconfigure("box", fill="#E2E2E2", outline="#C0C0C0")  # macOS blue
            else:
                self._tk_canvas.itemconfigure("box", fill="#007aff", outline="#007aff")  # macOS blue
            self._tk_canvas.itemconfigure("check", state="normal")
        else:
            self._tk_canvas.itemconfigure("box", fill="white", outline="#888")
            self._tk_canvas.itemconfigure("check", state="hidden")

    def update(self, *args):
        if self._disabled:
            return
        if self._tk_variable.get():
            self.set_checked(True)
            if self._on_check:
                self._on_check()
        else:
            self.set_checked(False)
            if self._on_uncheck:
                self._on_uncheck()

class Frame(BaseComponent):
    def __init__(
            self,
            x           : int = 0,
            y           : int = 20,
            width       : int = 200,
            height      : int = 60,
            background  : str = "#FFFFFF",
            border_color: str = "#D0D0D0",
            border_width: int = 1,
            border_radius: int = 14,
            context     : Any = None,
        ):
        self._tkparent = context._base_canvas

        # Create simple rounded rectangle
        def create_rounded_rectangle(canvas, x1, y1, x2, y2, radius=8, **kwargs):
            points = []
            for x, y in [(x1, y1 + radius), (x1, y1), (x1 + radius, y1),
                        (x2 - radius, y1), (x2, y1), (x2, y1 + radius),
                        (x2, y2 - radius), (x2, y2), (x2 - radius, y2),
                        (x1 + radius, y2), (x1, y2), (x1, y2 - radius)]:
                points.extend([x, y])
            return canvas.create_polygon(points, smooth=True, **kwargs)

        # Handle empty background (transparent effect)
        fill_color = background if background else ""

        self._tk_frame = create_rounded_rectangle(
            self._tkparent, x, y, x + width, y + height,
            radius=border_radius, fill=fill_color,
            outline=border_color, width=border_width
        )

class MarkdownViewer(BaseComponent):
    def __init__(
            self,
            x,
            y,
            background: str  = "#E5E5E5",
            foreground: str  = "#ACACAC",
            text      : str  = "Hello, World!",
            width     : int  = 94,
            height    : int  = 44,
            radius    : int  = 6,
            context   : Any  = None,
            font_size : int  = BaseComponent.font_small,
        ):

        self.__states__ = {}
        self._tkparent = context._base_canvas
        w  = int(str(width))
        h  = int(str(height))
        r  = int(str(radius))
        self._bg = background
        self._fg = foreground
        bg = self._bg
        fg = self._fg

        # Create rounded corners using the same method as other components
        super().__init__(x=x, y=y, width=w, height=h, background=bg, border_radius=r)

        # frame & text widget with proper insets to respect rounded borders
        self._tk_frame = tk.Frame(self._tkparent, bg=self._bg, highlightthickness=0, bd=0)
        self._tk_text = tk.Text(
            self._tk_frame,
            wrap        = "word",
            undo        = False,
            bg          = self._bg,
            relief      = "flat",
            borderwidth = 0,
            highlightthickness = 0,
        )
        self._tk_text.pack(side="left", fill="both", expand=True, padx=8, pady=8)

        # scrollbar with matching background
        self._tk_scrollbar = tk.Scrollbar(
            self._tk_frame,
            command=self._tk_text.yview,
            bg=self._bg,  # Match background
            troughcolor=self._bg,  # Match trough color
            activebackground=self._bg  # Match active background
        )
        self._tk_scrollbar.pack(side="right", fill="y")
        self._tk_text.configure(yscrollcommand=self._tk_scrollbar.set)

        # disable editing
        self._tk_text.configure(state="disabled")
        self._tk_text.bind("<FocusIn>", lambda e: e.widget.master.focus())

        # place frame with minimal insets
        inset = 2  # Small inset for clean appearance
        self._tk_frame.place(x=x + inset, y=y + inset, width=w - 2 * inset, height=h - 2 * inset)

        # configure fonts for markdown headers using a more compact hierarchy
        # make base text and headings smaller for dense legal content
        self._tk_base_font = tkFont.Font(family="Plus Jakarta Sans", size=BaseComponent.font_tiny)
        # map h1->h2, h2->h3, h3->body to reduce perceived size
        self._tk_h1_font   = tkFont.Font(family="Plus Jakarta Sans", size=BaseComponent.font_h2, weight="bold")
        self._tk_h2_font   = tkFont.Font(family="Plus Jakarta Sans", size=BaseComponent.font_h3, weight="bold")
        self._tk_h3_font   = tkFont.Font(family="Plus Jakarta Sans", size=BaseComponent.font_body, weight="bold")
        self._tk_h4_font   = tkFont.Font(family="Plus Jakarta Sans", size=BaseComponent.font_small)
        self._tk_h5_font   = tkFont.Font(family="Plus Jakarta Sans", size=BaseComponent.font_tiny)

        self._tk_text.tag_configure("h1", font=self._tk_h1_font, foreground=foreground)
        self._tk_text.tag_configure("h2", font=self._tk_h2_font, foreground=foreground)
        self._tk_text.tag_configure("h3", font=self._tk_h3_font, foreground=foreground)
        self._tk_text.tag_configure("h4", font=self._tk_h4_font, foreground=foreground)
        self._tk_text.tag_configure("h5", font=self._tk_h5_font, foreground=foreground)
        self._tk_text.tag_configure("p",  font=self._tk_base_font, foreground=fg)

        # insert markdown text
        self.insert_markdown(text)

    def insert_markdown(self, text: str):
        """Parse simple markdown (#, ##, ###) and insert styled text"""
        self._tk_text.configure(state="normal")
        self._tk_text.delete("1.0", "end")
        for line in text.splitlines():
            if line.startswith("##### "):
                self._tk_text.insert("end", line[6:] + "\n", "h5")
            elif line.startswith("#### "):
                self._tk_text.insert("end", line[5:] + "\n", "h4")
            elif line.startswith("### "):
                self._tk_text.insert("end", line[4:] + "\n", "h3")
            elif line.startswith("## "):
                self._tk_text.insert("end", line[3:] + "\n", "h2")
            elif line.startswith("# "):
                self._tk_text.insert("end", line[2:] + "\n", "h1")
            else:
                self._tk_text.insert("end", line + "\n", "p")
        self._tk_text.configure(state="disabled")

# - - - - - - - - - - - - - - - - - - <endof> components </endof> - - - - - - - - - - - - - - - - - - #

# - - - - - - - - - - - - - - - - - - <start> utils </start> - - - - - - - - - - - - - - - - - - #

# - - - - - - - - - - - - - - - - - - <endof> utils </endof> - - - - - - - - - - - - - - - - - - #
class BaseView:
    design_screenshot_path: str = None
    __cache__: dict = {
        "design_screenshots": {}
    }
    def __init__(self, context):
        self._context  = context
        w: int = self._context._width
        h: int = self._context._height
        k: str = f"{w}-{h}"
        m: str = "design_screenshots"
        p: str = self.design_screenshot_path
        BaseView.__cache__[m][k] = BaseView.__cache__[m].get(k, {})
        if not (p in BaseView.__cache__[m][k]):
            try:
                BaseView.__cache__[m][k][p] = ImageTk.PhotoImage(
                    Image.open(project_path(p)).resize((w, h))
                )
            except Exception as e:
                print(f"Error loading image: {e}")
    def destroy(self) -> int:
        """
            Get all attributes of the class instance, that isintance of [BaseComponent, RoundedButton]
        """
        count: int = 0
        for attr in dir(self):
            for element in [BaseComponent, RoundedButton]:
                try:
                    if isinstance(getattr(self, attr), element):
                        getattr(self, attr).destroy()
                        count += 1
                except:
                    pass
                try:
                    if issubclass(type(getattr(self, attr)), element):
                        getattr(self, attr).destroy()
                        count += 1
                except:
                    pass
        return count
    def render(self):
        self._context.clear_view()
        w: int = self._context._width
        h: int = self._context._height
        k: str = f"{w}-{h}"
        m: str = "design_screenshots"
        p: str = self.design_screenshot_path
        try:
            self._context._base_canvas.itemconfig(
                self._context._base_area,
                image = BaseView.__cache__[m][k][p]
            )
        except Exception as e:
            print(f"Error rendering image: {e}")
            self._context._base_canvas.delete("all")
            self._context._base_area = self._context._base_canvas.create_image(0, 0, anchor = "nw")

    def after_render(self):
        pass

class BaissSideBar(BaseComponent):

    def __init__(self, context: "Any", step: int, step_title: str = None):

        items: List[str] = ["Welcome", "License Agreement", "Installation Folder", "Installation", "Finish"]
        top_y: int = 36
        self.items = []
        for index in range(len(items)):
            _title: str = items[index]
            if (index + 1) == step and step_title:
                _title = step_title
            item = InputField(
                x          = 30 + 0,
                y          = top_y + index * 41,
                width      = 180,
                height     = 40,
                radius     = 3,
                background = "#E2E2E2" if index == (step - 1) else "#FFFFFF",
                foreground = "#050D14" if index == (step - 1) else "#8B8B8B",
                context    = context,
                text_align = "left",
                text       = _title + (" ✓" if index < step - 1 else "" if index == step - 1 else "" ),
            )
            self.items.append(item)

    def destroy(self) -> int:
        count: int = 0
        for item in self.items:
            count += item.destroy()
        return count

# - - - - - - - - - - - - - <start> step-1 </start> - - - - - - - - - - - - - #

class BaissStepView(BaseView):
    pass

class Step1View(BaissStepView):
    step_number           : int = 1
    sidebar_title         : str = "Welcome"
    sidebar_description   : str = "Begin installation"
    design_screenshot_path: str = "assets/img/background.jpg"

    def render(self):
        super().render()

        logo_img = Image.open(project_path("assets/img/logo.png"))
        logo_img = logo_img.resize((240, 60))
        self._logo_image = ImageTk.PhotoImage(logo_img)
        self._logo_id = self._context._base_canvas.create_image(480, 300, image=self._logo_image)


        # Welcome title (centered)
        self._title = TextField(
            x          = 480,  # Center of 960px width
            y          = 365,
            color      = "#FFFFFF",
            context    = self._context,
            text       = "Welcome to the Setup Wizard",
            font_size  = BaseComponent.font_h1,
            font_name  = "Plus Jakarta Sans",
            font_weight= "bold",
            anchor     = "center"
        )

        # Description (centered)
        self._description = TextField(
            x          = 480,  # Center of 960px width
            y          = 400,
            color      = "#A5CFE5",
            context    = self._context,
            text       = "This wizard will guide you through the installation of the BAISS desktop app.",
            font_size  = BaseComponent.font_small,
            font_name  = "Plus Jakarta Sans",
            anchor     = "center"
        )

        # Start button (centered)
        self._button_next = RoundedButton(
            x          = 440,
            y          = 432,
            width      = 80,
            context    = self._context,
            text       = "Start",
            command    = lambda: self._context.view(Step2View)
        )
# - - - - - - - - - - - - - <endof> step-1 </endof> - - - - - - - - - - - - - #

# - - - - - - - - - - - - - <start> step-2 </start> - - - - - - - - - - - - - #
class Step2View(BaissStepView):
    design_screenshot_path: str = "assets/img/step-2.png2"

    def render(self):
        super().render()
        self._cancel_button2 = MarkdownViewer(
            context    = self._context,
            x          = 246,
            y          = 200,
            width      = 680,
            height     = 330,
            radius     = 8,
            background = "#EEEEEE",
            foreground = "#121417",
            font_size  = BaseComponent.font_tiny,
            text       = open(project_path("assets/md/LICENSE.md"), "r", encoding="utf-8").read(),
        )
        self._cancel_button = RoundedButton(
            context    = self._context,
            x          = 730,
            y          = 672,
            background = "#E5EBF6",
            foreground = "#0B5FB0",
            text       = "Cancel",
            command    = self._context.cancel,
        )
        self._next_button = RoundedButton(
            context    = self._context,
            x          = 837,
            y          = 673,
        )
        self._next_button.add_state("enabled" , {"background": "#0B5FB0", "foreground": "#FFFFFF", "command": lambda: self._context.view(Step3View)})
        self._next_button.add_state("disabled", {"background": "#D9D9D9", "foreground": "#8B8B8B", "command": None})
        self._next_button.set_state("disabled")

        # Add frame around the checkbox area with full width
        self._checkbox_frame = Frame(
            context      = self._context,
            x            = 250,
            y            = 560,
            width        = 686,
            height       = 50,
            background   = "#FFFFFF",
            border_color = "#D0D0D0",
            border_width = 1,
            border_radius = 12,
        )

        self._agree_checkbox = CheckBox(
            context      = self._context,
            x            = 265,
            y            = 570,
            text         = "I accept the terms in the License Agreement",
            on_check     = lambda: self._next_button.set_state("enabled"),
            on_uncheck   = lambda: self._next_button.set_state("disabled"),
        )
        self._sidebar = BaissSideBar(self._context, step = 2)
        self._title = TextField(
            x          = 250,
            y          = 36,
            color      = "#000000",
            context    = self._context,
            text       = "License Agreement",
            font_size  = BaseComponent.font_h1
        )
        self._info_message = TextArea(
            x           = 238,
            y           = 85,
            context     = self._context,
            text        = "Please read the following license agreement carefully.\nThis is a legal agreement between you and BAISS. By clicking 'I accept the terms in the License Agreement' below, you agree to be bound by the terms of this agreement. If you do not agree to the terms of this agreement, click 'I do NOT accept the terms in the License Agreement'.",
            width       = 686,
            background  = "#FFFFFF",
            foreground  = "#050D14",
            font_size   = BaseComponent.font_tiny,
        )

# - - - - - - - - - - - - - <endof> step-2 </endof> - - - - - - - - - - - - - #

# - - - - - - - - - - - - - <start> step-3 </start> - - - - - - - - - - - - - #

class Step3View(BaissStepView):
    step_number           : int = 3
    sidebar_title         : str = "Installation folder"
    sidebar_description   : str = "Choose folder and options"
    design_screenshot_path: str = "assets/img/step-3.png5"

    def browse_folder(self):
        folder_name: str = filedialog.askdirectory(
            title = "Select folder to install app"
        )
        if not isinstance(folder_name, str) or not os.path.exists(folder_name):
            return
        folder_name = self._context._package_installer.set_destination_path(folder_name)
        if self._input:
            self._input.set_value(folder_name)

    def next_step(self):
        # check if installation path exists, if it is, switch to repair view
        destination_path: str = self._context._package_installer.get_destination_path()
        if os.path.exists(destination_path):
            self._context.view(ProgramMaintenanceView)
        else:
            self._context.view(Step4View)

    def render(self):
        super().render()
        self._title = TextField(
            x           = 246,
            y           = 38,
            color       = "#050D14",
            context     = self._context,
            text        = "Installation Folder",
            font_name   = "Plus Jakarta Sans",
            font_size   = BaseComponent.font_h1,
        )
        self._description = TextField(
            x           = 246,
            y           = 84,
            color       = "#050D14",
            context     = self._context,
            text        = "Setup will install BAISS in the following folder.",
            font_name   = "Plus Jakarta Sans",
            font_size   = 13,
        )
        self._input = InputField(
            x          = 245,
            y          = 120,
            width      = 432,
            height     = 40,
            context    = self._context,
            text       = self._context._config.get_install_path()
        )

        # Add blue border frame around the input field
        self._input_border_frame = Frame(
            context      = self._context,
            x            = 243,
            y            = 118,
            width        = 436,
            height       = 44,
            background   = "",
            border_color = "#c4ced8",
            border_width = 1,
            border_radius = 6,
        )
        self._browse_button = RoundedButton(
            context    = self._context,
            x          = 686,
            y          = 120,
            width      = 92,
            height     = 42,
            font_size  = BaseComponent.font_tiny,
            text       = "Browse...",
            command    = self.browse_folder
        )
        # self._available_space = TextField(
        #     x           = 246,
        #     y           = 148,
        #     color       = "#808B96",
        #     context     = self._context,
        #     text        = "Required: 450 MB   Available: 73 GB",
        #     font_name   = "Plus Jakarta Sans",
        #     font_size   = BaseComponent.font_small,
        # )
        self._hint = TextField(
            x           = 245,
            y           = 340,
            color       = "#6B7582",
            context     = self._context,
            text        = "You can change these extras later in Settings.",
            font_name   = "Plus Jakarta Sans",
            font_size   = BaseComponent.font_tiny,
        )
        self._options = TextArea(
            x           = 238,
            y           = 196,
            context     = self._context,
            text        = "Additional Options:",
            width       = 686,
            background  = "#FFFFFF",
            foreground  = "#050D14",
            font_family = "Plus Jakarta Sans",
            font_size   = BaseComponent.font_body,
        )
        self._back_button = RoundedButton(
            context    = self._context,
            x          = 246,
            y          = 673,
            text       = "Back",
            command    = lambda: self._context.view(Step2View),
        )
        self._cancel_button = RoundedButton(
            context    = self._context,
            x          = 542,
            y          = 672,
            background = "#E5EBF6",
            foreground = "#0B5FB0",
            text       = "Cancel",
            command    = self._context.cancel,
        )
        self._next_button = RoundedButton(
            context    = self._context,
            x          = 837,
            y          = 673,
            text       = "Next",
            command    = self.next_step
        )
        self._option_baiss_app = CheckBox(
            context      = self._context,
            x            = 246,
            y            = 229,
            text         = "BAISS App",
            foreground   = "#C0C0C0",
            checked      = True,
            disabled     = True,
            font_size    = BaseComponent.font_small,
        )
        self._add_desktop_shortcut = CheckBox(
            context      = self._context,
            x            = 246,
            y            = 267,
            checked      = True,
            text         = "Create a desktop Shortcut",
            on_check     = lambda *args : self._context._config.set_desktop_shortcut(True),
            on_uncheck   = lambda *args : self._context._config.set_desktop_shortcut(False),
        )
        self._context._config.set_desktop_shortcut(True)
        self._add_to_launchpad = CheckBox(
            context      = self._context,
            x            = 246,
            y            = 304,
            text         = "Add BAISS to the Start Menu",
            on_check     = lambda *args : self._context._config.set_configure_start_menu(True),
            on_uncheck   = lambda *args : self._context._config.set_configure_start_menu(False),
        )
        self._sidebar = BaissSideBar(self._context, step = 3)

# - - - - - - - - - - - - - <endof> step-3 </endof> - - - - - - - - - - - - - #

# - - - - - - - - - - - - - <start> step-4 </start> - - - - - - - - - - - - - #
class Step4View(BaissStepView):
    step_number           : int = 4
    sidebar_title         : str = "Installation"
    sidebar_description   : str = "Monitor installation progress"
    design_screenshot_path: str = "assets/img/step-4.png5"
    description_text      : str = "Installing files ..."
    current_file_prefix   : str = "Installing file: "

    def render(self):
        super().render()
        self._title = TextField(
            x           = 246,
            y           = 38,
            color       = "#050D14",
            context     = self._context,
            text        = self.sidebar_title,
            font_name   = "Plus Jakarta Sans",
            font_size   = BaseComponent.font_h1,
        )
        self._description = TextField(
            x           = 246,
            y           = 80,
            color       = "#050D14",
            context     = self._context,
            text        = self.description_text,
            font_name   = "Plus Jakarta Sans",
            font_size   = BaseComponent.font_body,
        )
        self._progress_bar = ProgressBar(
            context  = self._context,
            x        = 245,
            y        = 123,
            width    = 682,
            height   = 8
        )
        self._progress = InputField(
            x          =  890,
            y          =  90,
            width      =  50,
            height     =  34,
            background = "#FFFFFF",
            context    =  self._context,
            text_align = "center",
            text       = "0%",
        )
        self._current_file = InputField(
            x          =  245,
            y          =  144,
            width      =  682,
            height     =  34,
            background = "#FFFFFF",
            context    =  self._context,
            text_align = "center",
            text       = ""
        )
        self._cancel_button = RoundedButton(
            context    = self._context,
            x          = 540,
            y          = 192,
            height     = 34,
            background = "#E2E8F0",
            foreground = "#1F2933",
            text       = "Cancel",
            command    = self._context.cancel,
        )
        self._sidebar = BaissSideBar(self._context, step = 4, step_title = self.sidebar_title)

    def after_render(self):
        if hasattr(self, "_progress_bar") and hasattr(self, "progress_callback"):
            self._progress_bar.run_callback(self.progress_callback, on_finish = self.finish_progress)

    def progress_callback(self):
        def _callback(member, progress):
            progress = min(100, max(0, int(str(progress), 10)))
            self._progress.set_value(f"{progress}%")
            self._current_file.set_value(self.current_file_prefix + member.filename)
            self._progress_bar.set_value(progress / 100.0)
        self._context._package_installer.install(_callback)

    def finish_progress(self):
        self._cancel_button.destroy()
        self._current_file.set_value("Done!")
        self._progress.set_value("100%")
        self._progress_bar.set_value(1.0)
        self._description.set_value("Installation Complete")
        if self._context._config.get_desktop_shortcut():
            self._context._package_installer.create_desktop_shortcut()
        if self._context._config.get_configure_start_menu():
            self._context._package_installer.configure_start_menu()
        self._context.view(OnFinishView)

# - - - - - - - - - - - - - <endof> step-4 </endof> - - - - - - - - - - - - - #

# - - - - - - - - - - - - - <start> error-view </start> - - - - - - - - - - - - - #
class ErrorView(BaissStepView):

    design_screenshot_path: str = "assets/img/error-view.png"

# - - - - - - - - - - - - - <endof> error-view </endof> - - - - - - - - - - - - - #

# - - - - - - - - - - - - - <start> ProgramMaintenanceView </start> - - - - - - - - - - - - - #
class ProgramMaintenanceView(BaseView):
    step_number: int = 1
    sidebar_title: str = "Welcome"
    sidebar_description: str = "Begin installation"
    design_screenshot_path: str = "assets/img/program-maintenance-view.png"
    def __init__(self, context):
        super().__init__(context = context)
        self._title_label: CanvasLabel | None = None
        self._subtitle_label: CanvasLabel | None = None
        self._option_cards: list[OptionCard] = []
        self._cancel_button: RoundedButton | None = None
        self._next_button: RoundedButton | None = None
        self._selected_var = tk.StringVar()
        self._options = [
            {
                "id": "upgrade",
                "title": "Upgrade BAISS",
                "description": "Install a different version of BAISS. (Current: v1.8.2)",
                "icon": "assets/icons/upgdrade-icon.png",
            },
            {
                "id": "repair",
                "title": "Repair installation",
                "description": "Fix issues with your current installation.",
                "icon": "assets/icons/repair-icon.png",
            },
            {
                "id": "uninstall",
                "title": "Uninstall BAISS",
                "description": "Remove BAISS from your computer.",
                "icon": "assets/icons/uninstall-icon.png",
            },
        ]
        self._options_by_id = {option["id"]: option for option in self._options}
        self._selected_option = self._options[0]["id"] if self._options else None
        if self._selected_option:
            self._selected_var.set(self._selected_option)

    def render(self):
        super().render()
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 70,
            y          = 40,
            text       = "Program Maintenance",
            font_size  = 22,
            weight     = "bold",
            background = "#f3f3f3",
            foreground = "#171717",
        )
        self._subtitle_label = CanvasLabel(
            context    = self._context,
            x          = 70,
            y          = 80,
            text       = "Update, fix or remove the application.",
            font_size  = 12,
            background = "#f3f3f3",
            foreground = "#4B5563",
        )

        self._option_cards = []
        start_y = 140
        spacing = 20
        for index, option in enumerate(self._options):
            card_y = start_y + index * (105 + spacing)
            card = OptionCard(
                context   = self._context,
                x         = 60,
                y         = card_y,
                title     = option["title"],
                description = option["description"],
                icon_path = option["icon"],
                variable  = self._selected_var,
                value     = option["id"],
                on_change = self._on_option_selected,
            )
            self._option_cards.append(card)

        button_y = self._context._height - 88
        button_height = 44
        next_width = 90
        cancel_width = 120
        next_x = 800
        cancel_x = 650

        self._cancel_button = RoundedButton(
            context    = self._context,
            x          = cancel_x,
            y          = button_y,
            width      = cancel_width,
            height     = button_height,
            font_size  = 13,
            background = "#E3E9F4",
            foreground = "#1F2937",
            text       = "Cancel",
            command    = self._context.cancel,
        )
        self._next_button = RoundedButton(
            context    = self._context,
            x          = next_x,
            y          = button_y,
            width      = next_width,
            height     = button_height,
            font_size  = 13,
            background = "#0B5FB0",
            foreground = "white",
            text       = "Next",
            command    = self._on_next,
        )
        if self._selected_option is None:
            self._next_button.disable()

    def _on_option_selected(self, option_id: str):
        self._selected_option = option_id
        for card in self._option_cards:
            card.refresh()
        if self._next_button:
            self._next_button.enable()

    def _on_next(self):
        option = self._options_by_id.get(self._selected_option)
        if option:
            title = option['title'].lower()
            # messagebox.showinfo("BAISS Maintenance", title)
            if "upgrade" in title:
                self._context.view(UpgradeView)
            elif "repair" in title:
                self._context.view(RepairView)
            elif "uninstall" in title:
                self._context.view(UninstallView)

    def destroy(self) -> int:
        for card in getattr(self, "_option_cards", []):
            card.destroy()
        self._option_cards.clear()
        return super().destroy()

# - - - - - - - - - - - - - <endof> ProgramMaintenanceView </endof> - - - - - - - - - - - - - #

# - - - - - - - - - - - - - <start> UpgradeView </start> - - - - - - - - - - - - - #
class UpgradeView(Step4View):
    sidebar_title         : str = "Upgrading BAISS"
    sidebar_description   : str = "Please wait while BAISS is upgrading. Your data and settings will be preserved."
    description_text      : str = "Please wait while BAISS is upgrading. Your data and settings will be preserved."
    current_file_prefix   : str = "Upgrading: "

    def render(self):
        super().render()
# - - - - - - - - - - - - - <endof> UpgradeView </endof> - - - - - - - - - - - - - #

# - - - - - - - - - - - - - <start> RepairView </start> - - - - - - - - - - - - - #
class RepairView(Step4View):
    sidebar_title         : str = "Repairing BAISS"
    sidebar_description   : str = "Please wait while BAISS is repairing. We will preserve your data and settings."
    description_text      : str = "Please wait while BAISS is repairing. We will preserve your data and settings."
    current_file_prefix   : str = "Repairing: "

    def render(self):
        super().render()
# - - - - - - - - - - - - - <endof> RepairView </endof> - - - - - - - - - - - - - #

# - - - - - - - - - - - - - <start> UninstallView </start> - - - - - - - - - - - - - #
class UninstallView(Step4View):
    sidebar_title         : str = "Uninstalling BAISS"
    sidebar_description   : str = "Please wait while BAISS is uninstalling."
    description_text      : str = "Please wait while BAISS is uninstalling."
    current_file_prefix   : str = "Uninstalling: "
    def render(self):
        self._context.view(UninstallCompleteView)
        # raise NotImplementedError("UninstallView is not implemented yet.")
        # super().render()
# - - - - - - - - - - - - - <endof> UninstallView </endof> - - - - - - - - - - - - - #

# - - - - - - - - - - - - - <start> OnFinishView </start> - - - - - - - - - - - - - #
class OnFinishView(BaseView):
    step_number               : int = 5
    sidebar_title             : str = "Finish"
    sidebar_description       : str = "Finish setup"
    design_screenshot_path    : str = "assets/img/background.jpg"
    on_complete_message       : str = "Baiss has been successfully installed on your computer."
    on_complete_title         : str = "Setup Complete"
    on_complete_launch_message: str = "Launch BAISS now"

    def __init__(self, context):
        super().__init__(context = context)
        self._launch_checkbox = None
        self._finish_button = None
        self._title_label = None
        self._message_label = None
        self._logo_image = None
        self._logo_id = None

    def _on_launch_toggle(self, checked):
        self._context._config.set_launch_after_setup(checked)

    def destroy(self) -> int:
        count = super().destroy()
        if self._logo_id is not None:
            try:
                self._context._base_canvas.delete(self._logo_id)
            except tk.TclError:
                pass
            self._logo_id = None
        return count

    def render(self):
        super().render()
        canvas = self._context._base_canvas
        center_x = self._context._width // 2
        if self.on_complete_title:
            self._title_label = CanvasLabel(
                context    = self._context,
                x          = center_x,
                y          = 270,
                text       = self.on_complete_title,
                font_size  = 20,
                weight     = "bold",
                background = None,
                foreground = "#FFFFFF",
                anchor     = "center",
            )
        logo_img = Image.open(project_path("assets/img/logo.png"))
        max_logo_width = 240
        if logo_img.width > max_logo_width:
            ratio = max_logo_width / logo_img.width
            logo_img = logo_img.resize(
                (int(logo_img.width * ratio), int(logo_img.height * ratio)),
                Image.LANCZOS,
            )
        self._logo_image = ImageTk.PhotoImage(logo_img)
        self._logo_id = canvas.create_image(center_x, 330, image=self._logo_image)
        if self.on_complete_message:
            self._message_label = CanvasLabel(
                context    = self._context,
                x          = center_x,
                y          = 390,
                text       = self.on_complete_message,
                font_size  = 13,
                background = None,
                foreground = "#E5E7EB",
                anchor     = "center",
            )
        if self.on_complete_launch_message:
            initial_checked = self._context._config.get_launch_after_setup()
            self._context._config.set_launch_after_setup(initial_checked)
            self._launch_checkbox = CanvasCheckButton(
                context                   = self._context,
                x                         = center_x - 80,
                y                         = 425,
                text                      = self.on_complete_launch_message,
                command                   = self._on_launch_toggle,
                initial_checked           = initial_checked,
                font_size                 = 13,
                foreground                = "#FFFFFF",
                indicator_size            = 24,
                indicator_outline         = "#FFFFFF",
                indicator_fill            = "#0B5FB0",
                indicator_unchecked_fill  = "",
                check_color               = "#FFFFFF",
                spacing                   = 12,
            )
        button_width = 90
        button_height = 46
        self._finish_button = RoundedButton(
            context    = self._context,
            x          = center_x - button_width // 2,
            y          = 470,
            width      = button_width,
            height     = button_height,
            font_size  = 14,
            background = "#0B5FB0",
            foreground = "white",
            text       = "Finish",
            command    = self._context.finish,
        )
# - - - - - - - - - - - - - <endof> OnFinishView </endof> - - - - - - - - - - - - - #

# - - - - - - - - - - - - - <start> UpgradeCompleteView </start> - - - - - - - - - - - - - #
class UpgradeCompleteView(OnFinishView):
    on_complete_message       : str = "Your app has been successfully upgraded."
    on_complete_title         : str = "Upgrade Complete"
# - - - - - - - - - - - - - <endof> UpgradeCompleteView </endof> - - - - - - - - - - - - - #

# - - - - - - - - - - - - - <start> DowngradeCompleteView </start> - - - - - - - - - - - - - #
class DowngradeCompleteView(OnFinishView):
    on_complete_message       : str = "Your app has been successfully downgraded."
    on_complete_title         : str = "Downgrade Complete"
# - - - - - - - - - - - - - <endof> DowngradeCompleteView </endof> - - - - - - - - - - - - - #

# - - - - - - - - - - - - - <start> repair-complete-view </start> - - - - - - - - - - - - - #
class RepairCompleteView(OnFinishView):
    on_complete_message        : str = "Your app has been successfully repaired."
    on_complete_title          : str = "Repair Complete"
# - - - - - - - - - - - - - <endof> repair-complete-view </endof> - - - - - - - - - - - - - #

# - - - - - - - - - - - - - <start> uninstall-complete-view </start> - - - - - - - - - - - - - #
class UninstallCompleteView(OnFinishView):
    on_complete_title          : str = "Uninstall Complete"
    on_complete_message        : str = "The application has been successfully uninstalled."
    on_complete_launch_message : str = None
# - - - - - - - - - - - - - <endof> uninstall-complete-view </endof> - - - - - - - - - - - - - #

class BaissSetupWizard:

    def __init__(self,
        width : int = 960,
        height: int = 760,
        title : str = "Baiss Setup Wizard"
    ):
        # Enable DPI awareness for better font rendering on high-DPI displays
        try:
            if os.name == 'nt':  # Windows
                ctypes.windll.shcore.SetProcessDpiAwareness(1)  # System DPI aware
        except:
            pass

        self._tkroot = tk.Tk()
        self._tkroot.title(title)
        self._tkroot.configure(bg="#FFFFFF")
        self._tkroot.iconbitmap(project_path("assets/ico/favicon.ico"))
        self._screen_width      = self._tkroot.winfo_screenwidth()
        self._screen_height     = self._tkroot.winfo_screenheight()
        self._width             = width
        self._height            = height
        self._current_view      = None
        self._package_installer = BaissPackageInstaller()
        self._config            = BaissConfig(self._package_installer)
        x = int((self._screen_width  / 2) - (self._width  / 2))
        y = int((self._screen_height / 2) - (self._height / 2))
        self._tkroot.geometry(f"{self._width}x{self._height}+{x}+{y}")
        self._tkroot.resizable(False, False)
        self._base_canvas = tk.Canvas(self._tkroot, width=self._width, height=self._height, highlightthickness=0, bg = "#FFFFFF")
        self._base_canvas.pack(fill = "both", expand = True)
        self._base_area = self._base_canvas.create_image(0, 0, anchor = "nw")

    def clear_view(self):
        """Clear all existing buttons and elements from the view."""
        if self._current_view:
            self._current_view.destroy()

    def finish(self):
        # Check if user wants to launch the app after setup
        if self._config.get_launch_after_setup():
            self._package_installer.launch_application()

        self._tkroot.quit()
        self._tkroot.destroy()
        sys.exit(0)

    def cancel(self):
        self._tkroot.quit()
        self._tkroot.destroy()
        sys.exit(0)

    def view(self, view_object: BaseView):
        self.clear_view()
        next_view = view_object(context = self)
        next_view.render()
        next_view.after_render()
        self._current_view = next_view
        return self._current_view

    def run(self):
        self.view(Step1View)
        self._tkroot.mainloop()

if __name__ == "__main__":
    app = BaissSetupWizard()
    app.run()
