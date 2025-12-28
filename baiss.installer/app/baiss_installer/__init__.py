import os
import sys
import shutil
import logging
import pathlib
import zipfile
import subprocess
import platform as platform_module
sys.path.insert(0,  os.path.dirname(os.path.dirname(os.path.abspath(__file__))) )
from baiss_installer.utils  import path_join
from baiss_installer.utils  import project_root
from baiss_installer.utils  import project_path
from baiss_installer.dotapp.bundler import DotAppBundler

logging.basicConfig(level=logging.DEBUG)
logger = logging.getLogger(__name__)

class BaissPackageInstaller:
    """
        Macos:
            codesign --deep --force --sign - "Baiss Desktop.app"
    """
    def __init__(self, folder_name: str = None, title: str = "Baiss"):
        if folder_name is None:
            if self.is_macos():
                folder_name = "Baiss.app" # "Baiss Desktop.app"
            elif self.is_windows():
                folder_name = "Baiss"
            else:
                folder_name = "baiss"
        if not folder_name:
            raise ValueError("Folder name cannot be empty")
        self._folder_name: str = folder_name
        self._path       : str = self.default_installation_path
        self._title      : str = title

    def get_description(self) -> str:
        return f"{self._title} - A cross-platform desktop application for Baiss 1234."

    def get_destination_path(self) -> str:
        """
        Get the current installation path for the application.
        :return: The installation path.
        """
        return self._path

    def set_destination_path(self, path: str):
        """
        Set the installation path for the application.
        :param path: The desired installation path.
        """
        self._path = path_join(path + f"/{self._folder_name}")
        return self._path

    @classmethod
    def runtime(cls) -> str:
        arch: str = platform_module.machine().lower()
        if arch in ["amd64", "x86_64"]:
            return f"{cls.platform()}-x64"
        if arch in ["i386", "i686", "x86"]:
            return f"{cls.platform()}-x86"
        if ("arm" in arch) or ("aarch" in arch):
            return f"{cls.platform()}-arm64"
        return f"{cls.platform()}-{arch}"

    @classmethod
    def platform(cls) -> str:
        """
        Returns the current operating system platform.
        Possible return values include "win32" for Windows, "darwin" for macOS, and "linux" for Linux.
        """
        if "darwin" in sys.platform:
            return "osx"
        elif "linux" in sys.platform:
            return "linux"
        return "win"

    @classmethod
    def is_windows(cls) -> bool:
        """
        Check if the current operating system is Windows.
        :return: True if the OS is Windows, False otherwise.
        """
        return "win" in cls.platform()

    @classmethod
    def is_macos(cls) -> bool:
        """
        Check if the current operating system is macOS.
        :return: True if the OS is macOS, False otherwise.
        """
        return "osx" in cls.platform()

    @classmethod
    def is_linux(cls) -> bool:
        """
        Check if the current operating system is Linux.
        :return: True if the OS is Linux, False otherwise.
        """
        return "linux" in cls.platform()

    @property
    def default_installation_path(self) -> str:
        """
        Returns the default installation path based on the operating system.
        On macOS, this typically corresponds to the "/Applications/AppName" directory.
        On Windows, this typically corresponds to the "C:\\Program Files\\AppName" directory.
        On Linux, this typically corresponds to the "/opt/applications/AppName" directory.
        """
        if self.is_macos():
            return path_join(self.home, "Applications", self._folder_name)
        if self.is_windows():
            if sys.maxsize > 2 ** 32:
                return path_join(os.environ["USERPROFILE"], self._folder_name)
            else:
                return path_join(os.environ["USERPROFILE"], self._folder_name)
        return path_join("/opt", "applications", self._folder_name)

    @property
    def home(self) -> str:
        """
        Returns the path to the current user's home directory.
        On Windows, this typically corresponds to "C:\\Users\\Username".
        On macOS and Linux, this typically corresponds to "/home/username" or "/Users/username".
        """
        return pathlib.Path.home()

    @property
    def project_srcs(self) -> str:
        """
        Returns the path to the zip file containing the application to be installed.
        Adjust the path as necessary to point to the correct zip file for your application.
        """
        zipdir: str = project_path("assets/zip")
        srcs = []
        for basename in os.listdir(zipdir):
            lbasename: str = basename.lower()
            if lbasename.endswith(".zip") and (self.runtime() in lbasename):
                srcs.append(os.path.join(zipdir, basename))
        if not srcs:
            raise FileNotFoundError("Could not find the Baiss zip file for the current platform.")
        return srcs

    def extract(self, progress_callback=None):
        dst_path: str = self._path
        if self.is_macos():
            dst_path = path_join(self._path + "/Contents/Resources")
            # dst_path = path_join(self._path + "/Contents/MacOS")
        pindex: int = 0
        for src in self.project_srcs:
            dst: str = dst_path
            folder_name : str = None
            src_basename: str = os.path.basename(src)
            if src_basename.lower().startswith("baiss-") and ("@" in src_basename):
                folder_name = src_basename.split("@")[-1].split(".")[0]
                dst = path_join(dst_path, folder_name)
            with zipfile.ZipFile(src, 'r') as zip_ref:
                members = zip_ref.infolist()
                total   = len(members)
                for i, member in enumerate(members, start=1):
                    os.makedirs(dst, exist_ok=True)
                    zip_ref.extract(member, dst)
                    progress = int(min(100.0, ((pindex * 1.0 / len(self.project_srcs)) * 1.0 / total) * 100.0))
                    if progress_callback:
                        progress_callback(member, progress)
                    else:
                        logger.debug(f"Extracting {member.filename} to {dst_path} ({progress}%)")
                    pindex += 1
        if self.is_macos():
            icon_path: str = project_path("assets/icns/baiss-desktop-icon.icns")
            rers_path: str = path_join(self._path, "Contents", "Resources")
            os.makedirs(rers_path, exist_ok=True)
            shutil.copy(icon_path, rers_path)

    def create_shortcut(self, name: str, target: str, icon: str = None, desktop=True, start_menu=True):
        """
        Create shortcuts for the installed application.
        :param name: Shortcut display name
        :param target: Path to the executable
        :param icon: Path to icon (.ico)
        :param desktop: Whether to create on desktop
        :param start_menu: Whether to create in Start Menu
        """
        if self.is_windows():
            import winshell
            from win32com.client import Dispatch
            shell = Dispatch("WScript.Shell")

            if desktop:
                desktop_path = winshell.desktop()
                shortcut_path = os.path.join(desktop_path, f"{name}.lnk")
                self.make_windows_shortcut(shell, shortcut_path, target, icon)

            if start_menu:
                start_menu_path = winshell.start_menu()
                shortcut_path = os.path.join(start_menu_path, f"{name}.lnk")
                self.make_windows_shortcut(shell, shortcut_path, target, icon)

    def make_windows_shortcut(self, shell, shortcut_path: str, target: str, icon: str = None):
        """
        Helper method to create a Windows shortcut.
        :param shell: The WScript.Shell COM object
        :param shortcut_path: Path where the shortcut will be created
        :param target: Path to the executable
        :param icon: Path to icon (.ico)
        """
        if not self.is_windows():
            raise EnvironmentError("This method is only supported on Windows.")
        shortcut = shell.CreateShortCut(shortcut_path)
        shortcut.Targetpath = target
        shortcut.WorkingDirectory = os.path.dirname(target)
        if icon:
            shortcut.IconLocation = icon
        shortcut.save()

    def repair(self, progress_callback=None):
        """
        Repair the installation by re-extracting files and re-creating shortcuts.
        :param progress_callback: A callback function for reporting progress during extraction.
        """
        return self.install(progress_callback)

    def update(self, progress_callback=None):
        """
        Update the installation by re-extracting files and re-creating shortcuts.
        :param progress_callback: A callback function for reporting progress during extraction.
        """
        return self.install(progress_callback)

    def install(self, progress_callback=None):
        """
        Perform the installation process, including extracting files and creating shortcuts.
        :param progress_callback: A callback function for reporting progress during extraction.
        """
        self.extract(progress_callback)
        # exe_path : str = path_join(self._path, "baiss-desktop.exe")
        # icon_path: str = path_join(self._path, "icon.ico")
        # self.create_shortcut(self._title, exe_path, icon = icon_path)
        if self.is_macos():
            entrypoint: str = os.path.join(self._path, "Contents", "Resources", "Baiss.UI")
            os.chmod(entrypoint, 0o755)
            app = DotAppBundler(
                title            = self._title,
                entrypoint       = "Baiss.UI",
                path             = self._path,
                bundle_icon_file = "baiss-desktop-icon"
            )
            app.init_bundle()
            app.fini_bundle()

    def _create_symlink(self, source: str, destination: str):
        """
        Create a symbolic link from source to destination.
        :param source: The source file or directory
        :param destination: The destination path for the symlink
        """
        if not os.path.exists(destination):
            subprocess.run(["ln", "-s", source, destination])

    def create_macos_shortcuts(self, app_path: str):
        """
        Create shortcuts for the installed application on macOS.
        :param app_path: Path to the .app bundle
        """
        if not self.is_macos():
            raise EnvironmentError("This method is only supported on macOS.")

        desktop_path      : str = path_join(self.home, "Desktop"     , f"{self._title}.app")
        applications_path : str = path_join(self.home, "Applications", f"{self._title}.app")

        # Create Desktop Shortcut
        if not os.path.exists(desktop_path):
            self._create_symlink(source = app_path, destination = desktop_path)

        # Create Applications Shortcut
        if not os.path.exists(applications_path):
            self._create_symlink(source = app_path, destination = applications_path)

    def add_to_dock(self, app_path: str):

        if not self.is_macos():
            raise EnvironmentError("This method is only supported on macOS.")

        # By default

    # <start> windows </start>
    def create_windows_shortcut(self, dotexe: str, dotico: str , shortcut_dir: str = None):
        """
        Create a desktop shortcut for the installed application on Windows.
        """
        if not self.is_windows():
            raise EnvironmentError("This method is only supported on Windows.")
        if shortcut_dir is None:
            shortcut_dir = os.path.join(os.environ["USERPROFILE"], "Desktop")
        os.makedirs(shortcut_dir, exist_ok=True)
        import win32com.client
        shortcut_path             = os.path.join(shortcut_dir, f"{self._title}.lnk")
        shell                     = win32com.client.Dispatch("WScript.Shell")
        shortcut                  = shell.CreateShortcut(shortcut_path)
        shortcut.TargetPath       = dotexe
        shortcut.WorkingDirectory = os.path.dirname(dotexe)
        shortcut.IconLocation     = dotico
        shortcut.Description      = self.get_description()
        shortcut.Save()

    def configure_windows_start_menu(self , dotexe: str, dotico: str):
        if not self.is_windows():
            raise EnvironmentError("This method is only supported on Windows.")
        start_menu_dir = os.path.join(os.environ["APPDATA"], "Microsoft", "Windows", "Start Menu", "Programs")
        return self.create_windows_shortcut(
            dotexe = dotexe,
            dotico = dotico,
            shortcut_dir = start_menu_dir
        )

    def get_dotexe_path(self) -> str:
        """
        Get the path to the main executable of the installed application on Windows.
        """
        if not self.is_windows():
            raise EnvironmentError("This method is only supported on Windows.")
        return path_join(self._path, "Baiss.UI.exe")

    # <endof> windows </endof>

    # ! <start>             --------------         launch application    ------------------------ </start>

    def get_executable_path(self) -> str:
        """
        Get the path to the main executable based on the operating system.
        Returns the path to launch the application.
        """
        if self.is_macos():
            # On macOS, return path to .app bundle or the executable inside
            return self._path  # Launch the .app bundle
        elif self.is_windows():
            return path_join(self._path, "Baiss.UI.exe")
        else:  # Linux
            return path_join(self._path, "Baiss.UI")

    def launch_application(self):
        """
        Launch the installed application.
        """
        try:
            executable_path = self.get_executable_path()
            if not os.path.exists(executable_path):
                logger.error(f"Executable not found: {executable_path}")
                return False

            if self.is_macos():
                # On macOS, use 'open' command to launch .app bundle
                subprocess.Popen(["open", executable_path])
            elif self.is_windows():
                # On Windows, directly execute the .exe
                subprocess.Popen([executable_path], shell=False)
            else:  # Linux
                # On Linux, execute the binary
                subprocess.Popen([executable_path])

            logger.info(f"Application launched: {executable_path}")
            return True
        except Exception as e:
            logger.error(f"Failed to launch application: {e}")
            return False
    #  <end>             --------------         launch application    ------------------------ </end>

    def create_desktop_shortcut(self):
        """
        Create desktop shortcuts based on the operating system.
        """
        if self.is_macos():
            app_path: str = path_join(self._path)
            self.create_macos_shortcuts(app_path)

        if self.is_windows():
            self.create_windows_shortcut(
                dotexe = self.get_dotexe_path(),
                dotico = project_path("assets/ico/favicon.ico")
            )

    def configure_start_menu(self):
        """
        Configure start menu entries based on the operating system.
        """
        if self.is_macos():
            app_path: str = path_join(self._path)
            self.add_to_dock(app_path)
        elif self.is_windows():
            self.configure_windows_start_menu(
                dotexe = self.get_dotexe_path(),
                dotico = project_path("assets/ico/favicon.ico")
            )

if __name__ == "__main__":
    # baiss-1.0.0-osx-x64-python-venv.zip
    installer = BaissPackageInstaller()
    installer.extract()
