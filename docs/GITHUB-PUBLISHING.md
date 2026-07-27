# Publishing this project on GitHub

This guide is written for a first public upload.

## 1. Decide the repository contents

For this project, the cleanest public repository would include:

- the source code in `src/`
- the documentation files
- helper scripts that are actually useful to reproduce the build or tests

It is better to leave out:

- local caches
- build output
- temporary logs
- downloaded third-party packages
- signed third-party binaries unless redistribution is clearly allowed

The `.gitignore` in the repository already prepares most of this.

## 2. License

This project now uses the `MIT` license.

That means:

- anyone can use, study, modify and redistribute the code
- the copyright notice and license text must stay with the software
- the project is provided without warranty

## 3. Create the repository on GitHub

On GitHub:

1. Click `New repository`
2. Choose a name such as `FanControl.NvidiaThermals`
3. Keep it empty for the first upload:
   - do not add README
   - do not add `.gitignore`
   - do not add a license there
4. Create the repository

## 4. Connect the local folder to GitHub

From the project folder, the usual commands are:

```powershell
git remote add origin https://github.com/GDuqueB/FanControl.NvidiaThermals.git
git add .
git commit -m "Initial public version"
git branch -M main
git push -u origin main
```

If the repository already has a remote, we should inspect it first before changing anything.

## 5. Recommended first public version

For a clean first version, I would recommend publishing:

- plugin source
- README
- publishing notes
- no bundled `Nvidia.bin`
- no bundled `PawnIOLib.dll`

If you want to offer a ready-to-use package later, the best place is usually a GitHub Release rather than committing binaries into the repository itself.

## 6. Suggested next step

The next sensible step is to prepare the first public commit:

1. review which folders should stay out of the first commit
2. stage the files we do want to publish
3. create the initial commit
4. push to GitHub
