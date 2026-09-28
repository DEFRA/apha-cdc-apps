Build, Test and Publish Workflow
This document explains the component-aware release process implemented by
`.github/workflows/build_test_publish.yml`.
The workflow builds, tests and publishes the API and Web container images. Each
component has its own semantic version so that an API-only change does not
increase the Web version, and a Web-only change does not increase the API
version.
Components


Component
Source paths
Git tag pattern
ECR tag pattern
API
`src/CDC.Api/`, `tests/CDC.Api.Tests/`
`api-vX.Y.Z`
`vX.Y.Z-<last-8-digest>`
Web
`src/CDC.Web/`, `tests/CDC.Web.Tests/`
`web-vX.Y.Z`
`vX.Y.Z-<last-8-digest>`
API and Web images must be stored in separate ECR repositories because their
semantic versions can be different.

What changed

The previous workflow used one release type and one version for every changed
component. 

The updated workflow provides:
Independent API and Web semantic versions.
Component-specific PR release labels.
Validation that PR labels match the components changed.
Independent manual release selections for API and Web.
Component Git tags such as `api-v1.3.0` and `web-v2.4.8`.
Immutable ECR tags containing the last eight characters of the image digest.
Idempotent rerun handling for existing component Git tags.
Atomic creation of multiple Git tags when both components are released.
Developer PR process
Create a feature branch.
Make and test the required changes.
Push the branch and create a pull request to `main`.
Determine which application components changed.
Apply exactly one release label for every changed component.
Do not add a release label for an unchanged component.
Wait for `Validate component release labels` and the other required checks.
Obtain the required review approvals.
Merge the PR only after every required check passes.
Do not create Git tags or ECR tags manually. They are created by the workflow
after the PR is merged and image publication succeeds.
Release labels
The following labels must exist in the GitHub repository:


Label
Use when
`release:api:patch`
API backward-compatible bug fix
`release:api:minor`
API backward-compatible feature
`release:api:major`
API breaking change
`release:web:patch`
Web backward-compatible bug fix
`release:web:minor`
Web backward-compatible feature
`release:web:major`
Web breaking change
Generic labels such as `release:patch`, `release:minor` and `release:major` are
not supported.
Required labels by changed component


Changed components
Required labels
API only
Exactly one `release:api:` label
Web only
Exactly one `release:web:` label
API and Web
One `release:api:` and one `release:web:` label
No application component
No component release label
Semantic-version rules


Increment
Example
Meaning
Patch
`1.2.3` to `1.2.4`
Backward-compatible defect correction
Minor
`1.2.3` to `1.3.0`
Backward-compatible functionality
Major
`1.2.3` to `2.0.0`
Breaking or incompatible change
For the first `1.0.0` release of a component, use its `major` label. If there is
no existing component tag, the calculation starts from `0.0.0`.
PR examples
API patch only
Changed files:
```text
src/CDC.Api/Controllers/CustomerController.cs
tests/CDC.Api.Tests/CustomerControllerTests.cs
```
Required label:
```text
release:api:patch
```
Result when the current API version is `1.2.3`:
```text
Git tag: api-v1.2.4
ECR tag: v1.2.4-<last-8-digest>
```
The Web version is not changed.
Web minor release only
Required label:
```text
release:web:minor
```
Result when the current Web version is `2.4.8`:
```text
Git tag: web-v2.5.0
ECR tag: v2.5.0-<last-8-digest>
```
The API version is not changed.
API and Web in the same PR
Required labels:
```text
release:api:patch
release:web:minor
```
The workflow calculates and publishes the versions independently.
Shared-file behaviour
Changes to shared build files currently mark both API and Web as changed. These
include:
```text
CDC.slnx
Directory.Build.*
Directory.Packages.props
NuGet.config
global.json
.github/actions/strip-nuget-cache-mount/**
.github/workflows/build_test_publish.yml
```
Therefore, under the current configuration, a PR that changes only
`build_test_publish.yml` requires one API release label and one Web release
label. For a pipeline-only correction, use the two patch labels unless the
workflow path is removed from both application path filters.
If the team decides that pipeline-only changes must not publish application
images, remove this path from the API and Web filters:
```yaml
'.github/workflows/build_test_publish.yml'
```
After that change, a pipeline-only PR requires no component release label and
does not publish API or Web images.
Automated workflow sequence
Pull request
`Detect affected components` identifies API and Web changes.
`Validate component release labels` confirms that labels match the changes.
`Format, build and test solution` validates the .NET solution.
`SonarCloud analysis` runs when `SONAR_ENABLED` is `true`.
Each affected Windows container is built and validated.
No production image or Git tag is created during the PR run.
After merge to main
The workflow identifies the merged PR associated with the commit.
It retrieves the PR labels using the GitHub API.
It calculates the next version independently for each changed component.
It reuses an existing component tag when rerunning the same release commit.
It downloads the validated container-image artifact.
It assumes the AWS publishing role through GitHub OIDC.
It verifies the expected AWS account and Region.
It pushes a unique candidate image to the component ECR repository.
It reads the full SHA-256 image digest from ECR.
It creates the immutable release tag `vX.Y.Z-<last-8-digest>`.
It verifies that the release tag points to the expected digest.
It creates the component Git tag after the publication gate passes.
When both components are released, the Git tags are pushed atomically.
Manual release
Manual releases are available through Actions > Build, Test and Publish CDC
Images > Run workflow.
Select `main` and choose an increment independently for each component:


Input value
Behaviour
`none`
Do not release the component
`patch`
Increase the patch version
`minor`
Increase the minor version and reset patch to zero
`major`
Increase the major version and reset minor and patch to zero
At least one component must have a value other than `none`. Manual production
releases from branches other than `main` are rejected.

GitHub repository setup
Create the six labels
Open Repository > Issues > Labels > New label and create all labels listed
in the Release labels section.
Required status checks
Configure the `main` branch ruleset to require at least:
```text
Validate component release labels
Format, build and test solution
CI and publication gate
```
Also require SonarCloud when it is enabled for the repository.

Workflow permissions
The final release job requires permission to create component Git tags. Ensure
the repository and tag rulesets allow this workflow identity to create:
```text
api-v*
web-v*
```
Do not run a separate tag-on-merge workflow because the component tags are
created by `finalise-release`.
Required GitHub configuration
The repository or `ecr-production` environment must provide the variables and
secrets referenced by the workflow.
Variables
```text
EXPECTED_AWS_ACCOUNT_ID
EXPECTED_AWS_REGION
ECR_API_REPOSITORY
ECR_WEB_REPOSITORY
SONAR_ENABLED
SONAR_PROJECT_KEY
SONAR_ORGANIZATION
```
Environment secrets
```text
AWS_ENV_REGION
AWS_ENV_ACCOUNT
AWS_ENV_OIDC_ROLE
SONAR_TOKEN
```
`SONAR_TOKEN` is required only when SonarCloud is enabled.
Published identifiers
The workflow creates two different identifiers for different purposes:


Identifier
Example
Purpose
Component Git tag
`api-v1.3.0`
Records the component semantic version in Git
Immutable ECR tag
`v1.3.0-a1b2c3d4`
Identifies the published container image
Full ECR digest
`sha256:...`
Immutable deployment identity
Deployment automation should resolve the approved ECR tag and deploy the full
SHA-256 image digest. A mutable tag such as `latest` must not be used for
production deployment.
Common validation failures
Changed API has no API label
```text
Changed api requires exactly one release:api:patch,
release:api:minor or release:api:major label.
```
Add exactly one appropriate API label to the PR.
Multiple labels for one component
Remove the incorrect label so that the component has only one increment.
Incorrect:
```text
release:api:patch
release:api:minor
```
Correct:
```text
release:api:minor
```
Label exists for an unchanged component
Remove the label belonging to the component that did not change.
Generic release label used
Replace `release:patch`, `release:minor` or `release:major` with the appropriate
component-specific label.
Production publication did not originate from a merged PR
Application changes must normally reach `main` through an approved pull
request. Confirm that the branch rules prevent direct pushes to `main`.
Git tag creation is denied
Check the workflow `contents: write` permission and the tag rulesets covering
`api-v` and `web-v`.
ECR tag already points to another digest
Do not overwrite the tag. Investigate the previous publication, verify the
source commit and use a new semantic version when appropriate.
Ownership


Responsibility
Suggested owner
Select component release increment
PR author and reviewer
Apply or correct PR labels
PR author, reviewer or repository triage role
Approve application changes
Component code owners
Maintain workflow and repository controls
DevOps/platform team
Maintain AWS OIDC role and ECR permissions
Cloud/platform team
Select an approved image for deployment
Release/deployment owner
Quick reference
```text
API bug fix -> release:api:patch
API feature -> release:api:minor
API breaking -> release:api:major
Web bug fix -> release:web:patch
Web feature -> release:web:minor
Web breaking -> release:web:major