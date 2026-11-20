# Contributing

Thank you for helping improve the Couchbase .NET SDK. Bug reports, fixes and new features are all welcome.

## Before you start

- Search the [NCBC issue tracker](https://jira.issues.couchbase.com/browse/NCBC) and the [release notes](https://docs.couchbase.com/dotnet-sdk/current/project-docs/sdk-release-notes.html). The bug may be known or already fixed.
- Make sure the problem is a bug in the SDK and not a usage error. The [forums](https://www.couchbase.com/forums/c/net-sdk/6) and [Discord](https://discord.com/invite/sQ5qbPZuTh) can help.
- For a new feature, check that it does not exist yet. Small, focused changes are easier to review and merge.

## Submit a pull request

Push a branch to your fork and open a pull request against `master`.

Add tests with your change, and run the unit tests before you push.

```bash
dotnet test tests/Couchbase.UnitTests
```

## Pull request title and commits

We squash each pull request into one commit, so its title becomes the commit title. Give it a short title that says what the change does, for example `Fix retry on a closed connection`. If the change needs an NCBC ticket, we create one for you.

In the description, say why the change is needed and what it changes. Keep it short.

To fix review comments, push new commits to the same branch. Do not force push.

## Code style

Make new code look like the code around it. The repository `.editorconfig` holds the formatting rules.

## Contributor License Agreement

Couchbase must have a signed Contributor License Agreement (CLA) before it can merge your change. The first time you open a pull request, a bot comments with steps to sign it.

## Code review

A Couchbase engineer reviews every pull request, and CI must pass before it merges.

### Maintainers

- [David Kelly](https://github.com/davidkelly)
- [Emilien Bevierre](https://github.com/emilienbev)
