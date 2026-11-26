# Server validation

Run `./scripts/validate.ps1` from PowerShell for locked restore, formatting, build, tests, and coverage. Run `git config core.hooksPath .githooks` from the server repository to enable local hooks. On Unix, make the hook files executable if needed.

Use branches such as `feat/tenant-settings` and lowercase Conventional Commit subjects such as `feat(api): add tenant settings`. Open PRs against `master`. For protected-branch settings in GitHub, require pull requests, a review, and the **Build and test** and **Branch and commit conventions** checks after they first appear. Production deploys from `master` run the same build and test job before using FTPS. The production smoke test and patch-version tag follow deployment.

Run `dotnet restore MultiTenantPlatform.slnx --use-lock-file --force-evaluate` when updating NuGet dependencies and include the lockfile changes in the PR. CI audits transitive dependencies and treats high and critical advisories as errors. CI uploads coverage and requires at least 40% line and branch coverage. The initial local run measured 43.01% line coverage and 41.46% branch coverage.

## SonarQube Cloud

Import the server repository as a separate C# project in your SonarQube Cloud organization and use CI-based analysis. Confirm the server Project Information keys match [sonar-project.properties](../sonar-project.properties). The workflow reads both keys from that file. Set `SONAR_TOKEN` as a GitHub Actions secret with permission to analyze the server project. The workflow uses SonarScanner for .NET and sends Coverlet OpenCover results to the quality gate. It analyzes `master` and same-repository PRs with access to Actions secrets. Fork and Dependabot PRs run the core CI without a Sonar token. For repositories accepting fork PRs, account for that coverage when choosing required checks.

CodeQL requires GitHub code scanning to be available for the repository.
