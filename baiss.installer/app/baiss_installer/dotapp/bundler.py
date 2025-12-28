#!/usr/bin/env python3
import os
import shutil
import subprocess

INFO_PLIST_TEMPLATE = """<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" 
 "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>{{bundle_executable}}</string>
    <key>CFBundleIdentifier</key>
    <string>com.tbeninnovation.baiss</string>
    <key>CFBundleName</key>
    <string>{{title}}</string>
    <key>CFBundleVersion</key>
    <string>1.0</string>
    <key>CFBundleIconFile</key>
    <string>{{bundle_icon_file}}</string>
    <key>CFBundleGetInfoString</key>
    <string>{{description}}</string>
</dict>
</plist>
"""

class DotAppBundler:

    def __init__(self,
            title            : str,
            entrypoint       : str,
            path             : str = None,
            description      : str = None,
            version          : str = "1.0.0",
            bundle_identifier: str = None,
            bundle_executable: str = None,
            bundle_icon_file : str = "appicon"
        ) -> None:
        if bundle_executable is None:
            bundle_executable = title
        self._bundle_executable = bundle_executable
        self.set_title(title)
        self.set_entrypoint(entrypoint)
        if description is None:
            description = f"{self.title} {version}"
        if path is None:
            path = f"{self.title}.app"
        self.set_path(path)
        self.__config__: dict[str, str] = {
            "title"            : self.title,
            "entrypoint"       : self.entrypoint,
            "description"      : description,
            "bundle_executable": bundle_executable,
            "bundle_icon_file" : bundle_icon_file
        }

    @property
    def title(self) -> str:
        return self._title

    @property
    def entrypoint(self) -> str:
        return self._entrypoint
    
    @property
    def path(self) -> str:
        return self._path

    def set_title(self, title: str) -> str:
        self._title = title
        return self.title

    def set_entrypoint(self, entrypoint: str) -> str:
        self._entrypoint = entrypoint
        return self.entrypoint
    
    def set_path(self, path: str) -> str:
        self._path = path

    def init_bundle(self):
        """
        Initializes the .app bundle structure by creating the necessary directories.
        If the bundle directory already exists, it will not be recreated.
        """
        macos_dir    : str = os.path.join(self.path, "Contents", "MacOS")
        resources_dir: str = os.path.join(self.path, "Contents", "Resources")
        os.makedirs(macos_dir    , exist_ok=True)
        os.makedirs(resources_dir, exist_ok=True)

    def fini_bundle(self):
        """
        Finalizes the .app bundle by copying the entry point and creating a symlink
        for the executable if it does not already exist.
        """
        resources_dir  : str = os.path.join(self.path, "Contents", "Resources")
        entrypoint_path: str = os.path.join(resources_dir, self.entrypoint)
        if not os.path.exists(entrypoint_path):
            shutil.copy(self.entrypoint, entrypoint_path)
        if os.path.exists( os.path.join(self.path, "Contents", "MacOS", self._bundle_executable) ):
            return # already done
        os.symlink(
            os.path.join("..", "Resources", self.entrypoint),
            os.path.join(self.path, "Contents", "MacOS", self._bundle_executable)
        )
        self.create_info_plist()
        # self.sign_bundle()

    def create_info_plist(self):
        info_plist_path    = os.path.join(self.path, "Contents", "Info.plist")
        info_plist_content = INFO_PLIST_TEMPLATE
        for key, val in self.__config__.items():
            info_plist_content = info_plist_content.replace(f"{{{{{key}}}}}", val)
        with open(info_plist_path, "w") as f:
            f.write(info_plist_content)
        return info_plist_path
    
    def is_valid_bundle_dir(self, path: str = None) -> bool:
        if (not path) or (not isinstance(path, str)):
            return False
        if not path.endswith(".app"):
            return False
        if not os.path.isdir(path):
            return False
        return True

    def sign_bundle(self, path: str = None):
        if path is None:
            path = self.path
        if not self.is_valid_bundle_dir(path):
            raise ValueError("Can only sign .app bundles")
        subprocess.run([
            "codesign",
            "--force",
            "--sign", "-",
            "--deep",
            self.path
        ], check=True)
        return self.validate_bundle(path = self.path)

    def validate_bundle(self, path: str = None):
        """
        Validates the bundle by checking if the necessary directories and files exist.
        Raises FileNotFoundError if any required component is missing.
        """
        if path is None:
            path = self.path
        if not self.is_valid_bundle_dir(path):
            raise ValueError("Can only sign .app bundles")
        result = subprocess.run([
            "codesign"   ,
            "--verify"   ,
            "--deep"     ,
            "--strict"   ,
            "--verbose=2",
            path
        ], capture_output=True, text=True)
        if result.returncode != 0:
            raise ValueError(f"Bundle is not properly signed: {result.stderr}")
        required_paths = [
            os.path.join(path, "Contents", "MacOS"),
            os.path.join(path, "Contents", "Resources"),
            os.path.join(path, "Contents", "Info.plist"),
            os.path.join(path, "Contents", "MacOS", self._bundle_executable)
        ]
        for req_path in required_paths:
            if not os.path.exists(req_path):
                raise FileNotFoundError(f"Required path missing: {req_path}")
        return True
