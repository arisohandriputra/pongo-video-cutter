' ==================================================================*
'  Pongo Video Cutter - Splash Screen Code-Behind
'  -----------------------------------------------------------------
'  Author  : Ari Sohandri Putra
'  Web     : https://pongo.my.id
'  GitHub  : https://github.com/arisohandriputra
'  License : MIT (see LICENSE file or pongo.my.id/license.htm)
' -----------------------------------------------------------------
'  Tiny splash screen shown for ~2 seconds on startup. Drives a single
'  progress bar via Timer1, then hides itself and opens Form1.
' ==================================================================*

Public Class frmSplash

    Private Sub Timer1_Tick(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Timer1.Tick
        ProgressBar1.Increment(1)
        If ProgressBar1.Value = 100 Then
            Timer1.Stop()
            Me.Hide()
            Form1.Show()
        End If
    End Sub
End Class
