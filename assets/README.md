# Package assets

`icon.png` — the NuGet package icon, wired in `Directory.Build.props`
(`PackageIcon`, Exists-conditional: pack works without it and picks it up
automatically the day it lands here). nuget.org requirements: PNG (or JPEG),
128×128 recommended, 1 MB maximum. `PackageMetadataTests` validates the file
whenever it is present.
