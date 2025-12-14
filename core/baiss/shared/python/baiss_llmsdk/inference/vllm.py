# File: @baiss_llmsdk/inference/vllm.py
# Docs: https://vllm.ai

"""
    AssertionError: vLLM only supports Linux platform (including WSL).

    - For macOS CPU-only:
        * python3 -m pip install --upgrade pip setuptools wheel
        * pip install torch torchvision torchaudio
        * pip install "vllm==0.5.4" --no-build-isolation

"""

import os
import sys
import time
import json
import asyncio
import logging
from enum import Enum
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Union, AsyncGenerator, Iterator, Any, Tuple

logger = logging.getLogger(__name__)

class VllmInferenceEngine:
    pass

if __name__ == "__main__":
    pass
