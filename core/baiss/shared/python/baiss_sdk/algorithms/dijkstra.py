"""
    Dijkstra's algorithm implementation for finding the shortest path in a graph.
    This module provides a class for performing Dijkstra's algorithm, which is
    commonly used in routing and navigation systems. The algorithm efficiently
    finds the shortest path from a source node to all other nodes in a weighted
    graph. It uses a priority queue to explore the nearest unvisited node and
    updates the shortest path distances iteratively. The implementation is
    designed to handle graphs with non-negative weights and can be easily
    integrated into various applications requiring pathfinding capabilities.
"""

import heapq

class Dijkstra:
    def __init__(self, graph):
        """
        Initialize the Dijkstra's algorithm with a given graph.
        
        Args:
            graph (dict): A dictionary representing the graph where keys are node identifiers
                          and values are lists of tuples (neighbor, weight).
        """
        self.graph = graph

    def find_shortest_path(self, start, goal):
        """
        Find the shortest path from start to goal using Dijkstra's algorithm.
        
        Args:
            start: The starting node identifier.
            goal: The goal node identifier.
        
        Returns:
            A tuple containing the shortest path as a list of nodes and the total cost.
        """
        # Priority queue for maintaining the nodes to explore
        open_set = []
        heapq.heappush(open_set, (0, start))
        
        # Dictionary to store the cost from start to each node
        g_costs = {node: float('inf') for node in self.graph}
        g_costs[start] = 0
        
        # Dictionary to track the previous node in the path
        came_from = {node: None for node in self.graph}

        while open_set:
            current_cost, current_node = heapq.heappop(open_set)

            if current_node == goal:
                return self.reconstruct_path(came_from, current_node), g_costs[goal]

            for neighbor, weight in self.graph[current_node]:
                tentative_g_cost = g_costs[current_node] + weight
                
                if tentative_g_cost < g_costs[neighbor]:
                    g_costs[neighbor] = tentative_g_cost
                    came_from[neighbor] = current_node
                    heapq.heappush(open_set, (tentative_g_cost, neighbor))

        return [], float('inf')  # Return empty path and infinite cost if no path found

    def reconstruct_path(self, came_from, current_node):
        """
        Reconstruct the path from start to goal using the came_from mapping.
        
        Args:
            came_from (dict): A dictionary mapping each node to its predecessor.
            current_node: The current node to backtrack from.
        
        Returns:
            A list representing the path from start to goal.
        """
        path = []
        while current_node is not None:
            path.append(current_node)
            current_node = came_from[current_node]
        return path[::-1]  # Reverse the path to get it from start to goal