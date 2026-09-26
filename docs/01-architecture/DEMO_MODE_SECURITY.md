# Demo Mode Security Boundary

Runtime modes have separate purposes:

| Mode | Data | Entry | Side effects |
|---|---|---|---|
| Development | local fictitious data | developer workflow | local test adapters |
| Demo | dedicated synthetic tenant | rate-limited `Launch Live Demo` | sandboxed only |
| Production | customer data | normal authenticated users | configured production providers |

Demo is not Production with Development seeds enabled. `ProductionConfigurationValidator` remains strict. Demo has explicit configuration validation, synthetic-only startup/reset, no customer data, no reusable browser credentials, no real outbound e-mail/webhook without an explicit sandbox, and prominent `SIMULATED DEMO` labelling.

The launch endpoint issues a server-side, short-lived, scoped DemoOperator HttpOnly session. It uses existing CSRF/session controls, bounded lifetime, least privilege, and server-side rate limiting. The browser never receives a seeded administrator password. Demo controls are authorized server-side, visible only in Demo mode, and cannot cross visitor or tenant boundaries.

Release validation includes session expiry, denied sensitive administration, reset concurrency, launch limits, side-effect sandboxing, and cross-tenant denial.
