/*
    MCTS Algorithm Template
*/

#include <iostream>
#include <vector>
#include <cmath>
#include <ctime>
#include <cstdlib>
#include <limits>
#include <algorithm>

typedef struct state_s {
    int id;
} state_t;

typedef struct action_s {
    int id;
} action_t;

typedef double score_t;

std::vector<action_t> getBestActions(const state_t& istate, size_t max_actions = 10) {
    // Ghir example
    std::vector<action_t> actions;
    for (size_t i = 0; i < max_actions; ++i) {
        action_t a;
        a.id = i;
        actions.push_back(a);
    }
    return actions;
}

state_t simulate(const state_t& state, const std::vector<action_t>& actions) {
    // Ghir example
    state_t next;
    next.id = state.id + 1; // Dummy transition
    return next;
}

score_t evaluate(const state_t& state) {
    // Ghir example
    return (score_t)(state.id); // Dummy reward
}

namespace mcts {

    const int    iterations  = 1000;
    const double exploration = std::sqrt(2.0);

    typedef struct node_s {
        state_t  state;
        action_t action;
        score_t  total_score;
        int      visits;

        node_s() : total_score(0.0), visits(0) {
            action.id = -1;
        }
    } node_t;

    action_t search(const state_t& root_state) {
        std::vector<action_t> actions = getBestActions(root_state);
        size_t n = actions.size();

        std::vector<node_t> nodes(n);
        for (size_t i = 0; i < n; ++i) {
            nodes[i].state = simulate(root_state, std::vector<action_t>(1, actions[i]));
            nodes[i].action = actions[i];
        }

        for (int iter = 0; iter < iterations; ++iter) {
            for (size_t i = 0; i < n; ++i) {
                node_t& node = nodes[i];
                // Simulate a playout from this node
                state_t rollout_state = node.state;
                for (int d = 0; d < 5; ++d) {
                    std::vector<action_t> rollout_actions = getBestActions(rollout_state);
                    if (rollout_actions.empty()) break;
                    size_t idx = rand() % rollout_actions.size();
                    rollout_state = simulate(rollout_state, std::vector<action_t>(1, rollout_actions[idx]));
                }

                score_t result = evaluate(rollout_state);
                node.total_score += result;
                node.visits += 1;
            }
        }

        // Select best action based on average reward
        int best_idx = -1;
        double best_avg = -std::numeric_limits<double>::infinity();
        for (size_t i = 0; i < n; ++i) {
            if (nodes[i].visits > 0) {
                double avg = nodes[i].total_score / nodes[i].visits;
                if (avg > best_avg) {
                    best_avg = avg;
                    best_idx = (int)i;
                }
            }
        }

        if (best_idx == -1) {
            // fallback
            return actions[0];
        }

        return nodes[best_idx].action;
    }

} // namespace mcts

int main() {
    srand((unsigned int)time(0));
    state_t root;
    root.id = 0;

    action_t best_action = mcts::search(root);
    std::cout << "Best action id: " << best_action.id << std::endl;
    return 0;
}
