# docker run -it -v ${PWD}:/home/baiss.desktop python:3.11 bash -c "cd /home/baiss.desktop && bash"
"""

- https://baiss.ai/releases/releases.json
- https://baiss.ai/releases/win-x64/releases.json

"""

import os
import sys
import json
import uuid
import shutil
import logging
import tarfile
import zipfile
import datetime
import subprocess
import urllib.request
import platform as platform_module
from typing import List
logging.basicConfig(level = logging.INFO, format = "[%(levelname)s] %(message)s")
logger = logging.getLogger(__name__)

REPOSITORY_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOTNET_URLS = {
    "osx-x64"  : "https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.413/dotnet-sdk-8.0.413-osx-x64.tar.gz",
    "osx-arm64": "https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.413/dotnet-sdk-8.0.413-osx-arm64.tar.gz",

    "win-x64"  : "https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.413/dotnet-sdk-8.0.413-win-x64.zip",
    "win-arm64": "https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.413/dotnet-sdk-8.0.413-win-arm64.zip",

    "linux-x64": "https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.413/dotnet-sdk-8.0.413-linux-x64.tar.gz",
}

# - - - - - - - - - - - - - - - - - - - - <Configs> - - - - - - - - - - - - - - - - - - - -

class MacosConfig:
    DOTNET_FRAMEWORK                 = "8.0"
    DOTNET_RUNTIME_FRAMEWORK_VERSION = "8.0.0"
    SUPPORTED_OS_PLATFORM_VERSION    = "10.15" # Catalina
    MIN_OS_VERSION                   = "10.15" # Catalina
    TARGET_OS_VERSION                = "10.15" # Catalina

class WindowsConfig:
    DOTNET_FRAMEWORK                 = "8.0"
    DOTNET_RUNTIME_FRAMEWORK_VERSION = "8.0.0"
    MIN_OS_VERSION                   = "10.0.17763.0" # Windows 10 1809
    TARGET_OS_VERSION                = "10.0.19041.0" # Windows 10 2004

# - - - - - - - - - - - - - - - - - - - - </Configs> - - - - - - - - - - - - - - - - - - - -
def get_current_application_version() -> str:
    """Get the current application version based on the branch name."""
    branch_name = os.environ.get("GITHUB_REF_NAME")
    logger.info(f"Branch name: {branch_name}")
    if not os.path.exists("release.json"):
        raise Exception("release.json not found")
    with open("release.json", "r") as f:
        release = json.load(f)
    if branch_name == "dev":
        return release["currentVersion"] + "-beta"
    elif branch_name == "main":
        return release["currentVersion"]
    else:
        raise Exception(f"Branch {branch_name} is not supported.")

def is_latest_version() -> bool:
    """Check if the current version is the latest version."""
    branch_name = os.environ.get("GITHUB_REF_NAME")
    if branch_name == "dev":
        return False
    elif branch_name == "main":
        return True
    else:
        raise Exception(f"Branch {branch_name} is not supported.")

CURRENT_BAISS_DESKTOP_VERSION = get_current_application_version()


def path_join(*args) -> str:
    """
    Joins path components into a single path, normalizing separators.
    Args:
        *args: Path components to join.
    Returns:
        str: The joined and normalized path.
    Raises:
        ValueError: If no path items are provided.
    """
    items: List[str] = []
    for arg1 in args:
        arg1 = str(arg1).replace("\\", "/")
        if arg1.startswith("/"):
            items.append("/")
        for arg2 in arg1.split("/"):
            arg2 = arg2.strip("/")
            if arg2:
                items.append(arg2)
    if not items:
        raise ValueError("No path items provided.")
    path: str = "/".join(items)
    while ("//" in path):
        path = path.replace("//", "/")
    return path.replace("/", os.sep)

def platform() -> str:
    """Return the platform name based on the operating system."""
    if "darwin" in sys.platform:
        return "osx"
    elif "linux" in sys.platform:
        return "linux"
    return "win"

def archname() -> str:
    arch = platform_module.machine().lower()
    if arch in ["amd64", "x86_64"]:
        return "x64"
    if arch in ["i386", "i686", "x86"]:
        return "x86"
    if "arm" in arch or "aarch" in arch:
        return "arm64"
    return arch

def is_valid_runtime(runtime: str) -> bool:
    """
    Validate if the given runtime string is in the correct format and supported.
    Args:
        runtime (str): The runtime string to validate (e.g., "win-x64").
    Returns:
        bool: True if the runtime is valid, False
    """
    parts = runtime.split("-")
    if len(parts) != 2:
        return False
    plat, arch = parts
    valid_plats = ["win", "osx", "linux"]
    valid_archs = ["x64", "arm64", "x86"]
    return plat in valid_plats and arch in valid_archs

def get_current_runtime() -> str:
    """
    Get the current runtime identifier based on the operating system and architecture.
    Returns:
        str: The runtime identifier in the format "<platform>-<architecture>".
    """
    return f"{platform()}-{archname()}"

def get_repo_root() -> str:
    """
    Get the root directory of the repository by navigating two levels up from the current file's location.
    Raises FileNotFoundError if the expected directories are not found.
    Returns:
        str: The absolute path to the repository root directory.
    """
    repo_root   : str = REPOSITORY_ROOT # Use fixed path, to avoid issues, during chdir changes
    baissui_dir : str = os.path.join(repo_root, "Baiss", "Baiss.UI")
    baiss_core  : str = os.path.join(repo_root, "core")
    if not os.path.exists(baissui_dir):
        raise FileNotFoundError(f"Baiss.UI directory not found: {baissui_dir}")
    if not os.path.exists(baiss_core):
        raise FileNotFoundError(f"core directory not found: {baiss_core}")
    return repo_root

def get_dot_tmp_dir() -> str:
    """
    Get or create the .tmp directory in the repository root.
    Returns:
        str: The absolute path to the .tmp directory.
    Raises:
        FileNotFoundError: If the repository root cannot be determined.
    """
    dot_tmp_dir: str = os.path.join(get_repo_root(), ".tmp")
    os.makedirs(dot_tmp_dir, exist_ok = True)
    return dot_tmp_dir

def get_baissui_dir() -> str:
    """
    Get the Baiss.UI directory path.
    Returns:
        str: The absolute path to the Baiss.UI directory.
    Raises:
        FileNotFoundError: If the Baiss.UI directory does not exist.
    """
    baissui_dir: str = os.path.join(get_repo_root(), "Baiss", "Baiss.UI")
    if not os.path.exists(baissui_dir):
        raise FileNotFoundError(f"Baiss.UI directory not found: {baissui_dir}")
    return baissui_dir

def get_build_output_dir() -> str:
    """
    Get or create the build output directory inside the .tmp directory.
    Returns:
        str: The absolute path to the build output directory.
    Raises:
        FileNotFoundError: If the .tmp directory cannot be determined.
    """
    build_output_dir: str = os.path.join(get_dot_tmp_dir(), "build")
    os.makedirs(build_output_dir, exist_ok = True)
    return build_output_dir

def get_dotnet_install_dir() -> str:
    """
    Get the .NET installation directory inside the .tmp directory.
    Returns:
        str: The absolute path to the .NET installation directory.
    Raises:
        FileNotFoundError: If the .tmp directory cannot be determined.
    """
    dotnet_install_dir: str = os.path.join(get_dot_tmp_dir(), "sysroot", get_current_runtime(), "opt", "dotnet")
    os.makedirs(dotnet_install_dir, exist_ok = True)
    return dotnet_install_dir

def get_downloads_dir() -> str:
    """
    Get or create the downloads directory inside the .tmp directory.
    Returns:
        str: The absolute path to the downloads directory.
    Raises:
        FileNotFoundError: If the .tmp directory cannot be determined.
    """
    downloads_dir: str = os.path.join(get_dot_tmp_dir(), "downloads")
    os.makedirs(downloads_dir, exist_ok = True)
    return downloads_dir

def download_file(url: str, output_path: str):
    """
    Download a file from a URL to a specified output path.
    Args:
        url (str): The URL of the file to download.
        output_path (str): The path where the downloaded file will be saved.
    Raises:
        Exception: If the download fails.
    """
    try:
        with urllib.request.urlopen(url) as response, open(output_path, "wb") as out_file:
            shutil.copyfileobj(response, out_file)
    except Exception as e:
        import ssl
        import certifi
        context = ssl.create_default_context(cafile=certifi.where())
        with urllib.request.urlopen(url, context=context) as response, open(output_path, "wb") as out_file:
            shutil.copyfileobj(response, out_file)

def download_dotnet():
    """
    Download and extract the .NET SDK for the current platform if not already installed.
    Returns:
        str: The path to the dotnet executable.
    Raises:
        RuntimeError: If no .NET SDK URL is configured for the current platform.
        FileNotFoundError: If the .NET SDK executable is not found after extraction.
    """
    plat_arch: str = get_current_runtime()
    os.makedirs(get_dotnet_install_dir(), exist_ok = True)
    if not (plat_arch in DOTNET_URLS):
        raise RuntimeError(f"No .NET SDK URL configured for {plat_arch}")
    dotnet_url: str = DOTNET_URLS[plat_arch]
    dotnet_zip: str = os.path.join(get_downloads_dir(), os.path.basename(dotnet_url))
    dotnet_exe: str = os.path.join(get_dotnet_install_dir(), "dotnet")

    if platform() == "win":
        dotnet_exe += ".exe"

    if os.path.exists(dotnet_exe):
        logger.info(".NET SDK already installed at %s", get_dotnet_install_dir())
        return dotnet_exe

    if os.path.exists(dotnet_zip):
        logger.info(".NET SDK archive already downloaded at %s", dotnet_zip)
    else:
        logger.info("Downloading .NET SDK from %s", dotnet_url)
        download_file(url = dotnet_url, output_path = dotnet_zip)
        logger.info(".NET SDK archive downloaded to %s", dotnet_zip)

    # Handle different archive formats based on file extension
    if dotnet_zip.endswith('.zip'):
        with zipfile.ZipFile(dotnet_zip, 'r') as zip_ref:
            zip_ref.extractall(get_dotnet_install_dir())
    elif dotnet_zip.endswith('.tar.gz'):
        with tarfile.open(dotnet_zip, "r:gz") as tar:
            tar.extractall(get_dotnet_install_dir())
    else:
        raise RuntimeError(f"Unsupported archive format: {dotnet_zip}")

    # Make sure `dotnet` is executable
    if platform() != "win":
        os.chmod(dotnet_exe, 0o755)

    if not os.path.exists(dotnet_exe):
        raise FileNotFoundError(f".NET SDK executable not found after extraction: {dotnet_exe}")

    logger.info(".NET SDK ready: %s", dotnet_exe)
    return dotnet_exe

class CrossPlatformBuilder:
    """
    A class to build a .NET application for multiple platforms using the `dotnet publish` command.
    Attributes:
    runtime (str): The target runtime identifier (e.g., "win-x64", "osx-arm64").
    outdir (str): The output directory for the build artifacts.
    dotnet_exe (str): The path to the `dotnet` executable.
    override (bool): Whether to override existing build outputs.
    Methods:
    build(): Build the application for the specified runtime.
    compress(): Compress the build output directory into a zip archive.
    command (str): The constructed `dotnet publish` command.
    Raises:
    NotImplementedError: If the specified runtime is not supported.
    RuntimeError: If the build process fails.
    FileNotFoundError: If the build output directory does not exist.
    """
    def __init__(self,
            runtime   : str,
            outdir    : str = get_build_output_dir(),
            dotnet_exe: str = "dotnet",
            override  : bool = False,
        ):
        self._override = override
        self._runtime  = runtime
        self._output   = f"{outdir}/{self._runtime}"
        self._config   = None
        os.makedirs(outdir, exist_ok = True)
        if self._runtime.startswith("osx"):
            self._config = MacosConfig()
        elif self._runtime.startswith("win"):
            self._config = WindowsConfig()
        else:
            raise NotImplementedError(f"Unsupported platform: {platform()}")

        base_command = [
            dotnet_exe, "publish", "--configuration", "Release",
            "--runtime", self._runtime,
            "--output", self._output,
            "--self-contained", "true",
            "-p:PublishSingleFile=true",
            "-p:PublishReadyToRun=true",
            f"--framework", f"net{self._config.DOTNET_FRAMEWORK}",
            f"-p:RuntimeFrameworkVersion={self._config.DOTNET_RUNTIME_FRAMEWORK_VERSION}"
        ]
        if self._runtime.startswith("osx"):
            # macOS specific settings
            platform_command = [
                f"-p:SupportedOSPlatformVersion={self._config.SUPPORTED_OS_PLATFORM_VERSION}",
                f"-p:MacOSXMinVersion={self._config.MIN_OS_VERSION}",
                f"-p:TargetOSVersion={self._config.TARGET_OS_VERSION}"
            ]
        elif self._runtime.startswith("win"):
            # Windows specific settings
            platform_command = [
                f"-p:SupportedOSPlatformVersion={self._config.MIN_OS_VERSION}",
                f"-p:TargetPlatformMinVersion={self._config.MIN_OS_VERSION}",
                "-p:WindowsAppSDKSelfContained=true"
            ]
        else:
            raise NotImplementedError(f"Unsupported platform: {self._runtime}")

        self._command = base_command + platform_command

    @property
    def command(self) -> str:
        if not self._command or not isinstance(self._command, list):
            raise RuntimeError("Build command not initialized.")
        return self._command

    @staticmethod
    def shell(cmd: list[str]) -> str:
        if not cmd or not isinstance(cmd, list):
            raise RuntimeError("Build command not initialized.")
        # logger.info(f"\n\t\t >>>> Running command: {cmd} <<<<")
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
        return result.stdout

    def build(self):
        """
        Build the application for the specified runtime.
        Returns:
        bool: True if the build and compression were successful, False otherwise.
        Raises:
        RuntimeError: If the build process fails.
        """
        if os.path.exists(self._output):
            if self._override:
                logger.warning("Overriding existing build output at %s", self._output)
                shutil.rmtree(self._output)
            else:
                logger.info("Build output already exists at %s, skipping build.", self._output)
                return self.compress()
        logger.info("Building for runtime: %s", self._output)
        os.chdir(get_baissui_dir())
        CrossPlatformBuilder.shell(self.command)
        about_file: str  = os.path.join(self._output, "about.json")
        about_json: dict = {
            "website"     : "https://baiss.ai",
            "version"     : CURRENT_BAISS_DESKTOP_VERSION,
            "updated_at"  : datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "random_uuid4": str(uuid.uuid4()),
        }
        os.makedirs(os.path.dirname(about_file), exist_ok = True)
        with open(about_file, 'w') as f:
            json.dump(about_json, f, indent=4)
        logger.info("Build completed for runtime: %s", self._output)
        return self.compress()

    def compress(self):
        """
        Compress the build output directory into a zip archive.
        Returns:
        bool: True if compression was successful, False otherwise.
        Raises:
        FileNotFoundError: If the build output directory does not exist.
        RuntimeError: If compression fails.
        """
        self.compress_baiss_frontend()
        self.compress_baiss_backend_core()
    
    def compress_baiss_frontend(self):
        """
        Compress the build output directory into a zip archive.
        Returns:
            bool: True if compression was successful, False otherwise.
        Raises:
            FileNotFoundError: If the build output directory does not exist.
        """
        baiss_zip: str = os.path.join(get_downloads_dir(), f"baiss-desktop-{CURRENT_BAISS_DESKTOP_VERSION}-{self._runtime}.zip")
        if os.path.exists(baiss_zip):
            if self._override:
                logger.warning("Overriding existing archive at %s", baiss_zip)
                os.remove(baiss_zip)
            else:
                logger.info("Archive already exists at %s, skipping compression.", baiss_zip)
                return
        shutil.make_archive(base_name = baiss_zip[:-4], format = 'zip', root_dir = self._output)
        if not os.path.exists(baiss_zip):
            raise FileNotFoundError(f"Failed to create archive: {baiss_zip}")
        logger.info("Compressed build output to %s", baiss_zip)
        return True

    def compress_baiss_backend_core(self):
        """
        Compress the core directory into a zip archive containing the 'core' folder at the top level.
        """
        core_dir  = os.path.join(get_repo_root(), "core")
        baiss_zip = os.path.join(get_downloads_dir(), f"baiss-core-{CURRENT_BAISS_DESKTOP_VERSION}-{self._runtime}.zip")
        if not os.path.exists(core_dir):
            raise FileNotFoundError(f"Core directory does not exist: {core_dir}")
        if os.path.exists(baiss_zip):
            logger.info("Archive already exists at %s, skipping compression.", baiss_zip)
            return baiss_zip
        shutil.make_archive(
            base_name = baiss_zip[:-4],
            format    = 'zip',
            root_dir  = os.path.dirname(core_dir),  # parent directory
            base_dir  = os.path.basename(core_dir)  # "core"
        )
        if not os.path.exists(baiss_zip):
            raise FileNotFoundError(f"Failed to create archive: {baiss_zip}")
        logger.info("Compressed core directory (with top-level folder) to %s", baiss_zip)
        return baiss_zip

def get_target_runtimes(argv: List[str]) -> List[str]:
    runtimes: List[str] = []
    keywords = ["--target-runtime="]
    for item in argv:
        litem: str = item.strip().lower()
        for keyword in keywords:
            if not litem.startswith(keyword):
                continue
            for runtime in litem[len(keyword):].split(","):
                runtime = runtime.strip().lower()
                if runtime in ["current", "this", "me"]:
                    runtime = get_current_runtime()
                if runtime in ["all", "*"]:
                    runtimes.extend(list(DOTNET_URLS))
                    continue
                runtimes.append(runtime)
    runtimes = list(set(runtimes))
    for runtime in runtimes:
        if not is_valid_runtime(runtime):
            raise ValueError(f"Invalid runtime specified: {runtime}")
    if not runtimes:
        all_runtimes = ','.join(list(DOTNET_URLS))
        print("No target runtimes specified, defaulting to current runtime.")
        print(f"Usage: python {argv[0]} --target-runtime={get_current_runtime()}")
        print(f"       python {argv[0]} --target-runtime={all_runtimes}")
        print(f"       python {argv[0]} --target-runtime=all")
        print(f"       python {argv[0]} --target-runtime=current")
        print(f"Available runtimes: {all_runtimes}")
        raise ValueError("No target runtimes specified.")

    return runtimes

def main(argv):
    runtimes   = get_target_runtimes(argv)
    dotnet_exe = download_dotnet()
    failed = []
    for runtime in runtimes:
        try:
            CrossPlatformBuilder(runtime = runtime, dotnet_exe = dotnet_exe).build()
        except Exception as e:
            logger.warning("Build failed for runtime %s: %s", runtime, str(e))
            failed.append(runtime)
    if failed:
        logger.error("Build failed for runtimes: %s", ", ".join(failed))
        raise RuntimeError("Build process encountered errors.")

    logger.info("Build completed successfully for all runtimes.")

if __name__ == "__main__":
    # python scripts/build.py --runtime=win-x64,osx-x64
    logger.info("Current Runtime: %s", get_current_runtime())
    exit( main(sys.argv) )
