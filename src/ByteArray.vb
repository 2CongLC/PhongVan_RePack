Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text
Imports System.IO
Imports System.IO.Compression
Imports System.Reflection
Imports System.Xml
Imports System.Collections
Imports System.Xml.Serialization
Imports System.Collections.Concurrent
Imports System.Security.Cryptography
Imports SevenZip
Imports Zlib

Public Enum CompressionAlgorithm
    Deflate
    Gzip
    Zlib
    LZMA
End Enum
Public Enum Endian
    BIG_ENDIAN = 0
    LITTLE_ENDIAN = 1
End Enum

Public Class ByteArray
    Implements IDisposable

#Region "Khai báo áp dụng trên lớp"
    Private inStream As MemoryStream
    Private br As BinaryReader
    Private bw As BinaryWriter
    Private _endian As Endian
#End Region

#Region "Tải mới thông số trên lớp"
    Public Sub New()
        inStream = New MemoryStream()
        Init()
    End Sub
    Public Sub New(ByVal _ms As MemoryStream)
        inStream = _ms
        Init()
    End Sub
    Public Sub New(ByVal buffer As Byte(), ByVal Optional position As Integer = 0, ByVal Optional length As Integer = -1)
        If length = -1 Then
            length = buffer.Length
        End If
        inStream = New MemoryStream()
        inStream.Write(buffer, position, length)
        inStream.Position = 0
        Init()
    End Sub
    Private Sub Init()
        br = New BinaryReader(inStream)
        bw = New BinaryWriter(inStream)
        _objectReferences = New List(Of Object)(15)
        _stringReferences = New List(Of String)()
        _classDefinitions = New List(Of ClassDefinition)(2)
        _endian = Endian.LITTLE_ENDIAN

        If _typeExchange Is Nothing Then
            _typeExchange = New ConcurrentDictionary(Of Type, AMF3SerializationDefine)()
            _typeExchange.TryAdd(GetType(Undefined), AMF3SerializationDefine.Undefined)
            _typeExchange.TryAdd(GetType(Nullable), AMF3SerializationDefine.Null)
            _typeExchange.TryAdd(GetType(Boolean), AMF3SerializationDefine.BooleanFalse)
            _typeExchange.TryAdd(GetType(Byte), AMF3SerializationDefine.Int)
            _typeExchange.TryAdd(GetType(SByte), AMF3SerializationDefine.Int)
            _typeExchange.TryAdd(GetType(Short), AMF3SerializationDefine.Int)
            _typeExchange.TryAdd(GetType(UShort), AMF3SerializationDefine.Int)
            _typeExchange.TryAdd(GetType(Integer), AMF3SerializationDefine.Int)
            _typeExchange.TryAdd(GetType(Double), AMF3SerializationDefine.Number)
            _typeExchange.TryAdd(GetType(Single), AMF3SerializationDefine.Number)
            _typeExchange.TryAdd(GetType(String), AMF3SerializationDefine.String)
            _typeExchange.TryAdd(GetType(XmlDocument), AMF3SerializationDefine.XML)
            _typeExchange.TryAdd(GetType(Date), AMF3SerializationDefine.Date)
            _typeExchange.TryAdd(GetType(ArrayList), AMF3SerializationDefine.Array)
            _typeExchange.TryAdd(GetType(IASObjectDefinition), AMF3SerializationDefine.Object)
            '_typeExchange.TryAdd(typeof(XmlNode), AMF3SerializationDefine.XML);
            _typeExchange.TryAdd(GetType(ByteArray), AMF3SerializationDefine.ByteArray)
            _typeExchange.TryAdd(GetType(List(Of Integer)), AMF3SerializationDefine.VectorInt)
            _typeExchange.TryAdd(GetType(List(Of UInteger)), AMF3SerializationDefine.VectorUint)
            _typeExchange.TryAdd(GetType(List(Of Double)), AMF3SerializationDefine.VectorNumber)
            _typeExchange.TryAdd(GetType(List(Of Single)), AMF3SerializationDefine.VectorNumber)
            _typeExchange.TryAdd(GetType(List(Of Object)), AMF3SerializationDefine.VectorObject)
            _typeExchange.TryAdd(GetType(Hashtable), AMF3SerializationDefine.Dictionary)
        End If
    End Sub
#End Region

#Region "Nén và giải nén dữ liệu theo chuẩn RFC và LZMA"
    ''' <summary>
    ''' Nén dữ liệu
    ''' </summary>
    ''' <param name="algorithm"></param>
    Public Sub Compress(ByVal Optional algorithm As CompressionAlgorithm = CompressionAlgorithm.Zlib)
        Select Case algorithm
            Case CompressionAlgorithm.Deflate
                Using _inStream As MemoryStream = New MemoryStream(inStream.ToArray())
                    Using outStream As MemoryStream = New MemoryStream()
                        Using DeflateStream As IO.Compression.DeflateStream = New IO.Compression.DeflateStream(outStream, IO.Compression.CompressionMode.Compress, True)
                            _inStream.CopyTo(DeflateStream)
                        End Using
                        inStream = outStream
                    End Using
                End Using
                Exit Select
            Case CompressionAlgorithm.Gzip
                Using _inStream As MemoryStream = New MemoryStream(inStream.ToArray())
                    Using outStream As MemoryStream = New MemoryStream()
                        Using GzipStream As IO.Compression.GZipStream = New IO.Compression.GZipStream(outStream, IO.Compression.CompressionMode.Compress, True)
                            _inStream.CopyTo(GzipStream)
                        End Using
                        inStream = outStream
                    End Using
                End Using
                Exit Select
            Case CompressionAlgorithm.Zlib
                Using _inStream As MemoryStream = New MemoryStream(inStream.ToArray())
                    Using outStream As MemoryStream = New MemoryStream()
                        Using zlibStream As ZlibStream = New ZlibStream(outStream, Zlib.CompressionMode.Compress, True)
                            _inStream.CopyTo(zlibStream)
                        End Using
                        inStream = outStream
                    End Using
                End Using
                Exit Select
            Case CompressionAlgorithm.LZMA
                Using _inStream As MemoryStream = New MemoryStream(inStream.ToArray())
                    Using outStream As MemoryStream = New MemoryStream()
                        Dim propIDs As SevenZip.CoderPropID() = {SevenZip.CoderPropID.DictionarySize, SevenZip.CoderPropID.PosStateBits, SevenZip.CoderPropID.LitContextBits, SevenZip.CoderPropID.LitPosBits, SevenZip.CoderPropID.Algorithm, SevenZip.CoderPropID.NumFastBytes, SevenZip.CoderPropID.MatchFinder, SevenZip.CoderPropID.EndMarker}
                        Dim properties = {1 << 23, 2, 3, 0, 1, 128, "bt4", False}
                        Dim encoder As SevenZip.Compression.LZMA.Encoder = New SevenZip.Compression.LZMA.Encoder()
                        encoder.SetCoderProperties(propIDs, properties)
                        encoder.WriteCoderProperties(outStream)
                        Dim fileSize As Long = _inStream.Length
                        For i As Integer = 0 To 8 - 1
                            outStream.WriteByte(fileSize >> 8 * i)
                        Next
                        outStream.Flush()
                        encoder.Code(_inStream, outStream, -1, -1, Nothing)
                        outStream.Flush()
                        inStream = outStream
                    End Using
                End Using
                Exit Select
        End Select
    End Sub
    ''' <summary>
    ''' Giải nén giữ liệu
    ''' </summary>
    ''' <param name="algorithm"></param>
    Public Sub Uncompress(ByVal Optional algorithm As CompressionAlgorithm = CompressionAlgorithm.Zlib)
        Select Case algorithm
            Case CompressionAlgorithm.Deflate
                Position = 0
                Using _inStream As MemoryStream = New MemoryStream(inStream.ToArray())
                    Using outStream As MemoryStream = New MemoryStream()
                        Using DeflateStream As IO.Compression.DeflateStream = New IO.Compression.DeflateStream(_inStream, IO.Compression.CompressionMode.Decompress, False)
                            DeflateStream.CopyTo(outStream)
                        End Using
                        inStream = outStream
                        inStream.Position = 0
                    End Using
                End Using
                Exit Select
            Case CompressionAlgorithm.Gzip
                Position = 0
                Using _inStream As MemoryStream = New MemoryStream(inStream.ToArray())
                    Using outStream As MemoryStream = New MemoryStream()
                        Using GzipStream As IO.Compression.GZipStream = New IO.Compression.GZipStream(_inStream, IO.Compression.CompressionMode.Decompress, False)
                            GzipStream.CopyTo(outStream)
                        End Using
                        inStream = outStream
                        inStream.Position = 0
                    End Using
                End Using
                Exit Select
            Case CompressionAlgorithm.Zlib
                Position = 0
                Using _inStream As MemoryStream = New MemoryStream(inStream.ToArray())
                    Using outStream As MemoryStream = New MemoryStream()
                        Using ZlibStream As ZlibStream = New ZlibStream(_inStream, Zlib.CompressionMode.Decompress, False)
                            ZlibStream.CopyTo(outStream)
                        End Using
                        inStream = outStream
                        inStream.Position = 0
                    End Using
                End Using
                Exit Select
            Case CompressionAlgorithm.LZMA
                Position = 0
                Using _inStream As MemoryStream = New MemoryStream(inStream.ToArray())
                    Using outStream As MemoryStream = New MemoryStream()
                        Dim properties = New Byte(4) {}
                        If _inStream.Read(properties, 0, 5) <> 5 Then Throw (New Exception("input .lzma is too short"))
                        Dim decoder As SevenZip.Compression.LZMA.Decoder = New SevenZip.Compression.LZMA.Decoder()
                        decoder.SetDecoderProperties(properties)
                        Dim outSize As Long = 0
                        For i = 0 To 8 - 1
                            Dim v As Integer = _inStream.ReadByte()
                            If v < 0 Then Throw (New Exception("Can't Read 1"))
                            outSize = outSize Or CLng(v) << 8 * i
                        Next
                        Dim compressedSize = _inStream.Length - _inStream.Position
                        decoder.Code(_inStream, outStream, compressedSize, outSize, Nothing)
                        inStream = outStream
                        inStream.Position = 0
                    End Using
                End Using
                Exit Select
        End Select
    End Sub
#End Region

#Region "Xuất luồng dữ liệu đầu ra"
    Public Property Endian As Endian
        Set(value As Endian)
            _endian = value
        End Set
        Get
            Return _endian
        End Get
    End Property
    Public ReadOnly Property Length As UInteger
        Get
            Return inStream.Length
        End Get
    End Property
    Public Property Position As UInteger
        Get
            Return inStream.Position
        End Get
        Set(value As UInteger)
            inStream.Position = value
        End Set
    End Property
    Public ReadOnly Property BytesAvailable As UInteger
        Get
            Return Length - Position
        End Get
    End Property
    Public Function GetBuffer() As Byte()
        Return inStream.GetBuffer()
    End Function
    Public Function ToArray() As Byte()
        Return inStream.ToArray()
    End Function
    Public Overrides Function ToString() As String
        Return inStream.ToString()
    End Function
#End Region

#Region "Đọc dữ liệu"

    Private Function ReadLittleEndian(length As Integer) As Byte()
        Return br.ReadBytes(length)
    End Function
    Private Function ReadBigEndian(length As Integer) As Byte()
        Dim little As Byte() = ReadLittleEndian(length)
        Dim reverse As Byte() = New Byte(length - 1) {}
        Dim i As Integer = length - 1, j As Integer = 0
        While i >= 0
            reverse(j) = little(i)
            i -= 1
            j += 1
        End While
        Return reverse
    End Function
    Public Function ReadBytesEndian(length As Integer) As Byte()
        If _endian = Endian.LITTLE_ENDIAN Then
            Return ReadLittleEndian(length)
        Else
            Return ReadBigEndian(length)
        End If
    End Function

    ''' <summary>
    ''' Đọc một Byte đã ký từ luồng byte.
    ''' </summary>
    ''' <returns></returns>
    Public Function ReadByte() As SByte
        Dim buffer As SByte = CSByte(inStream.ReadByte)
        Return buffer
    End Function
    ''' <summary>
    ''' Đọc số byte dữ liệu, được chỉ định bởi tham số độ dài, từ luồng byte.
    ''' </summary>
    ''' <param name="bytes"></param>
    ''' <param name="offset"></param>
    ''' <param name="length"></param>
    Public Sub ReadBytes(bytes As ByteArray, offset As UInteger, length As UInteger)
        Dim content As Byte() = New Byte(length - 1) {}
        inStream.Read(content, offset, length)
        bytes.WriteBytes(New ByteArray(content), 0, content.Length)
    End Sub
    ''' <summary>
    ''' Đọc giá trị Boolean từ luồng byte.
    ''' </summary>
    ''' <returns></returns>
    Public Function ReadBoolean() As Boolean
        Return inStream.ReadByte = 1
    End Function
    ''' <summary>
    ''' Đọc số dấu phẩy động chính xác kép (64 bit) của IEEE 754 từ luồng byte.
    ''' </summary>
    ''' <returns></returns>
    Public Function ReadDouble() As Double
        Dim bytes As Byte() = ReadBytesEndian(8)
        Return BitConverter.ToDouble(bytes, 0)
    End Function
    ''' <summary>
    ''' Đọc số dấu phẩy động có độ chính xác đơn (32 bit) của IEEE 754 từ luồng byte.
    ''' </summary>
    ''' <returns></returns>
    Public Function ReadFloat() As Single
        Dim bytes As Byte() = ReadBytesEndian(4)
        Return BitConverter.ToSingle(bytes, 0)
    End Function
    ''' <summary>
    ''' Đọc một số nguyên 32 Bit từ luồng byte.
    ''' </summary>
    ''' <returns></returns>
    Public Function ReadInt() As Integer
        Dim bytes As Byte() = ReadBytesEndian(4)
        Dim value As Integer = bytes(3) << 24 Or CInt(bytes(2)) << 16 Or CInt(bytes(1)) << 8 Or bytes(0)
        Return value
    End Function
    ''' <summary>
    ''' Đọc một chuỗi đa chuỗi có độ dài được chỉ định từ luồng byte bằng cách sử dụng bộ ký tự được chỉ định.
    ''' </summary>
    ''' <param name="length"></param>
    ''' <param name="charset"></param>
    ''' <returns></returns>
    Public Function ReadMultiByte(length As UInteger, charset As String) As String
        Dim bytes As Byte() = ReadBytesEndian(CInt(length))
        Return Encoding.GetEncoding(charset).GetString(bytes)
    End Function

    ''' <summary>
    ''' Đọc một số nguyên 16 Bit từ luồng byte.
    ''' </summary>
    ''' <returns></returns>
    Public Function ReadShort() As Short
        Dim bytes As Byte() = ReadBytesEndian(2)
        Return bytes(1) << 8 Or bytes(0)
    End Function
    ''' <summary>
    ''' Đọc một Byte không dấu từ luồng byte.
    ''' </summary>
    ''' <returns></returns>
    Public Function ReadUnsignedByte() As Byte
        Return CByte(inStream.ReadByte)
    End Function
    ''' <summary>
    ''' Đọc một số nguyên 32 bit không dấu từ luồng byte.
    ''' </summary>
    ''' <returns></returns>
    Public Function ReadUnsignedInt() As UInteger
        Dim bytes As Byte() = ReadBytesEndian(4)
        Return BitConverter.ToUInt32(bytes, 0)
    End Function
    ''' <summary>
    ''' Đọc một số nguyên 16 bit không dấu từ luồng byte.
    ''' </summary>
    ''' <returns></returns>
    Public Function ReadUnsignedShort() As UShort
        Dim bytes As Byte() = ReadBytesEndian(2)
        Return BitConverter.ToUInt16(bytes, 0)
    End Function
    ''' <summary>
    ''' Đọc một chuỗi các byte UTF-8 được chỉ định bởi tham số độ dài từ luồng byte và trả về một chuỗi.
    ''' </summary>
    ''' <returns></returns>
    Public Function ReadUTFBytes(length As Integer) As String
        If length = 0 Then
            Return String.Empty
        End If
        Return New UTF8Encoding(False, True).GetString(br.ReadBytes(length))
        ' Return Encoding.GetEncoding("GB2312").GetString(br.ReadBytes(length))
        ' Return Encoding.GetEncoding("UTF-8", New EncoderReplacementFallback(String.Empty), New DecoderReplacementFallback(String.Empty)).GetString(br.ReadBytes(length))
    End Function
    ''' <summary>
    ''' Đọc một chuỗi UTF-8 từ luồng byte.
    ''' </summary>
    ''' <returns></returns>
    Public Function ReadUTF() As String
        Dim length As Integer = ReadShort()
        Return ReadUTFBytes(length)
    End Function

#End Region

#Region "Ghi dữ liệu"
    Private Sub WriteLittleEndian(bytes As Byte())
        If bytes Is Nothing Then
            Return
        End If
        inStream.Write(bytes, 0, bytes.Length)
    End Sub
    Private Sub WriteBigEndian(bytes As Byte())
        If bytes Is Nothing Then
            Return
        End If
        For i = bytes.Length - 1 To 0 Step -1
            inStream.WriteByte(bytes(i))
        Next
    End Sub
    Friend Sub WriteBytesEndian(bytes As Byte())
        If _endian = Endian.LITTLE_ENDIAN Then
            WriteLittleEndian(bytes)
        Else
            WriteBigEndian(bytes)
        End If
    End Sub
    ''' <summary>
    '''  Ghi một giá trị Boolean.
    ''' </summary>
    ''' <param name="value"></param>
    Public Sub WriteBoolean(value As Boolean)
        inStream.WriteByte(If(value, CByte(1), CByte(0)))
    End Sub
    ''' <summary>
    ''' Ghi một byte vào luồng byte.
    ''' </summary>
    ''' <param name="value"></param>
    Public Sub WriteByte(value As SByte)
        inStream.WriteByte(value)
    End Sub
    ''' <summary>
    ''' Ghi một chuỗi các byte có độ dài từ mảng byte được chỉ định, byte, bắt đầu bù (chỉ số dựa trên zero) vào luồng byte.
    ''' </summary>
    ''' <param name="bytes"></param>
    ''' <param name="offset"></param>
    ''' <param name="length"></param>
    Public Sub WriteBytes(bytes As ByteArray, Optional offset As UInteger = 0, Optional length As UInteger = 0)
        inStream.Write(bytes.ToArray(), offset, length)
    End Sub
    ''' <summary>
    ''' Ghi một số dấu phẩy động có độ chính xác kép (64 bit) của IEEE 754 vào luồng byte.
    ''' </summary>
    ''' <param name="value"></param>
    Public Sub WriteDouble(value As Double)
        Dim bytes As Byte() = BitConverter.GetBytes(value)
        WriteBytesEndian(bytes)
    End Sub
    ''' <summary>
    ''' Ghi một số dấu phẩy động có độ chính xác đơn (32 bit) của IEEE 754 vào luồng byte.
    ''' </summary>
    ''' <param name="value"></param>
    Public Sub WriteFloat(value As Single)
        Dim bytes As Byte() = BitConverter.GetBytes(value)
        WriteBytesEndian(bytes)
    End Sub
    ''' <summary>
    ''' Ghi một số nguyên có chữ ký 32 bit vào luồng byte.
    ''' </summary>
    ''' <param name="value"></param>
    Public Sub WriteInt(value As Integer)
        Dim bytes As Byte() = BitConverter.GetBytes(value)
        WriteBytesEndian(bytes)
    End Sub
    ''' <summary>
    ''' Ghi một chuỗi đa chuỗi vào luồng byte bằng cách sử dụng bộ ký tự được chỉ định.
    ''' </summary>
    ''' <param name="value"></param>
    ''' <param name="charset"></param>
    Public Sub WriteMultiByte(value As String, charset As String)
        Dim bytes As Byte() = Encoding.GetEncoding(charset).GetBytes(value)
        WriteBytesEndian(bytes)
    End Sub
    ''' <summary>
    ''' Ghi một số nguyên 16 bit vào luồng byte.
    ''' </summary>
    ''' <param name="value"></param>
    Public Sub WriteShort(value As Short)
        Dim bytes As Byte() = BitConverter.GetBytes(value)
        WriteBytesEndian(bytes)
    End Sub
    ''' <summary>
    ''' Ghi một chuỗi UTF-8 vào luồng byte.
    ''' </summary>
    ''' <param name="value"></param>
    Public Sub WriteUTF(value As String)
        Dim utf8 As UTF8Encoding = New UTF8Encoding()
        Dim count As Integer = utf8.GetByteCount(value)
        Dim buffer As Byte() = utf8.GetBytes(value)
        WriteShort(count)
        If buffer.Length > 0 Then
            bw.Write(buffer)
        End If
    End Sub
    ''' <summary>
    ''' Ghi một chuỗi UTF-8 vào luồng byte.
    ''' </summary>
    ''' <param name="value"></param>
    Public Sub WriteUTFBytes(value As String)
        Dim utf8 As UTF8Encoding = New UTF8Encoding()
        Dim buffer As Byte() = utf8.GetBytes(value)
        If buffer.Length > 0 Then
            bw.Write(buffer)
        End If
    End Sub
    ''' <summary>
    ''' Ghi một byte không dấu vào luồng byte.
    ''' </summary>
    ''' <param name="value"></param>
    Public Sub WriteUnsignedByte(value As Byte)
        inStream.WriteByte(value)
    End Sub
    ''' <summary>
    ''' Ghi một số nguyên không dấu 32 bit vào luồng byte.
    ''' </summary>
    ''' <param name="value"></param>
    Public Sub WriteUnsignedInt(value As UInteger)
        Dim bytes As Byte() = New Byte(3) {}
        bytes(3) = CByte(&HFF And value >> 24)
        bytes(2) = CByte(&HFF And value >> 16)
        bytes(1) = CByte(&HFF And value >> 8)
        bytes(0) = CByte(&HFF And value >> 0)
        WriteBytesEndian(bytes)
    End Sub
    ''' <summary>
    ''' Ghi một số nguyên 16 bit không dấu vào luồng byte.
    ''' </summary>
    ''' <param name="value"></param>
    Public Sub WriteUnsignedShort(value As UShort)
        Dim bytes As Byte() = BitConverter.GetBytes(value)
        WriteBigEndian(bytes)
    End Sub
#End Region

#Region "Lấy mã MD5Hash"
    ''' <summary>
    ''' Lấy mã MD5Hash
    ''' </summary>
    ''' <returns></returns>
    Public Function ComputeMD5() As String
        Dim md5 As MD5 = New MD5CryptoServiceProvider()
        Dim retVal As Byte() = md5.ComputeHash(GetBuffer())
        Dim sb As StringBuilder = New StringBuilder()
        For i = 0 To retVal.Length - 1
            sb.Append(retVal(i).ToString("x2"))
        Next
        Return sb.ToString()
    End Function
#End Region

#Region "Đọc dữ liệu AMF3"
    Public Enum TimezoneCompensation
        ''' <summary>
        ''' No timezone compensation.
        ''' </summary>
        <XmlEnum(Name:="none")>
        None = 0
        ''' <summary>
        ''' Auto timezone compensation.
        ''' </summary>
        <XmlEnum(Name:="auto")>
        Auto = 1
        ''' <summary>
        ''' Convert to the server timezone.
        ''' </summary>
        <XmlEnum(Name:="server")>
        Server = 2
        ''' <summary>
        ''' Ignore UTCKind for DateTimes received from the client code.
        ''' </summary>
        <XmlEnum(Name:="ignoreUTCKind")>
        IgnoreUTCKind = 3
    End Enum
    Private Enum AMF3SerializationDefine
        Undefined = 0
        Null = 1
        BooleanFalse = 2
        BooleanTrue = 3
        Int = 4
        Number = 5
        [String] = 6
        XMLDoc = 7
        [Date] = 8
        Array = 9
        [Object] = 10
        XML = 11
        ByteArray = 12
        VectorInt = 13
        VectorUint = 14
        VectorNumber = 15
        VectorObject = 16
        Dictionary = 17
    End Enum
    Private _objectReferences As List(Of Object)
    Private _stringReferences As List(Of String)
    Private _classDefinitions As List(Of ClassDefinition)
    Private Shared _typeExchange As ConcurrentDictionary(Of Type, AMF3SerializationDefine)
    Private Shared f_TimezoneCompensation = TimezoneCompensation.Server

    Private Function ReadAMF3IntegerData() As Integer
        Dim acc As Integer = br.ReadByte()
        Dim tmp As Integer
        If acc < 128 Then
            Return acc
        Else
            acc = (acc And &H7F) << 7
            tmp = br.ReadByte()

            If tmp < 128 Then
                acc = acc Or tmp
            Else
                acc = (acc Or tmp And &H7F) << 7
                tmp = br.ReadByte()

                If tmp < 128 Then
                    acc = acc Or tmp
                Else
                    acc = (acc Or tmp And &H7F) << 8
                    tmp = br.ReadByte()
                    acc = acc Or tmp
                End If
            End If
        End If
        Dim mask = 1 << 28 ' mask
        Dim r = -(acc And mask) Or acc
        Return r
    End Function
    ''' <summary>
    ''' Đọc dữ liệu AMF3 kiểu 32 bit từ luồng byte.
    ''' </summary>
    ''' <returns></returns>
    Public Function ReadU29Int() As Integer
        Return ReadAMF3IntegerData()
    End Function
    Friend Function ReadClassDefinition(ByVal handle As Integer) As ClassDefinition
        Dim classDefinition As ClassDefinition = Nothing
        'an inline object
        Dim inlineClassDef = (handle And 1) <> 0
        handle = handle >> 1

        If inlineClassDef Then
            'inline class-def
            Dim typeIdentifier As String = TryCast(ReadAMF3Data(AMF3SerializationDefine.String), String)
            'flags that identify the way the object is serialized/deserialized
            Dim externalizable = (handle And 1) <> 0
            handle = handle >> 1
            Dim dynamic = (handle And 1) <> 0
            handle = handle >> 1
            Dim members = New ClassMember(handle - 1) {}

            For i = 0 To handle - 1
                Dim name As String = TryCast(ReadAMF3Data(AMF3SerializationDefine.String), String)
                Dim classMember As ClassMember = New ClassMember(name, BindingFlags.Default, MemberTypes.Custom, Nothing)
                members(i) = classMember
            Next

            classDefinition = New ClassDefinition(typeIdentifier, members, externalizable, dynamic)
            AddClassReference(classDefinition)
        Else
            'A reference to a previously passed class-def
            classDefinition = ReadClassReference(handle)
        End If

        Return classDefinition
    End Function

    Private Function findObjectIndex(ByVal value As Object) As Integer
        Dim __index = _objectReferences.IndexOf(value)

        If __index <> -1 Then
            Return __index
        End If

        For i = 0 To _objectReferences.Count - 1

            If compareEqual(_objectReferences(i), value) Then
                Return i
            End If
        Next

        Return -1
    End Function

    Private Function compareEqual(ByVal a As Object, ByVal b As Object) As Boolean
        If a Is b Then
            Return True
        ElseIf a Is Nothing AndAlso b Is Nothing Then
            Return True
        ElseIf TypeOf a Is Undefined AndAlso TypeOf b Is Undefined Then
            Return True
        ElseIf (TypeOf a Is Single OrElse TypeOf a Is Double) AndAlso (TypeOf b Is Single OrElse TypeOf b Is Double) Then
            Return Math.Abs(CDbl(a) - CDbl(b)) <= 0.000001
        ElseIf (TypeOf a Is Byte OrElse TypeOf a Is SByte OrElse TypeOf a Is Short OrElse TypeOf a Is UShort OrElse TypeOf a Is Integer) AndAlso (TypeOf b Is Byte OrElse TypeOf b Is SByte OrElse TypeOf b Is Short OrElse TypeOf b Is UShort OrElse TypeOf b Is Integer) Then
            Return CInt(a) = CInt(b)
        Else
            Dim aType As Type = a.GetType()
            Dim bType As Type = b.GetType()

            If aType IsNot bType Then
                Return False
            End If

            If TypeOf a Is Date Then

                If CDate(a).CompareTo(CDate(b)) = 0 Then
                    Return True
                End If
            ElseIf TypeOf a Is ByteArray Then

                If TryCast(a, ByteArray).Length = TryCast(b, ByteArray).Length Then
                    Dim __a As Byte() = TryCast(a, ByteArray).ToArray()
                    Dim __b As Byte() = TryCast(b, ByteArray).ToArray()

                    For j = 0 To __a.Length - 1

                        If __a(j) <> __b(j) Then
                            Continue For
                        End If
                    Next

                    Return True
                End If
            ElseIf TypeOf a Is IList Then

                If TryCast(a, IList).Count = TryCast(b, IList).Count Then

                    For j = 0 To TryCast(b, IList).Count - 1

                        If Not compareEqual(TryCast(a, IList)(j), TryCast(b, IList)(j)) Then
                            Return False
                        End If
                    Next

                    Return True
                End If
            ElseIf TypeOf a Is Dictionary(Of String, Object) Then

                For Each key In TryCast(a, Dictionary(Of String, Object))

                    If TryCast(b, Dictionary(Of String, Object)).ContainsKey(key.Key) Then

                        If Not compareEqual(TryCast(a, Dictionary(Of String, Object))(key.Key), TryCast(b, Dictionary(Of String, Object))(key.Key)) Then
                            Return False
                        End If
                    Else
                        Return False
                    End If
                Next

                Return True
            ElseIf TypeOf a Is Hashtable Then

                For Each entry As DictionaryEntry In TryCast(a, Hashtable)

                    If TryCast(b, Hashtable).ContainsKey(entry.Key) Then

                        If Not compareEqual(TryCast(a, Hashtable)(entry.Key), TryCast(b, Hashtable)(entry.Key)) Then
                            Return False
                        End If
                    Else

                        For Each entryChildInB As DictionaryEntry In TryCast(b, Hashtable)

                            If compareEqual(entry.Key, entryChildInB.Key) AndAlso compareEqual(TryCast(a, Hashtable)(entry.Key), TryCast(b, Hashtable)(entryChildInB.Key)) Then
                                Return True
                            End If
                        Next

                        Return False
                    End If
                Next

                Return True
            ElseIf TypeOf a Is XmlDocument Then
                Dim aStream As MemoryStream = New MemoryStream()
                Dim aWriter As XmlTextWriter = New XmlTextWriter(aStream, Encoding.UTF8)
                aWriter.Formatting = Formatting.Indented
                TryCast(a, XmlDocument).Save(aWriter)
                Dim bStream As MemoryStream = New MemoryStream()
                Dim bWriter As XmlTextWriter = New XmlTextWriter(bStream, Encoding.UTF8)
                bWriter.Formatting = Formatting.Indented
                TryCast(b, XmlDocument).Save(bWriter)
                aWriter.Close()
                aWriter.Dispose()
                bWriter.Close()
                bWriter.Dispose()

                If aStream.Length <> bStream.Length Then
                    aStream.Close()
                    aStream.Dispose()
                    bStream.Close()
                    bStream.Dispose()
                    Return False
                End If

                Dim aBuffer As Byte() = aStream.ToArray()
                Dim bBuffer As Byte() = bStream.ToArray()
                aStream.Close()
                aStream.Dispose()
                bStream.Close()
                bStream.Dispose()

                For i = 0 To aBuffer.Length - 1

                    If aBuffer(i) <> bBuffer(i) Then
                        Return False
                    End If
                Next

                Return True
            Else

                If TypeOf a Is IASObjectDefinition Then
                    Dim aClassDefinintion As ClassDefinition = TryCast(a, IASObjectDefinition).classDefinition

                    For j = 0 To aClassDefinintion.MemberCount - 1
                        Dim aFieldMember As FieldInfo = a.GetType().GetField(aClassDefinintion.Members(j).Name)

                        If aFieldMember IsNot Nothing Then
                            Dim bFieldMember As FieldInfo = b.GetType().GetField(aClassDefinintion.Members(j).Name)
                            Dim aValue = aFieldMember.GetValue(a)
                            Dim bValue = bFieldMember.GetValue(b)

                            If Not compareEqual(aValue, bValue) Then
                                Return False
                            End If
                        Else
                            Dim aPropertyInfo As PropertyInfo = a.GetType().GetProperty(aClassDefinintion.Members(j).Name)

                            If aPropertyInfo IsNot Nothing Then
                                Dim bPropertyInfo As PropertyInfo = b.GetType().GetProperty(aClassDefinintion.Members(j).Name)
                                Dim aValue = aPropertyInfo.GetValue(a)
                                Dim bValue = bPropertyInfo.GetValue(b)

                                If Not compareEqual(aValue, bValue) Then
                                    Return False
                                End If
                            End If
                        End If
                    Next
                End If

                If TypeOf a Is IExternalizable Then
                    Dim aBytes As ByteArray = New ByteArray()
                    Dim bBytes As ByteArray = New ByteArray()
                    TryCast(a, IExternalizable).writeExternal(aBytes)
                    TryCast(b, IExternalizable).writeExternal(bBytes)

                    If Not compareEqual(aBytes, bBytes) Then
                        aBytes.Dispose()
                        bBytes.Dispose()
                        Return False
                    End If
                End If

                If TypeOf a Is IDynamic Then
                    Dim aDyn As Dictionary(Of String, Object) = TryCast(a, IDynamic).dynamicRoot
                    Dim bDyn As Dictionary(Of String, Object) = TryCast(b, IDynamic).dynamicRoot

                    If Not compareEqual(aDyn, bDyn) Then
                        Return False
                    End If
                End If

                If Not (TypeOf a Is IExternalizable OrElse TypeOf a Is IDynamic OrElse TypeOf a Is IASObjectDefinition) Then
                    Return False
                End If

                Return True
            End If
        End If

        Return False
    End Function
    Private Sub AddAMF3StringReference(ByVal instance As String)
        _stringReferences.Add(instance)
    End Sub

    Private Function ReadAMF3StringReference(ByVal index As Integer) As String
        Return _stringReferences(index)
    End Function

    Private Sub AddAMF3ObjectReference(ByVal instance As Object)
        _objectReferences.Add(instance)
    End Sub

    Private Function ReadAMF3ObjectReference(ByVal index As Integer) As Object
        Return _objectReferences(index)
    End Function

    Friend Sub AddClassReference(ByVal classDefinition As ClassDefinition)
        _classDefinitions.Add(classDefinition)
    End Sub

    Friend Function ReadClassReference(ByVal index As Integer) As ClassDefinition
        Return TryCast(_classDefinitions(index), ClassDefinition)
    End Function
    Private Function ReadAMF3Data() As Object
        Dim typeCode As Byte = br.ReadByte()
        Return ReadAMF3Data(CType(typeCode, AMF3SerializationDefine))
    End Function
    Private Function ReadAMF3Data(ByVal typeMarker As AMF3SerializationDefine) As Object
        Dim returnResult As Object

        Select Case typeMarker
            Case AMF3SerializationDefine.Undefined
                'null
                returnResult = New Undefined()
                Exit Select
            Case AMF3SerializationDefine.Null
                'null
                returnResult = Nothing
                Exit Select
            Case AMF3SerializationDefine.BooleanFalse
                'boolean
                returnResult = False
                Exit Select
            Case AMF3SerializationDefine.BooleanTrue
                'boolean
                returnResult = True
                Exit Select
            Case AMF3SerializationDefine.Int
                'int
                returnResult = ReadAMF3IntegerData()
                Exit Select
            Case AMF3SerializationDefine.Number
                'number
                returnResult = ReadDouble()
                Exit Select
            Case AMF3SerializationDefine.String
                'string
                Dim handle As Integer = ReadAMF3IntegerData()
                Dim inline = (handle And 1) <> 0
                handle = handle >> 1

                If inline Then
                    Dim length = handle
                    If length = 0 Then Return String.Empty
                    Dim str = ReadUTFBytes(length)
                    AddAMF3StringReference(str)
                    returnResult = str
                Else
                    Return ReadAMF3StringReference(handle)
                End If

                Exit Select
            Case AMF3SerializationDefine.XMLDoc
                'xml
                Dim handle As Integer = ReadAMF3IntegerData()
                Dim inline = (handle And 1) <> 0
                handle = handle >> 1
                Dim xml = String.Empty

                If inline Then
                    If handle > 0 Then xml = ReadUTFBytes(handle) 'length
                    Dim xmlDocument As XmlDocument = New XmlDocument()
                    If Not Equals(xml, Nothing) AndAlso Not Equals(xml, String.Empty) Then xmlDocument.LoadXml(xml)
                    AddAMF3ObjectReference(xmlDocument)
                    returnResult = xmlDocument
                Else
                    Return TryCast(ReadAMF3ObjectReference(handle), XmlDocument)
                End If

                Exit Select
            Case AMF3SerializationDefine.Date
                'date
                Dim handle As Integer = ReadAMF3IntegerData()
                Dim inline = (handle And 1) <> 0
                handle = handle >> 1

                If inline Then
                    'double milliseconds = this.readDouble();
                    Dim milliseconds As Double = ReadDouble()
                    Dim start As Date = New DateTime(1970, 1, 1, 0, 0, 0)
                    Dim [date] = start.AddMilliseconds(milliseconds)
                    [date] = Date.SpecifyKind([date], DateTimeKind.Utc)

                    Select Case f_TimezoneCompensation
                                                                'No conversion by default
                        Case TimezoneCompensation.None
                                                                'Not applicable for AMF3
                        Case TimezoneCompensation.Auto
                        Case TimezoneCompensation.Server
                            'Convert to local time
                            [date] = [date].ToLocalTime()
                    End Select

                    AddAMF3ObjectReference([date])
                    returnResult = [date]
                Else
                    Return CDate(ReadAMF3ObjectReference(handle))
                End If

                Exit Select
            Case AMF3SerializationDefine.Array
                'array
                Dim handle As Integer = ReadAMF3IntegerData()
                Dim inline = (handle And 1) <> 0
                handle = handle >> 1

                If inline Then
                    Dim hashtable As Dictionary(Of String, Object) = Nothing
                    Dim key As String = TryCast(ReadAMF3Data(AMF3SerializationDefine.String), String)

                    While Not Equals(key, Nothing) AndAlso Not Equals(key, String.Empty)

                        If hashtable Is Nothing Then
                            hashtable = New Dictionary(Of String, Object)()
                            AddAMF3ObjectReference(hashtable)
                        End If

                        Dim value As Object = ReadAMF3Data()
                        hashtable.Add(key, value)
                        key = TryCast(ReadAMF3Data(AMF3SerializationDefine.String), String)
                    End While

                    'Not an associative array
                    If hashtable Is Nothing Then
                        Dim array As ArrayList = New ArrayList(handle)
                        AddAMF3ObjectReference(array)

                        For i = 0 To handle - 1
                            'Grab the type for each element.
                            Dim typeCode As Byte = br.ReadByte()
                            Dim value = ReadAMF3Data(typeCode)
                            array.Add(value)
                        Next

                        returnResult = array
                    Else

                        For i = 0 To handle - 1
                            Dim value As Object = ReadAMF3Data()
                            hashtable.Add(i.ToString(), value)
                        Next

                        returnResult = hashtable
                    End If
                Else
                    Return ReadAMF3ObjectReference(handle)
                End If

                Exit Select
            Case AMF3SerializationDefine.Object
                'object
                Dim handle As Integer = ReadAMF3IntegerData()
                Dim inline = (handle And 1) <> 0
                handle = handle >> 1

                If inline Then
                    Dim classDefinition = ReadClassDefinition(handle)
                    returnResult = classDefinition.getClass()
                    AddAMF3ObjectReference(returnResult)

                    If classDefinition.IsExternalizable Then

                        If TypeOf returnResult Is IExternalizable Then
                            Dim externalizable As IExternalizable = TryCast(returnResult, IExternalizable)
                            externalizable.readExternal(Me)
                        Else
                            Throw New Exception("returnResult must be IExternalizable")
                        End If
                    Else

                        For i = 0 To classDefinition.MemberCount - 1
                            Dim key = classDefinition.Members(i).Name
                            Dim value As Object = ReadAMF3Data()

                            If TypeOf returnResult Is ASObject Then
                                TryCast(returnResult, ASObject).dynamicRoot(key) = value
                            Else
                                Dim type As Type = returnResult.GetType()
                                Dim propertyInfo = type.GetProperty(key)
                                Dim fieldInfo = type.GetField(key)

                                If propertyInfo IsNot Nothing Then
                                    propertyInfo.SetValue(returnResult, value)
                                ElseIf fieldInfo IsNot Nothing Then
                                    fieldInfo.SetValue(returnResult, value)
                                ElseIf TypeOf returnResult Is IDynamic Then
                                    TryCast(returnResult, IDynamic).dynamicRoot(key) = value
                                Else
                                    Throw New Exception("Type Property or Field Must Exsit Or Type Must Be IDynamic!")
                                End If
                            End If
                        Next

                        If classDefinition.IsDynamic Then
                            Dim key As String = TryCast(ReadAMF3Data(AMF3SerializationDefine.String), String)

                            While Not Equals(key, Nothing) AndAlso Not Equals(key, String.Empty)
                                Dim value As Object = ReadAMF3Data()

                                If TypeOf returnResult Is ASObject Then
                                    TryCast(returnResult, ASObject).dynamicRoot(key) = value
                                Else
                                    Dim type As Type = returnResult.GetType()
                                    Dim propertyInfo = type.GetProperty(key)
                                    Dim fieldInfo = type.GetField(key)

                                    If propertyInfo IsNot Nothing Then
                                        propertyInfo.SetValue(returnResult, value)
                                    ElseIf fieldInfo IsNot Nothing Then
                                        fieldInfo.SetValue(returnResult, value)
                                    ElseIf TypeOf returnResult Is IDynamic Then
                                        TryCast(returnResult, IDynamic).dynamicRoot(key) = value
                                    Else
                                        Throw New Exception("Type Property or Field Must Exsit Or Type Must Be IDynamic!")
                                    End If
                                End If

                                key = TryCast(ReadAMF3Data(AMF3SerializationDefine.String), String)
                            End While
                        End If
                    End If
                Else
                    'U290-ref
                    Return ReadAMF3ObjectReference(handle)
                End If

                Exit Select
            Case AMF3SerializationDefine.XML
                'xml
                Dim handle As Integer = ReadAMF3IntegerData()
                Dim inline = (handle And 1) <> 0
                handle = handle >> 1
                Dim xml = String.Empty

                If inline Then
                    If handle > 0 Then xml = ReadUTFBytes(handle) 'length
                    Dim xmlDocument As XmlDocument = New XmlDocument()
                    If Not Equals(xml, Nothing) AndAlso Not Equals(xml, String.Empty) Then xmlDocument.LoadXml(xml)
                    AddAMF3ObjectReference(xmlDocument)
                    returnResult = xmlDocument
                Else
                    Return TryCast(ReadAMF3ObjectReference(handle), XmlDocument)
                End If

                Exit Select
            Case AMF3SerializationDefine.ByteArray
                'bytearray
                Dim handle As Integer = ReadAMF3IntegerData()
                Dim inline = (handle And 1) <> 0
                handle = handle >> 1

                If inline Then
                    Dim length = handle
                    Dim buffer = br.ReadBytes(length)
                    Dim ba As ByteArray = New ByteArray(buffer)
                    AddAMF3ObjectReference(ba)
                    returnResult = ba
                Else
                    Return TryCast(ReadAMF3ObjectReference(handle), ByteArray)
                End If

                Exit Select
            Case AMF3SerializationDefine.VectorInt
                'vector.<int>
                Dim handle As Integer = ReadAMF3IntegerData()
                Dim inline = (handle And 1) <> 0
                handle = handle >> 1

                If inline Then
                    Dim list As List(Of Integer) = New List(Of Integer)(handle)
                    AddAMF3ObjectReference(list)
                    Dim fixed As Integer = ReadAMF3IntegerData()

                    For i = 0 To handle - 1
                        Dim buffer = br.ReadBytes(4)
                        list.Add(buffer(0) << 24 Or buffer(1) << 16 Or buffer(2) << 8 Or buffer(3))
                    Next

                    returnResult = (If(fixed = 1, TryCast(list.AsReadOnly(), IList(Of Integer)), list))
                Else
                    Return TryCast(ReadAMF3ObjectReference(handle), List(Of Integer))
                End If

                Exit Select
            Case AMF3SerializationDefine.VectorUint
                'vector.<uint>
                Dim handle As Integer = ReadAMF3IntegerData()
                Dim inline = (handle And 1) <> 0
                handle = handle >> 1

                If inline Then
                    Dim list As List(Of UInteger) = New List(Of UInteger)(handle)
                    AddAMF3ObjectReference(list)
                    Dim fixed As Integer = ReadAMF3IntegerData()

                    For i = 0 To handle - 1
                        'todo
                        list.Add(CUInt(ReadInt()))
                    Next

                    returnResult = (If(fixed = 1, TryCast(list.AsReadOnly(), IList(Of UInteger)), list))
                Else
                    Return TryCast(ReadAMF3ObjectReference(handle), List(Of UInteger))
                End If

                Exit Select
            Case AMF3SerializationDefine.VectorNumber
                'vector.<doubel>
                Dim handle As Integer = ReadAMF3IntegerData()
                Dim inline = (handle And 1) <> 0
                handle = handle >> 1

                If inline Then
                    Dim list As List(Of Double) = New List(Of Double)(handle)
                    AddAMF3ObjectReference(list)
                    Dim fixed As Integer = ReadAMF3IntegerData()

                    For i = 0 To handle - 1
                        list.Add(ReadDouble)
                    Next

                    returnResult = (If(fixed = 1, TryCast(list.AsReadOnly(), IList(Of Double)), list))
                Else
                    Return TryCast(ReadAMF3ObjectReference(handle), List(Of Double))
                End If

                Exit Select
            Case AMF3SerializationDefine.VectorObject
                'vector.<object>
                Dim handle As Integer = ReadAMF3IntegerData()
                Dim inline = (handle And 1) <> 0
                handle = handle >> 1

                If inline Then
                    Dim fixed As Integer = ReadAMF3IntegerData()
                    Dim typeIdentifier As String = TryCast(ReadAMF3Data(AMF3SerializationDefine.String), String)
                    Dim list As IList = New List(Of Object)()
                    AddAMF3ObjectReference(list)

                    For i = 0 To handle - 1
                        Dim typeCode As Byte = br.ReadByte()
                        Dim obj = ReadAMF3Data(typeCode)
                        list.Add(obj)
                    Next

                    If fixed = 1 Then Return TryCast(list.GetType().GetMethod("AsReadOnly").Invoke(list, Nothing), IList)
                    returnResult = list
                Else
                    Return TryCast(ReadAMF3ObjectReference(handle), List(Of Object))
                End If

                Exit Select
            Case AMF3SerializationDefine.Dictionary
                'dictionary
                Dim handle As Integer = ReadAMF3IntegerData()
                Dim inline = (handle And 1) <> 0
                handle = handle >> 1

                If inline Then
                    Dim weakKeys As Boolean = ReadBoolean()
                    Dim result As Hashtable = New Hashtable(handle)
                    AddAMF3ObjectReference(result)

                    For i = 0 To handle - 1
                        Dim key As Object = ReadAMF3Data()
                        Dim value As Object = ReadAMF3Data()
                        result(key) = value
                    Next

                    returnResult = result
                Else
                    Return TryCast(ReadAMF3ObjectReference(handle), Hashtable)
                End If

                Exit Select
            Case Else
                Throw New Exception("Not Support Type")
        End Select

        Return returnResult
    End Function
    ''' <summary>
    ''' Đọc một đối tượng từ mảng byte, được mã hóa theo định dạng nối tiếp AMF.
    ''' </summary>
    ''' <returns></returns>
    Public Function ReadObject() As Object
        _objectReferences.Clear()
        _stringReferences.Clear()
        _classDefinitions.Clear()
        Return ReadAMF3Data()
    End Function
#End Region

#Region "Ghi dữ liệu AMF3"
    Private Sub WriteAMF3IntegerData(ByVal value As Integer)
        'Sign contraction - the high order bit of the resulting value must match every bit removed from the number
        'Clear 3 bits 
        value = value And &H1FFFFFFF

        If value < &H80 Then
            inStream.WriteByte(value)
        ElseIf value < &H4000 Then
            inStream.WriteByte(value >> 7 And &H7F Or &H80)
            inStream.WriteByte(value And &H7F)
        Else

            If value < &H200000 Then
                inStream.WriteByte(value >> 14 And &H7F Or &H80)
                inStream.WriteByte(value >> 7 And &H7F Or &H80)
                inStream.WriteByte(value And &H7F)
            Else
                inStream.WriteByte(value >> 22 And &H7F Or &H80)
                inStream.WriteByte(value >> 15 And &H7F Or &H80)
                inStream.WriteByte(value >> 8 And &H7F Or &H80)
                inStream.WriteByte(value And &HFF)
            End If
        End If
    End Sub
    ''' <summary>
    ''' Ghi kiểu dữ liệu AMF3 kiểu 32 bit bào luồng byte.
    ''' </summary>
    ''' <param name="value"></param>
    Public Sub WriteU29Int(value As Integer)
        WriteAMF3IntegerData(value)
    End Sub
    Private Sub writeAMF3Undefined()
        WriteUnsignedByte(AMF3SerializationDefine.Undefined)
    End Sub

    Private Sub WriteAMF3Null()
        WriteUnsignedByte(AMF3SerializationDefine.Null)
    End Sub

    Private Sub writeAMF3Boolean(ByVal __bool As Boolean)
        WriteUnsignedByte(If(__bool, AMF3SerializationDefine.BooleanTrue, AMF3SerializationDefine.BooleanFalse))
    End Sub

    Private Sub writeAMF3Int(ByVal __value As Integer)
        If __value >= -268435456 AndAlso __value <= 268435455 Then 'check valid range for 29bits
            WriteUnsignedByte(AMF3SerializationDefine.Int)
            WriteAMF3IntegerData(__value)
        Else
            WriteAMF3Double(__value)
        End If
    End Sub

    Private Sub WriteAMF3Double(ByVal value As Double)
        WriteUnsignedByte(AMF3SerializationDefine.Number)
        WriteDouble(value)
    End Sub

    Private Sub writeAMF3String(ByVal __object As Object, ByVal Optional withFlag As Boolean = False)
        If withFlag Then
            WriteUnsignedByte(AMF3SerializationDefine.String)
        End If

        If Equals(TryCast(__object, String), String.Empty) Then
            WriteAMF3IntegerData(1)
        Else
            Dim value = CStr(__object)
            Dim __index = _stringReferences.IndexOf(value) ' findObjectIndex(value); 
            If __index = -1 Then
                _stringReferences.Add(value)
                '_objectReferences.Add(value);
                Dim utf8Encoding As UTF8Encoding = New UTF8Encoding()
                Dim byteCount = utf8Encoding.GetByteCount(value)
                Dim handle = byteCount
                handle = handle << 1
                handle = handle Or 1
                WriteAMF3IntegerData(handle)
                Dim buffer = utf8Encoding.GetBytes(value)

                If buffer.Length > 0 Then
                    bw.Write(buffer)
                End If
            Else
                Dim handle = __index
                handle = handle << 1
                WriteAMF3IntegerData(handle)
            End If
        End If
    End Sub

    Private Sub writeAMF3XMLDoc(ByVal value As XmlDocument)
        WriteUnsignedByte(AMF3SerializationDefine.XMLDoc)
        Dim xml = String.Empty

        If value.DocumentElement IsNot Nothing AndAlso Not Equals(value.DocumentElement.OuterXml, Nothing) Then
            xml = value.DocumentElement.OuterXml
        End If

        If Equals(xml, String.Empty) Then
            WriteAMF3IntegerData(1)
        Else
            Dim __index = findObjectIndex(value)

            If __index = -1 Then
                _objectReferences.Add(value)
                Dim utf8Encoding As UTF8Encoding = New UTF8Encoding()
                Dim byteCount = utf8Encoding.GetByteCount(xml)
                Dim handle = byteCount
                handle = handle << 1
                handle = handle Or 1
                WriteAMF3IntegerData(handle)
                Dim buffer = utf8Encoding.GetBytes(xml)
                If buffer.Length > 0 Then bw.Write(buffer)
            Else
                Dim handle = __index
                handle = handle << 1
                WriteAMF3IntegerData(handle)
            End If
        End If
    End Sub

    Private Sub writeAMF3Date(ByVal value As Date)
        WriteUnsignedByte(AMF3SerializationDefine.Date)
        Dim __index = findObjectIndex(value)

        If __index = -1 Then
            _objectReferences.Add(value)
            Dim handle = 1
            WriteAMF3IntegerData(handle)

            ' Write date (milliseconds from 1970).
            Dim timeStart As Date = New DateTime(1970, 1, 1, 0, 0, 0)

            Select Case f_TimezoneCompensation
                Case TimezoneCompensation.IgnoreUTCKind
                    'Do not convert to UTC, consider we have it in universal time
                    Exit Select
                Case Else
                    value = value.ToUniversalTime()
                    Exit Select
            End Select

            Dim span = value.Subtract(timeStart)
            WriteDouble(span.TotalMilliseconds)
        Else
            Dim handle = __index
            handle = handle << 1
            WriteAMF3IntegerData(handle)
        End If
    End Sub

    Private Sub writeAMF3Array(ByVal value As ArrayList)
        WriteUnsignedByte(AMF3SerializationDefine.Array)
        Dim __index = findObjectIndex(value)

        If __index = -1 Then
            _objectReferences.Add(value)
            Dim handle = value.Count
            handle = handle << 1
            handle = handle Or 1
            WriteAMF3IntegerData(handle)
            writeAMF3String(String.Empty) 'hash name
            For i = 0 To value.Count - 1
                WriteObject(value(i), True)
            Next
        Else
            Dim handle = __index
            handle = handle << 1
            WriteAMF3IntegerData(handle)
        End If
    End Sub

    Private Sub writeAMF3OrginObject(ByVal value As IASObjectDefinition)
        WriteUnsignedByte(AMF3SerializationDefine.Object)
        Dim __index = findObjectIndex(value)

        If __index = -1 Then
            _objectReferences.Add(value)
            __index = -1

            For i = 0 To _classDefinitions.Count - 1

                If Equals(_classDefinitions(i).ClassName, value.classDefinition.ClassName) Then
                    __index = i
                    Exit For
                End If
            Next

            Dim handle As Integer

            If __index = -1 Then

                If value.classDefinition.IsExternalizable Then
                    'U29O-traits-ext
                    handle = &H7
                Else
                    'U29O-traits
                    handle = value.classDefinition.Members.Length
                    handle = handle << 4

                    If value.classDefinition.IsDynamic Then
                        handle = handle Or &HB
                    Else
                        handle = handle Or &H3
                    End If
                End If

                WriteAMF3IntegerData(handle)
                writeAMF3String(value.classDefinition.ClassName)
            Else
                'U29O-traits-ref
                handle = __index
                handle = handle << 2
                handle = handle Or 1
                WriteAMF3IntegerData(handle)
            End If



            'WriteAMF3IntegerData(true ? 1 : 0);
            If TypeOf value Is IDynamic Then

                For Each entry In TryCast(value, IDynamic).dynamicRoot
                    writeAMF3String(entry.Key)
                    WriteObject(entry.Value, True)
                Next
            Else
                Dim typeValue As Type = value.GetType()

                For Each member In value.classDefinition.Members
                    writeAMF3String(member.Name)
                    Dim feildInfo = typeValue.GetField(member.Name)

                    If feildInfo IsNot Nothing Then
                        WriteObject(feildInfo.GetValue(value), True)
                    Else
                        Dim propertyInfo = typeValue.GetProperty(member.Name)

                        If propertyInfo IsNot Nothing Then
                            WriteObject(propertyInfo.GetValue(value), True)
                        End If
                    End If
                Next
            End If

            writeAMF3String(String.Empty)
        Else
            'U29O-ref
            Dim handle = __index
            handle = handle << 1
            WriteAMF3IntegerData(handle)
        End If
    End Sub

    Private Sub writeAMF3ByteArray(ByVal byteArray As ByteArray)
        _objectReferences.Add(byteArray)
        WriteUnsignedByte(AMF3SerializationDefine.ByteArray)
        Dim handle As Integer = byteArray.Length
        handle = handle << 1
        handle = handle Or 1
        WriteAMF3IntegerData(handle)
        Dim buffer As Byte() = byteArray.ToArray()

        If buffer IsNot Nothing Then
            inStream.Write(buffer, 0, buffer.Length)
        End If
    End Sub

    Private Sub writeAMF3XML(ByVal value As XmlDocument)
        WriteUnsignedByte(AMF3SerializationDefine.XML)
        Dim xml = String.Empty

        If value.DocumentElement IsNot Nothing AndAlso Not Equals(value.DocumentElement.OuterXml, Nothing) Then
            xml = value.DocumentElement.OuterXml
        End If

        If Equals(xml, String.Empty) Then
            WriteAMF3IntegerData(1)
        Else
            Dim __index = findObjectIndex(value)

            If __index = -1 Then
                _objectReferences.Add(value)
                Dim utf8Encoding As UTF8Encoding = New UTF8Encoding()
                Dim byteCount = utf8Encoding.GetByteCount(xml)
                Dim handle = byteCount
                handle = handle << 1
                handle = handle Or 1
                WriteAMF3IntegerData(handle)
                Dim buffer = utf8Encoding.GetBytes(xml)
                If buffer.Length > 0 Then bw.Write(buffer)
            Else
                Dim handle = __index
                handle = handle << 1
                WriteAMF3IntegerData(handle)
            End If
        End If
    End Sub

    Private Sub writeAMF3VectorInt(ByVal value As IList(Of Integer))
        WriteUnsignedByte(AMF3SerializationDefine.VectorInt)
        Dim __index = findObjectIndex(value)

        If __index = -1 Then
            _objectReferences.Add(value)
            Dim handle = value.Count
            handle = handle << 1
            handle = handle Or 1
            WriteAMF3IntegerData(handle)
            WriteAMF3IntegerData(If(value.IsReadOnly, 1, 0))

            For i = 0 To value.Count - 1
                Dim bytes = BitConverter.GetBytes(value(i))
                WriteBytesEndian(bytes)
            Next
        Else
            Dim handle = __index
            handle = handle << 1
            WriteAMF3IntegerData(handle)
        End If
    End Sub

    Private Sub writeAMF3VectorUint(ByVal value As IList(Of UInteger))
        WriteUnsignedByte(AMF3SerializationDefine.VectorUint)
        Dim __index = findObjectIndex(value)

        If __index = -1 Then
            _objectReferences.Add(value)
            Dim handle = value.Count
            handle = handle << 1
            handle = handle Or 1
            WriteAMF3IntegerData(handle)
            WriteAMF3IntegerData(If(value.IsReadOnly, 1, 0))

            For i = 0 To value.Count - 1
                Dim bytes = BitConverter.GetBytes(value(i))
                WriteBytesEndian(bytes)
            Next
        Else
            Dim handle = __index
            handle = handle << 1
            WriteAMF3IntegerData(handle)
        End If
    End Sub

    Private Sub writeAMF3VectorNumber(ByVal value As IList(Of Double))
        WriteUnsignedByte(AMF3SerializationDefine.VectorNumber)
        Dim __index = findObjectIndex(value)

        If __index = -1 Then
            _objectReferences.Add(value)
            Dim handle = value.Count
            handle = handle << 1
            handle = handle Or 1
            WriteAMF3IntegerData(handle)
            WriteAMF3IntegerData(If(value.IsReadOnly, 1, 0))

            For i = 0 To value.Count - 1
                WriteDouble(value(i))
            Next
        Else
            Dim handle = __index
            handle = handle << 1
            WriteAMF3IntegerData(handle)
        End If
    End Sub

    Private Sub writeAMF3VectorObject(ByVal value As IList(Of Object))
        WriteUnsignedByte(AMF3SerializationDefine.VectorObject)
        Dim __index = findObjectIndex(value)

        If __index = -1 Then
            _objectReferences.Add(value)
            Dim handle = value.Count
            handle = handle << 1
            handle = handle Or 1
            WriteAMF3IntegerData(handle)
            WriteAMF3IntegerData(If(value.IsReadOnly, 1, 0))
            writeAMF3String("")

            For i = 0 To value.Count - 1
                WriteObject(value(i), True)
            Next
        Else
            Dim handle = __index
            handle = handle << 1
            WriteAMF3IntegerData(handle)
        End If
    End Sub

    Private Sub writeAMF3Dictionary(ByVal value As Hashtable)
        WriteUnsignedByte(AMF3SerializationDefine.Dictionary)
        Dim __index = findObjectIndex(value)

        If __index = -1 Then
            _objectReferences.Add(value)
            Dim handle = value.Count
            handle = handle << 1
            handle = handle Or 1
            WriteAMF3IntegerData(handle)
            WriteAMF3IntegerData(If(False, 1, 0))

            For Each entry As DictionaryEntry In value
                WriteObject(entry.Key, True)
                WriteObject(entry.Value, True)
            Next
        Else
            Dim handle = __index
            handle = handle << 1
            WriteAMF3IntegerData(handle)
        End If
    End Sub
    Private Sub writeAMF3Object(ByVal __object As Object, ByVal Optional __classDefinition As ClassDefinition = Nothing)
        If __object Is Nothing Then
            WriteAMF3Null()
            Return
        End If

        Dim __typecode = AMF3SerializationDefine.Undefined
        Dim itType As Type = __object.GetType()

        While itType IsNot Nothing

            If _typeExchange.ContainsKey(itType) Then
                __typecode = CType(_typeExchange(__object.GetType()), AMF3SerializationDefine)
                Exit While
            Else
                Dim interfaceTypes As Type() = itType.GetInterfaces()
                Dim finded = False

                For Each interfaceType In interfaceTypes

                    If _typeExchange.ContainsKey(interfaceType) Then
                        __typecode = _typeExchange(interfaceType)
                        finded = True
                        Exit For
                    End If
                Next

                If finded Then
                    Exit While
                End If
            End If

            itType = itType.BaseType
        End While

        Select Case __typecode
            Case AMF3SerializationDefine.Undefined
                writeAMF3Undefined()
                Return
            Case AMF3SerializationDefine.BooleanFalse, AMF3SerializationDefine.BooleanTrue                 'Boolean
                Dim value As Boolean = __object
                writeAMF3Boolean(value)
                Return
            Case AMF3SerializationDefine.Int                 'int
                Dim value = Convert.ToInt32(__object)
                writeAMF3Int(value)
                Return
            Case AMF3SerializationDefine.Number                 'Number
                Dim value = Convert.ToDouble(__object)
                WriteAMF3Double(value)
                Return
            Case AMF3SerializationDefine.String                 'String
                Dim value = CStr(__object)
                writeAMF3String(value, True)
                Return
            Case AMF3SerializationDefine.XMLDoc                 'XML
                Dim value = CType(__object, XmlDocument)
                writeAMF3XMLDoc(value)
                Return
            Case AMF3SerializationDefine.Date                 'Date
                Dim value As Date = __object
                writeAMF3Date(value)
                Return
            Case AMF3SerializationDefine.Array                 'Array
                Dim value = CType(__object, ArrayList)
                writeAMF3Array(value)
                Exit Select
            Case AMF3SerializationDefine.Object                'Object
                Dim value = CType(__object, IASObjectDefinition)
                writeAMF3OrginObject(value)
                Exit Select
            Case AMF3SerializationDefine.XML
                Dim value = CType(__object, XmlDocument)
                writeAMF3XML(value)
                Exit Select
            Case AMF3SerializationDefine.ByteArray                'ByteArray
                Dim byteArray = CType(__object, ByteArray)
                writeAMF3ByteArray(byteArray)
                Return
            Case AMF3SerializationDefine.VectorInt                'Vector.<int>
                Dim value = CType(__object, IList(Of Integer))
                writeAMF3VectorInt(value)
                Return
            Case AMF3SerializationDefine.VectorUint                'Vector.<uint>
                Dim value = CType(__object, IList(Of UInteger))
                writeAMF3VectorUint(value)
                Return
            Case AMF3SerializationDefine.VectorNumber                'Vector.<Number>
                Dim value As IList(Of Double)

                If TypeOf __object Is List(Of Single) Then
                    value = New List(Of Double)()

                    For i = 0 To TryCast(__object, IList(Of Single)).Count - 1
                        value.Add(CDbl(TryCast(__object, IList(Of Single))(i)))
                    Next
                Else
                    value = CType(__object, IList(Of Double))
                End If

                writeAMF3VectorNumber(value)
                Return
            Case AMF3SerializationDefine.VectorObject                'Vector.<Object>
                Dim value = CType(__object, IList(Of Object))
                writeAMF3VectorObject(value)
                Return
            Case AMF3SerializationDefine.Dictionary                'Dictionary
                Dim value = CType(__object, Hashtable)
                writeAMF3Dictionary(value)
                Return
            Case Else
                Throw New Exception("Type Not Support")
        End Select
    End Sub
    ''' <summary>
    ''' Ghi một đối tượng vào mảng byte theo định dạng nối tiếp AMF.
    ''' </summary>
    ''' <param name="value"></param>
    ''' <param name="inStruct"></param>
    Public Sub WriteObject(value As Object, ByVal Optional inStruct As Boolean = False)
        If Not inStruct Then
            _objectReferences.Clear()
            _stringReferences.Clear()
            _classDefinitions.Clear()
        End If
        writeAMF3Object(value)
    End Sub
#End Region

#Region "Hỗ trợ IDisposable "
    Public Sub Dispose() Implements IDisposable.Dispose
        inStream.Close()
        inStream.Dispose()
        _objectReferences.Clear()
        _objectReferences = Nothing
        _stringReferences.Clear()
        _stringReferences = Nothing
        _classDefinitions.Clear()
        _classDefinitions = Nothing
        br.Close()
        br.Dispose()
        bw.Close()
        bw.Dispose()
    End Sub
#End Region

End Class

#Region "Interface"
Public Interface IExternalizable
    Sub writeExternal(ByVal output As ByteArray)
    Sub readExternal(ByVal input As ByteArray)
End Interface

Public Interface IDynamic
    Property dynamicRoot As Dictionary(Of String, Object)
End Interface

Public Interface IASObjectDefinition
    Property classDefinition As ClassDefinition
End Interface

#End Region

#Region "Phân lớp bổ sung"
Public Class Undefined
    Implements IComparable
    Public Function CompareTo(ByVal obj As Object) As Integer Implements IComparable.CompareTo
        If TypeOf obj Is Undefined Then
            Return 0
        End If
        Return -1
    End Function
End Class
Public NotInheritable Class ClassMember
    Private _name As String
    Private _bindingFlags As BindingFlags
    Private _memberType As MemberTypes
    Private _customAttributes As Object()

    Friend Sub New(ByVal name As String, ByVal bindingFlags As BindingFlags, ByVal memberType As MemberTypes, ByVal customAttributes As Object())
        _name = name
        _bindingFlags = bindingFlags
        _memberType = memberType
        _customAttributes = customAttributes
    End Sub
    Public ReadOnly Property Name As String
        Get
            Return _name
        End Get
    End Property
    Public ReadOnly Property BindingFlags As BindingFlags
        Get
            Return _bindingFlags
        End Get
    End Property
    Public ReadOnly Property MemberType As MemberTypes
        Get
            Return _memberType
        End Get
    End Property
    Public ReadOnly Property CustomAttributes As Object()
        Get
            Return _customAttributes
        End Get
    End Property
End Class
Public Class ASObject
    Implements IASObjectDefinition, IDynamic

    Private _dynamicRoot As Dictionary(Of String, Object)
    Private _classDefinition As ClassDefinition

    Public Sub New()
        _classDefinition = New ClassDefinition("", New ClassMember(-1) {}, False, True)
        _dynamicRoot = New Dictionary(Of String, Object)()
    End Sub

    Public Property classDefinition As ClassDefinition Implements IASObjectDefinition.classDefinition
        Set(ByVal value As ClassDefinition)
            _classDefinition = value
        End Set
        Get
            Return _classDefinition
        End Get
    End Property

    Public Property dynamicRoot As Dictionary(Of String, Object) Implements IDynamic.dynamicRoot
        Set(ByVal value As Dictionary(Of String, Object))
            _dynamicRoot = value
        End Set
        Get
            Return _dynamicRoot
        End Get
    End Property
End Class
Public NotInheritable Class ClassDefinition
    Private _className As String
    Private _members As ClassMember()
    Private _externalizable As Boolean
    Private _dynamic As Boolean
    Friend Shared EmptyClassMembers = New ClassMember(-1) {}

    Friend Sub New(ByVal className As String, ByVal members As ClassMember(), ByVal externalizable As Boolean, ByVal dynamic As Boolean)
        _className = className
        _members = members
        _externalizable = externalizable
        _dynamic = dynamic
    End Sub

    Public ReadOnly Property ClassName As String
        Get
            Return _className
        End Get
    End Property
    Public ReadOnly Property MemberCount As Integer
        Get
            If _members Is Nothing Then Return 0
            Return _members.Length
        End Get
    End Property
    Public ReadOnly Property Members As ClassMember()
        Get
            Return _members
        End Get
    End Property

    Public ReadOnly Property IsExternalizable As Boolean
        Get
            Return _externalizable
        End Get
    End Property
    Public ReadOnly Property IsDynamic As Boolean
        Get
            Return _dynamic
        End Get
    End Property

    Public ReadOnly Property IsTypedObject As Boolean
        Get
            Return Not Equals(_className, Nothing) AndAlso Not Equals(_className, String.Empty)
        End Get
    End Property

    Private Shared _registerClassAliases As Dictionary(Of String, Type) = New Dictionary(Of String, Type)()

    Public Shared Sub registerClassAlias(ByVal aliasName As String, ByVal classObject As Type)
        _registerClassAliases(aliasName) = classObject
    End Sub

    Public Function getClass() As Object
        Dim type As Type = Nothing

        If Not String.IsNullOrEmpty(_className) Then

            If _registerClassAliases.Keys.Contains(_className) Then
                type = _registerClassAliases(_className)
            Else
                Dim ass As Assembly() = AppDomain.CurrentDomain.GetAssemblies()

                For Each assembly In ass
                    Dim assemblyClassType = assembly.GetType(_className)

                    If assemblyClassType IsNot Nothing Then
                        type = assemblyClassType
                        Exit For
                    End If
                Next
            End If
        End If

        If type IsNot Nothing Then
            Return Activator.CreateInstance(type)
        Else
            Return New ASObject()
        End If
    End Function
End Class

#End Region

