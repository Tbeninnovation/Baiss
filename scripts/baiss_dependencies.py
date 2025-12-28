import os
import sys
import logging
from build import path_join
from build import download_file
from build import get_downloads_dir
from build import get_current_runtime
from build import get_target_runtimes
from build import CURRENT_BAISS_DESKTOP_VERSION

logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

class BaissDependencies:
    BAISS_DEPENDENCIES = {
        f"baiss-version-{CURRENT_BAISS_DESKTOP_VERSION}": {
            # OSX X64:
                "osx-x64": {
                    "baiss-dependency-1.0.0-osx-x64@python-venv.zip": "https://baiss-installation-public.s3.eu-west-3.amazonaws.com/baiss-python-venv-osx-x64.zip",
                    # "baiss-dependency-1.0.0-osx-x64@llama-cpp.zip"  : "https://github.com/ggml-org/llama.cpp/releases/download/b6900/llama-b6900-bin-macos-x64.zip"
                },

            # OSX arm64:
                "osx-arm64": {
                    "baiss-dependency-1.0.0-osx-arm64@python-venv.zip": "https://baiss-installation-public.s3.eu-west-3.amazonaws.com/baiss-python-venv-osx-arm64.zip",
                    # "baiss-dependency-1.0.0-osx-arm64@llama-cpp.zip"  : "https://github.com/ggml-org/llama.cpp/releases/download/b6900/llama-b6900-bin-macos-arm64.zip"
                },

            # Windows x64:
                "win-x64": {
                    "baiss-dependency-1.0.0-win-x64@python-venv.zip": "https://baiss-installation-public.s3.eu-west-3.amazonaws.com/baiss-python-venv-win-x64.zip",
                    # "baiss-dependency-1.0.0-win-x64@llama-cpp.zip"  : "https://github.com/ggml-org/llama.cpp/releases/download/b6900/llama-b6900-bin-win-cuda-12.4-x64.zip"
                },
            # Windows arm64:
                "win-arm64": {
                    # "baiss-1.0.0-win-arm64@python-venv.zip": "https://baiss-installation-public.s3.eu-west-3.amazonaws.com/baiss-python-venv-win-arm64.zip",
                    # "baiss-dependency-1.0.0-win-arm64@llama-cpp.zip"  : "https://github.com/ggml-org/llama.cpp/releases/download/b6900/llama-b6900-bin-win-cpu-arm64.zip"
                },
        }
    }
    @staticmethod
    def download(runtimes = [get_current_runtime()], baiss_version: str = CURRENT_BAISS_DESKTOP_VERSION) -> None:
        for runtime in runtimes:
            if runtime not in BaissDependencies.BAISS_DEPENDENCIES[f"baiss-version-{baiss_version}"]:
                logger.warning(f"Runtime '{runtime}' not found in dependencies for version '{baiss_version}'")
                continue
            for basename, dep_url in BaissDependencies.BAISS_DEPENDENCIES[f"baiss-version-{baiss_version}"][runtime].items():
                dep_zip : str = path_join(get_downloads_dir(), basename)
                if os.path.exists(dep_zip):
                    logger.info(f"Dependency already downloaded: {dep_zip}")
                    continue
                logger.info(f"Downloading dependency from {dep_url} to {dep_zip}")
                download_file(dep_url, dep_zip)
                if not os.path.exists(dep_zip):
                    logger.error(f"Failed to download dependency from {dep_url}")
                    raise Exception(f"Failed to download dependency from {dep_url}")

def main(argv) -> int:
    runtimes = get_target_runtimes(argv)
    BaissDependencies.download(runtimes)
    return 0

if __name__ == "__main__":
    logger.info("Current Runtime: %s", get_current_runtime())
    exit( main(sys.argv) )
