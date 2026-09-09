' ==================================================================*
'  Pongo Video Cutter - Styled Owner-Drawn Segment ListBox
'  -----------------------------------------------------------------
'  Author  : Ari Sohandri Putra
'  Web     : https://pongo.my.id
'  GitHub  : https://github.com/arisohandriputra
'  License : MIT (see LICENSE file or pongo.my.id/license.htm)
' -----------------------------------------------------------------
'  Custom ListBox that renders each segment row with a modern look
'  (rounded highlight, hover colour, per-item foreground colour). Used
'  in Form1 to display the list of cut segments.
' ==================================================================*

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Public Class ModernListBox
    Inherits ListBox

    Public Sub New()
        MyBase.New()
        Me.DrawMode = DrawMode.OwnerDrawVariable
        Me.ItemHeight = 45
        Me.BorderStyle = BorderStyle.None
        Me.BackColor = Color.FromArgb(248, 249, 252)
        Me.ForeColor = Color.FromArgb(50, 50, 70)
        Me.Font = New Font("Arial", 8, FontStyle.Regular)
        Me.HorizontalScrollbar = True
        Me.HorizontalExtent = 800
        Me.ScrollAlwaysVisible = True
        Me.MultiColumn = False
    End Sub

    Protected Overrides Sub OnDrawItem(ByVal e As DrawItemEventArgs)
        If e.Index < 0 Then Return

        e.DrawBackground()
        e.DrawFocusRectangle()

        Dim colors As Color() = {Color.FromArgb(0, 120, 215), Color.FromArgb(118, 75, 162), Color.FromArgb(234, 88, 12), _
                                 Color.FromArgb(0, 153, 136), Color.FromArgb(232, 17, 35), Color.FromArgb(0, 156, 76)}
        Dim colorIndex As Integer = e.Index Mod colors.Length
        Dim segmentColor As Color = colors(colorIndex)

        Dim isSelected As Boolean = (e.State And DrawItemState.Selected) = DrawItemState.Selected
        If isSelected Then
            e.Graphics.FillRectangle(New SolidBrush(Color.FromArgb(80, segmentColor)), e.Bounds)
        Else
            e.Graphics.FillRectangle(New SolidBrush(If(e.Index Mod 2 = 0, Color.FromArgb(252, 253, 255), Color.FromArgb(245, 247, 250))), e.Bounds)
        End If

        Dim colorBarRect As New Rectangle(e.Bounds.X, e.Bounds.Y, 5, e.Bounds.Height)
        e.Graphics.FillRectangle(New SolidBrush(segmentColor), colorBarRect)

        Dim circleRect As New Rectangle(e.Bounds.X + 15, e.Bounds.Y + (e.Bounds.Height - 25) \ 2, 25, 25)
        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        e.Graphics.FillEllipse(New SolidBrush(segmentColor), circleRect)
        e.Graphics.DrawEllipse(New Pen(Color.White, 2), circleRect)

        Dim numberText As String = (e.Index + 1).ToString()
        Dim numberFont As New Font("Segoe UI", 9, FontStyle.Bold)
        Dim numberBrush As New SolidBrush(Color.White)
        Dim numberFormat As New StringFormat()
        numberFormat.Alignment = StringAlignment.Center
        numberFormat.LineAlignment = StringAlignment.Center
        e.Graphics.DrawString(numberText, numberFont, numberBrush, circleRect, numberFormat)
        numberFont.Dispose()
        numberBrush.Dispose()
        numberFormat.Dispose()

        If e.Index < Items.Count Then
            Dim item As Object = Items(e.Index)

            Try
                Dim startTime As Double = CDbl(item.GetType().GetProperty("StartTime").GetValue(item, Nothing))
                Dim endTime As Double = CDbl(item.GetType().GetProperty("EndTime").GetValue(item, Nothing))
                Dim caption As String = CStr(item.GetType().GetProperty("Caption").GetValue(item, Nothing))

                Dim startTs As TimeSpan = TimeSpan.FromSeconds(startTime)
                Dim endTs As TimeSpan = TimeSpan.FromSeconds(endTime)
                Dim startStr As String = String.Format("{0:00}:{1:00}:{2:00}", startTs.Hours, startTs.Minutes, startTs.Seconds)
                Dim endStr As String = String.Format("{0:00}:{1:00}:{2:00}", endTs.Hours, endTs.Minutes, endTs.Seconds)

                Dim timeText As String = startStr & "  -  " & endStr
                Dim timeFont As New Font("Consolas", 10, FontStyle.Bold)
                Dim timeRect As New Rectangle(e.Bounds.X + 50, e.Bounds.Y + 7, 200, 20)
                e.Graphics.DrawString(timeText, timeFont, New SolidBrush(Color.FromArgb(50, 50, 70)), timeRect)
                timeFont.Dispose()

                Dim captionText As String = caption
                If String.IsNullOrEmpty(captionText) Then captionText = "(no caption)"
                Dim captionFont As New Font("Segoe UI", 9, FontStyle.Italic)
                Dim captionColor As Color = If(String.IsNullOrEmpty(caption), Color.FromArgb(180, 110, 115, 125), Color.FromArgb(0, 120, 215))
                Dim captionRect As New Rectangle(e.Bounds.X + 260, e.Bounds.Y + 8, 500, 20)
                e.Graphics.DrawString(captionText, captionFont, New SolidBrush(captionColor), captionRect)
                captionFont.Dispose()

                Dim duration As Double = endTime - startTime
                Dim durText As String = "Duration: " & String.Format("{0:00}:{1:00}:{2:00}", _
                    TimeSpan.FromSeconds(duration).Hours, TimeSpan.FromSeconds(duration).Minutes, TimeSpan.FromSeconds(duration).Seconds)
                Dim durFont As New Font("Segoe UI", 8, FontStyle.Regular)
                Dim durRect As New Rectangle(e.Bounds.X + 50, e.Bounds.Y + 26, 200, 14)
                e.Graphics.DrawString(durText, durFont, New SolidBrush(Color.FromArgb(110, 115, 125)), durRect)
                durFont.Dispose()
            Catch ex As Exception
                Dim segText As String = item.ToString()
                Dim textRect As New Rectangle(e.Bounds.X + 50, e.Bounds.Y + 14, 700, 18)
                Dim textFont As New Font("Segoe UI", 9, FontStyle.Bold)
                e.Graphics.DrawString(segText, textFont, New SolidBrush(Color.FromArgb(50, 50, 70)), textRect)
                textFont.Dispose()
            End Try
        End If

        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.Default
    End Sub

    Protected Overrides Sub OnMeasureItem(ByVal e As MeasureItemEventArgs)
        MyBase.OnMeasureItem(e)
        e.ItemHeight = 45
        e.ItemWidth = 800
    End Sub

    Public Sub UpdateHorizontalExtent()
        Dim maxExtent As Integer = 800
        Try
            Using g As Graphics = Me.CreateGraphics()
                Dim timeFont As New Font("Consolas", 10, FontStyle.Bold)
                Dim captionFont As New Font("Segoe UI", 9, FontStyle.Italic)
                For i As Integer = 0 To Me.Items.Count - 1
                    Dim item As Object = Me.Items(i)
                    Dim segText As String = item.ToString()
                    Dim w As Integer = CInt(g.MeasureString(segText, timeFont).Width) + 300
                    If w > maxExtent Then maxExtent = w
                Next
                timeFont.Dispose()
                captionFont.Dispose()
            End Using
        Catch
        End Try
        Me.HorizontalExtent = maxExtent
    End Sub
End Class
