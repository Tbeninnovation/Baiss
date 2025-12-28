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
import subprocess
import threading
import tkinter as tk
from PIL import Image, ImageTk
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
    def __init__(self, x, y, width, height, background, foreground, text, command, radius: int = 6, tkparent = None, context = None, enabled: bool = True, font_size: int = BaseComponent.default_font_size):
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
    def render(self):
        super().render()

class Step2View(BaseView):
    step_number: int = 2
    sidebar_title: str = "Installation"
    sidebar_description: str = "Select installation options"
    design_screenshot_path: str = "assets/img/step-2.png"

    def __init__(self, context):
        super().__init__(context=context)
        self._frame_id = None
        self._frame = None
        self._vars = []
        self._checkbuttons = []
        self._btn_cancel = None
        self._btn_uninstall = None

    def destroy(self):
        self._clear_widgets()
        return super().destroy()

    def _clear_widgets(self):
        canvas = getattr(self._context, "_base_canvas", None)
        for cb, window_id in self._checkbuttons:
            if canvas and window_id:
                canvas.delete(window_id)
            if cb:
                cb.destroy()
        self._checkbuttons.clear()
        for attr in ("_btn_cancel", "_btn_uninstall"):
            btn = getattr(self, attr, None)
            if btn:
                btn.destroy()
                setattr(self, attr, None)
        self._vars.clear()

    def _on_uninstall_click(self):
        self._show_confirmation_modal()

    def _show_confirmation_modal(self):
        modal_width = 600
        modal_height = 400

        modal = tk.Toplevel(self._context._tkroot)
        modal.title("Delete Items")
        modal.configure(bg="#FFFFFF")
        modal.resizable(False, False)
        modal.transient(self._context._tkroot)
        modal.grab_set()
        modal.focus_set()

        modal.update_idletasks()
        x = (modal.winfo_screenwidth() // 2) - (modal_width // 2)
        y = (modal.winfo_screenheight() // 2) - (modal_height // 2)
        modal.geometry(f"{modal_width}x{modal_height}+{x}+{y}")

        heading_font = (BaseComponent.default_font_name, 18, "bold")
        subheading_font = (BaseComponent.default_font_name, 12)
        item_title_font = (BaseComponent.default_font_name, 12, "bold")
        item_size_font = (BaseComponent.default_font_name, 12)

        container = tk.Frame(modal, bg="#FFFFFF")
        container.pack(fill="both", expand=True, padx=24, pady=24)

        heading_label = tk.Label(
            container,
            text="Permanently delete selected items?",
            font=heading_font,
            bg="#FFFFFF",
            fg="#202124",
        )
        heading_label.pack(anchor="center")

        warning_label = tk.Label(
            container,
            text="This action can't be undone.",
            font=subheading_font,
            bg="#FFFFFF",
            fg="#5F6368",
        )
        warning_label.pack(anchor="center", pady=(4, 16))

        # Data used to render each row in the modal.
        items = [
            {"title": "Chats & history", "size": "~120 MB", "icon": "assets/icons/message.png"},
            {"title": "Downloaded models", "size": "~9.8 GB", "icon": "assets/icons/download.png"},
            {"title": "AI Workspace folder", "size": "~1.2 GB", "icon": "assets/icons/folder.png"},
        ]

        accent_color = "#FF5A5F"
        x_offset = 80

        # Store images to prevent garbage collection
        modal_images = []

        for index, item in enumerate(items):
            row = tk.Frame(container, bg="#FFFFFF")
            bottom_padding = 12 if index < len(items) - 1 else 0
            row.pack(fill="x", pady=(0, bottom_padding))

            content_frame = tk.Frame(row, bg="#FFFFFF")
            content_frame.pack(side="left", padx=(x_offset, 0))

            icon_container_bg = tk.Frame(content_frame, bg="#FFFFFF")
            icon_container_bg.pack(side="left", padx=(0, 12))

            # Load and resize icon
            try:
                icon_path = local_path(item["icon"])
                icon_img = Image.open(icon_path).convert("RGBA")

                # Slightly upscale icon so the asset's own background fills the space
                icon_size = 32
                icon_img = icon_img.resize((icon_size, icon_size))

                icon_photo = ImageTk.PhotoImage(icon_img)
                modal_images.append(icon_photo)  # Keep reference

                icon_label = tk.Label(
                    icon_container_bg,
                    image=icon_photo,
                    bg="#FFFFFF",
                )
                icon_label.image = icon_photo  # Prevent garbage collection
            except Exception as e:
                print(f"Failed to load icon {item['icon']}: {e}")
                icon_label = tk.Label(
                    icon_container_bg,
                    text="X",
                    font=(BaseComponent.default_font_name, 10, "bold"),
                    bg=accent_color,
                    fg="#FFFFFF",
                )
            icon_label.pack(expand=True)

            text_frame = tk.Frame(content_frame, bg="#FFFFFF")
            text_frame.pack(side="left", fill="x", expand=True)

            title_label = tk.Label(
                text_frame,
                text=item["title"],
                font=item_title_font,
                bg="#FFFFFF",
                fg="#202124",
            )
            title_label.pack(anchor="w")

            size_label = tk.Label(
                text_frame,
                text=item["size"],
                font=item_size_font,
                bg="#FFFFFF",
                fg=accent_color,
            )
            size_label.pack(anchor="w", pady=(4, 0))

        # Persist images on the modal instance to avoid being garbage collected
        modal._icon_images = modal_images

        button_frame = tk.Frame(container, bg="#FFFFFF")
        button_frame.pack(anchor="center", pady=(20, 0))

        def on_confirm():
            modal.destroy()
            self._context.finish()

        def on_cancel():
            modal.destroy()

        button_font = tkfont.Font(family=BaseComponent.default_font_name, size=12, weight="bold")
        modal_buttons: list[RoundedButton] = []

        def add_modal_button(text: str, command, bg_color: str, fg_color: str, pad_x=(0, 0)):
            padding_x = 24
            padding_y = 10
            text_width = button_font.measure(text)
            text_height = button_font.metrics("linespace")
            width = int(text_width + padding_x * 2)
            height = int(text_height + padding_y * 2)

            host_canvas = tk.Canvas(
                button_frame,
                width=width,
                height=height,
                bg="#FFFFFF",
                highlightthickness=0,
                bd=0,
            )
            host_canvas.pack(side="left", padx=pad_x)

            button = RoundedButton(
                x=0,
                y=0,
                width=width,
                height=height,
                background=bg_color,
                foreground=fg_color,
                text=text,
                command=command,
                radius=12,
                tkparent=host_canvas,
                context=self._context,
                font_size=12,
            )
            modal_buttons.append(button)
            return button

        add_modal_button(
            text="Go back",
            command=on_cancel,
            bg_color="#E4ECF7",
            fg_color="#1C2333",
            pad_x=(0, 16),
        )
        add_modal_button(
            text="Confirm",
            command=on_confirm,
            bg_color=accent_color,
            fg_color="#FFFFFF",
        )

        modal._buttons = modal_buttons

        modal.bind("<Escape>", lambda *_: on_cancel())

    def render(self):
        super().render()
        self._clear_widgets()
        canvas = self._context._base_canvas

        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 70,
            y          = 20,
            text       = "Uninstall BAISS",
            font_size  = 26,
            weight     = "bold",
            foreground = "#101820",
            background = "#FFFFFF"
        )

        self._description_label = CanvasLabel(
            context    = self._context,
            x          = 70,
            y          = 80,
            text       = "Choose what to remove. Your own files stay unless you say otherwise.",
            font_size  = 13,
            weight     = "normal",
            foreground = "#51565C",
            background = "#FFFFFF",
            max_width  = 520,
            justify    = "left"
        )

        self._core_title_label = CanvasLabel(
            context    = self._context,
            x          = 70,
            y          = 120,
            text       = "Core application files (removed)",
            font_size  = 15,
            weight     = "bold",
            foreground = "#15171A",
            background = "#FFFFFF"
        )

        # Core checkbuttons
        core_y = 160
        for label in [
            "App files & services",
            "Shortcuts & Start-menu entries",
            "Registry keys and cache"
        ]:
            var = tk.BooleanVar(value=True)
            self._vars.append(var)
            cb = Checkbutton(canvas, text=label, variable=var)
            cb_window = canvas.create_window(70, core_y, anchor="nw", window=cb)
            self._checkbuttons.append((cb, cb_window))
            core_y += 40

        self._optional_title_label = CanvasLabel(
            context    = self._context,
            x          = 70,
            y          = 280,
            text       = "Optional removals",
            font_size  = 15,
            weight     = "bold",
            foreground = "#15171A",
            background = "#FFFFFF"
        )

        self._optional_desc_label = CanvasLabel(
            context    = self._context,
            x          = 70,
            y          = 310,
            text       = "Select items to delete. Unselected items are kept.",
            font_size  = 12,
            weight     = "normal",
            foreground = "#51565C",
            background = "#FFFFFF"
        )

        optional_items = [
            ("Chats & history (~120 MB)", True, None),
            ("Downloaded AI models (~9.8 GB)", False, "Keeping models avoids a large re-download later."),
            ("The AI Workspace folder", False, "This is where the app stores its data and settings."),
        ]

        optional_y = 340
        for label, default, hint in optional_items:
            var = tk.BooleanVar(value=default)
            self._vars.append(var)
            cb = Checkbutton(canvas, text=label, variable=var)
            cb_window = canvas.create_window(70, optional_y, anchor="nw", window=cb)
            self._checkbuttons.append((cb, cb_window))
            optional_y += 45
            if hint:
                CanvasLabel(
                    context    = self._context,
                    x          = 80,
                    y          = optional_y - 20,
                    text       = hint,
                    font_size  = 11,
                    weight     = "normal",
                    foreground = "#595959",
                    background = "#FFFFFF"
                )
                optional_y += 10

        self._export_link_label = CanvasLabel(
            context    = self._context,
            x          = 70,
            y          = 579,
            text       = "Export settings & logs",
            font_size  = 12,
            weight     = "normal",
            foreground = "#2E6FD8",
            background = "#FFFFFF"
        )
        # Bind click event to the label
        self._export_link_label._tk_label.bind("<Button-1>", lambda *_: messagebox.showinfo("Export", "Exporting logs is not available in this preview."))

        button_y = self._context._height - 110
        self._btn_cancel = RoundedButton(
            x=self._context._width - 320,
            y=button_y,
            width=140,
            height=46,
            background="#E5ECF8",
            foreground="#1C2333",
            text="Cancel",
            command=self._context.cancel,
            context=self._context
        )
        self._btn_uninstall = RoundedButton(
            x=self._context._width - 160,
            y=button_y,
            width=140,
            height=46,
            background="#5595F6",
            foreground="#FFFFFF",
            text="Uninstall",
            command=self._on_uninstall_click,
            context=self._context
        )

class Step3View(BaseView):
    step_number: int = 1
    sidebar_title: str = "Welcome"
    sidebar_description: str = "Begin installation"
    design_screenshot_path: str = "assets/img/step-3.png"
    def __init__(self, context):
        super().__init__(context = context)
    def render(self):
        super().render()
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 70,
            y          = 20,
            text       = "Uninstall BAISS",
            font_size  = 26,
            weight     = "bold",
            foreground = "#101820",
            background = "#FFFFFF"
        )
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 70,
            y          = 75,
            text       = "Please wait while BAISS is uninstalled and the files you selected removed from your computer.",
            font_size  = 12,
            foreground = "#101820",
            background = "#FFFFFF"
        )
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 70,
            y          = 115,
            text       = "Uninstalling BAISS...",
            font_size  = 11,
            foreground = "#101820",
            background = "#FFFFFF"
        )
        self._title_label = CanvasLabel(
            context    = self._context,
            x          = 380,
            y          = 175,
            text       = "Removing application files...",
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


class Step4View(BaseView):
    step_number: int = 5
    sidebar_title: str = "Finish"
    sidebar_description: str = "Finish setup"
    design_screenshot_path: str = "assets/img/background.jpg"
    def __init__(self, context):
        super().__init__(context = context)
        self._launch_var = tk.BooleanVar(value=True)
        self._launch_checkbox = None
        self._launch_window = None
    def _on_launch_toggle(self, checked):
        self._context._cofing["LaunchAfterSetup"] = bool(checked)
    def destroy(self) -> int:
        count = super().destroy()
        if hasattr(self, '_logo_id'):
            self._context._base_canvas.delete(self._logo_id)
        if self._launch_window:
            self._context._base_canvas.delete(self._launch_window)
        if self._launch_checkbox:
            self._launch_checkbox.destroy()
        return count
    def render(self):
        super().render()
        cb = Checkbutton(self._context._base_canvas, text="Launch Baiss now", variable=self._launch_var, style="TCheckbutton")
        cb.config(command=lambda: self._on_launch_toggle(self._launch_var.get()))
        self._launch_window = self._context._base_canvas.create_window(400, 430, anchor="nw", window=cb)
        self._launch_checkbox = cb
        self._finish_button = RoundedButton(
            context    = self._context,
            x          = 490,
            y          = 470,
            width      = 94,
            height     = 44,
            font_size  = 12,
            background = "#0B5FB0",
            foreground = "white",
            text       = "Finish",
            command    = self._context.finish
        )
        self._finish_button = RoundedButton(
            context    = self._context,
            x          = 320,
            y          = 470,
            width      = 150,
            height     = 44,
            font_size  = 12,
            background = "#263864",
            foreground = "white",
            text       = "Send Feedback",
            command    = self._context.finish
        )

        self._setup_complete_label = CanvasLabel(
            context    = self._context,
            x          = 378,
            y          = 318,
            text       = "was successfully removed.",
            font_size  = 20,
            weight     = "bold",
            background = None,
            foreground = "#FFFFFF",
        )
        logo_img = Image.open(local_path("assets/img/logo.png"))
        width, height = logo_img.size
        logo_img = logo_img.resize((width - 70 , height - 15))
        self._logo_image = ImageTk.PhotoImage(logo_img)
        self._logo_id = self._context._base_canvas.create_image(280, 332, image=self._logo_image)
        self._setup_complete_label = CanvasLabel(
            context    = self._context,
            x          = 350,
            y          = 390,
            text       = "Thank you for using BAISS.",
            font_size  = 12,
            background = None,
            foreground = "#FFFFFF",
        )


class ErrorScreenView(BaseView):
    step_number: int = 6
    sidebar_title: str = "Error"
    sidebar_description: str = "Installation failed"
    design_screenshot_path: str = "assets/img/errorscreen.png"

    def __init__(self, context):
        super().__init__(context=context)
        self._link_label = None
        self._link_window = None

    def destroy(self) -> int:
        if self._link_window:
            self._context._base_canvas.delete(self._link_window)
            self._link_window = None
        if self._link_label:
            self._link_label.destroy()
            self._link_label = None
        return super().destroy()

    def render(self):
        super().render()
        canvas = self._context._base_canvas
        margin_x = 70

        self._title_label = CanvasLabel(
            context=self._context,
            x=margin_x,
            y=60,
            text="We couldn't install BAISS",
            font_size=24,
            foreground="#0D4AA2",
            background="#FFFFFF",
        )

        self._subtitle_label = CanvasLabel(
            context=self._context,
            x=margin_x,
            y=120,
            text="We've set your PC to the way it was before you started installing BAISS.",
            font_size=12,
            foreground="#1F2933",
            background="#FFFFFF",
            max_width=520,
            justify="left",
        )

        self._error_code_label = CanvasLabel(
            context=self._context,
            x=margin_x,
            y=240,
            text="BAISS-SETUP 0x4000D - Failed during COPY_DATA",
            font_size=16,
            foreground="#111827",
            background="#FFFFFF",
        )

        self._error_detail_label = CanvasLabel(
            context=self._context,
            x=margin_x,
            y=280,
            text="The installation failed in the Third Phase with an error during COPY_DATA operation.",
            font_size=13,
            weight="bold",
            foreground="#111827",
            background="#FFFFFF",
            max_width=620,
            justify="left",
        )

        instructions_text = (
            "Is your PC compatible? Is your internet connection working? Do you have enough free space on your main hard "
            "drive? Please check the above and try again."
        )
        self._instructions_label = CanvasLabel(
            context=self._context,
            x=margin_x,
            y=360,
            text=instructions_text,
            font_size=12,
            foreground="#1F2933",
            background="#FFFFFF",
            max_width=640,
            justify="left",
        )

        link_font = tkfont.Font(family=BaseComponent.default_font_name, size=12, underline=True)
        self._link_label = tk.Label(
            canvas,
            text="Go online & Check our documentation / Check Log File / Troubleshooting Tips",
            font=link_font,
            fg="#1C2333",
            bg="#FFFFFF",
            cursor="hand2",
        )
        self._link_window = canvas.create_window(margin_x, 530, anchor="nw", window=self._link_label)

        button_width = 118
        button_height = 46

        self._cancel_button = RoundedButton(
            context=self._context,
            x=800,
            y=650,
            width=button_width,
            height=button_height,
            font_size=12,
            background="#E4ECF7",
            foreground="#1C2333",
            text="Cancel",
            command=self._context.cancel,
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
        self.view(ErrorScreenView)
        self._tkroot.mainloop()
if __name__ == "__main__":
    app = BaissSetupWizard()
    app.run()

