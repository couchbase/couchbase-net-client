# Workflows

## Build and test

- **build-and-test.yml** — Builds the SDK and runs unit tests. Push to `master`, PRs, manual. Ubuntu x64/arm64, Windows, macOS.
- **nightly-unit-tests.yml** — Same unit tests across the full OS and framework matrix. Daily at 06:00 UTC, manual. 8 OS images, net8.0/net10.0, plus net48 on Windows.
- **build-fit-performer.yml** — Builds `couchbase-fit-performer.sln` only, to catch performer build breaks early. Push to `master`, PRs, manual. Ubuntu, macOS.

## FIT

- **fit-testing-dotnet.yml** — Runs FIT presets against a performer image. Daily at 00:00 UTC against `main`, manual with preset and performer tag inputs. Calls the shared `couchbaselabs/fit-cli` workflow, one job per preset.
- **publish-fit-performer.yml** — Publishes `ghcr.io/couchbase/dotnet-fit-performer`, tagged with the SDK version. Push of any tag, daily at 23:00 UTC for `main`, manual for any ref. Ubuntu.
- **pr-fit-performer.yml** — Publishes a performer image for the PR and comments how to run FIT with it. PRs that touch SDK or performer code. Ubuntu. Skips fork PRs.
- **prune-stale-images.yml** — Deletes performer images after 7 days, keeping `main` and release tags. Daily at 03:53 UTC, manual. Ubuntu. Same job as the other SDK repos.

## Release

- **release.yml** — Builds, tests, signs, and packs the NuGet packages, then publishes to NuGet and S3 and calls the docs workflow. Published GitHub release, or manual with a version tag. Windows for pack and sign, Ubuntu for publish.
- **apidocs.yml** — Builds the DocFX API docs and publishes them to S3. Manual, or called by `release.yml`. Ubuntu.

Manual runs of `release.yml` and `apidocs.yml` default to `publish: false`, which gives a smoke run that publishes nothing.
