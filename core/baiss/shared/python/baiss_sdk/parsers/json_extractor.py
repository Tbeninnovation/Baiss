import ast
import json

class JsonExtractor:
    """
    A class to extract JSON objects from a given text.
    This class is used to parse the input text and extract valid JSON objects.
    """

    def __init__(self, text):
        self._text    = text
        self._objects = self.extract_objects(text)

    @property
    def objects(self):
        """
        Returns the list of extracted JSON objects.
        """
        return self._objects    

    @staticmethod
    def parse_json_or_dict(text):
        """
        Parses a string and returns a JSON object or Pyhton dictionary.
        If the string is not a valid JSON, it raises an exception.
        """
        text = str(text).strip()
        if not text:
            raise ValueError("Input text cannot be empty.")
        try:
            return json.loads(text)
        except:
            try:
                return ast.literal_eval(text)
            except (ValueError, SyntaxError):
                raise ValueError("Invalid JSON or dictionary format.")
        raise ValueError("Invalid JSON or dictionary format.")

    @staticmethod
    def extract_objects(text):
        """
        Extracts JSON objects from the provided text.
        Returns a list of dictionaries representing the JSON objects.
        """
        text = str(text).strip()
        if not text:
            return []
        i = 0
        objects = []
        while i < len(text):
            if (text[i] != '{') and (text[i] != '['):
                i += 1
                continue
            _open  = text[i]
            _close = {"{" : "}", "[": "]"}[_open]
            j = i
            json_result = None
            while j < len(text):
                if text[j] == _close:
                    try:
                        json_result = JsonExtractor.parse_json_or_dict(text[i : j + 1])
                        break
                    except Exception as e:
                        json_result = None
                j += 1
            if json_result == None:
                i += 1
                continue
            i = j + 1
            objects.append(json_result)
        return objects
