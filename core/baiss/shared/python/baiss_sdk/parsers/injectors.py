from baiss_sdk.parsers.xml_extractor import XmlExtractor

def stdvname(s):
	return (s.lower().strip().replace("-","").replace("_",""))

class XMLTemplateInjector:

	def __init__(self, text):
		self._text = text

	@property
	def text(self):
		return self._text

	def replace(self, tagname, value, matcher = None):
		"""
		Replace the value of a tag in the text.
		Args:
			tagname: The name of the tag to replace.
			value: The value to replace the tag with.
			matcher: A dictionary of attributes to match the tag with.
		"""
		objects = XmlExtractor(self._text).objects
		oldvals = []
		for obj in objects:
			if stdvname(obj["name"]) != stdvname(tagname):
				continue
			matched = True
			if matcher:
				for key, val in matcher.items():
					if not (key in obj["attributes"]):
						matched = False
						break
					if stdvname(obj["attributes"][key]) != stdvname(val):
						matched = False
						break
			if not matched:
				continue
			oldval = obj["open"]
			if obj["body"]:
				oldval += obj["body"]
			if obj["close"]:
				oldval += obj["close"]
			if not (oldval in self._text):
				raise Exception(f"This error should never happen, so you need to fix XmlExtractor.")
			oldvals.append(oldval)

		for oldval in oldvals:
			self._text = self._text.replace(oldval, str(value))

		return self
