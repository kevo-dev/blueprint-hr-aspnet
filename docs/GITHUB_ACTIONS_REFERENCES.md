# GitHub Actions reference notes

These official GitHub sources were consulted while designing the repository workflows:

1. **Workflow syntax:** https://docs.github.com/actions/using-workflows/workflow-syntax-for-github-actions
   - Workflow YAML files live under `.github/workflows`.
   - `on` events can be filtered by branches, tags, paths, and manual dispatch.
   - Workflow-level and job-level permissions should be set explicitly.

2. **Deployment environments:** https://docs.github.com/actions/deployment/targeting-different-environments/using-environments-for-deployment
   - Jobs can reference named environments.
   - Environment protection rules, required reviewers, branch/tag restrictions, and environment secrets are configured in repository settings.
   - Environment secrets are available only after protection rules pass.
   - Availability of environment protection features depends on repository/account plan, especially for private repositories.

3. **Publishing Docker images:** https://docs.github.com/actions/guides/publishing-docker-images
   - GitHub's documented pattern uses `docker/login-action`, `docker/metadata-action`, and `docker/build-push-action`.
   - GHCR publishing requires package write permission and can use the workflow `GITHUB_TOKEN`.
   - GitHub documents image provenance attestations for published images.

4. **Secure use:** https://docs.github.com/en/actions/reference/security/secure-use
   - Use least-privilege token permissions and keep sensitive values in secrets.
   - Avoid checking out untrusted pull request code in privileged workflows.
   - Prefer environment variables for dynamic shell values rather than interpolating untrusted content directly into shell scripts.
   - GitHub recommends reviewing third-party actions and pinning them to full commit SHAs when immutable action references are required.

5. **Container registry:** https://docs.github.com/packages/working-with-a-github-packages-registry/working-with-the-container-registry
   - GHCR supports granular package permissions and workflow authentication using `GITHUB_TOKEN`.
