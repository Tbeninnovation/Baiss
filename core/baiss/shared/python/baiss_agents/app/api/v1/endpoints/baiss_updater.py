import os
import sys
import json
import shutil
import logging
import requests
import zipfile
import tarfile
import platform as platform_module
import urllib.request
from typing import List

logger = logging.getLogger(__name__)

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

class FS:
    @staticmethod
    def fast_movefile(src: str, dst: str):
        if not os.path.exists(src):
            raise FileNotFoundError(f"Source file '{src}' does not exist.")
        if not os.path.isfile(src):
            raise ValueError(f"Source '{src}' is not a file.")
        src = os.path.abspath(src)
        dst = os.path.abspath(dst)
        if src == dst:
            return True
        logger.info(f"Moving file from '{src}' to '{dst}'")
        os.replace(src, dst)
        return True

class BaissUpdater:

    def __init__(self,
            version       : str = "latest",
            project_root  : str = None,
            target_runtime: str = f"{platform()}-{archname()}",
        ):
        self._project_root = None
        self._project_root = project_root if project_root else self.get_project_root()
        self._target_runtime = target_runtime
        self._version        = version
        self._dependencies   = None
        self.clear_tmp_dir()

    @property
    def version(self) -> str:
        return self._version

    @property
    def dependencies(self) -> str:
        if self._dependencies:
            return self._dependencies
        dependencies_rsp   = requests.get("https://cdn.baiss.ai/update/dependencies.json")
        if dependencies_rsp.status_code != 200:
            raise RuntimeError("Failed to fetch dependencies.json")
        self._dependencies = dependencies_rsp.json()
        return self._dependencies

    def get_project_root(self) -> str:
        """
        Get the root directory of the project by traversing up the directory tree
        until a directory named 'core' is found.
        """
        if self._project_root:
            logger.info(f"Using provided project root: {self._project_root}")
            os.makedirs(self._project_root, exist_ok=True)
            return self._project_root
        index: int = 0
        path : str = os.path.abspath(__file__)
        paths: List[str] = []
        names: List[str] = ["core", "baiss"]
        while (index < 99999) and path and (path != os.path.dirname(path)):
            if (os.path.exists(os.path.join(path, *names))):
                paths.append(path)
            path = os.path.dirname(path)
            index += 1
        if not paths:
            raise RuntimeError("Project root directory not found.")
        self._project_root = paths[-1]
        logger.info(f"Using provided project root: {self._project_root}")
        return self._project_root

    def clear_tmp_dir(self):
        """
        Clear the temporary directory used for updates.
        """
        tmp_dir: str = self.get_tmp_dir()
        if os.path.exists(tmp_dir):
            shutil.rmtree(tmp_dir)
        os.makedirs(tmp_dir, exist_ok=True)

    def get_tmp_dir(self) -> str:
        """
        Get the temporary directory path within the project structure.
        """
        tmp_dir = os.path.join(self.get_project_root(), ".tmp", "baiss-updater")
        os.makedirs(tmp_dir, exist_ok=True)
        return tmp_dir

    def get_extract_dir(self) -> str:
        """
        Get the extraction directory path within the project structure.
        """
        tmp_dir    : str = self.get_tmp_dir()
        extract_dir: str = os.path.join(tmp_dir, "project")
        os.makedirs(extract_dir, exist_ok=True)
        return extract_dir

    def get_downloads_dir(self) -> str:
        """
        Get the downloads directory path within the project structure.
        """
        tmp_dir: str = self.get_tmp_dir()
        downloads_dir: str = os.path.join(tmp_dir, "downloads")
        os.makedirs(downloads_dir, exist_ok=True)
        return downloads_dir

    def get_dependencies_for_version(self, version: str = "latest") -> dict:
        """
        Get the dependencies for a specific version.
        Args:
            version (str): The version to get dependencies for.
        Returns:
            dict: A dictionary of dependencies for the specified version.
        Raises:
            ValueError: If the version is not found in the dependencies.
        """
        for key, val in self.dependencies.items():
            lkey: str = key.lower()
            if ("baiss" not in lkey) or ("version" not in lkey):
                continue
            if version in lkey:
                return (val)
        key = sorted(self.dependencies.keys())[-1]
        return self.dependencies[key]

    def download_file(self, url: str, output_path: str):
        """
        Download a file from a URL to a specified output path.
        Args:
            url (str): The URL of the file to download.
            output_path (str): The path where the downloaded file will be saved.
        Raises:
            Exception: If the download fails.
        """
        os.makedirs(os.path.dirname(output_path), exist_ok=True)
        try:
            with urllib.request.urlopen(url) as response, open(output_path, "wb") as out_file:
                shutil.copyfileobj(response, out_file)
        except Exception as e:
            import ssl
            import certifi
            context = ssl.create_default_context(cafile=certifi.where())
            with urllib.request.urlopen(url, context=context) as response, open(output_path, "wb") as out_file:
                shutil.copyfileobj(response, out_file)

    def download_all(self):
        """
        Download all necessary components: Baiss UI, Baiss Core, and Python dependencies.
        """
        deps: dict = self.get_dependencies_for_version(self.version)
        downloads_dir: str = self.get_downloads_dir()
        for dep_name, dep_url in deps.get(self._target_runtime, {}).items():
            dep_zip: str = os.path.join(downloads_dir, dep_name + ".zip")
            logger.info(f"Downloading {dep_url}")
            self.download_file(dep_url, dep_zip)

    def extract_all(self):
        downloads_dir   : str = self.get_downloads_dir()
        dependencies_dir: str = self.get_extract_dir()

        for src_basename in os.listdir(downloads_dir):
            lbasename: str = src_basename.lower()
            if not ("baiss" in lbasename):
                continue
            if not ("@" in lbasename):
                continue
            folder_name = src_basename.split("@")[-1].split(".")[0]
            src: str = os.path.join(downloads_dir   , src_basename)
            dst: str = dependencies_dir
            with zipfile.ZipFile(src, 'r') as zip_ref:
                members = zip_ref.infolist()
                total   = len(members)
                for i, member in enumerate(members, start=1):
                    progress: int = i * 100.0 / (total + 0.001)
                    progress: int = int(max(min(progress, 100), 0))
                    os.makedirs(dst, exist_ok=True)
                    zip_ref.extract(member, dst)
                    logger.info(f"Extracting {member.filename} to {dependencies_dir} ({progress}%)")
        self.extract_baiss_ui()
    
    def get_baiss_ui_zip(self) -> str:
        """
        Get the path to the Baiss UI zip file in the downloads directory.
        Returns:
            str: The path to the Baiss UI zip file.
        Raises:
            RuntimeError: If the Baiss UI zip file is not found.
        """
        downloads_dir: str = self.get_downloads_dir()
        baiss_uis: List[str] = []
        for basename in os.listdir(downloads_dir):
            lbasename: str = basename.lower()
            if not ("baiss" in lbasename):
                continue
            if not ("-desktop-" in lbasename):
                continue
            if not lbasename.endswith(".zip"):
                continue
            if not (self._target_runtime in lbasename):
                continue
            if "dependency" in lbasename:
                continue
            if ("-ui-" in lbasename):
                return os.path.join(downloads_dir, basename)
            baiss_uis.append(os.path.join(downloads_dir, basename))
        if not baiss_uis:
            raise RuntimeError("Baiss UI zip not found in downloads.")
        return baiss_uis[0]
    
    def extract_baiss_ui(self):
        """
        Extract the Baiss UI zip file to the project root directory.
        """
        baiss_ui_zip: str = self.get_baiss_ui_zip()
        extract_dir : str = self.get_extract_dir()
        with zipfile.ZipFile(baiss_ui_zip, 'r') as zip_ref:
            members = zip_ref.infolist()
            total   = len(members)
            for i, member in enumerate(members, start=1):
                progress: int = i * 100.0 / (total + 0.001)
                progress: int = int(max(min(progress, 100), 0))
                zip_ref.extract(member, extract_dir)
                logger.info(f"Extracting {member.filename} to {extract_dir} ({progress}%)")
    
    def replace_all(self):
        """
        Replace existing components with the newly extracted ones.
        """
        extract_dir   : str = self.get_extract_dir()
        project_root  : str = self.get_project_root()
        
        # Remove existing python-venv to prevent nesting
        venv_path = os.path.join(project_root, "python-venv")
        if os.path.exists(venv_path):
            shutil.rmtree(venv_path)
            logger.info(f"Removed existing {venv_path} to prevent nesting")
        
        llama_cpp_path = os.path.join(project_root, "llama-cpp")
        if os.path.exists(llama_cpp_path):
            shutil.rmtree(llama_cpp_path)
            logger.info(f"Removed existing {llama_cpp_path} to prevent nesting")
        
        baiss_config_path = os.path.join(project_root, "baiss_config.json")
        if os.path.exists(baiss_config_path):
            os.remove(baiss_config_path)
            logger.info(f"Removed existing {baiss_config_path} to prevent conflicts")
        
        for root, dirs, files in os.walk(extract_dir):
            for file in files:
                src_file: str = os.path.join(root, file)
                rel_path: str = os.path.relpath(src_file, extract_dir)
                dst_file: str = os.path.join(project_root, rel_path)
                os.makedirs(os.path.dirname(dst_file), exist_ok=True)
                if not self.is_ignored_file(dst_file):
                    FS.fast_movefile(src_file, dst_file)
                    logger.info(f"Replaced {dst_file} with {src_file}")
            for dir in dirs:
                src_dir : str = os.path.join(root, dir)
                rel_path: str = os.path.relpath(src_dir, extract_dir)
                dst_dir : str = os.path.join(project_root, rel_path)
                os.makedirs(dst_dir, exist_ok=True)

    def is_ignored_file(self, filepath: str) -> bool:
        """
        Determine if a file should be ignored during the replacement process.
        Args:
            filepath (str): The path to the file.
        """
        basename : str = os.path.basename(filepath).lower()
        extension: str = "." + basename.split(".")[-1]
        if not basename.endswith(extension):
            extension = None
        if extension in [".pyc", ".pyo"]:
            return True
        if basename.startswith("._"):
            return True
        return False
    
    def configure_permissions(self):
        for basename in os.listdir(self.get_project_root()):
            filename: str = os.path.join(self.get_project_root(), basename)
            if os.path.isdir(filename):
                continue
            if basename.lower() in ["baiss.ui"]:
                os.chmod(filename, 0o755)

if __name__ == "__main__":
    updater = BaissUpdater(
        # project_root   = ".tmp_test",
        # target_runtime = f"osx-arm64",
    )
    updater.download_all()
    updater.extract_all()
    updater.replace_all()
    updater.configure_permissions()
