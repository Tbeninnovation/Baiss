

all:
	python setup.py build_ext --inplace

clean:
	rm -rf structures.cpp
	rm -rf structures.*.so
	rm -rf build

re-test: clean all
	python test.py

re: clean all
