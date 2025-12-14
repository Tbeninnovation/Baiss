import os
import sys
from venv import logger
import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])
import tiktoken
import io
import json
import time
import logging
import mimetypes
from baiss_sdk import get_baiss_project_path
from baisstools.files import findpath
from typing                               import Dict, List
from baiss_sdk.utils                      import get_local_data_dir, load_system_prompt
from baiss_sdk.files                      import file_reader
from baiss_sdk.files                      import file_writer
from baiss_sdk.dsa.trees                  import FileStructureNode
from baiss_sdk.algorithms.beam            import Beam
from baiss_sdk.algorithms.mcts            import MCTS
import hashlib
from baiss_sdk.db                         import DbProxyClient
# from baiss_sdk.algorithms.bfs             import Bfs
from datetime import datetime

#TO DO : ADD field "file_hash" to the structure that has the hash of the file and can help to identify when the file has changed or duplications
encoder = tiktoken.get_encoding("cl100k_base")
# logger = logging.getLogger(__name__)
# logger.setLevel(logging.INFO)
logger = logging.getLogger(__name__)
class TreeStructure:
    @staticmethod
    def _update_hash_and_clean_deleted_files(
            db_client: DbProxyClient
        ) -> None:
        """
        Update file hashes in the database and clean up records for deleted files.
        """
        all_paths = db_client.get_all_paths()
        for path in all_paths:
            local_path = path
            if path.startswith("file://"):
                local_path = path[len("file://"):]
            if not os.path.exists(local_path):
                logger.info(f"File no longer exists, deleting from database: {path}")
                db_client.delete_by_paths([path])
                continue
            try:
                current_hash = TreeStructure._calculate_file_hash(local_path)
                if current_hash is None:
                    continue

                if db_client.check_if_path_exist_or_changed(path, current_hash):
                    continue

                # Update hash in database
                db_client.execute_query(
                    f"UPDATE BaissDocuments SET hash = '{current_hash}', processed = FALSE WHERE path = '{path}'"
                )
                logger.info(f"Updated hash for {path}")
            except Exception as e:
                logger.warning(f"Failed to update hash for {path}: {e}")


    @staticmethod
    def _generate_db(
            path               : str,
            extensions         : list[str]      = None ,
            excluded_extensions: list[str]      = None ,
            excluded_names     : list[str]      = None ,
            ignore_hidden      : bool           = False,
            ignore_folders     : bool           = True ,
            max_depth          : int            = 5,
            depth              : int            = 0,
            db_client         : DbProxyClient = None,
        ) -> None:
        """
        Internal recursive function for database generation.
        """
        # Stop recursion if max depth is reached
        if depth > max_depth:
            return

        if db_client is None:
            raise ValueError("Db client cannot be None.")
        from baiss_agents.app.core.config import global_token
        if global_token == True:
            raise Exception("Global token set to True, operation aborted.")

        original_path = path
        extensions = TreeStructure.norm_extensions(extensions)
        # logging.info(f"Normalized extensions: {extensions}")
        excluded_extensions = TreeStructure.norm_extensions(excluded_extensions)
        path_scheme = TreeStructure.get_path_scheme(original_path)

        if path_scheme == "s3://":
            raise ValueError("S3 paths are not supported yet.")
        elif path_scheme == "file://":
            if original_path.startswith("file://"):
                path = original_path[len("file://"):]
            else:
                path = original_path

        if not os.path.exists(path):
            raise ValueError(f"Path does not exist: {path}")

        basename = original_path.split("/")[-1]
        logger.info(f"Processing basename: {basename}")
        # Handle single file
        if not os.path.isdir(path):
            if global_token == True:
                raise Exception("Global token set to True, operation aborted.")
            file_ext = TreeStructure.get_file_extension(basename)
            if excluded_names and (basename in excluded_names):
                # logging.info(f"Excluded file: {basename}")
                return
            if ignore_hidden and basename.startswith('.'):
                # logging.info(f"Ignored hidden file: {basename}")
                return
            # TODO: Update this part later
            if extensions and (file_ext not in extensions):
                # logging.info(f"Excluded file by extension: {basename} ({file_ext})")
                return
            if excluded_extensions and (file_ext in excluded_extensions):
                # logging.info(f"Excluded file by excluded extension: {basename} ({file_ext})")
                return


            logger.info(f"Processing file: {original_path} with extension: {file_ext}")

            file_hash = None
            try:
                file_hash = TreeStructure._calculate_file_hash(path)
            except Exception as e:
                logging.warning(f"Could not calculate hash for {path}: {e}")
                file_hash = None

            # Insert into database
            try:
                # Check if file already exists in database
                existing_files = db_client.check_if_path_exist_or_changed(original_path, file_hash)
                logging.info(f"Existing files check for {original_path}: {existing_files}")
                if not existing_files and file_hash is not None:
                    logging.info(f"Inserting new file record for: {original_path}")

                    db_row = {
                        "path": original_path,
                        "hash": file_hash,
                        "depth": depth,
                        "name": basename,
                        "type": "file",
                        "keywords": "[]",  # Empty JSON array as string
                        "content_type": TreeStructure.content_type(path),
                        "last_modified": datetime.now(),
                        "processed": False
                    }
                    db_client.insert_rows("BaissDocuments", [db_row])
                    logging.info(f"Inserted file record for: {original_path}")
                elif file_hash is None:
                    logging.info(f"File already exists in database: {original_path}")

            except Exception as e:
                logging.warning(f"Failed to insert file record for {original_path}: {e}")
            return

        # Handle directory
        children = []
        try:
            for child_name in os.listdir(path):
                child_path = os.path.realpath(os.path.join(path, child_name))
                if global_token == True:
                    raise Exception("Global token set to True, operation aborted.")
                if child_path != os.path.join(path, child_name): # ignore symlinks
                    continue
                if excluded_names and (child_name in excluded_names):
                    continue
                if ignore_hidden and child_name.startswith('.'):
                    continue
                children.append(child_name)

                # Maintain original path scheme for recursive calls
                if path_scheme == "file://":
                    recursive_path = "file://" + child_path
                else:
                    recursive_path = child_path

                TreeStructure._generate_db(
                    path=recursive_path,
                    extensions=extensions,
                    excluded_extensions=excluded_extensions,
                    excluded_names=excluded_names,
                    ignore_hidden=ignore_hidden,
                    ignore_folders=ignore_folders,
                    max_depth=max_depth,
                    depth=depth + 1,
                    db_client=db_client
                )
        except PermissionError as e:
            logger.warning(f"Permission denied accessing directory {path}: {e}")
            return
        except Exception as e:
            logger.error(f"Error processing directory {path}: {e}")
            return

        # Insert folder record if not ignoring folders
        if not ignore_folders:
            try:
                # Check if folder already exists in database
                existing_folders = db_client.execute_query(f"SELECT path FROM BaissDocuments WHERE path = '{original_path}'")
                if not existing_folders:
                    # Calculate directory hash differently (based on contents)
                    dir_hash = None
                    try:
                        dir_hash = hashlib.sha256(str(sorted(children)).encode()).hexdigest()
                    except Exception as e:
                        logger.warning(f"Could not calculate hash for directory {path}: {e}")
                        dir_hash = ""

                    db_row = {
                        "path": original_path,
                        "hash": dir_hash,
                        "depth": depth,
                        "name": basename,
                        "type": "folder",
                        "keywords": "[]",
                        "content_type": None,
                        "last_modified": datetime.datetime.now()
                    }
                    db_client.insert_rows("BaissDocuments", [db_row])
                    logging.info(f"Inserted folder record for: {original_path}")
                else:
                    logging.info(f"Folder already exists in database: {original_path}")
            except Exception as e:
                logger.warning(f"Failed to insert folder record for {original_path}: {e}")
    @staticmethod
    def generate(
            path               : str,
            dest               : str            = None ,
            base_dir           : str            = None ,
            result             : dict           = None ,
            extensions         : list[str]      = None ,
            excluded_extensions: list[str]      = None ,
            excluded_names     : list[str]      = None ,
            ignore_hidden      : bool           = False,
            ignore_folders     : bool           = True ,
            depth              : int            = 0,
            callback           = None,
        ) -> Dict[str, Dict]:
        """
        Recursively find files in a directory and its subdirectories.
        Parameters:
            dirname (str): The directory to search in.
            base_dir (str): The base directory for relative paths.
            result (dict): A dictionary to store the results.
            extensions (list): List of file extensions to include.
            excluded_extensions (list): List of file extensions to exclude.
            excluded_names (list): List of filenames to exclude.
            callback: A callback function to call for each found file.
            ignore_hidden (bool): Whether to ignore hidden files.
        Returns:
            dict: A dictionary with file paths as keys and relative paths as values.
        """
        original_path       = path
        extensions          = TreeStructure.norm_extensions(extensions)
        excluded_extensions = TreeStructure.norm_extensions(excluded_extensions)
        path_scheme         = TreeStructure.get_path_scheme(original_path)

        if path_scheme == "s3://":
            raise ValueError("S3 paths are not supported yet.")
            # return (TreeStructure._generate_s3(path = path, result = result))
        elif path_scheme == "file://":
            if original_path.startswith("file://"):
                path = original_path[len("file://"):]
            else:
                path = original_path

        if not os.path.exists(path):
            raise ValueError(f"Path does not exist: {path}")

        if result is None:
            result = {}
        if not result.get("types", []):
            result["types"] = []
        if not result.get("files", {}):
            result["files"] = {}

        basename = original_path.split("/")[-1]
        if not os.path.isdir(path):
            file_ext = TreeStructure.get_file_extension(basename)
            if excluded_names and (basename in excluded_names):
                return result
            if ignore_hidden and basename.startswith('.'):
                return result
            if extensions and (file_ext not in extensions):
                return result
            if excluded_extensions and (file_ext in excluded_extensions):
                return result
            if file_ext and (file_ext not in result["types"]):
                result["types"].append(file_ext)
            logging.info(f"Adding file: {original_path} with extension: {file_ext}")
            result["files"][ original_path ] = {
                #TODO : ADD field "file_hash" to the structure that has the hash of the file and can help to identify when the file has changed or duplications
                "hash"        : TreeStructure._calculate_file_hash(path),  # TODO: Add file hash calculation

                "depth"       : depth,
                "name"        : basename,
                "type"        : "file",
                "content_type": TreeStructure.content_type(original_path),
                "children"    : None,
                "keywords"    : None,
            }
            logging.info(f"Result: {result}")
            return result

        children = []

        for child_name in os.listdir(path):
            child_path = os.path.realpath(os.path.join(path, child_name))
            if child_path != os.path.join(path, child_name): # ignore symlinks
                continue
            if excluded_names and (child_name in excluded_names):
                continue
            if ignore_hidden and child_name.startswith('.'):
                continue
            children.append(child_name)
            TreeStructure.generate(
                path                = child_path,
                base_dir            = base_dir,
                result              = result,
                extensions          = extensions,
                excluded_extensions = excluded_extensions,
                excluded_names      = excluded_names,
                ignore_hidden       = ignore_hidden,
                ignore_folders      = ignore_folders,
                depth               = depth + 1
            )

        if not ignore_folders:
            result["files"][ original_path ] = {
                "hash"        : TreeStructure._calculate_file_hash(path),  # TODO: Add file hash calculation
                "depth"       : depth,
                "name"        : basename,
                "type"        : "folder",
                "content_type": None,
                "children"    : list(set(children)),
                "keywords"    : None,
            }

        if depth < 1:
            result["type"] = "|".join(result["types"]).strip("|")
            # Always save when we're at the root level (depth < 1) and have a destination
            if dest:
                file_writer.FileWriter(dest).write_json(result)
                logger.info(f"Structure saved to {dest}")
        return result

    @staticmethod
    def _calculate_file_hash(file_path: str) -> str:
        """Calculates the SHA-256 hash of a file."""
        sha256_hash = hashlib.sha256()
        try:
            with open(file_path, "rb") as f:
                # Read and update hash in chunks of 4K
                for byte_block in iter(lambda: f.read(4096), b""):
                    sha256_hash.update(byte_block)
            return sha256_hash.hexdigest()
        except (IOError, FileNotFoundError) as e:
            logger.warning(f"Could not hash file {file_path}: {e}")
            return None

    def get_path_scheme(path: str) -> str:
        """
        Get the scheme of a given path.
        Args:
            path (str): The path to check.
        Returns:
            str: The scheme of the path (e.g., 'file://', 's3://', 'google-drive://', 'local-data://').
        """
        path = path.lower().strip()
        if path.startswith("/"):
            return "file://"
        for c in "#%?&.":
            path = path.split(c)[0]
        if not ("://" in path):
            return "file://"
        scheme = path.split("://")[0].strip()
        return scheme + "://"

    def get_path_without_scheme(path: str) -> str:
        return path

    @staticmethod
    def save(structure: Dict[str, Dict], destination: str) -> None:
        fw = file_writer.FileWriter(destination)
        fw.write(
            json.dumps(
                structure,
                ensure_ascii = False,
                indent       = 4
            )
        )
        return (True)

        destination = destination.strip()
        if destination.startswith("local-data://"):
            fullpath = os.path.join(get_local_data_dir(), destination[len("local-data://"):])
            os.makedirs(os.path.dirname(fullpath), exist_ok = True)
            with io.open(fullpath, "w", encoding="utf-8") as f:
                json.dump(structure, f, ensure_ascii = False, indent = 4)
            return (True)
        else:
            raise ValueError("Destination must start with 'local-data://'")
        return (False)

    @staticmethod
    def download_from_s3(bucket_name, local_dir, root="/"):
        raise

    @staticmethod
    def content_type(filename: str) -> str:
        """
        Guesses the MIME type of a file based on its file extension.

        Args:
            file_path (str): The path to the file.

        Returns:
            str: The guessed MIME type (e.g., 'text/plain'), or
                'application/octet-stream' if the type is unknown.
        """
        content_type, _ = mimetypes.guess_type(filename)
        logger.info(f"Guessed content type for {filename}: {content_type}")
        type = TreeStructure.map_extension(content_type)
        # if content_type is None:
        #     content_type = 'text/plain'
        # if content_type == "text/plain":
        #     extension = TreeStructure.get_file_extension(filename)
        #     if extension == "csv":
        #         return 'text/csv'
        #     elif extension == "json":
        #         return 'application/json'
        return type

    @staticmethod
    def map_extension(extension: str) -> str:
        if extension in ["md", "markdown", "text/markdown"]:
            return "md"
        elif extension in ["txt", "text/plain"]:
            return "txt"
        elif extension in ["csv", "text/csv"]:
            return "csv"
        elif extension in ["json", "jsonl", "ndjson", "application/json"]:
            return "json"
        elif extension in ["pdf", "application/pdf"]:
            return "pdf"
        elif extension in ["docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"]:
            return "docx"
        elif extension in ["ppt", "pptx"]:
            return "pptx"
        elif extension in ["xlsx", "xlsm", "xlsb", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"]:
            return "xlsx"
        elif extension in ["xml"]:
            return "xml"
        elif extension in ["application/vnd.ms-excel", "xls"]:
            return "xls"
        else:
            logger.warning(f"Unknown file extension: -- >>  {extension}")
            return "unknown"

    @staticmethod
    def get_file_extension(filename: str) -> str:
        """
        Get the file extension from a filename.
        Parameters:
            filename (str): The name of the file.
        Returns:
            str: The file extension, or an empty string if no extension is found.
        """
        if not filename:
            return ''
        basename = filename.split(os.sep)[-1].strip()
        while basename.endswith(("\\", "/", ".")):
            basename = basename[:-1].strip()
        if not ('.' in basename):
            return ''
        extension = basename.split(".")[-1]
        return extension

    @staticmethod
    def norm_extension(extension: str):
        extension = extension.lower().strip()
        while extension != extension.strip("."):
            extension = extension.strip(".").strip()
        return extension

    @staticmethod
    def norm_extensions(extensions: list[str]) -> list[str]:
        if not extensions:
            return []
        _extensions = []
        for ext in extensions:
            ext = TreeStructure.norm_extension(ext)
            if ext:
                _extensions.append( ext )
        extensions = list(set(_extensions))
        return extensions

    @staticmethod
    def _generate_s3( # never call this method directly, use TreeStructure.generate() instead
        path  : str,
        result: dict[str, str] = None) -> Dict[str, Dict]:

        raise


    @staticmethod
    def init_metadata(structure: Dict[str, Dict]) -> Dict[str, Dict]:
        for filename, fileinfo in list(structure.items()):
            try:
                fp     = file_reader.FileReader(filename)
                result = fp.dataframe
            except Exception as e:
                logger.info(f"Error reading file {filename}: {e}")
                continue
            metadata = {
                "columns" : {
                    "names" : result.columns.tolist(),
                    "dtypes": {str(k): str(v) for k, v in result.dtypes.to_dict().items()}
                },
                "head": json.loads(result.head(3).to_json(orient='records')),
                "tail": json.loads(result.tail(3).to_json(orient='records'))
            }
            metadata["head_size"] = len(metadata["head"])
            metadata["tail_size"] = len(metadata["tail"])
            structure[filename]["metadata"] = metadata
        return structure

    @staticmethod
    async def update_metadata(structure: Dict[str, Dict]) -> Dict[str, Dict]:
        """# return structure # remove me
        for filename, fileinfo in list(structure.items()):
            metadata_response = await get_metadata(
                MetaDataRequest(
                    filepath = filename
                )
            )
            metadata_result = json.loads(metadata_response.body)["result"]
            if metadata_result:
                for key, val in metadata_result.items():
                    fileinfo[key] = val
                ## structure.clear() # remove me
                structure[filename] = fileinfo
                ## break # remove me
        return structure
        """
    @staticmethod
    def tree_description(structure: Dict[str, Dict], depth: int = 0) -> str:
        raise NotImplementedError("This method is not implemented yet.")

    @staticmethod
    def directories(structure: Dict[str, Dict]) -> Dict[str, List[str]]:
        paths = {}
        for path, config in structure.items():
            children = config.get("children")
            if not children:
                continue
            for child in children:
                child_path = path.strip("/") + "/" + child.strip("/")
                paths[child_path] = {}
            paths[path] = {}
        for path in paths.copy():
            scheme = ""
            if "://" in path:
                scheme = path[:path.index("://") + len("://")]
            parent = scheme
            for item in path[len(scheme):].split("/"):
                parent += item
                paths[parent] = {}
                parent += "/"
        for path in paths.copy():
            basename = path.split("/")[-1]
            path     = path[:-len(basename)]
            if path.endswith("://"):
                continue
            paths[path.strip("/")][basename] = 1
        for path, children in paths.items():
            if path in structure:
                continue
            children = list(children.keys())
            structure[path] = {
                "depth"       : -1,
                "name"        : path.strip("/").split("/")[-1],
                "type"        : "folder",
                "content_type": None,
                "children"    : children if children else None,
                "keywords"    : None
            }
        return structure

    @staticmethod
    def graph(structure: Dict[str, Dict]) -> str:
        raise NotImplementedError(
            "This method is not implemented yet. Use TreeStructure.mermaid_graph() instead."
        )

    @staticmethod
    def roots(structure: Dict[str, Dict]) -> List[str]:
        """ Get the root directories from the structure.
        Args:
            structure (Dict[str, Dict]): The directory structure.
        Returns:
            List[str]: A list of root directories.
        """
        paths = set()
        for path, config in structure.items():
            if not config.get("children"):
                continue
            shceme = ""
            if "://" in path:
                shceme = path[:path.index("://") + len("://")]
            paths.add( shceme + path[len(shceme):].split("/")[0].strip("/") )
        return sorted(paths)

    @staticmethod
    async def element_description(
        element  : Dict,
        path     : str,
        structure: Dict[str, Dict]) -> str:
        """Generate a description for a single directory element.
        Args:
            element (Dict): The directory element.
        Returns:
            str: A description of the directory element.
        """
        structure[path]["general_description"] = "Debug to avoid timeout[" + __file__ + ", line: 384]: description !" # remove me
        structure[path]["column_descriptions"] = "Debug to avoid timeout[" + __file__ + ", line: 385]: description !" # remove me
        return structure # remove me
        metadata = element.get("metadata", {})
        if not isinstance(metadata, dict) or not metadata:
            raise ValueError("Element metadata is not a valid dictionary or is empty.")
        from baiss_agents.app.dbxproxy.dbx_model import DatabricksModel
        dbx_client = DatabricksModel()
        messages = [
            {
                "role"    : "user",
                "content" : [
                    {
                        "text": json.dumps(metadata, indent = 4)
                    }
                ]
            }
        ]
        system_prompt = load_system_prompt("metadata")
        for try_count in range(3):
            response = await dbx_client.send(
                messages      = messages,
                system_prompt = system_prompt,
                model         = "databricks-meta-llama-3-3-70b-instruct",
                temperature   = 0.3,
                max_tokens    = 2000
            )
            if response["error"]:
                logger.error(f"Error in metadata response: {response.get('message', 'Unknown error')}")
                time.sleep(1)
                continue
            try:
                validator = MetadataValidator(
                    text           = response["content"],
                    column_names   = metadata["columns"]["names"],
                    raise_on_error = True
                )
            except Exception as e:
                logger.error(f"Metadata validation failed: {str(e)}")
                continue
            structure[path]["general_description"] = validator.general_description
            structure[path]["column_descriptions"] = validator.column_descriptions
            return structure
        return structure

    @staticmethod
    async def group_description(group: List[Dict]) -> str:
        return "Debug to avoid timeout[" + __file__ + ", line: 431]: description !" # remove me
        body = {
            "files"       : [],
            "directories" : []
        }
        for element in group:
            if ("general_description" in element) and ("column_descriptions" in element):
                body["files"].append(
                    {
                        "general_description": element["general_description"],
                        "column_descriptions": element["column_descriptions"]
                    }
                )
            elif ("general_description" in element):
                body["directories"].append(
                    {
                        "general_description": element["general_description"]
                    }
                )
        if not body["files"] and not body["directories"]:
            raise ValueError("Group is empty or does not contain valid elements.")
        from baiss_agents.app.dbxproxy.dbx_model import DatabricksModel
        dbx_client = DatabricksModel()
        messages = [
            {
                "role"    : "user",
                "content" : [
                    {
                        "text": json.dumps(body, indent = 4)
                    }
                ]
            }
        ]
        system_prompt = load_system_prompt("tree_description")
        for try_count in range(3):
            response = await dbx_client.send(
                messages      = messages,
                system_prompt = system_prompt,
                model         = "databricks-meta-llama-3-3-70b-instruct",
                temperature   = 0.3,
                max_tokens    = 2000
            )
            if response["error"]:
                logger.error(f"Error[group_description]: {response.get('message', 'Unknown error')}")
                time.sleep(1)
                continue
            try:
                validator = DescriptionValidator(
                    text           = response["content"],
                    raise_on_error = True
                )
            except Exception as e:
                logger.error(f"Metadata validation failed: {str(e)}")
                continue
            return validator.general_description
        return None

    @staticmethod
    def read_structure(structure_path: str) -> Dict[str, Dict]:
        try:
            # Try UTF-8 first
            with open(structure_path, "r", encoding="utf-8") as f:
                structure = json.load(f)
            return structure
        except UnicodeDecodeError:
            # Fallback to latin-1 if UTF-8 fails
            try:
                with open(structure_path, "r", encoding="latin-1") as f:
                    structure = json.load(f)
                return structure
            except Exception as e:
                # If both fail, try cp1252 (Windows default) with error handling
                with open(structure_path, "r", encoding="cp1252", errors="ignore") as f:
                    structure = json.load(f)
                return structure


    @staticmethod
    async def tree_description(structure: Dict[str, Dict]) -> Dict[str, Dict]:
        """Generate a general description for each directory in the structure.
        Algorithm:
            - Name: BFS from bottom to top
            - Description: This algorithm traverses the directory structure in a breadth-first manner,
            - Time Complexity: O(n * depth), where n is the number of directories and depth is the maximum depth of the directory tree.
            - Steps:
                1. Start with the root directories.
                2. For each root, check if it has a general description.
                3. If it does, mark it as visited.
                4. If it doesn't, check its children.
                5. If a parent has enough descripted children, generate a general description for it.
                6. Repeat until all roots are visited.
        Args:
            Structure (Dict[str, Dict]): The directory structure.
        Returns:
            Dict[str, Dict]: The updated structure with general descriptions.
        """
        CHUNK_SIZE    = 5
        roots         = TreeStructure.roots(structure)
        visited_roots = {}
        structure     = TreeStructure.directories(structure)
        while len(visited_roots) < len(roots):
            for path, config in list(structure.items()):
                if (path in roots) and config.get("general_description"):
                    visited_roots[path] = True
                if config.get("general_description"):
                    continue
                if not config.get("children"):
                    await TreeStructure.element_description(config, path, structure)
                    continue
                descripted_children = []
                for child in config["children"]:
                    child_path = path.strip("/") + "/" + child.strip("/")
                    if not structure.get(child_path):
                        continue
                    child_config = structure[child_path]
                    if not child_config.get("general_description"):
                        continue
                    descripted_children.append( child_config )
                if ( len(descripted_children) >= min(CHUNK_SIZE, len(config["children"])) ):
                    structure[path]["general_description"] = await TreeStructure.group_description(descripted_children)
        return structure

    @staticmethod
    def toCanonicalTree(structure: Dict) -> FileStructureNode:
        """
                        [root-1] [root-2] [root-3]...
                         /          |         \
                       [child-1] [child-2] [child-3] ...

        """
        root_data = {"name": "root", "type": "directory", "keywords": []}
        root = FileStructureNode("root", root_data)
        nodes = {"root": root}

        # Sort files by path depth to ensure parents are created before children
        sorted_files = sorted(structure["files"].items(), key=lambda x: x[0].count(os.path.sep))

        for path, data in sorted_files:
            parts = path.strip(os.path.sep).split(os.path.sep)
            current_path_str = ""
            parent = root
            for i, part in enumerate(parts):
                parent_path_str = current_path_str
                current_path_str = os.path.join(current_path_str, part)

                is_file_node = (i == len(parts) - 1)

                if current_path_str not in nodes:
                    node_data = data if is_file_node else {"name": part, "type": "directory", "keywords": []}
                    new_node = FileStructureNode(current_path_str, node_data)
                    parent._children.append(new_node)
                    new_node._parent = parent
                    nodes[current_path_str] = new_node

                parent = nodes[current_path_str]

        return root

    @staticmethod
    def search(structure: Dict[str, Dict], question: str, algorithm: str = "Beam") -> dict[str, any]:
        __algorithms__ = {
            "beam"  : Beam,
            "mcts"  : MCTS,
            # "bfs" : Bfs
        }
        algoObject = __algorithms__.get(algorithm.lower())
        if not algoObject:
            raise ValueError(f"Algorithm '{algorithm}' is not supported. Supported algorithms: {', '.join(__algorithms__.keys())}")
        if not isinstance(structure, dict):
            raise ValueError("Structure must be a dictionary.")
        if not structure:
            raise ValueError("Structure cannot be empty.")
        if not question:
            raise ValueError("Question cannot be empty.")
        tree = TreeStructure.toCanonicalTree(structure)
        if not tree:
            raise ValueError("Tree structure cannot be empty.")
        return algoObject(tree).search(question)

    @staticmethod
    def searchv2(question: str, algorithm: str = "Beam"):
        """
        Search for the best matching file and chunk based on a question.
        Args:
            question (str): The question to search for.
            algorithm (str): The algorithm to use for searching (default is "Beam").
        Returns:
            dict: A dictionary containing the best matching file and chunk.
        """

        from sklearn.metrics.pairwise import cosine_similarity
        from sklearn.feature_extraction.text import TfidfVectorizer

        jsonsdir   = get_baiss_project_path("local-data", "processed_jsons")
        file_paths = []
        best_file  = None
        best_chunk = None
        best_score = -1


        for jsonfile in findpath(jsonsdir):
            structure = TreeStructure.load(jsonfile)
            if not structure:
                continue

            for path, data in structure["files"].items():
                chunks = data.get('chunks', [])
                if not chunks:
                    continue

                chunk_text = " ".join(chunk['full_text'] for chunk in chunks )

                keywords = data.get('keywords', [])
                if not keywords:
                    continue

                keywords_text = " ".join( keywords )
                weighted_text = (keywords_text + " ") * 3 + chunk_text  # keywords weighted 3x

                # TF-IDF vectorization on chunks + question
                vectorizer = TfidfVectorizer()
                tfidf_matrix = vectorizer.fit_transform([weighted_text] + [question])
                similarities = cosine_similarity(tfidf_matrix[-1], tfidf_matrix[:-1]).flatten()

                # Find the best chunk in this file
                max_index = similarities.argmax()
                max_score = similarities[max_index]

                if max_score > best_score:
                    best_score = max_score
                    best_file = path
                    best_chunk = chunks[max_index]
        return {
            "file"  : best_file,
            "chunk" : best_chunk,
        }

    @staticmethod
    def load(jsonpath: str) -> dict:
        """
        Load a JSON structure from a file.
        Args:
            jsonpath (str): The path to the JSON file.
        Returns:
            dict: The loaded JSON structure.
        """
        try:
            with open(jsonpath, "r") as f:
                structure = json.load(f)
        except:
            structure = {}
        if not structure:
            return {}
        if not ("type" in structure):
            return {}
        if not ("files" in structure):
            return {}
        return structure




if __name__ == "__main__":
    pass
