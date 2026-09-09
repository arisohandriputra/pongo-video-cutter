' ==================================================================*
'  Pongo Video Cutter - DirectShow-based Video Preview Player
'  -----------------------------------------------------------------
'  Author  : Ari Sohandri Putra
'  Web     : https://pongo.my.id
'  GitHub  : https://github.com/arisohandriputra
'  License : MIT (see LICENSE file or pongo.my.id/license.htm)
' -----------------------------------------------------------------
'  Wraps DirectShowLib-2005 to render a video preview inside a WinForms
'  panel. Exposes Play / Pause / Stop / Seek and the Volume control,
'  and fires PositionChanged / PlaybackEnded events back to Form1.
' ==================================================================*

Imports System.Runtime.InteropServices
Imports DirectShowLib
Imports System.IO
Imports System.Security.Principal

Public Class VideoPlayer
    Inherits System.Windows.Forms.Control

    Private graphBuilder As IGraphBuilder = Nothing
    Private mediaControl As IMediaControl = Nothing
    Private mediaPosition As IMediaPosition = Nothing
    Private mediaEvent As IMediaEvent = Nothing
    Private videoWindow As IVideoWindow = Nothing
    Private basicVideo As IBasicVideo = Nothing
    Private basicAudio As IBasicAudio = Nothing
    Private mediaSeeking As IMediaSeeking = Nothing
    Private WithEvents logoPictureBox As New PictureBox()

    Private videoWidth As Integer = 0
    Private videoHeight As Integer = 0
    Private aspectRatio As Double = 1.0

    Private videoFile As String = ""
    Private _isPlaying As Boolean = False
    Private hasVideo As Boolean = False
    Private _currentPosition As Double = 0
    Private _volume As Integer = 0
    Private _videoDuration As Double = 0
    Private _frameRate As Double = 25.0

    Private WithEvents positionTimer As New System.Windows.Forms.Timer()
    Private WithEvents colorTimer As New System.Windows.Forms.Timer()
    Private colorIndex As Integer = 0

    Private colors As Color() = { _
        Color.FromArgb(0, 120, 215), Color.FromArgb(118, 75, 162), Color.FromArgb(234, 88, 12), Color.FromArgb(0, 153, 136), _
        Color.FromArgb(232, 17, 35), Color.FromArgb(0, 156, 76), Color.FromArgb(45, 137, 239), Color.FromArgb(123, 60, 230) _
    }

    Public Event PositionChanged(ByVal position As Double)
    Public Event PlaybackEnded()
    Public Event FrameStepped(ByVal newPosition As Double)

    Private lavFiltersPath As String = ""

    Public Sub New()
        MyBase.New()
        Me.SetStyle(ControlStyles.AllPaintingInWmPaint, True)
        Me.SetStyle(ControlStyles.OptimizedDoubleBuffer, True)
        Me.SetStyle(ControlStyles.ResizeRedraw, True)
        Me.SetStyle(ControlStyles.UserPaint, True)
        Me.BackColor = Color.Black
        Me.Size = New Size(640, 360)

        Try
            logoPictureBox.Image = My.Resources.animated
            logoPictureBox.SizeMode = PictureBoxSizeMode.Zoom
            logoPictureBox.BackColor = Color.Transparent
            logoPictureBox.Visible = False
            Me.Controls.Add(logoPictureBox)
        Catch ex As Exception
            Debug.WriteLine("Failed to load animated logo: " & ex.Message)
        End Try

        colorTimer.Interval = 300
        colorTimer.Enabled = True
        colorTimer.Start()

        positionTimer.Interval = 50
        AddHandler positionTimer.Tick, AddressOf PositionTimer_Tick

        CheckAndInstallLAVFilters()
    End Sub

    Private Function IsLAVFiltersRegistered() As Boolean
        Try
            Dim key As Microsoft.Win32.RegistryKey = _
                Microsoft.Win32.Registry.ClassesRoot.OpenSubKey("CLSID\{171252A0-8820-4AFE-9DF8-5C92B2D66B04}")

            If key IsNot Nothing Then
                key.Close()
                Debug.WriteLine("LAV Filters already registered")
                Return True
            End If

            Debug.WriteLine("LAV Filters not registered")
            Return False

        Catch ex As Exception
            Debug.WriteLine("IsLAVFiltersRegistered error: " & ex.Message)
            Return False
        End Try
    End Function

    Private Sub CheckAndInstallLAVFilters()
        Try
            If IsLAVFiltersRegistered() Then
                Debug.WriteLine("LAV Filters already installed, no need to register")
                Return
            End If

            Dim lavFolder As String = FindLAVFiltersFolder()

            If String.IsNullOrEmpty(lavFolder) Then
                Debug.WriteLine("LAV Filters folder not found!")
                Return
            End If

            Debug.WriteLine("LAV Filters found at: " & lavFolder)
            Debug.WriteLine("LAV Filters not registered, requesting admin rights...")

            Dim batchFile As String = Path.Combine(Path.GetTempPath(), "register_lav_filters.bat")
            Dim sb As New System.Text.StringBuilder()

            sb.AppendLine("@echo off")
            sb.AppendLine("cd /d """ & lavFolder & """")

            Dim axFiles As String() = Directory.GetFiles(lavFolder, "*.ax")

            If axFiles.Length = 0 Then
                Debug.WriteLine("No .ax files found in: " & lavFolder)
                Return
            End If

            For Each axFile As String In axFiles
                sb.AppendLine("regsvr32 /s """ & axFile & """")
                Debug.WriteLine("Will register: " & Path.GetFileName(axFile))
            Next

            sb.AppendLine("exit")

            File.WriteAllText(batchFile, sb.ToString())

            Dim psi As New ProcessStartInfo()
            psi.FileName = batchFile
            psi.UseShellExecute = True
            psi.Verb = "runas"
            psi.WorkingDirectory = lavFolder
            psi.CreateNoWindow = False

            Debug.WriteLine("Requesting administrator privileges...")

            Try
                Dim proc As Process = Process.Start(psi)
                If proc IsNot Nothing Then
                    proc.WaitForExit()
                    proc.Close()
                    Debug.WriteLine("LAV Filters registration completed")

                    Try
                        File.Delete(batchFile)
                    Catch ex As Exception
                    End Try

                    MessageBox.Show("Video codec (LAV Filters) successfully applied!" & vbNewLine & vbNewLine & _
                                  "Find more information at https://pongo.my.id/info.htm", _
                                  "LAV Filters Applied", MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If
            Catch ex As Exception
                Debug.WriteLine("User declined admin rights: " & ex.Message)

                Try
                    File.Delete(batchFile)
                Catch ex2 As Exception
                End Try

                MessageBox.Show("LAV Filters is not installed." & vbNewLine & vbNewLine & _
                              "To install LAV Filters, please run the .bat file" & vbNewLine & _
                              "in the LAVFilters folder as Administrator.", _
                              "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try

        Catch ex As Exception
            Debug.WriteLine("CheckAndInstallLAVFilters error: " & ex.Message)
        End Try
    End Sub

    Private Function FindLAVFiltersFolder() As String
        Try
            Dim possiblePaths As New List(Of String)()

            Dim appPath As String = AppDomain.CurrentDomain.BaseDirectory
            possiblePaths.Add(appPath & "LAVFilters")
            possiblePaths.Add(appPath & "LAVFilters\x86")
            possiblePaths.Add(appPath & "LAVFilters\x64")

            Try
                Dim projectPath As String = Directory.GetParent(Directory.GetParent(appPath).FullName).FullName
                possiblePaths.Add(projectPath & "\LAVFilters")
                possiblePaths.Add(projectPath & "\LAVFilters\x86")
                possiblePaths.Add(projectPath & "\LAVFilters\x64")
            Catch ex As Exception
            End Try

            For Each path As String In possiblePaths
                If Directory.Exists(path) Then
                    Dim axFiles As String() = Directory.GetFiles(path, "*.ax", SearchOption.TopDirectoryOnly)
                    If axFiles.Length > 0 Then
                        Debug.WriteLine("Found LAV Filters in: " & path)
                        Return path
                    End If
                End If
            Next

            If Directory.Exists(appPath) Then
                Dim axFiles As String() = Directory.GetFiles(appPath, "*.ax", SearchOption.AllDirectories)
                If axFiles.Length > 0 Then
                    Dim foundFolder As String = Path.GetDirectoryName(axFiles(0))
                    Debug.WriteLine("Found .ax files in: " & foundFolder)
                    Return foundFolder
                End If
            End If

            Debug.WriteLine("LAV Filters folder not found!")
            Return ""

        Catch ex As Exception
            Debug.WriteLine("FindLAVFiltersFolder error: " & ex.Message)
            Return ""
        End Try
    End Function

    Public Function LoadVideo(ByVal filePath As String) As Boolean
        Try
            Cleanup()

            If Not Me.IsHandleCreated Then
                Me.CreateControl()
            End If

            videoFile = filePath
            hasVideo = False

            graphBuilder = DirectCast(New FilterGraph(), IGraphBuilder)

            Dim hr As Integer = -1
            Try
                hr = graphBuilder.RenderFile(filePath, Nothing)
                Debug.WriteLine("RenderFile result: 0x" & hr.ToString("X8"))
            Catch ex As Exception
                Debug.WriteLine("RenderFile exception: " & ex.Message)
            End Try

            If hr < 0 Then
                Debug.WriteLine("Failed to render file: 0x" & hr.ToString("X8"))

                If Not IsLAVFiltersRegistered() Then
                    MessageBox.Show("LAV Filters is not installed!" & vbNewLine & vbNewLine & _
                                  "Please restart the application and allow LAV Filters installation.", _
                                  "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End If

                hasVideo = False
                Me.Invalidate()
                Return False
            End If

            Try
                mediaControl = DirectCast(graphBuilder, IMediaControl)
                mediaPosition = DirectCast(graphBuilder, IMediaPosition)
                mediaEvent = DirectCast(graphBuilder, IMediaEvent)
                videoWindow = DirectCast(graphBuilder, IVideoWindow)
                basicVideo = DirectCast(graphBuilder, IBasicVideo)
                basicAudio = DirectCast(graphBuilder, IBasicAudio)
                mediaSeeking = DirectCast(graphBuilder, IMediaSeeking)
                If basicAudio IsNot Nothing Then
                    basicAudio.put_Volume(_volume)
                End If
            Catch ex As Exception
                Debug.WriteLine("Failed to get interfaces: " & ex.Message)
            End Try

            GetVideoDimensions()
            GetDurationFromGraph()
            GetFrameRateFromGraph()
            SetupVideoWindow()

            hasVideo = True
            positionTimer.Start()

            Me.Invalidate()
            Application.DoEvents()

            Debug.WriteLine("Video loaded successfully")
            Return True

        Catch ex As Exception
            Debug.WriteLine("LoadVideo error: " & ex.Message)
            hasVideo = False
            Me.Invalidate()
            Return False
        End Try
    End Function

    Private Sub GetVideoDimensions()
        Try
            If basicVideo IsNot Nothing Then
                Dim width As Integer = 0
                Dim height As Integer = 0
                Dim hr As Integer = basicVideo.GetVideoSize(width, height)

                If hr >= 0 AndAlso width > 0 AndAlso height > 0 Then
                    videoWidth = width
                    videoHeight = height
                    aspectRatio = width / height
                    Debug.WriteLine("Video size: " & width & "x" & height)
                End If
            End If
        Catch ex As Exception
            Debug.WriteLine("GetVideoDimensions error: " & ex.Message)
        End Try
    End Sub

    Private Sub GetDurationFromGraph()
        Try
            If mediaPosition IsNot Nothing Then
                Dim duration As Double = 0
                Dim hr As Integer = mediaPosition.get_Duration(duration)
                If hr >= 0 AndAlso duration > 0 Then
                    _videoDuration = duration
                End If
            End If
        Catch ex As Exception
        End Try
    End Sub

    Private Sub GetFrameRateFromGraph()
        Try
            If mediaSeeking IsNot Nothing Then
                Dim avgFrameDuration As Long = 0
                Dim fmt As Guid = New Guid("36578495-0010-0010-8000-00AA00389B71")
                Dim hr As Integer = mediaSeeking.ConvertTimeFormat(avgFrameDuration, fmt, 0, Nothing)
                If hr >= 0 AndAlso avgFrameDuration > 0 Then
                    _frameRate = 10000000.0 / avgFrameDuration
                    Debug.WriteLine("Frame rate: " & _frameRate.ToString("F2") & " fps")
                End If
            End If
        Catch ex As Exception
            _frameRate = 25.0
        End Try
    End Sub

    Public ReadOnly Property FrameRate() As Double
        Get
            Return _frameRate
        End Get
    End Property

    Public ReadOnly Property FrameDuration() As Double
        Get
            If _frameRate > 0 Then
                Return 1.0 / _frameRate
            End If
            Return 0.04
        End Get
    End Property

    Public Property Volume() As Integer
        Get
            Return _volume
        End Get
        Set(ByVal value As Integer)
            If value < -10000 Then value = -10000
            If value > 0 Then value = 0
            _volume = value
            If basicAudio IsNot Nothing Then
                Try
                    basicAudio.put_Volume(value)
                Catch ex As Exception
                End Try
            End If
        End Set
    End Property

    Public Sub SetVolumePercent(ByVal percent As Integer)
        If percent < 0 Then percent = 0
        If percent > 100 Then percent = 100
        If percent = 0 Then
            Volume = -10000
        Else
            Volume = CInt(Math.Log10(percent / 100.0) * 2000)
        End If
    End Sub

    Private Sub SetupVideoWindow()
        Try
            If videoWindow Is Nothing Then Return
            If Not Me.IsHandleCreated Then Me.CreateControl()

            Dim handle As IntPtr = Me.Handle
            Dim videoRect As Rectangle = CalculateVideoRect()

            videoWindow.put_Owner(handle)
            videoWindow.put_WindowStyle(WS_CHILD Or WS_CLIPCHILDREN)
            videoWindow.SetWindowPosition(videoRect.X, videoRect.Y, videoRect.Width, videoRect.Height)
            videoWindow.put_MessageDrain(handle)
            videoWindow.put_Visible(OABool.True)

            Me.Refresh()
            Application.DoEvents()

        Catch ex As Exception
            Debug.WriteLine("SetupVideoWindow error: " & ex.Message)
        End Try
    End Sub

    Private Function CalculateVideoRect() As Rectangle
        If videoWidth = 0 OrElse videoHeight = 0 Then
            Return New Rectangle(0, 0, Me.Width, Me.Height)
        End If

        Dim controlRatio As Double = Me.Width / Me.Height
        Dim videoRatio As Double = aspectRatio

        If controlRatio > videoRatio Then
            Dim newHeight As Integer = Me.Height
            Dim newWidth As Integer = CInt(Me.Height * videoRatio)
            Return New Rectangle((Me.Width - newWidth) \ 2, 0, newWidth, newHeight)
        Else
            Dim newWidth As Integer = Me.Width
            Dim newHeight As Integer = CInt(Me.Width / videoRatio)
            Return New Rectangle(0, (Me.Height - newHeight) \ 2, newWidth, newHeight)
        End If
    End Function

    Private Sub PositionTimer_Tick(ByVal sender As Object, ByVal e As EventArgs)
        Try
            If hasVideo AndAlso _isPlaying AndAlso mediaPosition IsNot Nothing Then
                Dim pos As Double = 0
                Dim hr As Integer = mediaPosition.get_CurrentPosition(pos)

                If hr >= 0 AndAlso pos >= 0 Then
                    _currentPosition = pos
                    RaiseEvent PositionChanged(pos)

                    If _videoDuration > 0 AndAlso pos >= _videoDuration - 0.1 Then
                        _isPlaying = False
                        positionTimer.Stop()
                        RaiseEvent PlaybackEnded()
                    End If
                End If
            End If
        Catch ex As Exception
        End Try
    End Sub

    Public Sub Play()
        Try
            If mediaControl IsNot Nothing AndAlso hasVideo Then
                SetupVideoWindow()
                Dim hr As Integer = mediaControl.Run()

                If hr >= 0 Then
                    _isPlaying = True
                    positionTimer.Start()
                    Debug.WriteLine("Playback started")
                End If

                Me.Invalidate()
                Application.DoEvents()
            End If
        Catch ex As Exception
            Debug.WriteLine("Play error: " & ex.Message)
        End Try
    End Sub

    Public Sub Pause()
        Try
            If mediaControl IsNot Nothing Then
                mediaControl.Pause()
                _isPlaying = False
                positionTimer.Stop()
            End If
        Catch ex As Exception
        End Try
    End Sub

    Public Sub StopVideo()
        Try
            If mediaControl IsNot Nothing Then
                mediaControl.Stop()
                _isPlaying = False
                positionTimer.Stop()

                If mediaPosition IsNot Nothing Then
                    mediaPosition.put_CurrentPosition(0)
                End If

                _currentPosition = 0
            End If
        Catch ex As Exception
        End Try
    End Sub

    Public Sub Seek(ByVal seconds As Double)
        Try
            If mediaPosition IsNot Nothing Then
                mediaPosition.put_CurrentPosition(seconds)
                _currentPosition = seconds
                Me.Invalidate()
                Application.DoEvents()
            End If
        Catch ex As Exception
        End Try
    End Sub

    Public Sub StepForward(ByVal frames As Integer)
        Try
            If mediaPosition IsNot Nothing AndAlso hasVideo Then
                If _isPlaying Then Pause()

                Dim pos As Double = 0
                mediaPosition.get_CurrentPosition(pos)
                Dim newPos As Double = pos + (FrameDuration * frames)
                If newPos > _videoDuration Then newPos = _videoDuration
                mediaPosition.put_CurrentPosition(newPos)
                _currentPosition = newPos
                RaiseEvent FrameStepped(newPos)
                RaiseEvent PositionChanged(newPos)
                Me.Invalidate()
            End If
        Catch ex As Exception
        End Try
    End Sub

    Public Sub StepBackward(ByVal frames As Integer)
        Try
            If mediaPosition IsNot Nothing AndAlso hasVideo Then
                If _isPlaying Then Pause()

                Dim pos As Double = 0
                mediaPosition.get_CurrentPosition(pos)
                Dim newPos As Double = pos - (FrameDuration * frames)
                If newPos < 0 Then newPos = 0
                mediaPosition.put_CurrentPosition(newPos)
                _currentPosition = newPos
                RaiseEvent FrameStepped(newPos)
                RaiseEvent PositionChanged(newPos)
                Me.Invalidate()
            End If
        Catch ex As Exception
        End Try
    End Sub

    Public Sub FastForward(ByVal seconds As Double)
        Try
            If mediaPosition IsNot Nothing AndAlso hasVideo Then
                Dim pos As Double = 0
                mediaPosition.get_CurrentPosition(pos)
                Dim newPos As Double = pos + seconds
                If newPos > _videoDuration Then newPos = _videoDuration
                mediaPosition.put_CurrentPosition(newPos)
                _currentPosition = newPos
                RaiseEvent PositionChanged(newPos)
                Me.Invalidate()
            End If
        Catch ex As Exception
        End Try
    End Sub

    Public Sub Rewind(ByVal seconds As Double)
        Try
            If mediaPosition IsNot Nothing AndAlso hasVideo Then
                Dim pos As Double = 0
                mediaPosition.get_CurrentPosition(pos)
                Dim newPos As Double = pos - seconds
                If newPos < 0 Then newPos = 0
                mediaPosition.put_CurrentPosition(newPos)
                _currentPosition = newPos
                RaiseEvent PositionChanged(newPos)
                Me.Invalidate()
            End If
        Catch ex As Exception
        End Try
    End Sub

    Public Function GetCurrentPosition() As Double
        Try
            If mediaPosition IsNot Nothing Then
                Dim pos As Double = 0
                mediaPosition.get_CurrentPosition(pos)
                Return pos
            End If
        Catch ex As Exception
        End Try
        Return _currentPosition
    End Function

    Public Function GetDuration() As Double
        Return _videoDuration
    End Function

    Public Function IsVideoPlaying() As Boolean
        Return _isPlaying
    End Function

    Public Sub Cleanup()
        Try
            positionTimer.Stop()

            If mediaControl IsNot Nothing Then
                Try
                    mediaControl.Stop()
                Catch ex As Exception
                End Try
            End If

            If videoWindow IsNot Nothing Then
                Try
                    videoWindow.put_Visible(OABool.False)
                    videoWindow.put_Owner(IntPtr.Zero)
                Catch ex As Exception
                End Try
            End If

            If mediaControl IsNot Nothing Then
                Marshal.ReleaseComObject(mediaControl)
                mediaControl = Nothing
            End If
            If mediaPosition IsNot Nothing Then
                Marshal.ReleaseComObject(mediaPosition)
                mediaPosition = Nothing
            End If
            If mediaEvent IsNot Nothing Then
                Marshal.ReleaseComObject(mediaEvent)
                mediaEvent = Nothing
            End If
            If videoWindow IsNot Nothing Then
                Marshal.ReleaseComObject(videoWindow)
                videoWindow = Nothing
            End If
            If basicVideo IsNot Nothing Then
                Marshal.ReleaseComObject(basicVideo)
                basicVideo = Nothing
            End If
            If basicAudio IsNot Nothing Then
                Marshal.ReleaseComObject(basicAudio)
                basicAudio = Nothing
            End If
            If mediaSeeking IsNot Nothing Then
                Marshal.ReleaseComObject(mediaSeeking)
                mediaSeeking = Nothing
            End If
            If graphBuilder IsNot Nothing Then
                Marshal.ReleaseComObject(graphBuilder)
                graphBuilder = Nothing
            End If

            _isPlaying = False
            hasVideo = False
            _currentPosition = 0
            _videoDuration = 0

            Me.Invalidate()

        Catch ex As Exception
            Debug.WriteLine("Cleanup error: " & ex.Message)
        End Try
    End Sub

    Private Sub colorTimer_Tick(ByVal sender As Object, ByVal e As EventArgs) Handles colorTimer.Tick
        If Not hasVideo Then
            colorIndex += 1
            If colorIndex >= colors.Length Then colorIndex = 0
            Me.Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnResize(ByVal e As EventArgs)
        MyBase.OnResize(e)
        Try
            If videoWindow IsNot Nothing AndAlso hasVideo Then
                Dim videoRect As Rectangle = CalculateVideoRect()
                videoWindow.SetWindowPosition(videoRect.X, videoRect.Y, videoRect.Width, videoRect.Height)
            End If
        Catch ex As Exception
        End Try
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
        MyBase.OnPaint(e)
        e.Graphics.Clear(Color.Black)

        If Not hasVideo Then
            DrawLogo(e.Graphics)
        End If
    End Sub

    Private Sub DrawLogo(ByVal g As Graphics)
        Try
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAlias

            Dim logoSize As Integer = 80
            Dim logoX As Single = (Me.Width - logoSize) / 2
            Dim logoY As Single = (Me.Height / 2) - 80

            If logoPictureBox.Image IsNot Nothing Then
                Dim logoRect As New RectangleF(logoX, logoY, logoSize, logoSize)

                Using shadowBrush As New SolidBrush(Color.FromArgb(80, 0, 0, 0))
                    Dim shadowRect As New RectangleF(logoX + 3, logoY + 3, logoSize, logoSize)
                    g.FillEllipse(shadowBrush, shadowRect)
                End Using

                Using path As New Drawing2D.GraphicsPath()
                    path.AddEllipse(logoX, logoY, logoSize, logoSize)
                    g.SetClip(path)

                    g.DrawImage(logoPictureBox.Image, logoX, logoY, logoSize, logoSize)

                    g.ResetClip()
                End Using

                Using borderPen As New Pen(Color.FromArgb(120, colors(colorIndex)), 2)
                    g.DrawEllipse(borderPen, logoX, logoY, logoSize, logoSize)
                End Using

                Dim glowPoints As New List(Of PointF)()
                Dim glowRadius As Integer = 50
                Dim centerX As Single = logoX + logoSize / 2
                Dim centerY As Single = logoY + logoSize / 2

                For i As Integer = 0 To 360 Step 10
                    Dim angle As Double = i * Math.PI / 180.0
                    Dim px As Single = centerX + CSng(Math.Cos(angle) * glowRadius)
                    Dim py As Single = centerY + CSng(Math.Sin(angle) * glowRadius)
                    glowPoints.Add(New PointF(px, py))
                Next

                Using glowBrush As New Drawing2D.PathGradientBrush(glowPoints.ToArray())
                    Dim centerColor As Color = Color.FromArgb(50, colors(colorIndex))
                    Dim surroundColor As Color = Color.FromArgb(0, colors(colorIndex))
                    glowBrush.CenterColor = centerColor
                    glowBrush.SurroundColors = New Color() {surroundColor}
                    glowBrush.CenterPoint = New PointF(centerX, centerY)
                    g.FillEllipse(glowBrush, logoX - 15, logoY - 15, logoSize + 30, logoSize + 30)
                End Using
            Else
                Dim fallbackSize As Integer = 60
                Dim fallbackX As Single = (Me.Width - fallbackSize) / 2
                Dim fallbackY As Single = (Me.Height / 2) - 70

                Using circleBrush As New SolidBrush(Color.FromArgb(80, colors(colorIndex)))
                    g.FillEllipse(circleBrush, fallbackX, fallbackY, fallbackSize, fallbackSize)
                End Using

                Using circlePen As New Pen(colors(colorIndex), 3)
                    g.DrawEllipse(circlePen, fallbackX, fallbackY, fallbackSize, fallbackSize)
                End Using

                Using playBrush As New SolidBrush(Color.White)
                    Dim points As Point() = {
                        New Point(CInt(fallbackX + 20), CInt(fallbackY + 15)),
                        New Point(CInt(fallbackX + 20), CInt(fallbackY + fallbackSize - 15)),
                        New Point(CInt(fallbackX + fallbackSize - 15), CInt(fallbackY + fallbackSize / 2))
                    }
                    g.FillPolygon(playBrush, points)
                End Using
            End If

            Dim titleFont As New Font("Arial Black", 28, FontStyle.Bold)
            Dim subtitleFont As New Font("Arial", 12, FontStyle.Regular)

            Dim titleText As String = "Pongo Video Cutter"
            Dim subtitleText As String = "Drag & Drop Video Here..."

            Dim titleSize As SizeF = g.MeasureString(titleText, titleFont)
            Dim subtitleSize As SizeF = g.MeasureString(subtitleText, subtitleFont)

            Dim titleX As Single = (Me.Width - titleSize.Width) / 2
            Dim titleY As Single = (Me.Height / 2) + 10
            Dim subtitleX As Single = (Me.Width - subtitleSize.Width) / 2
            Dim subtitleY As Single = titleY + titleSize.Height + 10

            Dim mainColor As Color = colors(colorIndex)

            Dim rect As New RectangleF(titleX - 10, titleY - 10, titleSize.Width + 20, titleSize.Height + 20)
            Dim brush As New Drawing2D.LinearGradientBrush(rect, mainColor, Color.White, 45)

            Using textShadowBrush As New SolidBrush(Color.FromArgb(100, 0, 0, 0))
                g.DrawString(titleText, titleFont, textShadowBrush, titleX + 3, titleY + 3)
            End Using

            g.DrawString(titleText, titleFont, brush, titleX, titleY)

            Dim subtitleBrush As New SolidBrush(Color.FromArgb(220, 80, 80, 100))
            g.DrawString(subtitleText, subtitleFont, subtitleBrush, subtitleX, subtitleY)

            Dim linePen As New Pen(mainColor, 2)
            Dim lineY As Single = titleY + titleSize.Height + 5
            Dim lineWidth As Single = Math.Min(300, titleSize.Width)
            Dim lineX As Single = (Me.Width - lineWidth) / 2

            g.DrawLine(linePen, lineX, lineY, lineX + lineWidth, lineY)

            titleFont.Dispose()
            subtitleFont.Dispose()
            brush.Dispose()
            subtitleBrush.Dispose()
            linePen.Dispose()

        Catch ex As Exception
            Try
                Dim fallbackBrush As New SolidBrush(Color.White)
                Dim fallbackFont As New Font("Arial", 14, FontStyle.Bold)
                g.DrawString("Pongo Video Cutter", fallbackFont, fallbackBrush, 10, 10)
                fallbackBrush.Dispose()
                fallbackFont.Dispose()
            Catch
            End Try
        End Try
    End Sub

    Private Const WS_CHILD As Integer = &H40000000
    Private Const WS_CLIPCHILDREN As Integer = &H2000000

    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        If disposing Then
            colorTimer.Stop()
            colorTimer.Dispose()
            positionTimer.Stop()
            positionTimer.Dispose()
            Cleanup()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
