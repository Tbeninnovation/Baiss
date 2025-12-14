import os
import logging
from dotenv import load_dotenv

logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(name)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

os.environ["AWS_REGION"] = "eu-west-3"

# Load environment variables once at module level
load_dotenv()

__cache__ = {
    
}

def get_local_data_dir() -> str:
    """
    Returns the path to the local data directory.
    """
    basename = "local-data"
    if os.environ.get("LOCAL_DATA_DIR"):
        return os.environ["LOCAL_DATA_DIR"]
    dirname = os.path.dirname(os.path.abspath(__file__))
    while dirname and dirname != os.path.dirname(dirname):
        if os.path.exists(os.path.join(dirname, basename)):
            if os.path.isdir(os.path.join(dirname, basename)):
                os.environ["LOCAL_DATA_DIR"] = os.path.join(dirname, basename)
                return os.environ["LOCAL_DATA_DIR"]
        dirname = os.path.dirname(dirname)
    raise FileNotFoundError(
        f"Local data directory not found. Please set the LOCAL_DATA_DIR environment variable or create a '{basename}' directory in the project root."
    )
    return None

def get_system_prompt_dir(default: str = None) -> str:
    SYSTEM_PROMPT_DIR = os.environ.get("SYSTEM_PROMPT_DIR")
    if isinstance(SYSTEM_PROMPT_DIR, str) and os.path.exists(SYSTEM_PROMPT_DIR):
        if os.path.isdir(SYSTEM_PROMPT_DIR):
            return SYSTEM_PROMPT_DIR
    path = os.path.dirname(os.path.abspath(__file__))
    while path and path != os.path.dirname(path):
        sysdir = os.path.join(path, "baiss_agents", "app", "system_prompt")
        if os.path.exists(sysdir):
            logger.info(f"Using system prompt directory: {sysdir}")
            os.environ["SYSTEM_PROMPT_DIR"] = sysdir
            return sysdir
        path = os.path.dirname(path)
    if isinstance(default, str) and os.path.exists(default):
        if os.path.isdir(default):
            logger.info(f"Using default system prompt directory: {default}")
            return default
    raise FileNotFoundError(
        "System prompt directory not found. Please ensure the 'system_prompt' directory exists in the app directory."
    )

def load_system_prompt(name: str) -> str:
    """
    Loads a system prompt from the local data directory.
    """
    paths  = []
    prefix = "baiss_agents/app/system_prompt/"
    sysdir = get_system_prompt_dir()
    if name.startswith(prefix):
        name = "/" + name
    if ("/" + prefix in name):
        name = name.split("/" + prefix)[-1]
    if name.lower().endswith(".md"):
        name = name[:-3]
    name = (name + ".md").strip("/ .")
    for basename in ([""] + list(os.listdir(sysdir))):
        filename = os.path.join(sysdir, basename, name)
        if os.path.exists(filename):
            paths.append(filename)
    name = name.strip("/ .").split("/")[-1].lower()
    for basename in os.listdir(sysdir):
        if basename.strip().lower() == name + ".md":
            paths.append(os.path.join(sysdir, basename))
        if basename.strip().lower() == name:
            try   : basenames = os.listdir(os.path.join(sysdir, basename))
            except: basenames = []
            for basename2 in basenames:
                if basename2.strip().lower() == name + ".md":
                    paths.append(os.path.join(sysdir, basename, basename2))
    for path in paths:
        try    : return open(path).read()
        except : pass
    raise FileNotFoundError(
        f"System prompt '{name}' not found in the system prompt directory."
    )

def parse_bucket_url(path: str):
    if not path:
        raise ValueError("File path cannot be empty.")
    path = str(path).strip()
    if not ("://" in path):
        raise ValueError("Invalid file path. Must contain '://'.")
    if ".." in path:
        raise ValueError("Invalid Path: File path cannot contain '..'")
    schema  = path[:path.index("://") + len("://")].lower()
    path    = path[len(schema):].strip("/")
    if not (path):
        raise ValueError("File path cannot be empty after schema.")
    bucket  = path.split("/")[0]
    if not (bucket):
        raise ValueError("File path must contain a bucket name.")
    path    = path[len(bucket):].strip("/")
    schema  = schema.split("://")[0].strip()

    if not (schema in ["s3", "local-data", "file"]):
        raise ValueError("Invalid schema. Must be 's3', 'local-data', or 'file'.")

    return [
        schema,
        bucket.strip("/"),
        path.strip("/")
    ]
