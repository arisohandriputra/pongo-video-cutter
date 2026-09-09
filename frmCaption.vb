' ==================================================================*
'  Pongo Video Cutter - Segment Caption Editor Code-Behind
'  -----------------------------------------------------------------
'  Author  : Ari Sohandri Putra
'  Web     : https://pongo.my.id
'  GitHub  : https://github.com/arisohandriputra
'  License : MIT (see LICENSE file or pongo.my.id/license.htm)
' -----------------------------------------------------------------
'  Modal dialog used to add or edit a free-text caption / note on a
'  segment. Exposes SegmentIndex, StartTime, EndTime, Caption
'  properties and returns DialogResult.OK / .Cancel.
' ==================================================================*

Public Class frmCaption

    Private _segmentIndex As Integer = 0
    Private _startTime As String = ""
    Private _endTime As String = ""
    Private _caption As String = ""

    Public Property SegmentIndex() As Integer
        Get
            Return _segmentIndex
        End Get
        Set(ByVal value As Integer)
            _segmentIndex = value
            UpdateInfo()
        End Set
    End Property

    Public Property StartTime() As String
        Get
            Return _startTime
        End Get
        Set(ByVal value As String)
            _startTime = value
            UpdateInfo()
        End Set
    End Property

    Public Property EndTime() As String
        Get
            Return _endTime
        End Get
        Set(ByVal value As String)
            _endTime = value
            UpdateInfo()
        End Set
    End Property

    Public Property Caption() As String
        Get
            Return _caption
        End Get
        Set(ByVal value As String)
            _caption = value
            txtCaption.Text = value
        End Set
    End Property

    Private Sub UpdateInfo()
        If lblSegmentInfo IsNot Nothing Then
            lblSegmentInfo.Text = "Segment #" & _segmentIndex.ToString() & "  -  " & _startTime & " to " & _endTime
        End If
    End Sub

    Private Sub frmCaption_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        UpdateInfo()
        txtCaption.Text = _caption
        txtCaption.Focus()
        txtCaption.SelectionStart = 0
        txtCaption.SelectionLength = txtCaption.TextLength
        AcceptButton = btnOK
        CancelButton = btnCancel
    End Sub

    Private Sub btnOK_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnOK.Click
        _caption = txtCaption.Text
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    Private Sub btnCancel_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnCancel.Click
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    Private Sub btnClear_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnClear.Click
        txtCaption.Text = ""
        txtCaption.Focus()
    End Sub
End Class
