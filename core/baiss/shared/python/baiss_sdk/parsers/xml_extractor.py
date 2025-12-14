import re
import json
from typing import List, Dict, Any, Optional

TAG_TERMINATORS  = ['<', '/', '>']
NAME_TERMINATORS = ['<', '/', '>', '=', '"', "'", ' ', '\t', '\n', '\r', "\v", "\f"]

def skip(pos: int, text: str, length: int, charset: str = "\r\n\v\f\t ") -> int:
    """
    Skip characters in the text until a character not in 'charset' is found.
    Args:
        pos: Starting position in the text.
        text: The text to process.
        length: Length of the text.
        charset: Characters to skip.
    Returns:
        The new position after skipping characters.
    """
    while (pos < length) and (text[pos] in charset):
        pos += 1
    return pos

class XmlExtractor:
    """
    A class to extract XML-like tags and their content from text.
    Specifically designed to parse <agent_call> and <answer> tags from Leai responses.
    """

    def __init__(self, text: str = ""):
        self._text        = text.strip()
        self._objects     = XmlExtractor.extract_objects(text)
        self._agent_calls = []
        self._answer      = None
        if self._text:
            self._agent_calls = XmlExtractor.extract_agent_calls(self._text)
            self._answer      = XmlExtractor.extract_answer(self._text)

    @property
    def objects(self):
        """
        Returns the list of extracted JSON objects.
        """
        return self._objects  

    @property
    def agent_calls(self) -> List[Dict[str, Any]]:
        """Returns the list of extracted agent call objects."""
        return self._agent_calls

    @property
    def answer(self) -> Optional[str]:
        """Returns the extracted answer content."""
        return self._answer

    def has_agent_calls(self) -> bool:
        """Check if the text contains any agent calls."""
        return len(self._agent_calls) > 0

    @staticmethod
    def extract_answer(text: str) -> Optional[str]:
        """
        Extract content from <answer> tags.
        Returns the text content inside the first <answer></answer> block.
        """
        text = str(text).strip()
        if not text:
            return None
        
        pattern = r'<answer>(.*?)</answer>'
        match = re.search(pattern, text, re.DOTALL | re.IGNORECASE)
        
        if match:
            return match.group(1).strip()
        return None

    @staticmethod
    def extract_answers(text: str) -> List[str]:
        """
        Extract all content from <answer> tags.
        Returns a list of text content from all <answer></answer> blocks.
        """
        text = str(text).strip()
        if not text:
            return []
        
        pattern = r'<answer>(.*?)</answer>'
        matches = re.findall(pattern, text, re.DOTALL | re.IGNORECASE)
        
        return [match.strip() for match in matches if match.strip()]

    @staticmethod
    def extract_agent_calls(text: str) -> List[Dict[str, Any]]:
        """
        Extract and parse <agent_call> blocks from text.
        Each block should contain JSON with agent, dataset, and question.
        
        Returns a list of dictionaries with parsed agent call parameters.
        """
        text = str(text).strip()
        if not text:
            return []
        
        # Find all <agent_call> blocks
        pattern = r'<agent_call>(.*?)</agent_call>'
        matches = re.findall(pattern, text, re.DOTALL | re.IGNORECASE)
        
        agent_calls = []
        for match_content in matches:
            try:
                # Clean up the JSON content
                json_content = match_content.strip()
                
                # Parse the JSON
                call_data = json.loads(json_content)
                
                # Validate required fields
                if all(key in call_data for key in ['agent', 'dataset', 'question']):
                    agent_calls.append({
                        'agent': call_data['agent'],
                        'dataset': call_data['dataset'],
                        'question': call_data['question']
                    })
                else:
                    print(f"Warning: Invalid agent call missing required fields: {call_data}")
                    
            except json.JSONDecodeError as e:
                print(f"Warning: Failed to parse agent call JSON: {match_content[:100]}... Error: {e}")
                continue
            except Exception as e:
                print(f"Warning: Error processing agent call: {e}")
                continue
        
        return agent_calls

    @staticmethod
    def create_agent_answer_block(content: str, dataset: str = None, error: str = None) -> str:
        """
        Create an <agent_answer> block with the provided content.
        
        Args:
            content: The answer content from data expert
            dataset: Optional dataset identifier
            error: Optional error message if data extraction failed
        """
        if error:
            answer_content = f"Error extracting data from {dataset}: {error}"
        else:
            answer_content = content
        
        return f"<agent_answer>\n{answer_content}\n</agent_answer>"

    @staticmethod
    def get_open_tag(pos, text, length):
        """
        Get the opening tag at the given position in the text.
        Args:
            pos: Starting position in the text.
            text: The text to process.
            length: Length of the text.
        Returns:
            The opening tag or None if not found.
        """
        if (pos >= length) or (text[pos] != "<"):
            return None
        start_tag = pos
        pos = skip(pos + 1, text, length)
        if pos >= length:
            return None
        if (text[pos:pos + 3] == "!--"): # comment
            return None
        while pos < length:
            if text[pos] in ['"', "'"]:
                quote_char = text[pos]
                pos += 1
                while (pos < length) and (text[pos] != quote_char):
                    if text[pos] == "\\":
                        pos += 1
                    pos += 1
                if pos >= length:
                    return None
                if text[pos] == quote_char:
                    pos += 1
                continue
            elif text[pos] == ">":
                return (text[start_tag:pos + 1])
            pos += 1
        return None

    @staticmethod
    def extract_body(pos, text, length):
        """
        Extract the body of an XML-like tag at the given position in the text.
        Args:
            pos: Starting position in the text.
            text: The text to process.
            length: Length of the text.
        Returns:
            The body of the tag or None if not found.
        """
        if (pos >= length) or (text[pos] != '<'):
            return None
        tagopen = XmlExtractor.get_open_tag(pos, text, length)
        tagname = XmlExtractor.tag_name(tagopen)
        if not tagopen or not tagname:
            return None
        pos += len(tagopen)
        body_start = pos
        while pos < length:
            comntlen = XmlExtractor.comment_length(pos, text, length)
            if comntlen:
                pos += comntlen
                continue
            if text[pos] == "<":
                tmp = skip(pos + 1, text, length)
                if (tmp + 1 < length) and (text[tmp] == "/"):
                    tmp = skip(tmp + 1, text, length)
                    close_name = ""
                    while (tmp < length) and not(text[tmp] in NAME_TERMINATORS):
                        close_name += text[tmp]
                        tmp += 1
                    tmp = skip(tmp, text, length)
                    if (tmp < length) and (text[tmp] == ">"):
                        if tagname.strip().lower() == close_name.strip().lower():
                            body_end = pos
                            return text[body_start:body_end]
                else:
                    subopen = XmlExtractor.get_open_tag(pos, text, length)
                    subname = XmlExtractor.tag_name(subopen)
                    if subopen and subname:
                        subbody = XmlExtractor.extract_body(pos, text, length)
                        if subbody != None:
                            pos += len(subopen) + len(subbody)
                            while (pos < length) and (text[pos] != '>'):
                                pos += 1
                            assert (text[pos] == '>')
                            pos += 1
                            continue
            pos += 1
        return None

    @staticmethod
    def get_opentags(text: str) -> List[str]:
        """
        Get all opening tags in the text.
        Args:
            text: The text to process.
        Returns:
            A list of opening tags.
        """
        pos    = 0
        length = len(text)
        result = []
        while pos < length:
            tagopen = XmlExtractor.get_open_tag(pos, text, length)
            if tagopen:
                result.append(tagopen)
            pos += 1
        return result

    @staticmethod
    def get_comments(text: str) -> List[str]:
        """
        Get all comments in the text.
        Args:
            text: The text to process.
        Returns:
            A list of comments.
        """
        pos    = 0
        length = len(text)
        result = []
        while pos < length:
            comntlen = XmlExtractor.comment_length(pos, text, length)
            if comntlen:
                result.append(text[pos:pos + comntlen])
                pos += comntlen
                continue
            pos += 1
        return result

    @staticmethod
    def comment_length(pos: int, text: str, length: int) -> int:
        """
        Get the length of a comment at the given position in the text.
        Args:
            pos: Starting position in the text.
            text: The text to process.
            length: Length of the text.
        Returns:
            The length of the comment or 0 if not found.
        """
        start = pos
        if (pos >= length) or (text[pos] != '<'):
            return 0
        pos = skip(pos + 1, text, length)
        if (pos >= length) or (text[pos:pos + 3] != '!--'):
            return 0
        while pos < length:
            if (text[pos:pos + 2] == '--'):
                pos = skip(pos + 2, text, length)
                if (pos < length) and (text[pos] == '>'):
                    return pos - start + 1
                continue
            pos += 1
        return 0

    @staticmethod
    def extract_objects(text):
        """
        Extract all XML-like objects from the text.
        Args:
            text: The text to process.
        Returns:
            A list of XML-like objects.
        """
        pos     = 0
        length  = len(text)
        objects = []
        while pos < length:
            comntlen = XmlExtractor.comment_length(pos, text, length)
            if comntlen:
                pos += comntlen
                continue
            tagopen    = XmlExtractor.get_open_tag(pos, text, length)
            tagname    = XmlExtractor.tag_name(tagopen)
            attributes = XmlExtractor.tag_attributes(tagopen)
            if not tagname:
                pos += 1
                continue
            tagbody = XmlExtractor.extract_body(pos, text, length)
            closetg = ""
            if tagbody != None:
                tmp = pos + len(tagopen) + len(tagbody)
                while tmp < length:
                    closetg += text[tmp]
                    if text[tmp] == '>':
                        break
                    tmp += 1
                if (tmp >= length) or (text[tmp] != '>'):
                    raise Exception("This error should never happen, so you need to fix XmlExtractor.")
            objects.append(
                {
                    "open"      : tagopen,
                    "name"      : tagname,
                    "body"      : tagbody,
                    "close"     : closetg,
                    "attributes": attributes
                }
            )
            pos += 1
        return objects
    
    @staticmethod
    def tag_name(text: str) -> Optional[str]:
        """
        Extract the tag name from an XML-like tag.
        Args:
            text: The tag string to extract the name from.
        Returns:
            The tag name or None if not found.
        """
        text = str(text).strip()
        if not text.startswith("<"):
            return None
        length = len(text)
        pos    = skip(1, text, length)
        if (pos >= length) or (text[pos] in TAG_TERMINATORS):
            return None
        if (text[pos:pos + 3] == "!--"): # comment
            return None
        # skip tag name
        tag_name = ""
        while (pos < length) and not(text[pos] in NAME_TERMINATORS):
            tag_name += text[pos]
            pos += 1
        return tag_name

    @staticmethod
    def tag_attributes(text: str) -> Dict[str, str]:
        """
        Extract attributes from an XML-like tag.
        Args:
            text: The tag string to extract attributes from.
        Returns:
            A dictionary of attributes and their values.
        """
        text = str(text).strip()
        if not text.startswith("<"):
            return {}
        pos    = 1 # Start after the opening '<'
        length = len(text)
        # skip spaces before tag name
        while (pos < length) and text[pos].isspace():
            pos += 1
        if (pos >= length) or (text[pos] in TAG_TERMINATORS):
            return {}
        # skip tag name
        while (pos < length) and not(text[pos] in NAME_TERMINATORS):
            pos += 1
        if (pos >= length) or not(text[pos].isspace()):
            return {}
        attributes = {}
        while True:
            while (pos < length) and text[pos].isspace():
                pos += 1
            if (pos >= length) or (text[pos] in TAG_TERMINATORS):
                break
            name = ""
            while pos < length:
                if text[pos] in NAME_TERMINATORS:
                    break
                name += text[pos]
                pos  += 1
            if not name:
                break
            attributes[name] = None
            while (pos < length) and text[pos].isspace():
                pos += 1
            if (pos >= length) or (text[pos] in TAG_TERMINATORS):
                break
            if text[pos] != '=':
                continue
            pos += 1 # skip '='
            # skip spaces after '='
            while (pos < length) and text[pos].isspace():
                pos += 1
            if (pos >= length) or (text[pos] in TAG_TERMINATORS):
                break
            value  = ""
            if text[pos] in ["'", '"']:
                quote_char = text[pos]
                pos += 1
                while (pos < length) and (text[pos] != quote_char):
                    if text[pos] == "\\":
                        pos += 1
                    if (pos < length):
                        value += text[pos]
                    pos += 1
                if (pos >= length) or (text[pos] != quote_char):
                    return None
                pos += 1
            else:
                while pos < length:
                    if text[pos].isspace():
                        break
                    if text[pos] in TAG_TERMINATORS:
                        break
                    value += text[pos]
                    pos   += 1
            attributes[name] = value
        return attributes

    @staticmethod
    def remove_comments(text: str):
        """
        Remove all comments from the text.
        Args:
            text: The text to process.
        Returns:
            The text with comments removed.
        """
        pos    = 0
        length = len(text)
        result = ""
        while pos < length:
            comntlen = XmlExtractor.comment_length(pos, text, length)
            if comntlen:
                pos += comntlen
                continue
            result += text[pos]
            pos += 1
        return (result)
