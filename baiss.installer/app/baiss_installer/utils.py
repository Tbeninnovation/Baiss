import os
import sys
import shutil
import subprocess
from typing import List

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

def project_root() -> str:
    """
    Finds the root directory of the project by searching for specific subdirectories.
    Returns:
        str: The absolute path to the project root.
    Raises:
        FileNotFoundError: If the project root cannot be found.
    """
    base_paths: List[str] = [os.path.dirname(os.path.abspath(__file__))]
    try:
        base_paths.append(sys._MEIPASS)
    except AttributeError:
        pass
    base_paths.append(os.getcwd())
    for path in base_paths:
        test_index: int = 0
        while (path != os.path.dirname(path)) and (test_index < 100):
            path: str = os.path.dirname(path)
            for subp in ["assets/img", "assets/icons"]:
                if os.path.exists(path_join(path, subp)):
                    return path
            test_index += 1
    raise FileNotFoundError("Project root not found.")

def project_path(*items) -> str:
    """
    Resolves the absolute path to a resource within the project directory.
    Args:
        *items: Path components relative to the project root.
    Returns:
        str: The absolute path to the resource.
    Raises:
        FileNotFoundError: If the project root or resource path cannot be found.
    """
    respath: str = path_join(project_root(), *items)
    if not os.path.exists(respath):
        raise FileNotFoundError(f"Resource path '{items}' not found.")
    return respath
