#!/usr/bin/env bash
# Pushes the validated local image to ECR under an immutable candidate tag, derives the release tag from
# the pushed digest, promotes it, and confirms the release tag resolves to the same digest.
# Writes image_tag, image_uri, image_digest, candidate_tag and digest_suffix to $GITHUB_OUTPUT.
#
# Called by the publish job in build-test-publish-images.yml, after the AWS login and ECR login steps.
#
# Environment:
#   COMPONENT                  api | web
#   ECR_REGISTRY, ECR_REPOSITORY, RELEASE_VERSION   from the earlier steps
#   EXPECTED_AWS_ACCOUNT_ID    registry id for ECR calls
#   GITHUB_SHA, GITHUB_RUN_ID, GITHUB_RUN_ATTEMPT, GITHUB_OUTPUT   set by the runner
set -euo pipefail
 
LOCAL_IMAGE="cdc-${COMPONENT}:${GITHUB_SHA}"
CANDIDATE_TAG="${COMPONENT}-sha-${GITHUB_SHA::12}-${GITHUB_RUN_ID}-${GITHUB_RUN_ATTEMPT}"
CANDIDATE_URI="${ECR_REGISTRY}/${ECR_REPOSITORY}:${CANDIDATE_TAG}"
 
docker tag "${LOCAL_IMAGE}" "${CANDIDATE_URI}"
docker push "${CANDIDATE_URI}"
 
IMAGE_DIGEST="$(aws ecr describe-images \
  --registry-id "${EXPECTED_AWS_ACCOUNT_ID}" \
  --repository-name "${ECR_REPOSITORY}" \
  --image-ids imageTag="${CANDIDATE_TAG}" \
  --query 'imageDetails[0].imageDigest' \
  --output text)"
 
[[ "${IMAGE_DIGEST}" =~ ^sha256:[0-9a-f]{64}$ ]] || {
  echo "::error::ECR returned an invalid image digest '${IMAGE_DIGEST}'."
  exit 1
}
 
DIGEST_HEX="${IMAGE_DIGEST#sha256:}"
DIGEST_SUFFIX="${DIGEST_HEX: -8}"
RELEASE_TAG="v${RELEASE_VERSION}-${DIGEST_SUFFIX}"
RELEASE_URI="${ECR_REGISTRY}/${ECR_REPOSITORY}:${RELEASE_TAG}"
 
EXISTING_DIGEST="$(aws ecr describe-images \
  --registry-id "${EXPECTED_AWS_ACCOUNT_ID}" \
  --repository-name "${ECR_REPOSITORY}" \
  --image-ids imageTag="${RELEASE_TAG}" \
  --query 'imageDetails[0].imageDigest' \
  --output text 2>/dev/null || true)"
 
if [[ -n "${EXISTING_DIGEST}" && "${EXISTING_DIGEST}" != "None" ]]; then
  if [[ "${EXISTING_DIGEST}" != "${IMAGE_DIGEST}" ]]; then
    echo "::error::Immutable tag ${RELEASE_TAG} already points to ${EXISTING_DIGEST}; expected ${IMAGE_DIGEST}."
    exit 1
  fi
  echo "${RELEASE_TAG} already points to the expected digest; continuing safely."
else
  # Promote the exact local image that produced CANDIDATE_TAG. This
  # avoids retrieving or translating the manifest before retagging.
  docker tag "${CANDIDATE_URI}" "${RELEASE_URI}"
  docker push "${RELEASE_URI}"
fi
 
RELEASE_DIGEST=""
for ATTEMPT in $(seq 1 10); do
  RELEASE_DIGEST="$(aws ecr describe-images \
    --registry-id "${EXPECTED_AWS_ACCOUNT_ID}" \
    --repository-name "${ECR_REPOSITORY}" \
    --image-ids imageTag="${RELEASE_TAG}" \
    --query 'imageDetails[0].imageDigest' \
    --output text 2>/dev/null || true)"
 
  if [[ "${RELEASE_DIGEST}" == "${IMAGE_DIGEST}" ]]; then
    break
  fi
 
  if (( ATTEMPT < 10 )); then
    sleep 2
  fi
done
 
if [[ "${RELEASE_DIGEST}" != "${IMAGE_DIGEST}" ]]; then
  echo "::error::Release tag ${RELEASE_TAG} points to '${RELEASE_DIGEST:-missing}', but validated candidate ${CANDIDATE_TAG} points to '${IMAGE_DIGEST}'."
  exit 1
fi
 
{
  echo "image_tag=${RELEASE_TAG}"
  echo "image_uri=${RELEASE_URI}"
  echo "image_digest=${IMAGE_DIGEST}"
  echo "candidate_tag=${CANDIDATE_TAG}"
  echo "digest_suffix=${DIGEST_SUFFIX}"
} >> "${GITHUB_OUTPUT}"
