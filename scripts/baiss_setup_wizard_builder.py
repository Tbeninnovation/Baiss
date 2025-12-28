import os
import uuid
import base64
import shutil
import logging
import dotenv
import subprocess
from build import platform
from build import path_join
from build import download_file
from build import get_repo_root
from build import get_dot_tmp_dir
from build import get_downloads_dir
from build import get_current_runtime
from build import CURRENT_BAISS_DESKTOP_VERSION

dotenv.load_dotenv()

logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

class BaissSetupWizardBuilder:

    def __init__(self,
            project_root: str,
            app_name    : str = "BaissSetupWizard",
        ):
        self._app_name     = app_name
        self._project_root = project_root
        self._app_dir      = path_join(self._project_root, "app")
        self._temp_dir     = path_join(get_dot_tmp_dir(), "baiss-setup-wizard-temp")
        self._build_dir    = path_join(self._temp_dir, "build")
        self._icon_path    = path_join("app", "assets", "icons", "icon_256x256.png")
        self._assets_path  = path_join("app", "assets")
        self._entrypoint   = path_join("app", "main.py")
        if not os.path.exists(path_join(project_root, self._entrypoint)):
            raise FileNotFoundError(f"Entrypoint not found: {self._entrypoint}")

    def remove(self, path: str) -> bool:
        if not os.path.exists(path):
            return True
        try   : shutil.rmtree(path)
        except: pass
        if os.path.exists(path):
            return False
        return True

    def mkdirp(self, path: str):
        os.makedirs(path, exist_ok=True)

    def create_entitlements_file(self) -> str:
        """Create entitlements.plist file for code signing"""
        entitlements_content = """<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.cs.allow-unsigned-executable-memory</key>
    <true/>
    <key>com.apple.security.cs.disable-library-validation</key>
    <true/>
</dict>
</plist>"""

        entitlements_path = path_join(self._temp_dir, "entitlements.plist")
        with open(entitlements_path, "w") as f:
            f.write(entitlements_content)

        logger.info(f"Created entitlements file: {entitlements_path}")
        return entitlements_path

    @staticmethod
    def shell(cmd) -> str:
        if not cmd or not isinstance(cmd, list):
            raise RuntimeError("Build command not initialized.")
        result = subprocess.run(
            cmd,
            capture_output=True,  # Captures both stdout and stderr
            text=True             # Decodes bytes to string
        )
        # Log stdout
        if result.stdout:
            logger.info(result.stdout)
        # Log stderr
        if result.stderr:
            logger.warning(result.stderr)
        if result.returncode != 0:
            raise RuntimeError(f"Command failed with exit code {result.returncode}")
        return result

    def prepare(self):
        logger.info("Preparing build environment...")
        self.remove( self._temp_dir )
        self.mkdirp( self._temp_dir )
        self.remove( path_join(self._build_dir, get_current_runtime()) )
        self.mkdirp( path_join(self._build_dir, get_current_runtime()) )
        shutil.copytree(self._app_dir, path_join(self._temp_dir, "app"))
        tmp_zip: str = path_join(self._temp_dir, "app/assets/zip")
        self.remove( tmp_zip )
        self.mkdirp( tmp_zip )
        founds : dict[str, str] = {}
        BaissSetupWizardBuilder.compress_baiss_python_venv( get_current_runtime() )
        logger.info( f"Searching for Baiss and Python binaries in downloads directory: {os.listdir(get_downloads_dir())}" )
        for basename in os.listdir( get_downloads_dir() ):
            lbasename: str = basename.lower()
            for name in ["baiss", "python"]:
                if not (name in lbasename):
                    continue
                if not lbasename.endswith(".zip"):
                    continue
                if not (get_current_runtime() in lbasename):
                    continue
                filename = path_join(get_downloads_dir(), basename)
                shutil.copy( filename, tmp_zip )
                founds[name] = filename
                break
        logger.info( f"Found binaries for: {founds}" )
        if not ("baiss" in founds):
            raise FileNotFoundError("Baiss binary not found in downloads directory.")

    def sign_macos_app(self, dotapp_dir: str, team_name: str = None, team_id: str = None) -> int:

        if not os.path.exists(dotapp_dir):
            raise RuntimeError(f"Build failed, .app not found: {dotapp_dir}")

        if not ("BAISS_P12_FILE" in os.environ):
            BAISS_P12_BASE64 = os.environ["BAISS_P12_BASE64"]
            secrets_dir: str = path_join( get_repo_root(), "secrets" )
            os.makedirs(secrets_dir, exist_ok=True)
            secrets_file: str = path_join( secrets_dir, f"baiss-apple-certificates-{uuid.uuid4()}.p12" )
            with open(secrets_file, "wb") as f:
                f.write(base64.b64decode(BAISS_P12_BASE64))
            os.environ["BAISS_P12_FILE"] = secrets_file

        BAISS_KEYCHAIN_FILE = os.environ["BAISS_KEYCHAIN_FILE"]
        BAISS_KEYCHAIN_PASS = os.environ["BAISS_KEYCHAIN_PASS"]
        BAISS_P12_FILE      = os.environ["BAISS_P12_FILE"]
        BAISS_P12_PASS      = os.environ["BAISS_P12_PASS"]

        self.shell(["security", "create-keychain", "-p", BAISS_KEYCHAIN_PASS, BAISS_KEYCHAIN_FILE])
        self.shell(["security", "import", BAISS_P12_FILE, "-k", BAISS_KEYCHAIN_FILE, "-P", BAISS_P12_PASS, "-T", "/usr/bin/codesign"])
        self.shell(["security", "list-keychains", "-s", BAISS_KEYCHAIN_FILE])
        self.shell(["security", "default-keychain", "-s", BAISS_KEYCHAIN_FILE])
        self.shell(["security", "unlock-keychain", "-p", BAISS_KEYCHAIN_PASS, BAISS_KEYCHAIN_FILE])
        self.shell([
            "security", "set-key-partition-list",
            "-S", "apple-tool:,apple:",
            "-s",
            "-k", BAISS_KEYCHAIN_PASS,
            BAISS_KEYCHAIN_FILE
        ])
        logger.info("Retrieving signing identity...")
        result = self.shell(["security", "find-identity", "-v", "-p", "codesigning"])
        signid = str(result.stdout)
        signid = signid[signid.index("Developer ID Application:"):]
        signid = signid[:signid.index(")") + 1]
        # logger.info(f"Signing identity found: {signid}")

        # Create entitlements file
        entitlements_path = self.create_entitlements_file()

        logger.info("Signing .app bundle with entitlements...")
        # Sign with entitlements to allow loading unsigned libraries
        self.shell([
            "codesign", "--force",
            "--options", "runtime",
            "--entitlements", entitlements_path,
            "--sign", signid,
            "--timestamp",
            dotapp_dir
        ])

        logger.info("Verifying .app signature...")
        self.shell([
            "codesign", "--verify",
            "--deep",
            "--strict",
            "--verbose=2",
            dotapp_dir
        ])

        logger.info(".app signed successfully.")
        return 0

    def notarize_macos_dmg(self, dmg_path: str) -> int:
        """
        Notarize the macOS DMG with Apple's notarization service.
        Returns:
            int: Exit code (0 for success, non-zero for failure).
        Raises:
            RuntimeError: If the notarization process fails at any step.
        """
        if not os.path.exists(dmg_path):
            raise RuntimeError(f"DMG file not found for notarization: {dmg_path}")

        APPLE_ID = os.environ.get("APPLE_ID")
        TEAM_ID = os.environ.get("TEAM_ID")
        PASSWORD = os.environ.get("PASSWORD")

        if not APPLE_ID or not TEAM_ID or not PASSWORD:
            logger.warning("Apple notarization credentials not found. Skipping notarization.")
            return 0

        logger.info(f"Starting notarization for: {dmg_path}")

        command = [
            "xcrun", "notarytool", "submit", dmg_path,
            "--apple-id", APPLE_ID,
            "--team-id", TEAM_ID,
            "--password", PASSWORD,
            "--wait"
        ]

        self.shell(command)
        logger.info("DMG notarized successfully.")
        return 0

    def build_macos_dmg(self) -> int:
        """
        Build the macOS DMG installer for the Baiss Setup Wizard.
        Returns:
            int: Exit code (0 for success, non-zero for failure).
        Raises:
            EnvironmentError: If the script is not run on a macOS host.
            RuntimeError: If the build process fails at any step.
        """
        os.chdir( os.path.join(self._build_dir, get_current_runtime()) )
        setup_dmg : str = path_join(get_downloads_dir(), f"baiss-setup-{CURRENT_BAISS_DESKTOP_VERSION}-{get_current_runtime()}.dmg")
        dotapp_dir: str = path_join(self._temp_dir, "dist", f"{self._app_name}.app")
        command = [
            "hdiutil",
            "create",
            "-volname",
            self._app_name,
            "-srcfolder",
            dotapp_dir,
            "-ov", "-format", "UDZO",
            "-megabytes", "512",
            setup_dmg
        ]
        self.remove(setup_dmg)
        self.shell(command)

        # Notarize the DMG
        self.notarize_macos_dmg(setup_dmg)

        return 0

    def build_macos_zip(self) -> int:
        """
        Build the macOS ZIP archive for the Baiss Setup Wizard.
        Returns:
            int: Exit code (0 for success, non-zero for failure).
        Raises:
            RuntimeError: If the build process fails at any step.
        """
        os.chdir(self._build_dir)
        dotapp_dir = path_join(self._temp_dir, "dist", f"{self._app_name}.app")
        if not os.path.exists(dotapp_dir):
            raise RuntimeError(f"Build failed, .app not found: {dotapp_dir}")
        setup_zip: str = path_join(get_downloads_dir(), f"baiss-setup-{CURRENT_BAISS_DESKTOP_VERSION}-{get_current_runtime()}.zip")
        self.remove(setup_zip)
        shutil.make_archive(base_name = setup_zip[:-4], format = 'zip', root_dir = dotapp_dir)

    def build_macos(self) -> int:
        """
        Build the macOS application using PyInstaller.
        Returns:
            int: Exit code (0 for success, non-zero for failure).
        Raises:
            EnvironmentError: If the script is not run on a macOS host.
            RuntimeError: If the build process fails at any step.
        """
        if not platform().startswith("osx"):
            raise EnvironmentError("MacOS build can only be run on MacOS hosts.")
        self.prepare()
        os.chdir(self._temp_dir)
        command = [
            "pyinstaller",
            "--onefile"  ,
            "--windowed" ,
            "--name"     , self._app_name,
            "--icon"     , self._icon_path,
            "--add-data" , self._assets_path + ":assets",
            "--osx-bundle-identifier", "com.baiss.setupwizard",
            self._entrypoint
        ]

        logger.info("Building application with PyInstaller...")
        self.shell(command)
        dotapp_dir = path_join(self._temp_dir, "dist", f"{self._app_name}.app")
        if not os.path.exists(dotapp_dir):
            raise RuntimeError(f"Build failed, .app not found: {dotapp_dir}")
        self.sign_macos_app(dotapp_dir = dotapp_dir)
        self.build_macos_zip()
        self.build_macos_dmg()

    def build_windows(self) -> int:
        if not platform().startswith("win"):
            raise EnvironmentError("Windows build can only be run on Windows hosts.")
        self.prepare()
        os.chdir(self._temp_dir)
        command = [
            "pyinstaller",
            "--onefile",
            "--windowed",
            "--name", self._app_name,
            "--icon", self._icon_path,
            "--add-data", f"{self._assets_path};assets", # On Windows, --add-data uses ; separator
            self._entrypoint
        ]
        self.shell(command)
        self.build_windows_exe()
        self.build_windows_zip()

    def build_windows_exe(self) -> int:
        """
        Build the Windows EXE installer for the Baiss Setup Wizard.
        Returns:
            int: Exit code (0 for success, non-zero for failure).
        Raises:
            RuntimeError: If the build process fails at any step.
        """
        dist_dir  : str = path_join(self._temp_dir, "dist")
        source_exe: str = path_join(dist_dir, f"{self._app_name}.exe")
        dest_exe  : str = path_join(get_downloads_dir(), f"baiss-setup-{CURRENT_BAISS_DESKTOP_VERSION}-{get_current_runtime()}.exe")
        if not os.path.exists(source_exe):
            raise RuntimeError(f"Build failed, .exe not found: {source_exe}")
        self.remove(dest_exe)
        shutil.copy(source_exe, dest_exe)
        return 0

    def build_windows_zip(self) -> int:
        """
        Build the Windows ZIP archive for the Baiss Setup Wizard.
        Returns:
            int: Exit code (0 for success, non-zero for failure).
        Raises:
            RuntimeError: If the build process fails at any step.
        """
        dist_dir  : str = path_join(self._temp_dir, "dist")
        source_exe: str = path_join(dist_dir, f"{self._app_name}.exe")
        if not os.path.exists(source_exe):
            raise RuntimeError(f"Build failed, .exe not found: {source_exe}")
        setup_zip: str = path_join(get_downloads_dir(), f"baiss-setup-{CURRENT_BAISS_DESKTOP_VERSION}-{get_current_runtime()}.zip")
        self.remove(setup_zip)
        shutil.make_archive(base_name = setup_zip[:-4], format = 'zip', root_dir = dist_dir, base_dir = f"{self._app_name}.exe")
        return 0

    def build(self) -> int:
        if platform().startswith("osx"):
            return self.build_macos()
        elif platform().startswith("win"):
            return self.build_windows()
        raise EnvironmentError("Unsupported platform for build.")

    @staticmethod
    def compress_baiss_python_venv(runtime: str = get_current_runtime()) -> str | None:
        logger.info( f"Compressing Baiss Python venv for runtime: {runtime}" )
        return ""
        python_zip: str = path_join(get_downloads_dir(), f"baiss-python-{runtime}.zip")
        if os.path.exists(python_zip):
            logger.info( f"Python zip already exists: {python_zip}" )
            return python_zip
        venv_dir: str = path_join( get_dot_tmp_dir(), "sysroot", runtime, "opt", "miniconda", "envs", "baiss_venv" )
        if not os.path.exists( venv_dir ):
            logger.error( f"Failed to download Python zip: {python_zip}" )
            raise RuntimeError("Failed to download Baiss Python venv.")
        logger.info( f"Compressing Python venv: {venv_dir}" )
        shutil.make_archive(
            base_name = python_zip[:-4],
            format    = 'zip',
            root_dir  = os.path.dirname(venv_dir),
            base_dir  = os.path.basename(venv_dir)
        )
        if not os.path.exists(python_zip):
            logger.error( f"Failed to create Python zip: {python_zip}" )
            raise RuntimeError("Failed to compress Python venv.")
        logger.info( f"Python zip created: {python_zip}" )
        return python_zip

def main() -> int:
    REPO_ROOT   : str = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    project_root: str = path_join(REPO_ROOT, "baiss.installer")
    b = BaissSetupWizardBuilder(
        project_root = project_root,
        app_name     = "BaissSetupWizard",
    )
    b.build()

if __name__ == "__main__":
    exit(main())
