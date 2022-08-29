# ModFinder

A tool for installing Pathfinder: Wrath of the Righteous mods and their dependencies.

### [![Download zip](https://custom-icon-badges.herokuapp.com/badge/-Download-blue?style=for-the-badge&logo=download&logoColor=white "Download zip")](https://github.com/Pathfinder-WOTR-Modding-Community/ModFinder/releases/latest/download/ModFinder.zip) Latest Release

## Features

* Browse mods hosted on Nexus and GitHub
* Detects out of date mods
* Automatically installs mods hosted on GitHub
* Detects missing dependencies, enabling one-click install (mods on GitHub) or download link (mods on Nexus)
* Uninstall mods
* And more

## For Users

## For Mod Devs

Currently ModFinder only supports mods hosted on Nexus or GitHub.

To add (or change details about) your mod:

1. Update [internal_manifest.json](https://github.com/Pathfinder-WOTR-Modding-Community/ModFinder/blob/main/ManifestUpdater/Resources/internal_manifest.json)
    * Don't include any version data or description, this is automatically updated roughly every 2 hours
    * You can submit a PR or file an issue
    
That's it! The manifest format is documented [in the code](https://github.com/Pathfinder-WOTR-Modding-Community/ModFinder/blob/main/ModFinderClient/Mod/ModManifest.cs).
 
Assumptions for GitHub:

* The first release asset is a zip file containing the mod (i.e. what a user would drag into UMM)
    * You can specify a `ReleaseFilter`, look at [MewsiferConsole](https://github.com/Pathfinder-WOTR-Modding-Community/ModFinder/blob/main/ManifestUpdater/Resources/internal_manifest.json) for an example
* Your releases are tagged with a version string in the format `1.2.3e`. Prefixes are ignored.
    * If there's a mismatch between your GitHub tag version and `Info.json` version it will think the mod is always out of date

If necessary you can host your own `ModManifest` JSON file by adding a direct download link to `ExternalManifestUrls` in [master_manifest.json](https://github.com/Pathfinder-WOTR-Modding-Community/ModFinder/blob/main/ManifestUpdater/Resources/master_manifest.json). Keep in mind, this will not be automatically updated so it is up to you to populate description and version info.

## Acknowledgements

* Barley for starting this project in the first place ([ModFinder_WOTR](https://github.com/BarleyFlour/ModFinder_WOTR)) and working with Bubbles to complete like 80% before I decided to finish it
* Bubbles for his excellent work on the UI styling and Barley for handling the GitHub action setup
* The modding community on [Discord](https://discord.com/invite/wotr), an invaluable and supportive resource for help modding.
* All the Owlcat modders who came before me, wrote documents, and open sourced their code.

## Interested in modding?

* Check out the [OwlcatModdingWiki](https://github.com/WittleWolfie/OwlcatModdingWiki/wiki).
* Join us on [Discord](https://discord.com/invite/wotr).
