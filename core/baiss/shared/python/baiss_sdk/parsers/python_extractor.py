import os
import sys
import json

class PythonExtractor:
	"""
	Extracts Python functions, classes, and methods from a given Python code string.
	"""

	def __init__(self, text: str):
		self._text      = text.strip()
		self._functions = PythonExtractor.extract_functions(self._text)
	
	@property
	def functions(self):
		return self._functions

	@staticmethod
	def is_function_start(pos: int, text: str, length: int):
		if (pos > 0) and not (text[pos - 1].isspace()):
			return False
		if text[pos:pos + 4] == "async":
			pos += 4
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

def dataexpert_exec(**kwargs):
	sources = kwargs["sources"]
	pycode  = kwargs["code"]

	for function in PythonExtractor(pycode).functions:
		if not ("dataframes" in function["definition"]):
			continue
		print ("- " * 80)
		print (function["body"])

	response = {
		"success"       : False,
		"stdout"        : "",
		"stderr"        : None,
		"error"         : None,
		"execution_time": 0
	}

	return response
