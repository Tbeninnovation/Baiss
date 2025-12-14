
OUT_BIN_DIR := ./bin
OUT_LIB_DIR := ./lib

# CXXFLAGS=-std=c++11 -O3 -Wall -Wextra -Werror -fPIC

all: lib bin

bin:
	mkdir -p $(OUT_BIN_DIR)
	g++ "beam.cpp" -std=c++11 ${CXXFLAGS} -o $(OUT_BIN_DIR)/libbaiss_beam.exe
	g++ "mcts.cpp" -std=c++11 ${CXXFLAGS} -o $(OUT_BIN_DIR)/libbaiss_mcts.exe

lib:
	mkdir -p $(OUT_LIB_DIR)
	g++ "beam.cpp" -std=c++11 ${CXXFLAGS} -fPIC -shared -o $(OUT_LIB_DIR)/libbaiss_beam.so
	g++ "mcts.cpp" -std=c++11 ${CXXFLAGS} -fPIC -shared -o $(OUT_LIB_DIR)/libbaiss_mcts.so

.PHONY: bin lib all
