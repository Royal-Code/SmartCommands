
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
RCCMD043 | Usage | Error | WithTransaction requires a unit of work
RCCMD044 | Usage | Error | The endpoint name must not be empty or whitespace
RCCMD045 | Usage | Error | The MapGroup route prefix cannot derive a valid group class name
RCCMD046 | Usage | Error | Distinct group route prefixes generate the same group class
RCCMD047 | Usage | Error | Two endpoints in the same group generate the same handler method
RCCMD048 | Usage | Error | The property cannot be used in the endpoint response
RCCMD049 | Usage | Error | MapIdResultValue and MapResponseValues cannot be combined
RCCMD050 | Usage | Error | Invalid MapCreatedRoute pattern (DF17 named placeholders)
RCCMD051 | Usage | Error | Invalid endpoint filter type for WithEndpointFilter
RCCMD052 | Usage | Error | Invalid use of WithResultStatus
RCCMD053 | Usage | Error | The explicit result status conflicts with a response mapping
