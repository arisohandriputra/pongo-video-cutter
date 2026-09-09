' ==================================================================*
'  Pongo Video Cutter - About Dialog Code-Behind
'  -----------------------------------------------------------------
'  Author  : Ari Sohandri Putra
'  Web     : https://pongo.my.id
'  GitHub  : https://github.com/arisohandriputra
'  License : MIT (see LICENSE file or pongo.my.id/license.htm)
' -----------------------------------------------------------------
'  Small dialog that shows the product name, version, developer
'  credit, and the third-party libraries that make this app possible.
'  Links open in the user's default browser via Process.Start().
' ==================================================================*

Imports System.Diagnostics

''' <summary>
'''  About box for Pongo Video Cutter. Shows version info, developer
'''  credit, and quick links to the homepage, license page and the
'''  third-party libraries we depend on.
''' </summary>
Public Class frmAbout

    ' --- Close button ----------------------------------------------------
    Private Sub Button1_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button1.Click
        Me.Close()
    End Sub

    ' --- Homepage link -> https://pongo.my.id/ ---------------------------
    Private Sub LinkLabel1_LinkClicked(ByVal sender As System.Object, ByVal e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) Handles LinkLabel1.LinkClicked
        OpenUrl("https://pongo.my.id/")
    End Sub

    ' --- License link -> https://pongo.my.id/license.htm -----------------
    Private Sub LinkLabel2_LinkClicked(ByVal sender As System.Object, ByVal e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) Handles LinkLabel2.LinkClicked
        OpenUrl("https://pongo.my.id/license.htm")
    End Sub

    ' --- Developer GitHub profile -> https://github.com/arisohandriputra -
    Private Sub LinkLabel6_LinkClicked(ByVal sender As System.Object, ByVal e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) Handles LinkLabel6.LinkClicked
        OpenUrl("https://github.com/arisohandriputra")
    End Sub

    ' --- FFmpeg license (LGPL v2.1) --------------------------------------
    Private Sub LinkLabel3_LinkClicked(ByVal sender As System.Object, ByVal e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) Handles LinkLabel3.LinkClicked
        OpenUrl("https://ffmpeg.org/doxygen/4.4/md_LICENSE.html")
    End Sub

    ' --- DirectShowLib license (LGPL v2.1) ------------------------------
    Private Sub LinkLabel4_LinkClicked(ByVal sender As System.Object, ByVal e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) Handles LinkLabel4.LinkClicked
        OpenUrl("https://opensource.org/license/lgpl-2-1")
    End Sub

    ' --- LAV Filters license (GPL v2.0) ---------------------------------
    Private Sub LinkLabel5_LinkClicked(ByVal sender As System.Object, ByVal e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) Handles LinkLabel5.LinkClicked
        OpenUrl("https://github.com/nevcairiel/lavfilters?tab=GPL-2.0-1-ov-file")
    End Sub

    ' --- Tiny helper: launch a URL and fall back to a tidy message box --
    Private Sub OpenUrl(ByVal url As String)
        Try
            Process.Start(url)
        Catch ex As Exception
            MessageBox.Show(ex.Message, _
                            "Pongo Video Cutter - Unable to Open Link", _
                            MessageBoxButtons.OK, _
                            MessageBoxIcon.Warning)
        End Try
    End Sub

End Class
