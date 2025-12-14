from setuptools           import setup
from Cython.Build         import cythonize
from setuptools.extension import Extension

extensions = [
    Extension(
        "structures", ["structures.pyx"],
        language = "c++",
    )
]

setup(
    ext_modules = cythonize(extensions)
)
