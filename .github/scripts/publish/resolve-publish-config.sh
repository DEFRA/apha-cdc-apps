#!/usr/bin/env bash
# Validates the AWS and ECR configuration for one component and writes ecr_repository and
# release_version to $GITHUB_OUTPUT. Fails fast on any missing or malformed value.
#
# Called by the publish job in build-test-publish-images.yml.
#
# Environment:
#   COMPONENT                                api | web
#   AWS_REGION, AWS_ROLE_ARN                 target region and role
#   EXPECTED_AWS_ACCOUNT_ID, EXPECTED_AWS_REGION   guard values from repository variables
#   ECR_API_REPOSITORY, ECR_WEB_REPOSITORY   repository names from repository variables
#   VERSIONS_JSON                            JSON object, component to release version
set -euo pipefail
 
case "${COMPONENT}" in
  api)
    ECR_REPOSITORY="${ECR_API_REPOSITORY}"
    ;;
  web)
    ECR_REPOSITORY="${ECR_WEB_REPOSITORY}"
    ;;
  *)
    echo "::error::Unsupported component: ${COMPONENT}" >&2
    exit 1
    ;;
esac
 
RELEASE_VERSION="$(jq -er --arg component "${COMPONENT}" \
  '.[$component]' <<< "${VERSIONS_JSON}")"
 
[[ -n "${EXPECTED_AWS_ACCOUNT_ID}" ]] || {
  echo "::error::EXPECTED_AWS_ACCOUNT_ID is not configured." >&2
  exit 1
}
 
[[ -n "${EXPECTED_AWS_REGION}" ]] || {
  echo "::error::EXPECTED_AWS_REGION is not configured." >&2
  exit 1
}
 
[[ "${EXPECTED_AWS_ACCOUNT_ID}" =~ ^[0-9]{12}$ ]] || {
  echo "::error::EXPECTED_AWS_ACCOUNT_ID must contain exactly 12 digits." >&2
  exit 1
}
 
[[ "${AWS_REGION}" == "${EXPECTED_AWS_REGION}" ]] || {
  echo "::error::AWS_REGION does not match EXPECTED_AWS_REGION." >&2
  exit 1
}
 
[[ -n "${ECR_REPOSITORY}" ]] || {
  echo "::error::No ECR repository is configured for ${COMPONENT}." >&2
  exit 1
}
 
[[ "${RELEASE_VERSION}" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || {
  echo "::error::Invalid calculated release version '${RELEASE_VERSION}'." >&2
  exit 1
}
 
EXPECTED_ROLE_PREFIX="arn:aws:iam::${EXPECTED_AWS_ACCOUNT_ID}:role/"
[[ "${AWS_ROLE_ARN}" == "${EXPECTED_ROLE_PREFIX}"* && "${AWS_ROLE_ARN}" != "${EXPECTED_ROLE_PREFIX}" ]] || {
  echo "::error::The OIDC role ARN must reference the expected AWS account and include a role name." >&2
  exit 1
}
 
echo "ecr_repository=${ECR_REPOSITORY}" >> "${GITHUB_OUTPUT}"
echo "release_version=${RELEASE_VERSION}" >> "${GITHUB_OUTPUT}"
