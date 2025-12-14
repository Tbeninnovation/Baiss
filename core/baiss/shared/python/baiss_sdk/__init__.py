import os
import sys
import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])

def get_baiss_project_path(*args) -> str:
    project_root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
    project_root = os.path.dirname(project_root)  # Move up one more level to reach 'baiss-build'
    project_root = os.path.dirname(project_root)
    return os.path.join(project_root, *args)
