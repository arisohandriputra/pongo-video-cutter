' ==================================================================*
'  Pongo Video Cutter - Strongly-Typed Resources Generator
'  -----------------------------------------------------------------
'  Author  : Ari Sohandri Putra
'  Web     : https://pongo.my.id
'  GitHub  : https://github.com/arisohandriputra
'  License : MIT (see LICENSE file or pongo.my.id/license.htm)
' -----------------------------------------------------------------
'  Auto-generated wrapper around the project's .resx resource file.
'  Donate-tier image properties have been removed; only the icons,
'  buttons and timeline images that the app actually uses remain.
' ==================================================================*

Option Strict On
Option Explicit On

Imports System

Namespace My.Resources

    <Global.System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0"), _
     Global.System.Diagnostics.DebuggerNonUserCodeAttribute(), _
     Global.System.Runtime.CompilerServices.CompilerGeneratedAttribute(), _
     Global.Microsoft.VisualBasic.HideModuleNameAttribute()> _
    Friend Module Resources

        Private resourceMan As Global.System.Resources.ResourceManager

        Private resourceCulture As Global.System.Globalization.CultureInfo

        <Global.System.ComponentModel.EditorBrowsableAttribute(Global.System.ComponentModel.EditorBrowsableState.Advanced)> _
        Friend ReadOnly Property ResourceManager() As Global.System.Resources.ResourceManager
            Get
                If Object.ReferenceEquals(resourceMan, Nothing) Then
                    Dim temp As Global.System.Resources.ResourceManager = New Global.System.Resources.ResourceManager("Pongo_Video_Cutter.Resources", GetType(Resources).Assembly)
                    resourceMan = temp
                End If
                Return resourceMan
            End Get
        End Property

        <Global.System.ComponentModel.EditorBrowsableAttribute(Global.System.ComponentModel.EditorBrowsableState.Advanced)> _
        Friend Property Culture() As Global.System.Globalization.CultureInfo
            Get
                Return resourceCulture
            End Get
            Set
                resourceCulture = value
            End Set
        End Property

        ' ------------------------------------------------------------------
        ' NOTE: Donate-tier image properties (_5usd, _10usd, _20usd,
        '       _50usd, _100usd) were removed together with the Donate
        '       menu. The corresponding PNGs have been deleted from
        '       the Resources/ folder.
        ' ------------------------------------------------------------------

        Friend ReadOnly Property animated() As System.Drawing.Bitmap
            Get
                Dim obj As Object = ResourceManager.GetObject("animated", resourceCulture)
                Return CType(obj, System.Drawing.Bitmap)
            End Get
        End Property

        Friend ReadOnly Property bpause() As System.Drawing.Bitmap
            Get
                Dim obj As Object = ResourceManager.GetObject("bpause", resourceCulture)
                Return CType(obj, System.Drawing.Bitmap)
            End Get
        End Property

        Friend ReadOnly Property bplay() As System.Drawing.Bitmap
            Get
                Dim obj As Object = ResourceManager.GetObject("bplay", resourceCulture)
                Return CType(obj, System.Drawing.Bitmap)
            End Get
        End Property

        Friend ReadOnly Property close() As System.Drawing.Bitmap
            Get
                Dim obj As Object = ResourceManager.GetObject("close", resourceCulture)
                Return CType(obj, System.Drawing.Bitmap)
            End Get
        End Property

        Friend ReadOnly Property delete() As System.Drawing.Bitmap
            Get
                Dim obj As Object = ResourceManager.GetObject("delete", resourceCulture)
                Return CType(obj, System.Drawing.Bitmap)
            End Get
        End Property

        Friend ReadOnly Property diskette() As System.Drawing.Bitmap
            Get
                Dim obj As Object = ResourceManager.GetObject("diskette", resourceCulture)
                Return CType(obj, System.Drawing.Bitmap)
            End Get
        End Property

        Friend ReadOnly Property ffmpeg() As System.Drawing.Bitmap
            Get
                Dim obj As Object = ResourceManager.GetObject("ffmpeg", resourceCulture)
                Return CType(obj, System.Drawing.Bitmap)
            End Get
        End Property

        Friend ReadOnly Property folder() As System.Drawing.Bitmap
            Get
                Dim obj As Object = ResourceManager.GetObject("folder", resourceCulture)
                Return CType(obj, System.Drawing.Bitmap)
            End Get
        End Property

        Friend ReadOnly Property icons() As System.Drawing.Icon
            Get
                Dim obj As Object = ResourceManager.GetObject("icons", resourceCulture)
                Return CType(obj, System.Drawing.Icon)
            End Get
        End Property

        Friend ReadOnly Property timeend() As System.Drawing.Bitmap
            Get
                Dim obj As Object = ResourceManager.GetObject("timeend", resourceCulture)
                Return CType(obj, System.Drawing.Bitmap)
            End Get
        End Property

        Friend ReadOnly Property timestart() As System.Drawing.Bitmap
            Get
                Dim obj As Object = ResourceManager.GetObject("timestart", resourceCulture)
                Return CType(obj, System.Drawing.Bitmap)
            End Get
        End Property

        Friend ReadOnly Property zoom() As System.Drawing.Bitmap
            Get
                Dim obj As Object = ResourceManager.GetObject("zoom", resourceCulture)
                Return CType(obj, System.Drawing.Bitmap)
            End Get
        End Property

        Friend ReadOnly Property zoom_in() As System.Drawing.Bitmap
            Get
                Dim obj As Object = ResourceManager.GetObject("zoom_in", resourceCulture)
                Return CType(obj, System.Drawing.Bitmap)
            End Get
        End Property

        Friend ReadOnly Property zoom_out() As System.Drawing.Bitmap
            Get
                Dim obj As Object = ResourceManager.GetObject("zoom_out", resourceCulture)
                Return CType(obj, System.Drawing.Bitmap)
            End Get
        End Property
    End Module
End Namespace
