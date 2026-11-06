---
sidebar_position: 2
---

# Installing

Watari installs into a Basis project as a Unity package. It builds against the Basis SDK,
Jiggle Physics and HVR Basis Comms, all of which ship with the Basis framework, so a Basis
project already has everything it needs.

A Basis project is the `Basis` folder inside the Basis framework download, opened in Unity, as
described in Basis's [avatar setup](https://docs.basisvr.org/en/docs/avatar/setup). In any other
project ALCOM reports **Some packages are not found** for `com.basis.sdk`,
`dev.hai-vr.basis.comms` and `com.gator-dragon-games.jigglephysics`. Basis's own VPM listing does
not satisfy them: its `0.0.1` is a May 2026 build that Watari does not compile against.

:::danger Do not install the VRChat SDK into a Basis project

It is not needed. VRChat components arrive as missing scripts, which is what Watari reads.

It changes layers and the physics collision matrix, which are project-wide. Basis uses layer 3
and layers 6 to 11 for its player, avatar and interaction handling, so this breaks Basis itself.

:::

Four ways to install it.

## VPM

The package is published to a VPM listing at `https://vpm.yuna0x0.com/index.json`. Add that
listing to your VPM client once, and the package and its updates appear alongside everything else
you install that way.

### ALCOM

[ALCOM](https://vrc-get.anatawa12.com/en/alcom/) is an open-source VPM client with a graphical
interface, and works with any Unity project.

**Add the listing.** This is stored in ALCOM, not in a project.

1. Open **Resources** in the sidebar, on the **Repositories** tab, and press **Add Repository**.
2. Enter `https://vpm.yuna0x0.com/index.json` and confirm. It then sits in the list.

![Adding the listing as a repository in ALCOM](/img/install-alcom-repository.webp)

**Install it into your project.**

3. Open **Projects**. If your Basis project is not listed, press the arrow beside
   **Create New Project**, choose **Add Existing Project**, and pick the project folder.
4. Press **Manage** on its row.

![Adding an existing project and opening Manage](/img/install-alcom-project.webp)

5. Find **Watari (Converter for Basis)** and press the **+** at the end of its row.

![Installing Watari from the project's package list](/img/install-alcom-package.webp)

For a particular version, pick it from the **Installed** dropdown on that row instead of pressing
**+**. Updates later appear as a newer number under **Latest**.

### vrc-get

[vrc-get](https://github.com/vrc-get/vrc-get) is the command line client ALCOM is built on, so
the two share their repository list:

```sh
vrc-get repo add https://vpm.yuna0x0.com/index.json
vrc-get install com.yuna0x0.basis.convert
```

### VPM CLI

VRChat's own [VPM CLI](https://vcc.docs.vrchat.com/vpm/cli/) reads the same listing:

```sh
vpm add repo https://vpm.yuna0x0.com/index.json
vpm add package com.yuna0x0.basis.convert
```

## OpenUPM

For projects managed with plain Unity Package Manager rather than a VPM client.

With the [OpenUPM CLI](https://openupm.com/docs/getting-started-cli.html):

```sh
openupm add com.yuna0x0.basis.convert
```

Or add the scoped registry by hand, in **Edit > Project Settings > Package Manager**:

- Name: `OpenUPM`
- URL: `https://package.openupm.com`
- Scope: `com.yuna0x0.basis.convert`

Then add `com.yuna0x0.basis.convert` in **Window > Package Manager > + > Install package by
name**.

## Git URL

No extra tooling, and useful for trying an unreleased version.

**Window > Package Manager > + > Install package from git URL**:

```
https://github.com/yuna0x0/watari-basis.git?path=/Packages/com.yuna0x0.basis.convert
```

Add `#v{{VERSION}}` to the end to pin a version. Packages added this way are updated by removing and
re-adding them, or by changing the version at the end of the URL.

{/*
  IMAGE PLACEHOLDER: Unity's Package Manager with the git URL field filled in.
  Save as docs/static/img/install-package-manager.webp, then replace this comment with:
  ![Adding the package by git URL](/img/install-package-manager.webp)
*/}

## Manual

Every release also has a `.unitypackage` attached, for projects that do not use a package manager
at all.

Download it from the
[releases page](https://github.com/yuna0x0/watari-basis/releases) and drag it into your project.
It restores to `Packages/com.yuna0x0.basis.convert`, so Unity treats it as an embedded package.
Updating means deleting that folder first, then importing the new one.
