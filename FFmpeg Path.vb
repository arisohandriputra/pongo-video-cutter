' ==================================================================*
'  Pongo Video Cutter - FFmpeg / FFprobe Path Picker Code-Behind
'  -----------------------------------------------------------------
'  Author  : Ari Sohandri Putra
'  Web     : https://pongo.my.id
'  GitHub  : https://github.com/arisohandriputra
'  License : MIT (see LICENSE file or pongo.my.id/license.htm)
' -----------------------------------------------------------------
'  Modal dialog that lets the user point Pongo at their own ffmpeg.exe
'  and ffprobe.exe. Persists the chosen paths to My.Settings.
' ==================================================================*

Public Class FFmpeg_Path

    Private Sub Button2_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button2.Click
        Me.Close()
    End Sub

    Private Sub LinkLabel1_LinkClicked(ByVal sender As System.Object, ByVal e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) Handles LinkLabel1.LinkClicked
        OpenFileDialog1.Filter = "ffmpeg.exe|ffmpeg.exe"

        If OpenFileDialog1.ShowDialog() = DialogResult.OK Then
            If String.Equals(System.IO.Path.GetFileName(OpenFileDialog1.FileName), _
                             "ffmpeg.exe", _
                             StringComparison.OrdinalIgnoreCase) Then
                TextBox1.Text = OpenFileDialog1.FileName
            End If
        End If
    End Sub

    Private Sub LinkLabel2_LinkClicked(ByVal sender As System.Object, ByVal e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) Handles LinkLabel2.LinkClicked
        OpenFileDialog2.Filter = "ffprobe.exe|ffprobe.exe"

        If OpenFileDialog2.ShowDialog() = DialogResult.OK Then
            If String.Equals(System.IO.Path.GetFileName(OpenFileDialog2.FileName), _
                             "ffprobe.exe", _
                             StringComparison.OrdinalIgnoreCase) Then
                TextBox2.Text = OpenFileDialog2.FileName
            End If
        End If
    End Sub

    Private Sub Button1_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button1.Click
        Form1.ffmpegPath = TextBox1.Text
        Form1.ffprobePath = TextBox2.Text
        My.Settings.ffmpegpath = TextBox1.Text
        My.Settings.ffprobepath = TextBox2.Text
        My.Settings.Save()
        Me.Close()
    End Sub

    Private Sub FFmpeg_Path_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load

    End Sub
End Class
