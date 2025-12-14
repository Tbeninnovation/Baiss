/*

*/

#pragma GCC optimize("O3", "inline")
#pragma GCC option("arch=native", "tune=native", "no-zero-upper")
#pragma GCC target("rdrnd", "popcnt", "avx", "bmi2", "avx2")

#include <emmintrin.h>
#include <stddef.h>
#include <immintrin.h>
#include <map>
#include <set>
#include <cassert>
#include <climits>
#include <iostream>
#include <string>
#include <vector>
#include <algorithm>
#include <cstring>
#include <chrono>
#include <cmath>
#include <sstream>
#include <array>

#define SQUARE(x) ((x) * (x))
#define ARRAY_SIZE(arr) ((sizeof(arr) / sizeof(*(arr))))
#define INF 1e18
#define TIME_LIMIT (45) // milliseconds for search

namespace baiss {
    namespace algorithms {

        // ============= FAST RANDOM NUMBER GENERATOR =============

        static uint32_t hash(uint32_t a) {
            a = (a ^ 61) ^ (a >> 16);
            a = a + (a << 3);
            a = a ^ (a >> 4);
            a = a * 0x27d4eb2d;
            a = a ^ (a >> 15);
            return a;
        }

        static uint32_t random(void) {
            static uint32_t g_seed;
            g_seed = ::hash(g_seed);
            return g_seed;
        }

        namespace mcts {

            namespace v1 {
                /*

                static const int max_node_count = 500000;

                // ============= MCTS NODE STRUCTURE =============
                typedef struct node_s node_t;

                struct Child {
                    node_t *node;
                    double score;
                    double square_score;
                    int visits;
                };

                struct node_s {
                    node_t *parent;
                    std::vector<Child> childs;
                    int last_selected_move;
                    int total_visit_count;
                    int root_id;
                    int player;
                } node_t;

                #define max_node_count 500000
                Node nodes[max_node_count];
                int nodes_count;
                
                Node *new_node(int root_id, int player) {
                    Node *node = &nodes[nodes_count++];
                    node->root_id = root_id;
                    node->player = player;
                    node->parent = 0;
                    node->childs.clear();
                    node->last_selected_move = -1;
                    node->total_visit_count = 0;
                    return node;
                }

                // ============= MCTS SEARCH ALGORITHM =============
                template<typename GameState, typename Action>
                vector<Action> search(const GameState &initial_state, 
                                        int max_depth = 2,
                                        double exploration_constant = 1.2) {
                    
                    nodes_count = 0;
                    vector<Node*> roots;
                    
                    // Initialize root nodes (customize based on your game)
                    // This example assumes you have multiple players/entities
                    for (int player = 0; player < 2; player++) {
                        roots.push_back(new_node(player, player));
                    }
                    
                    auto chrono_begin = chrono::steady_clock::now();
                    int iterations = 0;
                    
                    while (true) {
                        // Time limit check
                        if (chrono::duration_cast<chrono::milliseconds>(
                            chrono::steady_clock::now() - chrono_begin).count() > TIME_LIMIT) {
                            break;
                        }
                        
                        // Node limit check
                        if (nodes_count > max_node_count - 10000) {
                            break;
                        }
                        
                        // MCTS Simulation
                        GameState state = initial_state;
                        vector<Node*> current_nodes = roots;
                        
                        // Selection and Expansion phase
                        while (state.turn < initial_state.turn + max_depth && !state.isTerminal()) {
                            
                            // Get available actions for current state
                            auto available_actions = state.getAvailableActions();
                            
                            for (int i = 0; i < current_nodes.size(); i++) {
                                Node *node = current_nodes[i];
                                node->total_visit_count++;
                                
                                double best_ucb = -INF;
                                int best_action = -1;
                                bool found_new_action = false;
                                
                                double parent_log = log(node->total_visit_count);
                                
                                // UCB1 Selection
                                for (int j = 0; j < available_actions[i].size(); j++) {
                                    auto &action = available_actions[i][j];
                                    
                                    // Check if action already explored
                                    bool action_exists = false;
                                    int child_idx = -1;
                                    
                                    for (int k = 0; k < node->childs.size(); k++) {
                                        if (node->childs[k].action == action) {
                                            action_exists = true;
                                            child_idx = k;
                                            break;
                                        }
                                    }
                                    
                                    double ucb_value;
                                    
                                    if (action_exists && node->childs[child_idx].visits > 0) {
                                        // Calculate UCB1 with variance
                                        auto &child = node->childs[child_idx];
                                        double exploit = child.score / child.visits;
                                        double sample_variance = max(0.0, 
                                            child.square_score / child.visits - exploit * exploit);
                                        double visits_fraction = parent_log / child.visits;
                                        double variance_term = sample_variance + exploration_constant * sqrt(visits_fraction);
                                        
                                        ucb_value = exploit + sqrt(min(1.0, variance_term) * visits_fraction);
                                    } else {
                                        // New action - expand
                                        if (!found_new_action) {
                                            Child new_child;
                                            new_child.action = action;
                                            new_child.visits = 0;
                                            new_child.score = 0;
                                            new_child.square_score = 0;
                                            new_child.node = nullptr;
                                            
                                            node->childs.push_back(new_child);
                                            child_idx = node->childs.size() - 1;
                                            found_new_action = true;
                                        }
                                        ucb_value = INF; // Prioritize unexplored actions
                                    }
                                    
                                    if (ucb_value > best_ucb) {
                                        best_ucb = ucb_value;
                                        best_action = child_idx;
                                    }
                                }
                                
                                if (best_action != -1) {
                                    node->last_selected_move = best_action;
                                    
                                    // Create child node if doesn't exist
                                    if (!node->childs[best_action].node) {
                                        node->childs[best_action].node = new_node(
                                            node->root_id, node->player);
                                        node->childs[best_action].node->parent = node;
                                    }
                                    
                                    current_nodes[i] = node->childs[best_action].node;
                                    
                                    // Apply action to state
                                    state.applyAction(i, node->childs[best_action].action);
                                }
                            }
                            
                            state.nextTurn();
                        }
                        
                        // Evaluation phase
                        auto utilities = state.evaluate(roots);
                        
                        // Backpropagation phase
                        for (int i = 0; i < current_nodes.size(); i++) {
                            Node *node = current_nodes[i];
                            double value = utilities[i];
                            
                            node = node->parent;
                            while (node) {
                                auto &child = node->childs[node->last_selected_move];
                                child.visits++;
                                child.score += value;
                                child.square_score += value * value;
                                node = node->parent;
                            }
                        }
                        
                        iterations++;
                    }
                    
                    // Select best actions from root
                    vector<Action> result;
                    
                    for (auto root : roots) {
                        if (root->player == 0) { // Assuming player 0 is the main player
                            int best_child = -1;
                            double best_score = -INF;
                            
                            for (int i = 0; i < root->childs.size(); i++) {
                                if (root->childs[i].visits > 0) {
                                    double avg_score = root->childs[i].score / root->childs[i].visits;
                                    if (avg_score > best_score) {
                                        best_score = avg_score;
                                        best_child = i;
                                    }
                                }
                            }
                            
                            if (best_child != -1) {
                                result.push_back(root->childs[best_child].action);
                            }
                        }
                    }
                    
                    return result;
                }
            */
        }
    }
}

int main(void) {
    return 0;
}
