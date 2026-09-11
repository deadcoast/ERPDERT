#!/usr/bin/env node
// Benign test-owned package runtime. DERT must never launch it; only the explicit
// fresh-PowerShell before/after proof invokes this fixture.
console.log(JSON.stringify({ fixture: "DERT_NPM_DOCTOR_CONFLICT_OK", entrypoint: __filename }));
