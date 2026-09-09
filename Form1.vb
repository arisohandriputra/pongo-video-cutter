' ==================================================================*
'  Pongo Video Cutter - Main Form (Form1.vb)
'  -----------------------------------------------------------------
'  Author  : Ari Sohandri Putra
'  Web     : https://pongo.my.id
'  GitHub  : https://github.com/arisohandriputra
'  License : MIT (see LICENSE file or pongo.my.id/license.htm)
' -----------------------------------------------------------------
'  Pongo Video Cutter is a Free Open Source Lossless Video Cutting
'  utility. It wraps FFmpeg (LGPL v2.1) and DirectShowLib (LGPL v2.1)
'  to perform frame-accurate cuts without re-encoding, and ships
'  with a custom timeline track-bar, J/K/L shuttle playback and a
'  batch queue for unattended processing of multiple video files.
' -----------------------------------------------------------------
'  Project Structure:
'    Form1.vb              - Main application window / business logic
'    Form1.Designer.vb     - Auto-generated layout for the main window
'    frmAbout.vb            - About dialog (credits + third-party list)
'    frmCaption.vb          - Edit segment caption / note dialog
'    frmSplash.vb           - Splash screen shown on startup
'    FFmpeg Path.vb         - FFmpeg / FFprobe path picker dialog
'    CustomTrackBar.vb      - Owner-drawn multi-segment timeline control
'    VideoPlayer.vb         - DirectShow-based video preview player
'    ModernListBox.vb       - Styled owner-drawn listbox for segments
' ==================================================================*

Imports System.IO
Imports System.Diagnostics
Imports System.Collections.Generic

Public Class Form1
    Public ffmpegPath As String
    Public ffprobePath As String
    Private inputFile As String = ""
    Private outputFile As String = ""
    Private isUpdatingTrackbar As Boolean = False
    Private videoDuration As Double = 0
    Private currentPosition As Double = 0
    Private isPreviewPlaying As Boolean = False
    Private videoPlayer As VideoPlayer
    Private customTrackBar As CustomTrackBar
    Private isSettingStart As Boolean = False
    Private currentStartTime As Double = -1
    Private currentEndTime As Double = -1
    Private isPlayingSegment As Boolean = False
    Private segmentPlayStart As Double = 0
    Private segmentPlayEnd As Double = 0
    Private isUpdatingSegmentUI As Boolean = False
    Private isProcessingFile As Boolean = False

    Private batchQueue As New List(Of String)()
    Private jklPlaybackRate As Double = 1.0
    Private Const RECENT_FILES_MAX As Integer = 10
    Private hasUnsavedChanges As Boolean = False
    ' Holds the last stderr tail captured from FFmpeg so we can show a
    ' useful diagnostic message if a job fails.
    Private lastFFmpegError As String = ""

    Private Class VideoSegment
        Public StartTime As Double
        Public EndTime As Double
        Public SegmentIndex As Integer
        Public Caption As String

        Public Sub New()
            StartTime = 0
            EndTime = 0
            SegmentIndex = 0
            Caption = ""
        End Sub

        Public Sub New(ByVal start As Double, ByVal endT As Double, ByVal index As Integer)
            StartTime = start
            EndTime = endT
            SegmentIndex = index
            Caption = ""
        End Sub

        Public Overrides Function ToString() As String
            Dim startTs As TimeSpan = TimeSpan.FromSeconds(StartTime)
            Dim endTs As TimeSpan = TimeSpan.FromSeconds(EndTime)

            Dim startStr As String = String.Format("{0:00}:{1:00}:{2:00}", startTs.Hours, startTs.Minutes, startTs.Seconds)
            Dim endStr As String = String.Format("{0:00}:{1:00}:{2:00}", endTs.Hours, endTs.Minutes, endTs.Seconds)

            If Not String.IsNullOrEmpty(Caption) Then
                ' #{index} - {start} - {end} - {caption}
                Return String.Format("#{0} - {1} - {2} - {3}", SegmentIndex, startStr, endStr, Caption)
            Else
                ' No caption: just show #index - start - end
                Return String.Format("#{0} - {1} - {2}", SegmentIndex, startStr, endStr)
            End If
        End Function
    End Class

    Private Sub UpdateFormTitle()
        If String.IsNullOrEmpty(inputFile) Then
            Me.Text = "Pongo Video Cutter Pro"
        Else
            Me.Text = "Pongo Video Cutter Pro - " & Path.GetFileName(inputFile)
        End If
    End Sub

    Private Sub Form1_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        Me.Text = "Pongo Video Cutter Pro"
        Me.KeyPreview = True

        ApplyProVideoEditorTheme()

        Me.AllowDrop = True
        AddHandler Me.DragEnter, AddressOf Form1_DragEnter
        AddHandler Me.DragDrop, AddressOf Form1_DragDrop
        AddHandler Me.DragLeave, AddressOf Form1_DragLeave

        If String.IsNullOrEmpty(My.Settings.ffmpegpath) Then
            ffmpegPath = Application.StartupPath & "\ffmpeg.exe"
        Else
            ffmpegPath = My.Settings.ffmpegpath
        End If
        If String.IsNullOrEmpty(My.Settings.ffprobepath) Then
            ffprobePath = Application.StartupPath & "\ffprobe.exe"
        Else
            ffprobePath = My.Settings.ffprobepath
        End If

        EnableDragDropRecursively(Me)

        Timer3.Interval = 50

        If TrackBar1 IsNot Nothing Then
            TrackBar1.Visible = False
        End If

        customTrackBar = New CustomTrackBar()
        customTrackBar.Minimum = 0
        customTrackBar.Maximum = 1000
        customTrackBar.Width = TrackBar1.Width
        customTrackBar.Height = 80
        customTrackBar.Left = TrackBar1.Left
        customTrackBar.Top = TrackBar1.Top - 10
        customTrackBar.Anchor = TrackBar1.Anchor
        customTrackBar.BackColor = Color.FromArgb(15, 15, 15)
        customTrackBar.Enabled = True
        Panel5.Controls.Add(customTrackBar)
        customTrackBar.BringToFront()

        AddHandler customTrackBar.ValueChanged, AddressOf CustomTrackBar_ValueChanged
        AddHandler customTrackBar.Scroll, AddressOf CustomTrackBar_Scroll
        AddHandler customTrackBar.SegmentClicked, AddressOf CustomTrackBar_SegmentClicked
        AddHandler customTrackBar.SegmentChanged, AddressOf CustomTrackBar_SegmentChanged
        AddHandler lstSegments.SelectedIndexChanged, AddressOf lstSegments_SelectedIndexChanged

        If Panel1 IsNot Nothing Then
            Panel1.BackColor = Color.Black
            Panel1.BorderStyle = BorderStyle.None
            videoPlayer = New VideoPlayer()
            videoPlayer.Dock = DockStyle.Fill
            videoPlayer.BackColor = Color.Black
            AddHandler videoPlayer.PositionChanged, AddressOf VideoPlayer_PositionChanged
            AddHandler videoPlayer.PlaybackEnded, AddressOf VideoPlayer_PlaybackEnded
            Panel1.Controls.Add(videoPlayer)
        End If

        If lstSegments IsNot Nothing Then
            Dim oldListBox As ListBox = lstSegments
            Dim parentControl As Control = oldListBox.Parent
            Dim oldLocation As Point = oldListBox.Location
            Dim oldSize As Size = oldListBox.Size
            Dim oldAnchor As AnchorStyles = oldListBox.Anchor

            Dim modernListBox As New ModernListBox()
            modernListBox.Location = oldLocation
            modernListBox.Size = oldSize
            modernListBox.Anchor = oldAnchor
            modernListBox.Name = "lstSegmentsModern"
            modernListBox.Items.Clear()

            parentControl.Controls.Remove(oldListBox)
            oldListBox.Dispose()

            parentControl.Controls.Add(modernListBox)

            lstSegments = modernListBox

            AddHandler lstSegments.SelectedIndexChanged, AddressOf lstSegments_SelectedIndexChanged
        End If

        If chkMergeOutput IsNot Nothing Then
            chkMergeOutput.Checked = False
        End If

        SetupFileInfoTextBoxes()

        BuildRecentFilesMenu()

        If My.Settings.WindowWidth > 0 AndAlso My.Settings.WindowHeight > 0 Then
            Me.Width = My.Settings.WindowWidth
            Me.Height = My.Settings.WindowHeight
        End If

        If Not System.IO.File.Exists(ffmpegPath) Then
            If lblStatus IsNot Nothing Then
                lblStatus.Text = "Status: FFmpeg NOT FOUND!"
                lblStatus.ForeColor = Color.FromArgb(255, 80, 80)
            End If
            MessageBox.Show("FFmpeg was not found in the application directory!" & vbNewLine & vbNewLine & _
                          "Please download FFmpeg and place ffmpeg.exe in:" & vbNewLine & _
                          Application.StartupPath & vbNewLine & vbNewLine & _
                          "Download from: https://ffmpeg.org/download.html", _
                          "Pongo Video Cutter Pro - Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Else
            If lblStatus IsNot Nothing Then
                lblStatus.Text = "Status: Ready - Drag & Drop video or click Browse | SPACE=Play/Pause | I/O=In/Out | JKL=Shuttle"
                lblStatus.ForeColor = Color.FromArgb(50, 50, 70)
            End If
        End If
    End Sub

    Private Sub ApplyProVideoEditorTheme()
        Me.BackColor = Color.FromArgb(248, 249, 252)
        Me.ForeColor = Color.FromArgb(50, 50, 70)

        Dim allControls As New List(Of Control)()
        CollectAllControls(Me, allControls)

        For Each ctrl As Control In allControls
            ApplyThemeToControl(ctrl)
        Next
    End Sub

    Private Sub CollectAllControls(ByVal parent As Control, ByRef list As List(Of Control))
        For Each ctrl As Control In parent.Controls
            list.Add(ctrl)
            If ctrl.HasChildren Then
                CollectAllControls(ctrl, list)
            End If
        Next
    End Sub

    Private Sub ApplyThemeToControl(ByVal ctrl As Control)
        If TypeOf ctrl Is GroupBox Then
            CType(ctrl, GroupBox).ForeColor = Color.FromArgb(0, 120, 215)
            CType(ctrl, GroupBox).BackColor = Color.FromArgb(248, 249, 252)
            CType(ctrl, GroupBox).FlatStyle = FlatStyle.Flat
        ElseIf TypeOf ctrl Is Label Then
            ctrl.ForeColor = Color.FromArgb(50, 50, 70)
        ElseIf TypeOf ctrl Is Button Then
            ctrl.BackColor = Color.FromArgb(233, 236, 242)
            ctrl.ForeColor = Color.FromArgb(50, 50, 70)
            CType(ctrl, Button).FlatStyle = FlatStyle.Flat
            CType(ctrl, Button).FlatAppearance.BorderColor = Color.FromArgb(200, 205, 215)
            CType(ctrl, Button).FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 120, 215)
            CType(ctrl, Button).FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 90, 170)
            CType(ctrl, Button).FlatAppearance.BorderSize = 1
        ElseIf TypeOf ctrl Is TextBox Then
            ctrl.BackColor = Color.White
            ctrl.ForeColor = Color.FromArgb(50, 50, 70)
            CType(ctrl, TextBox).BorderStyle = BorderStyle.FixedSingle
        ElseIf TypeOf ctrl Is ListBox Then
            ctrl.BackColor = Color.White
            ctrl.ForeColor = Color.FromArgb(50, 50, 70)
        ElseIf TypeOf ctrl Is Panel Then
            ctrl.BackColor = Color.FromArgb(248, 249, 252)
        ElseIf TypeOf ctrl Is MenuStrip Then
            ctrl.BackColor = Color.FromArgb(248, 249, 252)
            ctrl.ForeColor = Color.FromArgb(50, 50, 70)
        ElseIf TypeOf ctrl Is ProgressBar Then
            CType(ctrl, ProgressBar).ForeColor = Color.FromArgb(0, 120, 215)
        ElseIf TypeOf ctrl Is RadioButton Then
            ctrl.ForeColor = Color.FromArgb(50, 50, 70)
            ctrl.BackColor = Color.FromArgb(248, 249, 252)
        End If
    End Sub

    Private Sub VideoPlayer_PositionChanged(ByVal position As Double)
        Try
            If Me.InvokeRequired Then
                Me.BeginInvoke(New MethodInvoker(Sub() VideoPlayer_PositionChanged(position)))
                Return
            End If

            currentPosition = position

            If Not isUpdatingTrackbar AndAlso customTrackBar IsNot Nothing AndAlso videoDuration > 0 Then
                isUpdatingTrackbar = True
                customTrackBar.Value = CInt((position / videoDuration) * 1000)
                isUpdatingTrackbar = False
            End If

            UpdateCurrentTimeDisplay()

            If isPlayingSegment AndAlso position >= segmentPlayEnd Then
                StopPreview()
                isPlayingSegment = False
                lblStatus.Text = "Status: Segment playback finished"
            End If

        Catch ex As Exception
        End Try
    End Sub

    Private Sub VideoPlayer_PlaybackEnded()
        Try
            If Me.InvokeRequired Then
                Me.BeginInvoke(New MethodInvoker(AddressOf VideoPlayer_PlaybackEnded))
                Return
            End If

            If isPreviewPlaying Then
                StopPreview()
                lblStatus.Text = "Status: Playback finished"
            End If
        Catch ex As Exception
        End Try
    End Sub

    Private Sub UpdateCurrentTimeDisplay()
        Try
            If Me.InvokeRequired Then
                Me.BeginInvoke(New MethodInvoker(AddressOf UpdateCurrentTimeDisplay))
                Return
            End If

            If lblCurrentTime IsNot Nothing Then
                lblCurrentTime.Text = FormatTime(currentPosition) & " / " & FormatTime(videoDuration)
            End If
        Catch ex As Exception
        End Try
    End Sub

    Private Sub SetupFileInfoTextBoxes()
        If txtFilename IsNot Nothing Then
            txtFilename.ReadOnly = True
            txtFilename.Text = ""
            txtFilename.BackColor = Color.FromArgb(30, 30, 30)
            txtFilename.ForeColor = Color.White
        End If

        If txtFilesize IsNot Nothing Then
            txtFilesize.ReadOnly = True
            txtFilesize.Text = ""
            txtFilesize.BackColor = Color.FromArgb(30, 30, 30)
            txtFilesize.ForeColor = Color.White
        End If

        If txtResolution IsNot Nothing Then
            txtResolution.ReadOnly = True
            txtResolution.Text = ""
            txtResolution.BackColor = Color.FromArgb(30, 30, 30)
            txtResolution.ForeColor = Color.White
        End If

        If txtDuration IsNot Nothing Then
            txtDuration.ReadOnly = True
            txtDuration.Text = ""
            txtDuration.BackColor = Color.FromArgb(30, 30, 30)
            txtDuration.ForeColor = Color.White
        End If
    End Sub

    Private Sub UpdateFileInfo(ByVal filePath As String)
        Try
            If txtFilename IsNot Nothing Then
                txtFilename.Text = Path.GetFileName(filePath)
            End If

            If txtFilesize IsNot Nothing Then
                Dim fileInfo As New FileInfo(filePath)
                txtFilesize.Text = FormatFileSize(fileInfo.Length)
            End If

            GetVideoInfoFromFFprobe(filePath)

            If txtMetadata IsNot Nothing Then
                txtMetadata.Text = "Loading metadata..." & vbNewLine & _
                                  "File: " & Path.GetFileName(filePath)
                txtMetadata.Refresh()
                Application.DoEvents()

                Dim metadata As String = GetVideoMetadata(filePath)
                txtMetadata.Text = metadata
                txtMetadata.SelectionStart = 0
                txtMetadata.ScrollToCaret()
            End If

        Catch ex As Exception
            Try
                System.IO.File.WriteAllText(Application.StartupPath & "\fileinfo_error.log", _
                    "Error: " & ex.Message & vbNewLine & ex.StackTrace)
            Catch
            End Try

            If txtMetadata IsNot Nothing Then
                txtMetadata.Text = "Error loading metadata: " & ex.Message
            End If
        End Try
    End Sub

    Private Sub GetVideoInfoFromFFprobe(ByVal filePath As String)
        Try
            Dim ffprobe As New Process()
            ffprobe.StartInfo.FileName = ffprobePath
            ffprobe.StartInfo.Arguments = String.Format( _
                "-v error -select_streams v:0 -show_entries stream=width,height,duration -show_entries format=duration -of default=noprint_wrappers=1 ""{0}""", _
                filePath _
            )
            ffprobe.StartInfo.UseShellExecute = False
            ffprobe.StartInfo.RedirectStandardOutput = True
            ffprobe.StartInfo.RedirectStandardError = True
            ffprobe.StartInfo.CreateNoWindow = True

            ffprobe.Start()
            Dim output As String = ffprobe.StandardOutput.ReadToEnd()
            Dim errorOutput As String = ffprobe.StandardError.ReadToEnd()
            ffprobe.WaitForExit()

            Dim width As Integer = 0
            Dim height As Integer = 0

            Dim lines As String() = output.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            For Each line As String In lines
                If line.StartsWith("width=") Then
                    Integer.TryParse(line.Substring(6), width)
                ElseIf line.StartsWith("height=") Then
                    Integer.TryParse(line.Substring(7), height)
                End If
            Next

            If txtResolution IsNot Nothing Then
                If width > 0 AndAlso height > 0 Then
                    txtResolution.Text = String.Format("{0} x {1}", width, height)
                Else
                    txtResolution.Text = "Unknown"
                End If
            End If

            If txtDuration IsNot Nothing Then
                If videoDuration > 0 Then
                    txtDuration.Text = FormatTime(videoDuration)
                Else
                    txtDuration.Text = "Unknown"
                End If
            End If

        Catch ex As Exception
            If txtResolution IsNot Nothing Then
                txtResolution.Text = "Unknown"
            End If
            If txtDuration IsNot Nothing Then
                txtDuration.Text = "Unknown"
            End If
        End Try
    End Sub

    Private Function FormatFileSize(ByVal bytes As Long) As String
        If bytes >= 1073741824 Then
            Return String.Format("{0:F2} GB", bytes / 1073741824)
        ElseIf bytes >= 1048576 Then
            Return String.Format("{0:F2} MB", bytes / 1048576)
        ElseIf bytes >= 1024 Then
            Return String.Format("{0:F2} KB", bytes / 1024)
        Else
            Return String.Format("{0} Bytes", bytes)
        End If
    End Function

    Private Sub EnableDragDropRecursively(ByVal parent As Control)
        For Each ctrl As Control In parent.Controls
            ctrl.AllowDrop = True
            AddHandler ctrl.DragEnter, AddressOf Form1_DragEnter
            AddHandler ctrl.DragDrop, AddressOf Form1_DragDrop
            AddHandler ctrl.DragLeave, AddressOf Form1_DragLeave

            If ctrl.HasChildren Then
                EnableDragDropRecursively(ctrl)
            End If
        Next
    End Sub

    Private Sub Form1_DragEnter(ByVal sender As Object, ByVal e As DragEventArgs)
        If Not isProcessingFile AndAlso e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Dim files As String() = CType(e.Data.GetData(DataFormats.FileDrop), String())

            If files IsNot Nothing AndAlso files.Length > 0 Then
                Dim fileExt As String = Path.GetExtension(files(0)).ToLower()
                Dim supportedExts As String() = {".mp4", ".avi", ".mkv", ".mov", ".flv", ".wmv", ".ts", ".m4v", ".webm"}

                If Array.IndexOf(supportedExts, fileExt) >= 0 Then
                    e.Effect = DragDropEffects.Copy

                    If Panel1 IsNot Nothing Then
                        Panel1.BackColor = Color.FromArgb(40, 60, 40)
                    End If

                    If lblStatus IsNot Nothing Then
                        lblStatus.Text = "Status: Drop video file to load..."
                        lblStatus.ForeColor = Color.FromArgb(0, 130, 80)
                    End If
                Else
                    e.Effect = DragDropEffects.None
                End If
            Else
                e.Effect = DragDropEffects.None
            End If
        Else
            e.Effect = DragDropEffects.None
        End If
    End Sub

    Private Sub Form1_DragLeave(ByVal sender As Object, ByVal e As System.EventArgs)
        If Panel1 IsNot Nothing Then
            Panel1.BackColor = Color.Black
        End If

        If Not isProcessingFile AndAlso lblStatus IsNot Nothing Then
            If String.IsNullOrEmpty(inputFile) Then
                lblStatus.Text = "Status: Ready - Drag & Drop video or click Browse | SPACE=Play/Pause | I/O=In/Out | JKL=Shuttle"
            Else
                lblStatus.Text = "Status: Ready - SPACE=Play/Pause | I/O=In/Out | JKL=Shuttle"
            End If
            lblStatus.ForeColor = Color.FromArgb(50, 50, 70)
        End If
    End Sub

    Private Sub Form1_DragDrop(ByVal sender As Object, ByVal e As DragEventArgs)
        If Panel1 IsNot Nothing Then
            Panel1.BackColor = Color.Black
        End If

        If isProcessingFile Then
            MessageBox.Show("Please wait for the current video to finish loading...", _
                          "Pongo Video Cutter Pro - Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Dim files As String() = CType(e.Data.GetData(DataFormats.FileDrop), String())

            If files IsNot Nothing AndAlso files.Length > 0 Then
                Dim droppedFile As String = files(0)
                Dim fileExt As String = Path.GetExtension(droppedFile).ToLower()
                Dim supportedExts As String() = {".mp4", ".avi", ".mkv", ".mov", ".flv", ".wmv", ".ts", ".m4v", ".webm"}

                If Array.IndexOf(supportedExts, fileExt) < 0 Then
                    MessageBox.Show("Unsupported file format!" & vbNewLine & vbNewLine & _
                                  "Supported formats:" & vbNewLine & _
                                  "• MP4 (.mp4)" & vbNewLine & _
                                  "• AVI (.avi)" & vbNewLine & _
                                  "• MKV (.mkv)" & vbNewLine & _
                                  "• MOV (.mov)" & vbNewLine & _
                                  "• FLV (.flv)" & vbNewLine & _
                                  "• WMV (.wmv)" & vbNewLine & _
                                  "• TS (.ts)" & vbNewLine & _
                                  "• M4V (.m4v)" & vbNewLine & _
                                  "• WebM (.webm)", _
                                  "Pongo Video Cutter Pro - Unsupported Format", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If

                If Not System.IO.File.Exists(droppedFile) Then
                    MessageBox.Show("The dropped file does not exist or is not accessible.", _
                                  "Pongo Video Cutter Pro - Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Return
                End If

                LoadDroppedVideo(droppedFile)
            End If
        End If
    End Sub

    Private Sub LoadDroppedVideo(ByVal filePath As String)
        Try
            isProcessingFile = True

            StopPreview()

            inputFile = filePath
            txtInputFile.Text = inputFile

            UpdateFormTitle()
            UpdateFileInfo(inputFile)
            AddToRecentFiles(inputFile)

            Dim ext As String = Path.GetExtension(inputFile)
            Dim dir As String = Path.GetDirectoryName(inputFile)
            Dim filename As String = Path.GetFileNameWithoutExtension(inputFile)
            txtOutputFile.Text = Path.Combine(dir, filename & "_cut" & ext)
            outputFile = txtOutputFile.Text

            lstSegments.Items.Clear()
            If customTrackBar IsNot Nothing Then
                customTrackBar.ClearSegments()
            End If
            isSettingStart = False
            currentStartTime = -1
            currentEndTime = -1
            isPlayingSegment = False
            isPreviewPlaying = False

            currentPosition = 0
            videoDuration = 0

            txtStartTime.Text = "00:00:00"
            txtEndTime.Text = "00:00:00"

            lblStatus.Text = "Status: Loading video..."
            lblStatus.ForeColor = Color.FromArgb(50, 50, 70)
            hasUnsavedChanges = False
            Application.DoEvents()

            LoadVideo()

        Catch ex As Exception
            MessageBox.Show("Failed to load the dropped video file." & vbNewLine & vbNewLine & _
                          "Please try again or use the Browse button.", _
                          "Pongo Video Cutter Pro - Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            isProcessingFile = False

            If Panel1 IsNot Nothing Then
                Panel1.BackColor = Color.Black
            End If
        End Try
    End Sub

    Private Sub Form1_KeyDown(ByVal sender As Object, ByVal e As System.Windows.Forms.KeyEventArgs) Handles Me.KeyDown
        If e.KeyCode = Keys.Space Then
            e.SuppressKeyPress = True
            TogglePreviewPlay()
            Return
        End If

        If e.KeyCode = Keys.I Then
            e.SuppressKeyPress = True
            btnSetStart.PerformClick()
            Return
        End If

        If e.KeyCode = Keys.O Then
            e.SuppressKeyPress = True
            btnSetEnd.PerformClick()
            Return
        End If

        If e.KeyCode = Keys.J Then
            e.SuppressKeyPress = True
            JKLShuttle(-1)
            Return
        End If

        If e.KeyCode = Keys.K Then
            e.SuppressKeyPress = True
            If isPreviewPlaying Then TogglePreviewPlay()
            Return
        End If

        If e.KeyCode = Keys.L Then
            e.SuppressKeyPress = True
            JKLShuttle(1)
            Return
        End If

        If e.KeyCode = Keys.Home Then
            e.SuppressKeyPress = True
            If videoPlayer IsNot Nothing AndAlso videoDuration > 0 Then
                videoPlayer.Seek(0)
                currentPosition = 0
            End If
            Return
        End If

        If e.KeyCode = Keys.End Then
            e.SuppressKeyPress = True
            If videoPlayer IsNot Nothing AndAlso videoDuration > 0 Then
                videoPlayer.Seek(videoDuration - 0.1)
            End If
            Return
        End If

        If e.KeyCode = Keys.PageUp Then
            e.SuppressKeyPress = True
            If videoPlayer IsNot Nothing Then videoPlayer.Rewind(10)
            Return
        End If

        If e.KeyCode = Keys.PageDown Then
            e.SuppressKeyPress = True
            If videoPlayer IsNot Nothing Then videoPlayer.FastForward(10)
            Return
        End If

        If e.KeyCode = Keys.Delete Then
            e.SuppressKeyPress = True
            btnRemoveSegment.PerformClick()
            Return
        End If

        If e.Control AndAlso e.KeyCode = Keys.D Then
            e.SuppressKeyPress = True
            btnClearSegments.PerformClick()
            Return
        End If

    End Sub

    Private Sub JKLShuttle(ByVal direction As Integer)
        If String.IsNullOrEmpty(inputFile) Then Return

        If direction < 0 Then
            If isPreviewPlaying Then
                jklPlaybackRate -= 1.0
                If jklPlaybackRate < -4.0 Then jklPlaybackRate = -4.0
            Else
                jklPlaybackRate = -1.0
                isPreviewPlaying = True
            End If

            If jklPlaybackRate < 0 Then
                If videoPlayer IsNot Nothing Then
                    videoPlayer.Rewind(Math.Abs(jklPlaybackRate) * 2.0)
                End If
                lblStatus.Text = "Status: Fast reverse x" & Math.Abs(jklPlaybackRate).ToString("F1")
            Else
                If videoPlayer IsNot Nothing Then videoPlayer.Play()
                lblStatus.Text = "Status: Forward x" & jklPlaybackRate.ToString("F1")
            End If
        ElseIf direction > 0 Then
            If isPreviewPlaying Then
                jklPlaybackRate += 1.0
                If jklPlaybackRate > 4.0 Then jklPlaybackRate = 4.0
            Else
                jklPlaybackRate = 1.0
                isPreviewPlaying = True
            End If

            If jklPlaybackRate = 1.0 Then
                If videoPlayer IsNot Nothing Then videoPlayer.Play()
                lblStatus.Text = "Status: Normal playback"
            Else
                If videoPlayer IsNot Nothing Then
                    videoPlayer.FastForward((jklPlaybackRate - 1.0) * 2.0)
                End If
                lblStatus.Text = "Status: Fast forward x" & jklPlaybackRate.ToString("F1")
            End If
        End If

        If btnPlayPause IsNot Nothing AndAlso isPreviewPlaying Then
            btnPlayPause.Image = My.Resources.bpause
        End If
    End Sub

    Private Sub CustomTrackBar_ValueChanged(ByVal sender As Object, ByVal e As EventArgs)
        If isUpdatingTrackbar Then Return

        Try
            If videoDuration > 0 Then
                currentPosition = (customTrackBar.Value / 1000.0) * videoDuration

                If videoPlayer IsNot Nothing Then
                    videoPlayer.Seek(currentPosition)
                End If
            End If
        Catch ex As Exception
        End Try
    End Sub

    Private Sub CustomTrackBar_Scroll(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Private Sub CustomTrackBar_SegmentClicked(ByVal sender As Object, ByVal segmentIndex As Integer)
        If segmentIndex >= 0 AndAlso segmentIndex < lstSegments.Items.Count Then
            isUpdatingSegmentUI = True
            lstSegments.SelectedIndex = segmentIndex
            isUpdatingSegmentUI = False

            Dim seg As VideoSegment = DirectCast(lstSegments.Items(segmentIndex), VideoSegment)
            txtStartTime.Text = FormatTime(seg.StartTime)
            txtEndTime.Text = FormatTime(seg.EndTime)

            If customTrackBar IsNot Nothing Then
                customTrackBar.SelectedSegmentIndexValue = segmentIndex
            End If

            PlaySegment(segmentIndex)

            lblStatus.Text = String.Format("Status: Segment {0} selected and playing", segmentIndex + 1)
        End If
    End Sub

    Private Sub CustomTrackBar_SegmentChanged(ByVal sender As Object, ByVal segmentIndex As Integer, ByVal startValue As Double, ByVal endValue As Double)
        If segmentIndex >= 0 AndAlso segmentIndex < lstSegments.Items.Count Then
            isUpdatingSegmentUI = True

            Dim seg As VideoSegment = DirectCast(lstSegments.Items(segmentIndex), VideoSegment)
            seg.StartTime = (startValue / 1000.0) * videoDuration
            seg.EndTime = (endValue / 1000.0) * videoDuration
            lstSegments.Items(segmentIndex) = seg

            isUpdatingSegmentUI = False

            txtStartTime.Text = FormatTime(seg.StartTime)
            txtEndTime.Text = FormatTime(seg.EndTime)

            lblStatus.Text = String.Format("Status: Segment {0} updated", segmentIndex + 1)

            If isPlayingSegment AndAlso segmentIndex = lstSegments.SelectedIndex Then
                segmentPlayStart = seg.StartTime
                segmentPlayEnd = seg.EndTime

                If currentPosition < segmentPlayStart Then
                    currentPosition = segmentPlayStart
                    If videoPlayer IsNot Nothing Then
                        videoPlayer.Seek(currentPosition)
                    End If
                ElseIf currentPosition > segmentPlayEnd Then
                    currentPosition = segmentPlayEnd
                    If videoPlayer IsNot Nothing Then
                        videoPlayer.Seek(currentPosition)
                    End If
                End If
            End If
        End If
    End Sub

    Private Sub btnBrowse_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnBrowse.Click
        Me.ActiveControl = Nothing
        StopPreview()

        OpenFileDialog1.Title = "Select Video File"
        OpenFileDialog1.Filter = "Video Files|*.mp4;*.avi;*.mkv;*.mov;*.flv;*.wmv;*.ts;*.m4v;*.webm|All Files|*.*"
        OpenFileDialog1.FileName = ""

        If Not String.IsNullOrEmpty(My.Settings.LastFolder) AndAlso Directory.Exists(My.Settings.LastFolder) Then
            OpenFileDialog1.InitialDirectory = My.Settings.LastFolder
        End If

        If OpenFileDialog1.ShowDialog() = DialogResult.OK Then
            inputFile = OpenFileDialog1.FileName
            txtInputFile.Text = inputFile
            My.Settings.LastFolder = Path.GetDirectoryName(inputFile)
            My.Settings.Save()

            UpdateFormTitle()
            UpdateFileInfo(inputFile)
            AddToRecentFiles(inputFile)

            Dim ext As String = Path.GetExtension(inputFile)
            Dim dir As String = Path.GetDirectoryName(inputFile)
            Dim filename As String = Path.GetFileNameWithoutExtension(inputFile)
            txtOutputFile.Text = Path.Combine(dir, filename & "_cut" & ext)
            outputFile = txtOutputFile.Text

            lstSegments.Items.Clear()
            If customTrackBar IsNot Nothing Then
                customTrackBar.ClearSegments()
            End If
            isSettingStart = False
            currentStartTime = -1
            currentEndTime = -1
            isPlayingSegment = False
            isPreviewPlaying = False

            currentPosition = 0
            videoDuration = 0

            txtStartTime.Text = "00:00:00"
            txtEndTime.Text = "00:00:00"

            lblStatus.Text = "Status: Loading video..."
            hasUnsavedChanges = False
            Application.DoEvents()

            LoadVideo()
        End If
    End Sub

    Private Sub LoadVideo()
        Try
            If Not Panel1.IsHandleCreated Then
                Panel1.CreateControl()
            End If

            If videoPlayer Is Nothing Then
                videoPlayer = New VideoPlayer()
                videoPlayer.Dock = DockStyle.Fill
                videoPlayer.Visible = True
                Panel1.Controls.Add(videoPlayer)
            End If

            If Not videoPlayer.IsHandleCreated Then
                videoPlayer.CreateControl()
            End If

            videoPlayer.Visible = True
            videoPlayer.BringToFront()

            Panel1.Refresh()
            Application.DoEvents()

            Dim loadSuccess As Boolean = videoPlayer.LoadVideo(inputFile)
            UpdateFormTitle()
            GetDurationFromFFprobe()

            currentPosition = 0
            txtStartTime.Text = "00:00:00"
            txtEndTime.Text = FormatTime(videoDuration)

            If customTrackBar IsNot Nothing Then
                customTrackBar.Value = 0
            End If

            If txtDuration IsNot Nothing Then
                If videoDuration > 0 Then
                    txtDuration.Text = FormatTime(videoDuration)
                Else
                    txtDuration.Text = "Unknown"
                End If
            End If

            GetVideoInfoFromFFprobe(inputFile)

            If loadSuccess Then
                lblStatus.Text = "Status: Video loaded - " & Path.GetFileName(inputFile) & " | Press SPACE to Play/Pause"
            Else
                lblStatus.Text = "Status: Video loaded (preview may not be available)"
            End If

            AutoPlayVideo()
        Catch ex As Exception
            lblStatus.Text = "Status: Error loading video"
            MessageBox.Show("Unable to load the selected video file." & vbNewLine & vbNewLine & _
                          "The file may be corrupted or in an unsupported format." & vbNewLine & _
                          "Please try another video file.", _
                          "Pongo Video Cutter Pro - Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub AutoPlayVideo()
        Try
            If videoDuration <= 0 Then
                Return
            End If

            isSettingStart = False
            currentStartTime = -1
            currentEndTime = -1

            txtStartTime.Text = "00:00:00"
            txtEndTime.Text = "00:00:00"

            System.Threading.Thread.Sleep(200)

            currentPosition = 0
            If videoPlayer IsNot Nothing Then
                videoPlayer.Seek(0)
                Application.DoEvents()
                System.Threading.Thread.Sleep(100)
            End If

            isPreviewPlaying = True
            isPlayingSegment = False
            jklPlaybackRate = 1.0

            If videoPlayer IsNot Nothing Then
                If trkVolume IsNot Nothing Then
                    videoPlayer.SetVolumePercent(trkVolume.Value)
                End If
                videoPlayer.Play()
            End If

            Timer3.Start()
            If btnPlayPause IsNot Nothing Then
                btnPlayPause.Image = My.Resources.bpause
            End If
            lblStatus.Text = "Status: Video playing... | Click 'Set Start' to mark segment beginning | Press SPACE to Play/Pause"

        Catch ex As Exception
            System.IO.File.WriteAllText(Application.StartupPath & "\autoplay_error.log", _
                "Error: " & ex.Message & vbNewLine & ex.StackTrace)
        End Try
    End Sub

    Private Sub GetDurationFromFFprobe()
        Try
            Dim ffprobe As New Process()
            ffprobe.StartInfo.FileName = ffprobePath
            ffprobe.StartInfo.Arguments = String.Format( _
                "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 ""{0}""", _
                inputFile _
            )
            ffprobe.StartInfo.UseShellExecute = False
            ffprobe.StartInfo.RedirectStandardOutput = True
            ffprobe.StartInfo.RedirectStandardError = True
            ffprobe.StartInfo.CreateNoWindow = True

            ffprobe.Start()
            Dim output As String = ffprobe.StandardOutput.ReadToEnd().Trim()
            Dim errorOutput As String = ffprobe.StandardError.ReadToEnd()
            ffprobe.WaitForExit()

            If Double.TryParse(output, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, videoDuration) Then
                If customTrackBar IsNot Nothing Then
                    customTrackBar.VideoDuration = videoDuration
                End If

                If txtEndTime IsNot Nothing Then
                    txtEndTime.Text = FormatTime(videoDuration)
                End If

                If txtDuration IsNot Nothing Then
                    txtDuration.Text = FormatTime(videoDuration)
                End If
            Else
                videoDuration = 0
                MessageBox.Show("Unable to determine video duration." & vbNewLine & _
                              "The video file may be corrupted or unsupported.", _
                              "Pongo Video Cutter Pro - Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If

        Catch ex As Exception
            videoDuration = 0
            System.IO.File.WriteAllText(Application.StartupPath & "\ffprobe_error.log", _
                "Error: " & ex.Message & vbNewLine & ex.StackTrace)
        End Try
    End Sub

    Private Sub UpdateTimeline()
        Try
            If videoPlayer IsNot Nothing AndAlso videoPlayer.IsVideoPlaying() Then
                currentPosition = videoPlayer.GetCurrentPosition()
            End If

            If videoDuration > 0 AndAlso customTrackBar IsNot Nothing Then
                isUpdatingTrackbar = True
                customTrackBar.Value = CInt((currentPosition / videoDuration) * 1000)
                isUpdatingTrackbar = False
            End If

            UpdateCurrentTimeDisplay()
        Catch ex As Exception
        End Try
    End Sub

    Private Sub btnPlayPause_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnPlayPause.Click
        Me.ActiveControl = Nothing
        TogglePreviewPlay()
    End Sub

    Private Sub TogglePreviewPlay()
        If String.IsNullOrEmpty(inputFile) Then
            MessageBox.Show("Please select a video file first!", "Pongo Video Cutter Pro - Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        If isPreviewPlaying Then
            isPreviewPlaying = False
            jklPlaybackRate = 0
            If videoPlayer IsNot Nothing Then
                videoPlayer.Pause()
            End If
            Timer3.Stop()
            btnPlayPause.Image = My.Resources.bplay
            lblStatus.Text = "Status: Preview paused at " & FormatTime(currentPosition)
        Else
            isPlayingSegment = False
            jklPlaybackRate = 1.0

            If currentPosition >= videoDuration Then
                currentPosition = 0
                If videoPlayer IsNot Nothing Then
                    videoPlayer.Seek(0)
                End If
            End If

            isPreviewPlaying = True
            If videoPlayer IsNot Nothing Then
                If trkVolume IsNot Nothing Then
                    videoPlayer.SetVolumePercent(trkVolume.Value)
                End If
                videoPlayer.Play()
            End If
            Timer3.Start()
            btnPlayPause.Image = My.Resources.bpause
            lblStatus.Text = "Status: Preview playing..."
        End If
    End Sub

    Private Sub StopPreview()
        isPreviewPlaying = False
        isPlayingSegment = False
        jklPlaybackRate = 0
        Timer3.Stop()
        If videoPlayer IsNot Nothing Then
            videoPlayer.StopVideo()
        End If
        If btnPlayPause IsNot Nothing Then
            btnPlayPause.Image = My.Resources.bplay
        End If
    End Sub

    Private Function IsPositionInSegment(ByVal pos As Double) As Boolean
        For i As Integer = 0 To lstSegments.Items.Count - 1
            Dim seg As VideoSegment = DirectCast(lstSegments.Items(i), VideoSegment)
            If pos >= seg.StartTime AndAlso pos <= seg.EndTime Then
                Return True
            End If
        Next
        Return False
    End Function

    Private Function GetNextSegmentStart(ByVal pos As Double) As Double
        Dim earliest As Double = -1
        For i As Integer = 0 To lstSegments.Items.Count - 1
            Dim seg As VideoSegment = DirectCast(lstSegments.Items(i), VideoSegment)
            If seg.StartTime > pos Then
                If earliest < 0 OrElse seg.StartTime < earliest Then
                    earliest = seg.StartTime
                End If
            End If
        Next
        Return earliest
    End Function

    Private Sub UpdatePlaySegmentsOnlyState()
        If chkPlaySegmentsOnly Is Nothing Then Return
        If lstSegments.Items.Count > 0 AndAlso Not String.IsNullOrEmpty(inputFile) Then
            chkPlaySegmentsOnly.Enabled = True
        Else
            chkPlaySegmentsOnly.Enabled = False
            chkPlaySegmentsOnly.Checked = False
            If customTrackBar IsNot Nothing Then
                customTrackBar.SegmentOnlyMode = False
            End If
        End If
    End Sub

    Private Sub chkPlaySegmentsOnly_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkPlaySegmentsOnly.CheckedChanged
        If customTrackBar IsNot Nothing Then
            customTrackBar.SegmentOnlyMode = chkPlaySegmentsOnly.Checked
        End If
        If chkPlaySegmentsOnly.Checked Then
            lblStatus.Text = "Status: Play Segments Only mode ON - non-segment areas will be skipped"
        Else
            lblStatus.Text = "Status: Normal playback mode"
        End If
    End Sub

    Private Sub btnSetStart_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnSetStart.Click
        Me.ActiveControl = Nothing

        txtStartTime.Text = FormatTime(currentPosition)
        currentStartTime = currentPosition
        isSettingStart = True

        If customTrackBar IsNot Nothing AndAlso videoDuration > 0 Then
            customTrackBar.TempStartMarker = CInt((currentPosition / videoDuration) * 1000)
            customTrackBar.TempEndMarker = -1
        End If

        lblStatus.Text = "Status: Start time set to " & txtStartTime.Text & " | Now click 'Set End'"
    End Sub

    Private Sub btnSetEnd_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnSetEnd.Click
        Me.ActiveControl = Nothing
        If Not isSettingStart OrElse currentStartTime < 0 Then
            MessageBox.Show("Please click 'Set Start' first to mark the beginning of the segment!", _
                          "Pongo Video Cutter Pro - Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        txtEndTime.Text = FormatTime(currentPosition)
        currentEndTime = currentPosition

        If currentEndTime <= currentStartTime Then
            MessageBox.Show("End time must be greater than start time!", _
                          "Pongo Video Cutter Pro - Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        If customTrackBar IsNot Nothing AndAlso videoDuration > 0 Then
            Dim startValue As Integer = CInt((currentStartTime / videoDuration) * 1000)
            Dim endValue As Integer = CInt((currentEndTime / videoDuration) * 1000)
            customTrackBar.AddSegment(startValue, endValue)
        End If

        Dim seg As New VideoSegment(currentStartTime, currentEndTime, lstSegments.Items.Count + 1)
        lstSegments.Items.Add(seg)
        TryCast(lstSegments, ModernListBox).UpdateHorizontalExtent()

        isSettingStart = False
        currentStartTime = -1
        currentEndTime = -1

        lblStatus.Text = String.Format("Status: Segment {0} added! Click 'Set Start' to create another segment", lstSegments.Items.Count)
        hasUnsavedChanges = True
        UpdatePlaySegmentsOnlyState()
    End Sub

    Private Sub btnRemoveSegment_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnRemoveSegment.Click
        Me.ActiveControl = Nothing
        If lstSegments.SelectedIndex >= 0 Then

            Dim index As Integer = lstSegments.SelectedIndex
            lstSegments.Items.RemoveAt(index)
            If customTrackBar IsNot Nothing Then
                customTrackBar.RemoveSegment(index)
            End If

            For i As Integer = 0 To lstSegments.Items.Count - 1
                Dim seg As VideoSegment = DirectCast(lstSegments.Items(i), VideoSegment)
                seg.SegmentIndex = i + 1
                lstSegments.Items(i) = seg
            Next
            TryCast(lstSegments, ModernListBox).UpdateHorizontalExtent()

            lblStatus.Text = "Status: Segment removed - Total: " & lstSegments.Items.Count & " segments"
            hasUnsavedChanges = True
            UpdatePlaySegmentsOnlyState()
        Else
            MessageBox.Show("Please select a segment to remove!", _
                          "Pongo Video Cutter Pro - Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Private Sub btnClearSegments_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnClearSegments.Click
        Me.ActiveControl = Nothing
        ' (Removed empty If-block; it was a no-op.)
        lstSegments.Items.Clear()
        If customTrackBar IsNot Nothing Then
            customTrackBar.ClearSegments()
        End If
        isSettingStart = False
        currentStartTime = -1
        currentEndTime = -1
        lblStatus.Text = "Status: All segments cleared"
        hasUnsavedChanges = True
        UpdatePlaySegmentsOnlyState()
    End Sub

    Private Sub btnSaveAs_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnSaveAs.Click
        Me.ActiveControl = Nothing
        If String.IsNullOrEmpty(inputFile) Then
            MessageBox.Show("Please select an input video file first!", _
                          "Pongo Video Cutter Pro - Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        SaveFileDialog1.Title = "Save Video As"
        Dim ext As String = Path.GetExtension(inputFile)
        SaveFileDialog1.Filter = "Video Files|*" & ext & "|MP4|*.mp4|MKV|*.mkv|AVI|*.avi|All Files|*.*"
        SaveFileDialog1.FileName = Path.GetFileNameWithoutExtension(inputFile) & "_cut" & ext

        If SaveFileDialog1.ShowDialog() = DialogResult.OK Then
            outputFile = SaveFileDialog1.FileName
            txtOutputFile.Text = outputFile
            lblStatus.Text = "Status: Output file set"
        End If
    End Sub

    Private Sub btnAddCaption_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnAddCaption.Click
        Me.ActiveControl = Nothing
        EditSegmentCaption()
    End Sub

    Private Sub lstSegments_DoubleClick(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles lstSegments.DoubleClick
        EditSegmentCaption()
    End Sub

    Private Sub EditSegmentCaption()
        If lstSegments.SelectedIndex < 0 Then
            MessageBox.Show("Please select a segment first to add/edit its caption.", "Pongo Video Cutter Pro - Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim seg As VideoSegment = DirectCast(lstSegments.Items(lstSegments.SelectedIndex), VideoSegment)
        Dim captionForm As New frmCaption()
        captionForm.SegmentIndex = lstSegments.SelectedIndex + 1
        captionForm.StartTime = FormatTime(seg.StartTime)
        captionForm.EndTime = FormatTime(seg.EndTime)
        captionForm.Caption = seg.Caption

        If captionForm.ShowDialog() = DialogResult.OK Then
            seg.Caption = captionForm.Caption
            lstSegments.Items(lstSegments.SelectedIndex) = seg
            TryCast(lstSegments, ModernListBox).UpdateHorizontalExtent()
            hasUnsavedChanges = True
            lblStatus.Text = "Status: Caption updated for segment " & (lstSegments.SelectedIndex + 1)
        End If
        captionForm.Dispose()
    End Sub

    Private Sub SaveProjectToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles SaveProjectToolStripMenuItem.Click
        Me.ActiveControl = Nothing
        If String.IsNullOrEmpty(inputFile) Then
            MessageBox.Show("Please open a video file first.", "Pongo Video Cutter Pro - Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If lstSegments.Items.Count = 0 Then
            MessageBox.Show("Please add at least one segment before saving the project.", "Pongo Video Cutter Pro - Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        SaveFileDialog1.Title = "Save Project"
        SaveFileDialog1.Filter = "Pongo Project|*.pongo|All Files|*.*"
        SaveFileDialog1.FileName = Path.GetFileNameWithoutExtension(inputFile) & ".pongo"
        If SaveFileDialog1.ShowDialog() = DialogResult.OK Then
            Try
                SaveProject(SaveFileDialog1.FileName)
                lblStatus.Text = "Status: Project saved to " & Path.GetFileName(SaveFileDialog1.FileName)
                hasUnsavedChanges = False
            Catch ex As Exception
                MessageBox.Show("Failed to save project: " & ex.Message, "Pongo Video Cutter Pro - Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub

    Private Sub SaveProject(ByVal filePath As String)
        Dim sb As New System.Text.StringBuilder()
        sb.AppendLine("PONGO_PROJECT_V1")
        sb.AppendLine("VideoFile=" & inputFile)
        sb.AppendLine("SegmentCount=" & lstSegments.Items.Count.ToString())
        For i As Integer = 0 To lstSegments.Items.Count - 1
            Dim seg As VideoSegment = DirectCast(lstSegments.Items(i), VideoSegment)
            sb.AppendLine("SEG" & (i + 1).ToString() & "_Start=" & seg.StartTime.ToString("F6", System.Globalization.CultureInfo.InvariantCulture))
            sb.AppendLine("SEG" & (i + 1).ToString() & "_End=" & seg.EndTime.ToString("F6", System.Globalization.CultureInfo.InvariantCulture))
            sb.AppendLine("SEG" & (i + 1).ToString() & "_Caption=" & EncodeBase64(seg.Caption))
        Next
        System.IO.File.WriteAllText(filePath, sb.ToString(), System.Text.Encoding.UTF8)
    End Sub

    Private Function EncodeBase64(ByVal text As String) As String
        If String.IsNullOrEmpty(text) Then Return ""
        Try
            Dim bytes As Byte() = System.Text.Encoding.UTF8.GetBytes(text)
            Return Convert.ToBase64String(bytes)
        Catch
            Return ""
        End Try
    End Function

    Private Function DecodeBase64(ByVal base64 As String) As String
        If String.IsNullOrEmpty(base64) Then Return ""
        Try
            Dim bytes As Byte() = Convert.FromBase64String(base64)
            Return System.Text.Encoding.UTF8.GetString(bytes)
        Catch
            Return ""
        End Try
    End Function

    Private Sub LoadProjectToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles LoadProjectToolStripMenuItem.Click
        Me.ActiveControl = Nothing
        OpenFileDialog1.Title = "Load Project"
        OpenFileDialog1.Filter = "Pongo Project|*.pongo|All Files|*.*"
        OpenFileDialog1.FileName = ""
        If OpenFileDialog1.ShowDialog() = DialogResult.OK Then
            Try
                LoadProject(OpenFileDialog1.FileName)
                lblStatus.Text = "Status: Project loaded - " & Path.GetFileName(OpenFileDialog1.FileName)
            Catch ex As Exception
                MessageBox.Show("Failed to load project: " & ex.Message, "Pongo Video Cutter Pro - Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub

    Private Sub LoadProject(ByVal filePath As String)
        If Not System.IO.File.Exists(filePath) Then
            Throw New Exception("Project file not found.")
        End If

        Dim lines As String() = System.IO.File.ReadAllLines(filePath, System.Text.Encoding.UTF8)
        If lines.Length = 0 OrElse Not lines(0).StartsWith("PONGO_PROJECT") Then
            Throw New Exception("Invalid project file format.")
        End If

        Dim projVideoFile As String = ""
        Dim segCount As Integer = 0
        Dim segStarts As New List(Of Double)()
        Dim segEnds As New List(Of Double)()
        Dim segCaptions As New List(Of String)()
        Dim tempStarts As New Dictionary(Of Integer, Double)()
        Dim tempEnds As New Dictionary(Of Integer, Double)()
        Dim tempCaptions As New Dictionary(Of Integer, String)()

        For Each line As String In lines
            If line.StartsWith("VideoFile=") Then
                projVideoFile = line.Substring(10)
            ElseIf line.StartsWith("SegmentCount=") Then
                Integer.TryParse(line.Substring(13), segCount)
            ElseIf line.StartsWith("SEG") Then
                Dim usIdx As Integer = line.IndexOf("_"c, 3)
                If usIdx > 3 Then
                    Dim numStr As String = line.Substring(3, usIdx - 3)
                    Dim segNum As Integer
                    If Integer.TryParse(numStr, segNum) Then
                        Dim rest As String = line.Substring(usIdx + 1)
                        If rest.StartsWith("Start=") Then
                            Dim v As Double
                            If Double.TryParse(rest.Substring(6), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, v) Then
                                tempStarts(segNum) = v
                            End If
                        ElseIf rest.StartsWith("End=") Then
                            Dim v As Double
                            If Double.TryParse(rest.Substring(4), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, v) Then
                                tempEnds(segNum) = v
                            End If
                        ElseIf rest.StartsWith("Caption=") Then
                            tempCaptions(segNum) = DecodeBase64(rest.Substring(8))
                        End If
                    End If
                End If
            End If
        Next

        For i As Integer = 1 To segCount
            If tempStarts.ContainsKey(i) AndAlso tempEnds.ContainsKey(i) Then
                segStarts.Add(tempStarts(i))
                segEnds.Add(tempEnds(i))
                If tempCaptions.ContainsKey(i) Then
                    segCaptions.Add(tempCaptions(i))
                Else
                    segCaptions.Add("")
                End If
            End If
        Next

        If String.IsNullOrEmpty(projVideoFile) OrElse Not System.IO.File.Exists(projVideoFile) Then
            Throw New Exception("Video file not found: " & projVideoFile)
        End If

        If isProcessingFile Then
            MessageBox.Show("Please wait for the current video to finish loading...", "Pongo Video Cutter Pro", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        isProcessingFile = True
        Try
            StopPreview()

            inputFile = projVideoFile
            txtInputFile.Text = inputFile
            UpdateFormTitle()
            UpdateFileInfo(inputFile)
            AddToRecentFiles(inputFile)

            Dim ext As String = Path.GetExtension(inputFile)
            Dim dir As String = Path.GetDirectoryName(inputFile)
            Dim filename As String = Path.GetFileNameWithoutExtension(inputFile)
            txtOutputFile.Text = Path.Combine(dir, filename & "_cut" & ext)
            outputFile = txtOutputFile.Text

            lstSegments.Items.Clear()
            If customTrackBar IsNot Nothing Then
                customTrackBar.ClearSegments()
            End If
            isSettingStart = False
            currentStartTime = -1
            currentEndTime = -1
            isPlayingSegment = False
            isPreviewPlaying = False
            currentPosition = 0
            videoDuration = 0

            txtStartTime.Text = "00:00:00"
            txtEndTime.Text = "00:00:00"

            lblStatus.Text = "Status: Loading project video..."
            Application.DoEvents()

            LoadVideo()

            System.Threading.Thread.Sleep(300)
            Application.DoEvents()

            For i As Integer = 0 To segStarts.Count - 1
                Dim seg As New VideoSegment(segStarts(i), segEnds(i), i + 1)
                seg.Caption = segCaptions(i)
                lstSegments.Items.Add(seg)

                If customTrackBar IsNot Nothing AndAlso videoDuration > 0 Then
                    Dim startValue As Integer = CInt((seg.StartTime / videoDuration) * 1000)
                    Dim endValue As Integer = CInt((seg.EndTime / videoDuration) * 1000)
                    customTrackBar.AddSegment(startValue, endValue)
                End If
            Next

            If segStarts.Count > 0 Then
                txtStartTime.Text = FormatTime(segStarts(0))
                txtEndTime.Text = FormatTime(segEnds(0))
            End If

            TryCast(lstSegments, ModernListBox).UpdateHorizontalExtent()
            lblStatus.Text = String.Format("Status: Project loaded - {0} segments restored", lstSegments.Items.Count)
            hasUnsavedChanges = False
            UpdatePlaySegmentsOnlyState()

        Finally
            isProcessingFile = False
            If Panel1 IsNot Nothing Then
                Panel1.BackColor = Color.Black
            End If
        End Try
    End Sub

    Private Sub btnCut_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnCut.Click
        Me.ActiveControl = Nothing
        If isPreviewPlaying Then
            TogglePreviewPlay()
        End If

        If String.IsNullOrEmpty(inputFile) Then
            MessageBox.Show("Please select an input video file first!", _
                          "Pongo Video Cutter Pro - Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        If lstSegments.Items.Count = 0 Then
            MessageBox.Show("Please add at least 1 segment to cut!" & vbNewLine & vbNewLine & _
                          "Steps:" & vbNewLine & _
                          "1. Click 'Set Start' at the beginning position" & vbNewLine & _
                          "2. Click 'Set End' at the ending position", _
                          "Pongo Video Cutter Pro - Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim mode As String = IIf(chkMergeOutput.Checked, "Merge into 1 file", "Split per segment")
        Dim result As DialogResult = MessageBox.Show("Process " & lstSegments.Items.Count & " segments?" & vbNewLine & vbNewLine & _
            "Mode: " & mode & vbNewLine & _
            "Input: " & Path.GetFileName(inputFile), _
            "Pongo Video Cutter Pro - Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question)

        If result = DialogResult.No Then Return

        ProcessAllSegments()

        If batchQueue.Count > 0 Then
            ProcessBatchQueue()
        End If
    End Sub



    Private Sub ProcessAllSegments()
        Try
            SetControlsEnabled(False)

            ProgressBar1.Style = ProgressBarStyle.Continuous
            ProgressBar1.Minimum = 0
            ProgressBar1.Maximum = lstSegments.Items.Count
            ProgressBar1.Value = 0

            Dim segmentFiles As New List(Of String)()
            Dim ext As String = Path.GetExtension(inputFile)
            Dim baseDir As String = Path.GetDirectoryName(inputFile)
            Dim baseName As String = Path.GetFileNameWithoutExtension(inputFile)

            If String.IsNullOrEmpty(outputFile) Then
                If chkMergeOutput.Checked OrElse lstSegments.Items.Count > 1 Then
                    outputFile = Path.Combine(baseDir, baseName & "_merged" & ext)
                Else
                    outputFile = Path.Combine(baseDir, baseName & "_cut" & ext)
                End If
                txtOutputFile.Text = outputFile
            End If

            If lstSegments.Items.Count = 1 AndAlso Not chkMergeOutput.Checked Then
                Dim seg As VideoSegment = DirectCast(lstSegments.Items(0), VideoSegment)

                lblStatus.Text = "Status: Processing 1 segment..."
                Application.DoEvents()

                Dim startTimeStr As String = FormatTimeForFFmpeg(seg.StartTime)
                Dim duration As Double = seg.EndTime - seg.StartTime
                Dim durationStr As String = FormatTimeForFFmpeg(duration)

                Dim args As String = String.Format("-ss {0} -i ""{1}"" -t {2} -c copy -avoid_negative_ts make_zero -y ""{3}""", startTimeStr, inputFile, durationStr, outputFile)

                If Not ExecuteFFmpegWithProgress(args, seg.StartTime, seg.EndTime) Then
                    Dim fallbackArgs As String = String.Format("-ss {0} -i ""{1}"" -t {2} -c:v libx264 -c:a aac -avoid_negative_ts make_zero -y ""{3}""", startTimeStr, inputFile, durationStr, outputFile)
                    If Not ExecuteFFmpegSilent(fallbackArgs) Then
                        Throw New Exception("Failed to process segment")
                    End If
                End If

                ProgressBar1.Value = 1
                lblStatus.Text = "Status: COMPLETE!"

                MessageBox.Show("Video cut successfully!" & vbNewLine & vbNewLine & _
                              "Output: " & outputFile, _
                              "Pongo Video Cutter Pro - Success", MessageBoxButtons.OK, MessageBoxIcon.Information)

                OpenOutputFolder(outputFile)
                Return
            End If

            For i As Integer = 0 To lstSegments.Items.Count - 1
                Dim seg As VideoSegment = DirectCast(lstSegments.Items(i), VideoSegment)

                lblStatus.Text = String.Format("Status: Processing segment {0}/{1}...", i + 1, lstSegments.Items.Count)
                Application.DoEvents()

                Dim segFile As String = Path.Combine(baseDir, String.Format("{0}_seg{1:00}_temp{2}", baseName, i + 1, ext))
                segmentFiles.Add(segFile)

                Dim startTimeStr As String = FormatTimeForFFmpeg(seg.StartTime)
                Dim duration As Double = seg.EndTime - seg.StartTime
                Dim durationStr As String = FormatTimeForFFmpeg(duration)

                Dim args As String = String.Format("-ss {0} -i ""{1}"" -t {2} -c copy -avoid_negative_ts make_zero -y ""{3}""", startTimeStr, inputFile, durationStr, segFile)

                If Not ExecuteFFmpegWithProgress(args, seg.StartTime, seg.EndTime) Then
                    Dim fallbackArgs As String = String.Format("-ss {0} -i ""{1}"" -t {2} -c:v libx264 -c:a aac -avoid_negative_ts make_zero -y ""{3}""", startTimeStr, inputFile, durationStr, segFile)
                    If Not ExecuteFFmpegSilent(fallbackArgs) Then
                        Throw New Exception("Failed to process segment " & (i + 1))
                    End If
                End If

                If Not System.IO.File.Exists(segFile) Then
                    Throw New Exception("Segment " & (i + 1) & " file was not found after processing")
                End If

                ProgressBar1.Value = i + 1
                Application.DoEvents()
            Next

            If chkMergeOutput.Checked Then
                lblStatus.Text = "Status: Merging segments..."
                Application.DoEvents()

                Dim listFile As String = Path.Combine(baseDir, "filelist.txt")
                Dim sb As New System.Text.StringBuilder()

                For Each file As String In segmentFiles
                    Dim safeFile As String = file.Replace("'", "'\''")
                    sb.AppendLine("file '" & safeFile & "'")
                Next

                System.IO.File.WriteAllText(listFile, sb.ToString())

                Dim mergeArgs As String = String.Format("-f concat -safe 0 -i ""{0}"" -c copy -y ""{1}""", listFile, outputFile)

                Dim mergeSuccess As Boolean = ExecuteFFmpegSilent(mergeArgs)

                If System.IO.File.Exists(listFile) Then
                    System.IO.File.Delete(listFile)
                End If

                For Each file As String In segmentFiles
                    If System.IO.File.Exists(file) Then
                        System.IO.File.Delete(file)
                    End If
                Next

                If Not mergeSuccess Then
                    Throw New Exception("Failed to merge segments")
                End If

                If Not System.IO.File.Exists(outputFile) Then
                    Throw New Exception("Output file not found after merge process")
                End If

            Else
                For i As Integer = 0 To segmentFiles.Count - 1
                    Dim finalFile As String = Path.Combine(baseDir, String.Format("{0}_seg{1:00}{2}", baseName, i + 1, ext))

                    If System.IO.File.Exists(finalFile) Then
                        System.IO.File.Delete(finalFile)
                    End If

                    If System.IO.File.Exists(segmentFiles(i)) Then
                        System.IO.File.Move(segmentFiles(i), finalFile)
                    End If
                Next

                outputFile = Path.Combine(baseDir, String.Format("{0}_seg{1:00}{2}", baseName, 1, ext))
                txtOutputFile.Text = outputFile
            End If

            ProgressBar1.Value = ProgressBar1.Maximum
            lblStatus.Text = "Status: COMPLETE!"

            If chkMergeOutput.Checked Then
                MessageBox.Show("All segments merged successfully!" & vbNewLine & vbNewLine & _
                              "Total segments: " & lstSegments.Items.Count & vbNewLine & _
                              "Output: " & outputFile, _
                              "Pongo Video Cutter Pro - Success", MessageBoxButtons.OK, MessageBoxIcon.Information)

                OpenOutputFolder(outputFile)
            Else
                Dim fileNames As New System.Text.StringBuilder()
                For Each file As String In segmentFiles
                    Dim finalName As String = file.Replace("_temp", "")
                    fileNames.AppendLine(Path.GetFileName(finalName))
                Next

                MessageBox.Show("All segments cut successfully!" & vbNewLine & vbNewLine & _
                              "Total segments: " & lstSegments.Items.Count & vbNewLine & vbNewLine & _
                              "Output files:" & vbNewLine & _
                              fileNames.ToString(), _
                              "Pongo Video Cutter Pro - Success", MessageBoxButtons.OK, MessageBoxIcon.Information)

                OpenOutputFolder(outputFile)
            End If

        Catch ex As Exception
            lblStatus.Text = "Status: ERROR!"
            Dim errorMsg As String = "An error occurred while processing the video." & vbNewLine & vbNewLine
            errorMsg &= "Error: " & ex.Message & vbNewLine & vbNewLine

            Dim ffmpegErr As String = GetFFmpegErrorSummary()
            If Not String.IsNullOrEmpty(ffmpegErr) Then
                errorMsg &= "FFmpeg Output (last 15 lines):" & vbNewLine & ffmpegErr & vbNewLine
            End If

            errorMsg &= "Please check that:" & vbNewLine & _
                        "• The input file is valid and not corrupted" & vbNewLine & _
                        "• You have enough disk space" & vbNewLine & _
                        "• The output directory is writable" & vbNewLine & _
                        "• FFmpeg supports the selected output codec" & vbNewLine & vbNewLine & _
                        "Full error log saved to: " & Application.StartupPath & "\ffmpeg_error.log"

            MessageBox.Show(errorMsg, _
                          "Pongo Video Cutter Pro - Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            If ProgressBar1 IsNot Nothing Then
                ProgressBar1.Value = 0
            End If

            SetControlsEnabled(True)

            Me.Cursor = Cursors.Default
        End Try
    End Sub

    ' (Moved up to the field declarations block at the top of Form1.)

    Private Function ExecuteFFmpegWithProgress(ByVal args As String, ByVal segStart As Double, ByVal segEnd As Double) As Boolean
        Try
            lastFFmpegError = ""
            Dim ffmpeg As New Process()
            ffmpeg.StartInfo.FileName = ffmpegPath
            ffmpeg.StartInfo.Arguments = args
            ffmpeg.StartInfo.UseShellExecute = False
            ffmpeg.StartInfo.CreateNoWindow = True
            ffmpeg.StartInfo.RedirectStandardError = True
            ffmpeg.StartInfo.RedirectStandardOutput = True
            ffmpeg.StartInfo.WorkingDirectory = Application.StartupPath

            Dim errorBuilder As New System.Text.StringBuilder()
            Dim outputBuilder As New System.Text.StringBuilder()

            AddHandler ffmpeg.ErrorDataReceived, Sub(s As Object, e As DataReceivedEventArgs)
                                                    If e.Data IsNot Nothing Then
                                                        errorBuilder.AppendLine(e.Data)
                                                    End If
                                                End Sub
            AddHandler ffmpeg.OutputDataReceived, Sub(s As Object, e As DataReceivedEventArgs)
                                                      If e.Data IsNot Nothing Then
                                                          outputBuilder.AppendLine(e.Data)
                                                      End If
                                                  End Sub

            ffmpeg.Start()
            ffmpeg.BeginErrorReadLine()
            ffmpeg.BeginOutputReadLine()

            Dim segDuration As Double = segEnd - segStart
            If segDuration <= 0 Then segDuration = 1.0

            Dim lastProgressUpdate As DateTime = DateTime.Now

            Do While Not ffmpeg.HasExited
                System.Threading.Thread.Sleep(100)
                Application.DoEvents()

                If (DateTime.Now - lastProgressUpdate).TotalMilliseconds >= 200 Then
                    lastProgressUpdate = DateTime.Now
                    Dim errorLines As String() = errorBuilder.ToString().Split(New String() {vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                    If errorLines.Length > 0 Then
                        Dim lastLine As String = errorLines(errorLines.Length - 1)
                        If lastLine.Contains("time=") Then
                            Try
                                Dim timeIdx As Integer = lastLine.IndexOf("time=")
                                Dim timeStr As String = lastLine.Substring(timeIdx + 5).Trim()
                                Dim endIdx As Integer = timeStr.IndexOf(" ")
                                If endIdx > 0 Then timeStr = timeStr.Substring(0, endIdx)
                                Dim currentTime As Double
                                If Double.TryParse(timeStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, currentTime) Then
                                    Dim pct As Integer = CInt((currentTime / segDuration) * 100)
                                    If pct < 0 Then pct = 0
                                    If pct > 100 Then pct = 100
                                    If ProgressBar1 IsNot Nothing Then
                                        ProgressBar1.Style = ProgressBarStyle.Continuous
                                        ProgressBar1.Value = pct
                                    End If
                                    lblStatus.Text = String.Format("Status: Processing... {0}%", pct)
                                    Application.DoEvents()
                                End If
                            Catch
                            End Try
                        End If
                    End If
                End If
            Loop

            ffmpeg.WaitForExit()

            Dim errorOutput As String = errorBuilder.ToString()

            If ffmpeg.ExitCode <> 0 Then
                lastFFmpegError = errorOutput
                System.IO.File.WriteAllText(Application.StartupPath & "\ffmpeg_error.log", _
                    "Command: " & args & vbNewLine & _
                    "Error: " & errorOutput & vbNewLine & _
                    "Output: " & outputBuilder.ToString())
            End If

            Return ffmpeg.ExitCode = 0

        Catch ex As Exception
            lastFFmpegError = ex.Message
            System.IO.File.WriteAllText(Application.StartupPath & "\ffmpeg_exception.log", _
                "Exception: " & ex.Message & vbNewLine & _
                "Stack: " & ex.StackTrace)
            Return False
        End Try
    End Function

    Private Function ExecuteFFmpegSilent(ByVal args As String) As Boolean
        Try
            lastFFmpegError = ""
            Dim ffmpeg As New Process()
            ffmpeg.StartInfo.FileName = ffmpegPath
            ffmpeg.StartInfo.Arguments = args
            ffmpeg.StartInfo.UseShellExecute = False
            ffmpeg.StartInfo.CreateNoWindow = True
            ffmpeg.StartInfo.RedirectStandardError = True
            ffmpeg.StartInfo.RedirectStandardOutput = True
            ffmpeg.StartInfo.WorkingDirectory = Application.StartupPath

            Dim errorBuilder As New System.Text.StringBuilder()
            Dim outputBuilder As New System.Text.StringBuilder()

            AddHandler ffmpeg.ErrorDataReceived, Sub(s As Object, e As DataReceivedEventArgs)
                                                    If e.Data IsNot Nothing Then
                                                        errorBuilder.AppendLine(e.Data)
                                                    End If
                                                End Sub
            AddHandler ffmpeg.OutputDataReceived, Sub(s As Object, e As DataReceivedEventArgs)
                                                      If e.Data IsNot Nothing Then
                                                          outputBuilder.AppendLine(e.Data)
                                                      End If
                                                  End Sub

            ffmpeg.Start()
            ffmpeg.BeginErrorReadLine()
            ffmpeg.BeginOutputReadLine()

            ffmpeg.WaitForExit()

            Dim errorOutput As String = errorBuilder.ToString()

            If ffmpeg.ExitCode <> 0 Then
                lastFFmpegError = errorOutput
                System.IO.File.WriteAllText(Application.StartupPath & "\ffmpeg_error.log", _
                    "Command: " & args & vbNewLine & _
                    "Error: " & errorOutput & vbNewLine & _
                    "Output: " & outputBuilder.ToString())
            End If

            Return ffmpeg.ExitCode = 0

        Catch ex As Exception
            lastFFmpegError = ex.Message
            System.IO.File.WriteAllText(Application.StartupPath & "\ffmpeg_exception.log", _
                "Exception: " & ex.Message & vbNewLine & _
                "Stack: " & ex.StackTrace)
            Return False
        End Try
    End Function

    Private Function GetFFmpegErrorSummary() As String
        If String.IsNullOrEmpty(lastFFmpegError) Then Return ""
        Dim lines As String() = lastFFmpegError.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
        If lines.Length = 0 Then Return ""
        Dim sb As New System.Text.StringBuilder()
        Dim startIdx As Integer = Math.Max(0, lines.Length - 15)
        For i As Integer = startIdx To lines.Length - 1
            sb.AppendLine(lines(i))
        Next
        Return sb.ToString()
    End Function

    Private Sub SetControlsEnabled(ByVal enabled As Boolean)
        btnCut.Enabled = enabled
        btnBrowse.Enabled = enabled
        btnSaveAs.Enabled = enabled
        btnSetStart.Enabled = enabled
        btnSetEnd.Enabled = enabled
        btnPlayPause.Enabled = enabled
        btnRemoveSegment.Enabled = enabled
        btnClearSegments.Enabled = enabled
        btnAddCaption.Enabled = enabled

        If customTrackBar IsNot Nothing Then
            customTrackBar.Enabled = enabled
        End If

        chkMergeOutput.Enabled = enabled

        If lstSegments IsNot Nothing Then
            lstSegments.Enabled = enabled
        End If

        If txtInputFile IsNot Nothing Then
            txtInputFile.Enabled = enabled
        End If

        If txtOutputFile IsNot Nothing Then
            txtOutputFile.Enabled = enabled
        End If

        If txtStartTime IsNot Nothing Then
            txtStartTime.Enabled = enabled
        End If

        If txtEndTime IsNot Nothing Then
            txtEndTime.Enabled = enabled
        End If

        If txtFilename IsNot Nothing Then
            txtFilename.Enabled = enabled
        End If

        If txtFilesize IsNot Nothing Then
            txtFilesize.Enabled = enabled
        End If

        If txtResolution IsNot Nothing Then
            txtResolution.Enabled = enabled
        End If

        If txtDuration IsNot Nothing Then
            txtDuration.Enabled = enabled
        End If

        If zoomInButton IsNot Nothing Then
            zoomInButton.Enabled = enabled
        End If

        If zoomOutButton IsNot Nothing Then
            zoomOutButton.Enabled = enabled
        End If

        If ZoomResetButton IsNot Nothing Then
            ZoomResetButton.Enabled = enabled
        End If

        If ZoomInTimelineToolStripMenuItem IsNot Nothing Then
            ZoomInTimelineToolStripMenuItem.Enabled = enabled
        End If

        If ZoomOutTimelineToolStripMenuItem IsNot Nothing Then
            ZoomOutTimelineToolStripMenuItem.Enabled = enabled
        End If

        If ResetZoomToolStripMenuItem IsNot Nothing Then
            ResetZoomToolStripMenuItem.Enabled = enabled
        End If

        If StartTimeToolStripMenuItem IsNot Nothing Then
            StartTimeToolStripMenuItem.Enabled = enabled
        End If

        If EndTimeToolStripMenuItem IsNot Nothing Then
            EndTimeToolStripMenuItem.Enabled = enabled
        End If

        If RemoveSegmentToolStripMenuItem IsNot Nothing Then
            RemoveSegmentToolStripMenuItem.Enabled = enabled
        End If

        If ClearAllSegmentToolStripMenuItem IsNot Nothing Then
            ClearAllSegmentToolStripMenuItem.Enabled = enabled
        End If

        If OpenVideoFilesToolStripMenuItem IsNot Nothing Then
            OpenVideoFilesToolStripMenuItem.Enabled = enabled
        End If

        If ExportToolStripMenuItem IsNot Nothing Then
            ExportToolStripMenuItem.Enabled = enabled
        End If

        Me.AllowDrop = enabled

        If enabled Then
            Me.Cursor = Cursors.Default
        Else
            Me.Cursor = Cursors.WaitCursor
        End If
    End Sub

    Private Function FormatTime(ByVal seconds As Double) As String
        Dim ts As TimeSpan = TimeSpan.FromSeconds(seconds)
        Return String.Format("{0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds)
    End Function

    Private Function FormatTimeForFFmpeg(ByVal seconds As Double) As String
        Dim time As TimeSpan = TimeSpan.FromSeconds(seconds)
        Return String.Format("{0:00}:{1:00}:{2:00}", time.Hours, time.Minutes, time.Seconds)
    End Function

    Private Sub OpenOutputFolder(ByVal filePath As String)
        Try
            If String.IsNullOrEmpty(filePath) Then
                Return
            End If

            Dim folderPath As String = Path.GetDirectoryName(filePath)

            If System.IO.Directory.Exists(folderPath) Then
                Process.Start("explorer.exe", """" & folderPath & """")
            Else
                Dim inputFolder As String = Path.GetDirectoryName(inputFile)
                If System.IO.Directory.Exists(inputFolder) Then
                    Process.Start("explorer.exe", """" & inputFolder & """")
                End If
            End If

        Catch ex As Exception
            System.IO.File.WriteAllText(Application.StartupPath & "\folderopen_error.log", _
                "Error: " & ex.Message & vbNewLine & ex.StackTrace)
        End Try
    End Sub

    Private Sub Form1_FormClosing(ByVal sender As Object, ByVal e As System.Windows.Forms.FormClosingEventArgs) Handles Me.FormClosing
        If hasUnsavedChanges AndAlso lstSegments.Items.Count > 0 Then
            Dim result As DialogResult = MessageBox.Show( _
                "You have unsaved segment changes." & vbNewLine & vbNewLine & _
                "Do you want to save your project before closing?" & vbNewLine & vbNewLine & _
                "Yes = Save project" & vbNewLine & _
                "No = Close without saving" & vbNewLine & _
                "Cancel = Stay in application", _
                "Pongo Video Cutter Pro - Unsaved Changes", _
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning)

            If result = DialogResult.Cancel Then
                e.Cancel = True
                Return
            ElseIf result = DialogResult.Yes Then
                If String.IsNullOrEmpty(inputFile) Then
                    MessageBox.Show("Cannot save project: no video file loaded.", "Pongo Video Cutter Pro", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    e.Cancel = True
                    Return
                End If
                SaveFileDialog1.Title = "Save Project"
                SaveFileDialog1.Filter = "Pongo Project|*.pongo|All Files|*.*"
                SaveFileDialog1.FileName = Path.GetFileNameWithoutExtension(inputFile) & ".pongo"
                If SaveFileDialog1.ShowDialog() = DialogResult.OK Then
                    Try
                        SaveProject(SaveFileDialog1.FileName)
                    Catch ex As Exception
                        MessageBox.Show("Failed to save project: " & ex.Message, "Pongo Video Cutter Pro - Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                        e.Cancel = True
                        Return
                    End Try
                Else
                    e.Cancel = True
                    Return
                End If
            End If
        End If

        Try
            If Me.WindowState = FormWindowState.Normal Then
                My.Settings.WindowWidth = Me.Width
                My.Settings.WindowHeight = Me.Height
                My.Settings.WindowState = CInt(Me.WindowState)
            ElseIf Me.WindowState = FormWindowState.Maximized Then
                My.Settings.WindowState = CInt(Me.WindowState)
            End If
            My.Settings.Save()
        Catch
        End Try

        StopPreview()

        If videoPlayer IsNot Nothing Then
            videoPlayer.Cleanup()
            videoPlayer.Dispose()
            videoPlayer = Nothing
        End If

        Dim listFile As String = Application.StartupPath & "\filelist.txt"
        If System.IO.File.Exists(listFile) Then
            System.IO.File.Delete(listFile)
        End If
        Application.Exit()
    End Sub

    Private Sub Timer3_Tick(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Timer3.Tick
        If isPreviewPlaying Then
            If customTrackBar IsNot Nothing AndAlso customTrackBar.IsDraggingSegment Then
                Return
            End If

            UpdateTimeline()

            If isPlayingSegment Then
                If currentPosition >= segmentPlayEnd Then
                    If chkPlaySegmentsOnly IsNot Nothing AndAlso chkPlaySegmentsOnly.Checked Then
                        Dim nextStart As Double = GetNextSegmentStart(segmentPlayEnd)
                        If nextStart >= 0 AndAlso nextStart < videoDuration Then
                            currentPosition = nextStart
                            Dim nextSegIdx As Integer = -1
                            For i As Integer = 0 To lstSegments.Items.Count - 1
                                Dim s As VideoSegment = DirectCast(lstSegments.Items(i), VideoSegment)
                                If Math.Abs(s.StartTime - nextStart) < 0.01 Then
                                    nextSegIdx = i
                                    Exit For
                                End If
                            Next
                            If nextSegIdx >= 0 Then
                                Dim nextSeg As VideoSegment = DirectCast(lstSegments.Items(nextSegIdx), VideoSegment)
                                segmentPlayStart = nextSeg.StartTime
                                segmentPlayEnd = nextSeg.EndTime
                                If videoPlayer IsNot Nothing Then
                                    videoPlayer.Seek(currentPosition)
                                End If
                                If customTrackBar IsNot Nothing Then
                                    isUpdatingTrackbar = True
                                    customTrackBar.Value = CInt((currentPosition / videoDuration) * 1000)
                                    isUpdatingTrackbar = False
                                End If
                                lblStatus.Text = String.Format("Status: Playing segment {0} ({1} - {2})", nextSegIdx + 1, FormatTime(nextSeg.StartTime), FormatTime(nextSeg.EndTime))
                            Else
                                StopPreview()
                                lblStatus.Text = "Status: Segment-only playback finished"
                            End If
                        Else
                            StopPreview()
                            lblStatus.Text = "Status: Segment-only playback finished"
                        End If
                    Else
                        StopPreview()
                        isPlayingSegment = False
                        lblStatus.Text = String.Format("Status: Segment playback finished")
                        currentPosition = segmentPlayEnd
                        If customTrackBar IsNot Nothing Then
                            customTrackBar.Value = CInt((currentPosition / videoDuration) * 1000)
                        End If
                    End If
                End If
            Else
                If chkPlaySegmentsOnly IsNot Nothing AndAlso chkPlaySegmentsOnly.Checked AndAlso lstSegments.Items.Count > 0 Then
                    If Not IsPositionInSegment(currentPosition) Then
                        Dim nextStart As Double = GetNextSegmentStart(currentPosition)
                        If nextStart >= 0 AndAlso nextStart < videoDuration Then
                            currentPosition = nextStart
                            If videoPlayer IsNot Nothing Then
                                videoPlayer.Seek(currentPosition)
                            End If
                            If customTrackBar IsNot Nothing Then
                                isUpdatingTrackbar = True
                                customTrackBar.Value = CInt((currentPosition / videoDuration) * 1000)
                                isUpdatingTrackbar = False
                            End If
                            lblStatus.Text = "Status: Skipped to next segment"
                        Else
                            StopPreview()
                            lblStatus.Text = "Status: Segment-only playback finished - no more segments"
                            currentPosition = 0
                            If customTrackBar IsNot Nothing Then
                                customTrackBar.Value = 0
                            End If
                        End If
                    End If
                End If

                If currentPosition >= videoDuration AndAlso videoDuration > 0 Then
                    StopPreview()
                    lblStatus.Text = "Status: Preview finished"
                    currentPosition = 0
                    If customTrackBar IsNot Nothing Then
                        customTrackBar.Value = 0
                    End If
                End If
            End If
        End If
    End Sub

    Private Sub lblStatus_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles lblStatus.Click

    End Sub

    Private Sub lstSegments_SelectedIndexChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles lstSegments.SelectedIndexChanged
        If isUpdatingSegmentUI Then Return

        If lstSegments.SelectedIndex >= 0 AndAlso lstSegments.SelectedIndex < lstSegments.Items.Count Then
            If customTrackBar IsNot Nothing Then
                customTrackBar.SelectedSegmentIndexValue = lstSegments.SelectedIndex
            End If

            If Not isUpdatingSegmentUI Then
                PlaySegment(lstSegments.SelectedIndex)
            End If
        End If
    End Sub

    Private Sub PlaySegment(ByVal segmentIndex As Integer)
        If segmentIndex < 0 OrElse segmentIndex >= lstSegments.Items.Count Then
            Return
        End If

        If String.IsNullOrEmpty(inputFile) Then
            MessageBox.Show("Please select a video file first!", "Pongo Video Cutter Pro - Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        If isPreviewPlaying Then
            isPreviewPlaying = False
            isPlayingSegment = False
            Timer3.Stop()
            If videoPlayer IsNot Nothing Then
                videoPlayer.Pause()
            End If
            Application.DoEvents()
            System.Threading.Thread.Sleep(50)
        End If

        Dim seg As VideoSegment = DirectCast(lstSegments.Items(segmentIndex), VideoSegment)

        currentPosition = seg.StartTime
        segmentPlayStart = seg.StartTime
        segmentPlayEnd = seg.EndTime

        isUpdatingTrackbar = True
        If customTrackBar IsNot Nothing Then
            customTrackBar.Value = CInt((currentPosition / videoDuration) * 1000)
        End If
        isUpdatingTrackbar = False

        If videoPlayer IsNot Nothing Then
            videoPlayer.Seek(currentPosition)
            Application.DoEvents()
            System.Threading.Thread.Sleep(100)
        End If

        isPreviewPlaying = True
        isPlayingSegment = True
        jklPlaybackRate = 1.0

        If videoPlayer IsNot Nothing Then
            videoPlayer.Play()
        End If

        Timer3.Start()
        btnPlayPause.Image = My.Resources.bpause
        lblStatus.Text = String.Format("Status: Playing Segment {0} ({1} - {2})", segmentIndex + 1, FormatTime(seg.StartTime), FormatTime(seg.EndTime))
    End Sub

    Private Sub zoomInButton_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles zoomInButton.Click
        Me.ActiveControl = Nothing
        If customTrackBar IsNot Nothing Then
            customTrackBar.ZoomIn()
        End If
    End Sub

    Private Sub zoomOutButton_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles zoomOutButton.Click
        Me.ActiveControl = Nothing
        If customTrackBar IsNot Nothing Then
            customTrackBar.ZoomOut()
        End If
    End Sub

    Private Sub ZoomResetButton_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ZoomResetButton.Click
        Me.ActiveControl = Nothing
        If customTrackBar IsNot Nothing Then
            customTrackBar.ResetZoom()
        End If
    End Sub

    Private Sub ZoomInTimelineToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ZoomInTimelineToolStripMenuItem.Click
        zoomInButton.PerformClick()
    End Sub

    Private Sub ZoomOutTimelineToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ZoomOutTimelineToolStripMenuItem.Click
        zoomOutButton.PerformClick()
    End Sub

    Private Sub ResetZoomToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ResetZoomToolStripMenuItem.Click
        ZoomResetButton.PerformClick()
    End Sub

    Private Sub StartTimeToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles StartTimeToolStripMenuItem.Click
        btnSetStart.PerformClick()
    End Sub

    Private Sub EndTimeToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles EndTimeToolStripMenuItem.Click
        btnSetEnd.PerformClick()
    End Sub

    Private Sub RemoveSegmentToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles RemoveSegmentToolStripMenuItem.Click
        btnRemoveSegment.PerformClick()
    End Sub

    Private Sub ClearAllSegmentToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ClearAllSegmentToolStripMenuItem.Click
        btnClearSegments.PerformClick()
    End Sub

    Private Sub OpenVideoFilesToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles OpenVideoFilesToolStripMenuItem.Click
        Me.ActiveControl = Nothing
        StopPreview()

        OpenFileDialog1.Title = "Select Video File"
        OpenFileDialog1.Filter = "Video Files|*.mp4;*.avi;*.mkv;*.mov;*.flv;*.wmv;*.ts;*.m4v;*.webm|All Files|*.*"
        OpenFileDialog1.FileName = ""

        If Not String.IsNullOrEmpty(My.Settings.LastFolder) AndAlso Directory.Exists(My.Settings.LastFolder) Then
            OpenFileDialog1.InitialDirectory = My.Settings.LastFolder
        End If

        If OpenFileDialog1.ShowDialog() = DialogResult.OK Then
            inputFile = OpenFileDialog1.FileName
            txtInputFile.Text = inputFile
            My.Settings.LastFolder = Path.GetDirectoryName(inputFile)
            My.Settings.Save()

            UpdateFormTitle()
            UpdateFileInfo(inputFile)
            AddToRecentFiles(inputFile)

            Dim ext As String = Path.GetExtension(inputFile)
            Dim dir As String = Path.GetDirectoryName(inputFile)
            Dim filename As String = Path.GetFileNameWithoutExtension(inputFile)
            txtOutputFile.Text = Path.Combine(dir, filename & "_cut" & ext)
            outputFile = txtOutputFile.Text

            lstSegments.Items.Clear()
            If customTrackBar IsNot Nothing Then
                customTrackBar.ClearSegments()
            End If
            isSettingStart = False
            currentStartTime = -1
            currentEndTime = -1
            isPlayingSegment = False
            isPreviewPlaying = False

            currentPosition = 0
            videoDuration = 0

            txtStartTime.Text = "00:00:00"
            txtEndTime.Text = "00:00:00"

            lblStatus.Text = "Status: Loading video..."
            hasUnsavedChanges = False
            Application.DoEvents()

            LoadVideo()
        End If
    End Sub

    Private Sub ExportToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ExportToolStripMenuItem.Click
        btnCut.PerformClick()
    End Sub

    Private Sub ExitToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ExitToolStripMenuItem.Click
        Me.Close()
    End Sub

    Private Sub RadioButton1_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles RadioButton1.CheckedChanged
        If RadioButton1.Checked Then
            RadioButton1.ForeColor = Color.FromArgb(0, 120, 215)
        Else
            RadioButton1.ForeColor = Color.FromArgb(110, 115, 125)
        End If
    End Sub

    Private Sub chkMergeOutput_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkMergeOutput.CheckedChanged
        If chkMergeOutput.Checked Then
            chkMergeOutput.ForeColor = Color.FromArgb(0, 120, 215)
        Else
            chkMergeOutput.ForeColor = Color.FromArgb(110, 115, 125)
        End If
    End Sub

    Private Sub FFmpegPathToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles FFmpegPathToolStripMenuItem.Click
        FFmpeg_Path.TextBox1.Text = ffmpegPath
        FFmpeg_Path.TextBox2.Text = ffprobePath
        FFmpeg_Path.ShowDialog()
    End Sub

    Private Sub PanelLeftToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles PanelLeftToolStripMenuItem.Click
        If PanelLeftToolStripMenuItem.Checked Then
            Panel2.Visible = False
            PanelLeftToolStripMenuItem.Checked = False
        Else
            Panel2.Visible = True
            PanelLeftToolStripMenuItem.Checked = True
        End If
    End Sub

    Private Sub PanelRightToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles PanelRightToolStripMenuItem.Click
        If PanelRightToolStripMenuItem.Checked Then
            Panel3.Visible = False
            PanelRightToolStripMenuItem.Checked = False
        Else
            Panel3.Visible = True
            PanelRightToolStripMenuItem.Checked = True
        End If
    End Sub

    ' NOTE: The "Donate" menu item has been removed from the Help menu.
    '       The DonateToolStripMenuItem_Click handler that used to live
    '       here has been deleted along with the menu item itself.

    Private Sub SendFeedbackToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles SendFeedbackToolStripMenuItem.Click
        ' Opens the user's mail client pre-addressed to the developer.
        Try
            Process.Start("mailto:arisohandriputra@gmail.com")
        Catch ex As Exception
            MessageBox.Show(ex.Message, _
                            "Pongo Video Cutter - Unable to Open Mail Client", _
                            MessageBoxButtons.OK, _
                            MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Function GetVideoMetadata(ByVal filePath As String) As String
        Try
            Dim ffprobe As New Process()
            ffprobe.StartInfo.FileName = ffprobePath
            ffprobe.StartInfo.Arguments = String.Format( _
                "-v error -show_format -show_streams ""{0}""", _
                filePath _
            )
            ffprobe.StartInfo.UseShellExecute = False
            ffprobe.StartInfo.RedirectStandardOutput = True
            ffprobe.StartInfo.RedirectStandardError = True
            ffprobe.StartInfo.CreateNoWindow = True
            ffprobe.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8

            ffprobe.Start()
            Dim output As String = ffprobe.StandardOutput.ReadToEnd()
            Dim errorOutput As String = ffprobe.StandardError.ReadToEnd()
            ffprobe.WaitForExit()

            If String.IsNullOrEmpty(output) Then
                Return "No metadata available"
            End If

            Return ParseMetadataSimple(output)

        Catch ex As Exception
            Return "Error getting metadata: " & ex.Message
        End Try
    End Function

    Private Function ParseMetadataSimple(ByVal output As String) As String
        Dim sb As New System.Text.StringBuilder()

        Try
            Dim lines As String() = output.Split(New Char() {ControlChars.Lf, ControlChars.Cr}, StringSplitOptions.RemoveEmptyEntries)

            Dim inFormat As Boolean = False
            Dim inStream As Boolean = False
            Dim streamIndex As Integer = 0
            Dim currentStreamInfo As String = ""
            Dim bitrate As String = ""
            Dim formatName As String = ""
            Dim formatLongName As String = ""
            Dim metadataList As New List(Of String)()
            Dim streamList As New List(Of String)()

            For Each line As String In lines
                Dim cleanLine As String = line.Trim()

                If cleanLine.Length = 0 Then Continue For

                If cleanLine = "[FORMAT]" Then
                    inFormat = True
                    inStream = False
                    Continue For
                ElseIf cleanLine = "[/FORMAT]" Then
                    inFormat = False
                    Continue For
                ElseIf cleanLine = "[STREAM]" Then
                    inFormat = False
                    inStream = True
                    streamIndex += 1
                    currentStreamInfo = "Stream #" & (streamIndex - 1) & ": "
                    Continue For
                ElseIf cleanLine = "[/STREAM]" Then
                    inStream = False
                    If currentStreamInfo.Length > 0 Then
                        streamList.Add(currentStreamInfo)
                        currentStreamInfo = ""
                    End If
                    Continue For
                End If

                If inFormat Then
                    Dim eqIndex As Integer = cleanLine.IndexOf("=")
                    If eqIndex > 0 Then
                        Dim key As String = cleanLine.Substring(0, eqIndex)
                        Dim value As String = cleanLine.Substring(eqIndex + 1)

                        If key = "bit_rate" Then
                            bitrate = value
                        ElseIf key = "format_name" Then
                            formatName = value
                        ElseIf key = "format_long_name" Then
                            formatLongName = value
                        ElseIf key.StartsWith("TAG:") Then
                            Dim tagName As String = key.Substring(4)
                            metadataList.Add(tagName & " : " & value)
                        End If
                    End If
                End If

                If inStream Then
                    Dim eqIndex As Integer = cleanLine.IndexOf("=")
                    If eqIndex > 0 Then
                        Dim key As String = cleanLine.Substring(0, eqIndex)
                        Dim value As String = cleanLine.Substring(eqIndex + 1)

                        Select Case key
                            Case "codec_type"
                                currentStreamInfo &= value
                            Case "codec_name"
                                currentStreamInfo &= " (" & value & ")"
                            Case "width"
                                currentStreamInfo &= ", " & value & "x"
                            Case "height"
                                currentStreamInfo &= value
                            Case "r_frame_rate"
                                If value.Contains("/") Then
                                    Dim parts As String() = value.Split("/"c)
                                    If parts.Length = 2 Then
                                        Dim num As Double, den As Double
                                        If Double.TryParse(parts(0), num) AndAlso Double.TryParse(parts(1), den) AndAlso den > 0 Then
                                            currentStreamInfo &= ", " & (num / den).ToString("F2") & " fps"
                                        End If
                                    End If
                                End If
                            Case "channels"
                                currentStreamInfo &= ", " & value & " ch"
                            Case "sample_rate"
                                currentStreamInfo &= ", " & value & " Hz"
                            Case "bit_rate"
                                Dim rate As Integer
                                If Integer.TryParse(value, rate) Then
                                    currentStreamInfo &= ", " & FormatBitrate(rate)
                                End If
                        End Select
                    End If
                End If
            Next

            If formatLongName.Length > 0 Then
                sb.AppendLine("Format         : " & formatLongName)
            End If
            If formatName.Length > 0 Then
                sb.AppendLine("Container      : " & formatName)
            End If
            If bitrate.Length > 0 Then
                Dim rate As Integer
                If Integer.TryParse(bitrate, rate) Then
                    sb.AppendLine("Overall Bitrate: " & FormatBitrate(rate))
                End If
            End If
            If videoDuration > 0 Then
                sb.AppendLine("Duration       : " & FormatTime(videoDuration))
            End If
            If streamList.Count > 0 Then
                sb.AppendLine()
                sb.AppendLine("Streams:")
                For Each info As String In streamList
                    sb.AppendLine("  " & info)
                Next
            End If

            If metadataList.Count > 0 Then
                sb.AppendLine()
                sb.AppendLine("Metadata Tags:")
                For Each tag As String In metadataList
                    sb.AppendLine("  " & tag)
                Next
            End If

            Return sb.ToString()

        Catch ex As Exception
            Return "Error parsing metadata: " & ex.Message
        End Try
    End Function

    Private Function FormatBitrate(ByVal bitrate As Integer) As String
        If bitrate >= 1000000 Then
            Return (bitrate / 1000000).ToString("F1") & " Mb/s"
        ElseIf bitrate >= 1000 Then
            Return (bitrate / 1000).ToString("F0") & " kb/s"
        Else
            Return bitrate.ToString() & " b/s"
        End If
    End Function

    Private Sub LicenseToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles LicenseToolStripMenuItem.Click
        ' Opens the project's license page in the default browser.
        Try
            Process.Start("https://pongo.my.id/license.htm")
        Catch ex As Exception
            MessageBox.Show(ex.Message, _
                            "Pongo Video Cutter - Unable to Open Link", _
                            MessageBoxButtons.OK, _
                            MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub AboutPongoToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles AboutPongoToolStripMenuItem.Click
        frmAbout.ShowDialog()
    End Sub

    Private Sub TutorialsToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles TutorialsToolStripMenuItem.Click
        ' Opens the project's tutorials page in the default browser.
        Try
            Process.Start("https://pongo.my.id/tutorials.htm")
        Catch ex As Exception
            MessageBox.Show(ex.Message, _
                            "Pongo Video Cutter - Unable to Open Link", _
                            MessageBoxButtons.OK, _
                            MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub AddToRecentFiles(ByVal filePath As String)
        Try
            Dim recent As String = My.Settings.RecentFiles
            Dim list As New List(Of String)()

            If Not String.IsNullOrEmpty(recent) Then
                list.AddRange(recent.Split(New String() {"|"}, StringSplitOptions.RemoveEmptyEntries))
            End If

            list.Remove(filePath)
            list.Insert(0, filePath)

            While list.Count > RECENT_FILES_MAX
                list.RemoveAt(list.Count - 1)
            End While

            My.Settings.RecentFiles = String.Join("|", list.ToArray())
            My.Settings.Save()

            BuildRecentFilesMenu()
        Catch ex As Exception
        End Try
    End Sub

    Private Sub BuildRecentFilesMenu()
        If RecentFilesToolStripMenuItem Is Nothing Then Return

        RecentFilesToolStripMenuItem.DropDownItems.Clear()

        Dim recent As String = My.Settings.RecentFiles
        If String.IsNullOrEmpty(recent) Then
            Dim emptyItem As New ToolStripMenuItem("(No recent files)")
            emptyItem.Enabled = False
            RecentFilesToolStripMenuItem.DropDownItems.Add(emptyItem)
            Return
        End If

        Dim list As String() = recent.Split(New String() {"|"}, StringSplitOptions.RemoveEmptyEntries)

        If list.Length = 0 Then
            Dim emptyItem As New ToolStripMenuItem("(No recent files)")
            emptyItem.Enabled = False
            RecentFilesToolStripMenuItem.DropDownItems.Add(emptyItem)
            Return
        End If

        For Each f As String In list
            If String.IsNullOrEmpty(f) Then Continue For
            If Not System.IO.File.Exists(f) Then Continue For

            Dim item As New ToolStripMenuItem(Path.GetFileName(f))
            item.ToolTipText = f
            Dim filePathCopy As String = f
            AddHandler item.Click, Sub()
                                      If isProcessingFile Then Return
                                      If Not System.IO.File.Exists(filePathCopy) Then
                                          MessageBox.Show("The file no longer exists.", "Pongo Video Cutter Pro", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                          Return
                                      End If
                                      LoadDroppedVideo(filePathCopy)
                                  End Sub
            RecentFilesToolStripMenuItem.DropDownItems.Add(item)
        Next

        If RecentFilesToolStripMenuItem.DropDownItems.Count > 0 Then
            RecentFilesToolStripMenuItem.DropDownItems.Add(New ToolStripSeparator())
            Dim clearItem As New ToolStripMenuItem("Clear Recent Files")
            AddHandler clearItem.Click, Sub()
                                           My.Settings.RecentFiles = ""
                                           My.Settings.Save()
                                           BuildRecentFilesMenu()
                                           lblStatus.Text = "Status: Recent files cleared"
                                       End Sub
            RecentFilesToolStripMenuItem.DropDownItems.Add(clearItem)
        Else
            Dim emptyItem As New ToolStripMenuItem("(No recent files)")
            emptyItem.Enabled = False
            RecentFilesToolStripMenuItem.DropDownItems.Add(emptyItem)
        End If
    End Sub

    Private Sub AddToBatchQueueToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles AddToBatchQueueToolStripMenuItem.Click
        If String.IsNullOrEmpty(inputFile) Then
            MessageBox.Show("Please open a video file first.", "Pongo Video Cutter Pro - Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        If lstSegments.Items.Count = 0 Then
            MessageBox.Show("Please add at least one segment before adding to the batch queue.", "Pongo Video Cutter Pro - Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        batchQueue.Add(inputFile)
        UpdateBatchQueueUI()
        lblStatus.Text = "Status: Added to batch queue. Total: " & batchQueue.Count & " file(s)"
    End Sub

    Private Sub ViewBatchQueueToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ViewBatchQueueToolStripMenuItem.Click
        Dim sb As New System.Text.StringBuilder()
        sb.AppendLine("Batch Queue (" & batchQueue.Count & " file(s)):")
        sb.AppendLine()
        For i As Integer = 0 To batchQueue.Count - 1
            sb.AppendLine((i + 1).ToString() & ". " & Path.GetFileName(batchQueue(i)))
            sb.AppendLine("   " & batchQueue(i))
        Next

        If batchQueue.Count = 0 Then
            sb.AppendLine("(empty)")
            sb.AppendLine()
            sb.AppendLine("To add files: open a video, mark segments, then use File > Add to Batch Queue.")
        Else
            sb.AppendLine()
            sb.AppendLine("Click OK to process all queued files using the current segment list.")
        End If

        Dim result As DialogResult = MessageBox.Show(sb.ToString(), "Pongo Video Cutter Pro - Batch Queue", _
            If(batchQueue.Count > 0, MessageBoxButtons.OKCancel, MessageBoxButtons.OK), MessageBoxIcon.Information)

        If result = DialogResult.OK AndAlso batchQueue.Count > 0 Then
            ProcessBatchQueue()
        End If
    End Sub

    Private Sub ClearBatchQueueToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ClearBatchQueueToolStripMenuItem.Click
        batchQueue.Clear()
        UpdateBatchQueueUI()
        lblStatus.Text = "Status: Batch queue cleared"
    End Sub

    Private Sub UpdateBatchQueueUI()
        If BatchQueueStatusLabel IsNot Nothing Then
            BatchQueueStatusLabel.Text = "Batch: " & batchQueue.Count & " file(s)"
        End If
    End Sub



    Private Sub trkVolume_Scroll(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles trkVolume.Scroll
        If videoPlayer IsNot Nothing Then
            videoPlayer.SetVolumePercent(trkVolume.Value)
        End If
        If lblVolumePercent IsNot Nothing Then
            lblVolumePercent.Text = trkVolume.Value.ToString() & "%"
        End If
    End Sub

    Private Sub ProcessBatchQueue()
        If batchQueue.Count = 0 Then Return
        If lstSegments.Items.Count = 0 Then
            MessageBox.Show("No segments defined. Add segments before processing the batch queue.", "Pongo Video Cutter Pro", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim savedInputFile As String = inputFile
        Dim savedOutputFile As String = outputFile

        Try
            SetControlsEnabled(False)

            For i As Integer = 0 To batchQueue.Count - 1
                lblStatus.Text = String.Format("Status: Batch processing {0}/{1}...", i + 1, batchQueue.Count)
                Application.DoEvents()

                inputFile = batchQueue(i)
                Dim ext As String = Path.GetExtension(inputFile)
                Dim dir As String = Path.GetDirectoryName(inputFile)
                Dim baseName As String = Path.GetFileNameWithoutExtension(inputFile)
                outputFile = Path.Combine(dir, baseName & "_cut" & ext)

                ProgressBar1.Minimum = 0
                ProgressBar1.Maximum = lstSegments.Items.Count
                ProgressBar1.Value = 0

                For j As Integer = 0 To lstSegments.Items.Count - 1
                    Dim seg As VideoSegment = DirectCast(lstSegments.Items(j), VideoSegment)

                    Dim startTimeStr As String = FormatTimeForFFmpeg(seg.StartTime)
                    Dim duration As Double = seg.EndTime - seg.StartTime
                    Dim durationStr As String = FormatTimeForFFmpeg(duration)

                    Dim segFile As String
                    If chkMergeOutput.Checked Then
                        segFile = Path.Combine(dir, String.Format("{0}_seg{1:00}_temp{2}", baseName, j + 1, ext))
                    Else
                        segFile = Path.Combine(dir, String.Format("{0}_seg{1:00}{2}", baseName, j + 1, ext))
                    End If

                    Dim args As String = String.Format("-ss {0} -i ""{1}"" -t {2} -c copy -avoid_negative_ts make_zero -y ""{3}""", startTimeStr, inputFile, durationStr, segFile)

                    If Not ExecuteFFmpegSilent(args) Then
                        Dim fallbackArgs As String = String.Format("-ss {0} -i ""{1}"" -t {2} -c:v libx264 -c:a aac -avoid_negative_ts make_zero -y ""{3}""", startTimeStr, inputFile, durationStr, segFile)
                        ExecuteFFmpegSilent(fallbackArgs)
                    End If

                    ProgressBar1.Value = j + 1
                    Application.DoEvents()
                Next

                If chkMergeOutput.Checked AndAlso lstSegments.Items.Count > 1 Then
                    Dim listFile As String = Path.Combine(dir, "filelist_" & baseName & ".txt")
                    Dim sb As New System.Text.StringBuilder()
                    For j As Integer = 0 To lstSegments.Items.Count - 1
                        Dim segFile As String = Path.Combine(dir, String.Format("{0}_seg{1:00}_temp{2}", baseName, j + 1, ext))
                        Dim safeFile As String = segFile.Replace("'", "'\''")
                        sb.AppendLine("file '" & safeFile & "'")
                    Next
                    System.IO.File.WriteAllText(listFile, sb.ToString())

                    Dim mergeArgs As String = String.Format("-f concat -safe 0 -i ""{0}"" -c copy -y ""{1}""", listFile, outputFile)
                    ExecuteFFmpegSilent(mergeArgs)

                    If System.IO.File.Exists(listFile) Then System.IO.File.Delete(listFile)
                    For j As Integer = 0 To lstSegments.Items.Count - 1
                        Dim segFile As String = Path.Combine(dir, String.Format("{0}_seg{1:00}_temp{2}", baseName, j + 1, ext))
                        If System.IO.File.Exists(segFile) Then System.IO.File.Delete(segFile)
                    Next
                End If

                ProgressBar1.Value = ProgressBar1.Maximum
                Application.DoEvents()
            Next

            lblStatus.Text = "Status: Batch processing COMPLETE!"
            MessageBox.Show("Batch processing complete!" & vbNewLine & vbNewLine & _
                          "Processed " & batchQueue.Count & " file(s).", _
                          "Pongo Video Cutter Pro - Success", MessageBoxButtons.OK, MessageBoxIcon.Information)

        Catch ex As Exception
            lblStatus.Text = "Status: Batch ERROR!"
            Dim batchErr As String = "Batch processing error: " & ex.Message & vbNewLine & vbNewLine
            Dim ffmpegErr As String = GetFFmpegErrorSummary()
            If Not String.IsNullOrEmpty(ffmpegErr) Then
                batchErr &= "FFmpeg Output (last 15 lines):" & vbNewLine & ffmpegErr
            End If
            MessageBox.Show(batchErr, "Pongo Video Cutter Pro - Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            inputFile = savedInputFile
            outputFile = savedOutputFile
            ProgressBar1.Value = 0
            SetControlsEnabled(True)
        End Try
    End Sub
End Class
