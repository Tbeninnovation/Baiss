// @repository-root/c++/baiss_sdk/parsers/keywords_extractor.cpp

// #include "baiss_sdk/include/keywords_extractor.h"
// #include "baiss_sdk/include/string_utils.h"

// g++ -std=c++11 -o keywords_extractor.exe keywords_extractor.cpp

#include <iostream>
#include <string>
#include <vector>
#include <algorithm>
#include <cctype>
#include <cstddef>
#include <set>
#include <iostream>
#include <fstream>
#include <string>

namespace KeywordsExtractor {

    // get string language (e.g., "english", "french", etc.)
    std::string get_language_from_string(const std::string& content) {
        return "english";
    }

    bool is_stop_word(const std::string& word, const std::string& language) {
        // Simple Example:
        static const std::vector<std::string> english_stop_words = {
            "the", "is", "in", "and", "to", "a", "of", "that", "it", "with",
            "as", "for", "on", "was", "at", "by", "an", "this", "be", "are"
        };
        if (language == "english") {
            return std::find(english_stop_words.begin(), english_stop_words.end(), word) != english_stop_words.end();
        }
        return false;
    }

    // Function to split a string into words
    std::set<std::string> split_string(const std::string& str) {
        // split string: remove stop-words, duplicate words, and split by whitespace
        std::set<std::string> words;
        std::string word;
        for (char ch : str) {

            // Check if the character is a whitespace
            if (std::ispunct(ch)) {
                // Ignore punctuation characters
                continue;
            }

            if (std::isspace(ch)) {
                if (!word.empty()) {
                    // Convert to lowercase
                    std::string lower_word = word;
                    std::transform(lower_word.begin(), lower_word.end(), lower_word.begin(), ::tolower);
                    // Check if the word is not a stop word
                    if (!is_stop_word(lower_word, get_language_from_string(str))) {
                        words.insert(lower_word);
                    }
                    word.clear();
                }
            } else {
                word += ch;
            }
        }
        return words;
    }

    // Function to extract keywords from a string
    std::set<std::string> extract_keywords_from_string(const std::string& content) {
        std::set<std::string> keywords;
        std::set<std::string> words = split_string(content);

        for (const auto& word : words) {
            // Check if the word is not a stop word
            if (!is_stop_word(word, get_language_from_string(content))) {
                keywords.insert(word);
            }
        }

        return keywords;
    }

    std::string read_file_content(const std::string& filename) {
        std::ifstream file(filename.c_str(), std::ios::in | std::ios::binary);
        
        if (!file) {
            std::cerr << "Error: Cannot open file " << filename << std::endl;
            return "";
        }
    
        std::string content;
        file.seekg(0, std::ios::end);
        content.resize(static_cast<std::string::size_type>(file.tellg()));
        file.seekg(0, std::ios::beg);
        file.read(&content[0], content.size());
        file.close();
        
        return content;
    }

    std::set<std::string> extract_keywords_from_file(std::string filename) {
        std::string content = read_file_content(filename);
        return (extract_keywords_from_string(content));
    }

};

int main(void) {
    std::string filename = "keywords_extractor.cpp";
    std::set<std::string> keywords = KeywordsExtractor::extract_keywords_from_file(filename);
    for (const auto& keyword : keywords) {
        std::cout << keyword << std::endl;
    }
    return (0);
}
