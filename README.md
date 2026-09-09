# Pongo Video Cutter

**Free Open Source Lossless Video Cutting software for Windows.**

Pongo Video Cutter lets you cut and merge video files **without re-encoding**, so the output is byte-for-byte identical to the source in the cut regions. It wraps [FFmpeg](https://ffmpeg.org/) for the actual cutting/merging and [DirectShowLib-2005](https://sourceforge.net/projects/directshownet/) for the in-app preview player.

![Pongo Video Cutter](Resources/animated.gif)

- **Project home:** <https://pongo.my.id>
- **Developer:** [Ari Sohandri Putra](https://github.com/arisohandriputra)
- **License:** MIT (see `LICENSE` or <https://pongo.my.id/license.htm>)
- **Platform:** Windows 7 SP1 or newer (32-bit `x86` build)
- **Runtime:** .NET Framework 2.0 (ships with Windows 7+)

---

## Features

- **Lossless cuts** — Pongo uses FFmpeg's `-c copy -avoid_negative_ts make_zero` mode so the source stream is never re-encoded.
- **Multi-segment timeline** — mark as many in/out points as you want on a single video, then export them either as separate files or as a single merged file.
- **Custom zoomable timeline** — the owner-drawn trackbar supports click-to-seek, drag-to-move, scroll-wheel zoom, and per-segment colour selection.
- **J / K / L shuttle playback** — J = reverse, K = pause, L = forward (with double / triple press for 2x / 4x speed), just like the pros.
- **Per-segment captions / notes** — attach a free-text note to each segment so you can remember *why* you marked it.
- **Project save / load** — save your cut list to a `.pvc` project file and resume later.
- **Batch queue** — queue up multiple input videos and process them all in one click.
- **Recent files list** — quick access to the last 10 files you opened.
- **Drag-and-drop** — drop a video anywhere on the window to open it.
- **FFmpeg auto-detect** — looks for `ffmpeg.exe` / `ffprobe.exe` next to the app, or lets you point at your own copy via *Settings → FFmpeg Path*.

---

## Screenshots
<img width="1175" height="700" alt="image" src="https://github.com/user-attachments/assets/7cadeb72-161b-4045-8c92-e6c2af635dcd" />
<img width="820" height="478" alt="image" src="https://github.com/user-attachments/assets/db6f9036-9887-49aa-b4cc-c913288f147c" />
<img width="627" height="185" alt="image" src="https://github.com/user-attachments/assets/e7a97f3b-6550-41b9-b991-5734f595386b" />

---

## Project structure

```
Pongo Video Cutter/
├── Pongo Video Cutter.sln            # Visual Studio solution
├── Pongo Video Cutter.vbproj         # Project file (.NET Framework 2.0, VB.NET)
├── App.config                        # .NET runtime config
├── ApplicationEvents.vb              # My.Application startup / shutdown events
├── Form1.vb                          # Main window (all business logic)
├── Form1.Designer.vb                 # Main window layout (auto-generated)
├── frmAbout.vb                       # About dialog (credits + third-party list)
├── frmAbout.Designer.vb              # About dialog layout
├── frmCaption.vb                     # Segment caption editor
├── frmCaption.Designer.vb            # Segment caption editor layout
├── frmSplash.vb                      # Splash screen
├── frmSplash.Designer.vb             # Splash screen layout
├── FFmpeg Path.vb                    # FFmpeg / FFprobe path picker
├── FFmpeg Path.Designer.vb           # Path picker layout
├── CustomTrackBar.vb                 # Owner-drawn multi-segment timeline
├── VideoPlayer.vb                    # DirectShow-based video preview
├── ModernListBox.vb                  # Styled owner-drawn listbox for segments
├── My Project/
│   ├── AssemblyInfo.vb               # Assembly attributes (title, version, etc.)
│   ├── Application.Designer.vb        # Auto-generated My.Application
│   ├── Application.myapp             # Startup form config
│   ├── Resources.Designer.vb         # Strongly-typed resource accessor
│   ├── Resources.resx                # Resource entries (images, icons)
│   ├── Settings.Designer.vb          # Strongly-typed settings accessor
│   └── Settings.settings             # User-scope settings (paths, window, recent)
├── Resources/                        # App icons and embedded bitmaps
│   ├── icons.ico
│   ├── animated.gif                  # Used by splash + about dialog
│   ├── bplay.png / bpause.png
│   ├── folder.png / close.png / delete.png / diskette.png
│   ├── timestart.png / timeend.png
│   ├── zoom.png / zoom_in.png / zoom_out.png
│   └── ffmpeg.jpg
├── lib/
│   ├── DirectShowLib-2005.dll        # DirectShowLib-2005 (LGPL v2.1)
│   └── README.txt
└── bin/Debug/                        # Pre-built Debug output for convenience
```

---

## Prerequisites

To **run** Pongo Video Cutter you need:

1. **Windows 7 SP1 or newer** (32-bit build, runs fine on 64-bit Windows).
2. **.NET Framework 2.0** (already installed on Windows 7+; if you're on a stripped-down Windows, grab the redistributable from Microsoft).
3. **FFmpeg binaries** — drop `ffmpeg.exe` and `ffprobe.exe` next to `Pongo Video Cutter.exe`, or set their paths in *Settings → FFmpeg Path*. Recommended build: <https://www.gyan.dev/ffmpeg/builds/>.

To **build** the project from source you need:

1. **Visual Studio 2010 or newer** (Community Edition is free and works fine).
   - Or the **MSBuild tools** alone if you don't want the IDE.
2. **.NET Framework 2.0 targeting pack** (usually ships with VS).
3. The `lib/DirectShowLib-2005.dll` is already bundled in this repo.

---

## Building

1. Clone or download this repository.
2. Open `Pongo Video Cutter.sln` in Visual Studio.
3. Select `Debug` or `Release` and `x86` as the platform.
4. Press **F5** (Debug) or **Ctrl+Shift+B** (Build).
5. The compiled executable appears in `bin/Debug/` or `bin/Release/`.

> The project targets `.NET Framework 2.0` so it runs on the widest possible range of Windows machines. You can bump the target framework to 4.x in the project properties if you want newer language features, but it's not required.

---

## Usage

### Basic workflow

1. **Open a video.** Drag-and-drop a file onto the window, or use *File → Open Video Files...* (`Ctrl+O`).
2. **Preview it.** Press `Space` to play/pause, or use `J` / `K` / `L` for shuttle playback.
3. **Mark a segment.** Move the playhead to the start point and press the **Set Start** button (or `I`); move to the end point and press **Set End** (or `O`).
4. **Add more segments.** Repeat step 3 for every cut you want.
5. *(Optional)* **Add a caption.** Select a segment in the list and press the **Add Caption** button to attach a free-text note.
6. **Choose output mode.** Tick the **Merge Output** checkbox if you want a single merged file instead of one file per segment.
7. **Pick an output file.** Click the **...** button next to *Output File* and choose a path.
8. **Cut!** Click the **Cut** button (or *File → Export* / `Ctrl+E`).

### Project files

- *File → Save Project...* (`Ctrl+S`) saves your cut list and input file path to a `.pvc` file.
- *File → Load Project...* (`Ctrl+L`) restores a saved project.

### Batch queue

- *File → Add to Batch Queue* adds the current video (with its segments) to the queue.
- *File → View Batch Queue...* shows the queue.
- *File → Clear Batch Queue* empties it.

### Keyboard shortcuts

| Key | Action |
|-----|--------|
| `Space`        | Play / pause |
| `J` / `K` / `L`| Shuttle reverse / pause / forward (double = 2x, triple = 4x) |
| `I`            | Set start time of the current segment |
| `O`            | Set end time of the current segment |
| `Del`          | Remove the selected segment |
| `Ctrl+D`       | Clear all segments |
| `Ctrl+O`       | Open video files |
| `Ctrl+E`       | Export (cut) |
| `Ctrl+S`       | Save project |
| `Ctrl+L`       | Load project |
| `Ctrl+T`       | Zoom in timeline |
| `Ctrl+U`       | Zoom out timeline |
| `Ctrl+R`       | Reset timeline zoom |
| `F1`           | Open online tutorials |

---

## Third-party libraries

This project would not exist without these amazing open-source projects:

| Library | Purpose | License |
|---------|---------|---------|
| [FFmpeg](https://ffmpeg.org/)               | Lossless cutting, merging, metadata probing | [LGPL v2.1](https://ffmpeg.org/doxygen/4.4/md_LICENSE.html) |
| [DirectShowLib-2005](https://sourceforge.net/projects/directshownet/) | DirectShow wrapper for the in-app preview player | [LGPL v2.1](https://opensource.org/license/lgpl-2-1) |
| [LAV Filters](https://github.com/nevcairiel/lavfilters) | Recommended codec pack for DirectShow playback | [GPL v2.0](https://github.com/nevcairiel/lavfilters?tab=GPL-2.0-1-ov-file) |

The required `ffmpeg.exe` and `ffprobe.exe` are **not** redistributed in this repository — please download them separately from one of the official builds listed on the FFmpeg site.

---

## License

Pongo Video Cutter is released under the **MIT License**.

```
MIT License

Copyright (c) 2026 Ari Sohandri Putra

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

The third-party libraries this project depends on remain under their own licenses — please respect them.

---

## Author

**Ari Sohandri Putra**

- Web:    <https://pongo.my.id>
- GitHub: <https://github.com/arisohandriputra>
- Email:  `arisohandriputra@gmail.com`

Bug reports, feature requests and pull requests are welcome on GitHub.

---

## Changelog

### v1.0 — initial release

- First public release.
