#pragma once

#include <map>
#include <string>
#include <vector>
#include <unordered_map>
#include <fstream>

namespace baiss_sdk {

    struct FileInfo {
        std::string name;
        std::string type;
        int depth;
        size_t size;
        std::string contentType;
        std::vector<std::string> children;
        std::string lastModified;

        FileInfo() : depth(0), size(0) {}
    };

    class JsonStructure {
        /*
            {
                "type": "txt | csv | ...",
                "files": {
                    "<path>": {
                    "name"         : "requirements.txt",
                    "type"         : "file",
                    "depth"        : 2,
                    "size"         : 118,
                    "last_modified": "2025-07-30 21:40:19",
                    "content_type" : "text/plain",
                    "children": null,
                    "keywords": null
                },
                ...
            }
        */
        public:
            std::string type;
            std::map<std::string, baiss_sdk::FileInfo> files;



            bool setString(std::string key, std::string value) {
                if (key == "type") {
                    this->type = value;
                    return true;
                }

                // This function should set a key-value pair in the JSON structure
                // Implementation is not provided in the original code snippet
                return false;
            }

            bool setLiteral(std::string key, std::string value) {
                // This function should set a key-value pair in the JSON structure
                // Implementation is not provided in the original code snippet
                return false;
            }

            bool setStringsList(std::string key, const std::vector<std::string>& value) {
                // This function should set a key with a list of strings in the JSON structure
                // Implementation is not provided in the original code snippet
                return false;
            }

            std::string toString(void) const {
                std::string content = "";
                return (content);
            }
    };

    class TreeStructure {
    private:
        /**
         * Format a timestamp as an ISO 8601 string.
         * @param timestamp The timestamp to format.
         * @return Formatted timestamp string.
         */
        static std::string formatTimestamp(time_t timestamp);

        /**
         * Get the content type of a file.
         * @param filename The name of the file.
         * @return The content type of the file.
         */
        static std::string getContentType(const std::string& filename);

        /**
         * Check if a file has an allowed extension.
         * @param filename The name of the file.
         * @param allowedExtensions Vector of allowed file extensions.
         * @return True if file extension is allowed, false otherwise.
         */
        static bool isAllowedFileType(const std::string& filename, const std::vector<std::string>& allowedExtensions);

        /**
         * Check if a path should be excluded from the tree structure.
         * @param fullPath The full path to check.
         * @return True if path should be excluded, false otherwise.
         */
        static bool isExcludedPath(const std::string& fullPath);

        /**
         * Check if a folder should be excluded from the tree structure.
         * @param folderName The name of the folder.
         * @return True if folder should be excluded, false otherwise.
         */
        static bool isExcludedFolder(const std::string& folderName);

        /**
         * Escapes special characters in a string for JSON format
         * @param str: The string to escape
         * @return: JSON-safe string with escaped characters
         */
        static std::string escapeJson(const std::string& str);

        /**
         * Writes a FileInfo object as JSON to file
         * @param file: reference to output file stream
         * @param info: FileInfo object to write
         * @param indent: number of spaces for indentation (default 2)
         */
        static void writeJsonValue(std::ofstream& file, const FileInfo& info, int indent = 2);

    public:
        using TreeMap = std::unordered_map<std::string, FileInfo>;
        using Json    = baiss_sdk::JsonStructure;
        /**
         * Generate a complete directory tree structure from the given path
         * @param path The directory path to scan
         * @param allowedExtensions Vector of allowed file extensions (empty vector means no filtering)
         * @return TreeMap containing the complete file structure
         */
        static TreeMap generate(const std::string& path, const std::vector<std::string>& allowedExtensions = {});

        /**
         * Saves the complete tree structure to a JSON file
         * @param structure The TreeMap containing file structure data
         * @param filename Output file path for JSON data
         * @return True if file saved successfully, false on error
         */
        static bool saveToJson(const TreeMap& structure, const std::string& filename);

        /**
         * Saves the tree structure to a JSON file with a type wrapper
         * @param structure The TreeMap containing file structure data
         * @param filename Output file path for JSON data
         * @param fileType The file type string to include in the wrapper
         * @return True if file saved successfully, false on error
         */
        static bool saveToJsonWithWrapper(const TreeMap& structure, const std::string& filename, const std::string& fileType);

        /**
            * Load a JSON file and return its structure
            * @param filename The name of the JSON file to load
            * @return JsonStructure containing the loaded data
        */
        static TreeStructure::Json loadJsonFile(std::string filename);

        /**
         * Load a JSON string and return its structure
         * @param structureString The JSON string to load
         * @return JsonStructure containing the loaded data
         */
        static TreeStructure::Json loadJsonString(std::string structureString);

    };
}

#ifdef __cplusplus
extern "C" {
#endif

int nativegen(char** targetPaths, char* outputDir, char* outputFile, size_t targetPathsLength);

#ifdef __cplusplus
}
#endif
