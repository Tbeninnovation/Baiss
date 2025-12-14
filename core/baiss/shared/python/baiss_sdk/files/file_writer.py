import os
import json

import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])

import logging
from dotenv    import load_dotenv

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

class FileWriter:
    """Class to write files to S3 or local storage."""
    def __init__(self, file_path: str):

        self._schema        = "file://"
        self._relative_path = file_path
        if ("://" in file_path):
            self._schema        = file_path.split("://")[0] + "://"
            self._relative_path = file_path[len(self._schema):]

    @staticmethod
    def convert_sets_to_lists(obj):
        if isinstance(obj, set):
            return list(obj)
        elif isinstance(obj, dict):
            return {key: FileWriter.convert_sets_to_lists(value) for key, value in obj.items()}
        elif isinstance(obj, list):
            return [FileWriter.convert_sets_to_lists(item) for item in obj]
        else:
            return obj
    def write_json(self, jobject: dict):
        serializable_jobject = FileWriter.convert_sets_to_lists(jobject)

        content = json.dumps(serializable_jobject, indent = 4)
        return self.write(content)

    def write(self, content: str):
        """
        Write content to a file in S3 or local storage.
        :param content: Content to write to the file.
        """
        if self._schema == "file://":
            os.makedirs(os.path.dirname(self._relative_path), exist_ok = True)
            try:
                with open(self._relative_path, 'w') as f:
                    f.write(content)
                return True
            except Exception as e:
                return False
            return False
        else:
            raise NotImplementedError(f"Schema '{self._schema}' is not supported yet.")
