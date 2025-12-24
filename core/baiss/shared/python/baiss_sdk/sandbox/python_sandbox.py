import io
import uuid
import types
import contextlib
import importlib
import multiprocessing
from baiss_sdk.parsers.python_extractor import PythonExtractor

__SAFE_BUILTINS__ = {
    # '__builtins__': __builtins__,
    'print'       : print,
    'type'        : type,
    'all'         : all,
    'len'         : len,
    'str'         : str,
    'int'         : int,
    'float'       : float,
    'list'        : list,
    'dict'        : dict,
    'tuple'       : tuple,
    'set'         : set,
    'range'       : range,
    'abs'         : abs,
    'round'       : round,
    'max'         : max,
    'min'         : min,
    'sum'         : sum,
    'sorted'      : sorted,
    'isinstance'  : isinstance,
    'Exception'   : Exception,
    '__import__'  : __import__,
}

def _worker_exec(code: str, result_queue: multiprocessing.Queue, builtins: dict, retvar_name: str, module_names: list, tool_references: list):
    """
    Worker function that accepts module names as strings and tool references.
    Reconstructs the environment by dynamically importing modules and tools.
    
    Args:
        code: The code to execute
        result_queue: Queue for returning results
        builtins: Safe builtin functions/values (picklable only)
        retvar_name: Name of variable to return
        module_names: List of module names to import (strings)
        tool_references: List of tool reference dicts with keys:
            - name: The name to expose in sandbox
            - module_path: The module to import from
            - class_name: The class name (optional)
            - method_name: The method name (optional, if class_name is provided)
            - init_kwargs: kwargs to pass to class __init__ (optional)
    """
    import asyncio
    
    stdout = io.StringIO()
    stderr = io.StringIO()
    
    # 1. Reconstruct the environment with builtins
    session_globals = {'__builtins__': builtins}
    
    # 2. Dynamically import requested modules inside the worker
    for mod_name in module_names:
        try:
            mod = importlib.import_module(mod_name)
            session_globals[mod_name] = mod
            if mod_name == "pandas":
                session_globals["pd"] = mod
        except ImportError as e:
            stderr.write(f"Warning: Could not import module '{mod_name}': {e}\n")
    
    # 3. Resolve tool references (functions/methods from other modules)
    for tool_ref in tool_references:
        try:
            name = tool_ref["name"]
            module_path = tool_ref["module_path"]
            class_name = tool_ref.get("class_name")
            method_name = tool_ref.get("method_name")
            init_kwargs = tool_ref.get("init_kwargs", {})
            
            # Import the module
            mod = importlib.import_module(module_path)
            
            if class_name:
                # Get the class and instantiate it
                cls = getattr(mod, class_name)
                instance = cls(**init_kwargs)
                if method_name:
                    # Get the method from the instance
                    func = getattr(instance, method_name)
                else:
                    # Use the instance itself
                    func = instance
            else:
                # Get a function directly from the module
                func = getattr(mod, method_name or name)
            
            session_globals[name] = func
        except Exception as e:
            stderr.write(f"Warning: Could not resolve tool '{tool_ref.get('name', 'unknown')}': {e}\n")

    try:
        with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            exec(code, session_globals)
        
        result_queue.put({
            "success"  : True,
            "status"   : 200,
            "stdout"   : str(stdout.getvalue()).strip(),
            "stderr"   : str(stderr.getvalue()).strip(),
            "return"   : session_globals.get(retvar_name),
            "error"    : None
        })
    except Exception as e:
        result_queue.put({
            "success"  : False,
            "status"   : 500,
            "stdout"   : str(stdout.getvalue()).strip(),
            "stderr"   : str(stderr.getvalue()).strip(),
            "return"   : session_globals.get(retvar_name),
            "error"    : f"{type(e).__name__}: {str(e)}\n{stderr.getvalue()}",
        })

class PythonSandbox:

    def __init__(self,
        code    : str  = "",
        builtins: list = None,
        timeout : int  = 5000
    ):
        self._code     = code
        self._builtins = {}
        self._timeout  = timeout
        self._modules  = [
            "json", 
            "math", 
            "asyncio"]
        self._tool_references = []
        
        # Default to safe builtins if none provided
        keys_to_use = builtins if builtins is not None else list(__SAFE_BUILTINS__.keys())
        for key in keys_to_use:
            if key in __SAFE_BUILTINS__:
                self._builtins[key] = __SAFE_BUILTINS__[key]

    @property
    def code(self):
        return self._code
    
    @property
    def builtins(self):
        return self._builtins

    @property
    def timeout(self):
        return self._timeout

    def add_builtins(self, **kwargs):
        """
            Adds built-in functions or variables to the sandbox environment.
            Note: Do not add modules here, use add_modules() instead.
            Args:
                **kwargs: Key-value pairs representing the built-in names and their corresponding values.
        """
        for key, value in kwargs.items():
            self._builtins[key] = value

    def add_modules(self, *module_names: str):
        """
            Adds module names to be imported in the sandbox environment.
            Modules are imported by name (string) to avoid pickle issues.
            Args:
                *module_names: Module names as strings (e.g., "numpy", "requests")
        """
        for mod_name in module_names:
            if mod_name not in self._modules:
                self._modules.append(mod_name)

    def add_tool_reference(self, 
        name: str, 
        module_path: str, 
        class_name: str = None, 
        method_name: str = None, 
        init_kwargs: dict = None
    ):
        """
            Adds a tool reference that will be resolved in the worker process.
            This allows exposing functions/methods without pickling them.
            
            Args:
                name: The name to expose in the sandbox (e.g., "search_local_documents")
                module_path: The module to import from (e.g., "baiss_sdk.tools")
                class_name: The class name if it's a method (e.g., "allTools")
                method_name: The method/function name (e.g., "search_local_documents")
                init_kwargs: kwargs to pass to class __init__ (optional)
            
            Example:
                sandbox.add_tool_reference(
                    name="search_local_documents",
                    module_path="baiss_sdk.tools",
                    class_name="allTools",
                    method_name="search_local_documents",
                    init_kwargs={"url_embedding": None}
                )
        """
        self._tool_references.append({
            "name": name,
            "module_path": module_path,
            "class_name": class_name,
            "method_name": method_name,
            "init_kwargs": init_kwargs or {}
        })

    def _sanitize_builtins(self):
        """
            Removes any modules from builtins to prevent Pickle errors.
            Returns a copy of builtins with only picklable values.
        """
        clean_builtins = {}
        for k, v in self.builtins.items():
            if isinstance(v, types.ModuleType):
                # Modules cannot be pickled, skip them
                continue 
            clean_builtins[k] = v
        return clean_builtins

    def get_function_by_name(self, name: str):
        for function in PythonExtractor(self._code).functions:
            if function["name"] == name:
                return function
        return None

    def map_arguments(self, args):
        if not args:
            args = []
        builtins = self.builtins.copy()
        argnames = []
        for argument in args:
            argname = ("a" + str(uuid.uuid4())).replace("-","")
            while (argname in builtins):
                argname = ("a" + str(uuid.uuid4())).replace("-","")
            argnames.append(argname)
            builtins[argname] = argument
        return [argnames, builtins]

    def _get_new_varname(self, argnames, function_body, builtins):
        retvar_name = ("a" + str(uuid.uuid4())).replace("-","")
        while (retvar_name in builtins) or (retvar_name in argnames) or (retvar_name in function_body):
            retvar_name = ("a" + str(uuid.uuid4())).replace("-","")
        return retvar_name

    def call_function(self, name, args, timeout: int, kwargs: dict = None):
        """
            Calls a function by name with the provided arguments in a sandboxed environment.
            Args:
                name (str): The name of the function to call.
                args (list): The arguments to pass to the function.
                kwargs (dict, optional): Keyword arguments to pass to the function.
                timeout (int): Timeout in seconds for the execution.
            return:
                {
                    "success"  : bool,
                    "status"   : 200 | 500,
                    "stdout"   : str,
                    "stderr"   : str,
                    "return"   : any,
                    "error"    : str | None
                }
        """
        function = self.get_function_by_name(name)
        if not function:
            raise ValueError(f"Function '{name}' not found in the provided code.")
        function_body = function['body']
        if not isinstance(function_body, str) or not function_body.strip():
            raise ValueError(f"Function '{name}' has no valid body.")
        [argnames, builtins] = self.map_arguments(args)
        retvar_name = self._get_new_varname(argnames, function_body, builtins)
        code = function['body'].rstrip() + "\n" + retvar_name + "=" + function['name'] + "("
        if argnames:
            for argname in argnames:
                code += argname + ", "
            code = code.strip(" ,")
        code += ")"
        
        # Sanitize builtins (remove modules)
        safe_builtins = {}
        for k, v in builtins.items():
            if not isinstance(v, types.ModuleType):
                safe_builtins[k] = v
        
        result_queue = multiprocessing.Queue()
        process = multiprocessing.Process(
            target = _worker_exec,
            args   = (code, result_queue, safe_builtins, retvar_name, self._modules, self._tool_references)
        )
        process.start()
        process.join(timeout = timeout)
        if process.is_alive():
            process.terminate()
            process.join()
            return {
                "success"  : False,
                "status"   : 500,
                "stdout"   : "",
                "stderr"   : "",
                "return"   : None,
                "error"    : f"TimeoutError: Execution timed out after {timeout} seconds."
            }
        elif process.exitcode == 0:
            if result_queue.empty():
                return {
                    "success"  : False,
                    "status"   : 500,
                    "stdout"   : "",
                    "stderr"   : "",
                    "return"   : None,
                    "error"    : "No result was returned from the execution process."
                }
            result = result_queue.get()
            return result
        return {
            "success"  : False,
            "status"   : 500,
            "stdout"   : "",
            "stderr"   : "",
            "return"   : None,
            "error"    : f"Process exited with code {process.exitcode}."
        }

    def execute(self, code: str = None, timeout: int = 30):
        """
            Executes the provided code in a sandboxed environment.
            Supports both sync and async code (use asyncio.run() in the code for async).
            Args:
                code (str, optional): The code to execute. If None, uses the initialized code.
                timeout (int): Timeout in seconds for the execution. Default is 30 seconds.
            return:
                {
                    "success"  : bool,
                    "status"   : 200 | 500,
                    "stdout"   : str,
                    "stderr"   : str,
                    "return"   : None,
                    "error"    : str | None
                }
        """
        if code is None:
            code = self._code
        
        # Sanitize builtins (remove modules)
        safe_builtins = self._sanitize_builtins()
        
        result_queue = multiprocessing.Queue()
        process = multiprocessing.Process(
            target = _worker_exec,
            args   = (code, result_queue, safe_builtins, "", self._modules, self._tool_references)
        )
        process.start()
        process.join(timeout = timeout)
        if process.is_alive():
            process.terminate()
            process.join()
            return {
                "success"  : False,
                "status"   : 500,
                "stdout"   : "",
                "stderr"   : "",
                "return"   : None,
                "error"    : f"TimeoutError: Execution timed out after {timeout} seconds."
            }
        elif process.exitcode == 0:
            if result_queue.empty():
                return {
                    "success"  : False,
                    "status"   : 500,
                    "stdout"   : "",
                    "stderr"   : "",
                    "return"   : None,
                    "error"    : "No result was returned from the execution process."
                }
            result = result_queue.get()
            return result
        return {
            "success"  : False,
            "status"   : 500,
            "stdout"   : "",
            "stderr"   : "",
            "return"   : None,
            "error"    : f"Process exited with code {process.exitcode}."
        }
