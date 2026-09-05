# Test project instructions

This project uses xUnit and Moq to specify planning and step behavior. Parent instructions in `../AGENTS.md` also apply.

## Test rules

- Name tests as behavior and expected result.
- For behavior changes, run the new/changed test before production edits and confirm a relevant RED failure. A compile or harness error is not evidence of the intended behavioral defect. If the test is already GREEN, record that result and reassess the need for a production change; never manufacture RED.
- Test observable plans, API calls, execution order and resulting character state rather than private implementation details.
- Use Moq only at external/service boundaries; keep domain calculation real where practical.
- Cover bounded failure behavior for recursion, repeated actions and missing data.
- Update constructor mocks whenever DI-facing signatures change.
- Do not enable the commented circular-dependency test without first verifying that its fixture asserts the intended contract.

## Commands

From the repository root:

```text
dotnet test Artiact.Tests/Artiact.Tests.csproj --no-restore --filter FullyQualifiedName~<TestClass>
dotnet test Artiact.sln --no-restore
```

See `../docs/development.md` for the test-class map.
