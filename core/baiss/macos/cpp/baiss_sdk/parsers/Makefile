
OUT_BIN_DIR := ./bin
OUT_LIB_DIR := ./lib

# CXXFLAGS=-std=c++11 -O3 -Wall -Wextra -Werror -fPIC

all: lib bin

bin:
	mkdir -p $(OUT_BIN_DIR)
	g++ "keywords_extractor.cpp" -std=c++11 ${CXXFLAGS} -o $(OUT_BIN_DIR)/libbaiss_parser.exe

lib:
	mkdir -p $(OUT_LIB_DIR)
	g++ "keywords_extractor.cpp" -std=c++11 ${CXXFLAGS} -fPIC -shared -o $(OUT_LIB_DIR)/libbaiss_parser.so
