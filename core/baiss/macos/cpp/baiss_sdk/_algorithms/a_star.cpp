//File: a_star.cpp
/*
A* (A-star) algorithm implementation for finding the shortest path in a graph.
    This module provides a class for performing A* search with a focus on
    heuristic-based pathfinding. The A* algorithm combines features of Dijkstra's
    algorithm and greedy best-first search, making it efficient for pathfinding
    in large graphs. It uses a priority queue to explore nodes based on the
    estimated cost to reach the goal, allowing it to find the optimal path
    while minimizing the search space. The implementation is designed to handle
    graphs with varying edge weights and can be easily integrated into various
applications requiring efficient pathfinding capabilities.
*/

#include <vector>
#include <unordered_map>
#include <algorithm>
#include <cmath>
#include <functional>
#include "a_star.h"
// Node structure representing a point in the graph
struct Node {
    int x, y; // Coordinates of the node
    // Additional properties can be added as needed

    // Overloading equality operator for comparison
    bool operator==(const Node& other) const {
        return x == other.x && y == other.y;
    }

    // Function to get neighboring nodes (to be defined based on the graph structure)
    std::vector<Node> getNeighbors() const {
        std::vector<Node> neighbors;
        // Add logic to populate neighbors based on the graph structure
        return neighbors;
    }
};
// Heuristic function type definition
using Heuristic = std::function<float(const Node&, const Node&)>;
// A* algorithm class for pathfinding


class AStar {
private:
    // Private members for the A* algorithm
    std::vector<Node> openSet;  // Nodes to be evaluated
    std::vector<Node> closedSet; // Nodes already evaluated
    std::unordered_map<Node, Node> cameFrom; // Tracks the path
    std::unordered_map<Node, float> gScore; // Cost from start to node
    std::unordered_map<Node, float> fScore; // Estimated cost from start to goal
    Node startNode; // Starting node
    Node goalNode; // Goal node
    Heuristic heuristic; // Heuristic function for estimating costs
    float distance(Node a, Node b) {
        // Calculate the distance between two nodes
        return std::sqrt(std::pow(a.x - b.x, 2) + std::pow(a.y - b.y, 2));
    }
    void reconstructPath(Node current) {
        // Reconstruct the path from start to goal
        std::vector<Node> totalPath;
        while (cameFrom.find(current) != cameFrom.end()) {
            totalPath.push_back(current);
            current = cameFrom[current];
        }
        std::reverse(totalPath.begin(), totalPath.end());
        // Return or store the path as needed
    }
public:
    AStar(Node start, Node goal, Heuristic h) 
        : startNode(start), goalNode(goal), heuristic(h) {
        // Initialize the A* algorithm with start and goal nodes and a heuristic
        gScore[startNode] = 0;
        fScore[startNode] = heuristic(startNode, goalNode);
        openSet.push_back(startNode);
    }

    std::vector<Node> findPath() {
        // Main function to find the path using A* algorithm
        while (!openSet.empty()) {
            Node current = *std::min_element(openSet.begin(), openSet.end(), 
                [this](const Node& a, const Node& b) {
                    return fScore[a] < fScore[b];
                });

            if (current == goalNode) {
                reconstructPath(current);
                return totalPath; // Return the found path
            }

            openSet.erase(std::remove(openSet.begin(), openSet.end(), current), openSet.end());
            closedSet.push_back(current);

            for (const auto& neighbor : current.getNeighbors()) {
                if (std::find(closedSet.begin(), closedSet.end(), neighbor) != closedSet.end()) {
                    continue; // Ignore already evaluated nodes
                }

                float tentativeGScore = gScore[current] + distance(current, neighbor);

                if (std::find(openSet.begin(), openSet.end(), neighbor) == openSet.end()) {
                    openSet.push_back(neighbor); // Add new node to open set
                } else if (tentativeGScore >= gScore[neighbor]) {
                    continue; // This is not a better path
                }

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeGScore;
                fScore[neighbor] = gScore[neighbor] + heuristic(neighbor, goalNode);
            }
        }
        return {}; // Return empty path if no path found
    }
};
