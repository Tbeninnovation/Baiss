
"""
	step 1) backend sent paths [files/folders] /endpoint

	step 2)
		generate raw tree sctrcutre, depends on fileType:
			if windows:
					python
			else:
				C++
	step 3) another functions
		- Check if we have access to google drive, ==> enhance tree structure that generated in step 2
		function per type
		function:
			generate another tree sctrcutre

		input: @/raw_jsons/some-file-name.json

		@/processed_jsons/some-file-name.json
			add this fields:
				"data": [
	                {
	                    "page_number": <page_number>,
	                    "tags": [],
	                    "full_text": "",
	                    "tables": [],
	                    "images": [],
	                    "chunks"    : [],
	                    "full_text" :"bla bla"
	                },
	                ...
	            ]

"""

"""

chunks: [
	{
		"chunk_id": <uuid4>
		 "page_number": 1,
		 "full_text": "5131fd2g31dfg3df23g"
	},
	{
		 "page_number": 1,
		 "full_text": "546fdg"
	}
]

"""
import os
import json
import logging
import baisstools
from typing import List, Dict, Any
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])
from baiss_sdk import get_baiss_project_path
from baisstools       import platform
from baisstools.files import findpath as baistools_findpath
from baiss_sdk.files.file_reader                            import FileReader
from baiss_sdk.files.file_writer                            import FileWriter
from baiss_sdk.files.structures                             import TreeStructure
from baiss_sdk.files.structures.pdf_tree_structure          import PdfTreeStructure
from baiss_sdk.files.structures.csv_tree_structure			import CsvTreeStructure
from baiss_sdk.files.structures.excel_tree_structure		import ExcelTreeStructure
from baiss_sdk.files.structures.text_tree_structure		import TextTreeStructure
from baiss_sdk.files.structures.md_tree_structure           import MdTreeStructure
# from baiss_sdk.files.structures.google_drive_tree_structure import GoogleDriveTreeStructure
# from baiss_sdk.parsers.keywords_extractor                   import KeywordsExtractor
from baiss_sdk.parsers import extract_chunks as extract_chunks_from_plain_txt
from baiss_sdk.db                         import DbProxyClient
from baiss_sdk.files.embeddings import Embeddings
def findpath(*args, **kwargs):
	res=baistools_findpath(*args, *kwargs)
	if not res:
		res = {}
	return res

logger = logging.getLogger(__name__)

# class KeywordsExtractor:

# 	@staticmethod
# 	def from_text(text: str) -> List[str]:
# 		_keywords = []
# 		for c in "\r\t\v\f\n":
# 			text = text.replace(c, " ")
# 		for item in text.split(" "):
# 			_keywords.append(item.strip())
# 		return list(set(_keywords))

class BaseTreeStructure:

	@staticmethod
	def chunkify(structure: Dict[Any, Any], chunk_token_count: int = 1000000) -> Dict[Any, Any]:
		if not structure:
			return structure
		_members = structure.get("files", {})
		if not _members:
			return structure
		for member_key, member_val in list( _members.items() ):
			member_content = FileReader(member_key).content
			member_content = member_content.decode("UTF-8")
			chunks = extract_chunks_from_plain_txt(member_content, chunk_token_count = chunk_token_count)
			_members[member_key]["chunks"] = chunks

		return structure

class TreeStructureScanner:

	@classmethod
	def read_processed_paths(cls) -> List[str]:
		processed_jsons = get_baiss_project_path("local-data", "processed_jsons")
		paths = []
		for processed_jsonfile in findpath(processed_jsons):
			structure = TreeStructure.load(processed_jsonfile)
			if not structure:
				continue
			_files = structure.get("files", {})
			if not _files:
				continue
			for member_key in list( _files.keys() ):
				paths.append(member_key)
		return paths


	@classmethod
	def delete_path_file_or_folder(cls, paths: List[str]) -> bool:
		"""
			Delete files or folders from the tree structure json files in local-data/raw_jsons and local-data/processed_jsons
		"""
		db_client = DbProxyClient()
		db_client.connect()
		db_client.delete_by_paths(paths=paths)
		db_client.disconnect()


	@classmethod
	def delete_path_extension(cls, extensions: List[str]) -> bool:
		"""
			Delete files or folders from the tree structure json files in local-data/raw_jsons and local-data/processed_jsons
		"""
		db_client = DbProxyClient()
		db_client.connect()
		db_client.delete_by_extensions(extensions=extensions)
		db_client.disconnect()


	@classmethod
	def get_chunks_by_paths(cls, paths: List[str]) -> List[Dict[str, Any]]:
		"""
			Get all chunks from the tree structure json files in local-data/raw_jsons and local-data/processed_jsons that match the given paths
		"""
		db_client = DbProxyClient()
		db_client.connect()
		chunks = db_client.get_chunks_by_paths(paths=paths)
		db_client.disconnect()
		return chunks


	@staticmethod
	def _extract_keywords(structure_file: str) -> Dict[Any, Any]:
		"""
			This method should be called after processing json files.
		"""
		structure = TreeStructure.load(structure_file)
		logger.info(f"structure is not none: {structure is not None}")
		if not structure:
			return {}
		_types = structure.get("types", [])
		_type  = structure.get("type", None)
		if _type == "pdf" or _types == ["pdf"]:
			logger.info("Extracting keywords from PDF structure.")
			return TreeStructureScanner._extract_keywords_from_pdf(structure)
		elif _type in ["md", "txt"] or set(_types) <= {"md", "txt"}:
			return TreeStructureScanner._extract_keywords_from_txt(structure)
		return structure

	# @staticmethod
	# def _extract_keywords_from_txt(structure: Dict[Any, Any]) -> Dict[Any, Any]:
	# 	_members = structure.get("files", {})
	# 	if not _members:
	# 		return structure
	# 	for member_key, member_val in list( _members.items() ):
	# 		member_chunks = member_val.get("chunks", [])
	# 		if not member_chunks:
	# 			continue
	# 		full_content: str = ""
	# 		for part_data in member_chunks:
	# 			if not part_data:
	# 				raise ValueError(f"Member {member_key} has empty chunk data, cannot extract keywords.")
	# 			part_content = part_data.get("full_text")
	# 			if not part_content:
	# 				continue
	# 			full_content += part_content + "\n\n"
	# 		extract = KeywordsExtractor(content=full_content)
	# 		keywords = extract.extract_with_nltk(top_n = 10000000)
	# 		if keywords:
	# 			_members[member_key]["keywords"] = keywords
	# 		else:
	# 			_members[member_key]["keywords"] = []
	# 	return structure

	# @staticmethod
	# def _extract_keywords_from_pdf(structure: Dict[Any, Any]) -> Dict[Any, Any]:
	# 	"""
	# 		This method extracts keywords from the full text of the PDF files in the structure.
	# 		It uses the KeywordsExtractor to extract keywords from each file.
	# 	"""
	# 	_types = structure.get("types", [])
	# 	_type  = structure.get("type", None)
	# 	if (_types != ["pdf"]) and (_type != "pdf"):
	# 		raise ValueError("The structure is not a PDF structure.")
	# 	_files = structure.get("files", {})
	# 	if not _files:
	# 		# TO DO: update this part
	# 		logger.warning("No files found in the structure for keyword extraction.")
	# 		return structure

	# 	for member_key, member_val in list( _files.items() ):
	# 		member_data = member_val.get("chunks", [])
	# 		if not member_data:
	# 			continue
	# 		full_content: str = "" # Full PDF content, including all pages
	# 		for page_data in member_data:
	# 			if not page_data:
	# 				continue
	# 			page_full_text = page_data.get("full_text", "")
	# 			if page_full_text:
	# 				full_content += page_full_text + "\n\n"
	# 		extract = KeywordsExtractor(content=full_content)
	# 		keywords = extract.extract_with_nltk(top_n = 10000000)

	# 		if keywords:
	# 			_files[member_key]["keywords"] = keywords
	# 		else:
	# 			_files[member_key]["keywords"] = []
	# 	return structure

	@staticmethod
	def _generate_keywords_for_files():
		"""
			This method should be called after processing json files.
			It will extract keywords from the full text of the files, that extracted in the previous step (TreeStructureScanner._process_json_files).
		"""
		processed_jsons = get_baiss_project_path("local-data", "processed_jsons")
		logger.info(f"Extracting keywords for files in: {processed_jsons}")
		for processed_jsonfile in findpath(processed_jsons):
			# logger.info(f"Processing file for keywords: {processed_jsonfile}")
			structure = TreeStructureScanner._extract_keywords(processed_jsonfile)
			# logger.info(f"structure: {structure}")
			# print(f"structure: {structure}")
			FileWriter(processed_jsonfile).write_json(structure)

	@staticmethod
	def _generate_raw_tree_structure(paths: List[str], extensions: List[str] = None, db_client: DbProxyClient = None):

		if not extensions:
			raise ValueError("Extensions list cannot be empty.")
		if db_client is None:
			raise ValueError("Db client cannot be None.")

		for path in paths:
			# if GoogleDriveTreeStructure.is_valid_path(path):
			# 	dest_path = get_baiss_project_path("local-data", "raw_jsons", "google_drive_tree_structure.json")
			# 	structure = None
			# 	try:
			# 		with open(dest_path, "r") as f:
			# 			structure = json.load(f)
			# 	except:
			# 		pass
			# 	if not structure:
			# 		structure = {}
			# 	GoogleDriveTreeStructure.generate(
			# 		path = path,
			# 		dest = dest_path
			# 	)
			# TODO: Add google drive later on
			if True: # Local File
				if platform.is_windows():
					TreeStructure._update_hash_and_clean_deleted_files(db_client)
					TreeStructure._generate_db(
							path       = path,
							# dest       = dest_path,
							# result     = structure,
							extensions = extensions,
							db_client  = db_client
						)
					
				else:
					# TODO: use C++
					# for ext in extensions:
					# dest_path = get_baiss_project_path("local-data", "raw_jsons", f"{ext}_tree_structure.json")
					# logging.info(f"dest_path: {dest_path} ")
					# structure = None
					# try:
					# 	with open(dest_path, "r") as f:
					# 		structure = json.load(f)
					# except:
					# 	pass
					# if not structure:
					# 	structure = {}
					# logging.info(f"processing path: {path} with extension: {extensions}")
					TreeStructure._update_hash_and_clean_deleted_files(db_client)
					TreeStructure._generate_db(
						path       = path,
						# dest       = dest_path,
						# result     = structure,
						extensions = extensions,
						db_client  = db_client
					)
					

	@staticmethod
	async def _process_json_files(db_client: DbProxyClient = None, extensions: List[str] = None, token = None):
		if db_client is None:
			raise ValueError("Db client cannot be None.")
		if extensions is None:
			raise ValueError("Extensions list cannot be None.")
		raw_data = db_client.retrieve_unprocessed_files(extensions = extensions)
		logger.info(f"Retrieved {raw_data} unprocessed files for extensions: {extensions}")
		for path, id, content_type in raw_data:
			from baiss_agents.app.core.config import global_token
			if global_token == True:
				raise Exception("Global token set to True, operation aborted.")

			if content_type == "google-drive":
				raise NotImplementedError("Google Drive structure processing is not implemented yet.")
			elif content_type == "md":
				await MdTreeStructure.update_md_tree_structure_v2(path, id, content_type, db_client)
			elif content_type == "text/csv" or content_type == "csv":
				await CsvTreeStructure.update_csv_tree_structure_v2(path, id, content_type, db_client)
			elif content_type == "application/pdf" or content_type == "pdf":
				await PdfTreeStructure.update_pdf_tree_structure_v2(path, id, content_type, db_client)
			elif content_type == "txt" or content_type == "text/plain" or content_type == "docx":
				await TextTreeStructure.update_text_tree_structure(path, id, content_type, db_client)
			elif content_type == "xlsx" or content_type == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or content_type == "xls":
				await ExcelTreeStructure.update_excel_tree_structure_v2(path, id, content_type,  db_client)
			else:
				raise ValueError(f"Unknown structure type: {content_type}")
			
	@staticmethod
	async def _process_files_fallback(db_client: DbProxyClient = None):
		if db_client is None:
			raise ValueError("Db client cannot be None.")
		try:
			chunks = db_client.get_all_paths_wo_embeddings()
			for id, content in chunks:
				from baiss_agents.app.core.config import global_token, embedding_url
				if global_token == True:
					raise Exception("Global token set to True, operation aborted.")
				embedding = Embeddings(url= embedding_url)
				embedded_content = await embedding.embed(content)
				if embedded_content is None:
					logger.info(f"Filling in missing embeddings for id: {id}")
					continue
				else:
					db_client.fill_in_missing_embeddings(id=id, embedding=embedded_content)
		except Exception as e:
			raise e

	@staticmethod
	def _generate_keywords_for_folders():
		"""
			This method should be called after _generate_keywords_for_files.
			It will extract keywords for the parent folders of the files, that extracted in the previous step (TreeStructureScanner._generate_keywords_for_files).
		"""
		processed_jsons = get_baiss_project_path("local-data", "processed_jsons")
		for processed_jsonfile in findpath(processed_jsons):
			visited   = {}
			structure = TreeStructure.load(processed_jsonfile)
			_members  = structure.get("files", {})
			if not _members:
				continue
			while True:
				new_visits = 0
				for member_key, member_val in list(_members.items()):
					member_keywords = member_val.get("keywords", [])
					if member_key in visited:
						continue
					new_visits += 1
					visited[member_key] = True
					if not member_keywords:
						continue
					parent_key = member_val.get("parent", None)
					if not parent_key:
						parent_key = member_key[:-len(member_key.split("/")[-1])].rstrip("/")
					parent_val = _members.get(parent_key, None)
					if not parent_val:
						parent_val = {
							"type"    : "folder",
							"children": [],
							"keywords": [],
						}
					parent_keywords = parent_val.get("keywords", [])
					if not parent_keywords:
						parent_keywords = []
					parent_children = parent_val.get("children", [])
					if not parent_children:
						parent_children = []
					if member_keywords:
						parent_keywords.extend(member_keywords)
					parent_val["keywords"] = list(set(parent_keywords))
					_members[parent_key] = parent_val
				if new_visits < 1:
					break
			FileWriter(processed_jsonfile).write_json(structure)

async def generate_full_tree_structures(paths, extensions: List[str] = None):

	try:
		db_client = DbProxyClient()
		db_client.connect()
		db_client.create_db_and_tables()


		TreeStructureScanner._generate_raw_tree_structure(paths = paths, extensions = extensions, db_client = db_client)
		logger.info(f"Generating full tree structures for paths: {paths} with extensions: {extensions}")
		await TreeStructureScanner._process_json_files(db_client=db_client, extensions = extensions)
		logger.info(f"Completed processing json files for paths: {paths} with extensions: {extensions}")
		await TreeStructureScanner._process_files_fallback(db_client=db_client)
		logger.info(f"Completed processing files fallback for paths: {paths} with extensions: {extensions}")

		# TODO ( Abdelmathin) : generate keywords for files
		# TreeStructureScanner._generate_keywords_for_files()
		db_client.disconnect()
	# TODO ( Abdelmathin): generate keywords for folders
	# TreeStructureScanner._generate_keywords_for_folders()
	except Exception as e:
		logger.error(f"Error generating full tree structures: {e}")
		raise e

if __name__ == "__main__":
	pass


