# File: @/baiss.desktop/desktop-app/baiss_sdk/dsa/trees.py

import random

class TreeNode:
    def __init__(self, value):
        self._value    = value
        self._children = []

    def add_child(self, child_node):
        self._children.append(child_node)

    def __repr__(self):
        return f"TreeNode({self.value})"
    
    @property
    def value(self):
        return self._value
    
    @property
    def children(self):
        return self._children

class FileStructureNode:
    """
    Represents a node in a file structure tree.
    Each node contains information about a file or directory, including its identifier,
    metadata, and keywords associated with it.
    """

    def __init__(self, identifier: str, data: dict):
        self._coefficient = 0.0
        self._parent      = None
        self._children    = []
        self._identifier  = identifier
        self._data        = data
        self._keywords    = data.get("keywords", [])

    def __repr__(self):
        return f"<FileStructureNode id={self._identifier}/>"
    
    def clone(self):
        """ Create a deep copy of the node. """
        return FileStructureNode(self._identifier, self._data.copy())

    @property
    def identifier(self):
        return self._identifier

    @property
    def data(self):
        return self._data
    
    @property
    def keywords(self):
        self._keywords

    def children(self):
        return self._children
    
    @staticmethod
    def randtree(depth = 5, max_children = 20):
        """ Generate a random tree structure. """
        if depth < 1:
            return None
        keywords = [
            "ui", "ux", "interface", "frontend", "design", "interaction", "layout", "theme", "color",
            "style", "component", "widget", "element", "button", "form", "input", "output", "icon",
            "svg", "image", "animation", "transition", "responsive", "adaptive", "breakpoint", "screen",
            "device", "dpi", "window", "dialog", "modal", "menu", "toolbar", "navigation", "panel",
            "card", "grid", "list", "tree", "accordion", "table", "row", "column", "datagrid", "viewer",
            "preview", "render", "canvas", "drawing", "paint", "drag", "drop", "hover", "focus",
            "click", "touch", "gesture", "shortcut", "keyboard", "accessibility", "a11y", "dark",
            "light", "theme", "palette", "font", "typography", "spacing", "margin", "padding", "border",
            "shadow", "depth", "elevation", "iconography", "material", "bootstrap", "tailwind", "qt",
            "tkinter", "pyside", "kivy", "html", "css", "js", "react", "vue", "svelte", "framework",
            "event", "binding", "state", "reactive", "observer", "data", "store", "flux", "redux",
            "emit", "listen", "route", "transition", "page", "view", "screen", "form_validation"
        ]
        node = FileStructureNode(
            f"Node {random.randint(1, 100)}",
            {
                "name"         : f"Node {random.randint(1, 100)}",
                "type"         : "directory" if random.choice([True, False]) else "file",
                "depth"        : depth,
                "size"         : random.randint(1, 10000),
                "last_modified": "2025-07-30 21:40:19",
                "content_type" : "text/plain",
                "children"     : None,
                "keywords"     : random.sample(keywords, k = random.randint(1, 10))
            }
        )
        for _ in range(random.randint(1, max_children)):
            child = FileStructureNode.randtree(depth - 1, max_children)
            if child:
                child._parent = node  # Set parent for the child node
                node._children.append(child)
        return node
    
    def similarity(self, other, reference_keywords: list[str]) -> float:
        """
        Compare this node with another node based on the must similarity of their keywords.
        Using: cosine similarity or Jaccard index.
        """
        lhs = set(self._keywords)
        rhs = set(other._keywords)

        reference_set = set(reference_keywords)
        l_inter = len(lhs.intersection(reference_set))
        l_union = len(lhs.union(reference_set))
        r_inter = len(rhs.intersection(reference_set))
        r_union = len(rhs.union(reference_set))

        if l_union == 0 or r_union == 0:
            return 0.0
        # Calculate Jaccard similarity for both sides
        # Jaccard similarity = |A ∩ B| / |A ∪ B|
        lhs_similarity = l_inter / l_union
        rhs_similarity = r_inter / r_union
        return (lhs_similarity + rhs_similarity) / 2.0
