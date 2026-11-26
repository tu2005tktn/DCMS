## 2.2 Test Levels

Four levels are planned: Unit Test (service methods, xUnit), Integration Test (real API over HTTP on a throw-away PostgreSQL schema), System Test (end-to-end through the web UI with the real API and database) and Acceptance Test (walk-through of the main business flows by the clinic representative). The table shows which test types are performed at each level.

| Type of Tests \ Test Level | Unit Test | Integration Test | System Test | Acceptance Test |
|---|:---:|:---:|:---:|:---:|
| Functional Testing | ✓ | ✓ | ✓ | ✓ |
| Input Validation Testing | ✓ | ✓ | ✓ | |
| Background Job Testing | | ✓ | | |
| Performance Testing | | | ✓ | |
| Security Testing | | ✓ | ✓ | |
| UI Testing (End to End) | | | ✓ | ✓ (optional) |
