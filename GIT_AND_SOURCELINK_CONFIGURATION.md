# 🔗 SharpDispatch - Git & Source Link Configuration

## ✅ Git Configuration Added

Your project is now configured for professional Git integration and Source Link support.

---

## 🔧 Configuration Details

### Source Link Settings

```xml
<!-- GitHub Source Link Support -->
<PublishRepositoryUrl>true</PublishRepositoryUrl>
<EmbedAllSources>true</EmbedAllSources>
<DebugType>embedded</DebugType>
<Deterministic>true</Deterministic>

<!-- GitHub Source Link Package -->
<PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0" PrivateAssets="All" />
```

### What Each Setting Does

| Setting | Purpose |
|---------|---------|
| `PublishRepositoryUrl` | Include repository URL in package metadata |
| `EmbedAllSources` | Embed source code in symbol package |
| `DebugType` | Use embedded debugging symbols |
| `Deterministic` | Reproducible builds (same hash every time) |
| `SourceLink.GitHub` | Automatic GitHub URL generation for sources |

---

## 🎯 Benefits

### For Developers Using Your Package

✅ **Step Into Code**
- F11 steps directly into SharpDispatch source
- Full source code visible in debugger
- No source mismatch errors

✅ **Source Link Navigation**
- Click on code to view GitHub source
- Direct links in debugger
- View exact line in repository

✅ **No File Dependencies**
- No need to have source code locally
- Works with NuGet symbol server
- Automatic source retrieval

### For Your Package

✅ **Professional Quality**
- Enterprise-grade debugging support
- Reproducible builds
- Transparent source code access

✅ **Better Discoverability**
- Repository URL published
- Source links in metadata
- Developer trust increased

---

## 🏗️ How It Works

### Build Process

```
1. Source code → Compile
              ↓
2. Generate PDB symbols with GitHub URLs
              ↓
3. Embed sources in symbol package (.snupkg)
              ↓
4. Create deterministic hash (same every time)
              ↓
5. Package ready for publication
```

### Runtime Debugging

```
Developer debugging your package:
              ↓
Visual Studio encounters code from SharpDispatch.dll
              ↓
Checks embedded source links
              ↓
Automatically downloads from GitHub
              ↓
Shows source code in debugger
              ↓
Developer can step through, set breakpoints, etc.
```

---

## 📦 Package Contents

When published, your package includes:

### .nupkg (Main Package)
- Compiled assembly (SharpDispatch.dll)
- XML documentation
- Metadata
- Logo
- README

### .snupkg (Symbol Package)
- PDB debug symbols (embedded)
- **Embedded source code** (NEW)
- **GitHub links** (NEW)
- Line number information
- Variable naming

---

## 🔐 Security & Transparency

### Source Code Access
- ✅ Sources are embedded in symbol package
- ✅ Only on NuGet symbol server
- ✅ Not in main package
- ✅ Developer choice to use

### Deterministic Builds
- ✅ Same source → Same hash
- ✅ Verifiable builds
- ✅ Transparent supply chain
- ✅ Build reproducibility

---

## 🚀 Publishing Considerations

### Before Publishing

```bash
# Clean build ensures deterministic output
dotnet clean -c Release

# Fresh pack for clean symbols
dotnet pack -c Release
```

### After Publishing

Developers will be able to:

```csharp
// In their code
var dispatcher = provider.GetRequiredService<ICommandDispatcher>();
```

Then when debugging:
- Press **F11** to step into SharpDispatch methods
- See source code directly
- GitHub URLs embedded for quick reference

---

## 📋 Verification Checklist

### Build Time

```
[ ] Source Link package referenced             ✓
[ ] Deterministic builds enabled               ✓
[ ] Debug symbols embedded                     ✓
[ ] Repository URL configured                 ✓
[ ] Git repository accessible                 ✓
```

### Package Time

```
[ ] .snupkg contains embedded sources
[ ] GitHub URLs in symbol metadata
[ ] Deterministic hash generated
[ ] Symbol package size reasonable (10-20KB)
```

### After Publishing

```
[ ] Developers can step into your code
[ ] GitHub links work in debugger
[ ] No source file dependencies needed
[ ] Symbol server finds symbols
```

---

## 🔗 GitHub Integration

### Repository Requirements

Your GitHub repository must:
- ✅ Be public (for source links to work)
- ✅ Have proper git history
- ✅ Repository URL match in .csproj

### Automatic Detection

The build system automatically:
- Detects GitHub repository
- Generates source links
- Embeds them in symbols
- Creates reproducible builds

---

## 📊 Deterministic Builds

### What is Deterministic?

```
Input:  Source Code + Build Tools
          ↓
      Deterministic Build
          ↓
Output: Exact same binary (same hash)

Important for:
• Reproducibility verification
• Supply chain transparency
• Build verification
```

### Real-World Example

```
Build 1: 2025-01-28 10:00 AM
Input:    SharpDispatch.cs
Output:   SharpDispatch.dll (hash: ABC123...)

Build 2: 2025-02-01 03:00 PM
Input:    SharpDispatch.cs (unchanged)
Output:   SharpDispatch.dll (hash: ABC123...) ← SAME!
```

---

## 🛠️ For CI/CD

### GitHub Actions Example

```yaml
jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
        with:
          fetch-depth: 0  # Full history for git info
      
      - uses: actions/setup-dotnet@v3
      
      - run: dotnet pack -c Release
      
      - run: dotnet nuget push bin/Release/*.nupkg
```

---

## 📚 Advanced Configuration

### Optional: Custom Source Link

If you use a different git provider:

```xml
<!-- For Azure DevOps -->
<PackageReference Include="Microsoft.SourceLink.AzureDevOpsGit" Version="8.0.0" PrivateAssets="All" />

<!-- For GitLab -->
<PackageReference Include="Microsoft.SourceLink.GitLab" Version="8.0.0" PrivateAssets="All" />
```

---

## ✨ Complete .csproj Configuration

Your project now has:

```xml
<PropertyGroup>
  <!-- Basic Package Info -->
  <PackageId>SharpDispatch</PackageId>
  <Version>1.0.0</Version>
  
  <!-- Metadata -->
  <Authors>MPCoreDeveloper</Authors>
  <Description>Standalone CQRS command dispatching...</Description>
  <Copyright>Copyright (c) 2025 MPCoreDeveloper...</Copyright>
  
  <!-- Distribution -->
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageIcon>SharpDispatch.jpg</PackageIcon>
  <PackageReadmeFile>NuGet.README.md</PackageReadmeFile>
  
  <!-- Searchability -->
  <PackageTags>cqrs;command-dispatch;...</PackageTags>
  
  <!-- Repository -->
  <RepositoryUrl>https://github.com/MPCoreDeveloper/SharpDispatch</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  
  <!-- Symbol Package -->
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  
  <!-- Git & Source Link (NEW) -->
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
  <EmbedAllSources>true</EmbedAllSources>
  <DebugType>embedded</DebugType>
  <Deterministic>true</Deterministic>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0" PrivateAssets="All" />
</ItemGroup>
```

---

## 🎯 Publishing Steps

### 1. Clean Build

```bash
dotnet clean -c Release
dotnet build -c Release
```

### 2. Create Packages

```bash
dotnet pack -c Release
```

Generates:
- `SharpDispatch.1.0.0.nupkg` (main)
- `SharpDispatch.1.0.0.snupkg` (symbols with sources)

### 3. Publish Both

```powershell
.\Publish-Packages.ps1 -ApiKey YOUR_API_KEY
```

### 4. Verify

Visit: `https://www.nuget.org/packages/SharpDispatch/`

Check:
- ✓ Symbol package listed
- ✓ GitHub URL visible
- ✓ Sources embedded confirmation

---

## 💡 Pro Tips

### For Maximum Quality

1. **Always use clean builds** before packaging
2. **Tag releases in Git** for traceability
3. **Keep repository public** for source links
4. **Test debugging locally** before publishing
5. **Monitor package size** (symbols should be 10-20KB)

### For Future Releases

```bash
# Tag the release
git tag v1.0.1

# Clean and pack
dotnet clean -c Release && dotnet pack -c Release

# Publish
.\Publish-Packages.ps1 -ApiKey YOUR_KEY
```

---

## 🔍 Verification Commands

### Check Deterministic Builds

```bash
# Build twice
dotnet clean -c Release && dotnet pack -c Release
dotnet clean -c Release && dotnet pack -c Release

# Compare hashes (should be identical)
Get-FileHash src/SharpDispatch/bin/Release/SharpDispatch.1.0.0.nupkg
```

### Inspect Symbol Package

```bash
# List contents of .snupkg
# (it's a ZIP file)
Expand-Archive src/SharpDispatch/bin/Release/SharpDispatch.1.0.0.snupkg -Force
```

---

## 🎉 Summary

Your project now has:

✅ **Professional Git Integration**
- GitHub source links
- Embedded sources in symbols
- Repository URL published

✅ **Deterministic Builds**
- Reproducible builds
- Same output every time
- Verifiable source

✅ **Developer Experience**
- Step into code
- Source navigation
- No file dependencies

✅ **Enterprise Ready**
- Supply chain transparency
- Build reproducibility
- Professional debugging

---

<div align="center">

### 🚀 Ready to Publish

Your SharpDispatch package is now fully configured with:
- Git integration
- Source Link support
- Deterministic builds
- Professional debugging

**Next Step:**
```powershell
.\Publish-Packages.ps1 -ApiKey YOUR_NUGET_API_KEY
```

</div>
