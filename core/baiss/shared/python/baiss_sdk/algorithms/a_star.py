"""
    A* (A-star) algorithm implementation for finding the shortest path in a graph.
    This module provides a class for performing A* search with a focus on
    heuristic-based pathfinding. The A* algorithm combines features of Dijkstra's
    algorithm and greedy best-first search, making it efficient for pathfinding
    in large graphs. It uses a priority queue to explore nodes based on the
    estimated cost to reach the goal, allowing it to find the optimal path
    while minimizing the search space. The implementation is designed to handle
    graphs with varying edge weights and can be easily integrated into various
    applications requiring efficient pathfinding capabilities.
"""

import heapq

class AStar:
    def __init__(self, graph, heuristic):
        """
        Initialize the A* algorithm with a given graph and heuristic function.
        
        Args:
            graph (dict): A dictionary representing the graph where keys are node identifiers
                          and values are lists of tuples (neighbor, weight).
            heuristic (callable): A function that estimates the cost from a node to the goal.
        """
        self.graph = graph
        self.heuristic = heuristic

    def find_shortest_path(self, start, goal):
        """
        Find the shortest path from start to goal using A* algorithm.
        
        Args:
            start: The starting node identifier.
            goal: The goal node identifier.
        
        Returns:
            A tuple containing the shortest path as a list of nodes and the total cost.
        """
        # Priority queue for maintaining the nodes to explore
        open_set = []
        heapq.heappush(open_set, (0 + self.heuristic(start, goal), start))
        
        # Dictionary to store the cost from start to each node
        g_costs = {node: float('inf') for node in self.graph}
        g_costs[start] = 0
        
        # Dictionary to track the previous node in the path
        came_from = {node: None for node in self.graph}

        while open_set:
            current_f_cost, current_node = heapq.heappop(open_set)

            if current_node == goal:
                return self.reconstruct_path(came_from, current_node), g_costs[goal]

            for neighbor, weight in self.graph[current_node]:
                tentative_g_cost = g_costs[current_node] + weight
                
                if tentative_g_cost < g_costs[neighbor]:
                    came_from[neighbor] = current_node
                    g_costs[neighbor] = tentative_g_cost
                    f_cost = tentative_g_cost + self.heuristic(neighbor, goal)
                    heapq.heappush(open_set, (f_cost, neighbor))

        return [], float('inf')  # No path found

    def reconstruct_path(self, came_from, current):
        """
        Reconstruct the path from start to goal based on the came_from mapping.
        
        Args:
            came_from (dict): Mapping of nodes to their predecessors in the path.
            current: The current node to trace back from.
        
        Returns:
            A list representing the path from start to goal.
        """
        path = []
        while current is not None:
            path.append(current)
            current = came_from[current]
        return path[::-1]
