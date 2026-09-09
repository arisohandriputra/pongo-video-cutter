Pongo Video Cutter - External Dependencies
==========================================

This project requires the following external file:

  lib\DirectShowLib-2005.dll

This DLL is part of the open-source "DirectShowLib V2-1" library
(LGPL / GPL licensed) used for video playback via DirectShow.

HOW TO OBTAIN
-------------
1. Download "DirectShowLibV2-1" from:
   https://sourceforge.net/projects/directshownet/files/

2. Extract the ZIP archive.

3. Copy the file:
   DirectShowLibV2-1\lib\DirectShowLib-2005.dll

   into this folder:
   Pongo Video Cutter\lib\

4. Re-open the project in Visual Studio and rebuild.

NOTE
----
If the DLL is missing, the project will fail to compile with an error
about the "DirectShowLib-2005" reference. The DLL is NOT distributed
with this source code for licensing reasons.
