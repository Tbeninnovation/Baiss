import os
import sys
import json
from typing import List, Dict, Any

class PythonExtractor:
	"""
	Extracts Python functions, classes, and methods from a given Python code string.
	"""

	def __init__(self, text: str):
		self._text      = text.strip()
		self._functions = PythonExtractor.extract_functions(self._text)
		self._sections  = PythonExtractor.extract_sections(self._text)

	@property
	def functions(self):
		return self._functions

	@property
	def sections(self):
		return self._sections

	@staticmethod
	def is_function_start(pos: int, text: str, length: int):
		if (pos > 0) and not (text[pos - 1].isspace()):
			return False
		if text[pos:pos + 5] == "async":
			pos += 5
		elif text[pos:pos + 3] == "def":
			pos += 3
		else:
			return False
		if (pos >= length) or not text[pos].isspace():
			return False
		return True

	@staticmethod
	def skip_string(pos: int, text: str, length: int):
		lim = None
		for key in ['"""', "'''", "'", '"']:
			if text[pos:pos + len(key)] == key:
				lim  = key
				pos += len(key)
				break
		if lim == None:
			return pos
		while pos < length:
			if text[pos: pos + len(lim)] == lim:
				return pos + len(lim)
			if text[pos] == '\\':
				pos += 1
			pos += 1
		return pos

	@staticmethod
	def skip_comment(pos: int, text: str, length: int):
		if (pos >= length) or (text[pos] != '#'):
			return pos
		while (pos < length) and (text[pos] != '\n'):
			pos += 1
		return pos

	@staticmethod
	def skip_definition(pos: int, text: str, length: int):
		ntuple = 0
		nlist  = 0
		ndict  = 0
		while pos < length:
			epos = PythonExtractor.skip_comment(pos, text, length)
			if pos != epos:
				pos = epos
				continue
			epos = PythonExtractor.skip_string(pos, text, length)
			if pos != epos:
				pos = epos
				continue
			if text[pos] == '{':
				ndict += 1
			elif text[pos] == '}':
				ndict -= 1
			elif text[pos] == '[':
				nlist += 1
			elif text[pos] == ']':
				nlist -= 1
			elif text[pos] == '(':
				ntuple += 1
			elif text[pos] == ')':
				ntuple -= 1
			elif text[pos] == ':':
				if (ntuple == 0) and (nlist == 0) and (ndict == 0):
					pos += 1
					break
			pos += 1
		return pos

	@staticmethod
	def extract_function(pos: int, text: str, length: int):
		if not PythonExtractor.is_function_start(pos, text, length):
			return {}
		function = {}
		while (pos < length) and text[pos].isspace():
			pos += 1
		indentation = 0
		while (pos > 0) and (text[pos - 1] in [" ", "\t"]):
			indentation += 1
			pos -= 1
		function["start"] = pos
		function["name"]  = text[pos:].split("(")[0].strip()
		for c in "\r\t\v\f\n ":
			function["name"] = function["name"].split(c)[-1]
		head_start = pos
		pos  = PythonExtractor.skip_definition(pos, text, length)
		head = text[head_start:pos].strip()
		while (pos < length) and (text[pos].isspace()) and (text[pos] != '\n'):
			pos += 1
		if (pos >= length) or (text[pos] != '\n'):
			return {}
		while pos < length:
			epos = PythonExtractor.skip_comment(pos, text, length)
			if pos != epos:
				pos = epos
				continue
			epos = PythonExtractor.skip_string(pos, text, length)
			if pos != epos:
				pos = epos
				continue
			if text[pos] == '\n':
				pos += 1
				subindent = 0
				while (pos < length) and text[pos].isspace() and (text[pos] != '\n'):
					subindent += 1
					pos += 1
				if pos >= length:
					break
				if text[pos] == '\n':
					continue
				if subindent <= indentation:
					break
				continue
			pos += 1
		function["end"]        = pos
		function["body"]       = text[function["start"]:function["end"]]
		function["definition"] = head.strip()
		return function

	@staticmethod
	def extract_sections(text: str):
		"""
		Extracts Python code sections from the provided text.
		Only sections enclosed in triple backticks (```) and labeled as Python are extracted.
		"""
		sections : List[str] = []
		start_pos: int	     = text.find("```")
		while start_pos >= 0:
			end_pos   = text.find("```", start_pos + 3)
			if end_pos < 0:
				break
			section   = text[start_pos + 3:end_pos].strip()
			start_pos = end_pos + 3
			first_word = section.split()[0].strip().lower()
			if not (first_word in ["python", "py"]):
				continue
			section = section[len(first_word):].strip()
			sections.append(section)
		return sections

	@staticmethod
	def extract_functions(text: str):
		"""
		Extracts functions from the provided Python code string.
		"""
		pos       = 0
		length    = len(text)
		functions = []
		while pos < length:
			function = PythonExtractor.extract_function(pos, text, length)
			if function:
				functions.append(function)
				pos = function["end"]
				continue
			pos += 1
		return functions
