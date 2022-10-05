# Contributing

>Please read the instructions carefully (particularly the **Code Review** section) in order to ensure your contributions are properly submitted to our system.

In addition to filing bugs, you may contribute to the SDK by submitting patches to <https://review.couchbase.org> which uses **Gerrit** as our code review system.

Pull requests will not be ignored, however our engineers will submit your approved patches to Gerrit before re-basing. It is therefore recommended to submit your changes directly to our code review system.

## Branches & Tags

Patches must be made in a new branch named with the corresponding issue code and its title (i.e.  `NCBC-####: Fixing KV Unit test`). Once submitted to Gerrit, tested and approved by our engineers, the `master` branch will be re-based on the new branch.

**Tags** (i.e. 3.3.5) indicate the release version of the SDK, and are annotated inside the repository.


## Guidelines

If you wish to contribute a new feature or a bug fix to the library, please follow the code guidelines to help ensure your changes get merged upstream.

### Code Formatting

For any code change, ensure the new code you write looks similar to the code surrounding it. We have no strict code styling policies, but we do request that your code complement the existing style & logic.

### For New Features

Ensure the feature you are adding does not already exist, and think of how this feature may be useful for other users. Try to keep efficiency and ease-of-use in mind. In general, less intrusive changes are more likely to be accepted.

### For Fixing Bugs

Ensure the fix you are providing is actually from a bug and not a usage error, and that it has not been fixed in a more recent version of the SDK. Please read the [Release Notes](https://docs.couchbase.com/dotnet-sdk/current/project-docs/sdk-release-notes.html) as well as the [Issue Tracker](https://issues.couchbase.com/browse) to see a list of open and resolved issues.

## Code Review

### Signing up to Gerrit

Everything that is merged into the SDK goes through a code review process. This is done via [Gerrit](https://review.couchbase.org).

1. Sign up for a Gerrit account at https://review.couchbase.org by clicking on _Register_ at the top right.
2. Go to you account settings and agree to the CLA (Contributor License Agreement) by clicking on _Agreements_ on the left.
3. Add your GitHub email address in _Email addresses_. Note that this email must match the one associated with your commits (usually your GitHub username).
4. Add your **Public** SSH key under _SSH Keys_.
5. Test that your SSH connection is properly set-up by executing:
```
$ ssh review.couchbase.org
```
If successful, you should see the following message:
```
  ****    Welcome to Gerrit Code Review    ****

  Hi FIRSTNAME LASTNAME, you have successfully connected over SSH.

  Unfortunately, interactive shells are disabled.
  To clone a hosted Git repository, use:

  git clone ssh://${USERNAME}@review.couchbase.org:29418/REPOSITORY_NAME.git

 Connection to review.couchbase.org closed.
```

If you do not have an SSH Key setup on your machine or do not know where it is located, you can go to [GitHub's Tutorial](https://docs.github.com/en/authentication/connecting-to-github-with-ssh/generating-a-new-ssh-key-and-adding-it-to-the-ssh-agent) and follow the steps for MacOS, Windows or Linux. Make sure to only follow the steps required to generate and locate your **Public Key** to add it to Gerrit.

### Setting up your fork with Gerrit
> Note: Replace ${USERNAME} with your GitHub username (i.e ssh://JohnDoe@review.couchbase.org)
1. Clone the repository

```
$ git clone https://github.com/couchbase/couchbase-net-client.git
```

2. Create a connection to the remote Gerrit repository

```
$ git remote add gerrit ssh://${USERNAME}@review.couchbase.org:29418/couchbase-net-client.git
```

3. Securely copy Gerrit's `commit-msg` hook to your local Git hooks. This will generate a `Change-ID` for your commits, which allows Gerrit to group together different revisions of the same patch.

```
$ scp -P 29418 ${USERNAME}@review.couchbase.org:hooks/commit-msg .git/hooks
```

4. Grant execution permission to the owner of the file (you). (Note: Use `chmod +x` to grant every user permission)

```
$ chmod u+x .git/hooks/commit-msg
```

#### Pushing a changeset

Make sure that you push your changes using no more than exactly **one commit**. Further changes should be amended to your initial commit using `git commit --amend`.

Use the following command to push your changes to Gerrit:

```
$ git push gerrit HEAD:refs/for/master
```
>Tip: Exit VIM using `ESC -> :wq -> RETURN`
### Common problems
You may encounter some errors when pushing. The most common
are:

* `You are not authorized to push to this repository`. You will get this if your account has not yet been approved.  Feel free to ask in our [Discord](https://discord.gg/smsZJphg) or in the forums for help if blocked.
* `(prohibited by Gerrit: not permitted: create)` Ensure that your are not globally signing your commits with a different key, such as a GPG Key set for another repository. If so, remove signing using `git config commit.gpgsign false`.
* `Missing Change-Id`. You need to install the `commit-msg` hook as described above.  Note that even once you do this, you will need to ensure that any prior commits already have this header - this may be done by doing an interactive rebase (i.e. `git rebase -i origin/master` and selecting `reword` for all the commits, which will automatically fill-in the `Change-ID`).


Once you've pushed your changeset, you can add people to review. Currently these are:

* **Jeffry Morris**
* **Richard Ponton**