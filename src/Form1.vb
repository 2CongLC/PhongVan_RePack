Imports System.IO
Imports System.Net.Http.Headers
Imports System.Text

Public Class Form1
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load

    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        ' OpenFileDialog1.Filter = "fyString|fyString.xml|uiXml|uiXml.xml"
        If OpenFileDialog1.ShowDialog = DialogResult.OK AndAlso SaveFileDialog1.ShowDialog = DialogResult.OK Then
            encryptUiXml(OpenFileDialog1.FileName, SaveFileDialog1.FileName)
            MessageBox.Show("Đã xong !")
        End If
    End Sub
    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        ' OpenFileDialog1.Filter = "fyString|fyString.res|uiXml|uiXml.res"
        If OpenFileDialog1.ShowDialog = DialogResult.OK AndAlso SaveFileDialog1.ShowDialog = DialogResult.OK Then
            decryptUixml(OpenFileDialog1.FileName, SaveFileDialog1.FileName)
            MessageBox.Show("Đã xong !")
        End If
    End Sub
    Private Function getNumBytesUTF8(value As String) As Integer
        Dim param1 As ByteArray = New ByteArray()
        param1.WriteUTFBytes(value)
        Return param1.Length
    End Function
    Public Sub encryptUiXml(inFile As String, outFile As String)
        Dim param1 As ByteArray = New ByteArray(File.ReadAllBytes(inFile))
        Dim utfbytes As String = param1.ReadUTFBytes(param1.BytesAvailable)
        Dim encoder As String = New FileInfo(inFile).Name.Split(".")(0) + "|||" + utfbytes
        Dim param2 As ByteArray = New ByteArray()
        param2.Endian = Endian.LITTLE_ENDIAN
        Dim count As Integer = getNumBytesUTF8(encoder)
        param2.WriteInt(count)
        param2.WriteUTFBytes(encoder)
        param2.Compress()
        'Dim param3 As ByteArray = New ByteArray(param2.ToArray)
        File.WriteAllBytes(outFile, param2.ToArray())
    End Sub
    Public Sub decryptUixml(inFile As String, outFile As String)
        Dim param1 As ByteArray = New ByteArray(File.ReadAllBytes(inFile))
        param1.Uncompress()
        Dim param2 As ByteArray = New ByteArray(param1.ToArray())
        param2.Endian = Endian.LITTLE_ENDIAN
        Dim sb As StringBuilder = New StringBuilder()
        While param2.BytesAvailable > 0
            Dim count As Integer = param2.ReadInt
            Dim utfbytes As String = param2.ReadUTFBytes(count)
            Dim col As String() = utfbytes.Split(New String() {"|||"}, StringSplitOptions.RemoveEmptyEntries)
            sb.Append(col(1))
        End While
        File.WriteAllText(outFile, sb.ToString())
    End Sub

    Private Sub LinkLabel1_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs) Handles LinkLabel1.LinkClicked
        Process.Start("https://2conglc-vn.blogspot.com/")
    End Sub
End Class
