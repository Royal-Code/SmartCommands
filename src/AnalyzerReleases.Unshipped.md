
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
RCCMD024 | Usage | Error | WithRetryOnConcurrency requires WithWorkContext
RCCMD025 | Usage | Error | WithRetryOnConcurrency max attempts must be greater than zero
RCCMD026 | Usage | Error | A command type must declare only one command method
RCCMD027 | Usage | Error | A command type must declare only one Map attribute
RCCMD028 | Usage | Error | A Map attribute requires route pattern and endpoint name
RCCMD029 | Usage | Error | A command parameter uses a name reserved by generated code
RCCMD030 | Usage | Error | Duplicate endpoint name across mapped endpoints
RCCMD031 | Usage | Error | EditEntity route parameter cannot be resolved
RCCMD032 | Usage | Error | EditEntity route parameter is incompatible with the entity id
RCCMD033 | Usage | Error | A parameter declares conflicting binding sources
RCCMD034 | Usage | Error | AsParameters is not supported on external command parameters
RCCMD035 | Usage | Error | A route-bound parameter is absent from the route template
RCCMD036 | Usage | Error | GET and DELETE endpoints cannot infer a request body
RCCMD037 | Usage | Error | The endpoint has more than one request body source
RCCMD038 | Usage | Error | Invalid command validation method
RCCMD039 | Usage | Error | Parameter not allowed in a command validation method
RCCMD040 | Usage | Error | The same parameter name is declared with different types
RCCMD041 | Usage | Error | Invalid endpoint metadata attribute argument
RCCMD042 | Usage | Error | The same parameter name is declared with incompatible roles
