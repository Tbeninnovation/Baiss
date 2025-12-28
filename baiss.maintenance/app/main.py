"""
    File: main.py
    Requirements:
        - Python 3.10+
        - python -m pip install Pillow
    Instructions:
        - Write Everything in in this file, not other files are allowed.
        - To run the installer, exec        self._description_label = tk.Label(
            self._frame,
            text=description,
            font=(BaseComponent.default_font_name, 11),
            fg="#4B5563",
            bg=self._frame["bg"],
            anchor="w",
            wraplength=650,
            justify="left",
        )n main.py
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
import subprocess
import threading
import tkinter as tk
from PIL import Image, ImageTk, ImageDraw
from tkinter import filedialog, messagebox
from tkinter import font as tkfont
from tkinter.ttk import *
# - - - - - - - - - - - - - - - - - - <start> components </start> - - - - - - - - - - - - - - - - - - #
class BaseComponent:
    default_font_name: str = "Arial"
    default_font_size: int = 15
    def destroy(self) -> int:
        """
            Destroy all attributes of the class instance, that starts with '_tk_'
        """
        count: int = 0
        for attr_name in dir(self):
            if attr_name.startswith('_tk_'):
                try:
                    getattr(self, attr_name).destroy()
                    count += 1
                except:
                    pass
        return count
class RoundedButton(BaseComponent):
    def __init__(self, x, y, width, height, background, foreground, text, command, radius: int = 10, tkparent = None, context = None, enabled: bool = True, font_size: int = BaseComponent.default_font_size):
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
        self._arc1 = self._tkparent.create_arc((x, y, x + 2 * r, y + 2 * r), start=90, extent=90, fill=bg, outline=bg)
        self._arc2 = self._tkparent.create_arc((x + w - 2 * r, y, x + w, y + 2 * r), start=0, extent=90, fill=bg, outline=bg)
        self._arc3 = self._tkparent.create_arc((x, y + h - 2 * r, x + 2 * r, y + h), start=180, extent=90, fill=bg, outline=bg)
        self._arc4 = self._tkparent.create_arc((x + w - 2 * r, y + h - 2 * r, x + w, y + h), start=270, extent=90, fill=bg, outline=bg)
        self._rct1 = self._tkparent.create_rectangle(x + r, y, x + w - r, y + h, fill = bg, outline = bg)
        self._rct2 = self._tkparent.create_rectangle(x, y + r, x + w, y + h - r, fill=bg, outline=bg)
        self._cnvs = tk.Canvas(self._tkparent, width= w - 2 - r // 2, height=h - 2 - r // 2, bg=bg, highlightthickness=0)
        self._tkparent.create_window(x + 1 + w//2, y + 1 + h//2, window=self._cnvs)
        self._text_id = self._cnvs.create_text(0 + (w - 2 - r)//2, 0 + (h - 2 - r)//2, text=text, fill=fg, font=(BaseComponent.default_font_name, font_size))
        if enabled:
            self._cnvs.bind("<Button-1>", lambda e: command())
            self._cnvs.bind("<Enter>", lambda e: self._cnvs.config(cursor="hand2"))
            self._cnvs.bind("<Leave>", lambda e: self._cnvs.config(cursor=""))
        else:
            self._cnvs.config(cursor="")
            self._set_disabled_colors()
    def destroy(self) -> int:
        self._tkparent.delete(self._arc1)
        self._tkparent.delete(self._arc2)
        self._tkparent.delete(self._arc3)
        self._tkparent.delete(self._arc4)
        self._tkparent.delete(self._rct1)
        self._tkparent.delete(self._rct2)
        self._cnvs.destroy()
    def _set_enabled_colors(self):
        bg = self._bg
        fg = self._fg
        self._tkparent.itemconfig(self._arc1, fill=bg, outline=bg)
        self._tkparent.itemconfig(self._arc2, fill=bg, outline=bg)
        self._tkparent.itemconfig(self._arc3, fill=bg, outline=bg)
        self._tkparent.itemconfig(self._arc4, fill=bg, outline=bg)
        self._tkparent.itemconfig(self._rct1, fill=bg, outline=bg)
        self._tkparent.itemconfig(self._rct2, fill=bg, outline=bg)
        self._cnvs.config(bg=bg)
        self._cnvs.itemconfig(self._text_id, fill=fg)
    def _set_disabled_colors(self):
        disabled_bg = "#CCCCCC"
        disabled_fg = "#666666"
        self._tkparent.itemconfig(self._arc1, fill=disabled_bg, outline=disabled_bg)
        self._tkparent.itemconfig(self._arc2, fill=disabled_bg, outline=disabled_bg)
        self._tkparent.itemconfig(self._arc3, fill=disabled_bg, outline=disabled_bg)
        self._tkparent.itemconfig(self._arc4, fill=disabled_bg, outline=disabled_bg)
        self._tkparent.itemconfig(self._rct1, fill=disabled_bg, outline=disabled_bg)
        self._tkparent.itemconfig(self._rct2, fill=disabled_bg, outline=disabled_bg)
        self._cnvs.config(bg=disabled_bg)
        self._cnvs.itemconfig(self._text_id, fill=disabled_fg)
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
        icon_full_path = local_path(icon_path)
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

# - - - - - - - - - - - - - - - - - - <start> utils </start> - - - - - - - - - - - - - - - - - - #
def local_path(path: str) -> str:
    """
        Get the absolute path to a resource, works for dev and for PyInstaller.
        :param path: relative path to the resource
        :return: absolute path to the resource
    """
    path = path.replace("\\", "/").strip("/")
    while "//" in path:
        path = path.replace("//", "/")
    base_paths = [os.path.dirname(os.path.abspath(__file__))]
    try:
        base_paths.append(sys._MEIPASS)
    except AttributeError:
        pass
    for base_path in base_paths:
        # Check both assets/img and assets/icons
        for sub_dir in ["assets/img", "assets/icons"]:
            ref_dir : str = os.path.join(base_path, sub_dir)
            res_path: str = os.path.join(base_path, path)
            if os.path.exists(ref_dir) and os.path.exists(res_path):
                return res_path
    raise FileNotFoundError(f"Resource path '{path}' not found in any of the base paths.")

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
            BaseView.__cache__[m][k][p] = ImageTk.PhotoImage(
                Image.open(local_path(p)).resize((w, h))
            )
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
        self._context._base_canvas.itemconfig(
            self._context._base_area,
            image = BaseView.__cache__[m][k][p]
        )
# - - - - - - - - - - - - - <start> step-1 </start> - - - - - - - - - - - - - #
class Step1View(BaseView):
    step_number: int = 1
    sidebar_title: str = "Welcome"
    sidebar_description: str = "Begin installation"
    design_screenshot_path: str = "assets/img/step-1.png"
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
            messagebox.showinfo("BAISS Maintenance", f"Selected option: {option['title']}")

    def destroy(self) -> int:
        for card in getattr(self, "_option_cards", []):
            card.destroy()
        self._option_cards.clear()
        return super().destroy()
class UpgradeView(BaseView):
    step_number: int = 1
    sidebar_title: str = "Welcome"
    sidebar_description: str = "Begin installation"
    design_screenshot_path: str = "assets/img/step-2.png"
    def __init__(self, context):
        super().__init__(context = context)
    def render(self):
        super().render()
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 75,
            y          = 25,
            text       = "Upgrade Status",
            font_size  = 26,
            weight     = "bold",
            foreground = "#101820",
            background = "#FFFFFF"
        )
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 75,
            y          = 80,
            text       = "Please wait while BAISS is ungraded. Your data and settings will be preserved.",
            font_size  = 11,
            foreground = "#101820",
            background = "#FFFFFF"
        )
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 75,
            y          = 115,
            text       = "Upgrading BAISS",
            font_size  = 11,
            foreground = "#101820",
            background = "#FFFFFF"
        )
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 410,
            y          = 175,
            text       = "Verifying package…",
            font_size  = 10,
            foreground = "#606060",
            background = "#FFFFFF"
        )

        self._btn_cancel = RoundedButton(
            x=800,
            y=680,
            width=100,
            height=46,
            background="#E5ECF8",
            foreground="#1C2333",
            text="Cancel",
            font_size=12,
            command=self._context.cancel,
            context=self._context
        )
class DowngradeView(BaseView):
    step_number: int = 1
    sidebar_title: str = "Welcome"
    sidebar_description: str = "Begin installation"
    design_screenshot_path: str = "assets/img/step-2.png"
    def __init__(self, context):
        super().__init__(context = context)
    def render(self):
        super().render()
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 75,
            y          = 25,
            text       = "Downgrade Status",
            font_size  = 26,
            weight     = "bold",
            foreground = "#101820",
            background = "#FFFFFF"
        )
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 75,
            y          = 80,
            text       = "Please wait. Your data and settings will be preserved. Newer features may be unavailable until you upgrade again.",
            font_size  = 11,
            foreground = "#101820",
            background = "#FFFFFF"
        )
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 75,
            y          = 115,
            text       = "Downgrading BAISS",
            font_size  = 11,
            foreground = "#101820",
            background = "#FFFFFF"
        )
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 410,
            y          = 175,
            text       = "Verifying package…",
            font_size  = 10,
            foreground = "#606060",
            background = "#FFFFFF"
        )

        self._btn_cancel = RoundedButton(
            x=800,
            y=680,
            width=100,
            height=46,
            background="#E5ECF8",
            foreground="#1C2333",
            text="Cancel",
            font_size=12,
            command=self._context.cancel,
            context=self._context
        )
class RepairView(BaseView):
    step_number: int = 1
    sidebar_title: str = "Welcome"
    sidebar_description: str = "Begin installation"
    design_screenshot_path: str = "assets/img/step-2.png"
    def __init__(self, context):
        super().__init__(context = context)
    def render(self):
        super().render()
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 75,
            y          = 25,
            text       = "Repair Status",
            font_size  = 26,
            weight     = "bold",
            foreground = "#101820",
            background = "#FFFFFF"
        )
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 75,
            y          = 80,
            text       = "Please wait. We’ll verify files, restore services, and fix shortcuts. Your content won’t be changed.",
            font_size  = 11,
            foreground = "#101820",
            background = "#FFFFFF"
        )
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 75,
            y          = 115,
            text       = "Repairing BAISS",
            font_size  = 11,
            foreground = "#101820",
            background = "#FFFFFF"
        )
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 410,
            y          = 175,
            text       = "Verifying package…",
            font_size  = 10,
            foreground = "#606060",
            background = "#FFFFFF"
        )

        self._btn_cancel = RoundedButton(
            x=800,
            y=680,
            width=100,
            height=46,
            background="#E5ECF8",
            foreground="#1C2333",
            text="Cancel",
            font_size=12,
            command=self._context.cancel,
            context=self._context
        )


class UpgradeFinalView(BaseView):
    step_number: int = 5
    sidebar_title: str = "Finish"
    sidebar_description: str = "Finish setup"
    design_screenshot_path: str = "assets/img/background.jpg"
    def __init__(self, context):
        super().__init__(context = context)
        self._launch_checkbox = None
        self._finish_button = None
        self._title_label = None
        self._message_label = None
        self._logo_image = None
        self._logo_id = None
    def _on_launch_toggle(self, checked):
        self._context._cofing["LaunchAfterSetup"] = bool(checked)
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
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = center_x,
            y          = 270,
            text       = "Upgrade Complete",
            font_size  = 20,
            weight     = "bold",
            background = None,
            foreground = "#FFFFFF",
            anchor     = "center",
        )
        logo_img = Image.open(local_path("assets/img/logo.png"))
        max_logo_width = 240
        if logo_img.width > max_logo_width:
            ratio = max_logo_width / logo_img.width
            logo_img = logo_img.resize(
                (int(logo_img.width * ratio), int(logo_img.height * ratio)),
                Image.LANCZOS,
            )
        self._logo_image = ImageTk.PhotoImage(logo_img)
        self._logo_id = canvas.create_image(center_x, 330, image=self._logo_image)
        self._message_label = CanvasLabel(
            context    = self._context,
            x          = center_x,
            y          = 390,
            text       = "Your app has been successfully upgraded.",
            font_size  = 13,
            background = None,
            foreground = "#E5E7EB",
            anchor     = "center",
        )
        initial_checked = bool(self._context._cofing.get("LaunchAfterSetup", True))
        self._context._cofing["LaunchAfterSetup"] = initial_checked
        self._launch_checkbox = CanvasCheckButton(
            context                   = self._context,
            x                         = center_x - 80,
            y                         = 425,
            text                      = "Launch BAISS now",
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

class DowngradeView(BaseView):
    step_number: int = 5
    sidebar_title: str = "Finish"
    sidebar_description: str = "Finish setup"
    design_screenshot_path: str = "assets/img/background.jpg"
    def __init__(self, context):
        super().__init__(context = context)
        self._launch_checkbox = None
        self._finish_button = None
        self._title_label = None
        self._message_label = None
        self._logo_image = None
        self._logo_id = None
    def _on_launch_toggle(self, checked):
        self._context._cofing["LaunchAfterSetup"] = bool(checked)
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
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = center_x,
            y          = 270,
            text       = "Downgrade Complete",
            font_size  = 20,
            weight     = "bold",
            background = None,
            foreground = "#FFFFFF",
            anchor     = "center",
        )
        logo_img = Image.open(local_path("assets/img/logo.png"))
        max_logo_width = 240
        if logo_img.width > max_logo_width:
            ratio = max_logo_width / logo_img.width
            logo_img = logo_img.resize(
                (int(logo_img.width * ratio), int(logo_img.height * ratio)),
                Image.LANCZOS,
            )
        self._logo_image = ImageTk.PhotoImage(logo_img)
        self._logo_id = canvas.create_image(center_x, 330, image=self._logo_image)
        self._message_label = CanvasLabel(
            context    = self._context,
            x          = center_x,
            y          = 390,
            text       = "Your app has been successfully downgraded.",
            font_size  = 13,
            background = None,
            foreground = "#E5E7EB",
            anchor     = "center",
        )
        initial_checked = bool(self._context._cofing.get("LaunchAfterSetup", True))
        self._context._cofing["LaunchAfterSetup"] = initial_checked
        self._launch_checkbox = CanvasCheckButton(
            context                   = self._context,
            x                         = center_x - 80,
            y                         = 425,
            text                      = "Launch BAISS now",
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

class RepairView(BaseView):
    step_number: int = 5
    sidebar_title: str = "Finish"
    sidebar_description: str = "Finish setup"
    design_screenshot_path: str = "assets/img/background.jpg"
    def __init__(self, context):
        super().__init__(context = context)
        self._launch_checkbox = None
        self._finish_button = None
        self._title_label = None
        self._message_label = None
        self._logo_image = None
        self._logo_id = None
    def _on_launch_toggle(self, checked):
        self._context._cofing["LaunchAfterSetup"] = bool(checked)
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
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = center_x,
            y          = 270,
            text       = "Repair Complete",
            font_size  = 20,
            weight     = "bold",
            background = None,
            foreground = "#FFFFFF",
            anchor     = "center",
        )
        logo_img = Image.open(local_path("assets/img/logo.png"))
        max_logo_width = 240
        if logo_img.width > max_logo_width:
            ratio = max_logo_width / logo_img.width
            logo_img = logo_img.resize(
                (int(logo_img.width * ratio), int(logo_img.height * ratio)),
                Image.LANCZOS,
            )
        self._logo_image = ImageTk.PhotoImage(logo_img)
        self._logo_id = canvas.create_image(center_x, 330, image=self._logo_image)
        self._message_label = CanvasLabel(
            context    = self._context,
            x          = center_x,
            y          = 390,
            text       = "Your app has been successfully repaired.",
            font_size  = 13,
            background = None,
            foreground = "#E5E7EB",
            anchor     = "center",
        )
        initial_checked = bool(self._context._cofing.get("LaunchAfterSetup", True))
        self._context._cofing["LaunchAfterSetup"] = initial_checked
        self._launch_checkbox = CanvasCheckButton(
            context                   = self._context,
            x                         = center_x - 80,
            y                         = 425,
            text                      = "Launch BAISS now",
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



class BaissSetupWizard:
    def __init__(self,
        width : int = 960,
        height: int = 760,
        title : str = "Baiss Setup Wizard"
    ):
        self._tkroot = tk.Tk()
        self._tkroot.title(title)
        self._tkroot.iconbitmap("assets/icons/icon_32x32.ico")
        self._screen_width  = self._tkroot.winfo_screenwidth()
        self._screen_height = self._tkroot.winfo_screenheight()
        self._width         = width
        self._height        = height
        self._images        = {}
        self._current_view  = None
        self._cofing: dict  = {
            "InstallationDirectory": "C:\\Program Files\\Baiss",
            "LaunchAfterSetup": True
        }
        self._step_views    = [Step1View]
        self._active_step   = getattr(self._step_views[0], "step_number", 1)
        self._sidebar       = None
        img_path = local_path(f"assets/img/step-1.png")
        img = Image.open(img_path).resize((self._width, self._height))
        self._images[1] = ImageTk.PhotoImage(img)
        # Center the window
        x = int((self._screen_width / 2) - (self._width / 2))
        y = int((self._screen_height / 2) - (self._height / 2))
        self._tkroot.geometry(f"{self._width}x{self._height}+{x}+{y}")
        self._tkroot.resizable(False, False)
        self._base_canvas = tk.Canvas(self._tkroot, width=self._width, height=self._height, highlightthickness=0)
        self._base_canvas.pack(fill = "both", expand = True)
        self._base_area = self._base_canvas.create_image(0, 0, image = self._images[1], anchor = "nw")
        
        # Configure checkbox style for white background
        style = Style()
        style.configure("TCheckbutton", 
                        background="#FFFFFF",
                        font=(BaseComponent.default_font_name, 12),
                        indicatorsize=20)
        
    def _teardown(self):
        if self._watcher:
            self._watcher.stop()
            self._watcher = None
    def clear_view(self):
        """Clear all existing buttons and elements from the view."""
        if self._current_view:
            self._current_view.destroy()
    def next_view(self, next_step: int, direction: str = "left", animate: bool = False):
        """Switch background image for the requested step."""
        if next_step in self._images:
            self._base_canvas.itemconfig(self._base_area, image=self._images[next_step])
        return True
    def finish(self):
        self._teardown()
        self._tkroot.quit()
        self._tkroot.destroy()
        sys.exit(0)
    def cancel(self):
        self._teardown()
        self._tkroot.quit()
        self._tkroot.destroy()
        sys.exit(0)
    def view(self, view_object: BaseView):
        step_number = getattr(view_object, "step_number", None)
        if step_number is not None:
            try:
                self._active_step = int(step_number)
            except (TypeError, ValueError):
                pass
            else:
                if self._sidebar:
                    first_step = getattr(self._step_views[0], "step_number", 1) if self._step_views else None
                    last_step = getattr(self._step_views[-1], "step_number", len(self._step_views)) if self._step_views else None
                    if self._active_step == first_step or self._active_step == last_step:
                        self._sidebar.hide()
                    else:
                        self._sidebar.show()
                        self._sidebar.set_active(self._active_step)
        next_view = view_object(context = self)
        next_view.render()
        self._current_view = next_view
        return self._current_view
    def run(self):
        self.view(Step1View)
        self._tkroot.mainloop()
if __name__ == "__main__":
    app = BaissSetupWizard()
    app.run()
