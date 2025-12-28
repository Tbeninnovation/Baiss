import os
import sys
import logging
import tarfile
import zipfile
import subprocess
from build import download_file
from build import get_repo_root
from build import get_dot_tmp_dir
from build import get_downloads_dir
from build import get_current_runtime
from build import platform

logging.basicConfig(level=logging.INFO, format="[%(levelname)s] %(message)s")
logger = logging.getLogger(__name__)

# Python build standalone releases from astral-sh
# https://github.com/astral-sh/python-build-standalone/releases
PYTHON_BUILD_STANDALONE_URLS = {
    "osx-arm64": "https://github.com/astral-sh/python-build-standalone/releases/download/20241219/cpython-3.13.1+20241219-aarch64-apple-darwin-install_only.tar.gz",
    "osx-x64"  : "https://github.com/astral-sh/python-build-standalone/releases/download/20241219/cpython-3.13.1+20241219-x86_64-apple-darwin-install_only.tar.gz",
    "win-x64"  : "https://github.com/astral-sh/python-build-standalone/releases/download/20241219/cpython-3.13.1+20241219-x86_64-pc-windows-msvc-install_only.tar.gz",
    # TODO: Add win-arm64 later
    "win-arm64": "https://github.com/astral-sh/python-build-standalone/releases/download/20241219/cpython-3.13.1+20241219-x86_64-pc-windows-msvc-install_only.tar.gz",
}


def get_python_install_dir() -> str:
    """Get the directory where Python will be installed."""
    python_install_dir: str = os.path.join(get_dot_tmp_dir(), "sysroot", get_current_runtime(), "opt", "python-venv")
    os.makedirs(python_install_dir, exist_ok=True)
    return python_install_dir


def shell(cmd: list[str], cwd: str = None) -> str:
    """Run a shell command and return stdout."""
    logger.info(f"Running command: {' '.join(cmd)}")
    result = subprocess.run(
        cmd,
        capture_output=True,
        text=True,
        cwd=cwd
    )
    if result.stdout:
        logger.info(result.stdout)
    if result.stderr:
        logger.warning(result.stderr)
    if result.returncode != 0:
        raise RuntimeError(f"Command failed with exit code {result.returncode}: {result.stderr}")
    return result.stdout


class PythonBuilder:
    """
    Builder class for downloading pre-built Python from astral-sh/python-build-standalone,
    installing requirements, and packaging for deployment.
    """

    def __init__(self, runtime: str = None):
        self.repo_root = get_repo_root()
        self.tmp_dir = get_dot_tmp_dir()
        self.downloads_dir = get_downloads_dir()
        self._runtime = runtime or get_current_runtime()
        
        if self._runtime not in PYTHON_BUILD_STANDALONE_URLS:
            raise ValueError(f"Unsupported runtime: {self._runtime}. Supported: {list(PYTHON_BUILD_STANDALONE_URLS.keys())}")

    def _get_python_url(self) -> str:
        """Get the Python download URL for the current runtime."""
        return PYTHON_BUILD_STANDALONE_URLS[self._runtime]

    def _get_python_archive_path(self) -> str:
        """Get the path where the Python archive will be downloaded."""
        url = self._get_python_url()
        basename = os.path.basename(url)
        return os.path.join(self.downloads_dir, basename)

    def _get_python_exe(self) -> str:
        """Get the path to the Python executable."""
        python_dir = get_python_install_dir()
        if self._runtime.startswith("win"):
            return os.path.join(python_dir, "python", "python.exe")
        else:
            return os.path.join(python_dir, "python", "bin", "python3")

    def _get_pip_exe(self) -> str:
        """Get the path to the pip executable."""
        python_dir = get_python_install_dir()
        if self._runtime.startswith("win"):
            return os.path.join(python_dir, "python", "Scripts", "pip.exe")
        else:
            return os.path.join(python_dir, "python", "bin", "pip3")

    def download_python(self) -> str:
        """Download the pre-built Python archive."""
        url = self._get_python_url()
        archive_path = self._get_python_archive_path()
        
        if os.path.exists(archive_path):
            logger.info(f"Python archive already downloaded at: {archive_path}")
            return archive_path
        
        logger.info(f"Downloading Python from: {url}")
        download_file(url, archive_path)
        
        if not os.path.exists(archive_path):
            raise RuntimeError(f"Failed to download Python from: {url}")
        
        logger.info(f"Successfully downloaded Python to: {archive_path}")
        return archive_path

    def extract_python(self) -> str:
        """Extract the Python archive to the install directory."""
        archive_path = self.download_python()
        python_dir = get_python_install_dir()
        python_exe = self._get_python_exe()
        
        if os.path.exists(python_exe):
            logger.info(f"Python already extracted at: {python_dir}")
            return python_exe
        
        logger.info(f"Extracting Python to: {python_dir}")
        
        # Extract tar.gz archive
        with tarfile.open(archive_path, "r:gz") as tar:
            tar.extractall(python_dir)
        
        # Make Python executable on Unix systems
        if not self._runtime.startswith("win"):
            os.chmod(python_exe, 0o755)
            # Also make other binaries executable
            bin_dir = os.path.dirname(python_exe)
            for f in os.listdir(bin_dir):
                filepath = os.path.join(bin_dir, f)
                if os.path.isfile(filepath):
                    os.chmod(filepath, 0o755)
        
        if not os.path.exists(python_exe):
            raise RuntimeError(f"Failed to extract Python. Executable not found: {python_exe}")
        
        logger.info(f"Successfully extracted Python: {python_exe}")
        return python_exe

    def install_requirements(self) -> None:
        """Install requirements from requirements.txt into the Python environment."""
        requirements_txt = os.path.join(self.repo_root, "core", "baiss", "requirements.txt")
        
        if not os.path.exists(requirements_txt):
            raise FileNotFoundError(f"Requirements file not found: {requirements_txt}")
        
        python_exe = self.extract_python()
        pip_exe = self._get_pip_exe()
        
        logger.info(f"Upgrading pip...")
        shell([python_exe, "-m", "pip", "install", "--upgrade", "pip"])
        
        logger.info(f"Installing requirements from: {requirements_txt}")
        shell([python_exe, "-m", "pip", "install", "-r", requirements_txt])
        
        logger.info(f"Requirements installed successfully")

    def package(self) -> str:
        """Package the Python environment into a zip file for deployment."""
        python_dir = get_python_install_dir()
        output_filename = f"baiss-python-venv-{self._runtime}.zip"
        output_path = os.path.join(self.downloads_dir, output_filename)
        
        if os.path.exists(output_path):
            logger.info(f"Removing existing package: {output_path}")
            os.remove(output_path)
        
        logger.info(f"Packaging Python environment to: {output_path}")
        
        # Create zip archive
        with zipfile.ZipFile(output_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
            for root, dirs, files in os.walk(python_dir):
                for file in files:
                    file_path = os.path.join(root, file)
                    arcname = os.path.relpath(file_path, python_dir)
                    # Store under python-venv/ prefix for consistency
                    arcname = os.path.join("python-venv", arcname)
                    zipf.write(file_path, arcname)
        
        if not os.path.exists(output_path):
            raise RuntimeError(f"Failed to create package: {output_path}")
        
        # Get file size
        file_size_mb = os.path.getsize(output_path) / (1024 * 1024)
        logger.info(f"Successfully packaged Python environment: {output_path} ({file_size_mb:.2f} MB)")
        return output_path

    def build(self) -> str:
        """Full build pipeline: download, extract, install requirements, and package."""
        logger.info(f"Starting Python build for runtime: {self._runtime}")
        
        self.download_python()
        self.extract_python()
        self.install_requirements()
        output_path = self.package()
        
        logger.info(f"Build completed successfully: {output_path}")
        return output_path


def main():
    """Main entry point for the build script."""
    # Allow runtime to be passed as command line argument
    runtime = None
    if len(sys.argv) > 1:
        runtime = sys.argv[1]
    
    builder = PythonBuilder(runtime=runtime)
    builder.build()


if __name__ == "__main__":
    main()
