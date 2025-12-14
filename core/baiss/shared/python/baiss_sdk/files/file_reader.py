import logging
from dotenv    import load_dotenv
import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])

# Load environment variables once at module level
load_dotenv()

logger = logging.getLogger(__name__)
logger.setLevel(logging.INFO)

# Also create a console handler to ensure logs show up
formatter       = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
console_handler = logging.StreamHandler()
console_handler.setLevel(logging.INFO)
console_handler.setFormatter(formatter)
logger.addHandler(console_handler)

class FileReader:
    """Class to read files from S3 or local storage."""
    def __init__(self, file_path: str):

        self._schema        = "file://"
        self._relative_path = file_path
        if ("://" in file_path):
            self._schema        = file_path.split("://")[0] + "://"
            self._relative_path = file_path[len(self._schema):]

    @property
    def content(self):
        """Get the content of the file."""
        if self._schema == "file://":
            with open(self._relative_path, 'rb') as file:
                return file.read()
        else:
            raise NotImplementedError(f"Schema '{self._schema}' is not supported yet.")
        
    @staticmethod
    def update_file_path(file_path: str) -> str:
        if file_path.startswith("file://"):
            return file_path[len("file://"):]
        return file_path
