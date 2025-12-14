# import baisstools
# baisstools.repoinit()
import os
import re
import sys

import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])

from typing              import List, Dict, Tuple, Set
from dataclasses         import dataclass
from baiss_sdk.dsa.trees import FileStructureNode

class KeywordsExtractor:
    """
    Extracts keywords from a given text string.
    In a real implementation, this would use more sophisticated NLP techniques.
    """
    def __init__(self, text: str):
        self._text = text

    def extract_keywords(self) -> List[str]:
        """
        Extracts keywords by lowercasing the text and splitting it into unique words.
        """
        return list(set(self._text.lower().split()))

# A data class to hold the state of each item in the beam during the search.
@dataclass
class BeamSearchState:
    """Represents a state in the BEAM search, containing a node and its score."""
    node: FileStructureNode
    score: float

class Beam:
    """
    Implements a BEAM search algorithm to find relevant files within a FileStructureNode tree.

    The search algorithm intelligently explores the file tree to find nodes that best match
    the keywords extracted from a user's question.
    """
    def __init__(self, tree: FileStructureNode, beam_width: int = 3, max_depth: int = 10):
        """
        Initializes the Beam search algorithm.

        Args:
            tree (FileStructureNode): The root node of the file structure to search.
            beam_width (int): The number of promising paths to keep at each step of the search.
            max_depth (int): The maximum depth to search into the file tree.
        """
        self._tree       = tree
        self._beam_width = beam_width
        self._max_depth  = max_depth

    def _calculate_node_score(self, node: FileStructureNode, query_keywords: Set[str]) -> float:
        """
        Calculates a relevance score for a node based on the query keywords.

        The score is the Jaccard similarity between the query keywords and the node's
        keywords, which include its metadata keywords and words from its file path.

        Args:
            node (FileStructureNode): The node to score.
            query_keywords (Set[str]): The set of keywords from the user's query.

        Returns:
            float: A relevance score between 0.0 and 1.0.
        """
        # Combine the node's metadata keywords with words from its path for a richer keyword set.
        node_kw_set = set(node.keywords)
        path_words = set(re.split(r'[/._-]', node.identifier.lower()))
        node_kw_set.update(path_words)

        if not query_keywords and not node_kw_set:
            return 1.0
        if not query_keywords or not node_kw_set:
            return 0.0

        intersection = len(node_kw_set.intersection(query_keywords))
        union = len(node_kw_set.union(query_keywords))

        return intersection / union if union > 0 else 0.0

    def search(self, question: str, top_k: int = 5) -> List[FileStructureNode]:
        """
        Performs the BEAM search on the tree to find the most relevant nodes.

        Args:
            question (str): The user's natural language question.
            top_k (int): The number of best-matching nodes to return.

        Returns:
            List[FileStructureNode]: A list of the most relevant FileStructureNode objects,
                                     sorted by relevance score.
        """
        extractor = KeywordsExtractor(question)
        query_keywords = set(extractor.extract_keywords())

        if not query_keywords:
            return []

        # This dictionary stores the best intrinsic score for every relevant node found.
        best_nodes_found: Dict[str, Tuple[FileStructureNode, float]] = {}

        # Initialize the beam with the first level of the tree.
        initial_beam: List[BeamSearchState] = []
        for child_node in self._tree.children():
            if (not child_node) or (not child_node.keywords):
                continue
            score = self._calculate_node_score(child_node, query_keywords)
            if score > 0:
                state = BeamSearchState(node=child_node, score=score)
                initial_beam.append(state)
                best_nodes_found[child_node.identifier] = (child_node, score)
        
        # Sort and prune the initial beam.
        initial_beam.sort(key=lambda x: x.score, reverse=True)
        beam = initial_beam[:self._beam_width]

        # Explore the tree level by level.
        for depth in range(1, self._max_depth):
            if not beam:
                break

            candidates: List[BeamSearchState] = []
            for state in beam:
                for child_node in state.node.children():
                    child_score = self._calculate_node_score(child_node, query_keywords)
                    if child_score > 0:
                        # The path score accumulates to reward consistently relevant paths.
                        new_path_score = state.score + child_score
                        candidates.append(BeamSearchState(node=child_node, score=new_path_score))
                        
                        # Update the best score for this individual node if this is higher.
                        if child_node.identifier not in best_nodes_found or child_score > best_nodes_found[child_node.identifier][1]:
                            best_nodes_found[child_node.identifier] = (child_node, child_score)

            if not candidates:
                break
            
            # Sort all candidates and select the top `beam_width` to form the next beam.
            candidates.sort(key=lambda x: x.score, reverse=True)
            beam = candidates[:self._beam_width]

        # Sort the collected unique nodes by their individual relevance score.
        sorted_nodes = sorted(best_nodes_found.values(), key=lambda x: x[1], reverse=True)

        # Return the top_k nodes.
        return [node for node, score in sorted_nodes[:top_k]]
