#include "structures.hpp"
#include <iostream>
#include <dirent.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <unistd.h>
#include <algorithm>
#include <stack>
#include <sstream>
#include <chrono>
#include <filesystem>
#include <ctime>
#include <iomanip>
#include <optional> // Added for std::optional
#include <set>      // Added for std::set

#define CHAR_SINGLE_QUOTE '\''
#define CHAR_DOUBLE_QUOTE '"'
#define CHAR_BACK_SLASH   '\\'
#define CHAR_COLON         ':'
#define CHAR_COMMA         ','
#define CHAR_LEFT_BRACE    '{'
#define CHAR_RIGHT_BRACE   '}'
#define CHAR_LEFT_BRACKET  '['
#define CHAR_RIGHT_BRACKET ']'

namespace baiss_sdk {


    namespace parser {

        static size_t skipSpaces(size_t pos, const std::string& str, const size_t len) {
            while ( (pos < len) && isspace(str[pos]) ) {
                pos++;
            }
            return (pos);
        }

        static size_t parseListOfStrings(size_t pos, const std::string& str, const size_t len, std::vector<std::string>& lst) {
            lst.clear();
            pos = baiss_sdk::parser::skipSpaces(pos, str, len);
            if ( (pos >= len) || (str[pos] != CHAR_LEFT_BRACKET) ) {
                std::cerr << "Error: Invalid JSON string, expected '[' at position " << pos << std::endl;
                return len;
            }
            pos = baiss_sdk::parser::skipSpaces(pos + 1, str, len);
            while ( (pos < len) && (str[pos] != CHAR_RIGHT_BRACKET) ) {
                pos = baiss_sdk::parser::skipSpaces(pos, str, len);
                if (pos >= len) {
                    break ;
                }
                const char p = str[pos];
                if ( (p != CHAR_SINGLE_QUOTE) && (p != CHAR_DOUBLE_QUOTE) ) {
                    std::cerr << "Error: Invalid JSON string, expected `\"` or `'` at position " << pos << std::endl;
                    lst.clear();
                    return len;
                }
                pos++;
                std::string value = "";
                while ( (pos < len) && (str[pos] != p) ) {
                    if (str[pos] == CHAR_BACK_SLASH) {
                        pos++;
                        if (pos >= len) {
                            std::cerr << "Error: Invalid JSON string, unexpected end of input after escape character at position " << pos << std::endl;
                            lst.clear();
                            return len;
                        }
                    }
                    value += str[pos];
                    pos++;
                }
                if ( (pos >= len) || (str[pos] != p) ) {
                    std::cerr << "Error: Invalid JSON string, expected closing '" << p << "' at position " << pos << std::endl;
                    lst.clear();
                    return len;
                }
                lst.push_back(value);
                pos = baiss_sdk::parser::skipSpaces(pos + 1, str, len);
                if ( (pos >= len) || (str[pos] == CHAR_RIGHT_BRACKET) ) {
                    break ;
                }
                if ( (pos >= len) || (str[pos] != CHAR_COMMA) ) {
                    std::cerr << "Error: Invalid JSON string, expected ',' or ']' after value at position " << pos << std::endl;
                    lst.clear();
                    return len;
                }
                pos = baiss_sdk::parser::skipSpaces(pos + 1, str, len);
            }
            if ( (pos >= len) || (str[pos] != CHAR_RIGHT_BRACKET) ) {
                std::cerr << "Error: Invalid JSON string, expected ']' at position " << pos << std::endl;
                lst.clear();
                return len;
            }
            pos++;
            return (pos);
        }
    }

    static size_t parseFileInfo(size_t pos, const std::string str, const size_t len, baiss_sdk::JsonStructure& result) {
        pos = baiss_sdk::parser::skipSpaces(pos, str, len);
        if ((pos >= len) || (str[pos] != '{') ) {
            std::cerr << "Error: Invalid JSON string, expected '{' at position " << pos << std::endl;
        }
        pos = baiss_sdk::parser::skipSpaces(pos + 1, str, len);
        while ( true ) {
            pos = baiss_sdk::parser::skipSpaces(pos, str, len);
            if ( (pos >= len) || (str[pos] == '}') ) {
                break ;
            }
            const char p = str[pos];
            if ( (p != CHAR_SINGLE_QUOTE) && (p != CHAR_DOUBLE_QUOTE) ) {
                std::cerr << "Error: Invalid JSON string, expected '\"' or '}' at position " << pos << std::endl;
                return len;
            }
            pos++;
            std::string key = "";
            while ( (pos < len) && (str[pos] != p) ) {
                if (str[pos] == CHAR_BACK_SLASH) {
                    pos++;
                    if (pos >= len) {
                        std::cerr << "Error: Invalid JSON string, unexpected end of input after escape character at position " << pos << std::endl;
                        return len;
                    }
                }
                key += str[pos];
                pos++;
            }
            if ( (pos >= len) || (str[pos] != p) ) {
                std::cerr << "Error: Invalid JSON string, expected closing '" << p << "' at position " << pos << std::endl;
                return len;
            }
            pos = baiss_sdk::parser::skipSpaces(pos + 1, str, len);
            if ( (pos >= len) || (str[pos] != ':') ) {
                std::cerr << "Error: Invalid JSON string, expected ':' after key '" << key << "' at position " << pos << std::endl;
                return len;
            }
            pos = baiss_sdk::parser::skipSpaces(pos + 1, str, len);
            const char q = str[pos];
            if (pos >= len) {
                std::cerr << "Error: Invalid JSON string, unexpected end of input after key '" << key << "' at position " << pos << std::endl;
                return len;
            }
            if (q == CHAR_LEFT_BRACKET) {
                std::vector<std::string> value;
                pos = baiss_sdk::parser::parseListOfStrings(pos, str, len, value);
                if (pos >= len) {
                    std::cerr << "Error: Invalid JSON string, unexpected end of input after list at position " << pos << std::endl;
                    return len;
                }
                if (result.setStringsList(key, value) == false) {
                    std::cerr << "Warning: Failed to set list for key '" << key << "' at position " << pos << std::endl;
                }
            } else if ( (q == CHAR_SINGLE_QUOTE) || (q == CHAR_DOUBLE_QUOTE) ) {
                pos++;
                std::string value = "";
                while ( (pos < len) && (str[pos] != q) ) {
                    if (str[pos] == CHAR_BACK_SLASH) {
                        pos++;
                        if (pos >= len) {
                            std::cerr << "Error: Invalid JSON string, unexpected end of input after escape character at position " << pos << std::endl;
                            return len;
                        }
                    }
                    value += str[pos];
                    pos++;
                }
                if ( (pos >= len) || (str[pos] != q) ) {
                    std::cerr << "Error: Invalid JSON string, expected closing '" << q << "' at position " << pos << std::endl;
                    return len;
                }
                pos = baiss_sdk::parser::skipSpaces(pos + 1, str, len);
                if (result.setString(key, value) == false) {
                    std::cerr << "Warning: Failed to set string for key '" << key << "' at position " << pos << std::endl;
                }
            } else {
                std::string value = "";
                while ( (pos < len) && (!isspace(str[pos])) && (str[pos] != CHAR_COMMA) && (str[pos] != CHAR_RIGHT_BRACE) ) {
                    value += str[pos];
                    pos++;
                }
                if (result.setLiteral(key, value) == false) {
                    std::cerr << "Warning: Failed to set literal for key '" << key << "' at position " << pos << std::endl;
                }
            }
            pos = baiss_sdk::parser::skipSpaces(pos, str, len);
            if ( (pos < len) && (str[pos] == CHAR_RIGHT_BRACE) ) {
                break;
            }
            if ( (pos >= len) || (str[pos] != CHAR_COMMA) ) {
                std::cerr << "Error: Invalid JSON string, expected ',' or '}' after value for key '" << key << "' at position " << pos << std::endl;
                return len;
            }
            while ( (pos < len) && (str[pos] == CHAR_COMMA) ) {
                pos = baiss_sdk::parser::skipSpaces(pos + 1, str, len);
            }
        }
        if ( (pos >= len) || (str[pos] != CHAR_RIGHT_BRACE) ) {
            // If we reach here, it means we expected a '}' but didn't find it
            std::cerr << "Error: Invalid JSON string, expected '}' at position " << pos << std::endl;
            return len;
        }
        pos++;
        return (pos);
    }

    static size_t parseFilesSection(size_t pos, const std::string& str, const size_t len,
                                    baiss_sdk::JsonStructure& result) {
        pos = baiss_sdk::parser::skipSpaces(pos, str, len);
        if ( (pos >= len) || (str[pos] != ':') ) {
            std::cerr << "Error: Invalid JSON string, expected ':' after 'files' key at position " << pos << std::endl;
            return len;
        }
        pos = baiss_sdk::parser::skipSpaces(pos + 1, str, len);
        if ( (pos >= len) || (str[pos] != '{') ) {
            std::cerr << "Error: Invalid JSON string, expected '{' after 'files' key at position " << pos << std::endl;
            return len;
        }
        pos++;
        while (pos < len) {
            pos = baiss_sdk::parser::skipSpaces(pos, str, len);
            if (pos >= len) {
                std::cerr << "Error: Invalid JSON string, unexpected end of input" << std::endl;
                return len;
            }
            const char p = str[pos];
            if (p == '}') {
                pos++;
                break;
            }
            if ( (p != '"') && (p != '\'') ) {
                std::cerr << "Error: Invalid JSON string, expected '\"' or '}' at position " << pos << std::endl;
                return len;
            }
            pos++;
            std::string pathname = "";
            while ( (pos < len) && (str[pos] != p) ) {
                if (str[pos] == '\\') {
                    pos++;
                    if (pos >= len) {
                        std::cerr << "Error: Invalid JSON string, unexpected end of input after escape character at position " << pos << std::endl;
                        return len;
                    }
                }
                pathname += str[pos];
                pos++;
            }
            if ( (pos >= len) || (str[pos] != p) ) {
                std::cerr << "Error: Invalid JSON string, expected closing '" << p << "' at position " << pos << std::endl;
                return len;
            }
            pos = baiss_sdk::parser::skipSpaces(pos + 1, str, len);
            if ( (pos >= len) || (str[pos] != ':') ) {
                std::cerr << "Error: Invalid JSON string, expected ':' after key '" << pathname << "' at position " << pos << std::endl;
                return len;
            }
            pos = baiss_sdk::parser::skipSpaces(pos + 1, str, len);
            pos = baiss_sdk::parseFileInfo(pos, str, len, result);
            std::cout << str[pos] << std::endl;
            exit(0);    // <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<< DEBUG
        }
        return (0);
    }

    TreeStructure::Json TreeStructure::loadJsonString(std::string str) {
        TreeStructure::Json result;
        size_t pos = 0;
        size_t len = str.length();
        while ( (pos < len) && isspace(str[pos]) ) {
            pos++;
        }
        if ( (pos >= len) || ( str[pos] != '{') ) {
            std::cerr << "Error: Invalid JSON string" << std::endl;
            return TreeStructure::Json();
        }
        pos++;
        while (pos < len) {
            while ( (pos < len) && isspace(str[pos]) ) {
                pos++;
            }
            if (pos >= len) {
                std::cerr << "Error: Invalid JSON string, unexpected end of input" << std::endl;
                return TreeStructure::Json();
            }
            const char c = str[pos];
            if (c == '}') {
                break;
            }
            if ( (c != '"') && (c != '\'') ) {
                std::cerr << "Error: Invalid JSON string, expected '\"' or '}' at position " << pos << std::endl;
                return TreeStructure::Json();
            }
            std::string key;
            pos++;
            while ( (pos < len) && (str[pos] != c) ) {
                if (str[pos] == '\\') {
                    pos++;
                    if (pos >= len) {
                        std::cerr << "Error: Invalid JSON string, unexpected end of input after escape character at position " << pos << std::endl;
                        return TreeStructure::Json();
                    }
                }
                key += str[pos];
                pos++;
            }
            if ( (pos >= len) || (str[pos] != c) ) {
                std::cerr << "Error: Invalid JSON string, expected closing '" << c << "' at position " << pos << std::endl;
                return TreeStructure::Json();
            }
            pos++;
            if (key == "files") {
                pos = baiss_sdk::parseFilesSection(pos, str, len, result);
                continue;
            }
            while ( (pos < len) && isspace(str[pos]) ) {
                pos++;
            }
            if ( (pos >= len) || (str[pos] != ':') ) {
                std::cerr << "Error: Invalid JSON string, expected ':' after key '" << key << "' at position " << pos << std::endl;
                return TreeStructure::Json();
            }
            pos++;
            while ( (pos < len) && isspace(str[pos]) ) {
                pos++;
            }
            if ( (pos >= len) || ( (str[pos] != '"') && (str[pos] != '\'')) ) {
                std::cerr << "Error: Invalid JSON string, expected value after key '" << key << "' at position " << pos << std::endl;
                return TreeStructure::Json();
            }
            const char p = str[pos];
            pos++;
            std::string value;
            while ( (pos < len) && (str[pos] != p) ) {
                if (str[pos] == '\\') {
                    pos++;
                    if (pos >= len) {
                        std::cerr << "Error: Invalid JSON string, unexpected end of input after escape character at position " << pos << std::endl;
                        return TreeStructure::Json();
                    }
                }
                value += str[pos];
                pos++;
            }
            if ( (pos >= len) || (str[pos] != p) ) {
                std::cerr << "Error: Invalid JSON string, expected closing '" << p << "' for value at position " << pos << std::endl;
                return TreeStructure::Json();
            }
            pos++;
            while ( (pos < len) && isspace(str[pos]) ) {
                pos++;
            }
            if ( (pos < len) && (str[pos] != ',') && (str[pos] != '}') ) {
                std::cerr << "Error: Invalid JSON string, expected ',' or '}' after value for key '" << key << "' at position " << pos << std::endl;
                return TreeStructure::Json();
            }
            if (str[pos] == ',') {
                pos++;
            } else if (str[pos] == '}') {
                break;
            }
            if (key == "type") {
                result.type = value;
            }
        }
        std::cout << str<< std::endl;
        exit(0);


        return TreeStructure::Json();
    }

    TreeStructure::Json TreeStructure::loadJsonFile(std::string filename) {
        std::ifstream file(filename);
        if (!file) {
            return TreeStructure::loadJsonString("");
        }
        std::ostringstream buffer;
        buffer << file.rdbuf();
        file.close();
        std::string content = buffer.str();
        const size_t len = content.length();
        return (TreeStructure::loadJsonString(content));
    }






    std::string TreeStructure::formatTimestamp(time_t timestamp) {
        // Format timestamp as ISO 8601 string (YYYY-MM-DD HH:MM:SS)
        std::tm* timeinfo = std::localtime(&timestamp);
        std::ostringstream oss;
        oss << std::put_time(timeinfo, "%Y-%m-%d %H:%M:%S");
        return oss.str();
    }

    std::string TreeStructure::getContentType(const std::string& filename) {
        // Get the file extension from a filename.
        auto pos = filename.find_last_of('.');
        if (pos == std::string::npos) return "application/octet-stream";

        std::string ext = filename.substr(pos + 1);
        std::transform(ext.begin(), ext.end(), ext.begin(), ::tolower);

        // Simple content type mapping
        static const std::unordered_map<std::string, std::string> mimeTypes = {
            {"txt", "text/plain"}, {"json", "application/json"}, {"csv", "text/csv"},
            {"html", "text/html"}, {"css", "text/css"}, {"js", "application/javascript"},
            {"png", "image/png"}, {"jpg", "image/jpeg"}, {"jpeg", "image/jpeg"},
            {"gif", "image/gif"}, {"pdf", "application/pdf"}, {"zip", "application/zip"},
            {"xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"},
            {"md", "text/markdown"}
        };

        auto it = mimeTypes.find(ext);
        return (it != mimeTypes.end()) ? it->second : "application/octet-stream";
    }

    bool TreeStructure::isAllowedFileType(const std::string& filename, const std::vector<std::string>& allowedExtensions) {
        // Get the file extension from a filename
        auto pos = filename.find_last_of('.');
        if (pos == std::string::npos) return false;

        std::string ext = filename.substr(pos + 1);
        std::transform(ext.begin(), ext.end(), ext.begin(), ::tolower);

        // Check if extension is in the allowed list
        return std::find(allowedExtensions.begin(), allowedExtensions.end(), ext) != allowedExtensions.end();
    }

    bool TreeStructure::isExcludedPath(const std::string& fullPath) {
        // Convert to lowercase for case-insensitive matching
        std::string lowerPath = fullPath;
        std::transform(lowerPath.begin(), lowerPath.end(), lowerPath.begin(), ::tolower);

        // Check for path patterns that should be entirely excluded (with trailing slash)
        static const std::vector<std::string> excludedPathPatterns = {
            "/.venv/", "/venv/", "/site-packages/", "/node_modules/",
            "/__pycache__/", "/.git/", "/vendor/", "/.tox/",
            "/build/", "/dist/", "/target/", "/.pytest_cache/"
        };

        // Check for path patterns ending with these directories
        static const std::vector<std::string> excludedEndPatterns = {
            "/.venv", "/venv", "/site-packages", "/node_modules",
            "/__pycache__", "/.git", "/vendor", "/.tox",
            "/build", "/dist", "/target", "/.pytest_cache"
        };

        // Check if path contains excluded patterns
        for (const auto& pattern : excludedPathPatterns) {
            if (lowerPath.find(pattern) != std::string::npos) {
                return true;
            }
        }

        // Check if path ends with excluded patterns
        for (const auto& pattern : excludedEndPatterns) {
            if (lowerPath.length() >= pattern.length() &&
                lowerPath.substr(lowerPath.length() - pattern.length()) == pattern) {
                return true;
            }
        }

        return false;
    }

    bool TreeStructure::isExcludedFolder(const std::string& folderName) {
        // Define common folders to exclude
        static const std::vector<std::string> excludedFolders = {
            "__pycache__", ".git", ".vscode", ".idea", "node_modules",
            "build", "dist", "target", ".pytest_cache", ".mypy_cache",
            ".DS_Store", "Thumbs.db", ".cache", ".npm", ".yarn",
            // Virtual environments and package directories
            ".venv", "venv", "site-packages", "vendor", ".env",
            // Additional package manager directories
            "bower_components", ".tox", ".coverage", "htmlcov",
            // Language-specific build/package directories
            "bin", "obj", "packages", ".nuget", "lib"
        };

        // Check if folder name is in the excluded list
        if (std::find(excludedFolders.begin(), excludedFolders.end(), folderName) != excludedFolders.end()) {
            return true;
        }

        // Check for pattern matching (case-insensitive)
        std::string lowerFolderName = folderName;
        std::transform(lowerFolderName.begin(), lowerFolderName.end(), lowerFolderName.begin(), ::tolower);

        // Exclude any folder containing these patterns
        static const std::vector<std::string> excludedPatterns = {
            "site-packages", "node_modules", "__pycache__", ".pytest_cache"
        };

        for (const auto& pattern : excludedPatterns) {
            if (lowerFolderName.find(pattern) != std::string::npos) {
                return true;
            }
        }

        // Check for file extension patterns (folders ending with these extensions)
        static const std::vector<std::string> excludedExtensions = {
            ".app", ".jdk", ".apps"
        };

        for (const auto& ext : excludedExtensions) {
            if (lowerFolderName.length() >= ext.length() &&
                lowerFolderName.substr(lowerFolderName.length() - ext.length()) == ext) {
                return true;
            }
        }

        return false;
    }

    std::string TreeStructure::escapeJson(const std::string& str) {
        std::ostringstream escaped;

        for (char c : str) {
            switch (c) {
                case '"':  escaped << "\\\""; break;
                case '\\': escaped << "\\\\"; break;
                case '\b': escaped << "\\b";  break;
                case '\f': escaped << "\\f";  break;
                case '\n': escaped << "\\n";  break;
                case '\r': escaped << "\\r";  break;
                case '\t': escaped << "\\t";  break;
                default:   escaped << c;      break;
            }
        }
        return escaped.str();
    }

    void TreeStructure::writeJsonValue(std::ofstream& file, const FileInfo& info, int indent) {
        std::string indentStr(indent, ' ');
        std::string innerIndentStr(indent + 2, ' ');

        file << "{\n";

        file << innerIndentStr << "\"name\": \"" << escapeJson(info.name) << "\",\n";
        file << innerIndentStr << "\"type\": \"" << info.type << "\",\n";
        file << innerIndentStr << "\"depth\": " << info.depth << ",\n";
        file << innerIndentStr << "\"size\": " << info.size << ",\n";
        file << innerIndentStr << "\"last_modified\": \"" << escapeJson(info.lastModified) << "\",\n";

        file << innerIndentStr << "\"content_type\": " <<
                (info.type == "folder" ? "null" : "\"" + escapeJson(info.contentType) + "\"") << ",\n";

        // Check if children vector is not empty
        if (!info.children.empty()) {
            file << innerIndentStr << "\"children\": [";

            // Traditional for loop with index to know when we're at the last item
            for (size_t i = 0; i < info.children.size(); ++i) {
                file << "\"" << escapeJson(info.children[i]) << "\"";
                // Add comma except for last item
                if (i < info.children.size() - 1) file << ", ";
            }
            file << "],\n";
        } else {
            file << innerIndentStr << "\"children\": null,\n";
        }

        file << innerIndentStr << "\"keywords\": null\n";
        file << indentStr << "}";
    }

    TreeStructure::TreeMap TreeStructure::generate(const std::string& path, const std::vector<std::string>& allowedExtensions) {
        TreeMap result;
        result.reserve(1000);

        // Convert to absolute path first
        char* absolutePath = realpath(path.c_str(), nullptr);
        if (!absolutePath) {
            std::cerr << "Error: Could not resolve path " << path << std::endl;
            return result;
        }
        std::string basePath(absolutePath);
        free(absolutePath);  // realpath allocates memory that we need to free

        std::stack<std::pair<std::string, int>> dirsToProcess;
        dirsToProcess.emplace(basePath, 1);

        while (!dirsToProcess.empty()) {
            auto [currentDir, currentDepth] = dirsToProcess.top();
            dirsToProcess.pop();

            DIR* dir = opendir(currentDir.c_str());
            if (dir == nullptr) {
                continue ;
            }
            std::vector<std::string> children;
            struct dirent* entry;
            while ((entry = readdir(dir)) != nullptr) {
                std::string entryName(entry->d_name);
                if (entryName == "." || entryName == "..") {
                    continue;
                }

                // Build full absolute path properly
                std::string entryPath = currentDir + "/" + entryName;
                // Remove double slashes
                size_t pos = entryPath.find("//");
                while (pos != std::string::npos) {
                    entryPath.replace(pos, 2, "/");
                    pos = entryPath.find("//", pos);
                }

                struct stat statBuffer;
                if (stat(entryPath.c_str(), &statBuffer) != 0) {
                    continue;
                }

                FileInfo info;
                info.name = entryName;
                info.depth = currentDepth;
                info.size = statBuffer.st_size;
                info.lastModified = formatTimestamp(statBuffer.st_mtime);

                if (S_ISDIR(statBuffer.st_mode)) {
                    // Skip excluded folders (by name or path pattern)
                    if (isExcludedFolder(entryName) || isExcludedPath(entryPath)) {
                        continue;
                    }

                    info.type = "folder";
                    info.contentType = "";
                    // Always include directories (they might contain allowed files)
                    children.push_back(entryName);
                    // Add directory to stack for future processing
                    dirsToProcess.emplace(entryPath, currentDepth + 1);
                    // Store in result map using move semantics for efficiency
                    result[entryPath] = std::move(info);
                } else {
                    // Only include files if they have allowed extensions or if no filter is specified
                    if (allowedExtensions.empty() || isAllowedFileType(entryName, allowedExtensions)) {
                        info.type = "file";
                        // Determine MIME type based on extension
                        info.contentType = getContentType(entryName);
                        children.push_back(entryName);
                        result.emplace(entryPath, std::move(info));
                    }
                }
            }
            auto it = result.find(currentDir);
            if (it != result.end()) {
                it->second.children = std::move(children);
            }
            closedir(dir);
        }
        return result;
    }

    bool TreeStructure::saveToJson(const TreeMap& structure, const std::string& filename) {
        // Create output file stream
        std::ofstream file(filename);
        if (!file.is_open()) {
            std::cerr << "Error: Cannot create file " << filename << std::endl;
            return false;
        }

        file << "{\n";

        bool first = true;
        for (const auto& [path, info] : structure) {
            if (!first) {
                file << ",\n";
            }

            // Write key (file path) in quotes with proper escaping
            file << "  \"" << escapeJson(path) << "\": ";
            // Write value (FileInfo object) as nested JSON
            writeJsonValue(file, info);

            first = false;
        }
        if (!structure.empty()) {
            file << "\n";
        }

        file << "}\n";
        file.close();

        std::cout << "Tree structure saved to: " << filename << std::endl;
        std::cout << "Total entries: " << structure.size() << std::endl;
        return true;
    }

    bool TreeStructure::saveToJsonWithWrapper(const TreeMap& structure, const std::string& filename, const std::string& fileType) {
        // Create output file stream
        std::ofstream file(filename);
        if (!file.is_open()) {
            std::cerr << "Error: Cannot create file " << filename << std::endl;
            return false;
        }

        // Start with wrapper structure
        file << "{\n";
        file << "  \"type\": \"" << escapeJson(fileType) << "\",\n";
        file << "  \"files\": {\n";

        bool first = true;
        for (const auto& [path, info] : structure) {
            if (!first) {
                file << ",\n";
            }

            // Write key (file path) in quotes with proper escaping
            file << "  \"" << escapeJson(path) << "\": ";
            // Write value (FileInfo object) as nested JSON with additional indentation
            writeJsonValue(file, info, 2);

            first = false;
        }
        if (!structure.empty()) {
            file << "\n";
        }

        file << "  }\n";
        file << "}\n";
        file.close();

        std::cout << "Tree structure with wrapper saved to: " << filename << std::endl;
        std::cout << "Total entries: " << structure.size() << std::endl;
        return true;
    }
}

int nativegen(std::vector<std::string> targetPaths, std::string outputDir, std::string outputFile) {


    // Parse command line arguments with defaults
    std::string targetPath = targetPaths[0];

    // Create output directory if it doesn't exist
    try {
        std::filesystem::create_directories(outputDir);
        std::cout << "Output directory: " << std::filesystem::absolute(outputDir) << std::endl;
    } catch (const std::exception& e) {
        std::cerr << "Error creating directory " << outputDir << ": " << e.what() << std::endl;
        return 1;
    }

    // Define allowed file extensions
    std::vector<std::string> allowedExtensions = {"txt", "csv", "pdf", "xlsx", "md"};

    std::cout << "Scanning directory: " << targetPath << std::endl;
    std::cout << "Allowed file types: ";
    for (size_t i = 0; i < allowedExtensions.size(); ++i) {
        std::cout << allowedExtensions[i];
        if (i < allowedExtensions.size() - 1) std::cout << ", ";
    }
    std::cout << std::endl;
    std::cout << "Excluded folders: __pycache__, .git, .vscode, .idea, node_modules, .venv, venv, site-packages, vendor, build, dist, target, .pytest_cache, .mypy_cache, and other package/cache directories" << std::endl;

    // Measure execution time for performance tracking
    auto start = std::chrono::high_resolution_clock::now();

    // Generate the complete directory structure from any given path with file filtering
    auto structure = baiss_sdk::TreeStructure::generate(targetPath, allowedExtensions);

    auto end = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end - start);

    std::cout << "Scan completed in: " << duration.count() << "ms" << std::endl;

    // Save main structure to JSON file
    baiss_sdk::TreeStructure::saveToJson(structure, outputFile);

    // Generate separate tree structures for each file type
    std::cout << "\nGenerating individual file type structures:" << std::endl;
    for (const auto& fileType : allowedExtensions) {
        std::cout << "Generating " << fileType << " tree structure..." << std::endl;

        // Create single file type vector
        std::vector<std::string> singleType = {fileType};

        // Generate structure for this specific file type only
        auto singleTypeStructure = baiss_sdk::TreeStructure::generate(targetPath, singleType);

        baiss_sdk::TreeStructure::TreeMap filteredStructure;
        filteredStructure.reserve(singleTypeStructure.size());

        for (const auto& [path, info] : singleTypeStructure) {
            if (info.type == "file") {
                filteredStructure.emplace(path, info);
            }
        }

        // Create output filename
        std::string singleTypeOutputFile = outputDir + fileType + "_tree_structure.json";

        baiss_sdk::TreeStructure::saveToJsonWithWrapper(filteredStructure, singleTypeOutputFile, fileType);

        std::cout << "  " << fileType << " structure saved to: " << singleTypeOutputFile
                  << " (entries: " << filteredStructure.size() << ")" << std::endl;
    }
    return (0);
}

extern "C" int cnativegen(char** ctargetPaths, char* outputDir, char* outputFile, size_t targetPathsLength) {

    std::vector<std::string> targetPaths;
    for (size_t k = 0; k < targetPathsLength; k++) {
        std::string targetPath = "";
        if (ctargetPaths[k]) {
            targetPath = ctargetPaths[k];
        }
        targetPaths.push_back(targetPath);
    }
    return (nativegen(targetPaths, outputDir, outputFile));
}

#if __NAME__MAIN__




#endif // __NAME__MAIN__
