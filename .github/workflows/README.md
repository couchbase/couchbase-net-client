# Workflows

## Build and test

- **unit-tests.yml** — Builds the SDK and runs every unit test project. Reusable only, called by the two workflows below. 8 OS images, net8.0/net10.0, plus net48 on Windows.
- **build-and-test.yml** — Runs `unit-tests.yml`. Push to `master` and release branches, PRs, manual. Cancels a PR's superseded run.
- **nightly-unit-tests.yml** — Runs `unit-tests.yml` against `master`, to catch runner image and .NET release changes. Daily at 06:00 UTC, manual.
- **build-fit-performer.yml** — Builds `couchbase-fit-performer.sln` only, to catch performer build breaks early. Push to `master`, PRs, manual. Ubuntu, macOS.

## FIT

- **fit-testing-dotnet.yml** — Runs FIT presets against a performer image. Daily at 00:00 UTC against `main`, manual with preset and performer tag inputs. Calls the shared `couchbaselabs/fit-cli` workflow, one job per preset.
- **fit-testing-dotnet-release.yml** — Runs the `op-multi-release` preset group against a performer image and posts a combined summary to the #the-fit-stop Slack thread. Weekly, Sundays at 02:00 UTC against `main`, manual with preset, performer tag, and Slack thread inputs. Calls the shared `couchbaselabs/fit-cli` workflow, one job per preset. Modelled on the Java SDK's weekly release run.
- **publish-fit-performer.yml** — Publishes `ghcr.io/couchbase/dotnet-fit-performer`, tagged with the SDK version. Push of any tag, daily at 23:00 UTC for `main`, manual for any ref. Ubuntu.
- **pr-fit-performer.yml** — Publishes a performer image for the PR and comments how to run FIT with it. PRs that touch SDK or performer code. Ubuntu. Skips fork PRs.
- **prune-stale-images.yml** — Deletes performer images after 7 days, keeping `main` and release tags. Daily at 03:53 UTC, manual. Ubuntu. Same job as the other SDK repos.

## Release

- **release.yml** — Builds, tests, signs, and packs the NuGet packages, then publishes to NuGet and S3 and calls the docs workflow. Published GitHub release, or manual with a version tag. Windows for pack and sign, Ubuntu for publish.
- **apidocs.yml** — Builds the DocFX API docs and publishes them to S3. Manual, or called by `release.yml`. Ubuntu.

Manual runs of `release.yml` and `apidocs.yml` default to `publish: false`, which gives a smoke run that publishes nothing.
