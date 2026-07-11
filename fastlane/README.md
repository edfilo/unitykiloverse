fastlane documentation
----

# Installation

Make sure you have the latest version of the Xcode command line tools installed:

```sh
xcode-select --install
```

For _fastlane_ installation instructions, see [Installing _fastlane_](https://docs.fastlane.tools/#installing-fastlane)

# Available Actions

## iOS

### ios signing

```sh
[bundle exec] fastlane ios signing
```

Create/download App Store signing certificate and profile for K1L0.

### ios beta

```sh
[bundle exec] fastlane ios beta
```

Archive Release from existing Unity iOS export and upload to TestFlight.

### ios submit_beta

```sh
[bundle exec] fastlane ios submit_beta
```

Submit the already-uploaded K1L0 build to external TestFlight review.

### ios upload

```sh
[bundle exec] fastlane ios upload
```

Upload an already-built IPA to TestFlight (skip archive).

----

This README.md is auto-generated and will be re-generated every time [_fastlane_](https://fastlane.tools) is run.

More information about _fastlane_ can be found on [fastlane.tools](https://fastlane.tools).

The documentation of _fastlane_ can be found on [docs.fastlane.tools](https://docs.fastlane.tools).
