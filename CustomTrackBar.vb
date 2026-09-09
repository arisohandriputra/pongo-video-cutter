' ==================================================================*
'  Pongo Video Cutter - Custom Multi-Segment Timeline Control
'  -----------------------------------------------------------------
'  Author  : Ari Sohandri Putra
'  Web     : https://pongo.my.id
'  GitHub  : https://github.com/arisohandriputra
'  License : MIT (see LICENSE file or pongo.my.id/license.htm)
' -----------------------------------------------------------------
'  Owner-drawn trackbar that draws the playhead, segment markers and a
'  zoomable timeline ruler. Supports click-to-seek, drag-to-move and
'  per-segment selection.
' ==================================================================*

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports System.Collections.Generic

Public Class CustomTrackBar
    Inherits Control
    Private _playheadPriority As Boolean = True

    Private _minimum As Integer = 0
    Private _maximum As Integer = 1000
    Private _value As Integer = 0
    Private _isDragging As Boolean = False
    Private _segments As New List(Of TrackSegment)
    Private _selectedSegmentIndex As Integer = -1
    Private _draggingMarker As Integer = -1
    Private _tempStartMarker As Double = -1
    Private _tempEndMarker As Double = -1
    Private _isDraggingSegment As Boolean = False
    Private _isDraggingPlayhead As Boolean = False
    Private _lastMouseX As Integer = 0
    Private _isDraggingScrollbar As Boolean = False
    Private _drawnLabels As New List(Of Rectangle)

    Private _zoomLevel As Double = 1.0
    Private _zoomCenter As Double = 0.5
    Private _minZoomLevel As Double = 1.0
    Private _maxZoomLevel As Double = 200.0
    Private _autoScrollEnabled As Boolean = True
    Private _autoScrollMargin As Double = 0.15
    Private _zoomStep As Double = 1.5
    Private _isZooming As Boolean = False
    Private _segmentOnlyMode As Boolean = False

    Public Property SegmentOnlyMode() As Boolean
        Get
            Return _segmentOnlyMode
        End Get
        Set(ByVal value As Boolean)
            _segmentOnlyMode = value
            Invalidate()
        End Set
    End Property

    Public Event ValueChanged As EventHandler
    Public Event Scroll As EventHandler
    Public Event SegmentClicked As SegmentClickedEventHandler
    Public Event SegmentChanged As SegmentChangedEventHandler
    Public Event PlayheadDragging As EventHandler
    Public Event PlayheadDragStart As EventHandler
    Public Event PlayheadDragEnd As EventHandler
    Public Event ZoomChanged As EventHandler

    Public Delegate Sub SegmentClickedEventHandler(ByVal sender As Object, ByVal segmentIndex As Integer)
    Public Delegate Sub SegmentChangedEventHandler(ByVal sender As Object, ByVal segmentIndex As Integer, ByVal startValue As Double, ByVal endValue As Double)

    Public Structure TrackSegment
        Dim StartValue As Double
        Dim EndValue As Double
        Dim Color As Color

        Public Sub New(ByVal start As Double, ByVal endVal As Double, ByVal segColor As Color)
            StartValue = start
            EndValue = endVal
            Color = segColor
        End Sub
    End Structure
    Public Property PlayheadPriority() As Boolean
        Get
            Return _playheadPriority
        End Get
        Set(ByVal value As Boolean)
            _playheadPriority = value
        End Set
    End Property

    Public Sub New()
        MyBase.New()
        Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or _
                   ControlStyles.UserPaint Or _
                   ControlStyles.OptimizedDoubleBuffer Or _
                   ControlStyles.ResizeRedraw Or _
                   ControlStyles.Selectable, True)
        Me.Size = New Size(400, 100)
        Me.BackColor = Color.FromArgb(248, 249, 252)
        Me.TabStop = True
        Me.Cursor = Cursors.Default
    End Sub

    Public Property ZoomLevel() As Double
        Get
            Return _zoomLevel
        End Get
        Set(ByVal value As Double)
            If value < _minZoomLevel Then value = _minZoomLevel
            If value > _maxZoomLevel Then value = _maxZoomLevel
            If _zoomLevel <> value Then
                _zoomLevel = value
                Invalidate()
                RaiseEvent ZoomChanged(Me, EventArgs.Empty)
            End If
        End Set
    End Property

    Public ReadOnly Property CurrentZoomLevel() As Double
        Get
            Return _zoomLevel
        End Get
    End Property

    Public Property AutoScrollEnabled() As Boolean
        Get
            Return _autoScrollEnabled
        End Get
        Set(ByVal value As Boolean)
            _autoScrollEnabled = value
        End Set
    End Property

    Public Sub ZoomIn()
        If _isZooming Then Return
        _isZooming = True
        Try

            Dim playheadValue As Double = _value

            Dim newZoom As Double = _zoomLevel * _zoomStep
            If newZoom > _maxZoomLevel Then newZoom = _maxZoomLevel

            If newZoom = _zoomLevel Then
                _isZooming = False
                Return
            End If

            _zoomLevel = newZoom

            EnsureValueVisible(playheadValue)

            If _zoomLevel > 1.0 Then
                _zoomCenter = playheadValue / (_maximum - _minimum)
                _zoomCenter = Math.Max(0.0, Math.Min(1.0, _zoomCenter))
            Else
                _zoomCenter = 0.5
            End If

            Invalidate()
            RaiseEvent ZoomChanged(Me, EventArgs.Empty)
        Finally
            _isZooming = False
        End Try
    End Sub

    Public Sub ZoomOut()
        If _isZooming Then Return
        _isZooming = True
        Try

            Dim playheadValue As Double = _value

            Dim newZoom As Double = _zoomLevel / _zoomStep
            If newZoom < _minZoomLevel Then newZoom = _minZoomLevel

            If newZoom = _zoomLevel Then
                _isZooming = False
                Return
            End If

            _zoomLevel = newZoom

            If _zoomLevel <= 1.0 Then
                _zoomCenter = 0.5
            Else

                EnsureValueVisible(playheadValue)

                _zoomCenter = playheadValue / (_maximum - _minimum)
                _zoomCenter = Math.Max(0.0, Math.Min(1.0, _zoomCenter))
            End If

            Invalidate()
            RaiseEvent ZoomChanged(Me, EventArgs.Empty)
        Finally
            _isZooming = False
        End Try
    End Sub

    Public Sub ResetZoom()
        If _isZooming Then Return
        _isZooming = True
        Try
            _zoomLevel = 1.0
            _zoomCenter = 0.5
            Invalidate()
            RaiseEvent ZoomChanged(Me, EventArgs.Empty)
        Finally
            _isZooming = False
        End Try
    End Sub

    Public Function GetVisibleStartValue() As Double
        If _zoomLevel <= 1.0 Then
            Return _minimum
        End If

        Dim totalRange As Double = _maximum - _minimum
        Dim visibleRangeCalc As Double = totalRange / _zoomLevel

        Dim startValue As Double = (_zoomCenter * totalRange) - (visibleRangeCalc / 2.0)

        If startValue < _minimum Then startValue = _minimum
        If startValue > _maximum - visibleRangeCalc Then startValue = _maximum - visibleRangeCalc

        Return startValue
    End Function

    Public Function GetVisibleEndValue() As Double
        If _zoomLevel <= 1.0 Then
            Return _maximum
        End If

        Dim visibleStart As Double = GetVisibleStartValue()
        Dim totalRange As Double = _maximum - _minimum
        Dim visibleRangeCalc As Double = totalRange / _zoomLevel

        Return visibleStart + visibleRangeCalc
    End Function

    Public Sub EnsureValueVisible(ByVal value As Double)
        If _zoomLevel <= 1.0 Then Return

        Dim totalRange As Double = _maximum - _minimum
        Dim visibleRangeCalc As Double = totalRange / _zoomLevel
        Dim visibleStart As Double = GetVisibleStartValue()
        Dim visibleEnd As Double = visibleStart + visibleRangeCalc

        Dim marginValue As Double = visibleRangeCalc * _autoScrollMargin

        If value < visibleStart + marginValue OrElse value > visibleEnd - marginValue Then

            Dim newCenter As Double = value / totalRange
            _zoomCenter = Math.Max(0.0, Math.Min(1.0, newCenter))
            Invalidate()
            RaiseEvent ZoomChanged(Me, EventArgs.Empty)
        End If
    End Sub

    Public Sub ScrollToValue(ByVal value As Double)
        If _zoomLevel <= 1.0 Then Return

        Dim totalRange As Double = _maximum - _minimum
        If totalRange <= 0 Then Return

        Dim newCenter As Double = value / totalRange
        _zoomCenter = Math.Max(0.0, Math.Min(1.0, newCenter))
        Invalidate()
        RaiseEvent ZoomChanged(Me, EventArgs.Empty)
    End Sub

    Public Sub ScrollView(ByVal delta As Double)
        If _zoomLevel <= 1.0 Then Return

        Dim totalRange As Double = _maximum - _minimum
        If totalRange <= 0 Then Return

        Dim visibleRange As Double = totalRange / _zoomLevel
        Dim scrollAmount As Double = visibleRange * delta

        Dim newCenterValue As Double = (_zoomCenter * totalRange) + scrollAmount

        Dim minCenter As Double = (visibleRange / 2) / totalRange
        Dim maxCenter As Double = 1.0 - minCenter

        _zoomCenter = Math.Max(minCenter, Math.Min(maxCenter, newCenterValue / totalRange))

        Invalidate()
        RaiseEvent ZoomChanged(Me, EventArgs.Empty)
    End Sub

    Public ReadOnly Property IsDraggingPlayhead() As Boolean
        Get
            Return _isDraggingPlayhead
        End Get
    End Property

    Public ReadOnly Property IsDraggingSegment() As Boolean
        Get
            Return _isDraggingSegment
        End Get
    End Property

    Public Property SelectedSegmentIndexValue() As Integer
        Get
            Return _selectedSegmentIndex
        End Get
        Set(ByVal value As Integer)
            If value >= -1 AndAlso value < _segments.Count Then
                _selectedSegmentIndex = value
                Invalidate()
            End If
        End Set
    End Property

    Public ReadOnly Property SelectedSegmentIndex() As Integer
        Get
            Return _selectedSegmentIndex
        End Get
    End Property

    Public Property Minimum() As Integer
        Get
            Return _minimum
        End Get
        Set(ByVal value As Integer)
            _minimum = value
            If _value < _minimum Then _value = _minimum
            Invalidate()
        End Set
    End Property

    Public Property Maximum() As Integer
        Get
            Return _maximum
        End Get
        Set(ByVal value As Integer)
            _maximum = value
            If _value > _maximum Then _value = _maximum
            Invalidate()
        End Set
    End Property

    Public Property Value() As Integer
        Get
            Return _value
        End Get
        Set(ByVal value As Integer)
            If value < _minimum Then value = _minimum
            If value > _maximum Then value = _maximum
            If _value <> value Then
                _value = value
                Invalidate()
                RaiseEvent ValueChanged(Me, EventArgs.Empty)

                If _autoScrollEnabled AndAlso _zoomLevel > 1.0 Then
                    EnsureValueVisible(value)
                End If
            End If
        End Set
    End Property

    Public Property TempStartMarker() As Double
        Get
            Return _tempStartMarker
        End Get
        Set(ByVal value As Double)
            _tempStartMarker = value
            Invalidate()
        End Set
    End Property

    Public Property TempEndMarker() As Double
        Get
            Return _tempEndMarker
        End Get
        Set(ByVal value As Double)
            _tempEndMarker = value
            Invalidate()
        End Set
    End Property

    Public Sub AddSegment(ByVal startValue As Double, ByVal endValue As Double)
        Dim colors As Color() = {Color.FromArgb(0, 120, 215), Color.FromArgb(118, 75, 162), Color.FromArgb(234, 88, 12), _
                                 Color.FromArgb(0, 153, 136), Color.FromArgb(232, 17, 35), Color.FromArgb(0, 156, 76)}
        Dim colorIndex As Integer = _segments.Count Mod colors.Length
        _segments.Add(New TrackSegment(startValue, endValue, colors(colorIndex)))
        _tempStartMarker = -1
        _tempEndMarker = -1
        Invalidate()
    End Sub

    Public Sub RemoveSegment(ByVal index As Integer)
        If index >= 0 AndAlso index < _segments.Count Then
            _segments.RemoveAt(index)
            If _selectedSegmentIndex >= _segments.Count Then
                _selectedSegmentIndex = -1
            End If
            Invalidate()
        End If
    End Sub

    Public Sub ClearSegments()
        _segments.Clear()
        _selectedSegmentIndex = -1
        _tempStartMarker = -1
        _tempEndMarker = -1
        Invalidate()
    End Sub

    Public Sub UpdateSegment(ByVal index As Integer, ByVal startValue As Double, ByVal endValue As Double)
        If index >= 0 AndAlso index < _segments.Count Then
            Dim seg As TrackSegment = _segments(index)
            seg.StartValue = startValue
            seg.EndValue = endValue
            _segments(index) = seg
            Invalidate()
        End If
    End Sub

    Public Function GetSegmentCount() As Integer
        Return _segments.Count
    End Function

    Public Function GetSegment(ByVal index As Integer) As TrackSegment
        If index >= 0 AndAlso index < _segments.Count Then
            Return _segments(index)
        End If
        Return Nothing
    End Function

    Private Sub DrawTimeRuler(ByVal g As Graphics, ByVal trackLeft As Integer, ByVal trackY As Integer, ByVal trackWidth As Integer, ByVal visibleStart As Double, ByVal visibleEnd As Double)
        Dim visibleRange As Double = visibleEnd - visibleStart

        Dim startTimeInSeconds As Double = 0
        Dim endTimeInSeconds As Double = 0

        If _videoDuration > 0 Then
            startTimeInSeconds = (visibleStart / 1000.0) * _videoDuration
            endTimeInSeconds = (visibleEnd / 1000.0) * _videoDuration
        Else
            startTimeInSeconds = visibleStart
            endTimeInSeconds = visibleEnd
        End If

        Dim visibleTimeRange As Double = endTimeInSeconds - startTimeInSeconds

        Dim pixelsPerSecond As Double = trackWidth / visibleTimeRange

        Dim targetLabelSpacing As Double = 85
        Dim idealLabelInterval As Double = targetLabelSpacing / pixelsPerSecond

        Dim majorInterval As Double = GetNiceInterval(idealLabelInterval)

        Dim minorInterval As Double = majorInterval / 10

        Dim mediumInterval As Double = majorInterval / 5

        Dim timeFont As New Font("Segoe UI", 7, FontStyle.Regular)
        Dim timeBrush As New SolidBrush(Color.FromArgb(100, 105, 115))

        Dim minorTickPen As New Pen(Color.FromArgb(220, 222, 228), 1)
        Dim mediumTickPen As New Pen(Color.FromArgb(180, 185, 195), 1)
        Dim majorTickPen As New Pen(Color.FromArgb(140, 145, 155), 1)

        Dim rulerRect As New Rectangle(trackLeft, trackY - 22, trackWidth, 22)
        Using rulerBgBrush As New SolidBrush(Color.FromArgb(245, 246, 250))
            g.FillRectangle(rulerBgBrush, rulerRect)
        End Using

        Using rulerBorderPen As New Pen(Color.FromArgb(200, 205, 215), 1)
            g.DrawLine(rulerBorderPen, trackLeft, trackY - 1, trackLeft + trackWidth, trackY - 1)
        End Using

        Dim startMinorTick As Double = Math.Floor(startTimeInSeconds / minorInterval) * minorInterval
        For tickTime As Double = startMinorTick To endTimeInSeconds Step minorInterval
            If tickTime >= startTimeInSeconds AndAlso tickTime <= endTimeInSeconds Then
                Dim tickX As Integer = trackLeft + CInt(((tickTime - startTimeInSeconds) / visibleTimeRange) * trackWidth)

                If tickX >= trackLeft AndAlso tickX <= trackLeft + trackWidth Then

                    g.DrawLine(minorTickPen, tickX, trackY - 4, tickX, trackY - 1)
                End If
            End If
        Next

        Dim startMediumTick As Double = Math.Floor(startTimeInSeconds / mediumInterval) * mediumInterval
        For tickTime As Double = startMediumTick To endTimeInSeconds Step mediumInterval
            If tickTime >= startTimeInSeconds AndAlso tickTime <= endTimeInSeconds Then
                Dim tickX As Integer = trackLeft + CInt(((tickTime - startTimeInSeconds) / visibleTimeRange) * trackWidth)

                If tickX >= trackLeft AndAlso tickX <= trackLeft + trackWidth Then

                    g.DrawLine(mediumTickPen, tickX, trackY - 7, tickX, trackY - 1)
                End If
            End If
        Next

        Dim startMajorTick As Double = Math.Floor(startTimeInSeconds / majorInterval) * majorInterval
        Dim lastLabelX As Integer = -1000

        For tickTime As Double = startMajorTick To endTimeInSeconds Step majorInterval
            If tickTime >= startTimeInSeconds AndAlso tickTime <= endTimeInSeconds Then
                Dim tickX As Integer = trackLeft + CInt(((tickTime - startTimeInSeconds) / visibleTimeRange) * trackWidth)

                If tickX >= trackLeft AndAlso tickX <= trackLeft + trackWidth Then

                    g.DrawLine(majorTickPen, tickX, trackY - 10, tickX, trackY - 1)

                    Dim timeLabel As String = FormatTimeLabel(tickTime, majorInterval)

                    Dim textSize As SizeF = g.MeasureString(timeLabel, timeFont)
                    Dim labelX As Integer = tickX + 2
                    Dim labelY As Integer = trackY - 19

                    If labelX < trackLeft Then labelX = trackLeft
                    If labelX + CInt(textSize.Width) > trackLeft + trackWidth Then
                        labelX = trackLeft + trackWidth - CInt(textSize.Width) - 1
                    End If

                    If labelX > lastLabelX + 50 Then

                        g.DrawString(timeLabel, timeFont, timeBrush, labelX, labelY)
                        lastLabelX = labelX
                    End If
                End If
            End If
        Next

        timeFont.Dispose()
        timeBrush.Dispose()
        minorTickPen.Dispose()
        mediumTickPen.Dispose()
        majorTickPen.Dispose()
    End Sub

    Private Function GetNiceInterval(ByVal rawInterval As Double) As Double

        Dim niceIntervals As Double() = { _
            0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, _
            1, 2, 5, 10, 15, 30, _
            60, 120, 300, 600, 900, 1800, 3600, 7200 _
        }

        For Each interval As Double In niceIntervals
            If interval >= rawInterval Then
                Return interval
            End If
        Next

        Return niceIntervals(niceIntervals.Length - 1)
    End Function

    Private Function FormatTimeLabel(ByVal seconds As Double, ByVal interval As Double) As String

        Dim roundedSeconds As Double = Math.Round(seconds / interval) * interval
        Dim ts As TimeSpan = TimeSpan.FromSeconds(roundedSeconds)

        If interval < 0.1 Then

            Return String.Format("{0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds)
        ElseIf interval < 1 Then

            Return String.Format("{0:00}:{1:00}.{2:0}", ts.Minutes, ts.Seconds, ts.Milliseconds / 100)
        ElseIf interval < 60 Then

            If ts.Hours > 0 Then
                Return String.Format("{0}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds)
            Else
                Return String.Format("{0:00}:{1:00}", ts.Minutes, ts.Seconds)
            End If
        Else

            Return String.Format("{0}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds)
        End If
    End Function

    Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
        MyBase.OnPaint(e)

        _drawnLabels.Clear()

        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.Default

        Using bgBrush As New LinearGradientBrush(Me.ClientRectangle, Color.FromArgb(248, 249, 252), Color.FromArgb(248, 249, 252), LinearGradientMode.ForwardDiagonal)
            g.FillRectangle(bgBrush, Me.ClientRectangle)
        End Using

        Dim trackY As Integer = 42
        Dim trackHeight As Integer = 12
        Dim trackLeft As Integer = 15
        Dim trackRight As Integer = Me.Width - 15
        Dim trackWidth As Integer = trackRight - trackLeft

        Dim visibleStartValue As Double = GetVisibleStartValue()
        Dim visibleEndValue As Double = GetVisibleEndValue()
        Dim visibleRange As Double = visibleEndValue - visibleStartValue

        DrawTimeRuler(g, trackLeft, trackY, trackWidth, visibleStartValue, visibleEndValue)

        Using shadowBrush As New SolidBrush(Color.FromArgb(40, 0, 0, 0))
            g.FillRectangle(shadowBrush, trackLeft + 1, trackY + 2, trackWidth, trackHeight)
        End Using

        Using trackBrush As New SolidBrush(Color.FromArgb(225, 228, 235))
            g.FillRectangle(trackBrush, trackLeft, trackY, trackWidth, trackHeight)
        End Using

        Using trackPen As New Pen(Color.FromArgb(190, 195, 205), 1)
            g.DrawRectangle(trackPen, trackLeft, trackY, trackWidth, trackHeight)
        End Using

        DrawGridLines(g, trackLeft, trackY, trackWidth, trackHeight, visibleStartValue, visibleEndValue)

        For i As Integer = 0 To _segments.Count - 1
            Dim seg As TrackSegment = _segments(i)

            If seg.EndValue >= visibleStartValue AndAlso seg.StartValue <= visibleEndValue Then

                Dim startX As Integer = trackLeft + CInt(((seg.StartValue - visibleStartValue) / visibleRange) * trackWidth)
                Dim endX As Integer = trackLeft + CInt(((seg.EndValue - visibleStartValue) / visibleRange) * trackWidth)

                If startX < trackLeft Then startX = trackLeft
                If endX > trackRight Then endX = trackRight

                Dim isSelected As Boolean = (i = _selectedSegmentIndex)

                If endX > startX Then
                    Dim segRect As New Rectangle(startX, trackY + 2, endX - startX, trackHeight - 4)
                    Dim segPath As GraphicsPath = CreateRoundedRectangle(segRect, 3)

                    Dim alpha As Integer = If(isSelected, 200, 150)

                    Using segBrush As New SolidBrush(Color.FromArgb(alpha, seg.Color))
                        g.FillPath(segBrush, segPath)
                    End Using

                    Using highlightBrush As New SolidBrush(Color.FromArgb(80, 255, 255, 255))
                        Dim highlightRect As New Rectangle(startX + 2, trackY + 3, Math.Max(1, endX - startX - 4), (trackHeight - 6) \ 2)
                        g.FillRectangle(highlightBrush, highlightRect)
                    End Using

                    Dim borderColor As Color = If(isSelected, Color.Black, Color.FromArgb(220, seg.Color))
                    Dim borderWidth As Single = If(isSelected, 2.0F, 1.0F)

                    Using segPen As New Pen(borderColor, borderWidth)
                        g.DrawPath(segPen, segPath)
                    End Using

                    segPath.Dispose()
                End If

                If seg.StartValue >= visibleStartValue AndAlso seg.StartValue <= visibleEndValue Then
                    DrawMarker(g, startX, trackY + trackHeight \ 2, seg.Color, isSelected, True)
                End If

                If seg.EndValue >= visibleStartValue AndAlso seg.EndValue <= visibleEndValue Then
                    DrawMarker(g, endX, trackY + trackHeight \ 2, seg.Color, isSelected, False)
                End If

                If endX - startX > 20 Then
                    Dim labelText As String = If(endX - startX > 60, "Seg " & (i + 1).ToString(), (i + 1).ToString())
                    Dim labelFont As New Font("Segoe UI", 8, FontStyle.Bold)
                    Dim textSize As SizeF = g.MeasureString(labelText, labelFont)
                    Dim labelWidth As Integer = CInt(textSize.Width) + 14
                    Dim labelHeight As Integer = 20
                    Dim labelX As Integer = startX + (endX - startX - labelWidth) \ 2

                    If labelX < 0 Then labelX = 0
                    If labelX + labelWidth > Me.Width Then labelX = Me.Width - labelWidth - 1

                    Dim labelY As Integer = trackY - labelHeight - 5
                    Dim labelRect As New Rectangle(labelX, labelY, labelWidth, labelHeight)
                    DrawSegmentLabel(g, labelRect, labelText, seg.Color, isSelected)
                    labelFont.Dispose()
                End If
            End If
        Next

        If _segmentOnlyMode AndAlso _segments.Count > 0 Then
            Dim gapStarts As New List(Of Double)
            Dim gapEnds As New List(Of Double)
            Dim sortedSegs As New List(Of TrackSegment)(_segments)
            sortedSegs.Sort(Function(a As TrackSegment, b As TrackSegment) a.StartValue.CompareTo(b.StartValue))

            Dim currentPos As Double = visibleStartValue
            For Each seg As TrackSegment In sortedSegs
                If seg.EndValue < visibleStartValue Then Continue For
                If seg.StartValue > visibleEndValue Then Exit For
                If seg.StartValue > currentPos Then
                    gapStarts.Add(currentPos)
                    gapEnds.Add(seg.StartValue)
                End If
                If seg.EndValue > currentPos Then currentPos = seg.EndValue
            Next
            If currentPos < visibleEndValue Then
                gapStarts.Add(currentPos)
                gapEnds.Add(visibleEndValue)
            End If

            Using dimBrush As New SolidBrush(Color.FromArgb(125, 125, 125, 125))
                For i As Integer = 0 To gapStarts.Count - 1
                    Dim gapStart As Double = Math.Max(gapStarts(i), visibleStartValue)
                    Dim gapEnd As Double = Math.Min(gapEnds(i), visibleEndValue)
                    If gapEnd > gapStart Then
                        Dim gapStartX As Integer = trackLeft + CInt(((gapStart - visibleStartValue) / visibleRange) * trackWidth)
                        Dim gapEndX As Integer = trackLeft + CInt(((gapEnd - visibleStartValue) / visibleRange) * trackWidth)
                        If gapStartX < trackLeft Then gapStartX = trackLeft
                        If gapEndX > trackRight Then gapEndX = trackRight
                        If gapEndX > gapStartX Then
                            g.FillRectangle(dimBrush, gapStartX, trackY, gapEndX - gapStartX, trackHeight)
                        End If
                    End If
                Next
            End Using
        End If

        If _tempStartMarker >= visibleStartValue AndAlso _tempStartMarker <= visibleEndValue Then
            Dim tempStartX As Integer = trackLeft + CInt(((_tempStartMarker - visibleStartValue) / visibleRange) * trackWidth)
            DrawTempMarker(g, tempStartX, trackY, trackHeight, Color.FromArgb(0, 220, 0), "START")
        End If

        If _tempEndMarker >= visibleStartValue AndAlso _tempEndMarker <= visibleEndValue Then
            Dim tempEndX As Integer = trackLeft + CInt(((_tempEndMarker - visibleStartValue) / visibleRange) * trackWidth)
            DrawTempMarker(g, tempEndX, trackY, trackHeight, Color.FromArgb(255, 60, 60), "END")
        End If

        If _value >= visibleStartValue AndAlso _value <= visibleEndValue Then
            Dim playheadX As Integer = trackLeft + CInt(((_value - visibleStartValue) / visibleRange) * trackWidth)
            Dim playheadHeight As Integer = If(_isDraggingPlayhead, 20, 16)

            Using playheadPen As New Pen(Color.FromArgb(0, 120, 215), If(_isDraggingPlayhead, 3.0F, 2.0F))
                g.DrawLine(playheadPen, playheadX, trackY - playheadHeight \ 2, playheadX, trackY + trackHeight + playheadHeight \ 2)
            End Using

            Dim handleSize As Integer = If(_isDraggingPlayhead, 8, 6)
            Dim playheadPath As New GraphicsPath()
            playheadPath.AddPolygon(New Point() { _
                New Point(playheadX - handleSize, trackY - playheadHeight \ 2), _
                New Point(playheadX + handleSize, trackY - playheadHeight \ 2), _
                New Point(playheadX, trackY - 2) _
            })
            Using playheadBrush As New SolidBrush(Color.FromArgb(0, 120, 215))
                g.FillPath(playheadBrush, playheadPath)
            End Using
            playheadPath.Dispose()

            Dim handleCircleSize As Integer = If(_isDraggingPlayhead, 8, 6)
            Dim handleCircleY As Integer = trackY + trackHeight + playheadHeight \ 2
            Using handleCircleBrush As New SolidBrush(Color.FromArgb(0, 120, 215))
                g.FillEllipse(handleCircleBrush, playheadX - handleCircleSize \ 2, handleCircleY - handleCircleSize \ 2, handleCircleSize, handleCircleSize)
            End Using
            Using handleCirclePen As New Pen(Color.White, 1.5F)
                g.DrawEllipse(handleCirclePen, playheadX - handleCircleSize \ 2, handleCircleY - handleCircleSize \ 2, handleCircleSize, handleCircleSize)
            End Using
        End If

        Dim currentTime As Double = 0
        If _videoDuration > 0 Then
            currentTime = (_value / 1000.0) * _videoDuration
        End If

        Dim timeText As String = FormatTime(currentTime)
        Dim timeFont As New Font("Arial", 8, FontStyle.Bold)
        Dim timeBrush As New SolidBrush(Color.FromArgb(50, 50, 70))

        Dim timeBgWidth As Integer = 70
        Dim timeBgHeight As Integer = 18
        Dim timeBgX As Integer = 5
        Dim timeBgY As Integer = trackY + trackHeight + 10

        Dim timeBgRect As New Rectangle(timeBgX, timeBgY, timeBgWidth, timeBgHeight)
        Dim timeBgPath As GraphicsPath = CreateRoundedRectangle(timeBgRect, 3)
        Using timeBgBrush As New SolidBrush(Color.FromArgb(245, 247, 250))
            g.FillPath(timeBgBrush, timeBgPath)
        End Using

        Using timeBgPen As New Pen(Color.FromArgb(200, 205, 215), 1)
            g.DrawPath(timeBgPen, timeBgPath)
        End Using
        timeBgPath.Dispose()

        Dim timeRect As New Rectangle(timeBgX, timeBgY, timeBgWidth, timeBgHeight)
        Dim timeFormat As New StringFormat()
        timeFormat.Alignment = StringAlignment.Center
        timeFormat.LineAlignment = StringAlignment.Center
        g.DrawString(timeText, timeFont, timeBrush, timeRect, timeFormat)
        timeFont.Dispose()
        timeBrush.Dispose()
        timeFormat.Dispose()

        If _videoDuration > 0 Then
            Dim totalTimeText As String = FormatTime(_videoDuration)
            Dim totalFont As New Font("Arial", 8, FontStyle.Regular)
            Dim totalBrush As New SolidBrush(Color.FromArgb(100, 105, 115))

            Dim totalBgWidth As Integer = 70
            Dim totalBgHeight As Integer = 18
            Dim totalBgX As Integer = Me.Width - totalBgWidth - 5
            Dim totalBgY As Integer = trackY + trackHeight + 10

            Dim totalBgRect As New Rectangle(totalBgX, totalBgY, totalBgWidth, totalBgHeight)
            Dim totalBgPath As GraphicsPath = CreateRoundedRectangle(totalBgRect, 3)
            Using totalBgBrush As New SolidBrush(Color.FromArgb(245, 247, 250))
                g.FillPath(totalBgBrush, totalBgPath)
            End Using

            Using totalBgPen As New Pen(Color.FromArgb(200, 205, 215), 1)
                g.DrawPath(totalBgPen, totalBgPath)
            End Using
            totalBgPath.Dispose()

            Dim totalRect As New Rectangle(totalBgX, totalBgY, totalBgWidth, totalBgHeight)
            Dim totalFormat As New StringFormat()
            totalFormat.Alignment = StringAlignment.Center
            totalFormat.LineAlignment = StringAlignment.Center
            g.DrawString(totalTimeText, totalFont, totalBrush, totalRect, totalFormat)
            totalFont.Dispose()
            totalBrush.Dispose()
            totalFormat.Dispose()
        End If

        DrawZoomControls(g, trackY, trackHeight, trackLeft, trackWidth)
    End Sub

    Private Sub DrawGridLines(ByVal g As Graphics, ByVal trackLeft As Integer, ByVal trackY As Integer, ByVal trackWidth As Integer, ByVal trackHeight As Integer, ByVal visibleStart As Double, ByVal visibleEnd As Double)
        Dim visibleRange As Double = visibleEnd - visibleStart

        Dim gridInterval As Double
        If _zoomLevel >= 200 Then
            gridInterval = 1
        ElseIf _zoomLevel >= 100 Then
            gridInterval = 2
        ElseIf _zoomLevel >= 50 Then
            gridInterval = 5
        ElseIf _zoomLevel >= 20 Then
            gridInterval = 10
        ElseIf _zoomLevel >= 10 Then
            gridInterval = 20
        ElseIf _zoomLevel >= 5 Then
            gridInterval = 50
        ElseIf _zoomLevel >= 2 Then
            gridInterval = 100
        Else
            gridInterval = 200
        End If

        Using gridPen As New Pen(Color.FromArgb(60, 100, 100, 120), 1)
            Dim startGridValue As Double = Math.Ceiling(visibleStart / gridInterval) * gridInterval
            For gridValue As Double = startGridValue To visibleEnd Step gridInterval
                Dim gridX As Integer = trackLeft + CInt(((gridValue - visibleStart) / visibleRange) * trackWidth)
                If gridX >= trackLeft AndAlso gridX <= trackLeft + trackWidth Then
                    g.DrawLine(gridPen, gridX, trackY + 2, gridX, trackY + trackHeight - 2)
                End If
            Next
        End Using
    End Sub

    Private Sub DrawZoomControls(ByVal g As Graphics, ByVal trackY As Integer, ByVal trackHeight As Integer, ByVal trackLeft As Integer, ByVal trackWidth As Integer)

        Dim zoomText As String = String.Format("Zoom: {0}%", CInt(_zoomLevel * 100))
        Dim zoomFont As New Font("Arial", 8, FontStyle.Bold)
        Dim zoomBrush As New SolidBrush(Color.FromArgb(100, 105, 115))

        Dim zoomTextSize As SizeF = g.MeasureString(zoomText, zoomFont)
        Dim zoomX As Integer = trackLeft + (trackWidth - CInt(zoomTextSize.Width)) \ 2
        Dim zoomY As Integer = trackY + trackHeight + 8

        g.DrawString(zoomText, zoomFont, zoomBrush, zoomX, zoomY)

        If _zoomLevel > 1.0 Then
            Dim scrollBarY As Integer = trackY + trackHeight + 30
            Dim scrollBarHeight As Integer = 6

            Using scrollBgBrush As New SolidBrush(Color.FromArgb(225, 228, 235))
                g.FillRectangle(scrollBgBrush, trackLeft, scrollBarY, trackWidth, scrollBarHeight)
            End Using

            Dim thumbWidth As Integer = CInt(trackWidth / _zoomLevel)
            If thumbWidth < 20 Then thumbWidth = 20

            Dim visibleStartValue As Double = GetVisibleStartValue()
            Dim thumbX As Integer = trackLeft + CInt((visibleStartValue / (_maximum - _minimum)) * trackWidth)

            If thumbX < trackLeft Then thumbX = trackLeft
            If thumbX + thumbWidth > trackLeft + trackWidth Then thumbX = trackLeft + trackWidth - thumbWidth

            Using thumbBrush As New SolidBrush(Color.FromArgb(0, 120, 215))
                g.FillRectangle(thumbBrush, thumbX, scrollBarY, thumbWidth, scrollBarHeight)
            End Using

            Using thumbPen As New Pen(Color.FromArgb(0, 140, 230), 1)
                g.DrawRectangle(thumbPen, thumbX, scrollBarY, thumbWidth, scrollBarHeight)
            End Using
        End If

        zoomFont.Dispose()
        zoomBrush.Dispose()
    End Sub

    Private Function FormatTime(ByVal seconds As Double) As String
        If seconds < 0 Then seconds = 0
        Dim ts As TimeSpan = TimeSpan.FromSeconds(seconds)
        Return String.Format("{0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds)
    End Function

    Private Function CreateRoundedRectangle(ByVal rect As Rectangle, ByVal radius As Integer) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim d As Integer = radius * 2
        path.AddArc(rect.X, rect.Y, d, d, 180, 90)
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90)
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90)
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90)
        path.CloseFigure()
        Return path
    End Function

    Private Sub DrawMarker(ByVal g As Graphics, ByVal x As Integer, ByVal y As Integer, ByVal color As Color, ByVal isSelected As Boolean, ByVal isStart As Boolean)

        Dim markerHeight As Integer = If(isSelected, 12, 10)

        Dim markerWidth As Integer = If(isSelected, 4, 3)

        Dim markerPath As New GraphicsPath()

        If isStart Then

            markerPath.AddPolygon(New Point() { _
                New Point(x - markerWidth, y - markerHeight), _
                New Point(x, y - markerHeight), _
                New Point(x, y + markerHeight), _
                New Point(x - markerWidth, y + markerHeight) _
            })
        Else

            markerPath.AddPolygon(New Point() { _
                New Point(x, y - markerHeight), _
                New Point(x + markerWidth, y - markerHeight), _
                New Point(x + markerWidth, y + markerHeight), _
                New Point(x, y + markerHeight) _
            })
        End If

        Using shadowBrush As New SolidBrush(Color.FromArgb(80, 0, 0, 0))
            g.TranslateTransform(1, 1)
            g.FillPath(shadowBrush, markerPath)
            g.TranslateTransform(-1, -1)
        End Using

        Using markerBrush As New SolidBrush(color)
            g.FillPath(markerBrush, markerPath)
        End Using

        Dim markerColor As Color = If(isSelected, Color.Black, Color.FromArgb(220, color))
        Using markerPen As New Pen(markerColor, 1.5F)
            g.DrawPath(markerPen, markerPath)
        End Using

        markerPath.Dispose()
    End Sub

    Private Sub DrawSegmentLabel(ByVal g As Graphics, ByVal rect As Rectangle, ByVal text As String, ByVal color As Color, ByVal isSelected As Boolean)
        Dim labelPath As GraphicsPath = CreateRoundedRectangle(rect, 5)
        Dim alpha As Integer = If(isSelected, 220, 180)

        Dim shadowRect As New Rectangle(rect.X + 1, rect.Y + 1, rect.Width, rect.Height)
        Dim shadowPath As GraphicsPath = CreateRoundedRectangle(shadowRect, 5)
        Using shadowBrush As New SolidBrush(Color.FromArgb(100, 0, 0, 0))
            g.FillPath(shadowBrush, shadowPath)
        End Using
        shadowPath.Dispose()

        Using labelBgBrush As New SolidBrush(Color.FromArgb(alpha, color))
            g.FillPath(labelBgBrush, labelPath)
        End Using

        Dim labelColor As Color = If(isSelected, Color.White, Color.FromArgb(0, 0, 0))
        Using labelPen As New Pen(labelColor, 1.0F)
            g.DrawPath(labelPen, labelPath)
        End Using

        Using labelBrush As New SolidBrush(Color.White)
            Using labelFont As New Font("Segoe UI", 8, FontStyle.Bold)
                Dim sf As New StringFormat()
                sf.Alignment = StringAlignment.Center
                sf.LineAlignment = StringAlignment.Center
                sf.FormatFlags = StringFormatFlags.NoClip
                g.DrawString(text, labelFont, labelBrush, rect, sf)
                sf.Dispose()
            End Using
        End Using

        labelPath.Dispose()
    End Sub

    Private Sub DrawTempMarker(ByVal g As Graphics, ByVal x As Integer, ByVal trackY As Integer, ByVal trackHeight As Integer, ByVal color As Color, ByVal label As String)

        Using tempPen As New Pen(color, 2.0F)
            g.DrawLine(tempPen, x, trackY - 10, x, trackY + trackHeight + 10)
        End Using

        Dim markerWidth As Integer = 3
        Dim markerHeight As Integer = 10
        Dim markerY As Integer = trackY + (trackHeight - markerHeight) \ 2

        Dim markerRect As New Rectangle(x - markerWidth, markerY, markerWidth * 2, markerHeight)

        Using shadowBrush As New SolidBrush(Color.FromArgb(80, 0, 0, 0))
            g.FillRectangle(shadowBrush, markerRect.X + 1, markerRect.Y + 1, markerRect.Width, markerRect.Height)
        End Using

        Using markerBrush As New SolidBrush(color)
            g.FillRectangle(markerBrush, markerRect)
        End Using

        Using markerPen As New Pen(Color.White, 1.0F)
            g.DrawRectangle(markerPen, markerRect)
        End Using

        If Not String.IsNullOrEmpty(label) Then
            Dim labelFont As New Font("Segoe UI", 7, FontStyle.Bold)
            Dim textSize As SizeF = g.MeasureString(label, labelFont)
            Dim labelWidth As Integer = CInt(textSize.Width) + 12
            Dim labelHeight As Integer = CInt(textSize.Height) + 6
            Dim labelX As Integer = x - labelWidth \ 2
            Dim labelY As Integer = trackY - labelHeight - 8

            If labelX < 0 Then labelX = 0
            If labelX + labelWidth > Me.Width Then labelX = Me.Width - labelWidth - 1

            Dim labelRect As New Rectangle(labelX, labelY, labelWidth, labelHeight)
            Dim labelPath As GraphicsPath = CreateRoundedRectangle(labelRect, 3)

            Dim shadowRect As New Rectangle(labelRect.X + 1, labelRect.Y + 1, labelRect.Width, labelRect.Height)
            Dim shadowPath As GraphicsPath = CreateRoundedRectangle(shadowRect, 3)
            Using shadowBrush As New SolidBrush(Color.FromArgb(80, 0, 0, 0))
                g.FillPath(shadowBrush, shadowPath)
            End Using
            shadowPath.Dispose()

            Using labelBgBrush As New SolidBrush(color)
                g.FillPath(labelBgBrush, labelPath)
            End Using

            Using labelPen As New Pen(Color.White, 1.0F)
                g.DrawPath(labelPen, labelPath)
            End Using

            Using labelBrush As New SolidBrush(Color.White)
                Dim sf As New StringFormat()
                sf.Alignment = StringAlignment.Center
                sf.LineAlignment = StringAlignment.Center
                sf.FormatFlags = StringFormatFlags.NoClip
                g.DrawString(label, labelFont, labelBrush, labelRect, sf)
                sf.Dispose()
            End Using

            labelPath.Dispose()
            labelFont.Dispose()
        End If
    End Sub

    Private Function IsValueInSegmentRegion(ByVal value As Double) As Boolean
        For i As Integer = 0 To _segments.Count - 1
            Dim seg As TrackSegment = _segments(i)
            If value >= seg.StartValue AndAlso value <= seg.EndValue Then
                Return True
            End If
        Next
        Return False
    End Function

    Private Function ClampValueToNearestSegment(ByVal value As Double) As Double
        If _segments.Count = 0 Then Return value
        For i As Integer = 0 To _segments.Count - 1
            Dim seg As TrackSegment = _segments(i)
            If value >= seg.StartValue AndAlso value <= seg.EndValue Then
                Return value
            End If
        Next
        Dim bestIdx As Integer = -1
        Dim bestDist As Double = Double.MaxValue
        For i As Integer = 0 To _segments.Count - 1
            Dim seg As TrackSegment = _segments(i)
            If value < seg.StartValue Then
                Dim d As Double = seg.StartValue - value
                If d < bestDist Then
                    bestDist = d
                    bestIdx = i
                End If
            ElseIf value > seg.EndValue Then
                Dim d As Double = value - seg.EndValue
                If d < bestDist Then
                    bestDist = d
                    bestIdx = i
                End If
            End If
        Next
        If bestIdx < 0 Then Return value
        Dim bs As TrackSegment = _segments(bestIdx)
        If value < bs.StartValue Then
            Return bs.StartValue
        Else
            Return bs.EndValue
        End If
    End Function

    Protected Overrides Sub OnMouseDown(ByVal e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left Then
            Dim trackLeft As Integer = 15
            Dim trackRight As Integer = Me.Width - 15
            Dim trackWidth As Integer = trackRight - trackLeft
            Dim trackY As Integer = 42
            Dim trackHeight As Integer = 12

            Dim clickedX As Integer = e.X
            Dim clickedY As Integer = e.Y

            Dim visibleStartValue As Double = GetVisibleStartValue()
            Dim visibleEndValue As Double = GetVisibleEndValue()
            Dim visibleRange As Double = visibleEndValue - visibleStartValue

            If _zoomLevel > 1.0 Then
                Dim scrollBarY As Integer = trackY + trackHeight + 30
                Dim scrollBarHeight As Integer = 6

                If clickedY >= scrollBarY - 5 AndAlso clickedY <= scrollBarY + scrollBarHeight + 5 Then
                    _isDraggingScrollbar = True
                    _lastMouseX = clickedX
                    Me.Capture = True
                    Me.Cursor = Cursors.SizeWE

                    Dim scrollPercentage As Double = (clickedX - trackLeft) / trackWidth
                    scrollPercentage = Math.Max(0.0, Math.Min(1.0, scrollPercentage))

                    Dim totalRange As Double = _maximum - _minimum
                    _zoomCenter = scrollPercentage
                    _zoomCenter = Math.Max(0.0, Math.Min(1.0, _zoomCenter))

                    Invalidate()
                    RaiseEvent ZoomChanged(Me, EventArgs.Empty)
                    Return
                End If
            End If

            Dim trackAreaTop As Integer = trackY - 35
            Dim trackAreaBottom As Integer = trackY + trackHeight + 25

            Dim playheadX As Integer = trackLeft + CInt(((_value - visibleStartValue) / visibleRange) * trackWidth)
            Dim isNearPlayhead As Boolean = Math.Abs(clickedX - playheadX) <= 15 AndAlso
                                            clickedY >= trackAreaTop AndAlso clickedY <= trackAreaBottom

            If isNearPlayhead AndAlso (Not _segmentOnlyMode OrElse IsValueInSegmentRegion(_value)) Then
                _draggingMarker = 0
                _isDraggingPlayhead = True
                _lastMouseX = clickedX
                Me.Capture = True
                UpdateValueFromMouse(e.X)
                RaiseEvent PlayheadDragStart(Me, EventArgs.Empty)
                Me.Focus()
                Return
            End If

            For i As Integer = _segments.Count - 1 To 0 Step -1
                Dim seg As TrackSegment = _segments(i)

                If seg.EndValue >= visibleStartValue AndAlso seg.StartValue <= visibleEndValue Then
                    Dim startX As Integer = trackLeft + CInt(((seg.StartValue - visibleStartValue) / visibleRange) * trackWidth)
                    Dim endX As Integer = trackLeft + CInt(((seg.EndValue - visibleStartValue) / visibleRange) * trackWidth)

                    If startX < trackLeft Then startX = trackLeft
                    If endX > trackRight Then endX = trackRight

                    If Math.Abs(clickedX - startX) <= 15 AndAlso clickedY >= trackAreaTop AndAlso clickedY <= trackAreaBottom Then
                        _draggingMarker = 1
                        _isDraggingSegment = True
                        _selectedSegmentIndex = i
                        _lastMouseX = clickedX
                        Me.Cursor = Cursors.SizeWE
                        Me.Capture = True
                        Invalidate()
                        Return
                    End If

                    If Math.Abs(clickedX - endX) <= 15 AndAlso clickedY >= trackAreaTop AndAlso clickedY <= trackAreaBottom Then
                        _draggingMarker = 2
                        _isDraggingSegment = True
                        _selectedSegmentIndex = i
                        _lastMouseX = clickedX
                        Me.Cursor = Cursors.SizeWE
                        Me.Capture = True
                        Invalidate()
                        Return
                    End If
                End If
            Next

            For i As Integer = _segments.Count - 1 To 0 Step -1
                Dim seg As TrackSegment = _segments(i)

                If seg.EndValue >= visibleStartValue AndAlso seg.StartValue <= visibleEndValue Then
                    Dim startX As Integer = trackLeft + CInt(((seg.StartValue - visibleStartValue) / visibleRange) * trackWidth)
                    Dim endX As Integer = trackLeft + CInt(((seg.EndValue - visibleStartValue) / visibleRange) * trackWidth)

                    If startX < trackLeft Then startX = trackLeft
                    If endX > trackRight Then endX = trackRight

                    If clickedX >= startX + 15 AndAlso clickedX <= endX - 15 AndAlso _
                       clickedY >= trackAreaTop AndAlso clickedY <= trackAreaBottom Then
                        _selectedSegmentIndex = i
                        Invalidate()
                        RaiseEvent SegmentClicked(Me, i)
                        Return
                    End If
                End If
            Next

            If clickedY >= trackAreaTop AndAlso clickedY <= trackAreaBottom Then
                Dim clickedValue As Double
                Dim percentage As Double = (clickedX - trackLeft) / trackWidth
                If percentage < 0 Then percentage = 0
                If percentage > 1 Then percentage = 1
                clickedValue = visibleStartValue + percentage * visibleRange

                If _segmentOnlyMode AndAlso _segments.Count > 0 AndAlso Not IsValueInSegmentRegion(clickedValue) Then
                    Me.Cursor = Cursors.No
                    Return
                End If

                _draggingMarker = 0
                _isDraggingPlayhead = True
                _lastMouseX = clickedX
                Me.Capture = True
                UpdateValueFromMouse(e.X)
                RaiseEvent PlayheadDragStart(Me, EventArgs.Empty)
                RaiseEvent PlayheadDragging(Me, EventArgs.Empty)
                Me.Focus()
            End If
        ElseIf e.Button = MouseButtons.Right Then
            ZoomIn()
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(ByVal e As MouseEventArgs)
        MyBase.OnMouseMove(e)

        Dim trackLeft As Integer = 15
        Dim trackRight As Integer = Me.Width - 15
        Dim trackWidth As Integer = trackRight - trackLeft
        Dim trackY As Integer = 42
        Dim trackHeight As Integer = 12

        Dim trackAreaTop As Integer = trackY - 35
        Dim trackAreaBottom As Integer = trackY + trackHeight + 25

        Dim visibleStartValue As Double = GetVisibleStartValue()
        Dim visibleEndValue As Double = GetVisibleEndValue()
        Dim visibleRange As Double = visibleEndValue - visibleStartValue

        If e.Button = MouseButtons.Left Then
            If _isDraggingScrollbar Then
                Dim totalRange As Double = _maximum - _minimum
                Dim scrollPercentage As Double = (e.X - trackLeft) / trackWidth
                scrollPercentage = Math.Max(0.0, Math.Min(1.0, scrollPercentage))
                _zoomCenter = scrollPercentage
                _lastMouseX = e.X
                Invalidate()
                RaiseEvent ZoomChanged(Me, EventArgs.Empty)
                Return
            End If

            If _isDraggingPlayhead Then
                If _segmentOnlyMode AndAlso _segments.Count > 0 Then
                    Dim percentage As Double = (e.X - trackLeft) / trackWidth
                    If percentage < 0 Then percentage = 0
                    If percentage > 1 Then percentage = 1
                    Dim targetValue As Double = visibleStartValue + percentage * visibleRange
                    If IsValueInSegmentRegion(targetValue) Then
                        UpdateValueFromMouse(e.X)
                    Else
                        Dim clamped As Double = ClampValueToNearestSegment(targetValue)
                        Dim clampedPct As Double = (clamped - visibleStartValue) / visibleRange
                        If clampedPct < 0 Then clampedPct = 0
                        If clampedPct > 1 Then clampedPct = 1
                        Dim clampedX As Integer = trackLeft + CInt(clampedPct * trackWidth)
                        UpdateValueFromMouse(clampedX)
                    End If
                Else
                    UpdateValueFromMouse(e.X)
                End If
                _lastMouseX = e.X
                RaiseEvent PlayheadDragging(Me, EventArgs.Empty)
                Return
            ElseIf _draggingMarker = 1 AndAlso _selectedSegmentIndex >= 0 AndAlso _selectedSegmentIndex < _segments.Count Then
                Dim percentage As Double = (e.X - trackLeft) / trackWidth
                If percentage < 0 Then percentage = 0
                If percentage > 1 Then percentage = 1

                Dim newValue As Double = visibleStartValue + percentage * visibleRange
                Dim seg As TrackSegment = _segments(_selectedSegmentIndex)
                If newValue < seg.EndValue - Math.Max(1, visibleRange / trackWidth * 5) Then
                    seg.StartValue = newValue
                    _segments(_selectedSegmentIndex) = seg
                    _lastMouseX = e.X
                    Invalidate()
                    RaiseEvent SegmentChanged(Me, _selectedSegmentIndex, seg.StartValue, seg.EndValue)
                End If
                Return
            ElseIf _draggingMarker = 2 AndAlso _selectedSegmentIndex >= 0 AndAlso _selectedSegmentIndex < _segments.Count Then
                Dim percentage As Double = (e.X - trackLeft) / trackWidth
                If percentage < 0 Then percentage = 0
                If percentage > 1 Then percentage = 1

                Dim newValue As Double = visibleStartValue + percentage * visibleRange
                Dim seg As TrackSegment = _segments(_selectedSegmentIndex)
                If newValue > seg.StartValue + Math.Max(1, visibleRange / trackWidth * 5) Then
                    seg.EndValue = newValue
                    _segments(_selectedSegmentIndex) = seg
                    _lastMouseX = e.X
                    Invalidate()
                    RaiseEvent SegmentChanged(Me, _selectedSegmentIndex, seg.StartValue, seg.EndValue)
                End If
                Return
            End If
        Else

            Dim mouseX As Integer = e.X
            Dim mouseY As Integer = e.Y

            Dim isOverPlayhead As Boolean = False
            Dim isOverMarker As Boolean = False
            Dim isOverScrollbar As Boolean = False

            If _zoomLevel > 1.0 Then
                Dim scrollBarY As Integer = trackY + trackHeight + 30
                Dim scrollBarHeight As Integer = 6
                If mouseY >= scrollBarY - 5 AndAlso mouseY <= scrollBarY + scrollBarHeight + 5 Then
                    isOverScrollbar = True
                End If
            End If

            If Not isOverScrollbar Then
                Dim playheadX As Integer = trackLeft + CInt(((_value - visibleStartValue) / visibleRange) * trackWidth)
                If Math.Abs(mouseX - playheadX) <= 15 AndAlso mouseY >= trackAreaTop AndAlso mouseY <= trackAreaBottom Then
                    isOverPlayhead = True
                End If
            End If

            If Not isOverScrollbar AndAlso Not isOverPlayhead Then
                For i As Integer = 0 To _segments.Count - 1
                    Dim seg As TrackSegment = _segments(i)
                    If seg.EndValue >= visibleStartValue AndAlso seg.StartValue <= visibleEndValue Then
                        Dim startX As Integer = trackLeft + CInt(((seg.StartValue - visibleStartValue) / visibleRange) * trackWidth)
                        Dim endX As Integer = trackLeft + CInt(((seg.EndValue - visibleStartValue) / visibleRange) * trackWidth)

                        If startX < trackLeft Then startX = trackLeft
                        If endX > trackRight Then endX = trackRight

                        If (Math.Abs(mouseX - startX) <= 15 OrElse Math.Abs(mouseX - endX) <= 15) AndAlso _
                           mouseY >= trackAreaTop AndAlso mouseY <= trackAreaBottom Then
                            isOverMarker = True
                            Exit For
                        End If
                    End If
                Next
            End If

            If isOverScrollbar Then
                Me.Cursor = Cursors.SizeWE
            ElseIf isOverPlayhead Then
                Me.Cursor = Cursors.SizeWE
            ElseIf isOverMarker Then
                Me.Cursor = Cursors.SizeWE
            ElseIf _segmentOnlyMode AndAlso _segments.Count > 0 AndAlso _
                   mouseY >= trackAreaTop AndAlso mouseY <= trackAreaBottom Then
                Dim hoverPct As Double = (mouseX - trackLeft) / trackWidth
                If hoverPct < 0 Then hoverPct = 0
                If hoverPct > 1 Then hoverPct = 1
                Dim hoverValue As Double = visibleStartValue + hoverPct * visibleRange
                If IsValueInSegmentRegion(hoverValue) Then
                    Me.Cursor = Cursors.Default
                Else
                    Me.Cursor = Cursors.No
                End If
            Else
                Me.Cursor = Cursors.Default
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(ByVal e As MouseEventArgs)
        MyBase.OnMouseUp(e)

        If _isDraggingScrollbar Then
            _isDraggingScrollbar = False
            Me.Capture = False
            Me.Cursor = Cursors.Default
            Invalidate()
            Return
        End If

        If _isDraggingPlayhead Then
            _isDraggingPlayhead = False
            Me.Capture = False
            Me.Cursor = Cursors.Default
            RaiseEvent PlayheadDragEnd(Me, EventArgs.Empty)
            RaiseEvent Scroll(Me, EventArgs.Empty)
            Invalidate()
        End If

        If _draggingMarker >= 0 Then
            _draggingMarker = -1
            _isDraggingSegment = False
            Me.Capture = False
            Me.Cursor = Cursors.Default
            RaiseEvent Scroll(Me, EventArgs.Empty)
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseWheel(ByVal e As MouseEventArgs)
        MyBase.OnMouseWheel(e)

        Dim trackLeft As Integer = 15
        Dim trackRight As Integer = Me.Width - 15
        Dim trackWidth As Integer = trackRight - trackLeft
        Dim trackY As Integer = 42
        Dim trackHeight As Integer = 12

        Dim mouseX As Integer = e.X
        Dim mouseY As Integer = e.Y

        If mouseX < 0 OrElse mouseX > Me.Width OrElse mouseY < 0 OrElse mouseY > Me.Height Then
            Return
        End If

        Dim percentage As Double = (mouseX - trackLeft) / trackWidth
        percentage = Math.Max(0.0, Math.Min(1.0, percentage))

        Dim visibleStartValue As Double = GetVisibleStartValue()
        Dim visibleEndValue As Double = GetVisibleEndValue()
        Dim currentVisibleRange As Double = visibleEndValue - visibleStartValue
        Dim valueAtMouse As Double = visibleStartValue + percentage * currentVisibleRange

        Dim oldZoomLevel As Double = _zoomLevel

        If e.Delta > 0 Then

            _zoomLevel = Math.Min(_maxZoomLevel, _zoomLevel * _zoomStep)
        Else

            _zoomLevel = Math.Max(_minZoomLevel, _zoomLevel / _zoomStep)
        End If

        If _zoomLevel <> oldZoomLevel Then
            If _zoomLevel <= 1.0 Then

                _zoomCenter = 0.5
            Else

                Dim totalRange As Double = _maximum - _minimum
                If totalRange > 0 Then
                    Dim newVisibleRange As Double = totalRange / _zoomLevel
                    Dim newCenterValue As Double = valueAtMouse - (percentage - 0.5) * newVisibleRange
                    _zoomCenter = newCenterValue / totalRange
                    _zoomCenter = Math.Max(0.0, Math.Min(1.0, _zoomCenter))
                End If
            End If

            Invalidate()
            RaiseEvent ZoomChanged(Me, EventArgs.Empty)
        End If
    End Sub
    Protected Overrides Function IsInputKey(ByVal keyData As Keys) As Boolean

        If keyData = Keys.Left OrElse keyData = Keys.Right OrElse _
           keyData = Keys.Up OrElse keyData = Keys.Down Then
            Return True
        End If
        Return MyBase.IsInputKey(keyData)
    End Function

    Protected Overrides Sub OnKeyDown(ByVal e As KeyEventArgs)
        MyBase.OnKeyDown(e)

        If _zoomLevel > 1.0 Then
            Select Case e.KeyCode
                Case Keys.Left

                    ScrollView(-0.1)
                    e.Handled = True
                Case Keys.Right

                    ScrollView(0.1)
                    e.Handled = True
                Case Keys.Up

                    ZoomIn()
                    e.Handled = True
                Case Keys.Down

                    ZoomOut()
                    e.Handled = True
                Case Keys.Home

                    ScrollToValue(_minimum)
                    e.Handled = True
                Case Keys.End

                    ScrollToValue(_maximum)
                    e.Handled = True
            End Select
        Else

            Select Case e.KeyCode
                Case Keys.Left
                    Value = Math.Max(_minimum, Value - 1)
                    e.Handled = True
                Case Keys.Right
                    Value = Math.Min(_maximum, Value + 1)
                    e.Handled = True
            End Select
        End If
    End Sub

    Protected Overrides Sub OnMouseEnter(ByVal e As EventArgs)
        MyBase.OnMouseEnter(e)

        If Not Me.Focused Then
            Me.Focus()
        End If
    End Sub
    Protected Overrides Sub OnMouseHover(ByVal e As EventArgs)
        MyBase.OnMouseHover(e)

        If Me.CanFocus AndAlso Not Me.Focused Then
            Me.Focus()
        End If
    End Sub

    Private Sub UpdateValueFromMouse(ByVal mouseX As Integer)
        Dim trackLeft As Integer = 15
        Dim trackRight As Integer = Me.Width - 15
        Dim trackWidth As Integer = trackRight - trackLeft

        If trackWidth > 0 Then
            Dim percentage As Double = (mouseX - trackLeft) / trackWidth
            If percentage < 0 Then percentage = 0
            If percentage > 1 Then percentage = 1

            Dim visibleStartValue As Double = GetVisibleStartValue()
            Dim visibleEndValue As Double = GetVisibleEndValue()
            Dim visibleRange As Double = visibleEndValue - visibleStartValue

            Value = CInt(visibleStartValue + percentage * visibleRange)
        End If
    End Sub

    Private _videoDuration As Double = 0

    Public Property VideoDuration() As Double
        Get
            Return _videoDuration
        End Get
        Set(ByVal value As Double)
            _videoDuration = value
            Invalidate()
        End Set
    End Property
End Class