# 📦 SharpDispatch Package Publishing Guide

## ✅ Packages Generated Successfully

Both packages have been created and are ready for publication:

### Package Files

| Package | Type | Size | Location |
|---------|------|------|----------|
| **SharpDispatch.1.0.0.nupkg** | NuGet Package | 81.47 KB | `src/SharpDispatch/bin/Release/` |
| **SharpDispatch.1.0.0.snupkg** | Symbol Package | 9.98 KB | `src/SharpDispatch/bin/Release/` |

---

## 🎯 What Each Package Contains

### NuGet Package (.nupkg)
- ✓ Compiled assembly (SharpDispatch.dll)
- ✓ NuGet README (NuGet.README.md)
- ✓ Logo (SharpDispatch.jpg)
- ✓ License (MIT)
- ✓ XML documentation
- ✓ Package metadata

### Symbol Package (.snupkg)
- ✓ PDB debug symbols
- ✓ Source code links
- ✓ Allows developers to debug into your library
- ✓ Automatically matched with .nupkg

---

## 🚀 Publishing to NuGet.org

### Prerequisites
1. NuGet.org account
2. API key from https://www.nuget.org/account/ApiKeys
3. Both packages in Release folder

### Step 1: Verify Packages Exist

```powershell
Get-ChildItem -Path "src/SharpDispatch/bin/Release/" -Filter "SharpDispatch.1.0.0.*"
```

**Expected output:**
```
SharpDispatch.1.0.0.nupkg
SharpDispatch.1.0.0.snupkg
```

### Step 2: Push Packages to NuGet

```bash
# Push the main package
dotnet nuget push src/SharpDispatch/bin/Release/SharpDispatch.1.0.0.nupkg \
  --api-key YOUR_NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json

# Push the symbol package
dotnet nuget push src/SharpDispatch/bin/Release/SharpDispatch.1.0.0.snupkg \
  --api-key YOUR_NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

Or using a single command for both:

```bash
# Alternatively, dotnet pack automatically tries to push both
dotnet nuget push src/SharpDispatch/bin/Release/SharpDispatch.*.nupkg \
  --api-key YOUR_NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

### Step 3: Verify on NuGet.org

After pushing, verify at:
```
https://www.nuget.org/packages/SharpDispatch/
```

Check for:
- ✓ Package displays correctly
- ✓ Logo appears
- ✓ README shows
- ✓ Version 1.0.0 listed
- ✓ Symbol package available

---

## 🔒 Security: Managing Your API Key

### DO NOT commit API key to git!

Instead, use one of these methods:

#### Method 1: Command Line (Temporary)
```powershell
$apiKey = "your_actual_api_key_here"
dotnet nuget push ... --api-key $apiKey ...
```

#### Method 2: Global Configuration
```bash
# Save locally (not in version control)
dotnet nuget add source https://api.nuget.org/v3/index.json \
  --name nuget.org \
  --username __UNUSED__ \
  --password YOUR_API_KEY \
  --store-password-in-clear-text
```

Then use:
```bash
dotnet nuget push src/SharpDispatch/bin/Release/SharpDispatch.1.0.0.nupkg \
  --source nuget.org
```

#### Method 3: Environment Variable
```powershell
$env:NUGET_API_KEY = "your_api_key_here"
dotnet nuget push ... --api-key $env:NUGET_API_KEY ...
```

---

## 📋 Package Configuration Reference

Your `.csproj` has been updated with:

```xml
<!-- Symbol Package Generation -->
<IncludeSymbols>true</IncludeSymbols>
<SymbolPackageFormat>snupkg</SymbolPackageFormat>
<EmbedUntrackedSources>true</EmbedUntrackedSources>
<ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
```

### What Each Setting Does

| Setting | Purpose |
|---------|---------|
| `IncludeSymbols` | Generate symbol package (.snupkg) |
| `SymbolPackageFormat` | Use modern .snupkg format (not .symbols.nupkg) |
| `EmbedUntrackedSources` | Include source files for debugging |
| `ContinuousIntegrationBuild` | Optimize for CI/CD builds |

---

## 📦 For Next Releases

When you update the version:

1. Update version in `.csproj`:
   ```xml
   <Version>1.0.1</Version>
   ```

2. Build and pack:
   ```bash
   dotnet pack -c Release
   ```

3. Both packages regenerate automatically:
   ```
   SharpDispatch.1.0.1.nupkg
   SharpDispatch.1.0.1.snupkg
   ```

---

## ✨ What Developers Get

### When Using Your Package

```bash
dotnet add package SharpDispatch
```

Developers receive:
- ✓ Full library functionality
- ✓ XML documentation
- ✓ Logo in package listing
- ✓ Direct link to GitHub

### When Debugging

With symbol package published:
- ✓ F10/F11 step into SharpDispatch code
- ✓ View source code while debugging
- ✓ Set breakpoints in library code
- ✓ Full debugging experience

---

## 🎯 Publishing Checklist

Before publishing:

```
[ ] Version number correct in .csproj
[ ] CHANGELOG updated (if applicable)
[ ] Both .nupkg and .snupkg exist
[ ] Local test: dotnet add package SharpDispatch (local)
[ ] API key ready and secure
[ ] README.md current
[ ] Logo file included
[ ] License file included
```

After publishing:

```
[ ] Verify on NuGet.org within 5-10 minutes
[ ] Test: dotnet add package SharpDispatch
[ ] Verify logo displays
[ ] Verify symbol package available
[ ] Create GitHub release tag
[ ] Announce on social media
```

---

## 🔗 Useful Links

- **NuGet Package**: https://www.nuget.org/packages/SharpDispatch/
- **NuGet API Keys**: https://www.nuget.org/account/ApiKeys
- **GitHub Repository**: https://github.com/MPCoreDeveloper/SharpDispatch
- **Symbol Server**: https://www.nuget.org/api/v2/symbol/

---

## 📞 Troubleshooting

### Package Upload Fails

**Problem:** "401 Unauthorized"
- **Solution:** Check API key is valid and has publish rights

**Problem:** "The feed rejected the package"
- **Solution:** Check version not already published

**Problem:** Symbol package not uploading
- **Solution:** Make sure both .nupkg and .snupkg exist in Release folder

### Package Not Appearing

**Problem:** Package takes 5-10 minutes to appear
- **Solution:** Wait and refresh NuGet.org page

**Problem:** Old version still showing
- **Solution:** NuGet.org caches - try incognito/private window

---

## 🎉 Success!

Your packages are ready for publication to NuGet.org!

Both the main package and symbol package are generated and ready for seamless debugging experience for your users.

**Next Step:** Follow the "Publishing to NuGet.org" section above to publish your packages.
