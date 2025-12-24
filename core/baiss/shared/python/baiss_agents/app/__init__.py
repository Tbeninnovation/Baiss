"""
Baiss API Application Package
"""

import os
import sys
import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])



def get_version() -> str:
    """Get version lazily to avoid import-time dependency on initialized settings"""
    #try:
    #    from baiss_agents.app.core.config import get_settings
    #    return get_settings().VERSION
    #except RuntimeError:
    return "0.1.0"  # fallback version

__version__ = get_version()
