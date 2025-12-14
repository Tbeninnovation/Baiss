
OUT_BIN_DIR := ./bin
OUT_LIB_DIR := ./lib

# CXXFLAGS=-std=c++11 -O3 -Wall -Wextra -Werror

all: lib bin

bin:
	mkdir -p $(OUT_BIN_DIR)
	g++ "structures.cpp" -std=c++17 ${CXXFLAGS} -o $(OUT_BIN_DIR)/libbaiss_structures.exe

lib:
	mkdir -p $(OUT_LIB_DIR)
	g++ "structures.cpp" -std=c++17 ${CXXFLAGS} -fPIC -shared -o $(OUT_LIB_DIR)/libbaiss_structures.so

fclean:
	rm -rf bin lib

re: fclean all
