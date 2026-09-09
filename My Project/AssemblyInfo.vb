' ==================================================================*
'  Pongo Video Cutter - Assembly Metadata
'  -----------------------------------------------------------------
'  Author  : Ari Sohandri Putra
'  Web     : https://pongo.my.id
'  GitHub  : https://github.com/arisohandriputra
'  License : MIT (see LICENSE file or pongo.my.id/license.htm)
' -----------------------------------------------------------------
'  Holds the assembly attributes (title, description, company,
'  copyright, version) shown by Windows file properties and the
'  About dialog.
' ==================================================================*

Imports System
Imports System.Reflection
Imports System.Runtime.InteropServices

' --- Assembly metadata (visible in Windows file properties) ---------
<Assembly: AssemblyTitle("Pongo Video Cutter")>
<Assembly: AssemblyDescription("Free Open Source Lossless Video Cutting Software. Cuts and merges video segments without re-encoding.")> 
<Assembly: AssemblyCompany("Ari Sohandri Putra")>
<Assembly: AssemblyProduct("Pongo Video Cutter")>
<Assembly: AssemblyCopyright("© 2026 Ari Sohandri Putra. Released under the MIT License.")>
<Assembly: AssemblyTrademark("Pongo Video Cutter")>

' Make this assembly invisible to COM interop by default.
<Assembly: ComVisible(False)>

' Stable assembly GUID (do not change between releases).
<Assembly: Guid("86eae470-e342-4d9e-9e5f-183ce4e1b56a")>

' AssemblyVersion stays at 2.0.0.0 for the .NET Framework loader.
' Bump AssemblyFileVersion on every release.
<Assembly: AssemblyVersion("1.0.0.0")> 
<Assembly: AssemblyFileVersion("1.0.0.0")> 
