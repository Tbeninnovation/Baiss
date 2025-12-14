
def uniformize_argname(argname: str) -> str:
    """
    Uniformizes the argument name by converting it to lowercase and replacing spaces with underscores.
    """
    newname = ""
    for c in argname.lower():
        if c.isspace():
            continue
        if c in ['-', '_', '.']:
            continue
        newname += c
    return newname

class ArgList:
    def __init__(self, data = None):
        self._data = []
        if data == None:
            data = []
        for item in list(data):
            if not isinstance(item, str):
                raise TypeError("All items in ArgList must be strings.")
            self._data.append(uniformize_argname(item))

    def compare(self, other):
        if len(self._data) != len(other._data):
            return False
        for i in range(len(self._data)):
            if uniformize_argname(self._data[i]) != uniformize_argname(other._data[i]):
                return False
        return True
